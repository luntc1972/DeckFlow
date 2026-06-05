using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Reporting;

/// <summary>
/// Derives category suggestions for a card from its appearances in crawled deck entries.
/// </summary>
public static class CategorySuggestionReporter
{
    /// <summary>Returns suggested categories for <paramref name="cardName"/> from the supplied deck entries.</summary>
    /// <param name="entries">Deck entries to inspect.</param>
    /// <param name="cardName">Card name to match.</param>
    /// <returns>Suggested category labels, falling back to the original labels when all are excluded.</returns>
    public static IReadOnlyList<string> SuggestCategories(IEnumerable<DeckEntry> entries, string cardName)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);

        var normalizedName = CardNormalizer.Normalize(cardName);
        var categories = entries
            .Where(entry => string.Equals(entry.NormalizedName, normalizedName, StringComparison.Ordinal))
            .SelectMany(entry => SplitCategories(entry.Category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return CategoryFilter.IncludedOrFallback(categories);
    }

    /// <summary>Returns a text report listing the supplied category suggestions for <paramref name="cardName"/>.</summary>
    /// <param name="categories">Category suggestions to format.</param>
    /// <param name="cardName">Card name those suggestions apply to.</param>
    /// <returns>A newline-delimited suggestion list, or a no-results message when no suggestions exist.</returns>
    public static string ToText(IEnumerable<string> categories, string cardName)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);

        var items = categories.ToList();
        if (items.Count == 0)
        {
            return $"No deck-local category suggestion found for {cardName}.";
        }

        return string.Join(Environment.NewLine, items.Select(category => $"- {category}"));
    }

    private static IEnumerable<string> SplitCategories(string? categoryText)
    {
        if (string.IsNullOrWhiteSpace(categoryText))
        {
            yield break;
        }

        foreach (var item in categoryText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return item;
        }
    }
}
