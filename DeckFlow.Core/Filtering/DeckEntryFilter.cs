using DeckFlow.Core.Models;

namespace DeckFlow.Core.Filtering;

/// <summary>
/// Provides filtering helpers that remove unwanted deck entries before export or comparison.
/// </summary>
public static class DeckEntryFilter
{
    public static List<DeckEntry> ExcludeMaybeboard(IEnumerable<DeckEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries
            .Where(entry => !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
