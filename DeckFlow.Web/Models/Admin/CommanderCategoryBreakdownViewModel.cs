using DeckFlow.Web.Models;

namespace DeckFlow.Web.Models.Admin;

/// <summary>
/// Category summaries and sample size for a harvested commander.
/// </summary>
public sealed record CommanderCategoryBreakdownViewModel
{
    /// <summary>Commander name requested by the operator.</summary>
    public string CommanderName { get; init; } = string.Empty;

    /// <summary>Category summaries returned by the commander category aggregation.</summary>
    public IReadOnlyList<CommanderCategorySummary> Summaries { get; init; } = Array.Empty<CommanderCategorySummary>();

    /// <summary>Number of harvested decks observed for the commander.</summary>
    public int CommanderDeckCount { get; init; }
}
