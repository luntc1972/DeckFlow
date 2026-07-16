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
        "Mainboard",
        "Maybeboard",
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

    /// <summary>Returns whether <paramref name="category"/> is syntactic junk rather than a useful strategy label.</summary>
    /// <param name="category">Category label to evaluate.</param>
    /// <returns><see langword="true"/> when the category is null, blank, contains digits or sentence punctuation, has five or more words, contains non-ASCII text, or otherwise matches the junk heuristics.</returns>
    public static bool IsJunk(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return true;
        }

        var trimmed = category.Trim();
        if (trimmed.Any(char.IsAsciiDigit) || trimmed.Length <= 1 || trimmed.Length > 40)
        {
            return true;
        }

        if (trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length >= 5)
        {
            return true;
        }

        if (trimmed.Contains('?') ||
            trimmed.Contains('!') ||
            trimmed.Contains("...", StringComparison.Ordinal) ||
            trimmed.Contains(',') ||
            trimmed.Contains(';') ||
            trimmed.EndsWith(".", StringComparison.Ordinal))
        {
            return true;
        }

        return trimmed.Any(character => character > 127);
    }

    /// <summary>
    /// Returns non-generic categories when present, otherwise preserves the original category labels.
    /// </summary>
    /// <param name="categories">Observed category labels.</param>
    /// <returns>Filtered category labels, or the original labels when none survive the filter.</returns>
    public static IReadOnlyList<string> IncludedOrFallback(IEnumerable<string> categories)
    {
        var nonEmptyItems = categories
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .ToList();
        var nonJunkItems = nonEmptyItems
            .Where(category => !IsJunk(category))
            .ToList();
        var included = nonJunkItems
            .Where(IsIncluded)
            .ToList();

        return included.Count > 0 ? included : nonJunkItems;
    }
}
