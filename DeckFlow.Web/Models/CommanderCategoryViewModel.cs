using DeckFlow.Core.Reporting;

namespace DeckFlow.Web.Models;

/// <summary>View model for the commander category exploration workflow.</summary>
public sealed class CommanderCategoryViewModel
{
    /// <summary>Deck workflow tab that should render as active.</summary>
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.CommanderCategories;
    /// <summary>Commander category lookup request echoed back to the Razor view.</summary>
    public CommanderCategoryRequest Request { get; init; } = new();
    /// <summary>Harvested category rows matched for the requested commander.</summary>
    public IReadOnlyList<CategoryKnowledgeRow> CategoryRows { get; init; } = Array.Empty<CategoryKnowledgeRow>();
    /// <summary>Aggregated category summaries for the requested commander.</summary>
    public IReadOnlyList<CommanderCategorySummary> CategorySummaries { get; init; } = Array.Empty<CommanderCategorySummary>();
    /// <summary>Error message shown when the commander category lookup fails.</summary>
    public string? ErrorMessage { get; init; }
    /// <summary>Number of harvested decks contributing category data.</summary>
    public int HarvestedDeckCount { get; init; }
    /// <summary>Whether the lookup produced at least one category summary.</summary>
    public bool HasResults => CategorySummaries.Count > 0;
    /// <summary>Deck-count totals for cards included in the category rows.</summary>
    public CardDeckTotals CardDeckTotals { get; init; } = CardDeckTotals.Empty;
}
