using Microsoft.AspNetCore.Mvc;
using System.Net;
using DeckFlow.Core.Diffing;
using DeckFlow.Core.Exporting;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the deck sync and resolve workflows.
/// </summary>
public sealed class DeckSyncController : DeckToolControllerBase
{
    private readonly IDeckSyncService _deckSyncService;
    private readonly ILogger<DeckSyncController> _logger;

    /// <summary>
    /// Creates the deck sync controller.
    /// </summary>
    /// <param name="deckSyncService">Deck sync service.</param>
    /// <param name="logger">Logger.</param>
    public DeckSyncController(
        IDeckSyncService deckSyncService,
        ILogger<DeckSyncController> logger)
    {
        ArgumentNullException.ThrowIfNull(deckSyncService);
        ArgumentNullException.ThrowIfNull(logger);

        _deckSyncService = deckSyncService;
        _logger = logger;
    }

    /// <summary>
    /// Renders the deck sync view with default tab state.
    /// </summary>
    [HttpGet("/sync")]
    [FeatureFlagGate("tool.deck-sync.enabled")]
    public IActionResult Index()
    {
        return View("DeckSync", new DeckDiffViewModel
        {
            ActiveTab = DeckPageTab.Sync,
        });
    }

    /// <summary>
    /// Handles the deck sync POST to generate a diff report.
    /// </summary>
    /// <param name="request">Deck diff request data.</param>
    [HttpPost("/sync")]
    [FeatureFlagGate("tool.deck-sync.enabled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(DeckDiffRequest request)
    {
        return await RenderDiffAsync(request);
    }

    /// <summary>
    /// Persists user resolutions for printing conflicts and rebuilds the view.
    /// </summary>
    /// <param name="request">Deck diff request with resolutions.</param>
    [HttpPost("/resolve")]
    [FeatureFlagGate("tool.deck-sync.enabled")]
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
}
