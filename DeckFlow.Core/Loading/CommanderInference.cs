using DeckFlow.Core.Models;

namespace DeckFlow.Core.Loading;

/// <summary>
/// Infers the commander(s) of a Commander deck when the source did not tag a
/// <c>commander</c> board — i.e. a Moxfield plaintext export, where the commander (or
/// partner pair) is the leading card(s) and no <c>Commander</c> section header is present.
/// </summary>
public static class CommanderInference
{
    /// <summary>
    /// Returns the names of the leading entries that should be treated as the deck's
    /// commander(s) when no entry is already flagged on the <c>commander</c> board.
    /// </summary>
    /// <remarks>
    /// By Moxfield export convention the commander (or partner pair) appears first in the
    /// list. This mirrors the heuristic the deck-analysis packet path uses: take the first
    /// one or two leading quantity-1 entries, then apply a third-entry alphabetical guard so
    /// an alphabetically-sorted mainboard (no partner) is not mistaken for a partner pair.
    /// </remarks>
    /// <param name="entries">Parsed deck entries in source order.</param>
    /// <returns>
    /// The inferred commander name(s); empty when an explicit <c>commander</c> entry already
    /// exists, when the list is empty, or when no leading commander can be inferred.
    /// </returns>
    public static IReadOnlyList<string> InferLeadingCommanderNames(IReadOnlyList<DeckEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        // An explicit commander board wins — nothing to infer.
        if (entries.Any(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase)))
        {
            return Array.Empty<string>();
        }

        if (entries.Count == 0)
        {
            return Array.Empty<string>();
        }

        var leadingOneOfs = entries
            .TakeWhile(entry => entry.Quantity == 1
                && !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        // Keep this structure-only Take(2) + alphabetical guard intact. Wave 2 recovers real
        // partner/background pairs post-resolve in ManabaseAnalysisService using eligibility,
        // because Core alone cannot distinguish "partner 2" from an alphabetized mainboard card.
        // Third-entry alphabetical guard: if the second leading card sorts before the third,
        // the run is just an alphabetized mainboard, so only the first card is the commander.
        if (leadingOneOfs.Count == 2 && entries.Count > 2)
        {
            var thirdEntry = entries[2];
            if (string.Compare(leadingOneOfs[1].Name, thirdEntry.Name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                leadingOneOfs = leadingOneOfs.Take(1).ToList();
            }
        }

        return leadingOneOfs.Count > 0
            ? leadingOneOfs.Select(entry => entry.Name).ToList()
            : Array.Empty<string>();
    }

    /// <summary>
    /// Reflags inferred leading commander entries onto the <c>commander</c> board.
    /// </summary>
    /// <param name="entries">Parsed deck entries in source order.</param>
    /// <returns>
    /// The original list when an explicit commander board already exists or none can be inferred;
    /// otherwise a new list with the inferred commander entry or entries reflagged.
    /// </returns>
    public static List<DeckEntry> ReflagInferredCommanders(List<DeckEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        IReadOnlyList<string> commanderNames = InferLeadingCommanderNames(entries);
        if (commanderNames.Count == 0)
        {
            return entries;
        }

        var commanderNameSet = commanderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Only reflag the analyzed boards. The inferred commander is always a leading mainboard
        // entry, so restricting the promotion here keeps a same-named sideboard/maybeboard copy
        // from being pulled into the analyzed set as a second "commander".
        return entries
            .Select(entry => commanderNameSet.Contains(entry.Name)
                && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                ? entry with { Board = "commander" }
                : entry)
            .ToList();
    }
}
