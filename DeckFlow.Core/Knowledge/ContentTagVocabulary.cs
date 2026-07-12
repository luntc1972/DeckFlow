namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Controlled vocabulary for content tags across archetype, bracket, and card category dimensions.
/// </summary>
public static class ContentTagVocabulary
{
    /// <summary>Allowlisted Commander archetype and strategy tag values.</summary>
    public static readonly IReadOnlySet<string> Archetypes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "voltron",
        "aristocrats",
        "stax",
        "combo",
        "control",
        "tokens",
        "spellslinger",
        "reanimator",
        "blink",
        "tribal",
        "lands",
        "ramp",
        "aggro",
        "midrange",
        "value-engine"
    };

    /// <summary>Allowlisted Wizards February 2025 Commander bracket tag values.</summary>
    public static readonly IReadOnlySet<string> Brackets = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "Exhibition",
        "Core",
        "Upgraded",
        "Optimized",
        "cEDH"
    };

    /// <summary>Allowlisted functional card category tag values.</summary>
    public static readonly IReadOnlySet<string> CardCategories = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "ramp",
        "removal",
        "draw",
        "finishers",
        "win-cons",
        "counter",
        "protection",
        "board-wipe",
        "tutor",
        "recursion",
        "utility"
    };

    /// <summary>D-05 curated "always-strip" staple card set, distinct from the later per-creator >60% staple cut.</summary>
    public static readonly IReadOnlySet<string> Staples = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "Plains",
        "Island",
        "Swamp",
        "Mountain",
        "Forest",
        "Command Tower",
        "Sol Ring",
        "Arcane Signet",
        "Exotic Orchard",
        "Rogue's Passage",
        "Negate"
    };

    /// <summary>
    /// Returns whether a tag value is allowed for the supplied tag dimension.
    /// </summary>
    /// <param name="dimension">Tag dimension matching one of the <see cref="ContentTagDimension"/> constants.</param>
    /// <param name="value">Candidate tag value to validate.</param>
    /// <returns><see langword="true"/> when the value exists in the dimension allowlist.</returns>
    public static bool IsValid(string dimension, string value)
    {
        return dimension switch
        {
            ContentTagDimension.Archetype => Archetypes.Contains(value),
            ContentTagDimension.Bracket => Brackets.Contains(value),
            ContentTagDimension.CardCategory => CardCategories.Contains(value),
            _ => false
        };
    }
}
