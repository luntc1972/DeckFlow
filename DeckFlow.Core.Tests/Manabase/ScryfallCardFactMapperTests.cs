using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>Validates the Scryfall payload → <see cref="CardFact"/> mapping.</summary>
public sealed class ScryfallCardFactMapperTests
{
    [Fact]
    public void ToCardFact_BasicLand_MapsProducedManaAndLandFace()
    {
        var card = new ScryfallCardData
        {
            Name = "Island",
            TypeLine = "Basic Land — Island",
            OracleText = "({T}: Add {U}.)",
            ProducedMana = new[] { "U" },
            Cmc = 0,
        };

        CardFact fact = ScryfallCardFactMapper.ToCardFact(card, quantity: 6);

        Assert.Equal(6, fact.Quantity);
        Assert.True(fact.HasLandFace);
        Assert.Null(fact.ManaCost);
        Assert.Contains("U", fact.ProducedMana);
        Assert.Equal("Basic Land — Island", fact.TypeLine);
    }

    [Fact]
    public void ToCardFact_Spell_CarriesCostAndManaValue()
    {
        var card = new ScryfallCardData
        {
            Name = "Wrath of God",
            ManaCost = "{2}{W}{W}",
            Cmc = 4,
            TypeLine = "Sorcery",
            OracleText = "Destroy all creatures.",
        };

        CardFact fact = ScryfallCardFactMapper.ToCardFact(card, 1);

        Assert.Equal("{2}{W}{W}", fact.ManaCost);
        Assert.Equal(4, fact.ManaValue);
        Assert.False(fact.HasLandFace);
        Assert.Null(fact.Power);
    }

    [Fact]
    public void ToCardFact_Creature_MapsFixedPower()
    {
        var card = new ScryfallCardData
        {
            Name = "Perennial Behemoth",
            ManaCost = "{5}",
            Cmc = 5,
            TypeLine = "Artifact Creature — Construct",
            OracleText = "Reconfigure {5}.",
            Power = "5",
        };

        CardFact fact = ScryfallCardFactMapper.ToCardFact(card, 1);

        Assert.Equal(5, fact.Power);
    }

    [Fact]
    public void ToCardFact_VariablePower_MapsToNull()
    {
        // *goyf power ("*") is variable — not a fixed value, so it must not feed the greatest-power
        // cost reducer.
        var card = new ScryfallCardData
        {
            Name = "Tarmogoyf",
            ManaCost = "{1}{G}",
            Cmc = 2,
            TypeLine = "Creature — Lhurgoyf",
            OracleText = "Tarmogoyf's power is equal to the number of card types among cards in all graveyards.",
            Power = "*",
        };

        CardFact fact = ScryfallCardFactMapper.ToCardFact(card, 1);

        Assert.Null(fact.Power);
    }

    [Fact]
    public void ToCardFact_LandSpellMdfc_UsesFrontCostAndDetectsLandBack()
    {
        var card = new ScryfallCardData
        {
            Name = "Bala Ged Recovery // Bala Ged Sanctuary",
            Cmc = 3,
            Rarity = "uncommon",
            Layout = "modal_dfc",
            ProducedMana = new[] { "G" },
            CardFaces = new List<ScryfallFaceData>
            {
                new()
                {
                    Name = "Bala Ged Recovery",
                    ManaCost = "{2}{G}",
                    TypeLine = "Sorcery",
                    OracleText = "Return target permanent card from your graveyard to your hand.",
                },
                new()
                {
                    Name = "Bala Ged Sanctuary",
                    ManaCost = "",
                    TypeLine = "Land",
                    OracleText = "Bala Ged Sanctuary enters the battlefield tapped.",
                },
            },
        };

        CardFact fact = ScryfallCardFactMapper.ToCardFact(card, 1);

        Assert.Equal("{2}{G}", fact.ManaCost);          // front face cost
        Assert.Equal("Sorcery", fact.TypeLine);          // front face type → not a land slot
        Assert.True(fact.HasLandFace);                   // back face is a Land
        Assert.Contains("enters the battlefield tapped", fact.OracleText); // joined faces
    }

    [Fact]
    public void ToCardFact_TransformCardWithLandBack_HasNoLandFace()
    {
        // M6: a transform card's land back (Search for Azcanta // Azcanta) is reached only by
        // flipping the front — it is NOT castable from hand, so it earns no land credit. Contrast
        // with the modal_dfc case above, whose back IS a hand-playable land.
        var card = new ScryfallCardData
        {
            Name = "Search for Azcanta // Azcanta, the Sunken Ruin",
            Cmc = 2,
            Layout = "transform",
            CardFaces = new List<ScryfallFaceData>
            {
                new() { Name = "Search for Azcanta", ManaCost = "{1}{U}", TypeLine = "Legendary Enchantment", OracleText = "At the beginning of your upkeep, look at the top card..." },
                new() { Name = "Azcanta, the Sunken Ruin", ManaCost = "", TypeLine = "Legendary Land", OracleText = "{T}: Add {U}." },
            },
        };

        CardFact fact = ScryfallCardFactMapper.ToCardFact(card, 1);

        Assert.False(fact.HasLandFace);
    }

    [Fact]
    public void ToCardFact_TransformCardWithLandFront_HasLandFace()
    {
        // M6 guard: the FRONT face still counts even on a transform card. Westvale Abbey is a land
        // that transforms into a creature — it is a real, hand-playable land.
        var card = new ScryfallCardData
        {
            Name = "Westvale Abbey // Ormendahl, Profane Prince",
            Cmc = 0,
            Layout = "transform",
            CardFaces = new List<ScryfallFaceData>
            {
                new() { Name = "Westvale Abbey", ManaCost = "", TypeLine = "Land", OracleText = "{T}: Add {C}." },
                new() { Name = "Ormendahl, Profane Prince", ManaCost = "", TypeLine = "Legendary Creature — Demon", OracleText = "Flying, trample, haste" },
            },
        };

        CardFact fact = ScryfallCardFactMapper.ToCardFact(card, 1);

        Assert.True(fact.HasLandFace);
    }

    [Fact]
    public void ToCardFact_SplitCard_UsesFrontFaceManaValueNotCombinedCmc()
    {
        // Commit // Memory: Scryfall root cmc = 10 (combined), but the cast front is {3}{U} = 4.
        var card = new ScryfallCardData
        {
            Name = "Commit // Memory",
            Cmc = 10,
            Layout = "split",
            CardFaces = new List<ScryfallFaceData>
            {
                new() { Name = "Commit", ManaCost = "{3}{U}", TypeLine = "Instant", OracleText = "Put target..." },
                new() { Name = "Memory", ManaCost = "{4}{U}{U}", TypeLine = "Sorcery", OracleText = "Each player..." },
            },
        };

        CardFact fact = ScryfallCardFactMapper.ToCardFact(card, 1);

        Assert.Equal("{3}{U}", fact.ManaCost);
        Assert.Equal("Instant", fact.TypeLine);
        Assert.Equal(4, fact.ManaValue);     // front face, not the combined 10
        Assert.False(fact.HasLandFace);
    }

    [Fact]
    public void ToCardFacts_BatchPairsQuantitiesAndCommander()
    {
        var entries = new List<DeckCardEntry>
        {
            new() { Card = Land("Forest", "G"), Quantity = 10 },
            new() { Card = Spell("Xyris", "{2}{G}{U}{R}", 5), Quantity = 1, IsCommander = true },
        };

        IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(entries);

        Assert.Equal(2, facts.Count);
        Assert.Equal(10, facts.First(f => f.Name == "Forest").Quantity);
        Assert.True(facts.Single(f => f.Name == "Xyris").IsCommander);
    }

    [Fact]
    public void EndToEnd_MapThenClassifyThenAnalyze_ProducesReport()
    {
        var entries = new List<DeckCardEntry>
        {
            new() { Card = Spell("Counterspell", "{U}{U}", 2), Quantity = 1 },
        };
        for (int i = 0; i < 12; i++)
        {
            entries.Add(new DeckCardEntry { Card = Land($"Island{i}", "U"), Quantity = 1 });
        }

        IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(entries);
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: false);
        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.Equal(12, report.ActualLands);
        Assert.NotEmpty(report.ColorFindings);
        Assert.Equal(ManaColor.Blue, report.ColorFindings[0].Color);
    }

    private static ScryfallCardData Land(string name, string color) => new()
    {
        Name = name,
        TypeLine = "Land",
        OracleText = $"({{T}}: Add {{{color}}}.)",
        ProducedMana = new[] { color },
        Cmc = 0,
    };

    private static ScryfallCardData Spell(string name, string cost, double cmc) => new()
    {
        Name = name,
        ManaCost = cost,
        Cmc = cmc,
        TypeLine = "Instant",
        OracleText = "...",
    };
}
