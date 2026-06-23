using System.Text.RegularExpressions;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Parses how MUCH mana a source produces in a single activation from its oracle text — the
/// quantity side of MQ-02 (Sol Ring = 2, Ancient Tomb = 2, Gilded Lotus = 3). Returns 1 (the safe
/// default) whenever the amount is ambiguous, scaling/conditional, or splits across more than one
/// color. It deliberately does NOT model board-scaling sources (Cabal Coffers / Nykthos / Cradle)
/// or fixed multi-color splits ("Add {W}{U}") — those stay at 1 (conservative, never over-credits).
/// </summary>
/// <remarks>
/// Quantity feeds ONLY the castability simulator's affordability/curve math. It never touches the
/// Karsten color-SOURCE counts (a 2-mana rock is still ONE source of its color), per the phase 70
/// locked decision.
/// </remarks>
public static class ManaProductionAmount
{
    // First "Add <symbols>" clause, e.g. "Add {C}{C}" / "Add {R}{R}{R}" / "Add {2}".
    private static readonly Regex SymbolRun = new(
        @"Add\s+((?:\{[^}]+\}\s*)+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Symbol = new(@"\{([^}]+)\}", RegexOptions.Compiled);

    // Word form, e.g. "Add three mana of any one color" (Gilded Lotus).
    private static readonly Regex WordForm = new(
        @"Add\s+(one|two|three|four|five|six)\s+mana",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Mana produced per activation, parsed from <paramref name="oracleText"/>. Defaults to 1 when
    /// unparseable, scaling, conditional, or split across more than one color.
    /// </summary>
    public static int Parse(string? oracleText)
    {
        if (string.IsNullOrWhiteSpace(oracleText))
        {
            return 1;
        }

        Match symbols = SymbolRun.Match(oracleText);
        if (symbols.Success && TryAmountFromSymbols(symbols.Groups[1].Value, out int symbolAmount))
        {
            return symbolAmount;
        }

        Match words = WordForm.Match(oracleText);
        if (words.Success)
        {
            return WordToNumber(words.Groups[1].Value);
        }

        return 1;
    }

    private static bool TryAmountFromSymbols(string run, out int amount)
    {
        amount = 1;
        int units = 0;
        int distinctColors = 0;
        int colorSeen = 0;

        foreach (Match m in Symbol.Matches(run))
        {
            string token = m.Groups[1].Value.Trim().ToUpperInvariant();

            // Hybrid / Phyrexian / variable / snow symbols are out of scope — bail to the safe default.
            if (token.Contains('/') || token == "X" || token == "S")
            {
                return false;
            }

            if (int.TryParse(token, out int generic))
            {
                units += generic; // "{2}" = two generic mana
                continue;
            }

            units += 1;

            int bit = token switch
            {
                "W" => 1 << 0,
                "U" => 1 << 1,
                "B" => 1 << 2,
                "R" => 1 << 3,
                "G" => 1 << 4,
                _ => 0, // {C} colorless and anything else carry no color
            };

            if (bit != 0 && (colorSeen & bit) == 0)
            {
                colorSeen |= bit;
                distinctColors++;
            }
        }

        // A fixed split across more than one color ("Add {W}{U}") is not the single-chosen-color
        // model MQ-02 supports — keep it at 1 (safe, never over-credits color access).
        if (distinctColors > 1 || units <= 1)
        {
            return false;
        }

        amount = units;
        return true;
    }

    private static int WordToNumber(string word) => word.ToLowerInvariant() switch
    {
        "one" => 1,
        "two" => 2,
        "three" => 3,
        "four" => 4,
        "five" => 5,
        "six" => 6,
        _ => 1,
    };
}
