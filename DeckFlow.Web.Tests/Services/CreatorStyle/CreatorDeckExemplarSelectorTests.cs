using DeckFlow.Core.Content;
using DeckFlow.Core.Models;
using DeckFlow.Web.Services.CreatorStyle;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CreatorDeckExemplarSelectorTests
{
    [Fact]
    public void SelectExemplars_FiveDecks_ReturnsDefaultMaximumOfThreeWholeEntries()
    {
        CreatorDeckCacheEntry[] creatorDecks =
        [
            Deck("deck-c", "med", 100),
            Deck("deck-a", "high", 99),
            Deck("deck-e", "low", 101),
            Deck("deck-b", "high", 100),
            Deck("deck-d", "med", 98),
        ];

        IReadOnlyList<CreatorDeckCacheEntry> result = CreatorDeckExemplarSelector.SelectExemplars(creatorDecks, submittedDeckSize: 100);

        Assert.Equal(3, result.Count);
        Assert.Equal(["deck-c", "deck-d", "deck-e"], result.Select(static deck => deck.DeckId).ToArray());
    }

    [Fact]
    public void SelectExemplars_EquivalentInputPermutations_ReturnSameDeterministicOrdering()
    {
        CreatorDeckCacheEntry[] firstOrdering =
        [
            Deck("deck-3", "high", 100),
            Deck("deck-2", "high", 102),
            Deck("deck-1", "med", 100),
            Deck("deck-4", "high", 98),
        ];
        CreatorDeckCacheEntry[] secondOrdering =
        [
            Deck("deck-4", "high", 98),
            Deck("deck-1", "med", 100),
            Deck("deck-2", "high", 102),
            Deck("deck-3", "high", 100),
        ];

        IReadOnlyList<CreatorDeckCacheEntry> first = CreatorDeckExemplarSelector.SelectExemplars(firstOrdering, submittedDeckSize: 100);
        IReadOnlyList<CreatorDeckCacheEntry> second = CreatorDeckExemplarSelector.SelectExemplars(secondOrdering, submittedDeckSize: 100);

        string[] expectedOrder = ["deck-1", "deck-3", "deck-2"];
        Assert.Equal(expectedOrder, first.Select(static deck => deck.DeckId).ToArray());
        Assert.Equal(expectedOrder, second.Select(static deck => deck.DeckId).ToArray());
    }

    [Fact]
    public void SelectExemplars_FewerThanMaximum_ReturnsAllAvailableDecks()
    {
        CreatorDeckCacheEntry[] creatorDecks =
        [
            Deck("deck-2", "med", 101),
            Deck("deck-1", "high", 100),
        ];

        IReadOnlyList<CreatorDeckCacheEntry> result = CreatorDeckExemplarSelector.SelectExemplars(creatorDecks, submittedDeckSize: 100);

        Assert.Equal(2, result.Count);
        Assert.Equal(["deck-2", "deck-1"], result.Select(static deck => deck.DeckId).ToArray());
    }

    [Fact]
    public void SelectExemplars_EmptyCorpus_ReturnsEmptyList()
    {
        IReadOnlyList<CreatorDeckCacheEntry> result = CreatorDeckExemplarSelector.SelectExemplars([], submittedDeckSize: 100);

        Assert.Empty(result);
    }

    [Fact]
    public void SelectExemplars_NullDeckList_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CreatorDeckExemplarSelector.SelectExemplars(null!, submittedDeckSize: 100));
    }

    private static CreatorDeckCacheEntry Deck(string deckId, string confidenceMarker, int size)
        => new()
        {
            CreatorSlug = "snail",
            DeckId = deckId,
            ContentHash = $"{deckId}-hash",
            Size = size,
            ConfidenceMarker = confidenceMarker,
            Entries = Array.Empty<DeckEntry>(),
            CachedUtc = new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero),
        };
}
