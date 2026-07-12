namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Typed global baseline for lift calculations across processed harvested decks only.
/// Pair keys in <see cref="DecksWithCategoryPair"/> use the canonical sorted <c>catA|catB</c> format.
/// </summary>
public sealed record GlobalCategoryBaseline
{
    /// <summary>Total distinct processed decks represented by the shared deck-to-category aggregate.</summary>
    public required int TotalDecks { get; init; }

    /// <summary>Distinct processed deck counts by included category.</summary>
    public required IReadOnlyDictionary<string, int> DecksWithCategory { get; init; }

    /// <summary>Distinct processed deck counts by canonical sorted category-pair key.</summary>
    public required IReadOnlyDictionary<string, int> DecksWithCategoryPair { get; init; }
}
