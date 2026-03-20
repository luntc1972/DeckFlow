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
    public string CardName { get; init; } = string.Empty;
    public string ExactCategoriesText { get; init; } = string.Empty;
    public string ExactSuggestionContextText { get; init; } = string.Empty;
    public string InferredCategoriesText { get; init; } = string.Empty;
    public string InferredSuggestionContextText { get; init; } = string.Empty;
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
}

/// <summary>
/// Response payload returned from the commander category API.
/// </summary>
public sealed class CommanderCategoryApiResponse
{
    public string CommanderName { get; init; } = string.Empty;
    public int CardRowCount { get; init; }
    public int CategoryCount { get; init; }
    public int HarvestedDeckCount { get; init; }
    public int AdditionalDecksFound { get; init; }
    public CardDeckTotals CardDeckTotals { get; init; } = CardDeckTotals.Empty;
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
