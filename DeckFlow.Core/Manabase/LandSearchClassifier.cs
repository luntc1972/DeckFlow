using System.Text.RegularExpressions;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Classifies lands whose library searches target only lands.
/// </summary>
public static partial class LandSearchClassifier
{
    // Why: IsTutorCard and the Web fetch-land gate need the same definition of a land-only fetch land.
    /// <summary>
    /// Determines whether a card's front face is a land and all of its library searches are land-only.
    /// </summary>
    /// <param name="typeLine">The full card type line, if available.</param>
    /// <param name="oracleText">The oracle text to search for library-search clauses.</param>
    /// <returns>True if the front face is a land and every library search targets only lands.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="oracleText"/> is null.</exception>
    public static bool IsLandOnlyFetchLand(string? typeLine, string oracleText)
    {
        ArgumentNullException.ThrowIfNull(oracleText);
        if (!CardTypeLine.FrontFace(typeLine).Contains("Land", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }


        int searchCount = LibrarySearchPhraseRegex().Count(oracleText);
        MatchCollection matches = LibrarySearchTargetRegex().Matches(oracleText);

        // Why: every search must parse as a land-only target; an unparsed search may find a nonland card.
        if (searchCount == 0 || matches.Count != searchCount)
        {
            return false;
        }

        foreach (Match match in matches)
        {
            string[] tokens = match.Groups["target"].Value.ToLowerInvariant().Replace(',', ' ')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (!tokens.All(LandSearchTokens.Contains) || !tokens.Any(LandSearchTypeTokens.Contains))
            {
                return false;
            }
        }

        return true;
    }

    // Why: even a fully recognized target needs a land type to establish that it only finds lands.
    // Declared before LandSearchTokens, whose static initializer reads it.
    private static readonly HashSet<string> LandSearchTypeTokens = new(StringComparer.Ordinal)
    {
        "land", "lands", "plains", "island", "swamp", "mountain", "forest",
    };

    // Why: an unknown qualifier may allow the search to find a nonland card.
    private static readonly HashSet<string> LandSearchTokens = new(
        [.. LandSearchTypeTokens, "a", "an", "up", "to", "one", "two", "three", "or", "and", "basic", "snow", "card", "cards"],
        StringComparer.Ordinal);

    // Why: an unrecognized search form should retain its tutor role.
    [GeneratedRegex(@"search your library for (?<target>.+?\bcards?\b)(?=,|\.| and put| then| that share a land type)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LibrarySearchTargetRegex();

    // Why: counts every library search so one the target regex cannot parse disqualifies the card.
    [GeneratedRegex("search your library for", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LibrarySearchPhraseRegex();
}
