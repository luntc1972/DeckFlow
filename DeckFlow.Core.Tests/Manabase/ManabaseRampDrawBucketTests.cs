using System.Collections.Generic;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates the advisory-only ramp/draw bucket counts projected by <see cref="ManabaseClassifier"/>.
/// These counts are distinct from the historic land-target credit path and must not change it.
/// </summary>
public sealed class ManabaseRampDrawBucketTests
{
    private static CardFact Nonland(string name, double manaValue, string typeLine, string oracleText, params string[] producedMana) => new()
    {
        Name = name,
        Quantity = 1,
        ManaValue = manaValue,
        ManaCost = "{1}",
        TypeLine = typeLine,
        OracleText = oracleText,
        ProducedMana = producedMana,
    };

    private static CardFact Land(string name, string oracleText, params string[] producedMana) => new()
    {
        Name = name,
        Quantity = 1,
        ManaValue = 0,
        TypeLine = "Basic Land",
        OracleText = oracleText,
        ProducedMana = producedMana,
    };

    [Fact]
    public void Classify_CantripRock_SplitsOverlapAcrossBothBuckets()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(
            new[]
            {
                Nonland("Mind Stone", 2, "Artifact", "{T}: Add {C}. {1}, {T}, Sacrifice Mind Stone: Draw a card.", "C"),
            });

        Assert.Equal(0.5, deck.RampPieceCount);
        Assert.Equal(0.5, deck.DrawPieceCount);
        Assert.Equal(1, deck.RampDrawBothCount);
    }

    [Fact]
    public void Classify_PureRampAndPureDrawCards_CountExpectedBuckets()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(
            new[]
            {
                Nonland("Sol Ring", 1, "Artifact", "{T}: Add {C}{C}.", "C"),
                Nonland("Divination", 3, "Sorcery", "Draw two cards."),
            });

        Assert.Equal(1.0, deck.RampPieceCount);
        Assert.Equal(1.0, deck.DrawPieceCount);
        Assert.Equal(0, deck.RampDrawBothCount);
    }

    [Fact]
    public void Classify_WheelAndRepeatableDraw_CountAsDrawPieces()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(
            new[]
            {
                Nonland("Windfall", 3, "Sorcery", "Each player discards their hand, then draws seven cards."),
                Nonland("Mystic Remora", 1, "Enchantment", "Whenever an opponent casts a noncreature spell, you may draw a card unless that player pays {4}."),
            });

        Assert.Equal(0.0, deck.RampPieceCount);
        Assert.Equal(2.0, deck.DrawPieceCount);
        Assert.Equal(0, deck.RampDrawBothCount);
    }

    [Fact]
    public void Classify_ProducedManaOnlyCreature_IsNeitherSourceNorRampCredit()
    {
        // Efficacy R2 finding H2: bare produced_mana with no repeatable front-face "<cost>: Add"
        // ability no longer classifies as a rock/dork AT ALL (real-world shape: Treasure-makers
        // like Dockside Extortionist). It must not become a weighted source, a budget ramp piece,
        // or a land-target credit — a deck with it prices lands exactly like a vanilla creature.
        CardFact oneDropDorkWithoutBroadRampText = Nonland(
            "Treasure Trigger Creature",
            1,
            "Creature — Bird",
            "Flying.",
            "W",
            "U",
            "B",
            "R",
            "G");

        CardFact vanillaOneDrop = Nonland(
            "Flying Men",
            1,
            "Creature — Bird",
            "Flying.");

        CardFact[] deckWithDork =
        {
            oneDropDorkWithoutBroadRampText,
            Land("Forest", "{T}: Add {G}.", "G"),
            Land("Forest", "{T}: Add {G}.", "G"),
            Land("Forest", "{T}: Add {G}.", "G"),
            Land("Forest", "{T}: Add {G}.", "G"),
        };

        CardFact[] deckWithVanillaCreature =
        {
            vanillaOneDrop,
            Land("Forest", "{T}: Add {G}.", "G"),
            Land("Forest", "{T}: Add {G}.", "G"),
            Land("Forest", "{T}: Add {G}.", "G"),
            Land("Forest", "{T}: Add {G}.", "G"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(deckWithDork, rampCreditV2: false);
        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);
        ManabaseReport baselineReport = ManabaseAnalyzer.Analyze(
            ManabaseClassifier.Classify(deckWithVanillaCreature, rampCreditV2: false));

        Assert.Equal(0.0, deck.RampPieceCount);
        Assert.Equal(0.0, deck.DrawPieceCount);
        Assert.Equal(0, deck.RampAndDrawUnderThree);
        Assert.DoesNotContain(deck.Sources, s => s.Name == "Treasure Trigger Creature");
        Assert.NotNull(report.LandTarget);
        Assert.NotNull(baselineReport.LandTarget);
        Assert.Equal(baselineReport.TargetLands, report.TargetLands);
        Assert.Equal(baselineReport.LandTarget!.FinalTarget, report.LandTarget!.FinalTarget);
    }
}
