using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Threading.Tasks;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the deck-primer MVC workflow pages.
/// </summary>
public sealed class DeckPrimerController : DeckToolControllerBase
{
    private const string CorruptedZipMessage = "The uploaded zip contains an incomplete response payload. Re-export from the originating session or paste a fresh response.";
    private readonly IDeckPrimerPacketService _deckPrimerPacketService;
    private readonly PacketSessionCache _packetCache;
    private readonly ILogger<DeckPrimerController> _logger;

    /// <summary>
    /// Creates the deck-primer controller.
    /// </summary>
    public DeckPrimerController(
        IDeckPrimerPacketService deckPrimerPacketService,
        PacketSessionCache packetCache,
        ILogger<DeckPrimerController> logger)
    {
        ArgumentNullException.ThrowIfNull(deckPrimerPacketService);
        ArgumentNullException.ThrowIfNull(packetCache);
        ArgumentNullException.ThrowIfNull(logger);

        _deckPrimerPacketService = deckPrimerPacketService;
        _packetCache = packetCache;
        _logger = logger;
    }

    /// <summary>
    /// Renders the staged deck-primer workflow.
    /// </summary>
    [HttpGet("/deck-primer")]
    [FeatureFlagGate("tool.deck-primer.enabled")]
    public IActionResult DeckPrimer()
    {
        var defaultBracket = CommanderBracketCatalog.Find("Optimized")?.Value ?? string.Empty;
        return View("DeckPrimer", new DeckPrimerViewModel
        {
            ActiveTab = DeckPageTab.DeckPrimer,
            Request = new DeckPrimerRequest
            {
                TargetCommanderBracket = defaultBracket,
            },
        });
    }

    /// <summary>
    /// Processes a deck-primer workflow postback and regenerates the selected AI prompt.
    /// </summary>
    /// <param name="request">Current primer workflow request.</param>
    [HttpPost("/deck-primer")]
    [FeatureFlagGate("tool.deck-primer.enabled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeckPrimer(DeckPrimerRequest request)
    {
        request ??= new DeckPrimerRequest();

        try
        {
            var result = await _deckPrimerPacketService.BuildAsync(request, HttpContext.RequestAborted);
            var selectedPlatformKey = AiPlatform.Normalize(request.TargetAiPlatform).Key;
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = request,
                InputSummary = result.InputSummary,
                SuggestedChatTitle = result.SuggestedChatTitle,
                PrimerPromptText = result.PromptTextsByPlatform.GetValueOrDefault(selectedPlatformKey) ?? string.Empty,
                TimingSummary = result.TimingSummary,
                ImportWarning = result.ImportWarning,
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Deck-primer packet generation failed validation.");
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Deck-primer packet generation hit an upstream dependency.");
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = request,
                ErrorMessage = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Deck-primer packet generation failed unexpectedly.");
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = request,
                ErrorMessage = "Deck primer generation failed unexpectedly. Try again shortly.",
            });
        }
    }

    /// <summary>
    /// Builds and downloads a deck-primer packet zip for the current workflow request.
    /// </summary>
    /// <param name="request">Current deck-primer workflow request.</param>
    [HttpPost("/deck-primer/download")]
    [FeatureFlagGate("tool.deck-primer.enabled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeckPrimerDownload(DeckPrimerRequest request)
    {
        request ??= new DeckPrimerRequest();

        try
        {
            var cacheKey = await _deckPrimerPacketService.TryComputeCacheKeyAsync(request, HttpContext.RequestAborted);
            DeckPrimerPacketResult? result = null;
            if (cacheKey is not null
                && _packetCache.TryGet<DeckPrimerPacketResult>(cacheKey, out var cachedResult)
                && cachedResult is not null)
            {
                result = cachedResult;
            }

            result ??= await _deckPrimerPacketService.BuildAsync(request, HttpContext.RequestAborted);
            var bytes = PacketArtifactStore.BuildPrimerZip(
                request,
                result.InputSummary,
                result.RequestContextText,
                result.PromptTextsByPlatform.GetValueOrDefault("ChatGPT"),
                result.PromptTextsByPlatform.GetValueOrDefault("Claude"),
                result.PromptTextsByPlatform.GetValueOrDefault("Gemini"),
                result.DecklistText,
                PacketArtifactStore.OriginalDeckTextOrNull(request.DeckSource));
            var fileName = PacketArtifactStore.SuggestPrimerZipFileName(result.ResolvedCommanderName, request.TargetAiPlatform);
            Response.Headers["X-DeckFlow-Filename"] = fileName;
            return File(bytes, "application/zip", fileName);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Deck-primer packet download failed validation.");
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Deck-primer packet download hit an upstream dependency.");
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = request,
                ErrorMessage = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Deck-primer packet download failed unexpectedly.");
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = request,
                ErrorMessage = "Deck primer download failed unexpectedly. Try again shortly.",
            });
        }
    }

    /// <summary>
    /// Restores a deck-primer workflow from a previously downloaded packet zip.
    /// </summary>
    /// <param name="zipFile">Packet zip uploaded from a prior deck-primer session.</param>
    [HttpPost("/deck-primer/upload")]
    [FeatureFlagGate("tool.deck-primer.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> DeckPrimerUpload(IFormFile zipFile)
    {
        if (zipFile is null || zipFile.Length == 0)
        {
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = new DeckPrimerRequest
                {
                    TargetCommanderBracket = CommanderBracketCatalog.Find("Optimized")?.Value ?? string.Empty,
                },
                ErrorMessage = "Choose a .zip file produced by Download to import."
            });
        }

        if (!string.Equals(Path.GetExtension(zipFile.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = new DeckPrimerRequest
                {
                    TargetCommanderBracket = CommanderBracketCatalog.Find("Optimized")?.Value ?? string.Empty,
                },
                ErrorMessage = "Only .zip files produced by Download are accepted."
            });
        }

        var request = new DeckPrimerRequest();
        try
        {
            using var stream = zipFile.OpenReadStream();
            PacketArtifactStore.LoadPrimerFromZip(stream, request);
            var result = await _deckPrimerPacketService.BuildAsync(request, HttpContext.RequestAborted);
            var selectedPlatformKey = AiPlatform.Normalize(request.TargetAiPlatform).Key;
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = request,
                InputSummary = result.InputSummary,
                SuggestedChatTitle = result.SuggestedChatTitle,
                PrimerPromptText = result.PromptTextsByPlatform.GetValueOrDefault(selectedPlatformKey) ?? string.Empty,
                TimingSummary = result.TimingSummary,
                ImportWarning = result.ImportWarning,
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Deck-primer packet upload failed validation.");
            var errorMessage = exception.Message == ResponseParsers.TruncatedResponseMessage ? CorruptedZipMessage : exception.Message;
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = new DeckPrimerRequest
                {
                    TargetCommanderBracket = CommanderBracketCatalog.Find("Optimized")?.Value ?? string.Empty,
                },
                ErrorMessage = errorMessage,
            });
        }
        catch (InvalidDataException)
        {
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = new DeckPrimerRequest
                {
                    TargetCommanderBracket = CommanderBracketCatalog.Find("Optimized")?.Value ?? string.Empty,
                },
                ErrorMessage = "The uploaded file is not a valid .zip archive."
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Deck-primer packet upload hit an upstream dependency.");
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = request,
                ErrorMessage = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Deck-primer packet upload failed unexpectedly.");
            return View("DeckPrimer", new DeckPrimerViewModel
            {
                ActiveTab = DeckPageTab.DeckPrimer,
                Request = new DeckPrimerRequest
                {
                    TargetCommanderBracket = CommanderBracketCatalog.Find("Optimized")?.Value ?? string.Empty,
                },
                ErrorMessage = "Deck primer upload failed unexpectedly. Try again shortly.",
            });
        }
    }
}
