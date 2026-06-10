using DeckFlow.Core.Models;

namespace DeckFlow.Core.Reporting;

/// <summary>
/// Filters deck entries to those belonging to a specified category for targeted reporting.
/// </summary>
public static class CategoryCardReporter
{
    /// <summary>Returns deck entries whose category list includes <paramref name="category"/>.</summary>
    /// <param name="entries">Deck entries to search.</param>
    /// <param name="category">Category name to match.</param>
    /// <returns>Matching deck entries ordered by quantity and name.</returns>
    public static IReadOnlyList<DeckEntry> CardsInCategory(IEnumerable<DeckEntry> entries, string category)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        return entries
            .Where(entry => HasCategory(entry, category))
            .OrderByDescending(entry => entry.Quantity)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Returns a text report listing deck entries whose category list includes <paramref name="category"/>.</summary>
    /// <param name="entries">Deck entries to search.</param>
    /// <param name="category">Category name to match.</param>
    /// <returns>A newline-delimited card list, or a no-results message when no cards match.</returns>
    public static string ToText(IEnumerable<DeckEntry> entries, string category)
    {
        var matches = CardsInCategory(entries, category);
        if (matches.Count == 0)
        {
            return $"No cards found in category: {category}";
        }

        return string.Join(Environment.NewLine, matches.Select(entry => $"{entry.Quantity} {entry.Name}"));
    }

    private static bool HasCategory(DeckEntry entry, string category)
    {
        if (string.IsNullOrWhiteSpace(entry.Category))
        {
            return false;
        }

        return entry.Category
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(item => string.Equals(item, category, StringComparison.OrdinalIgnoreCase));
    }
}
