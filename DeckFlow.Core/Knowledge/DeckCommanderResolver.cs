using DeckFlow.Core.Models;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Resolves one stable commander name from imported deck entries.
/// </summary>
public static class DeckCommanderResolver
{
    /// <summary>
    /// Returns the alphabetically first commander-board entry, or null when no commander exists.
    /// </summary>
    /// <param name="entries">Imported deck entries.</param>
    /// <returns>A stable commander name, or null.</returns>
    public static string? ResolveCommanderName(IEnumerable<DeckEntry> entries)
        => entries
            .Where(IsCommanderEntry)
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .Select(entry => entry.Name)
            .FirstOrDefault();

    /// <summary>
    /// Determines whether an entry is an eligible Commander-board card.
    /// </summary>
    public static bool IsCommanderEntry(DeckEntry entry)
        => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase)
            && entry.Quantity > 0;
}
