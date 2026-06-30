using DeckFlow.Core.Analysis;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Covers <see cref="DeckStatAggregator"/> composition tallies and mana-value parsing.
/// </summary>
public sealed class DeckStatAggregatorTests
{
    private static DeckStatCardInput Card(int qty, string type, string oracle, string mana)
        => new(qty, type, oracle, mana);

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("{0}", 0)]
    [InlineData("{3}", 3)]
    [InlineData("{3}{B}{B}", 5)]
    [InlineData("{X}{R}", 1)]      // X = 0, R = 1
    [InlineData("{W/U}{2}", 3)]    // hybrid = 1, generic 2
    [InlineData("{C}{C}", 2)]      // colorless symbols = 1 each
    public void EstimateManaValue_SumsSymbols(string? manaCost, int expected)
    {
        Assert.Equal(expected, DeckStatAggregator.EstimateManaValue(manaCost));
    }

    [Fact]
    public void Compute_CountsLandsTowardCurveLowBucketAndExcludesFromAverage()
    {
        var summary = DeckStatAggregator.Compute(new[]
        {
            Card(10, "Basic Land — Island", "", ""),
            Card(1, "Land", "{T}: Add {C}{C}.", ""),
            Card(1, "Instant", "Counter target spell.", "{U}"),     // mv 1
            Card(1, "Sorcery", "Draw a card.", "{3}"),              // mv 3
        });

        Assert.Equal(13, summary.Cards);
        Assert.Equal(11, summary.Lands);
        // 11 lands + the mv<=1 instant all land in the "0-1" bucket.
        Assert.Equal(12, summary.ManaCurve["0-1"]);
        Assert.Equal(1, summary.ManaCurve["3"]);
        // average mana value over the 2 non-land cards: (1 + 3) / 2 = 2.00
        Assert.Equal(2.00m, summary.AverageManaValue);
    }

    [Fact]
    public void Compute_TalliesRolesViaClassifierAndRespectsQuantity()
    {
        var summary = DeckStatAggregator.Compute(new[]
        {
            Card(2, "Artifact", "{T}: Add one mana of any color.", "{2}"),   // ramp x2
            Card(1, "Instant", "Counter target spell. Draw a card.", "{U}"), // interaction + draw
            Card(1, "Sorcery", "Destroy all creatures.", "{2}{W}{W}"),       // wipe
            Card(1, "Creature — Avatar", "Reanimate target creature.", "{4}{B}{B}"), // creature + recursion
            Card(1, "Enchantment", "You win the game.", "{5}"),              // closing power
        });

        Assert.Equal(6, summary.Cards);
        Assert.Equal(0, summary.Lands);
        Assert.Equal(1, summary.Creatures);
        Assert.Equal(2, summary.Ramp);          // quantity-weighted
        Assert.Equal(1, summary.Draw);
        Assert.Equal(1, summary.Interaction);
        Assert.Equal(1, summary.Wipes);
        Assert.Equal(1, summary.Recursion);
        Assert.Equal(1, summary.ClosingPower);
    }

    [Fact]
    public void Compute_TalliesNewSignalFields()
    {
        var summary = DeckStatAggregator.Compute(new[]
        {
            Card(1, "Sorcery", "Search your library for a card, then shuffle.", "{1}{B}"), // tutor x1
            Card(2, "Artifact", "{T}: Add {C}{C}.", ""),                                   // fast mana x2 (MV 0)
            Card(1, "Instant", "Counter target spell.", "{U}{U}"),                          // counter x1
            Card(1, "Sorcery", "Draw two cards.", "{1}{U}"),                                // ramp/draw <=MV2 x1
        });

        Assert.Equal(1, summary.Tutors);
        Assert.Equal(2, summary.FastMana);              // quantity-weighted
        Assert.Equal(1, summary.Counters);
        Assert.Equal(1, summary.RampDrawUnderThreeMv);
    }

    [Fact]
    public void Compute_NoMatchingCards_LeavesNewSignalFieldsZero()
    {
        var summary = DeckStatAggregator.Compute(new[]
        {
            Card(1, "Creature — Beast", "", "{2}{G}"),  // matches none of the four new signals
        });

        Assert.Equal(0, summary.Tutors);
        Assert.Equal(0, summary.FastMana);
        Assert.Equal(0, summary.Counters);
        Assert.Equal(0, summary.RampDrawUnderThreeMv);
    }

    [Fact]
    public void Compute_SkipsNonPositiveQuantities()
    {
        var summary = DeckStatAggregator.Compute(new[]
        {
            Card(0, "Instant", "Counter target spell.", "{U}"),
            Card(-3, "Land", "", ""),
            Card(1, "Creature — Beast", "", "{2}{G}"),
        });

        Assert.Equal(1, summary.Cards);
        Assert.Equal(0, summary.Lands);
        Assert.Equal(1, summary.Creatures);
        Assert.Equal(0, summary.Interaction);
    }

    [Fact]
    public void Compute_EmptyInput_ReturnsZeroedSummary()
    {
        var summary = DeckStatAggregator.Compute(Array.Empty<DeckStatCardInput>());

        Assert.Equal(0, summary.Cards);
        Assert.Equal(0, summary.Lands);
        Assert.Equal(0m, summary.AverageManaValue);
        Assert.Equal(0, summary.ManaCurve["5+"]);
    }
}
