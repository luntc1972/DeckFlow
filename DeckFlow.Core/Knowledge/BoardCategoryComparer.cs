using System;
using System.Collections.Generic;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Case-insensitive equality comparer for (CardName, Category, Board) tuples used in knowledge-cache deduplication.
/// </summary>
public sealed class BoardCategoryComparer : IEqualityComparer<(string CardName, string Category, string Board)>
{
    /// <summary>
    /// Gets the shared comparer instance.
    /// </summary>
    public static BoardCategoryComparer Instance { get; } = new();

    private BoardCategoryComparer()
    {
    }

    /// <summary>
    /// Determines whether two card/category/board tuples are equal using case-insensitive comparisons.
    /// </summary>
    /// <param name="x">First tuple to compare.</param>
    /// <param name="y">Second tuple to compare.</param>
    /// <returns><see langword="true"/> when all tuple fields match; otherwise <see langword="false"/>.</returns>
    public bool Equals((string CardName, string Category, string Board) x, (string CardName, string Category, string Board) y)
    {
        return string.Equals(x.CardName, y.CardName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Category, y.Category, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Board, y.Board, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a case-insensitive hash code for the supplied card/category/board tuple.
    /// </summary>
    /// <param name="obj">Tuple to hash.</param>
    /// <returns>The combined hash code.</returns>
    public int GetHashCode((string CardName, string Category, string Board) obj)
    {
        var nameHash = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.CardName ?? string.Empty);
        var categoryHash = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Category ?? string.Empty);
        var boardHash = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Board ?? string.Empty);
        return HashCode.Combine(nameHash, categoryHash, boardHash);
    }
}
