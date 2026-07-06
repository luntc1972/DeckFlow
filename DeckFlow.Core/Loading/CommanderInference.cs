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
}
