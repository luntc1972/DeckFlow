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

    /// <summary>Returns a merged, ranked category list across all suggestion sources.</summary>
    /// <param name="exact">Categories from exact reference-deck matches.</param>
    /// <param name="inferred">Categories inferred from cached local knowledge.</param>
    /// <param name="edhrec">Categories inferred from EDHREC.</param>
    /// <param name="tagger">Categories returned by Scryfall Tagger.</param>
    /// <returns>Merged category labels ordered by cross-source agreement.</returns>
    public static IReadOnlyList<string> Merge(
        IEnumerable<string> exact,
        IEnumerable<string> inferred,
        IEnumerable<string> edhrec,
        IEnumerable<string> tagger)
    {
        ArgumentNullException.ThrowIfNull(exact);
        ArgumentNullException.ThrowIfNull(inferred);
        ArgumentNullException.ThrowIfNull(edhrec);
        ArgumentNullException.ThrowIfNull(tagger);

        var merged = new Dictionary<string, MergeEntry>(StringComparer.Ordinal);
        MergeSource(exact, SourceKind.Exact, merged);
        MergeSource(inferred, SourceKind.Inferred, merged);
        MergeSource(edhrec, SourceKind.Edhrec, merged);
        MergeSource(tagger, SourceKind.Tagger, merged);

        return merged.Values
            .OrderByDescending(entry => entry.SourceCount)
            .ThenBy(entry => entry.DisplayLabel, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.DisplayLabel)
            .ToList();
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

    private static void MergeSource(
        IEnumerable<string> categories,
        SourceKind source,
        IDictionary<string, MergeEntry> merged)
    {
        var sourceEntries = categories
            .Where(category => !CategoryFilter.IsJunk(category))
            .Select(category => new
            {
                DisplayLabel = CategoryCanonicalizer.Canonicalize(category),
                CanonicalKey = CategoryCanonicalizer.CanonicalKey(category),
            })
            .GroupBy(category => category.CanonicalKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        foreach (var category in sourceEntries)
        {
            if (!merged.TryGetValue(category.CanonicalKey, out var entry))
            {
                merged[category.CanonicalKey] = new MergeEntry(category.DisplayLabel, source);
                continue;
            }

            entry.SourceCount++;
            if (source == SourceKind.Tagger)
            {
                entry.DisplayLabel = category.DisplayLabel;
                entry.PreferredSource = source;
            }
        }
    }

    private enum SourceKind
    {
        Exact,
        Inferred,
        Edhrec,
        Tagger,
    }

    private sealed class MergeEntry
    {
        public MergeEntry(string displayLabel, SourceKind preferredSource)
        {
            DisplayLabel = displayLabel;
            PreferredSource = preferredSource;
            SourceCount = 1;
        }

        public string DisplayLabel { get; set; }

        public SourceKind PreferredSource { get; set; }

        public int SourceCount { get; set; }
    }
}
