using DeckFlow.Core.Models;

namespace DeckFlow.Web.Services.Packets;

/// <summary>
/// Shared first-match commander reflag helper (Cluster B from Phase 83 research). Body is a
/// verbatim copy of the byte-identical private methods previously duplicated in
/// <c>DeckComparisonService</c> and <c>MetaGapService</c>.
/// </summary>
/// <remarks>
/// <see cref="DeckFlow.Web.Services.DeckAnalysisPacketService"/>'s commander reflag (its <c>ResolvePreScryfallCommanderState</c>
/// / <c>BuildAsync</c> logic) is INTENTIONALLY NOT covered by this helper — it reflags every entry
/// whose name is in a partner-aware inferred-commander-names set, not just the first match, which
/// is load-bearing for partner-pair commander decks. Do not route Analysis through this helper.
/// </remarks>
internal static class DeckEntryReflagHelper
{
    /// <summary>
    /// Reflags the FIRST <c>Quantity == 1</c> name-match to <c>Board = "commander"</c> and leaves
    /// every other entry unchanged. No match leaves the list unchanged; entries with
    /// <c>Quantity &gt; 1</c> are never reflagged.
    /// </summary>
    internal static List<DeckEntry> ReflagCommanderEntry(List<DeckEntry> source, string commanderName)
    {
        var matched = false;
        var result = new List<DeckEntry>(source.Count);
        foreach (var entry in source)
        {
            if (!matched
                && entry.Quantity == 1
                && string.Equals(entry.Name, commanderName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(entry with { Board = "commander" });
                matched = true;
            }
            else
            {
                result.Add(entry);
            }
        }
        return result;
    }
}
