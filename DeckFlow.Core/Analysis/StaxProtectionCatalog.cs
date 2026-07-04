namespace DeckFlow.Core.Analysis;

/// <summary>
/// Curated coarse-presence catalog for stax/taxation and protection staples.
/// </summary>
public static class StaxProtectionCatalog
{
    private static readonly HashSet<string> StaxCards = new(StringComparer.OrdinalIgnoreCase)
    {
        "Blood Moon",
        "Collector Ouphe",
        "Drannith Magistrate",
        "Grand Arbiter Augustin IV",
        "Root Maze",
        "Rule of Law",
        "Sphere of Resistance",
        "Static Orb",
        "Thalia Guardian of Thraben",
        "Trinisphere",
        "Winter Orb",
    };

    private static readonly HashSet<string> ProtectionCards = new(StringComparer.OrdinalIgnoreCase)
    {
        "Boros Charm",
        "Clever Concealment",
        "Deflecting Swat",
        "Flawless Maneuver",
        "Heroic Intervention",
        "Teferi's Protection",
        "Veil of Summer",
    };

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
