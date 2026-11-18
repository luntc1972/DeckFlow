using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Analysis;

/// <summary>
/// Curated coarse-presence catalog for stax/taxation and protection staples.
/// </summary>
public static class StaxProtectionCatalog
{
    // Why: Scryfall, pasted or foil-marked deck names and listed names reduce to one CardNormalizer key.
    private static readonly string[] StaxNames =
    [
        "Blood Moon",
        "Collector Ouphe",
        "Damping Sphere",
        "Drannith Magistrate",
        "Grand Arbiter Augustin IV",
        "Root Maze",
        "Rule of Law",
        "Sphere of Resistance",
        "Static Orb",
        "Thalia, Guardian of Thraben",
        "Trinisphere",
        "Winter Orb",
    ];

    private static readonly string[] ProtectionNames =
    [
        "Boros Charm",
        "Clever Concealment",
        "Deflecting Swat",
        "Flawless Maneuver",
        "Heroic Intervention",
        "Teferi's Protection",
        "Veil of Summer",
    ];

    private static readonly NormalizedCardNameSet StaxCards = new(StaxNames);
    private static readonly NormalizedCardNameSet ProtectionCards = new(ProtectionNames);

    /// <summary>Gets every curated stax and protection name for exact-name snapshot validation.</summary>
    internal static IReadOnlyList<string> AllNames => [.. StaxNames, .. ProtectionNames];

    /// <summary>
    /// Returns <see langword="true"/> when the supplied card name is a curated stax or taxation staple.
    /// </summary>
    /// <param name="name">Card name to check.</param>
    public static bool IsStax(string name)
        => !string.IsNullOrWhiteSpace(name) && StaxCards.Contains(name);

    /// <summary>
    /// Returns <see langword="true"/> when the supplied card name is a curated protection staple.
    /// </summary>
    /// <param name="name">Card name to check.</param>
    public static bool IsProtection(string name)
        => !string.IsNullOrWhiteSpace(name) && ProtectionCards.Contains(name);
}
