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

    public static bool IsIncluded(string? category)
    {
        return !string.IsNullOrWhiteSpace(category) && !ExcludedCategories.Contains(category);
    }
}
