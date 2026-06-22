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
    public void Classify_ZeroCostSpell_KeepsManaValueZero()
    {
        // Regression: a 0-mana card (Ornithopter, kobolds, Shield Sphere) used to be clamped to
        // ManaValue 1 in the spell requirement, so the castability table displayed MV 1. The
        // min-1 cast-turn floor lives downstream; the printed MV must stay 0.
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Ornithopter",
                Quantity = 1,
                ManaCost = "{0}",
                ManaValue = 0,
                TypeLine = "Artifact Creature — Thopter",
                OracleText = "Flying",
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        SpellRequirement spell = Assert.Single(deck.Spells);
        Assert.Equal("Ornithopter", spell.Name);
        Assert.Equal(0, spell.ManaValue);
    }

    private static CostSuggestion? Suggestion(ManabaseDeck deck, string name) =>
        deck.CostSuggestions.FirstOrDefault(s => s.Name == name);

    private static ManabaseDeck ClassifyOne(string name, string? manaCost, int mv, string type, string oracle) =>
        ManabaseClassifier.Classify(new List<CardFact>
        {
            new()
            {
                Name = name,
                Quantity = 1,
                ManaCost = manaCost,
                ManaValue = mv,
                TypeLine = type,
                OracleText = oracle,
                ProducedMana = System.Array.Empty<string>(),
            },
        });

    [Fact]
    public void DetectSelfCost_FreePitchSpell_SuggestsZero()
    {
        ManabaseDeck deck = ClassifyOne(
            "Force of Will", "{3}{U}{U}", 5, "Instant",
            "You may pay 1 life and exile a blue card from your hand rather than pay this spell's mana cost. Counter target spell.");

        CostSuggestion? s = Suggestion(deck, "Force of Will");
        Assert.NotNull(s);
        Assert.Equal("0", s!.EffectiveCost);
    }

    [Fact]
    public void DetectSelfCost_BoardScalingSelfReducer_SuggestsColoredRemainder()
    {
        ManabaseDeck deck = ClassifyOne(
            "Blasphemous Act", "{8}{R}", 9, "Sorcery",
            "This spell costs {1} less to cast for each creature on the battlefield. Destroy all creatures.");

        CostSuggestion? s = Suggestion(deck, "Blasphemous Act");
        Assert.NotNull(s);
        Assert.Equal("{R}", s!.EffectiveCost);
    }

    [Fact]
    public void DetectSelfCost_EvokeWithManaCost_SuggestsThatCost()
    {
        ManabaseDeck deck = ClassifyOne(
            "Shriekmaw", "{4}{B}{B}", 6, "Creature — Elemental",
            "Evoke {1}{B}. When Shriekmaw enters the battlefield, destroy target nonartifact, nonblack creature.");

        CostSuggestion? s = Suggestion(deck, "Shriekmaw");
        Assert.NotNull(s);
        Assert.Equal("{1}{B}", s!.EffectiveCost);
    }

    [Fact]
    public void DetectSelfCost_EvokeWithNonManaCost_SuggestsZero()
    {
        ManabaseDeck deck = ClassifyOne(
            "Grief", "{2}{B}", 3, "Creature — Elemental Incarnation",
            "Menace. Evoke—Exile a black card from your hand. When Grief enters, target opponent reveals their hand.");

        CostSuggestion? s = Suggestion(deck, "Grief");
        Assert.NotNull(s);
        Assert.Equal("0", s!.EffectiveCost);
    }

    [Fact]
    public void DetectSelfCost_SuspendWithEmDash_SuggestsSuspendCost()
    {
        ManabaseDeck deck = ClassifyOne(
            "Crashing Footfalls", null, 0, "Sorcery",
            "Suspend 1—{G}. Create two 4/4 green Rhino creature tokens with trample.");

        CostSuggestion? s = Suggestion(deck, "Crashing Footfalls");
        Assert.NotNull(s);
        Assert.Equal("{G}", s!.EffectiveCost);
    }

    [Fact]
    public void DetectSelfCost_DeckWideReducer_IsNotSelfCost()
    {
        // Ruby Medallion discounts OTHER spells; it is a CostReducer, not a self-cost suggestion.
        ManabaseDeck deck = ClassifyOne(
            "Ruby Medallion", "{2}", 2, "Artifact",
            "Red spells you cast cost {1} less to cast.");

        Assert.Null(Suggestion(deck, "Ruby Medallion"));
    }

    [Fact]
    public void DetectSelfCost_OtherSpellScalingReducer_IsNotSelfCost()
    {
        // A card that reduces OTHER spells with a "for each" rider must NOT be read as a self-scaler
        // (the scaling regex is anchored on "this spell costs ...").
        ManabaseDeck deck = ClassifyOne(
            "Hypothetical Reducer", "{3}", 3, "Artifact",
            "Artifact spells you cast cost {1} less to cast for each artifact you control.");

        Assert.Null(Suggestion(deck, "Hypothetical Reducer"));
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

    [Fact]
    public void Classify_ColorlessNonSourcePayoff_IsSpellRow_NotManaSource()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Wurmcoil Engine",
                Quantity = 1,
                ManaCost = "{6}",
                ManaValue = 6,
                TypeLine = "Artifact Creature — Wurm",
                OracleText = "Deathtouch, lifelink. When Wurmcoil Engine dies, create tokens.",
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        SpellRequirement spell = Assert.Single(deck.Spells);
        Assert.Empty(spell.Pips);                 // colorless → no hard pips
        Assert.False(spell.IsManaSource);         // not a rock/dork → appears in castability rows
        Assert.Empty(deck.Sources);               // produces no mana → not a source
    }

    [Fact]
    public void Classify_ManaDorkAndRock_AreFlaggedIsManaSource()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Birds of Paradise",
                Quantity = 1,
                ManaCost = "{G}",
                ManaValue = 1,
                TypeLine = "Creature — Bird",
                OracleText = "Flying. {T}: Add one mana of any color.",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
            new()
            {
                Name = "Sol Ring",
                Quantity = 1,
                ManaCost = "{1}",
                ManaValue = 1,
                TypeLine = "Artifact",
                OracleText = "{T}: Add {C}{C}.",
                ProducedMana = new[] { "C" },
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.All(deck.Spells, s => Assert.True(s.IsManaSource));
        // Both still contribute to the source pool (dork 0.5, rock 0.75).
        Assert.Equal(2, deck.Sources.Count);
    }

    [Fact]
    public void Classify_GoblinElectromancer_DetectedAsInstantSorceryReducer()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Goblin Electromancer",
                Quantity = 1,
                ManaCost = "{U}{R}",
                ManaValue = 2,
                TypeLine = "Creature — Goblin Wizard",
                OracleText = "Instant and sorcery spells you cast cost {1} less to cast.",
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        CostReducer reducer = Assert.Single(deck.CostReduction);
        Assert.Equal(1, reducer.GenericReduction);
        Assert.Equal(ReductionScope.InstantSorcery, reducer.Scope);
        Assert.Equal(2, reducer.SourceManaValue);
    }

    [Theory]
    [InlineData("Spells you cast cost {1} less for each artifact you control.")] // "for each" scaling
    [InlineData("Spells your opponents cast cost {2} more.")]                    // opponent-facing
    [InlineData("This spell costs {1} less to cast for each creature you control.")] // not "you cast" + for each
    public void Classify_FalsePositiveReducerText_IsNotDetected(string oracle)
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Tricky Card",
                Quantity = 1,
                ManaCost = "{3}",
                ManaValue = 3,
                TypeLine = "Artifact",
                OracleText = oracle,
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.Empty(deck.CostReduction);
    }

    [Fact]
    public void Classify_CryptolithRite_GrantsAnyColorSourcesToNonDorkCreatures()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Cryptolith Rite",
                Quantity = 1,
                ManaCost = "{1}{G}",
                ManaValue = 2,
                TypeLine = "Enchantment",
                OracleText = "Creatures you control have \"{T}: Add one mana of any color.\"",
                ProducedMana = System.Array.Empty<string>(),
            },
            // A vanilla creature: becomes an eligible granted source.
            new()
            {
                Name = "Grizzly Bears",
                Quantity = 1,
                ManaCost = "{1}{G}",
                ManaValue = 2,
                TypeLine = "Creature — Bear",
                OracleText = "",
                ProducedMana = System.Array.Empty<string>(),
            },
            // Already a dork: must NOT be double-counted with a granted source.
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

        // The dork's own 0.5 source plus exactly one 0.25 granted source for Grizzly Bears.
        Assert.Single(deck.Sources, s => s.Name == "Grizzly Bears (granted)");
        Assert.DoesNotContain(deck.Sources, s => s.Name == "Llanowar Elves (granted)");
        ManaSource granted = deck.Sources.Single(s => s.Name == "Grizzly Bears (granted)");
        Assert.Equal(0.25, granted.Weight);
        Assert.False(granted.IsLand);
    }

    [Fact]
    public void Classify_GranterWithCommander_GrantsAnyColorSourceToEligibleCommander()
    {
        // MEDIUM-3: a GRANT-eligible commander (legendary creature that is NOT a rock/dork)
        // must receive a 0.25-weight any-color granted source when a granter is present.
        var granter = new CardFact
        {
            Name = "Cryptolith Rite",
            Quantity = 1,
            ManaCost = "{1}{G}",
            ManaValue = 2,
            TypeLine = "Enchantment",
            OracleText = "Creatures you control have \"{T}: Add one mana of any color.\"",
            ProducedMana = System.Array.Empty<string>(),
        };
        var commander = new CardFact
        {
            Name = "Brago, King Eternal",
            Quantity = 1,
            ManaCost = "{2}{W}{U}",
            ManaValue = 4,
            TypeLine = "Legendary Creature — Spirit Noble",
            OracleText = "Flying.",
            ProducedMana = System.Array.Empty<string>(),
            IsCommander = true,
        };

        ManabaseDeck withGranter = ManabaseClassifier.Classify(new List<CardFact> { granter, commander });
        ManabaseDeck withoutGranter = ManabaseClassifier.Classify(new List<CardFact> { commander });

        // Without the granter, the commander contributes no granted source.
        Assert.DoesNotContain(withoutGranter.Sources, s => s.Name == "Brago, King Eternal (granted)");

        // With the granter, the commander contributes exactly one 0.25 non-land any-color source.
        ManaSource granted = Assert.Single(
            withGranter.Sources, s => s.Name == "Brago, King Eternal (granted)");
        Assert.Equal(0.25, granted.Weight);
        Assert.False(granted.IsLand);
    }

    [Fact]
    public void Classify_EquipGranter_GrantsAnyColorSourcesToEligibleCreatures()
    {
        // MEDIUM-5: a Paradise-Mantle-style equip granter must be detected and grant eligible
        // (non-rock/dork) creatures a 0.25 any-color source.
        var equip = new CardFact
        {
            Name = "Paradise Mantle",
            Quantity = 1,
            ManaCost = "{0}",
            ManaValue = 0,
            TypeLine = "Artifact — Equipment",
            OracleText = "Equipped creature has \"{T}: Add one mana of any color.\" Equip {1}",
            ProducedMana = System.Array.Empty<string>(),
        };
        var creature = new CardFact
        {
            Name = "Grizzly Bears",
            Quantity = 1,
            ManaCost = "{1}{G}",
            ManaValue = 2,
            TypeLine = "Creature — Bear",
            OracleText = "",
            ProducedMana = System.Array.Empty<string>(),
        };

        ManabaseDeck withEquip = ManabaseClassifier.Classify(new List<CardFact> { equip, creature });
        ManabaseDeck withoutEquip = ManabaseClassifier.Classify(new List<CardFact> { creature });

        // No granter → no granted sources at all.
        Assert.DoesNotContain(withoutEquip.Sources, s => s.Name.EndsWith("(granted)"));

        // Equip granter → the eligible creature gains a 0.25 non-land any-color source.
        ManaSource granted = Assert.Single(
            withEquip.Sources, s => s.Name == "Grizzly Bears (granted)");
        Assert.Equal(0.25, granted.Weight);
        Assert.False(granted.IsLand);
    }

    [Fact]
    public void Classify_Commander_IsFlaggedOnSpellRequirement()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Brago, King Eternal",
                Quantity = 1,
                ManaCost = "{2}{W}{U}",
                ManaValue = 4,
                TypeLine = "Legendary Creature — Spirit Noble",
                OracleText = "Flying.",
                ProducedMana = System.Array.Empty<string>(),
                IsCommander = true,
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        SpellRequirement spell = Assert.Single(deck.Spells);
        Assert.True(spell.IsCommander);
        Assert.True((spell.Kinds & SpellKinds.Creature) != 0);
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

    private static CardFact Land(string name, string typeLine, string[] produced, string oracle = "") => new()
    {
        Name = name,
        Quantity = 1,
        TypeLine = typeLine,
        OracleText = oracle,
        ProducedMana = produced,
        ManaValue = 0,
        HasLandFace = true,
    };

    [Fact]
    public void Classify_TypedFetch_EmptyProducedMana_DerivesColorsFromNamedBasics()
    {
        // Flooded Strand has empty produced_mana on Scryfall but fetches Plains or Island -> W and U.
        var cards = new List<CardFact>
        {
            Spell("Brago", 4, "{2}{W}{U}"),
            Land("Plains", "Basic Land — Plains", new[] { "W" }),
            Land("Island", "Basic Land — Island", new[] { "U" }),
            Land("Flooded Strand", "Land", System.Array.Empty<string>(),
                "{T}, Pay 1 life, Sacrifice Flooded Strand: Search your library for a Plains or Island card..."),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManaSource fetch = Assert.Single(deck.Sources, s => s.Name == "Flooded Strand");
        Assert.Contains(ManaColor.White, fetch.Produces);
        Assert.Contains(ManaColor.Blue, fetch.Produces);
        Assert.DoesNotContain(ManaColor.Black, fetch.Produces);
    }

    [Fact]
    public void Classify_TypedFetch_ReachesOffColorTriomeSharingAType()
    {
        // A Plains/Island fetch can grab a Plains-typed triome (W/U/B), so it also supplies black.
        var cards = new List<CardFact>
        {
            Spell("Esper Thing", 5, "{2}{W}{U}{B}"),
            Land("Plains", "Basic Land — Plains", new[] { "W" }),
            Land("Raffine's Tower", "Land — Plains Island Swamp", new[] { "W", "U", "B" },
                "Raffine's Tower enters the battlefield tapped."),
            Land("Flooded Strand", "Land", System.Array.Empty<string>(),
                "Search your library for a Plains or Island card..."),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManaSource fetch = Assert.Single(deck.Sources, s => s.Name == "Flooded Strand");
        Assert.Contains(ManaColor.White, fetch.Produces);
        Assert.Contains(ManaColor.Blue, fetch.Produces);
        Assert.Contains(ManaColor.Black, fetch.Produces); // reached via the Plains-typed triome
    }
}
