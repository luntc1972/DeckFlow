namespace DeckFlow.Web.Services.CutLab;

/// <summary>Cut Lab legality helpers for cards that can appear in multiple copies.</summary>
public static class CutLabLegality
{
    private const int LegalMultipleCap = 150;

    private static readonly IReadOnlySet<string> AnyNumberNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Persistent Petitioners",
            "Dragon's Approach",
            "Relentless Rats",
            "Rat Colony",
            "Shadowborn Apostle",
            "Slime Against Humanity",
            "Templar Knights",
            "Nazgûl",
            "Seven Dwarves",
        };

    /// <summary>True when the provided card name can legally appear in multiple copies.</summary>
    /// <param name="cardName">Display card name.</param>
    /// <returns><see langword="true"/> for basics and the recognized any-number cards.</returns>
    public static bool IsLegalMultiple(string cardName)
    {
        ArgumentNullException.ThrowIfNull(cardName);

        return CutLabBasicLands.Contains(cardName) || AnyNumberNames.Contains(cardName);
    }

    /// <summary>Returns the legal quantity cap for the provided card name in Cut Lab tuning.</summary>
    /// <param name="cardName">Display card name.</param>
    /// <returns>The legal multiple cap, or 1 for singleton cards.</returns>
    public static int LegalMax(string cardName)
    {
        ArgumentNullException.ThrowIfNull(cardName);

        return IsLegalMultiple(cardName) ? LegalMultipleCap : 1;
    }
}
