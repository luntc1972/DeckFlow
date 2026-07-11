namespace DeckFlow.Core.Loading;

/// <summary>
/// Evaluates whether a card's type line and oracle text make it eligible to be a commander.
/// </summary>
public static class CommanderEligibility
{
    /// <summary>
    /// Returns whether the supplied type line and oracle text represent a commander-eligible card.
    /// </summary>
    /// <param name="typeLine">The card type line.</param>
    /// <param name="oracleText">The card oracle text, including joined face text when applicable.</param>
    /// <returns><see langword="true"/> when the card is eligible to be a commander.</returns>
    public static bool IsEligible(string typeLine, string? oracleText)
    {
        typeLine ??= string.Empty;
        oracleText ??= string.Empty;

        if (IsLegendaryType(typeLine, "Creature"))
        {
            return true;
        }

        if (IsLegendaryType(typeLine, "Vehicle"))
        {
            return true;
        }

        if (typeLine.Contains("Planeswalker", StringComparison.OrdinalIgnoreCase)
            && oracleText.Contains("can be your commander", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsLegendaryType(typeLine, "Enchantment") && HasBackgroundTypeToken(typeLine);
    }

    internal static bool IsLegendaryType(string typeLine, string requiredType) =>
        typeLine.Contains("Legendary", StringComparison.OrdinalIgnoreCase)
        && typeLine.Contains(requiredType, StringComparison.OrdinalIgnoreCase);

    private static bool HasBackgroundTypeToken(string typeLine) =>
        typeLine
            .Split([' ', '—'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => string.Equals(token, "Background", StringComparison.OrdinalIgnoreCase));
}
