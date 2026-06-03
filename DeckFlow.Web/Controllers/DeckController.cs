using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using DeckFlow.Core.Diffing;
using DeckFlow.Core.Exporting;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the MVC pages for deck compare, category suggestion, lookup, and ChatGPT-assisted workflows.
/// </summary>
public sealed class DeckController : Controller
{
    private const string CorruptedZipMessage = "The uploaded zip contains an incomplete response payload. Re-export from the originating session or paste a fresh response.";
    private static readonly TimeSpan SuggestionTimeout = TimeSpan.FromSeconds(20);
    private readonly IDeckSyncService _deckSyncService;
    private readonly IDeckConvertService _deckConvertService;
    private readonly ICardSearchService _cardSearchService;
    private readonly ICardLookupService _cardLookupService;
    private readonly IMechanicLookupService _mechanicLookupService;
    private readonly ICategorySuggestionService _categorySuggestionService;
    private readonly IDeckAnalysisPacketService _deckAnalysisPacketService;
    private readonly IDeckComparisonService _deckComparisonService;
    private readonly IMetaGapService _metaGapService;
    private readonly PacketSessionCache _packetCache;
    private readonly IScryfallSetService _scryfallSetService;
    private readonly ILogger<DeckController> _logger;

    /// <summary>
    /// Creates the main deck-tools controller.
    /// </summary>
    public DeckController(
        IDeckSyncService deckSyncService,
        IDeckConvertService deckConvertService,
        ICardSearchService cardSearchService,
        ICardLookupService cardLookupService,
        IMechanicLookupService mechanicLookupService,
        ICategorySuggestionService categorySuggestionService,
        IDeckAnalysisPacketService deckAnalysisPacketService,
        IDeckComparisonService deckComparisonService,
        IMetaGapService metaGapService,
        PacketSessionCache packetCache,
        IScryfallSetService scryfallSetService,
        ILogger<DeckController> logger)
    {
        _deckSyncService = deckSyncService;
        _deckConvertService = deckConvertService;
        _cardSearchService = cardSearchService;
        _cardLookupService = cardLookupService;
        _mechanicLookupService = mechanicLookupService;
        _categorySuggestionService = categorySuggestionService;
        _deckAnalysisPacketService = deckAnalysisPacketService;
        _deckComparisonService = deckComparisonService;
        _metaGapService = metaGapService;
        _packetCache = packetCache;
        _scryfallSetService = scryfallSetService;
        _logger = logger;
    }

    /// <summary>
    /// Renders the landing hub listing every tool in the app.
    /// </summary>
    [HttpGet("/")]
    public IActionResult Home()
    {
        return View("Home", DeckPageTab.Home);
    }

    /// <summary>Gets the branded error page shown when an unhandled exception occurs.</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public IActionResult Error()
    {
        return View("Error");
    }

    /// <summary>
    /// Renders the deck sync view with default tab state.
    /// </summary>
    [HttpGet("/sync")]
    public IActionResult Index()
    {
        return View("DeckSync", new DeckDiffViewModel
        {
            ActiveTab = DeckPageTab.Sync,
        });
    }

    /// <summary>
    /// Renders the suggest categories tab with fresh state.
    /// </summary>
    [HttpGet("/suggest-categories")]
    [FeatureFlagGate("feature.categories.enabled",
        Title = "Category suggestions temporarily unavailable",
        Message = "Category Suggestions is offline for maintenance. Category Reference remains available.",
        PrimaryActionLabel = "Open Category Reference",
        PrimaryActionUrl = "/commander-categories")]
    public IActionResult SuggestCategories()
    {
        return View("SuggestCategories", new DeckDiffViewModel
        {
            ActiveTab = DeckPageTab.SuggestCategories,
            SuggestionRequest = new CategorySuggestionRequest(),
        });
    }

    /// <summary>
    /// Renders the card lookup page.
    /// </summary>
    [HttpGet("/card-lookup")]
    public IActionResult CardLookup()
    {
        return View("CardLookup", new CardLookupViewModel
        {
            ActiveTab = DeckPageTab.CardLookup,
        });
    }

    /// <summary>
    /// Renders the mechanic rules lookup page.
    /// </summary>
    [HttpGet("/mechanic-lookup")]
    public IActionResult MechanicLookup()
    {
        return View("MechanicLookup", new MechanicLookupViewModel
        {
            ActiveTab = DeckPageTab.MechanicLookup,
        });
    }

    /// <summary>
    /// Renders the "Ask a Judge" page that primarily links to the live MTG judge chat
    /// and offers a secondary ChatGPT prompt generator. Optionally pre-fills a card name
    /// passed in via query string from a Card Lookup deep link.
    /// </summary>
    /// <param name="card">Optional card name to pre-populate the question form.</param>
    [HttpGet("/judge-questions")]
    public IActionResult JudgeQuestions(string? card)
    {
        return View("JudgeQuestions", new JudgeQuestionViewModel
        {
            ActiveTab = DeckPageTab.JudgeQuestions,
            PrefilledCardName = string.IsNullOrWhiteSpace(card) ? null : card.Trim(),
        });
    }

    /// <summary>
    /// Renders the staged deck-analysis packet workflow. Set options load asynchronously on the client.
    /// </summary>
    [HttpGet("/deck-analysis")]
    public IActionResult DeckAnalysis()
    {
        return View("DeckAnalysis", new DeckAnalysisViewModel
        {
            ActiveTab = DeckPageTab.DeckAnalysis,
            Request = new DeckAnalysisRequest(),
        });
    }

    /// <summary>
    /// Renders the staged deck-comparison workflow.
    /// </summary>
    [HttpGet("/deck-comparison")]
    public IActionResult DeckComparison()
    {
        return View("DeckComparison", new DeckComparisonViewModel
        {
            ActiveTab = DeckPageTab.DeckComparison,
            Request = new DeckComparisonRequest(),
        });
    }

    /// <summary>
    /// Renders the staged cEDH meta-gap workflow.
    /// </summary>
    [HttpGet("/cedh-meta-gap")]
    public IActionResult CedhMetaGap()
    {
        return View("CedhMetaGap", new MetaGapViewModel
        {
            ActiveTab = DeckPageTab.CedhMetaGap,
            Request = new MetaGapRequest(),
        });
    }

    /// <summary>
    /// Returns the Scryfall set catalog as JSON for client-side async loading.
    /// </summary>
    [HttpGet("/api/set-options")]
    public async Task<IActionResult> GetSetOptions()
    {
        var sets = await TryGetSetOptionsAsync();
        return Json(sets.Select(s => new { s.Code, s.DisplayLabel, s.SetType }));
    }

    /// <summary>
    /// Renders the deck format conversion page.
    /// </summary>
    [HttpGet("/convert")]
    public IActionResult Convert()
    {
        return View("DeckConvert", new DeckConvertViewModel());
    }

    /// <summary>
    /// Converts a single deck from one platform format to another.
    /// </summary>
    /// <param name="request">Deck convert request.</param>
    [HttpPost("/convert")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(DeckConvertRequest request)
    {
        request ??= new DeckConvertRequest();
        var hasInput = request.InputSource == DeckInputSource.PublicUrl
            ? !string.IsNullOrWhiteSpace(request.DeckUrl)
            : !string.IsNullOrWhiteSpace(request.DeckText);

        if (!hasInput)
        {
            return View("DeckConvert", new DeckConvertViewModel
            {
                Request = request,
                ErrorMessage = "Paste a deck export or enter a public URL before converting.",
            });
        }

        try
        {
            var result = await _deckConvertService.ConvertAsync(request, HttpContext.RequestAborted);
            return View("DeckConvert", new DeckConvertViewModel
            {
                Request = request,
                ConvertedText = result.ConvertedText,
                MissingCommander = result.CommanderMissing,
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            _logger.LogInformation(exception, "Deck conversion failed.");
            return View("DeckConvert", new DeckConvertViewModel
            {
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
    }
    /// <summary>
    /// Returns commander-eligible card name suggestions for the deck convert form typeahead.
    /// </summary>
    /// <param name="q">Partial commander name.</param>
    [HttpGet("/convert/commander-search")]
    public async Task<IActionResult> ConvertCommanderSearch(string q)
    {
        try
        {
            var names = await _cardSearchService.SearchCommandersAsync(q ?? string.Empty, HttpContext.RequestAborted);
            return Json(names);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Commander search autocomplete failed for query {Query}.", q);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception)
            });
        }
    }

    /// <summary>
    /// Provides card name suggestions for the suggest categories form.
    /// </summary>
    /// <param name="query">Partial card name.</param>
    [HttpGet("/suggest-categories/card-search")]
    public async Task<IActionResult> CardSearch(string query)
    {
        try
        {
            var names = await _cardSearchService.SearchAsync(query ?? string.Empty, HttpContext.RequestAborted);
            return Json(names);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Card search autocomplete failed for query {Query}.", query);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception)
            });
        }
    }

    /// <summary>
    /// Handles the deck sync POST to generate a diff report.
    /// </summary>
    /// <param name="request">Deck diff request data.</param>
    [HttpPost("/sync")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(DeckDiffRequest request)
    {
        return await RenderDiffAsync(request);
    }

    /// <summary>
    /// Verifies a pasted card list and returns the output as a downloadable text file.
    /// </summary>
    /// <param name="request">Card verification request.</param>
    [HttpPost("/card-lookup/download")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadCardLookup(CardLookupRequest request)
    {
        return await DownloadCardLookupAsync(request, CardLookupDownloadFormat.Text);
    }

    /// <summary>
    /// Verifies a pasted card list and returns the output as a downloadable JSON file.
    /// </summary>
    /// <param name="request">Card verification request.</param>
    [HttpPost("/card-lookup/download-json")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadCardLookupJson(CardLookupRequest request)
    {
        return await DownloadCardLookupAsync(request, CardLookupDownloadFormat.Json);
    }

    /// <summary>
    /// Looks up a single card by name and returns the formatted Oracle/rulings text as JSON.
    /// </summary>
    /// <param name="name">Card name.</param>
    [HttpGet("/card-lookup/single")]
    public async Task<IActionResult> SingleCardLookup(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "A card name is required." });
        }

        try
        {
            var result = await _cardLookupService.LookupSingleAsync(name, HttpContext.RequestAborted);
            if (result is null || string.IsNullOrEmpty(result.VerifiedText))
            {
                return NotFound(new { message = $"Scryfall could not find \"{name}\"." });
            }

            var mechanicRules = new List<object>();
            foreach (var mechanic in result.Mechanics)
            {
                MechanicLookupResult mechanicResult;
                try
                {
                    mechanicResult = await _mechanicLookupService.LookupAsync(mechanic, HttpContext.RequestAborted);
                }
                catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
                {
                    _logger.LogInformation(exception, "Keyword rules lookup failed for {Mechanic} during single-card lookup.", mechanic);
                    continue;
                }

                if (!mechanicResult.Found || string.IsNullOrWhiteSpace(mechanicResult.RulesText))
                {
                    continue;
                }

                mechanicRules.Add(new
                {
                    mechanicName = mechanicResult.MechanicName ?? mechanic,
                    ruleReference = mechanicResult.RuleReference,
                    matchType = mechanicResult.MatchType,
                    rulesText = mechanicResult.RulesText,
                    summaryText = mechanicResult.SummaryText
                });
            }

            return Json(new
            {
                cardName = result.CardName,
                verifiedText = result.VerifiedText,
                mechanicRules
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Single-card lookup failed for {CardName}.", name);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
    }

    /// <summary>
    /// Looks up official rules text for a mechanic or rules term.
    /// </summary>
    /// <param name="request">Mechanic lookup request.</param>
    [HttpPost("/mechanic-lookup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MechanicLookup(MechanicLookupRequest request)
    {
        request ??= new MechanicLookupRequest();
        if (string.IsNullOrWhiteSpace(request.MechanicName))
        {
            return View("MechanicLookup", new MechanicLookupViewModel
            {
                ActiveTab = DeckPageTab.MechanicLookup,
                Request = request,
                ErrorMessage = "A mechanic name is required.",
            });
        }

        try
        {
            var result = await _mechanicLookupService.LookupAsync(request.MechanicName, HttpContext.RequestAborted);
            return View("MechanicLookup", new MechanicLookupViewModel
            {
                ActiveTab = DeckPageTab.MechanicLookup,
                Request = request,
                MechanicName = result.MechanicName,
                RuleReference = result.RuleReference,
                MatchType = result.MatchType,
                RulesText = result.RulesText,
                SummaryText = result.SummaryText,
                RulesTextUrl = result.RulesTextUrl,
                NotFoundMessage = result.Found
                    ? null
                    : $"No official rules entry was found for {request.MechanicName.Trim()} in the current Wizards Comprehensive Rules text.",
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Mechanic lookup request failed validation.");
            return View("MechanicLookup", new MechanicLookupViewModel
            {
                ActiveTab = DeckPageTab.MechanicLookup,
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Mechanic lookup failed.");
            return View("MechanicLookup", new MechanicLookupViewModel
            {
                ActiveTab = DeckPageTab.MechanicLookup,
                Request = request,
                ErrorMessage = "Wizards of the Coast rules lookup is currently unavailable. Try again shortly.",
            });
        }
    }

    /// <summary>
    /// Processes a ChatGPT workflow postback and regenerates the next packet outputs.
    /// </summary>
    /// <param name="request">Current workflow request.</param>
    [HttpPost("/deck-analysis")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeckAnalysis(DeckAnalysisRequest request)
    {
        request ??= new DeckAnalysisRequest();

        try
        {
            var result = await _deckAnalysisPacketService.BuildAsync(request, HttpContext.RequestAborted);
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                Request = request,
                InputSummary = result.InputSummary,
                SuggestedChatTitle = result.SuggestedChatTitle,
                ReferenceText = result.ReferenceText,
                AnalysisPromptText = result.AnalysisPromptText,
                DeckProfileSchemaJson = result.DeckProfileSchemaJson,
                SetUpgradePromptText = result.SetUpgradePromptText,
                TimingSummary = result.TimingSummary,
                AnalysisResponse = result.AnalysisResponse,
                SetUpgradeResponse = result.SetUpgradeResponse,
                ImportWarning = result.ImportWarning,
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Deck-analysis packet generation failed validation.");
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Deck-analysis packet generation hit an upstream dependency.");
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                Request = request,
                ErrorMessage = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
    }

    /// <summary>
    /// Builds and downloads a deck-analysis packet zip for the current workflow request.
    /// </summary>
    /// <param name="request">Current deck-analysis workflow request.</param>
    [HttpPost("/deck-analysis/download")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeckAnalysisDownload(DeckAnalysisRequest request)
    {
        request ??= new DeckAnalysisRequest();

        try
        {
            // Audit F-02: intentional asymmetry - DeckAnalysis has no response-JSON short-circuit tier (deck-analysis lacks a paste-response-only re-download path; Comparison + CedhMetaGap implement the full 3-tier order per Phase 999.3 D-10).
            // Phase 999.3 D-10: service-owned cache-key parity before BuildAsync; misses fall through silently.
            // Misses intentionally pay one extra deck load here before BuildAsync (D-11 accepted trade-off).
            var cacheKey = await _deckAnalysisPacketService.TryComputeCacheKeyAsync(request, HttpContext.RequestAborted);
            if (cacheKey is not null
                && _packetCache.TryGet<DeckAnalysisPacketResult>(cacheKey, out var cachedResult)
                && cachedResult is not null)
            {
                var cachedCommanderName = !string.IsNullOrWhiteSpace(cachedResult.ResolvedCommanderName)
                    ? cachedResult.ResolvedCommanderName
                    : cachedResult.AnalysisResponse?.Commander ?? request.DeckName;
                var cachedRequestContextText = cachedResult.RequestContextText ?? DeckAnalysisPacketService.BuildRequestContextText(request, cachedCommanderName);
                var cachedBytes = PacketArtifactStore.BuildZip(
                    request,
                    cachedCommanderName,
                    cachedResult.InputSummary,
                    cachedRequestContextText,
                    cachedResult.ReferenceText,
                    cachedResult.AnalysisPromptText,
                    cachedResult.DeckProfileSchemaJson,
                    cachedResult.SetUpgradePromptText,
                    canonicalDeckListText: cachedResult.DecklistText,
                    originalDeckText: PacketArtifactStore.OriginalDeckTextOrNull(request.DeckSource));
                var cachedFileName = PacketArtifactStore.SuggestPacketZipFileName(cachedCommanderName, request.TargetAiPlatform);
                Response.Headers["X-DeckFlow-Filename"] = cachedFileName;
                return File(cachedBytes, "application/zip", cachedFileName);
            }

            var result = await _deckAnalysisPacketService.BuildAsync(request, HttpContext.RequestAborted);
            var commanderName = !string.IsNullOrWhiteSpace(result.ResolvedCommanderName)
                ? result.ResolvedCommanderName
                : result.AnalysisResponse?.Commander ?? request.DeckName;
            var requestContextText = result.RequestContextText ?? DeckAnalysisPacketService.BuildRequestContextText(request, commanderName);
            var bytes = PacketArtifactStore.BuildZip(
                request,
                commanderName,
                result.InputSummary,
                requestContextText,
                result.ReferenceText,
                result.AnalysisPromptText,
                result.DeckProfileSchemaJson,
                result.SetUpgradePromptText,
                canonicalDeckListText: result.DecklistText,
                originalDeckText: PacketArtifactStore.OriginalDeckTextOrNull(request.DeckSource));
            var fileName = PacketArtifactStore.SuggestPacketZipFileName(commanderName, request.TargetAiPlatform);
            Response.Headers["X-DeckFlow-Filename"] = fileName;
            return File(bytes, "application/zip", fileName);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Deck-analysis packet download failed validation.");
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Deck-analysis packet download hit an upstream dependency.");
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                Request = request,
                ErrorMessage = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
    }

    /// <summary>
    /// Restores a deck-analysis workflow from a previously downloaded packet zip.
    /// </summary>
    /// <param name="zipFile">Packet zip uploaded from a prior deck-analysis session.</param>
    [HttpPost("/deck-analysis/upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> DeckAnalysisUpload(IFormFile zipFile)
    {
        if (zipFile is null || zipFile.Length == 0)
        {
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                Request = new DeckAnalysisRequest(),
                ErrorMessage = "Choose a .zip file produced by Download to import."
            });
        }

        if (!string.Equals(Path.GetExtension(zipFile.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                Request = new DeckAnalysisRequest(),
                ErrorMessage = "Only .zip files produced by Download are accepted."
            });
        }

        var request = new DeckAnalysisRequest();
        try
        {
            await using var stream = zipFile.OpenReadStream();
            PacketArtifactStore.LoadFromZip(stream, request);
            var result = await _deckAnalysisPacketService.BuildAsync(request, HttpContext.RequestAborted);
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                Request = request,
                InputSummary = result.InputSummary,
                SuggestedChatTitle = result.SuggestedChatTitle,
                ReferenceText = result.ReferenceText,
                AnalysisPromptText = result.AnalysisPromptText,
                DeckProfileSchemaJson = result.DeckProfileSchemaJson,
                SetUpgradePromptText = result.SetUpgradePromptText,
                TimingSummary = result.TimingSummary,
                AnalysisResponse = result.AnalysisResponse,
                SetUpgradeResponse = result.SetUpgradeResponse,
                ImportWarning = result.ImportWarning,
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Deck-analysis packet upload failed validation.");
            string errorMessage = exception.Message == ResponseParsers.TruncatedResponseMessage ? CorruptedZipMessage : exception.Message;
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                Request = new DeckAnalysisRequest(),
                ErrorMessage = errorMessage
            });
        }
        catch (InvalidDataException)
        {
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                Request = new DeckAnalysisRequest(),
                ErrorMessage = "The uploaded file is not a valid .zip archive."
            });
        }
    }

    /// <summary>
    /// Processes the deck-comparison workflow.
    /// </summary>
    /// <param name="request">Current comparison workflow request.</param>
    [HttpPost("/deck-comparison")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeckComparison(DeckComparisonRequest request)
    {
        request ??= new DeckComparisonRequest();
        if (!ModelState.IsValid)
        {
            return View("DeckComparison", new DeckComparisonViewModel
            {
                ActiveTab = DeckPageTab.DeckComparison,
                Request = request,
                ErrorMessage = "The comparison form contains invalid values. Review the highlighted fields and try again."
            });
        }

        try
        {
            var result = await _deckComparisonService.BuildAsync(request, HttpContext.RequestAborted);
            return View("DeckComparison", new DeckComparisonViewModel
            {
                ActiveTab = DeckPageTab.DeckComparison,
                Request = request,
                InputSummary = result.InputSummary,
                DeckAListText = result.DeckAListText,
                DeckBListText = result.DeckBListText,
                DeckAComboText = result.DeckAComboText,
                DeckBComboText = result.DeckBComboText,
                ComparisonContextText = result.ComparisonContextText,
                ComparisonPromptText = result.ComparisonPromptText,
                FollowUpPromptText = result.FollowUpPromptText,
                ComparisonSchemaJson = result.ComparisonSchemaJson,
                ComparisonResponse = result.ComparisonResponse,
                TimingSummary = result.TimingSummary
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Deck-comparison failed validation.");
            return View("DeckComparison", new DeckComparisonViewModel
            {
                ActiveTab = DeckPageTab.DeckComparison,
                Request = request,
                ErrorMessage = exception.Message
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Deck-comparison hit an upstream dependency.");
            var errorMessage = exception.Message.Contains("Deck A", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("Deck B", StringComparison.OrdinalIgnoreCase)
                    ? exception.Message
                    : UpstreamErrorMessageBuilder.BuildScryfallMessage(exception);

            return View("DeckComparison", new DeckComparisonViewModel
            {
                ActiveTab = DeckPageTab.DeckComparison,
                Request = request,
                ErrorMessage = errorMessage
            });
        }
    }

    /// <summary>
    /// Builds and downloads a deck-comparison packet zip for the current workflow request.
    /// </summary>
    /// <param name="request">Current deck-comparison workflow request.</param>
    [HttpPost("/deck-comparison/download")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeckComparisonDownload(DeckComparisonRequest request)
    {
        request ??= new DeckComparisonRequest();
        if (!ModelState.IsValid)
        {
            return View("DeckComparison", new DeckComparisonViewModel
            {
                ActiveTab = DeckPageTab.DeckComparison,
                Request = request,
                ErrorMessage = "The comparison form contains invalid values. Review the highlighted fields and try again."
            });
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.DeckASource)
                && string.IsNullOrWhiteSpace(request.DeckBSource)
                && !string.IsNullOrWhiteSpace(request.ComparisonResponseJson))
            {
                var fallbackCommander = !string.IsNullOrWhiteSpace(request.DeckAName) ? request.DeckAName : request.DeckBName;
                var fallbackFileName = PacketArtifactStore.SuggestComparisonZipFileName(fallbackCommander, request.TargetAiPlatform);
                Response.Headers["X-DeckFlow-Filename"] = fallbackFileName;
                var fallbackBytes = PacketArtifactStore.BuildComparisonZip(
                    request,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    DeckComparisonService.BuildRequestContextText(request));
                return File(fallbackBytes, "application/zip", fallbackFileName);
            }

            // Phase 999.3 D-10: cache lookup runs after the response-json-only short-circuit and before BuildAsync.
            // Misses intentionally pay one extra deck load here before BuildAsync (D-11 accepted trade-off).
            var cacheKey = await _deckComparisonService.TryComputeCacheKeyAsync(request, HttpContext.RequestAborted);
            if (cacheKey is not null
                && _packetCache.TryGet<DeckComparisonResult>(cacheKey, out var cachedResult)
                && cachedResult is not null)
            {
                var cachedBytes = PacketArtifactStore.BuildComparisonZip(
                    request,
                    cachedResult.InputSummary,
                    cachedResult.DeckAListText,
                    cachedResult.DeckBListText,
                    cachedResult.DeckAComboText,
                    cachedResult.DeckBComboText,
                    cachedResult.ComparisonContextText,
                    cachedResult.ComparisonPromptText,
                    cachedResult.FollowUpPromptText,
                    cachedResult.ComparisonSchemaJson,
                    cachedResult.RequestContextText,
                    deckAOriginalText: PacketArtifactStore.OriginalDeckTextOrNull(request.DeckASource),
                    deckBOriginalText: PacketArtifactStore.OriginalDeckTextOrNull(request.DeckBSource));
                // Cached results were produced by BuildAsync, so the resolved commander invariant still applies.
                var cachedFileNameCommander = !string.IsNullOrWhiteSpace(cachedResult.ResolvedDeckACommander)
                    ? cachedResult.ResolvedDeckACommander!
                    : (!string.IsNullOrWhiteSpace(request.DeckAName) ? request.DeckAName : request.DeckBName);
                var cachedFileName = PacketArtifactStore.SuggestComparisonZipFileName(cachedFileNameCommander, request.TargetAiPlatform);
                Response.Headers["X-DeckFlow-Filename"] = cachedFileName;
                return File(cachedBytes, "application/zip", cachedFileName);
            }

            var result = await _deckComparisonService.BuildAsync(request, HttpContext.RequestAborted);
            var bytes = PacketArtifactStore.BuildComparisonZip(
                request,
                result.InputSummary,
                result.DeckAListText,
                result.DeckBListText,
                result.DeckAComboText,
                result.DeckBComboText,
                result.ComparisonContextText,
                result.ComparisonPromptText,
                result.FollowUpPromptText,
                result.ComparisonSchemaJson,
                result.RequestContextText,
                deckAOriginalText: PacketArtifactStore.OriginalDeckTextOrNull(request.DeckASource),
                deckBOriginalText: PacketArtifactStore.OriginalDeckTextOrNull(request.DeckBSource));
            // BuildAsync now validates both decks share the same commander, so
            // ResolvedDeckACommander and ResolvedDeckBCommander are equal here.
            var fileNameCommander = !string.IsNullOrWhiteSpace(result.ResolvedDeckACommander)
                ? result.ResolvedDeckACommander!
                : (!string.IsNullOrWhiteSpace(request.DeckAName) ? request.DeckAName : request.DeckBName);
            var fileName = PacketArtifactStore.SuggestComparisonZipFileName(fileNameCommander, request.TargetAiPlatform);
            Response.Headers["X-DeckFlow-Filename"] = fileName;
            return File(bytes, "application/zip", fileName);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Deck-comparison download failed validation.");
            return View("DeckComparison", new DeckComparisonViewModel
            {
                ActiveTab = DeckPageTab.DeckComparison,
                Request = request,
                ErrorMessage = exception.Message
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Deck-comparison download hit an upstream dependency.");
            var errorMessage = exception.Message.Contains("Deck A", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("Deck B", StringComparison.OrdinalIgnoreCase)
                    ? exception.Message
                    : UpstreamErrorMessageBuilder.BuildScryfallMessage(exception);

            return View("DeckComparison", new DeckComparisonViewModel
            {
                ActiveTab = DeckPageTab.DeckComparison,
                Request = request,
                ErrorMessage = errorMessage
            });
        }
    }

    /// <summary>
    /// Restores a deck-comparison workflow from a previously downloaded packet zip.
    /// </summary>
    /// <param name="zipFile">Packet zip uploaded from a prior deck-comparison session.</param>
    [HttpPost("/deck-comparison/upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public IActionResult DeckComparisonUpload(IFormFile zipFile)
    {
        if (zipFile is null || zipFile.Length == 0)
        {
            return View("DeckComparison", new DeckComparisonViewModel
            {
                ActiveTab = DeckPageTab.DeckComparison,
                Request = new DeckComparisonRequest(),
                ErrorMessage = "Choose a .zip file produced by Download to import."
            });
        }

        if (!string.Equals(Path.GetExtension(zipFile.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return View("DeckComparison", new DeckComparisonViewModel
            {
                ActiveTab = DeckPageTab.DeckComparison,
                Request = new DeckComparisonRequest(),
                ErrorMessage = "Only .zip files produced by Download are accepted."
            });
        }

        var request = new DeckComparisonRequest();
        try
        {
            using var stream = zipFile.OpenReadStream();
            var restored = PacketArtifactStore.LoadComparisonFromZip(stream, request);

            // Partial-zip case: response JSON not yet present (user downloaded
            // mid-workflow). Render the form on the WorkflowStep the loader
            // resolved (1 = re-paste decks, 2 = decks restored, ready to
            // regenerate). ComparisonResponse stays null so Step 3 doesn't render.
            if (string.IsNullOrWhiteSpace(request.ComparisonResponseJson))
            {
                return View("DeckComparison", new DeckComparisonViewModel
                {
                    ActiveTab = DeckPageTab.DeckComparison,
                    Request = request,
                    InputSummary = restored.InputSummary,
                    DeckAListText = restored.DeckAListText,
                    DeckBListText = restored.DeckBListText,
                    DeckAComboText = restored.DeckAComboText,
                    DeckBComboText = restored.DeckBComboText,
                    ComparisonContextText = restored.ComparisonContextText,
                    ComparisonPromptText = restored.ComparisonPromptText,
                    ComparisonSchemaJson = restored.ComparisonSchemaJson,
                    FollowUpPromptText = restored.FollowUpPromptText
                });
            }

            var comparisonResponse = DeckComparisonService.ParseComparisonResponse(request.ComparisonResponseJson);
            request.DeckAName = comparisonResponse.DeckAName;
            request.DeckBName = comparisonResponse.DeckBName;
            request.DeckABracket = comparisonResponse.DeckABracket;
            request.DeckBBracket = comparisonResponse.DeckBBracket;
            return View("DeckComparison", new DeckComparisonViewModel
            {
                ActiveTab = DeckPageTab.DeckComparison,
                Request = request,
                ComparisonResponse = comparisonResponse,
                InputSummary = restored.InputSummary,
                DeckAListText = restored.DeckAListText,
                DeckBListText = restored.DeckBListText,
                DeckAComboText = restored.DeckAComboText,
                DeckBComboText = restored.DeckBComboText,
                ComparisonContextText = restored.ComparisonContextText,
                ComparisonPromptText = restored.ComparisonPromptText,
                ComparisonSchemaJson = restored.ComparisonSchemaJson,
                FollowUpPromptText = restored.FollowUpPromptText
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Deck-comparison upload failed validation.");
            string errorMessage = exception.Message == ResponseParsers.TruncatedResponseMessage ? CorruptedZipMessage : exception.Message;
            return View("DeckComparison", new DeckComparisonViewModel
            {
                ActiveTab = DeckPageTab.DeckComparison,
                Request = new DeckComparisonRequest(),
                ErrorMessage = errorMessage
            });
        }
        catch (InvalidDataException)
        {
            return View("DeckComparison", new DeckComparisonViewModel
            {
                ActiveTab = DeckPageTab.DeckComparison,
                Request = new DeckComparisonRequest(),
                ErrorMessage = "The uploaded file is not a valid .zip archive."
            });
        }
    }

    /// <summary>
    /// Processes the cEDH meta-gap workflow.
    /// </summary>
    /// <param name="request">Current meta-gap workflow request.</param>
    [HttpPost("/cedh-meta-gap")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CedhMetaGap(MetaGapRequest request)
    {
        request ??= new MetaGapRequest();
        if (!ModelState.IsValid)
        {
            return View("CedhMetaGap", new MetaGapViewModel
            {
                ActiveTab = DeckPageTab.CedhMetaGap,
                Request = request,
                ErrorMessage = "The cEDH meta-gap form contains invalid values. Review the highlighted fields and try again."
            });
        }

        try
        {
            var result = await _metaGapService.BuildAsync(request, HttpContext.RequestAborted);
            request.WorkflowStep = request.WorkflowStep switch
            {
                >= 3 when result.AnalysisResponse is not null => 3,
                >= 2 when !string.IsNullOrWhiteSpace(result.PromptText) => 2,
                _ when result.FetchedEntries.Count > 0 => 2,
                _ => 1
            };

            return View("CedhMetaGap", new MetaGapViewModel
            {
                ActiveTab = DeckPageTab.CedhMetaGap,
                Request = request,
                InputSummary = result.InputSummary,
                ResolvedCommanderName = result.ResolvedCommanderName,
                PromptText = result.PromptText,
                SchemaJson = result.SchemaJson,
                FetchedEntries = result.FetchedEntries,
                AnalysisResponse = result.AnalysisResponse
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "cEDH meta-gap generation failed validation.");
            return View("CedhMetaGap", new MetaGapViewModel
            {
                ActiveTab = DeckPageTab.CedhMetaGap,
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "cEDH meta-gap generation hit an upstream dependency.");
            return View("CedhMetaGap", new MetaGapViewModel
            {
                ActiveTab = DeckPageTab.CedhMetaGap,
                Request = request,
                ErrorMessage = exception.StatusCode == HttpStatusCode.TooManyRequests
                    ? "EDH Top 16 is rate-limiting requests right now. Try again shortly."
                    : UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
    }

    /// <summary>
    /// Builds and downloads a cEDH meta-gap packet zip for the current workflow request.
    /// </summary>
    /// <param name="request">Current cEDH meta-gap workflow request.</param>
    [HttpPost("/cedh-meta-gap/download")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CedhMetaGapDownload(MetaGapRequest request)
    {
        request ??= new MetaGapRequest();
        if (!ModelState.IsValid)
        {
            return View("CedhMetaGap", new MetaGapViewModel
            {
                ActiveTab = DeckPageTab.CedhMetaGap,
                Request = request,
                ErrorMessage = "The cEDH meta-gap form contains invalid values. Review the highlighted fields and try again."
            });
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.DeckSource)
                && !string.IsNullOrWhiteSpace(request.MetaGapResponseJson))
            {
                var fallbackFileName = PacketArtifactStore.SuggestCedhMetaGapZipFileName(request.CommanderName, request.TargetAiPlatform);
                var fallbackBytes = PacketArtifactStore.BuildCedhMetaGapZip(
                    request,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    MetaGapService.BuildRequestContextText(request),
                    fetchedEntries: Array.Empty<EdhTop16Entry>());
                Response.Headers["X-DeckFlow-Filename"] = fallbackFileName;
                return File(fallbackBytes, "application/zip", fallbackFileName);
            }

            // Phase 999.3 D-10: cache lookup runs after the response-json-only short-circuit and before BuildAsync.
            // Misses intentionally pay one extra deck load here before BuildAsync (D-11 accepted trade-off).
            var cacheKey = await _metaGapService.TryComputeCacheKeyAsync(request, HttpContext.RequestAborted);
            if (cacheKey is not null
                && _packetCache.TryGet<MetaGapResult>(cacheKey, out var cachedResult)
                && cachedResult is not null)
            {
                var cachedBytes = PacketArtifactStore.BuildCedhMetaGapZip(
                    request,
                    cachedResult.InputSummary ?? string.Empty,
                    cachedResult.PromptText ?? string.Empty,
                    cachedResult.SchemaJson ?? string.Empty,
                    cachedResult.RequestContextText,
                    canonicalDeckListText: cachedResult.DecklistText,
                    originalDeckText: PacketArtifactStore.OriginalDeckTextOrNull(request.DeckSource),
                    fetchedEntries: cachedResult.FetchedEntries);
                var cachedFileNameCommander = cachedResult.ResolvedCommanderName ?? string.Empty;
                var cachedFileName = PacketArtifactStore.SuggestCedhMetaGapZipFileName(cachedFileNameCommander, request.TargetAiPlatform);
                Response.Headers["X-DeckFlow-Filename"] = cachedFileName;
                return File(cachedBytes, "application/zip", cachedFileName);
            }

            var result = await _metaGapService.BuildAsync(request, HttpContext.RequestAborted);
            var bytes = PacketArtifactStore.BuildCedhMetaGapZip(
                request,
                result.InputSummary ?? string.Empty,
                result.PromptText ?? string.Empty,
                result.SchemaJson ?? string.Empty,
                result.RequestContextText,
                canonicalDeckListText: result.DecklistText,
                originalDeckText: PacketArtifactStore.OriginalDeckTextOrNull(request.DeckSource),
                fetchedEntries: result.FetchedEntries);
            var fileName = PacketArtifactStore.SuggestCedhMetaGapZipFileName(result.ResolvedCommanderName ?? request.CommanderName, request.TargetAiPlatform);
            Response.Headers["X-DeckFlow-Filename"] = fileName;
            return File(bytes, "application/zip", fileName);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "cEDH meta-gap download failed validation.");
            return View("CedhMetaGap", new MetaGapViewModel
            {
                ActiveTab = DeckPageTab.CedhMetaGap,
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "cEDH meta-gap download hit an upstream dependency.");
            return View("CedhMetaGap", new MetaGapViewModel
            {
                ActiveTab = DeckPageTab.CedhMetaGap,
                Request = request,
                ErrorMessage = exception.StatusCode == HttpStatusCode.TooManyRequests
                    ? "EDH Top 16 is rate-limiting requests right now. Try again shortly."
                    : UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
    }

    /// <summary>
    /// Restores a cEDH meta-gap workflow from a previously downloaded packet zip.
    /// </summary>
    /// <param name="zipFile">Packet zip uploaded from a prior cEDH meta-gap session.</param>
    [HttpPost("/cedh-meta-gap/upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public IActionResult CedhMetaGapUpload(IFormFile zipFile)
    {
        if (zipFile is null || zipFile.Length == 0)
        {
            return View("CedhMetaGap", new MetaGapViewModel
            {
                ActiveTab = DeckPageTab.CedhMetaGap,
                Request = new MetaGapRequest(),
                ErrorMessage = "Choose a .zip file produced by Download to import."
            });
        }

        if (!string.Equals(Path.GetExtension(zipFile.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return View("CedhMetaGap", new MetaGapViewModel
            {
                ActiveTab = DeckPageTab.CedhMetaGap,
                Request = new MetaGapRequest(),
                ErrorMessage = "Only .zip files produced by Download are accepted."
            });
        }

        var request = new MetaGapRequest();
        try
        {
            using var stream = zipFile.OpenReadStream();
            var restored = PacketArtifactStore.LoadCedhMetaGapFromZip(stream, request);

            // Phase 10-05: round-trip the fetched entries through the next form
            // submit so the service can skip the edhtop16 re-fetch (also
            // bypasses upstream rate-limit on regenerate from a saved session).
            if (restored.FetchedEntries.Count > 0)
            {
                request.FetchedEntriesJson = JsonSerializer.Serialize(
                    restored.FetchedEntries,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }

            // Partial-zip case: response JSON not yet present. Loader has set
            // WorkflowStep correctly (2 if entries restored, 1 otherwise).
            // Propagate FetchedEntries to the view model so the reference table
            // and selection checkboxes render.
            if (string.IsNullOrWhiteSpace(request.MetaGapResponseJson))
            {
                return View("CedhMetaGap", new MetaGapViewModel
                {
                    ActiveTab = DeckPageTab.CedhMetaGap,
                    Request = request,
                    InputSummary = restored.InputSummary,
                    PromptText = restored.PromptText,
                    SchemaJson = restored.SchemaJson,
                    FetchedEntries = restored.FetchedEntries
                });
            }

            var analysisResponse = MetaGapService.ParseResponse(request.MetaGapResponseJson);
            request.CommanderName = analysisResponse.MetaGap.Commander;
            return View("CedhMetaGap", new MetaGapViewModel
            {
                ActiveTab = DeckPageTab.CedhMetaGap,
                Request = request,
                ResolvedCommanderName = analysisResponse.MetaGap.Commander,
                AnalysisResponse = analysisResponse,
                InputSummary = restored.InputSummary,
                PromptText = restored.PromptText,
                SchemaJson = restored.SchemaJson,
                FetchedEntries = restored.FetchedEntries
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "cEDH meta-gap upload failed validation.");
            string errorMessage = exception.Message == ResponseParsers.TruncatedResponseMessage ? CorruptedZipMessage : exception.Message;
            return View("CedhMetaGap", new MetaGapViewModel
            {
                ActiveTab = DeckPageTab.CedhMetaGap,
                Request = new MetaGapRequest(),
                ErrorMessage = errorMessage,
            });
        }
        catch (InvalidDataException)
        {
            return View("CedhMetaGap", new MetaGapViewModel
            {
                ActiveTab = DeckPageTab.CedhMetaGap,
                Request = new MetaGapRequest(),
                ErrorMessage = "The uploaded file is not a valid .zip archive."
            });
        }
    }

    /// <summary>
    /// Attempts to load set options without surfacing catalog failures as page-breaking errors.
    /// </summary>
    private async Task<IReadOnlyList<ScryfallSetOption>> TryGetSetOptionsAsync()
    {
        try
        {
            return await _scryfallSetService.GetSetsAsync(HttpContext.RequestAborted);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Set catalog lookup failed.");
            return Array.Empty<ScryfallSetOption>();
        }
    }

    /// <summary>
    /// Verifies a pasted card list and returns the result as either a text or JSON file download.
    /// </summary>
    /// <param name="request">Card verification request.</param>
    /// <param name="format">Download format (text or JSON).</param>
    private async Task<IActionResult> DownloadCardLookupAsync(CardLookupRequest request, CardLookupDownloadFormat format)
    {
        request ??= new CardLookupRequest();
        if (string.IsNullOrWhiteSpace(request.CardList))
        {
            return View("CardLookup", new CardLookupViewModel
            {
                ActiveTab = DeckPageTab.CardLookup,
                Request = request,
                ErrorMessage = "A card list is required.",
            });
        }

        try
        {
            var result = await _cardLookupService.LookupAsync(request.CardList, HttpContext.RequestAborted);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            if (format == CardLookupDownloadFormat.Json)
            {
                var json = JsonSerializer.Serialize(new
                {
                    verifiedOutputs = result.VerifiedOutputs,
                    missingLines = result.MissingLines,
                }, new JsonSerializerOptions { WriteIndented = true });
                return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", $"verified-cards-{timestamp}.json");
            }

            var output = BuildVerificationFile(result);
            return File(System.Text.Encoding.UTF8.GetBytes(output), "text/plain; charset=utf-8", $"verified-cards-{timestamp}.txt");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Bulk card verification request failed validation.");
            return View("CardLookup", new CardLookupViewModel
            {
                ActiveTab = DeckPageTab.CardLookup,
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Bulk card verification failed.");
            return View("CardLookup", new CardLookupViewModel
            {
                ActiveTab = DeckPageTab.CardLookup,
                Request = request,
                ErrorMessage = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
    }

    /// <summary>
    /// Builds a downloadable text payload for verified and missing cards.
    /// </summary>
    /// <param name="result">Verification result.</param>
    private static string BuildVerificationFile(CardLookupResult result)
    {
        var lines = new List<string>
        {
            "Verified Cards"
        };

        lines.AddRange(result.VerifiedOutputs.Count == 0 ? ["(none)"] : result.VerifiedOutputs);
        lines.Add(string.Empty);
        lines.Add("Cards With Errors");
        lines.AddRange(result.MissingLines.Count == 0 ? ["(none)"] : result.MissingLines);
        return string.Join(Environment.NewLine, lines);
    }

    private enum CardLookupDownloadFormat
    {
        Text,
        Json,
    }

    /// <summary>
    /// Suggests categories based on cached data and optional reference deck.
    /// </summary>
    /// <param name="request">Category suggestion request.</param>
    [HttpPost("/suggest-categories")]
    [FeatureFlagGate("feature.categories.enabled",
        Title = "Category suggestions temporarily unavailable",
        Message = "Category Suggestions is offline for maintenance. Category Reference remains available.",
        PrimaryActionLabel = "Open Category Reference",
        PrimaryActionUrl = "/commander-categories")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuggestCategories(CategorySuggestionRequest request)
    {
        request ??= new CategorySuggestionRequest();
        if (request.Mode == CategorySuggestionMode.ReferenceDeck && !HasSuggestionInput(request))
        {
            return View("SuggestCategories", new DeckDiffViewModel
            {
                ActiveTab = DeckPageTab.SuggestCategories,
                SuggestionRequest = request,
                SuggestionErrorMessage = request.ArchidektInputSource == DeckInputSource.PublicUrl
                    ? "An Archidekt deck URL is required."
                    : "Archidekt text is required.",
            });
        }

        if (string.IsNullOrWhiteSpace(request.CardName))
        {
            return View("SuggestCategories", new DeckDiffViewModel
            {
                ActiveTab = DeckPageTab.SuggestCategories,
                SuggestionRequest = request,
                SuggestionErrorMessage = "A card name is required.",
            });
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
            timeoutCts.CancelAfter(SuggestionTimeout);
            var cancellationToken = timeoutCts.Token;
            var result = await _categorySuggestionService.SuggestAsync(request, cancellationToken);
            var lookupMessage = result.NothingFound
                ? CategorySuggestionMessageBuilder.BuildNoSuggestionsMessage(result.CardName, result.CardDeckTotals)
                : null;
            var viewModel = new DeckDiffViewModel
            {
                ActiveTab = DeckPageTab.SuggestCategories,
                SuggestionRequest = request,
                ExactSuggestedCategoriesText = CategorySuggestionReporter.ToText(result.ExactCategories, result.CardName),
                ExactSuggestionContextText = "These are exact card-name matches found in the Archidekt reference deck you provided.",
                InferredCategoriesText = CategorySuggestionReporter.ToText(result.InferredCategories, result.CardName),
                InferredSuggestionContextText = "These come from the local cached store built from recent Archidekt decks.",
                EdhrecCategoriesText = CategorySuggestionReporter.ToText(result.EdhrecCategories, result.CardName),
                EdhrecSuggestionContextText = "These themes/tags are inferred from EDHREC’s deck data that include the card.",
                TaggerCategoriesText = CategorySuggestionReporter.ToText(result.TaggerCategories, result.CardName),
                TaggerSuggestionContextText = "These are community-curated functional tags from Scryfall Tagger.",
                NoSuggestionsFound = result.NothingFound,
                NoSuggestionsMessage = lookupMessage,
                SuggestionSourceSummary = result.UsedSources.Count == 0
                    ? null
                    : $"Source used: {string.Join(" + ", result.UsedSources)}",
                CardDeckTotals = result.CardDeckTotals
            };
            return View("SuggestCategories", viewModel);
        }
        catch (Exception exception) when (exception is DeckParseException or InvalidOperationException or HttpRequestException)
        {
            _logger.LogError(exception, "Failed to suggest categories for {CardName}.", request.CardName);
            return View("SuggestCategories", new DeckDiffViewModel
            {
                ActiveTab = DeckPageTab.SuggestCategories,
                SuggestionRequest = request,
                SuggestionErrorMessage = exception.Message,
            });
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return View("SuggestCategories", new DeckDiffViewModel
            {
                ActiveTab = DeckPageTab.SuggestCategories,
                SuggestionRequest = request,
                SuggestionErrorMessage = "Category lookup timed out after 20 seconds. Try again, or use a direct Archidekt deck with the card already categorized.",
            });
        }
    }

    /// <summary>
    /// Persists user resolutions for printing conflicts and rebuilds the view.
    /// </summary>
    /// <param name="request">Deck diff request with resolutions.</param>
    [HttpPost("/resolve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(DeckDiffRequest request)
    {
        try
        {
            var syncResult = await _deckSyncService.CompareDecksAsync(request, HttpContext.RequestAborted);
            var diff = syncResult.Diff;
            var updatedConflicts = diff.PrintingConflicts
                .Select(conflict => conflict with
                {
                    Resolution = request.Resolutions.TryGetValue(conflict.CardName, out var resolution)
                        ? resolution
                        : PrintingChoice.KeepArchidekt,
                })
                .ToList();

            var resolvedDiff = diff with { PrintingConflicts = updatedConflicts };
            return BuildViewModel(request, syncResult.LoadedDecks, resolvedDiff, ReconciliationReporter.GenerateSwapChecklist(updatedConflicts, DeckSyncSupport.GetTargetSystem(request.Direction)));
        }
        catch (Exception exception) when (exception is DeckParseException or InvalidOperationException or HttpRequestException)
        {
            _logger.LogError(exception, "Failed to resolve printing conflicts for {Direction}.", request.Direction);
            return View("DeckSync", new DeckDiffViewModel
            {
                Request = request,
                ErrorMessage = BuildUserFacingErrorMessage(request, exception),
            });
        }
    }

    /// <summary>
    /// Validates inputs and renders the diff view or error message.
    /// </summary>
    /// <param name="request">Deck diff request data.</param>
    private async Task<IActionResult> RenderDiffAsync(DeckDiffRequest request)
    {
        request ??= new DeckDiffRequest();
        if (!HasMoxfieldInput(request))
        {
            var leftSystem = DeckSyncSupport.GetLeftPanelSystem(request.Direction);
            return View("DeckSync", new DeckDiffViewModel
            {
                ActiveTab = DeckPageTab.Sync,
                Request = request,
                ErrorMessage = request.MoxfieldInputSource == DeckInputSource.PublicUrl
                    ? $"A {leftSystem} deck URL is required."
                    : $"{leftSystem} text is required.",
            });
        }

        if (!HasArchidektInput(request))
        {
            var rightSystem = DeckSyncSupport.GetRightPanelSystem(request.Direction);
            return View("DeckSync", new DeckDiffViewModel
            {
                ActiveTab = DeckPageTab.Sync,
                Request = request,
                ErrorMessage = request.ArchidektInputSource == DeckInputSource.PublicUrl
                    ? $"A {rightSystem} deck URL is required."
                    : $"{rightSystem} text is required.",
            });
        }

        try
        {
            var syncResult = await _deckSyncService.CompareDecksAsync(request, HttpContext.RequestAborted);
            _logger.LogInformation(
                "Running deck sync for {Direction}. MoxfieldUrlProvided={HasMoxfieldUrl} ArchidektUrlProvided={HasArchidektUrl}",
                request.Direction,
                !string.IsNullOrWhiteSpace(request.MoxfieldUrl),
                !string.IsNullOrWhiteSpace(request.ArchidektUrl));
            return BuildViewModel(request, syncResult.LoadedDecks, syncResult.Diff, null);
        }
        catch (Exception exception) when (exception is DeckParseException or InvalidOperationException or HttpRequestException)
        {
            _logger.LogError(
                exception,
                "Failed to render deck sync for {Direction}. MoxfieldUrl={MoxfieldUrl} ArchidektUrl={ArchidektUrl}",
                request.Direction,
                request.MoxfieldUrl,
                request.ArchidektUrl);
                return View("DeckSync", new DeckDiffViewModel
            {
                ActiveTab = DeckPageTab.Sync,
                Request = request,
                ErrorMessage = BuildUserFacingErrorMessage(request, exception),
            });
        }
    }

    /// <summary>
    /// Creates the DeckDiffViewModel for rendering after a comparison.
    /// </summary>
    /// <param name="request">Incoming request.</param>
    /// <param name="loadedDecks">Loaded deck entries.</param>
    /// <param name="diff">Diff result.</param>
    /// <param name="swapChecklistText">Optional swap checklist text.</param>
    private ViewResult BuildViewModel(DeckDiffRequest request, LoadedDecks loadedDecks, DeckDiff diff, string? swapChecklistText)
    {
        var sourceEntries = DeckSyncSupport.GetSourceEntries(request.Direction, loadedDecks);
        var targetEntries = DeckSyncSupport.GetTargetEntries(request.Direction, loadedDecks);
        var sourceSystem = DeckSyncSupport.GetSourceSystem(request.Direction);
        var targetSystem = DeckSyncSupport.GetTargetSystem(request.Direction);

        return View("DeckSync", new DeckDiffViewModel
        {
            ActiveTab = DeckPageTab.Sync,
            Request = request,
            Diff = diff,
            DeltaText = DeltaExporter.ToText(diff.ToAdd.ToList(), targetSystem),
            FullImportText = FullImportExporter.ToText(sourceEntries, targetEntries, request.Mode, targetSystem, diff.PrintingConflicts, request.CategorySyncMode),
            ReportText = ReconciliationReporter.ToText(diff, sourceSystem, targetSystem),
            SwapChecklistText = string.IsNullOrWhiteSpace(swapChecklistText) ? null : swapChecklistText,
            InstructionsText = ReconciliationReporter.GetInstructions(targetSystem),
        });
    }

    /// <summary>
    /// Builds a user-friendly error message for controller failures.
    /// </summary>
    /// <param name="request">Original request data.</param>
    /// <param name="exception">Exception that occurred.</param>
    private static string BuildUserFacingErrorMessage(DeckDiffRequest request, Exception exception)
    {
        if (IsMoxfieldForbidden(request, exception))
        {
            return "Moxfield blocked the deck URL request from this local web app with HTTP 403. Paste the Moxfield export text into the form instead, or run the compare from the CLI/WSL environment where URL fetches succeed.";
        }

        return exception.Message;
    }

    /// <summary>
    /// Determines whether a 403 from Moxfield should be surfaced with a tip.
    /// </summary>
    /// <param name="request">Deck diff request.</param>
    /// <param name="exception">Exception thrown by the request.</param>
    private static bool IsMoxfieldForbidden(DeckDiffRequest request, Exception exception)
    {
        return request.MoxfieldInputSource == DeckInputSource.PublicUrl
            && !string.IsNullOrWhiteSpace(request.MoxfieldUrl)
            && exception is HttpRequestException httpException
            && httpException.StatusCode == HttpStatusCode.Forbidden;
    }

    /// <summary>
    /// Checks if the request includes Moxfield input (text or URL).
    /// </summary>
    /// <param name="request">Deck diff request.</param>
    private static bool HasMoxfieldInput(DeckDiffRequest request)
        => request.MoxfieldInputSource == DeckInputSource.PublicUrl
            ? !string.IsNullOrWhiteSpace(request.MoxfieldUrl)
            : !string.IsNullOrWhiteSpace(request.MoxfieldText);

    /// <summary>
    /// Checks if the request includes Archidekt input (text or URL).
    /// </summary>
    /// <param name="request">Deck diff request.</param>
    private static bool HasArchidektInput(DeckDiffRequest request)
        => request.ArchidektInputSource == DeckInputSource.PublicUrl
            ? !string.IsNullOrWhiteSpace(request.ArchidektUrl)
            : !string.IsNullOrWhiteSpace(request.ArchidektText);

    /// <summary>
    /// Validates the suggestion request contains enough Archidekt input.
    /// </summary>
    /// <param name="request">Category suggestion request.</param>
    private static bool HasSuggestionInput(CategorySuggestionRequest request)
        => request.ArchidektInputSource == DeckInputSource.PublicUrl
            ? !string.IsNullOrWhiteSpace(request.ArchidektUrl)
            : !string.IsNullOrWhiteSpace(request.ArchidektText);

    /// <summary>
    /// Searches recent Archidekt decks live for potential categories.
    /// </summary>
    /// <param name="cardName">Card name to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
}
