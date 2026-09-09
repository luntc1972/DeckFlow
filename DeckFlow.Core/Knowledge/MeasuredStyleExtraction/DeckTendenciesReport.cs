namespace DeckFlow.Core.Knowledge.MeasuredStyleExtraction;

/// <summary>
/// Deterministic per-creator deck tendency summary derived from crawled deck samples.
/// </summary>
public sealed record DeckTendenciesReport
{
    /// <summary>Total creator deck samples represented in this report.</summary>
    public required int DeckCount { get; init; }

    /// <summary>Deck-level rows in source input order.</summary>
    public required IReadOnlyList<DeckTendencyDeckRow> Decks { get; init; }

    /// <summary>Repeated non-commander cards present in at least two decks.</summary>
    public required IReadOnlyList<RepeatCardRow> RepeatCards { get; init; }

    /// <summary>Repeated commander-board cards present in at least two decks.</summary>
    public required IReadOnlyList<RepeatCardRow> RepeatCommanders { get; init; }

    /// <summary>Quantity-weighted category tendencies across the creator deck sample.</summary>
    public required IReadOnlyList<CategoryTendencyRow> CategoryTendencies { get; init; }
}

/// <summary>
/// Deck-level report row for one creator deck sample.
/// </summary>
public sealed record DeckTendencyDeckRow
{
    /// <summary>Stable host-supplied deck identifier.</summary>
    public required string DeckId { get; init; }

    /// <summary>Optional host-supplied deck name when known.</summary>
    public string? DeckName { get; init; }

    /// <summary>Declared deck card count from the source sample.</summary>
    public required int CardCount { get; init; }

    /// <summary>Optional parent folder name captured by the host tier.</summary>
    public string? FolderName { get; init; }

    /// <summary>Commander-board card names in source entry order.</summary>
    public required IReadOnlyList<string> Commanders { get; init; }
}

/// <summary>
/// Repeated card row keyed by printed card name.
/// </summary>
public sealed record RepeatCardRow
{
    /// <summary>Printed card name used for report display.</summary>
    public required string CardName { get; init; }

    /// <summary>Distinct creator decks containing the card.</summary>
    public required int DeckCount { get; init; }

    /// <summary>Share of creator decks containing the card.</summary>
    public required double Frequency { get; init; }

    /// <summary>True when the card is a creator-personal staple at the default threshold.</summary>
    public required bool IsPersonalStaple { get; init; }
}

/// <summary>
/// Quantity-weighted category tendency row for one included category bucket.
/// </summary>
public sealed record CategoryTendencyRow
{
    /// <summary>Included category label.</summary>
    public required string Category { get; init; }

    /// <summary>Mean quantity-weighted count across all creator decks.</summary>
    public required double AverageCountPerDeck { get; init; }

    /// <summary>Share of creator decks containing the category.</summary>
    public required double PresenceRatio { get; init; }

    /// <summary>Optional global processed-deck presence ratio for this category.</summary>
    public double? BaselinePresenceRatio { get; init; }

    /// <summary>Optional creator-vs-global lift for this category.</summary>
    public double? Lift { get; init; }
}
