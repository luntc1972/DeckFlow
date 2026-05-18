namespace DeckFlow.Core.Models;

/// <summary>
/// A single card entry on one of a deck's boards.
/// </summary>
public sealed record DeckEntry
{
    /// <summary>The card's printed name as supplied by the source.</summary>
    public required string Name { get; init; }

    /// <summary>Lowercased card name with Unicode punctuation collapsed, used for lookup keys.</summary>
    public required string NormalizedName { get; init; }

    /// <summary>Number of copies of this card on the board.</summary>
    public required int Quantity { get; init; }

    /// <summary>Board slot this entry belongs to (e.g., "mainboard", "commander", "sideboard").</summary>
    public required string Board { get; init; }

    /// <summary>Three-to-five character Scryfall set code, or <see langword="null"/> if not specified.</summary>
    public string? SetCode { get; init; }

    /// <summary>Scryfall collector number within the set, or <see langword="null"/> if not specified.</summary>
    public string? CollectorNumber { get; init; }

    /// <summary>Comma-separated category tags assigned to the card, or <see langword="null"/> if untagged.</summary>
    public string? Category { get; init; }

    /// <summary><see langword="true"/> if the card is marked as a foil printing.</summary>
    public bool IsFoil { get; init; }
}
