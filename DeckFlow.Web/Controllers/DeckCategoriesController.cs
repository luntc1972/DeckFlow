using Microsoft.AspNetCore.Mvc;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the category suggestion workflows.
/// </summary>
public sealed class DeckCategoriesController : DeckToolControllerBase
{
    private readonly ICategorySuggestionService _categorySuggestionService;
    private readonly ICardSearchService _cardSearchService;
    private readonly ILogger<DeckCategoriesController> _logger;

    /// <summary>
    /// Creates the categories controller.
    /// </summary>
    public DeckCategoriesController(
        ICategorySuggestionService categorySuggestionService,
        ICardSearchService cardSearchService,
        ILogger<DeckCategoriesController> logger)
    {
        ArgumentNullException.ThrowIfNull(categorySuggestionService);
        ArgumentNullException.ThrowIfNull(cardSearchService);
        ArgumentNullException.ThrowIfNull(logger);

        _categorySuggestionService = categorySuggestionService;
        _cardSearchService = cardSearchService;
        _logger = logger;
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
    /// Provides card name suggestions for the suggest categories form.
    /// </summary>
    /// <param name="query">Partial card name.</param>
    [HttpGet("/suggest-categories/card-search")]
    [FeatureFlagGate("feature.categories.enabled",
        Title = "Category suggestions temporarily unavailable",
        Message = "Category Suggestions is offline for maintenance. Category Reference remains available.",
        PrimaryActionLabel = "Open Category Reference",
        PrimaryActionUrl = "/commander-categories")]
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
            using var timeoutCts = CreateTimeoutScope(SuggestionTimeout);
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
    /// Validates the suggestion request contains enough Archidekt input.
    /// </summary>
    /// <param name="request">Category suggestion request.</param>
    private static bool HasSuggestionInput(CategorySuggestionRequest request)
        => request.ArchidektInputSource == DeckInputSource.PublicUrl
            ? !string.IsNullOrWhiteSpace(request.ArchidektUrl)
            : !string.IsNullOrWhiteSpace(request.ArchidektText);
}
