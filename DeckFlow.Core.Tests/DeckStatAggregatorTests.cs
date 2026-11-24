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
            Card(1, "Creature — Avatar", "Return target creature card from your graveyard to the battlefield.", "{4}{B}{B}"), // creature + recursion
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
    public void Compute_LandTutorsCountAndFetchLandDoesNot()
    {
        DeckStatSummary stats = DeckStatAggregator.Compute(
        [
            Card(1, "Enchantment Land — Urza's Saga", "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — This Saga gains \"{T}: Add {C}.\"\nII — This Saga gains \"{2}, {T}: Create a 0/0 colorless Construct artifact creature token with 'This token gets +1/+1 for each artifact you control.'\"\nIII — Search your library for an artifact card with mana cost {0} or {1}, put it onto the battlefield, then shuffle.", ""),
            Card(1, "Legendary Land", "At the beginning of your upkeep, if you control three or more artifacts, you gain 1 life.\n{T}: Add {C}.\n{4}, {T}, Sacrifice Inventors' Fair: Search your library for an artifact card, reveal it, put it into your hand, then shuffle. Activate only if you control three or more artifacts.", ""),
            Card(1, "Land", "This land enters tapped.\n{T}: Add {U}.\nTransmute {1}{U}{U} ({1}{U}{U}, Discard this card: Search your library for a card with mana value 0, reveal it, put it into your hand, then shuffle. Transmute only as a sorcery.)", ""),
            Card(1, "Land", "{T}, Pay 1 life, Sacrifice this land: Search your library for an Island or Swamp card, put it onto the battlefield, then shuffle.", "")
        ]);

        Assert.Equal(3, stats.Tutors);
        Assert.Equal(4, stats.Lands);
        Assert.Equal(4, stats.ManaCurve["0-1"]);
        Assert.Equal(0, stats.Ramp);
    }

    [Fact]
    public void Compute_TutorFaceIsNotVetoedByLandSearchFace()
    {
        string[] faces = ["Search your library for a creature card, reveal it, put it into your hand, then shuffle.", "Search your library for a basic land card, put it onto the battlefield tapped, then shuffle."];
        DeckStatCardInput card = Card(1, "Sorcery // Sorcery", string.Join("\n", faces), "{1}{B}") with { FaceOracleTexts = faces };

        DeckStatSummary stats = DeckStatAggregator.Compute([card]);

        Assert.Equal(1, stats.Tutors);
    }

    [Fact]
    public void Compute_InstantFrontMdfc_CountsAsLandAndFrontFaceSpell()
    {
        string[] faces = ["Destroy target creature or planeswalker.", "As this land enters, you may pay 3 life. If you don't, it enters tapped.\n{T}: Add {B}."];
        DeckStatCardInput card = Card(1, "Instant // Land", string.Join("\n", faces), "{2}{B}{B}") with { FaceOracleTexts = faces };

        DeckStatSummary stats = DeckStatAggregator.Compute([card]);

        Assert.Equal(1, stats.Interaction);
        Assert.Equal(1, stats.Lands);
        Assert.Equal(0, stats.Ramp);
        Assert.Equal(1, stats.ManaCurve["0-1"]);
        Assert.Equal(0, stats.ManaCurve["4"]);
        Assert.Equal(0m, stats.AverageManaValue);
    }

    [Fact]
    public void Compute_SorceryFrontMdfc_CountsAsLandAndFrontFaceSpell()
    {
        string[] faces = ["Draw cards equal to the number of cards in your hand plus one. You have no maximum hand size for the rest of the game.", "As this land enters, you may pay 3 life. If you don't, it enters tapped.\n{T}: Add {U}."];
        DeckStatCardInput card = Card(1, "Sorcery // Land", string.Join("\n", faces), "{4}{U}{U}{U}") with { FaceOracleTexts = faces };

        DeckStatSummary stats = DeckStatAggregator.Compute([card]);

        Assert.Equal(1, stats.Draw);
        Assert.Equal(1, stats.Lands);
        Assert.Equal(0, stats.Ramp);
        Assert.Equal(1, stats.ManaCurve["0-1"]);
        Assert.Equal(0, stats.ManaCurve["5+"]);
    }

    [Fact]
    public void Compute_LibraryAndGraveyardSearch_CountsAsTutor()
    {
        string[] faces = ["(As a Siege enters, choose an opponent to protect it. You and others can attack it. When it's defeated, exile it, then cast it transformed.)\nWhen this Siege enters, search your library and/or graveyard for a non-Human creature card with mana value X or less and put it onto the battlefield. If you search your library this way, shuffle.", "Reach\nFor each non-Human creature you control, you may have that creature assign its combat damage as though it weren't blocked."];
        DeckStatSummary stats = DeckStatAggregator.Compute(
        [
            Card(1, "Sorcery", "Search your library and/or graveyard for a creature card with mana value X or less and put it onto the battlefield. If you search your library this way, shuffle. If X is 10 or more, creatures you control get +X/+X and gain haste until end of turn.", "{X}{G}{G}"),
            Card(1, "Battle — Siege // Legendary Creature — Dinosaur", string.Join("\n", faces), "{X}{G}{G}") with { FaceOracleTexts = faces }
        ]);

        Assert.Equal(2, stats.Tutors);
    }

    [Fact]
    public void Compute_LandFrontMdfc_StaysLandOnly()
    {
        string[] faces = ["Riverglide Pathway enters tapped unless you control two or more other lands.", "Lavaglide Pathway enters tapped unless you control two or more other lands."];
        DeckStatCardInput card = Card(1, "Land // Land", string.Join("\n", faces), "") with { FaceOracleTexts = faces };

        DeckStatSummary stats = DeckStatAggregator.Compute([card]);

        Assert.Equal(1, stats.Lands);
        Assert.Equal(1, stats.ManaCurve["0-1"]);
        Assert.Equal(0, stats.Ramp);
    }

    [Fact]
    public void Compute_SpellFrontMdfcWithoutFaceData_StaysLandOnly()
    {
        string oracle = string.Join("\n", "Destroy target creature or planeswalker.", "As this land enters, you may pay 3 life. If you don't, it enters tapped.\n{T}: Add {B}.");
        DeckStatSummary stats = DeckStatAggregator.Compute([Card(1, "Instant // Land", oracle, "{2}{B}{B}")]);

        Assert.Equal(1, stats.Lands);
        Assert.Equal(0, stats.Interaction);
        Assert.Equal(0, stats.Ramp);
    }

    [Fact]
    public void Compute_IslandcyclingSpellCountsAndFetchLandDoesNot()
    {
        DeckStatSummary stats = DeckStatAggregator.Compute([
            Card(1, "Sorcery", "Draw three cards.\nIslandcycling {1} ({1}, Discard this card: Search your library for an Island card, reveal it, put it into your hand, then shuffle.)", "{3}{U}{U}"),
            Card(1, "Sorcery", "Search your library for a card, put that card into your hand, then shuffle.", "{1}{B}"),
            Card(1, "Land", "{T}, Pay 1 life, Sacrifice this land: Search your library for an Island or Swamp card, put it onto the battlefield, then shuffle.", ""),
        ]);

        Assert.Equal(2, stats.Tutors);
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
        Assert.Equal(3, summary.RampDrawUnderThreeMv); // draw spell x1 + mana-symbol fast mana x2
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
