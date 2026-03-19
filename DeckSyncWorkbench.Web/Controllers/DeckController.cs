using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Threading.Tasks;
using DeckSyncWorkbench.Core.Diffing;
using DeckSyncWorkbench.Core.Exporting;
using DeckSyncWorkbench.Core.Integration;
using DeckSyncWorkbench.Core.Models;
using DeckSyncWorkbench.Core.Parsing;
using DeckSyncWorkbench.Core.Reporting;
using DeckSyncWorkbench.Web.Models;
using DeckSyncWorkbench.Web.Services;

namespace DeckSyncWorkbench.Web.Controllers;

public sealed class DeckController : Controller
{
    private static readonly TimeSpan SuggestionTimeout = TimeSpan.FromMinutes(1);
    private const int ExtendedHarvestDurationSeconds = 30;
    private readonly IDeckSyncService _deckSyncService;
    private readonly ICategoryKnowledgeStore _categoryKnowledgeStore;
    private readonly ICardSearchService _cardSearchService;
    private readonly ILogger<DeckController> _logger;

    public DeckController(
        IDeckSyncService deckSyncService,
        ICategoryKnowledgeStore categoryKnowledgeStore,
        ICardSearchService cardSearchService,
        ILogger<DeckController> logger)
    {
        _deckSyncService = deckSyncService;
        _categoryKnowledgeStore = categoryKnowledgeStore;
        _cardSearchService = cardSearchService;
        _logger = logger;
    }

    [HttpGet("/")]
    /// <summary>
    /// Renders the deck sync view with default tab state.
    /// </summary>
    public IActionResult Index()
    {
        return View("DeckSync", new DeckDiffViewModel
        {
            ActiveTab = DeckPageTab.Sync,
        });
    }

    [HttpGet("/suggest-categories")]
    /// <summary>
    /// Renders the suggest categories tab with fresh state.
    /// </summary>
    public IActionResult SuggestCategories()
    {
        return View("SuggestCategories", new DeckDiffViewModel
        {
            ActiveTab = DeckPageTab.SuggestCategories,
            SuggestionRequest = new CategorySuggestionRequest(),
        });
    }

    [HttpGet("/suggest-categories/card-search")]
    /// <summary>
    /// Provides card name suggestions for the suggest categories form.
    /// </summary>
    /// <param name="query">Partial card name.</param>
    public async Task<IActionResult> CardSearch(string query)
    {
        var names = await _cardSearchService.SearchAsync(query ?? string.Empty, HttpContext.RequestAborted);
        return Json(names);
    }

    [HttpPost("/")]
    [ValidateAntiForgeryToken]
    /// <summary>
    /// Handles the deck sync POST to generate a diff report.
    /// </summary>
    /// <param name="request">Deck diff request data.</param>
    public async Task<IActionResult> Index(DeckDiffRequest request)
    {
        return await RenderDiffAsync(request);
    }

    [HttpPost("/suggest-categories")]
    [ValidateAntiForgeryToken]
    /// <summary>
    /// Suggests categories based on cached data and optional reference deck.
    /// </summary>
    /// <param name="request">Category suggestion request.</param>
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
            var initialProcessedDeckCount = await _categoryKnowledgeStore.GetProcessedDeckCountAsync(cancellationToken);

            await _categoryKnowledgeStore.EnsureHarvestFreshAsync(_logger, cancellationToken);
            var exactCategories = request.Mode == CategorySuggestionMode.ReferenceDeck
                ? CategorySuggestionReporter.SuggestCategories(await LoadArchidektSuggestionEntriesAsync(request, cancellationToken), request.CardName)
                : [];
            var inferredCategories = await _categoryKnowledgeStore.GetCategoriesAsync(request.CardName, cancellationToken);
            if (inferredCategories.Count == 0)
            {
                await _categoryKnowledgeStore.ProcessNextDecksAsync(_logger, cancellationToken);
                inferredCategories = await _categoryKnowledgeStore.GetCategoriesAsync(request.CardName, cancellationToken);
            }
            var cardTotals = await _categoryKnowledgeStore.GetCardDeckTotalsAsync(request.CardName, cancellationToken: cancellationToken);
            var edhrecCategories = exactCategories.Count == 0 && inferredCategories.Count == 0
                ? await new EdhrecCardLookup().LookupCategoriesAsync(request.CardName, cancellationToken)
                : [];
            var nothingFound = exactCategories.Count == 0
                && inferredCategories.Count == 0
                && edhrecCategories.Count == 0;
            var usedSources = new List<string>();
            if (exactCategories.Count > 0)
            {
                usedSources.Add("reference deck");
            }

            if (inferredCategories.Count > 0)
            {
                usedSources.Add("cached store");
            }

            if (edhrecCategories.Count > 0)
            {
                usedSources.Add("EDHREC");
            }

            await _categoryKnowledgeStore.PersistObservedCategoriesAsync("edhrec", request.CardName, edhrecCategories, cancellationToken: cancellationToken);
            var finalProcessedDeckCount = await _categoryKnowledgeStore.GetProcessedDeckCountAsync(cancellationToken);
            var additionalDecksFound = Math.Max(finalProcessedDeckCount - initialProcessedDeckCount, 0);
            var lookupMessage = BuildNoSuggestionsMessage(request.CardName, cardTotals);
            var viewModel = new DeckDiffViewModel
            {
                ActiveTab = DeckPageTab.SuggestCategories,
                SuggestionRequest = request,
                ExactSuggestedCategoriesText = CategorySuggestionReporter.ToText(exactCategories, request.CardName),
                ExactSuggestionContextText = "These are exact card-name matches found in the Archidekt reference deck you provided.",
                InferredCategoriesText = CategorySuggestionReporter.ToText(inferredCategories, request.CardName),
                InferredSuggestionContextText = "These come from the local cached store built from recent Archidekt decks.",
                EdhrecCategoriesText = CategorySuggestionReporter.ToText(edhrecCategories, request.CardName),
                EdhrecSuggestionContextText = "These themes/tags are inferred from EDHREC’s deck data that include the card.",
                NoSuggestionsFound = nothingFound,
                NoSuggestionsMessage = nothingFound
                    ? lookupMessage
                    : null,
                SuggestionSourceSummary = usedSources.Count == 0
                    ? null
                    : $"Source used: {string.Join(" + ", usedSources)}",
                ExtendedHarvestTriggered = true,
                AdditionalDecksFound = additionalDecksFound,
                CardDeckTotals = cardTotals
            };
            ScheduleExtendedArchidektHarvest();
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
                SuggestionErrorMessage = "Category lookup timed out after 60 seconds. Try again, or use a direct Archidekt deck with the card already categorized.",
            });
        }
    }

    [HttpPost("/resolve")]
    [ValidateAntiForgeryToken]
    /// <summary>
    /// Persists user resolutions for printing conflicts and rebuilds the view.
    /// </summary>
    /// <param name="request">Deck diff request with resolutions.</param>
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
            return View("DeckSync", new DeckDiffViewModel
            {
                ActiveTab = DeckPageTab.Sync,
                Request = request,
                ErrorMessage = request.MoxfieldInputSource == DeckInputSource.PublicUrl
                    ? "A Moxfield deck URL is required."
                    : "Moxfield text is required.",
            });
        }

        if (!HasArchidektInput(request))
        {
            return View("DeckSync", new DeckDiffViewModel
            {
                ActiveTab = DeckPageTab.Sync,
                Request = request,
                ErrorMessage = request.ArchidektInputSource == DeckInputSource.PublicUrl
                    ? "An Archidekt deck URL is required."
                    : "Archidekt text is required.",
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
    /// Loads entries for reference deck suggestions based on request inputs.
    /// </summary>
    /// <param name="request">Suggestion request details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private static async Task<List<DeckEntry>> LoadArchidektSuggestionEntriesAsync(CategorySuggestionRequest request, CancellationToken cancellationToken)
    {
        if (request.ArchidektInputSource == DeckInputSource.PublicUrl)
        {
            return await new ArchidektApiDeckImporter().ImportAsync(request.ArchidektUrl, cancellationToken);
        }

        return new ArchidektParser().ParseText(request.ArchidektText);
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

    private void ScheduleExtendedArchidektHarvest()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _categoryKnowledgeStore.RunCacheSweepAsync(_logger, ExtendedHarvestDurationSeconds);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Extended Archidekt harvest failed.");
            }
        });
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

    private const string NoCachedDataMessage = "No card categories for {0} have been observed in the cached data yet. Run Show Categories again to refresh the cache.";
    private const string NoSuggestionsElsewhereMessage = "No category suggestions were found for {0}. You can run the lookup again to retry the live Archidekt and EDHREC checks.";

    internal static string BuildNoSuggestionsMessage(string cardName, CardDeckTotals deckTotals)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        return deckTotals.TotalDeckCount == 0
            ? string.Format(NoCachedDataMessage, cardName)
            : string.Format(NoSuggestionsElsewhereMessage, cardName);
    }

    /// <summary>
    /// Searches recent Archidekt decks live for potential categories.
    /// </summary>
    /// <param name="cardName">Card name to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
}
