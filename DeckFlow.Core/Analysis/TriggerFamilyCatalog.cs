using System.Text.RegularExpressions;

namespace DeckFlow.Core.Analysis;

/// <summary>Detects commander trigger families and cards that enable them.</summary>
public static class TriggerFamilyCatalog
{
    /// <summary>The family for commanders that care about their controller gaining life.</summary>
    public const string Lifegain = "lifegain";

    private const RegexOptions PatternOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly TriggerFamily[] Families =
    [
        new(Lifegain,
        [
            // The recipient must be the controller; a gain effect alone is not a payoff.
            new Regex(@"\bwhen(?:ever)? you gain(?: or lose)? life\b", PatternOptions),
            // End-step and other conditional rewards also care about an earlier gain.
            new Regex(@"\b(?:if|as long as) you (?:gain|gained|have gained) life\b", PatternOptions),
        ],
        [
            // Skip immediate trigger/condition prefixes so a payoff cannot enable itself.
            new Regex(@"(?<!whenever )(?<!when )(?<!if )(?<!each time )\byou (?:may )?gain (?!control\b)(?:life\b|[^.,;:\r\n]*?\blife\b)", PatternOptions),
            // Both printed and granted lifelink supply the event under this vocabulary.
            new Regex(@"(?<!lose )(?<!loses )\blifelink\b", PatternOptions),
        ]),
    ];

    /// <summary>Returns activated family keys in catalog order, without duplicates.</summary>
    public static IReadOnlyList<string> GetActiveFamilies(IReadOnlyList<string?>? commanderTexts)
    {
        if (commanderTexts is null || commanderTexts.Count == 0)
        {
            return Array.Empty<string>();
        }

        List<string>? active = null;
        foreach (var family in Families)
        {
            for (int i = 0; i < commanderTexts.Count; i++)
            {
                if (MatchesAny(commanderTexts[i], family.CommanderPatterns))
                {
                    (active ??= new List<string>()).Add(family.Key);
                    break;
                }
            }
        }

        return active is null ? Array.Empty<string>() : active.AsReadOnly();
    }

    /// <summary>Returns whether a card enables any known active family.</summary>
    public static bool IsEnabler(IReadOnlyList<string>? activeFamilies, string? oracleText)
    {
        if (activeFamilies is null || activeFamilies.Count == 0 || string.IsNullOrWhiteSpace(oracleText))
        {
            return false;
        }

        foreach (var family in Families)
        {
            for (int i = 0; i < activeFamilies.Count; i++)
            {
                if (string.Equals(activeFamilies[i], family.Key, StringComparison.Ordinal)
                    && MatchesAny(oracleText, family.CardPatterns))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MatchesAny(string? oracleText, Regex[] patterns)
    {
        if (string.IsNullOrWhiteSpace(oracleText))
        {
            return false;
        }

        foreach (var pattern in patterns)
        {
            if (pattern.IsMatch(oracleText))
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct TriggerFamily(string Key, Regex[] CommanderPatterns, Regex[] CardPatterns);
}
