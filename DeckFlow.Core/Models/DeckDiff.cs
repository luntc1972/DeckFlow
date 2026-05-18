namespace DeckFlow.Core.Models;

/// <summary>
/// The result of comparing two deck lists, categorised into adds, count mismatches, removals, and printing conflicts.
/// </summary>
public sealed record DeckDiff(
    IReadOnlyList<DeckEntry> ToAdd,
    IReadOnlyList<DeckEntry> CountMismatch,
    IReadOnlyList<DeckEntry> OnlyInArchidekt,
    IReadOnlyList<PrintingConflict> PrintingConflicts);
