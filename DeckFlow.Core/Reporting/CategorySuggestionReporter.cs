using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Reporting;

/// <summary>
/// Derives category suggestions for a card from its appearances in crawled deck entries.
/// </summary>
public static class CategorySuggestionReporter
{
    /// <summary>Weighted merged category metadata for Suggest Categories UI rendering.</summary>
    public sealed record CategorySourceWeight
    {
        /// <summary>Initializes one weighted merged category row.</summary>
        public CategorySourceWeight(string category, int sourceCount, int sourceTotal)
        {
            Category = category;
            SourceCount = sourceCount;
            SourceTotal = sourceTotal;
        }

        /// <summary>The merged category label shown to the user.</summary>
        public string Category { get; init; }

        /// <summary>The number of contributing sources that suggested this category.</summary>
        public int SourceCount { get; init; }

        /// <summary>The total number of sources that contributed at least one merged category.</summary>
        public int SourceTotal { get; init; }
    }

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

    /// <summary>Returns merged categories together with source-agreement weights.</summary>
    public static IReadOnlyList<CategorySourceWeight> MergeWeighted(
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
        var sourceTotal = 0;
        sourceTotal += MergeSource(exact, SourceKind.Exact, merged);
        sourceTotal += MergeSource(inferred, SourceKind.Inferred, merged);
        sourceTotal += MergeSource(edhrec, SourceKind.Edhrec, merged);
        sourceTotal += MergeSource(tagger, SourceKind.Tagger, merged);

        return merged.Values
            .OrderByDescending(entry => entry.SourceCount)
            .ThenByDescending(entry => entry.Authority)
            .ThenBy(entry => entry.DisplayLabel, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new CategorySourceWeight(entry.DisplayLabel, entry.SourceCount, sourceTotal))
            .ToList();
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

    private static int MergeSource(
        IEnumerable<string> categories,
        SourceKind source,
        IDictionary<string, MergeEntry> merged)
    {
        var sourceEntries = GetSourceEntries(categories);
        if (sourceEntries.Count == 0)
        {
            return 0;
        }

        foreach (var category in sourceEntries)
        {
            if (!merged.TryGetValue(category.CanonicalKey, out var entry))
            {
                merged[category.CanonicalKey] = new MergeEntry(category.DisplayLabel, source);
                continue;
            }

            entry.SourceCount++;
            entry.Authority = Math.Max(entry.Authority, GetAuthority(source));
            if (source == SourceKind.Tagger)
            {
                entry.DisplayLabel = category.DisplayLabel;
                entry.PreferredSource = source;
            }
        }

        return 1;
    }

    private static IReadOnlyList<(string DisplayLabel, string CanonicalKey)> GetSourceEntries(IEnumerable<string> categories)
        => categories
            .Where(category => !CategoryFilter.IsJunk(category))
            .Select(category => (
                DisplayLabel: CategoryCanonicalizer.Canonicalize(category),
                CanonicalKey: CategoryCanonicalizer.CanonicalKey(category)))
            .GroupBy(category => category.CanonicalKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

    private static int GetAuthority(SourceKind source) => source switch
    {
        SourceKind.Tagger => 3,
        SourceKind.Exact => 3,
        SourceKind.Inferred => 2,
        SourceKind.Edhrec => 1,
        _ => 0,
    };

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
            Authority = GetAuthority(preferredSource);
        }

        public string DisplayLabel { get; set; }

        public SourceKind PreferredSource { get; set; }

        public int SourceCount { get; set; }

        public int Authority { get; set; }
    }
}
