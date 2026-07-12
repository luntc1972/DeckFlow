using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;

namespace DeckFlow.Core.Knowledge.MeasuredStyleExtraction;

/// <summary>
/// Pure helper for multi-bucket category counting across creator deck samples.
/// </summary>
public static class CategoryCounter
{
    /// <summary>
    /// Counts strategy-relevant category buckets for a single creator deck sample.
    /// </summary>
    /// <param name="sample">Deck sample to count.</param>
    /// <param name="cardCategories">Resolved category map keyed by card name.</param>
    /// <returns>Per-category card counts for the supplied deck.</returns>
    public static IReadOnlyDictionary<string, int> CountPerDeck(
        CreatorDeckSample sample,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardCategories)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(cardCategories);

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in sample.Entries)
        {
            foreach (var category in GetIncludedCategories(entry, cardCategories))
            {
                counts[category] = counts.TryGetValue(category, out var count)
                    ? count + entry.Quantity
                    : entry.Quantity;
            }
        }

        return counts;
    }

    /// <summary>
    /// Returns the distinct strategy-relevant categories present in a deck sample.
    /// </summary>
    /// <param name="sample">Deck sample to inspect.</param>
    /// <param name="cardCategories">Resolved category map keyed by card name.</param>
    /// <returns>Distinct included category labels present in the deck.</returns>
    public static IReadOnlySet<string> DeckCategoryPresence(
        CreatorDeckSample sample,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardCategories)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(cardCategories);

        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in sample.Entries)
        {
            foreach (var category in GetIncludedCategories(entry, cardCategories))
            {
                present.Add(category);
            }
        }

        return present;
    }

    /// <summary>
    /// Computes mean per-deck counts for each category bucket across the creator sample.
    /// </summary>
    /// <param name="samples">Creator deck samples to aggregate.</param>
    /// <param name="cardCategories">Resolved category map keyed by card name.</param>
    /// <returns>Mean per-deck count for each observed category.</returns>
    public static IReadOnlyDictionary<string, double> AggregateCounts(
        IReadOnlyList<CreatorDeckSample> samples,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardCategories)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(cardCategories);

        if (samples.Count == 0)
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }

        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var sample in samples)
        {
            foreach (var pair in CountPerDeck(sample, cardCategories))
            {
                totals[pair.Key] = totals.TryGetValue(pair.Key, out var total)
                    ? total + pair.Value
                    : pair.Value;
            }
        }

        return totals.ToDictionary(
            pair => pair.Key,
            pair => pair.Value / (double)samples.Count,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetIncludedCategories(
        DeckEntry entry,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardCategories)
    {
        if (!TryGetCategories(entry, cardCategories, out var categories))
        {
            return Array.Empty<string>();
        }

        return CategoryFilter.IncludedOrFallback(categories)
            .Where(CategoryFilter.IsIncluded)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryGetCategories(
        DeckEntry entry,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardCategories,
        out IReadOnlyList<string> categories)
    {
        if (cardCategories.TryGetValue(entry.Name, out var namedCategories))
        {
            categories = namedCategories;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(entry.NormalizedName) &&
            cardCategories.TryGetValue(entry.NormalizedName, out var normalizedCategories))
        {
            categories = normalizedCategories;
            return true;
        }

        categories = Array.Empty<string>();
        return false;
    }
}
