namespace DeckFlow.Core.Models;

/// <summary>
/// Records a card that exists in both decks but with differing set/collector-number printings, along with the user's resolution choice.
/// </summary>
public sealed record PrintingConflict
{
    /// <summary>Display name of the card that has a printing mismatch.</summary>
    public required string CardName { get; init; }

    /// <summary>The Moxfield deck's version of the card (set code, collector number).</summary>
    public required DeckEntry MoxfieldVersion { get; init; }

    /// <summary>The Archidekt deck's version of the card (set code, collector number).</summary>
    public required DeckEntry ArchidektVersion { get; init; }

    /// <summary>Which printing the user chose to keep, or <see cref="PrintingChoice.Unresolved"/> if not yet decided.</summary>
    public PrintingChoice Resolution { get; init; } = PrintingChoice.Unresolved;
}
