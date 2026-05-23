namespace DeckFlow.Core.Reporting;

/// <summary>
/// A single row from the knowledge-cache database representing a card's category assignment in one harvested deck.
/// </summary>
public sealed record DeckCategoryEntry(
    string DeckId,
    string? DeckName,
    string CardName,
    string NormalizedCardName,
    string Category,
    string Board,
    int Count,
    string LastSeenUtc);
