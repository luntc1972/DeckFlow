using System;
using System.Collections.Generic;

namespace DeckFlow.Core.Reporting;

/// <summary>
/// Aggregated deck-count statistics for a single card across all harvested decks, broken down by board.
/// </summary>
public sealed record CardDeckTotals(int TotalDeckCount, IReadOnlyDictionary<string, int> BoardDeckCounts)
{
    /// <summary>
    /// Represents an empty set of deck totals.
    /// </summary>
    public static CardDeckTotals Empty { get; } = new(0, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
}
