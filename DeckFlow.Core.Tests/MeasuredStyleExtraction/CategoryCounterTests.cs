using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for multi-bucket measured-style category counting.
/// </summary>
public sealed class CategoryCounterTests
{
    /// <summary>
    /// Verifies a multi-category card increments every qualifying bucket.
    /// </summary>
    [Fact]
    public void CountPerDeck_MultiCategoryCardIncrementsEveryReturnedBucket()
    {
        var sample = Sample(
            "deck-1",
            Entry("Growth Spiral"),
            Entry("Cultivate"),
            Entry("Ponder"));
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Growth Spiral"] = ["Ramp", "Draw"],
            ["Cultivate"] = ["Ramp"],
            ["Ponder"] = ["Draw"],
        };

        var counts = CategoryCounter.CountPerDeck(sample, cardCategories);

        Assert.Equal(2, counts["Ramp"]);
        Assert.Equal(2, counts["Draw"]);
    }

    /// <summary>
    /// Verifies creature-only labels are excluded entirely from the returned buckets.
    /// </summary>
    [Fact]
    public void CountPerDeck_ExcludesCreatureOnlyLabels()
    {
        var sample = Sample("deck-1", Entry("Solemn Simulacrum"));
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Solemn Simulacrum"] = ["Creature"],
        };

        var counts = CategoryCounter.CountPerDeck(sample, cardCategories);

        Assert.False(counts.ContainsKey("Creature"));
        Assert.Empty(counts);
    }

    /// <summary>
    /// Verifies deck-level category presence remains boolean per category.
    /// </summary>
    [Fact]
    public void DeckCategoryPresence_DedupesRepeatedCategoryMatchesWithinOneDeck()
    {
        var sample = Sample(
            "deck-1",
            Entry("Llanowar Elves"),
            Entry("Fyndhorn Elves"),
            Entry("Growth Spiral"));
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Llanowar Elves"] = ["Ramp"],
            ["Fyndhorn Elves"] = ["Ramp"],
            ["Growth Spiral"] = ["Ramp", "Draw"],
        };

        var presence = CategoryCounter.DeckCategoryPresence(sample, cardCategories);

        Assert.Equal(2, presence.Count);
        Assert.Contains("Ramp", presence);
        Assert.Contains("Draw", presence);
    }

    /// <summary>
    /// Verifies aggregate counts report the mean per-deck bucket count across all creator decks.
    /// </summary>
    [Fact]
    public void AggregateCounts_ReturnsMeanPerDeckCountsAcrossSamples()
    {
        var samples = new[]
        {
            Sample("deck-1", Entry("Cultivate"), Entry("Growth Spiral")),
            Sample("deck-2", Entry("Ponder")),
        };
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cultivate"] = ["Ramp"],
            ["Growth Spiral"] = ["Ramp", "Draw"],
            ["Ponder"] = ["Draw"],
        };

        var aggregates = CategoryCounter.AggregateCounts(samples, cardCategories);

        Assert.Equal(1.0, aggregates["Ramp"]);
        Assert.Equal(1.0, aggregates["Draw"]);
    }

    private static CreatorDeckSample Sample(string deckId, params DeckEntry[] entries)
    {
        return new CreatorDeckSample
        {
            DeckId = deckId,
            Entries = entries,
            CardCount = entries.Sum(entry => entry.Quantity),
            ConfidenceMarker = "trusted",
        };
    }

    private static DeckEntry Entry(string name, int quantity = 1)
    {
        return new DeckEntry
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = quantity,
            Board = "mainboard",
        };
    }
}
