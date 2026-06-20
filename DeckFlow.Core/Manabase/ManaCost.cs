using System.Text.RegularExpressions;

namespace DeckFlow.Core.Manabase;

/// <summary>The parsed colored-pip content of a mana cost.</summary>
public sealed record ParsedManaCost
{
    /// <summary>Total mana value (generic + colored + colorless; X counts as 0).</summary>
    public required int ManaValue { get; init; }

    /// <summary>Hard single-color pip counts. Hybrid/Phyrexian pips are excluded (flexible).</summary>
    public required IReadOnlyDictionary<ManaColor, int> Pips { get; init; }

    /// <summary>Distinct colors with at least one hard pip — used to flag gold costs.</summary>
    public int DistinctColors => Pips.Count(p => p.Value > 0 && p.Key != ManaColor.Colorless);

    /// <summary>True if the cost contains X/Y/Z — the printed mana value is not the real cast turn.</summary>
    public bool HasVariableCost { get; init; }
}

/// <summary>
/// Parses Scryfall-style mana cost strings (e.g. <c>{2}{U}{U}</c>) into mana value and
/// hard colored pips. Hybrid (<c>{U/R}</c>), Phyrexian (<c>{U/P}</c>) and twobrid
/// (<c>{2/U}</c>) symbols are deliberately not counted as hard single-color pips because
/// they can be paid more than one way — Karsten counts them against combined sources.
/// </summary>
public static class ManaCostParser
{
    private static readonly Regex Symbol = new(@"\{([^}]+)\}", RegexOptions.Compiled);

    /// <summary>Parse a mana cost string. A null/empty cost yields zero mana value and no pips.</summary>
    public static ParsedManaCost Parse(string? manaCost)
    {
        var pips = new Dictionary<ManaColor, int>();
        int manaValue = 0;
        bool variable = false;

        if (string.IsNullOrWhiteSpace(manaCost))
        {
            return new ParsedManaCost { ManaValue = 0, Pips = pips };
        }

        foreach (Match match in Symbol.Matches(manaCost))
        {
            string token = match.Groups[1].Value.Trim().ToUpperInvariant();

            // Hybrid family: no hard single-color pip. Twobrid ({2/U}) is worth 2 mana value;
            // colored/Phyrexian hybrid ({U/R}, {U/P}) is worth 1.
            if (token.Contains('/'))
            {
                string head = token.Split('/')[0];
                manaValue += int.TryParse(head, out int twobrid) ? twobrid : 1;
                continue;
            }

            if (token is "X" or "Y" or "Z")
            {
                variable = true;
                continue;
            }

            if (int.TryParse(token, out int generic))
            {
                manaValue += generic;
                continue;
            }

            ManaColor? color = MapSymbol(token);
            if (color is null)
            {
                continue;
            }

            manaValue += 1;
            pips[color.Value] = pips.GetValueOrDefault(color.Value) + 1;
        }

        return new ParsedManaCost { ManaValue = manaValue, Pips = pips, HasVariableCost = variable };
    }

    /// <summary>Map a single Scryfall color/colorless letter to a <see cref="ManaColor"/>.</summary>
    public static ManaColor? MapSymbol(string token) => token switch
    {
        "W" => ManaColor.White,
        "U" => ManaColor.Blue,
        "B" => ManaColor.Black,
        "R" => ManaColor.Red,
        "G" => ManaColor.Green,
        "C" => ManaColor.Colorless,
        "S" => ManaColor.Colorless, // snow treated as its own "color"; mapped to colorless bucket
        _ => null,
    };
}
