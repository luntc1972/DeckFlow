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
            .Where(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .Select(entry => entry.Name)
            .FirstOrDefault();
}
