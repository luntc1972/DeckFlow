using DeckFlow.Core.Models;

namespace DeckFlow.Core.Reporting;

/// <summary>
/// Aggregates deck entries by category and returns sorted counts for reporting.
/// </summary>
public static class CategoryCountReporter
{
    /// <summary>Returns category totals aggregated from the supplied deck entries.</summary>
    /// <param name="entries">Deck entries to aggregate.</param>
    /// <returns>Category counts ordered by descending quantity and category name.</returns>
    public static IReadOnlyList<(string Category, int Count)> CountByQuantity(IEnumerable<DeckEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries
            .SelectMany(entry => SplitCategories(entry).Select(category => new { category, entry.Quantity }))
            .GroupBy(item => item.category, StringComparer.OrdinalIgnoreCase)
            .Select(group => (Category: group.First().category, Count: group.Sum(item => item.Quantity)))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Returns a text report listing category totals for the supplied deck entries.</summary>
    /// <param name="entries">Deck entries to aggregate.</param>
    /// <returns>A newline-delimited category-count list, or a no-results message when no categories exist.</returns>
    public static string ToText(IEnumerable<DeckEntry> entries)
    {
        var counts = CountByQuantity(entries);
        if (counts.Count == 0)
        {
            return "No Archidekt categories found.";
        }

        return string.Join(Environment.NewLine, counts.Select(item => $"{item.Category}: {item.Count}"));
    }

    private static IEnumerable<string> SplitCategories(DeckEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Category))
        {
            yield break;
        }

        foreach (var category in entry.Category.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return category;
        }
    }
}
