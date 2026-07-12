using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for creator-vs-baseline category lift calculations.
/// </summary>
public sealed class LiftCalculatorTests
{
    /// <summary>
    /// Verifies the lift metric demotes staple-like pairs with larger global marginals.
    /// </summary>
    [Fact]
    public void ComputeLift_DemotesStaplePairsComparedToRareCreatorFavoredPairs()
    {
        var creatorDecks = new[]
        {
            Sample("deck-1", Entry("Card AB"), Entry("Card CD")),
            Sample("deck-2", Entry("Card AB"), Entry("Card CD")),
            Sample("deck-3", Entry("Card A"), Entry("Card C")),
            Sample("deck-4", Entry("Card B"), Entry("Card D")),
        };
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Card AB"] = ["Ramp", "Draw"],
            ["Card CD"] = ["Tokens", "Sacrifice"],
            ["Card A"] = ["Ramp"],
            ["Card B"] = ["Draw"],
            ["Card C"] = ["Tokens"],
            ["Card D"] = ["Sacrifice"],
        };
        var baseline = new GlobalCategoryBaseline
        {
            TotalDecks = 100,
            DecksWithCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ramp"] = 80,
                ["Draw"] = 75,
                ["Tokens"] = 20,
                ["Sacrifice"] = 10,
            },
            DecksWithCategoryPair = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Draw|Ramp"] = 60,
                ["Sacrifice|Tokens"] = 5,
            },
        };

        var lift = LiftCalculator.ComputeLift(creatorDecks, cardCategories, baseline);
        var staplePair = Assert.Single(lift, item => item.CategoryA == "Draw" && item.CategoryB == "Ramp");
        var rarerPair = Assert.Single(lift, item => item.CategoryA == "Sacrifice" && item.CategoryB == "Tokens");

        Assert.Equal(2, staplePair.CreatorDecksWithBoth);
        Assert.Equal(2, rarerPair.CreatorDecksWithBoth);
        Assert.True(staplePair.Lift < rarerPair.Lift);
    }

    /// <summary>
    /// Verifies missing baseline categories are omitted instead of producing invalid numeric values.
    /// </summary>
    [Fact]
    public void ComputeLift_OmitsPairsWhenBaselineCategoryIsMissing()
    {
        var creatorDecks = new[]
        {
            Sample("deck-1", Entry("Card A"), Entry("Card B")),
        };
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Card A"] = ["Ramp"],
            ["Card B"] = ["Experimental"],
        };
        var baseline = new GlobalCategoryBaseline
        {
            TotalDecks = 100,
            DecksWithCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ramp"] = 40,
            },
            DecksWithCategoryPair = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        };

        var lift = LiftCalculator.ComputeLift(creatorDecks, cardCategories, baseline);

        Assert.Empty(lift);
    }

    /// <summary>
    /// Verifies pair lookup uses the canonical sorted category key regardless of input order.
    /// </summary>
    [Fact]
    public void ComputeLift_UsesSortedPairKeysAndOrderIndependentPairs()
    {
        var creatorDecks = new[]
        {
            Sample("deck-1", Entry("Card A"), Entry("Card B")),
            Sample("deck-2", Entry("Card B")),
        };
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Card A"] = ["Tokens"],
            ["Card B"] = ["Sacrifice"],
        };
        var baseline = new GlobalCategoryBaseline
        {
            TotalDecks = 10,
            DecksWithCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Tokens"] = 2,
                ["Sacrifice"] = 5,
            },
            DecksWithCategoryPair = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Sacrifice|Tokens"] = 1,
            },
        };

        var lift = LiftCalculator.ComputeLift(creatorDecks, cardCategories, baseline);
        var pair = Assert.Single(lift);

        Assert.Equal("Sacrifice", pair.CategoryA);
        Assert.Equal("Tokens", pair.CategoryB);
        Assert.Equal(1.0d / 2.0d / (0.5d * 0.2d), pair.Lift, precision: 10);
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
