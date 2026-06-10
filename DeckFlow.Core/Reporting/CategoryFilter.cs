namespace DeckFlow.Core.Reporting;

/// <summary>
/// Filters out generic card-type categories (Creature, Instant, etc.) that carry no deck-strategy value.
/// </summary>
public static class CategoryFilter
{
    private static readonly HashSet<string> ExcludedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Artifact",
        "Artifacts",
        "Battle",
        "Battles",
        "Creature",
        "Creatures",
        "Enchantment",
        "Enchantments",
        "Instant",
        "Instants",
        "Planeswalker",
        "Planeswalkers",
        "Sorcery",
        "Sorceries",
    };

    /// <summary>Returns whether <paramref name="category"/> is a strategy-relevant category.</summary>
    /// <param name="category">Category label to evaluate.</param>
    /// <returns><see langword="true"/> when the category is non-empty and not excluded; otherwise <see langword="false"/>.</returns>
    public static bool IsIncluded(string? category)
    {
        return !string.IsNullOrWhiteSpace(category) && !ExcludedCategories.Contains(category);
    }

    /// <summary>
    /// Returns non-generic categories when present, otherwise preserves the original category labels.
    /// </summary>
    /// <param name="categories">Observed category labels.</param>
    /// <returns>Filtered category labels, or the original labels when none survive the filter.</returns>
    public static IReadOnlyList<string> IncludedOrFallback(IEnumerable<string> categories)
    {
        var items = categories
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .ToList();
        var included = items
            .Where(IsIncluded)
            .ToList();

        return included.Count > 0 ? included : items;
    }
}
