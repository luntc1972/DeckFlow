using DeckFlow.Core.Models;

namespace DeckFlow.Core.Filtering;

/// <summary>
/// Provides filtering helpers that remove unwanted deck entries before export or comparison.
/// </summary>
public static class DeckEntryFilter
{
    /// <summary>Returns deck entries that are not assigned to the maybeboard.</summary>
    /// <param name="entries">Deck entries to filter.</param>
    /// <returns>Deck entries whose board is not the maybeboard.</returns>
    public static List<DeckEntry> ExcludeMaybeboard(IEnumerable<DeckEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries
            .Where(entry => !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
