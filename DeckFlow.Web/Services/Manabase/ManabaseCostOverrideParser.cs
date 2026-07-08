using System.Text.RegularExpressions;
using DeckFlow.Core.Manabase;

namespace DeckFlow.Web.Services.Manabase;

/// <summary>
/// Parses the mana-base "reduced / alternative costs" text box into a name → effective-cost map.
/// Each non-blank line is <c>Card Name: cost</c>; the cost is normalized to canonical braced form
/// (<c>0</c>, <c>{R}</c>, <c>{1}{B}</c>) via <see cref="ManaCostParser.NormalizeToBraced"/> so the
/// Core analyzer can parse it. Unparseable lines are ignored — never fatal.
/// </summary>
public static class ManabaseCostOverrideParser
{
    /// <summary>
    /// The outcome of parsing the override box: the accepted name → braced-cost map plus the raw text
    /// of any non-blank lines that were dropped (bad syntax or an unparseable cost). The malformed
    /// lines let the UI surface "N line(s) not applied" instead of dropping them silently.
    /// </summary>
    /// <param name="Overrides">Accepted overrides (card name → canonical braced cost).</param>
    /// <param name="MalformedLines">Trimmed text of each non-blank line that could not be parsed.</param>
    public sealed record OverrideParseResult(
        IReadOnlyDictionary<string, string> Overrides,
        IReadOnlyList<string> MalformedLines);

    // A cost is accepted only when it is EITHER a run of complete braced tokens ({1}{R}, {U/R})
    // OR bare shorthand of digits + single mana letters (0, 1R, WW). This rejects ambiguous mixed
    // input like "{1}R" (Parse would silently drop the trailing R) and slash shorthand like "U/R"
    // (would tokenize into bogus pips) — those lines are skipped instead of producing wrong math.
    private static readonly Regex ValidCost = new(
        @"^(?:\{[^{}]+\})+$|^[0-9wubrgcsxWUBRGCSX]+$", RegexOptions.Compiled);

    /// <summary>Parse the override text. Returns an empty map for null/blank input.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string? text)
        => ParseWithDiagnostics(text).Overrides;

    /// <summary>
    /// Parse the override text, keeping the raw text of every non-blank line that was dropped so the
    /// caller can tell the user which lines were not applied. Returns an empty map and no malformed
    /// lines for null/blank input.
    /// </summary>
    public static OverrideParseResult ParseWithDiagnostics(string? text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var malformed = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new OverrideParseResult(map, malformed);
        }

        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            // Card names do not contain ':'; the last colon separates the name from the cost.
            int colon = line.LastIndexOf(':');
            if (colon <= 0 || colon == line.Length - 1)
            {
                malformed.Add(line);
                continue;
            }

            string name = line[..colon].Trim();
            string costRaw = line[(colon + 1)..].Trim();
            if (name.Length == 0 || !ValidCost.IsMatch(costRaw))
            {
                malformed.Add(line);
                continue;
            }

            string braced = ManaCostParser.NormalizeToBraced(costRaw);
            ParsedManaCost parsed = ManaCostParser.Parse(braced);

            // A purely numeric cost ("0", "{0}", "{3}") is an intentional free/generic override.
            // Otherwise reject junk that parsed to no real mana (e.g. random letters) rather than
            // silently zeroing a card.
            string bareDigits = costRaw.Replace("{", string.Empty).Replace("}", string.Empty).Trim();
            bool isNumericCost = bareDigits.Length > 0 && bareDigits.All(char.IsDigit);
            if (!isNumericCost && parsed.ManaValue == 0 && parsed.Pips.Count == 0)
            {
                malformed.Add(line);
                continue;
            }

            map[name] = braced; // last line wins on duplicate names
        }

        return new OverrideParseResult(map, malformed);
    }
}
