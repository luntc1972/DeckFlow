namespace DeckFlow.Core.Models;

/// <summary>
/// Holds the parsed deck entries for both the Moxfield source deck and the Archidekt target deck.
/// </summary>
public sealed record LoadedDecks(List<DeckEntry> MoxfieldEntries, List<DeckEntry> ArchidektEntries);
