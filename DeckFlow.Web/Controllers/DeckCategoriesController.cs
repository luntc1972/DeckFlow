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
    [FeatureFlagGate("tool.categories.enabled")]
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
    [FeatureFlagGate("tool.categories.enabled")]
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
    [FeatureFlagGate("tool.categories.enabled")]
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
            // One merge pass drives both the plain copy text and the weighted table.
            var weighted = CategorySuggestionReporter.MergeWeighted(
                result.ExactCategories,
                result.InferredCategories,
                result.EdhrecCategories,
                result.TaggerCategories);
            var lookupMessage = result.NothingFound
                ? CategorySuggestionMessageBuilder.BuildNoSuggestionsMessage(result.CardName, result.CardDeckTotals)
                : null;
            var viewModel = new DeckDiffViewModel
            {
                ActiveTab = DeckPageTab.SuggestCategories,
                SuggestionRequest = request,
                MergedCategoriesText = CategorySuggestionReporter.ToText(
                    weighted.Select(weight => weight.Category), result.CardName),
                WeightedCategories = BuildWeightedCategories(weighted, result),
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

    // Ranks the weighted merge rows for display: most agreed-on first, then by popularity
    // (rows without a crawl percentage sink below those that have one), then alphabetical.
    private static IReadOnlyList<CategoryWeightRow> BuildWeightedCategories(
        IReadOnlyList<CategorySuggestionReporter.CategorySourceWeight> weighted,
        CategorySuggestionResult result)
        => weighted
            .Select(weight => BuildCategoryWeightRow(weight, result.CategoryDeckCounts, result.CardDeckTotals.TotalDeckCount))
            .OrderByDescending(row => row.SourceCount)
            .ThenBy(row => row.Percent is null ? 1 : 0)
            .ThenByDescending(row => row.Percent)
            .ThenBy(row => row.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static CategoryWeightRow BuildCategoryWeightRow(
        CategorySuggestionReporter.CategorySourceWeight weight,
        IReadOnlyDictionary<string, int> categoryDeckCounts,
        int totalDeckCount)
    {
        var canonicalKey = CategoryCanonicalizer.CanonicalKey(weight.Category);
        if (!categoryDeckCounts.TryGetValue(canonicalKey, out var deckCount) || totalDeckCount <= 0)
        {
            return new CategoryWeightRow(weight.Category, null, null, weight.SourceCount, weight.SourceTotal);
        }

        var percent = (int)Math.Round((double)deckCount * 100d / totalDeckCount, MidpointRounding.AwayFromZero);
        percent = Math.Clamp(percent, 0, 100);
        return new CategoryWeightRow(weight.Category, deckCount, percent, weight.SourceCount, weight.SourceTotal);
    }
}
