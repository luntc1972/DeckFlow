using DeckFlow.Core.Models;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>
/// Defines the main-deck entries shared by creator crawling and exemplar packets.
/// </summary>
internal static class CreatorMainDeck
{
    private static readonly HashSet<string> MainDeckBoards = new(StringComparer.Ordinal) { "commander", "mainboard" };

    /// <summary>
    /// Returns commander and mainboard entries without changing the supplied entries.
    /// </summary>
    /// <param name="entries">Imported or cached deck entries.</param>
    /// <returns>The main-deck entries in their original order.</returns>
    internal static DeckEntry[] GetEntries(IReadOnlyList<DeckEntry> entries)
    {
        // Why: legacy cached rows contain optional boards that must not affect creator measurements or exemplars.
        return entries.Where(entry => MainDeckBoards.Contains(entry.Board)).ToArray();
    }
}
