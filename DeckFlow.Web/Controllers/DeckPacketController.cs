using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using DeckFlow.Core.Analysis;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the packet-generation MVC pages for deck analysis, deck comparison, and cEDH meta-gap workflows.
/// </summary>
public sealed class DeckPacketController : DeckToolControllerBase
{
    private const string CorruptedZipMessage = "The uploaded zip contains an incomplete response payload. Re-export from the originating session or paste a fresh response.";
    private static readonly IReadOnlyDictionary<string, string> EmptySetUpgradeCardText
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly IDeckAnalysisPacketService _deckAnalysisPacketService;
    private readonly IDeckComparisonService _deckComparisonService;
    private readonly IMetaGapService _metaGapService;
    private readonly PacketSessionCache _packetCache;
    private readonly ILogger<DeckPacketController> _logger;
    private readonly IFeatureFlagCache? _flagCache;
    private readonly ICardSearchService? _cardSearchService;

    /// <summary>
    /// Creates the packet-workflow controller.
    /// </summary>
    public DeckPacketController(
        IDeckAnalysisPacketService deckAnalysisPacketService,
        IDeckComparisonService deckComparisonService,
        IMetaGapService metaGapService,
        PacketSessionCache packetCache,
        ILogger<DeckPacketController> logger,
        IFeatureFlagCache? flagCache = null,
        ICardSearchService? cardSearchService = null)
    {
        ArgumentNullException.ThrowIfNull(deckAnalysisPacketService);
        ArgumentNullException.ThrowIfNull(deckComparisonService);
        ArgumentNullException.ThrowIfNull(metaGapService);
        ArgumentNullException.ThrowIfNull(packetCache);
        ArgumentNullException.ThrowIfNull(logger);

        _deckAnalysisPacketService = deckAnalysisPacketService;
        _deckComparisonService = deckComparisonService;
        _metaGapService = metaGapService;
        _packetCache = packetCache;
        _logger = logger;
        _flagCache = flagCache;
        _cardSearchService = cardSearchService;
    }

    /// <summary>
    /// Single source of truth for the <c>analysis.command-zone-awareness</c> flag state stamped onto
    /// every <see cref="DeckAnalysisViewModel"/> render path (Codex MED-1). Reads the snapshot with the
    /// default-OFF pattern — a missing key means the feature stays off, so the companion designator UI
    /// cannot appear on some paths and not others.
    /// </summary>
    private bool IsCommandZoneAwarenessEnabled()
        => _flagCache is not null
            && _flagCache.Snapshot().TryGetValue(DeckAnalysisPacketService.CommandZoneAwarenessFlag, out var enabled)
            && enabled;

    // Serialize a download-zip artifact (win-con map, interaction audit) SOLELY from the typed,
    // flag-gated result -- never the raw posted *Json field (which is neither flag-gated nor
    // structurally validated). A null value (flag off) yields null, dropping the zip entry and
    // preserving flag-OFF byte-identity even when the client posts a stale field (Codex MED
    // findings #2/#3; generalized to interaction-audit per code-review fix #3).
    private static string? SerializeForArtifact<T>(T? value) where T : class
        => value is null ? null : JsonSerializer.Serialize(value);

    /// <summary>
    /// Renders the staged deck-analysis packet workflow. Set options load asynchronously on the client.
    /// </summary>
    [HttpGet("/deck-analysis")]
    [FeatureFlagGate("tool.deck-analysis.enabled")]
    public IActionResult DeckAnalysis()
    {
        return View("DeckAnalysis", new DeckAnalysisViewModel
        {
            ActiveTab = DeckPageTab.DeckAnalysis,
            CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled(),
            Request = new DeckAnalysisRequest(),
        });
    }

    /// <summary>
    /// Renders the staged deck-comparison workflow.
    /// </summary>
    [HttpGet("/deck-comparison")]
    [FeatureFlagGate("tool.deck-comparison.enabled")]
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
    [FeatureFlagGate("tool.cedh-meta-gap.enabled")]
    public IActionResult CedhMetaGap()
    {
        return View("CedhMetaGap", new MetaGapViewModel
        {
            ActiveTab = DeckPageTab.CedhMetaGap,
            Request = new MetaGapRequest(),
        });
    }

    /// <summary>
    /// Returns commander-eligible card name suggestions for the cEDH meta-gap commander
    /// override typeahead. EDH Top 16 requires the exact canonical card name, so the picker
    /// steers users to a real commander instead of a partial query that returns no results.
    /// </summary>
    /// <param name="q">Partial commander name.</param>
    [HttpGet("/cedh-meta-gap/commander-search")]
    [FeatureFlagGate("tool.cedh-meta-gap.enabled")]
    public async Task<IActionResult> CedhMetaGapCommanderSearch(string q)
    {
        if (_cardSearchService is null)
        {
            return Json(Array.Empty<string>());
        }

        try
        {
            var names = await _cardSearchService.SearchCommandersAsync(q ?? string.Empty, HttpContext.RequestAborted);
            return Json(names);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "cEDH meta-gap commander search autocomplete failed for query {Query}.", q);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception)
            });
        }
    }

    /// <summary>
    /// Processes a prompt workflow postback and regenerates the next packet outputs.
    /// </summary>
    /// <param name="request">Current workflow request.</param>
    [HttpPost("/deck-analysis")]
    [FeatureFlagGate("tool.deck-analysis.enabled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeckAnalysis(DeckAnalysisRequest request)
    {
        request ??= new DeckAnalysisRequest();

        try
        {
            var result = await _deckAnalysisPacketService.BuildAsync(request, HttpContext.RequestAborted);
            // Carry the computed score forward through the hidden ScoreJson form field so the Step-3
            // early-return (no live Scryfall data to recompute from) can restore it. Omitting this write
            // would silently drop the score at Step 3 even though it renders at Step 2 via Model.Score.
            // Codex code-review fix: explicitly CLEAR each field when its typed result is null (not just
            // skip the write) -- otherwise a stale posted value from a prior flag-ON request survives a
            // flag-OFF re-post and the view's `!string.IsNullOrEmpty(...)` gate renders the hidden
            // textarea anyway, breaking flag-OFF byte-identity.
            request.ScoreJson = result.Score is null ? string.Empty : JsonSerializer.Serialize(result.Score);
            request.InteractionAuditJson = result.InteractionAudit is null ? string.Empty : JsonSerializer.Serialize(result.InteractionAudit);
            request.WinConMapJson = result.WinConMap is null ? string.Empty : JsonSerializer.Serialize(result.WinConMap);
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled(),
                Request = request,
                InputSummary = result.InputSummary,
                SuggestedChatTitle = result.SuggestedChatTitle,
                ReferenceText = result.ReferenceText,
                AnalysisPromptText = result.AnalysisPromptText,
                DeckProfileSchemaJson = result.DeckProfileSchemaJson,
                SetUpgradePromptText = result.SetUpgradePromptText,
                TimingSummary = result.TimingSummary,
                AnalysisResponse = result.AnalysisResponse,
                Score = result.Score,
                InteractionAudit = result.InteractionAudit,
                WinConMap = result.WinConMap,
                SetUpgradeResponse = result.SetUpgradeResponse,
                SetUpgradeCardText = result.SetUpgradeCardText ?? EmptySetUpgradeCardText,
                ImportWarning = result.ImportWarning,
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Deck-analysis packet generation failed validation.");
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled(),
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
                CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled(),
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
    [FeatureFlagGate("tool.deck-analysis.enabled")]
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
                var cachedInteractionAuditJson = SerializeForArtifact(cachedResult.InteractionAudit);
                var cachedWinConMapJson = SerializeForArtifact(cachedResult.WinConMap);
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
                    originalDeckText: PacketArtifactStore.OriginalDeckTextOrNull(request.DeckSource),
                    interactionAuditJson: cachedInteractionAuditJson,
                    winConMapJson: cachedWinConMapJson);
                var cachedFileName = PacketArtifactStore.SuggestPacketZipFileName(cachedCommanderName, request.TargetAiPlatform);
                Response.Headers["X-DeckFlow-Filename"] = cachedFileName;
                return File(cachedBytes, "application/zip", cachedFileName);
            }

            var result = await _deckAnalysisPacketService.BuildAsync(request, HttpContext.RequestAborted);
            var commanderName = !string.IsNullOrWhiteSpace(result.ResolvedCommanderName)
                ? result.ResolvedCommanderName
                : result.AnalysisResponse?.Commander ?? request.DeckName;
            var requestContextText = result.RequestContextText ?? DeckAnalysisPacketService.BuildRequestContextText(request, commanderName);
            var freshInteractionAuditJson = SerializeForArtifact(result.InteractionAudit);
            var freshWinConMapJson = SerializeForArtifact(result.WinConMap);
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
                originalDeckText: PacketArtifactStore.OriginalDeckTextOrNull(request.DeckSource),
                interactionAuditJson: freshInteractionAuditJson,
                winConMapJson: freshWinConMapJson);
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
                CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled(),
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
                CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled(),
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
    [FeatureFlagGate("tool.deck-analysis.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> DeckAnalysisUpload(IFormFile zipFile)
    {
        if (zipFile is null || zipFile.Length == 0)
        {
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled(),
                Request = new DeckAnalysisRequest(),
                ErrorMessage = "Choose a .zip file produced by Download to import."
            });
        }

        if (!string.Equals(Path.GetExtension(zipFile.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled(),
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
            if (result.Score is not null)
            {
                request.ScoreJson = JsonSerializer.Serialize(result.Score);
            }
            if (result.InteractionAudit is not null)
            {
                request.InteractionAuditJson = JsonSerializer.Serialize(result.InteractionAudit);
            }
            if (result.WinConMap is not null)
            {
                request.WinConMapJson = JsonSerializer.Serialize(result.WinConMap);
            }
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled(),
                Request = request,
                InputSummary = result.InputSummary,
                SuggestedChatTitle = result.SuggestedChatTitle,
                ReferenceText = result.ReferenceText,
                AnalysisPromptText = result.AnalysisPromptText,
                DeckProfileSchemaJson = result.DeckProfileSchemaJson,
                SetUpgradePromptText = result.SetUpgradePromptText,
                TimingSummary = result.TimingSummary,
                AnalysisResponse = result.AnalysisResponse,
                Score = result.Score,
                InteractionAudit = result.InteractionAudit,
                WinConMap = result.WinConMap,
                SetUpgradeResponse = result.SetUpgradeResponse,
                SetUpgradeCardText = result.SetUpgradeCardText ?? EmptySetUpgradeCardText,
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
                CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled(),
                Request = new DeckAnalysisRequest(),
                ErrorMessage = errorMessage
            });
        }
        catch (InvalidDataException)
        {
            return View("DeckAnalysis", new DeckAnalysisViewModel
            {
                ActiveTab = DeckPageTab.DeckAnalysis,
                CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled(),
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
    [FeatureFlagGate("tool.deck-comparison.enabled")]
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
    [FeatureFlagGate("tool.deck-comparison.enabled")]
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
    [FeatureFlagGate("tool.deck-comparison.enabled")]
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
    [FeatureFlagGate("tool.cedh-meta-gap.enabled")]
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
    [FeatureFlagGate("tool.cedh-meta-gap.enabled")]
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
    [FeatureFlagGate("tool.cedh-meta-gap.enabled")]
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
}
