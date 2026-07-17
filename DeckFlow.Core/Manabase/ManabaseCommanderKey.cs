using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Builds the canonical lookup key for a commander (or partner pair) shared by the EDHREC
/// averages generator and <c>ManabaseBaselineProvider</c>. Each name is normalized separately
/// with <see cref="CardNormalizer.Normalize"/> BEFORE joining (normalizing a joined pair would
/// truncate at the " / " MDFC separator); pair components are ordinal-sorted so partner order
/// never matters; "||" cannot occur in a normalized name (punctuation is stripped), so pair keys
/// can never collide with lone-commander keys.
/// </summary>
public static class ManabaseCommanderKey
{
    /// <summary>Canonical key for a commander or partner pair. Blank partner = lone commander.</summary>
    public static string Create(string name, string? partnerName = null)
    {
        string first = CardNormalizer.Normalize(name);
        if (string.IsNullOrWhiteSpace(partnerName))
        {
            return first;
        }

        string second = CardNormalizer.Normalize(partnerName);
        return string.CompareOrdinal(first, second) <= 0
            ? $"{first}||{second}"
            : $"{second}||{first}";
    }
}
