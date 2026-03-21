using System;
using System.Collections.Generic;
using DeckSyncWorkbench.Core.Models;
using DeckSyncWorkbench.Core.Reporting;

namespace DeckSyncWorkbench.Web.Models.Api;

/// <summary>
/// Response payload returned from the card suggestion API.
/// </summary>
public sealed class CategorySuggestionApiResponse
{
    /// <summary>
    /// Card name that was queried.
    /// </summary>
    public string CardName { get; init; } = string.Empty;
    /// <summary>
    /// Exact category text from the optional reference deck.
    /// </summary>
    public string ExactCategoriesText { get; init; } = string.Empty;
    public string ExactSuggestionContextText { get; init; } = string.Empty;
    /// <summary>
    /// Categories inferred from the local cache.
    /// </summary>
    public string InferredCategoriesText { get; init; } = string.Empty;
    public string InferredSuggestionContextText { get; init; } = string.Empty;
    /// <summary>
    /// Fallback themes inferred from EDHREC data.
    /// </summary>
    public string EdhrecCategoriesText { get; init; } = string.Empty;
    public string EdhrecSuggestionContextText { get; init; } = string.Empty;
    public bool HasExactCategories { get; init; }
    public bool HasInferredCategories { get; init; }
    public bool HasEdhrecCategories { get; init; }
    public string? SuggestionSourceSummary { get; init; }
    public bool NoSuggestionsFound { get; init; }
    public string? NoSuggestionsMessage { get; init; }
    public CardDeckTotals CardDeckTotals { get; init; } = CardDeckTotals.Empty;
    public int AdditionalDecksFound { get; init; }
    public bool CacheSweepPerformed { get; init; }
}

/// <summary>
/// Response payload returned from the commander category API.
/// </summary>
public sealed class CommanderCategoryApiResponse
{
    /// <summary>
    /// Commander name that was queried.
    /// </summary>
    public string CommanderName { get; init; } = string.Empty;
    public int CardRowCount { get; init; }
    public int CategoryCount { get; init; }
    public int HarvestedDeckCount { get; init; }
    public int AdditionalDecksFound { get; init; }
    public CardDeckTotals CardDeckTotals { get; init; } = CardDeckTotals.Empty;
    public bool CacheSweepPerformed { get; init; }
    public IReadOnlyList<CommanderCategorySummaryDto> Summaries { get; init; } = Array.Empty<CommanderCategorySummaryDto>();
    public string? NoResultsMessage { get; init; }
}

/// <summary>
/// Simple DTO describing a commander category summary.
/// </summary>
public sealed class CommanderCategorySummaryDto
{
    public string Category { get; init; } = string.Empty;
    public int Count { get; init; }
    public int DeckCount { get; init; }
}
