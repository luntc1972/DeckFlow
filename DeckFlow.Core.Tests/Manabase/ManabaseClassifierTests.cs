using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates the Scryfall-fact classifier: mana-cost parsing and the build of a
/// <see cref="ManabaseDeck"/> with Karsten source weighting.
/// </summary>
public sealed class ManabaseClassifierTests
{
    [Fact]
    public void Parse_DoublePipWithGeneric_CountsManaValueAndPips()
    {
        ParsedManaCost cost = ManaCostParser.Parse("{2}{U}{U}");

        Assert.Equal(4, cost.ManaValue);
        Assert.Equal(2, cost.Pips[ManaColor.Blue]);
        Assert.Equal(1, cost.DistinctColors);
    }

    [Fact]
    public void Parse_GoldCost_ReportsTwoDistinctColors()
    {
        ParsedManaCost cost = ManaCostParser.Parse("{1}{W}{U}");

        Assert.Equal(2, cost.DistinctColors);
        Assert.Equal(1, cost.Pips[ManaColor.White]);
        Assert.Equal(1, cost.Pips[ManaColor.Blue]);
    }

    [Fact]
    public void Parse_HybridAndX_DoNotCreateHardPips()
    {
        ParsedManaCost cost = ManaCostParser.Parse("{X}{U/R}{U/R}");

        // X = 0, two hybrid symbols add 1 MV each, no hard single-color pip.
        Assert.Equal(2, cost.ManaValue);
        Assert.Empty(cost.Pips);
        Assert.True(cost.HasVariableCost);
    }

    [Fact]
    public void Parse_Twobrid_CountsTwoManaValue()
    {
        // {2/W} can be paid as 2 generic or 1 white — mana value is 2, no hard pip.
        ParsedManaCost cost = ManaCostParser.Parse("{2/W}{2/W}");

        Assert.Equal(4, cost.ManaValue);
        Assert.Empty(cost.Pips);
        Assert.False(cost.HasVariableCost);
    }

    [Fact]
    public void Classify_XSpell_AddsNoStrictRequirement()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Hydroid Krasis",
                Quantity = 1,
                ManaCost = "{X}{G}{U}",
                ManaValue = 2,
                TypeLine = "Creature — Jellyfish Hydra Beast",
                OracleText = "...",
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.Empty(deck.Spells);
    }

    [Fact]
    public void Classify_BasicFetchInThreeColorDeck_IsLandButDiscountedSource()
    {
        var cards = new List<CardFact>
        {
            // Three hard colors come from the spells so deck-color count = 3.
            Spell("Temur Charm", 3, "{G}{U}{R}"),
            new()
            {
                Name = "Evolving Wilds",
                Quantity = 1,
                TypeLine = "Land",
                OracleText = "{T}, Sacrifice Evolving Wilds: Search your library for a basic land card...",
                ProducedMana = new[] { "G", "U", "R" },
                ManaValue = 0,
                HasLandFace = true,
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManaSource fetch = Assert.Single(deck.Sources, s => s.Name == "Evolving Wilds");
        Assert.True(fetch.IsLand);                 // still occupies a land slot
        Assert.Equal(0.67, fetch.Weight, 2);       // but discounted as a color source
        Assert.Equal(1, ManabaseAnalyzer.Analyze(deck).ActualLands);
    }

    [Fact]
    public void Parse_EmptyCost_IsZero()
    {
        ParsedManaCost cost = ManaCostParser.Parse(null);
        Assert.Equal(0, cost.ManaValue);
        Assert.Empty(cost.Pips);
    }

    [Fact]
    public void Classify_BasicLand_BecomesFullWeightSource()
    {
        var cards = new List<CardFact>
        {
            Land("Island", 3, "U"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.Equal(3, deck.Sources.Count);
        Assert.All(deck.Sources, s => Assert.Equal(1.0, s.Weight));
        Assert.All(deck.Sources, s => Assert.Contains(ManaColor.Blue, s.Produces));
    }

    [Fact]
    public void Classify_ManaDork_CountsAsHalfSource_NotALand()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Llanowar Elves",
                Quantity = 1,
                ManaCost = "{G}",
                ManaValue = 1,
                TypeLine = "Creature — Elf Druid",
                OracleText = "{T}: Add {G}.",
                ProducedMana = new[] { "G" },
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManaSource dork = Assert.Single(deck.Sources);
        Assert.Equal(0.5, dork.Weight);
    }

    [Fact]
    public void Classify_ManaRock_CountsAsThreeQuarterSource()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Arcane Signet",
                Quantity = 1,
                ManaCost = "{2}",
                ManaValue = 2,
                TypeLine = "Artifact",
                OracleText = "{T}: Add one mana of any color in your commander's color identity.",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.Equal(0.75, Assert.Single(deck.Sources).Weight);
    }

    [Fact]
    public void Classify_GoldSpell_IsFlaggedGold()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Teferi, Time Raveler",
                Quantity = 1,
                ManaCost = "{1}{W}{U}",
                ManaValue = 3,
                TypeLine = "Legendary Planeswalker — Teferi",
                OracleText = "...",
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        SpellRequirement spell = Assert.Single(deck.Spells);
        Assert.True(spell.IsGold);
    }

    [Fact]
    public void Classify_ComputesAverageManaValueOfNonlandCards()
    {
        var cards = new List<CardFact>
        {
            Land("Forest", 1, "G"),
            Spell("Bear", 2, "{1}{G}"),
            Spell("Dragon", 6, "{4}{R}{R}"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        // Lands excluded; (2 + 6) / 2 = 4.0.
        Assert.Equal(4.0, deck.AverageManaValue);
    }

    private static CardFact Land(string name, int qty, string color) => new()
    {
        Name = name,
        Quantity = qty,
        TypeLine = name.StartsWith("Forest") || name.StartsWith("Island") || name.StartsWith("Mountain")
            ? $"Basic Land — {name}"
            : "Land",
        OracleText = $"{{T}}: Add {{{color}}}.",
        ProducedMana = new[] { color },
        ManaValue = 0,
        HasLandFace = true,
    };

    private static CardFact Spell(string name, int mv, string cost) => new()
    {
        Name = name,
        Quantity = 1,
        ManaCost = cost,
        ManaValue = mv,
        TypeLine = "Creature",
        OracleText = string.Empty,
        ProducedMana = System.Array.Empty<string>(),
    };
}
