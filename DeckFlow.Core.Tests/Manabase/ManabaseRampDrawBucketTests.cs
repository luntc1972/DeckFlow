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
    public void Classify_GiftTreasureReminderOnly_IsNotBudgetRamp()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(new[]
        {
            Nonland("Bilbo's Gambit", 3, "Instant", "Gift a Treasure (You may promise an opponent a gift as you cast this spell. If you do, they create a Treasure token before its other effects. It's an artifact with \"{T}, Sacrifice this token: Add one mana of any color.\")\nReturn target spell to its owner's hand. If the gift was promised, players can't cast spells this turn."),
        });

        Assert.Equal(0.0, deck.RampPieceCount);
        Assert.Equal(0, deck.RampDrawBothCount);
    }

    [Theory]
    [InlineData("When this enchantment enters, create two Treasure tokens.")]
    [InlineData("Whenever this creature deals combat damage to a player, create that many tapped Treasure tokens.")]
    [InlineData("When this Class enters, create two tapped Treasure tokens.")]
    [InlineData("When this creature enters, create X Treasure tokens, where X is the number of artifacts and enchantments your opponents control.")]
    [InlineData("create one Treasure token.")]
    [InlineData("create three Treasure tokens.")]
    [InlineData("create four tapped Treasure tokens.")]
    [InlineData("create five Treasure tokens.")]
    [InlineData("create six Treasure tokens.")]
    [InlineData("create seven Treasure tokens.")]
    [InlineData("create eight Treasure tokens.")]
    [InlineData("create nine Treasure tokens.")]
    [InlineData("create ten Treasure tokens.")]
    [InlineData("create 2 Treasure tokens.")]
    [InlineData("create a number of Treasure tokens equal to your devotion to red.")]
    [InlineData("create a tapped Treasure token.")]
    public void Classify_TreasureCountForms_AreBudgetRamp(string oracleText)
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(new[]
        {
            Nonland("Treasure Maker", 3, "Enchantment", oracleText),
        });

        Assert.Equal(1.0, deck.RampPieceCount);
    }

    [Fact]
    public void Classify_BasicLandcyclingReminderOnly_IsNotBudgetRamp()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(new[]
        {
            Nonland("Landcycler", 5, "Creature — Beast", "Basic landcycling {1}{G} ({1}{G}, Discard this card: Search your library for a basic land card, reveal it, put it into your hand, then shuffle.)"),
        });

        Assert.Equal(0.0, deck.RampPieceCount);
    }

    [Fact]
    public void Classify_LandToBattlefieldOnlyInReminder_EarnsNoRampOrLandCredit()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(new[]
        {
            Nonland("Reminder Seeker", 2, "Creature — Elf", "Reach (Search your library for a basic land card, put it onto the battlefield tapped, then shuffle.)"),
        }, rampCreditV2: true, landRampSim: true);

        Assert.Equal(0.0, deck.RampPieceCount);
        Assert.Equal(0, deck.RampAndDrawUnderThree);
        Assert.DoesNotContain(deck.Sources, source => source.Name == "Reminder Seeker");
    }

    [Fact]
    public void Classify_ZeroCostArtifactWithAddOnlyInReminder_IsNotFastMana()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(new[]
        {
            Nonland("Reminder Trinket", 0, "Artifact", "When this artifact enters, create a Treasure token. (It's an artifact with \"{T}, Sacrifice this token: Add one mana of any color.\")"),
        });

        Assert.Equal(0, deck.FastMana);
    }

    [Fact]
    public void Classify_DrawCardsEqualTo_CountsAsBudgetDrawPiece()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(new[]
        {
            Nonland("Greater Good", 4, "Enchantment", "Sacrifice a creature: Draw cards equal to the sacrificed creature's power, then discard three cards."),
        });

        Assert.Equal(1.0, deck.DrawPieceCount);
        Assert.Equal(0.0, deck.RampPieceCount);
    }

    [Fact]
    public void Classify_BigScore_IsRampAndDrawViaRealTreasureClause()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(new[]
        {
            Nonland("Big Score", 4, "Instant", "As an additional cost to cast this spell, discard a card.\nDraw two cards and create two Treasure tokens. (They're artifacts with \"{T}, Sacrifice this token: Add one mana of any color.\")", "B", "G", "R", "U", "W"),
        });

        Assert.Equal(0.5, deck.RampPieceCount);
        Assert.Equal(0.5, deck.DrawPieceCount);
        Assert.Equal(1, deck.RampDrawBothCount);
    }

    [Theory]
    [InlineData("create two Clue tokens.", "Enchantment", 3, 0.0)]
    [InlineData("Lands you control have \"{T}: Create a Treasure token.\"", "Artifact", 6, 1.0)]
    [InlineData("Target opponent creates a Treasure token.", "Instant", 1, 0.0)]
    public void Classify_TreasureClauseGuards_CountExpectedRamp(string oracleText, string typeLine, double manaValue, double expectedRamp)
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(new[]
        {
            Nonland("Treasure Guard", manaValue, typeLine, oracleText),
        });

        Assert.Equal(expectedRamp, deck.RampPieceCount);
    }

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

    [Fact]
    public void Classify_CommanderDrawText_IsExcludedFromBudget()
    {
        CardFact commander = Nonland("Bello, Bard of the Brambles", 3, "Legendary Creature — Raccoon Bard",
            "During your turn, each non-Equipment artifact and non-Aura enchantment you control with mana value 4 or greater is a 4/4 Elemental creature in addition to its other types and has indestructible, haste, and \"Whenever this creature deals combat damage to a player, draw a card.\"")
        with
        {
            IsCommander = true,
        };
        ManabaseDeck deck = ManabaseClassifier.Classify(new[]
        {
            commander,
            Nonland("Divination", 3, "Sorcery", "Draw two cards."),
        });

        Assert.Equal(1.0, deck.DrawPieceCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Classify_CommanderManaDork_IsExcludedFromBudgetAndLandCredit(bool rampCreditV2)
    {
        CardFact commander = Nonland("Dork Commander", 2, "Legendary Creature — Elf Druid", "{T}: Add {G}.", "G") with { IsCommander = true };
        ManabaseDeck deck = ManabaseClassifier.Classify(new[] { commander }, rampCreditV2: rampCreditV2);

        Assert.Equal((0.0, 0, 0), (deck.RampPieceCount, deck.RampAndDrawUnderThree, deck.RampAndDrawNames.Count));
    }

    [Fact]
    public void Classify_NonCommanderManaDork_StillEarnsBudgetAndLandCredit()
    {
        CardFact card = Nonland("Dork Commander", 2, "Legendary Creature — Elf Druid", "{T}: Add {G}.", "G") with { IsCommander = false };
        ManabaseDeck deck = ManabaseClassifier.Classify(new[] { card }, rampCreditV2: true);

        Assert.Equal(1.0, deck.RampPieceCount);
        Assert.Equal(1, deck.RampAndDrawUnderThree);
        Assert.Equal(new[] { "Dork Commander" }, deck.RampAndDrawNames);
    }

    [Fact]
    public void Classify_CommanderRampAndDraw_IsExcludedFromBothCount()
    {
        CardFact commander = Nonland("Overlap Commander", 3, "Legendary Creature — Elf Druid", "{T}: Add {G}.\n{2}, {T}: Draw a card.", "G") with { IsCommander = true };
        ManabaseDeck deck = ManabaseClassifier.Classify(new[] { commander }, rampCreditV2: true);

        Assert.Equal((0.0, 0.0, 0), (deck.RampPieceCount, deck.DrawPieceCount, deck.RampDrawBothCount));
    }
}
