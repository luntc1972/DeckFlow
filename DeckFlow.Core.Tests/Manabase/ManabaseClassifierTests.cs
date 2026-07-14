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

    private const string SkullsporeText =
        "This spell costs {X} less to cast, where X is the greatest power among creatures you control. "
        + "Whenever one or more creatures you control die, create a 1/1 green Saproling creature token.";

    private static CardFact Creature(string name, string manaCost, int mv, int? power) =>
        new()
        {
            Name = name,
            Quantity = 1,
            ManaCost = manaCost,
            ManaValue = mv,
            TypeLine = "Creature — Beast",
            OracleText = "Vanilla.",
            Power = power,
            ProducedMana = System.Array.Empty<string>(),
        };

    [Fact]
    public void DetectSelfCost_GreatestPowerReducer_ReducesGenericByMaxCreaturePower()
    {
        // The Skullspore Nexus ({4}{G}{G}) with a 5-power creature in the deck: X = 5 removes all 4
        // generic, leaving the colored floor {G}{G}.
        ManabaseDeck deck = ManabaseClassifier.Classify(new List<CardFact>
        {
            new()
            {
                Name = "The Skullspore Nexus",
                Quantity = 1,
                ManaCost = "{4}{G}{G}",
                ManaValue = 6,
                TypeLine = "Legendary Artifact",
                OracleText = SkullsporeText,
                ProducedMana = System.Array.Empty<string>(),
            },
            Creature("Perennial Behemoth", "{5}", 5, power: 5),
        });

        CostSuggestion? s = Suggestion(deck, "The Skullspore Nexus");
        Assert.NotNull(s);
        Assert.Equal("{G}{G}", s!.EffectiveCost);
    }

    [Fact]
    public void DetectSelfCost_GreatestPowerReducer_PartialReductionKeepsRemainingGeneric()
    {
        // Greatest fixed creature power is 2 → {4}{G}{G} reduced by 2 = {2}{G}{G}.
        ManabaseDeck deck = ManabaseClassifier.Classify(new List<CardFact>
        {
            new()
            {
                Name = "The Skullspore Nexus",
                Quantity = 1,
                ManaCost = "{4}{G}{G}",
                ManaValue = 6,
                TypeLine = "Legendary Artifact",
                OracleText = SkullsporeText,
                ProducedMana = System.Array.Empty<string>(),
            },
            Creature("Llanowar Elves", "{G}", 1, power: 1),
            Creature("Grizzly Bears", "{1}{G}", 2, power: 2),
        });

        CostSuggestion? s = Suggestion(deck, "The Skullspore Nexus");
        Assert.NotNull(s);
        Assert.Equal("{2}{G}{G}", s!.EffectiveCost);
    }

    [Fact]
    public void GreatestPowerReducer_AutoAppliesReducedCostToTheSpellRequirement()
    {
        // The discount is intrinsic (always-on), so the default analysis must already cast Skullspore
        // at the reduced cost — not merely suggest it. With a 5-power creature, {4}{G}{G} (MV 6) drops
        // to {G}{G} (MV 2) on the spell requirement itself, flagged IsCostOverridden.
        ManabaseDeck deck = ManabaseClassifier.Classify(new List<CardFact>
        {
            new()
            {
                Name = "The Skullspore Nexus",
                Quantity = 1,
                ManaCost = "{4}{G}{G}",
                ManaValue = 6,
                TypeLine = "Legendary Artifact",
                OracleText = SkullsporeText,
                ProducedMana = System.Array.Empty<string>(),
            },
            Creature("Perennial Behemoth", "{5}", 5, power: 5),
        });

        SpellRequirement skullspore = deck.Spells.Single(s => s.Name == "The Skullspore Nexus");
        Assert.Equal(2, skullspore.ManaValue);
        Assert.True(skullspore.IsCostOverridden);
        Assert.Equal(2, skullspore.Pips[ManaColor.Green]);
    }

    [Fact]
    public void DetectSelfCost_GreatestPowerReducer_NoFixedPowerCreature_FallsBackToColoredFloor()
    {
        // Only a variable-power creature (*goyf, Power == null): cannot measure X, so fall back to
        // the optimistic colored-pip floor {G}{G} rather than leaving the cost unreduced.
        ManabaseDeck deck = ManabaseClassifier.Classify(new List<CardFact>
        {
            new()
            {
                Name = "The Skullspore Nexus",
                Quantity = 1,
                ManaCost = "{4}{G}{G}",
                ManaValue = 6,
                TypeLine = "Legendary Artifact",
                OracleText = SkullsporeText,
                ProducedMana = System.Array.Empty<string>(),
            },
            Creature("Tarmogoyf", "{1}{G}", 2, power: null),
        });

        CostSuggestion? s = Suggestion(deck, "The Skullspore Nexus");
        Assert.NotNull(s);
        Assert.Equal("{G}{G}", s!.EffectiveCost);
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
    [InlineData("Giant spells you cast cost {1} less to cast.")]     // M5: tribal scope → dropped, not All
    [InlineData("Historic spells you cast cost {1} less to cast.")]  // M5: supertype scope → dropped, not All
    [InlineData("Noncreature spells you cast cost {1} less to cast.")] // M5: "noncreature" ≠ Creature (word match)
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
    public void Classify_BareAllScopeReducer_StillDetectedAsAll()
    {
        // M5 guard: an empty scope ("Spells you cast cost {N} less") must still map to All — the fix
        // only drops UNRECOGNIZED non-empty (tribal/supertype) scopes, not the legitimate all-spell one.
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Generic Reducer",
                Quantity = 1,
                ManaCost = "{4}",
                ManaValue = 4,
                TypeLine = "Artifact",
                OracleText = "Spells you cast cost {1} less to cast.",
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        CostReducer reducer = Assert.Single(ManabaseClassifier.Classify(cards).CostReduction);
        Assert.Equal(ReductionScope.All, reducer.Scope);
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
    public void Classify_RelicOfLegends_GrantsAnyColorSourceToLegendaryCreature()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Relic of Legends",
                Quantity = 1,
                ManaCost = "{3}",
                ManaValue = 3,
                TypeLine = "Artifact",
                OracleText = "{T}, Tap an untapped legendary creature you control: Add one mana of any color.",
                ProducedMana = System.Array.Empty<string>(),
            },
            new()
            {
                Name = "Raff Capashen, Ship's Mage",
                Quantity = 1,
                ManaCost = "{2}{W}{U}",
                ManaValue = 4,
                TypeLine = "Legendary Creature — Human Wizard",
                OracleText = "Flash.",
                ProducedMana = System.Array.Empty<string>(),
            },
            new()
            {
                Name = "Grizzly Bears",
                Quantity = 1,
                ManaCost = "{1}{G}",
                ManaValue = 2,
                TypeLine = "Creature — Bear",
                OracleText = string.Empty,
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManaSource granted = Assert.Single(deck.Sources, s => s.Name == "Raff Capashen, Ship's Mage (granted)");
        Assert.Equal(0.25, granted.Weight);
        Assert.DoesNotContain(deck.Sources, s => s.Name == "Grizzly Bears (granted)");
    }

    [Fact]
    public void Classify_NonManaTapAbility_DoesNotMatchLegendaryGranter()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Not A Relic",
                Quantity = 1,
                ManaCost = "{3}",
                ManaValue = 3,
                TypeLine = "Artifact",
                OracleText = "{T}, Tap an untapped legendary creature you control: Draw a card.",
                ProducedMana = System.Array.Empty<string>(),
            },
            new()
            {
                Name = "Raff Capashen, Ship's Mage",
                Quantity = 1,
                ManaCost = "{2}{W}{U}",
                ManaValue = 4,
                TypeLine = "Legendary Creature — Human Wizard",
                OracleText = "Flash.",
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.DoesNotContain(deck.Sources, s => s.Name.EndsWith("(granted)"));
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

    [Fact]
    public void Classify_PartnerPair_CountsTwoCommanders()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(new List<CardFact>
        {
            CommanderCard("Thrasios, Triton Hero", "{G/U}", 2, "Legendary Creature"),
            CommanderCard("Tymna the Weaver", "{1}{W}{B}", 3, "Legendary Creature"),
        });

        Assert.Equal(2, deck.CommanderCount);
    }

    [Fact]
    public void Classify_BackgroundStyleSecondCommander_CountsTwoCommanders()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(new List<CardFact>
        {
            CommanderCard("Wilson, Refined Grizzly", "{1}{G}", 2, "Legendary Creature"),
            CommanderCard("Sword Coast Sailor", "{1}{U}", 2, "Legendary Enchantment — Background"),
        });

        Assert.Equal(2, deck.CommanderCount);
    }

    [Fact]
    public void Classify_CompanionStyleCard_DoesNotIncreaseCommanderCount()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(new List<CardFact>
        {
            CommanderCard("Atraxa, Praetors' Voice", "{G}{W}{U}{B}", 4, "Legendary Creature"),
            new()
            {
                Name = "Jegantha, the Wellspring",
                Quantity = 1,
                ManaCost = "{4}{R/G}",
                ManaValue = 5,
                TypeLine = "Legendary Creature — Elemental Elk",
                OracleText = "Companion",
                ProducedMana = System.Array.Empty<string>(),
                IsCommander = false,
            },
        });

        Assert.Equal(1, deck.CommanderCount);
    }

    [Fact]
    public void RampDrawBudget_WithTwoCommanders_UsesHighestManaValueThreshold()
    {
        ManabaseRampDrawBudget budget = ManabaseRampDrawBudgetCalculator.Calculate(new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 2,
            AverageManaValue = 3.0,
            Sources = new List<ManaSource>(),
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Commander Four", ManaValue = 4, Pips = new Dictionary<ManaColor, int>(), IsCommander = true },
                new() { Name = "Commander Six", ManaValue = 6, Pips = new Dictionary<ManaColor, int>(), IsCommander = true },
                new() { Name = "Spell", ManaValue = 3, Pips = new Dictionary<ManaColor, int>() },
            },
            IsSingleton = true,
        });

        Assert.Equal(6.0, budget.Threshold);
        Assert.Equal(ManabaseRampDrawThresholdSource.CommanderManaValue, budget.ThresholdSource);
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

    private static CardFact CommanderCard(string name, string cost, int mv, string typeLine) => new()
    {
        Name = name,
        Quantity = 1,
        ManaCost = cost,
        ManaValue = mv,
        TypeLine = typeLine,
        OracleText = string.Empty,
        ProducedMana = System.Array.Empty<string>(),
        IsCommander = true,
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

    [Fact]
    public void Classify_EntersTapped_MatchesPostAug2024OracleWording()
    {
        // Efficacy R2 finding H1: Scryfall's Aug-2024 oracle update reworded
        // "enters the battlefield tapped" to "enters tapped" ("This land enters tapped.").
        // Live API data uses the new phrasing exclusively — both forms must classify as tapped.
        var cards = new List<CardFact>
        {
            Spell("Some Spell", 2, "{1}{U}"),
            Land("Azorius Guildgate", "Land — Gate", new[] { "W", "U" },
                "This land enters tapped.\n{T}: Add {W} or {U}."),
            Land("Raffine's Tower", "Land — Plains Island Swamp", new[] { "W", "U", "B" },
                "Raffine's Tower enters the battlefield tapped."),
            Land("Island", "Basic Land — Island", new[] { "U" },
                "{T}: Add {U}."),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManaSource gate = Assert.Single(deck.Sources, s => s.Name == "Azorius Guildgate");
        Assert.False(gate.EntersUntapped); // new wording

        ManaSource triome = Assert.Single(deck.Sources, s => s.Name == "Raffine's Tower");
        Assert.False(triome.EntersUntapped); // old wording still recognized

        ManaSource basic = Assert.Single(deck.Sources, s => s.Name == "Island");
        Assert.True(basic.EntersUntapped);
    }

    [Fact]
    public void Classify_PayLifeUntapped_ShocklandFlagOn_EntersUntapped()
    {
        var cards = new List<CardFact>
        {
            Spell("Some Spell", 2, "{1}{U}"),
            Land("Steam Vents", "Land — Island Mountain", new[] { "U", "R" },
                "As Steam Vents enters, you may pay 2 life. If you don't, it enters tapped."),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, payLifeUntapped: true);

        ManaSource shock = Assert.Single(deck.Sources, s => s.Name == "Steam Vents");
        Assert.True(shock.EntersUntapped);
    }

    [Fact]
    public void Classify_PayLifeUntapped_ShocklandFlagOff_EntersTapped()
    {
        var cards = new List<CardFact>
        {
            Spell("Some Spell", 2, "{1}{U}"),
            Land("Steam Vents", "Land — Island Mountain", new[] { "U", "R" },
                "As Steam Vents enters, you may pay 2 life. If you don't, it enters tapped."),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, payLifeUntapped: false);

        ManaSource shock = Assert.Single(deck.Sources, s => s.Name == "Steam Vents");
        Assert.False(shock.EntersUntapped);
    }

    [Fact]
    public void Classify_PayLifeUntapped_PlainTaplandStaysTapped()
    {
        var cards = new List<CardFact>
        {
            Spell("Some Spell", 2, "{1}{U}"),
            Land("Tranquil Cove", "Land", new[] { "W", "U" },
                "Tranquil Cove enters tapped. When Tranquil Cove enters, you gain 1 life. {T}: Add {W} or {U}."),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, payLifeUntapped: true);

        ManaSource tapland = Assert.Single(deck.Sources, s => s.Name == "Tranquil Cove");
        Assert.False(tapland.EntersUntapped);
    }

    [Fact]
    public void Classify_PayLifeUntapped_BasicLandUnaffectedAcrossFlagStates()
    {
        var cards = new List<CardFact>
        {
            Spell("Some Spell", 2, "{1}{U}"),
            Land("Island", "Basic Land — Island", new[] { "U" }, "{T}: Add {U}."),
        };

        ManabaseDeck off = ManabaseClassifier.Classify(cards, payLifeUntapped: false);
        ManabaseDeck on = ManabaseClassifier.Classify(cards, payLifeUntapped: true);

        ManaSource basicOff = Assert.Single(off.Sources, s => s.Name == "Island");
        ManaSource basicOn = Assert.Single(on.Sources, s => s.Name == "Island");
        Assert.True(basicOff.EntersUntapped);
        Assert.True(basicOn.EntersUntapped);
    }

    [Fact]
    public void Classify_PayLifeUntapped_AlwaysTappedLandWithPayLifeAbility_StaysTapped()
    {
        // Boseiju/Hall/Untaidake pattern: enters tapped AND has a "{T}, Pay N life:" ACTIVATED
        // ability. This must NOT be flipped untapped — the pay-life is a cost, not the shock ETB
        // choice. Guards against the over-broad "pay N life" match.
        var cards = new List<CardFact>
        {
            Spell("Some Spell", 2, "{1}{U}"),
            Land("Hall of the Bandit Lord", "Land", new[] { "W", "U", "B", "R", "G" },
                "Hall of the Bandit Lord enters tapped. {T}, Pay 3 life: Add one mana of any color."),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, payLifeUntapped: true);

        ManaSource land = Assert.Single(deck.Sources, s => s.Name == "Hall of the Bandit Lord");
        Assert.False(land.EntersUntapped);
    }

    [Fact]
    public void Classify_MdfcPayLifeLandBack_IsRealUntappedLand()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Agadeem's Awakening // Agadeem, the Undercrypt",
                Quantity = 1,
                ManaCost = "{X}{B}{B}{B}",
                ManaValue = 3,
                TypeLine = "Sorcery",
                OracleText = "Return from graveyard.\nAs Agadeem, the Undercrypt enters, you may pay 3 life. If you don't, it enters tapped.",
                LandFaceOracleText = "As Agadeem, the Undercrypt enters, you may pay 3 life. If you don't, it enters tapped.",
                ProducedMana = new[] { "B" },
                Rarity = "mythic",
                Layout = "modal_dfc",
                HasLandFace = true,
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManaSource source = Assert.Single(deck.Sources);
        Assert.True(source.IsLand);
        Assert.True(source.EntersUntapped);
        Assert.Equal(1.0, source.Weight);
    }

    [Fact]
    public void Classify_MdfcLandBack_AlwaysTapped_IsRealTappedLand()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Bala Ged Recovery // Bala Ged Sanctuary",
                Quantity = 1,
                ManaCost = "{2}{G}",
                ManaValue = 3,
                TypeLine = "Sorcery",
                OracleText = "Return target permanent card.\nBala Ged Sanctuary enters tapped.",
                LandFaceOracleText = "Bala Ged Sanctuary enters tapped.",
                ProducedMana = new[] { "G" },
                Rarity = "uncommon",
                Layout = "modal_dfc",
                HasLandFace = true,
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManaSource source = Assert.Single(deck.Sources);
        Assert.True(source.IsLand);
        Assert.False(source.EntersUntapped);
        // A real-land MDFC supplies its color at full weight 1.0; the tapped timing is the only
        // penalty (a color discount on top would double-count the downside).
        Assert.Equal(1.0, source.Weight, 2);
    }

    [Fact]
    public void Classify_MdfcLandBack_CountsAsLandByCopyCount()
    {
        var cards = new List<CardFact>
        {
            Land("Swamp", "Basic Land — Swamp", new[] { "B" }, "{T}: Add {B}."),
            Land("Swamp", "Basic Land — Swamp", new[] { "B" }, "{T}: Add {B}.") with { Name = "Swamp Two" },
            new()
            {
                Name = "Bala Ged Recovery // Bala Ged Sanctuary",
                Quantity = 2,
                ManaCost = "{2}{G}",
                ManaValue = 3,
                TypeLine = "Sorcery",
                OracleText = "Return target permanent card.\nBala Ged Sanctuary enters tapped.",
                LandFaceOracleText = "Bala Ged Sanctuary enters tapped.",
                ProducedMana = new[] { "G" },
                Rarity = "uncommon",
                Layout = "modal_dfc",
                HasLandFace = true,
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        // 2 basics + 2 MDFC land backs = 4 land sources; the MDFC backs are real lands (by copy).
        Assert.Equal(4, deck.Sources.Count(s => s.IsLand));
    }

    [Fact]
    public void Classify_InstantSorceryRituals_DetectedAsOneShotBurstMana()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Dark Ritual", Quantity = 1, ManaCost = "{B}", ManaValue = 1,
                TypeLine = "Instant", OracleText = "Add {B}{B}{B}.", ProducedMana = new[] { "B" },
            },
            new()
            {
                Name = "Rite of Flame", Quantity = 2, ManaCost = "{R}", ManaValue = 1,
                TypeLine = "Sorcery", OracleText = "Add {R}{R}.", ProducedMana = new[] { "R" },
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        // One entry per copy (Dark Ritual ×1 + Rite of Flame ×2 = 3).
        Assert.Equal(3, deck.OneShots.Count);

        OneShotMana dark = Assert.Single(deck.OneShots, o => o.Name == "Dark Ritual");
        Assert.Equal(3, dark.ProducedAmount);
        Assert.Equal(1, dark.OwnManaValue);
        Assert.Equal(2, dark.NetMana);
        Assert.Equal(new[] { ManaColor.Black }, dark.ProducedColors);

        OneShotMana rite = deck.OneShots.First(o => o.Name == "Rite of Flame");
        Assert.Equal(1, rite.NetMana); // Add RR (2) − {R} (1)
    }

    [Fact]
    public void Classify_SacrificeCostRituals_AreExcludedFromOneShotBurstMana()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Culling the Weak", Quantity = 1, ManaCost = "{B}", ManaValue = 1,
                TypeLine = "Instant",
                OracleText = "As an additional cost to cast this spell, sacrifice a creature.\nAdd {B}{B}{B}{B}.",
                ProducedMana = new[] { "B" },
            },
            new()
            {
                Name = "Infernal Plunge", Quantity = 1, ManaCost = "{R}", ManaValue = 1,
                TypeLine = "Instant",
                OracleText = "As an additional cost to cast this spell, sacrifice a creature.\nAdd {R}{R}{R}.",
                ProducedMana = new[] { "R" },
            },
            new()
            {
                Name = "Dark Ritual", Quantity = 1, ManaCost = "{B}", ManaValue = 1,
                TypeLine = "Instant", OracleText = "Add {B}{B}{B}.", ProducedMana = new[] { "B" },
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.DoesNotContain(deck.OneShots, o => o.Name == "Culling the Weak");
        Assert.DoesNotContain(deck.OneShots, o => o.Name == "Infernal Plunge");
        Assert.Contains(deck.OneShots, o => o.Name == "Dark Ritual");
    }

    [Fact]
    public void Classify_NonRitualsAndArtifactFastMana_AreNotOneShotBurstMana()
    {
        var cards = new List<CardFact>
        {
            // Artifact fast mana — stays in the FastMana lane, NEVER a one-shot (O-2 guard).
            new()
            {
                Name = "Lotus Petal", Quantity = 1, ManaCost = "{0}", ManaValue = 0,
                TypeLine = "Artifact",
                OracleText = "{T}, Sacrifice this artifact: Add one mana of any color.",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
            new()
            {
                Name = "Lion's Eye Diamond", Quantity = 1, ManaCost = "{0}", ManaValue = 0,
                TypeLine = "Artifact",
                OracleText = "{T}, Sacrifice this artifact: Add three mana of any one color.",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
            // Sac-outlet engine (not a self-contained one-shot) — and an artifact anyway.
            new()
            {
                Name = "Ashnod's Altar", Quantity = 1, ManaCost = "{3}", ManaValue = 3,
                TypeLine = "Artifact", OracleText = "Sacrifice a creature: Add {C}{C}.",
                ProducedMana = new[] { "C" },
            },
            // A non-mana instant.
            new()
            {
                Name = "Opt", Quantity = 1, ManaCost = "{U}", ManaValue = 1,
                TypeLine = "Instant", OracleText = "Scry 1.\nDraw a card.", ProducedMana = Array.Empty<string>(),
            },
            // A sorcery that "adds" no more than it costs — not net-positive.
            new()
            {
                Name = "Break-Even Ritual", Quantity = 1, ManaCost = "{1}{R}", ManaValue = 2,
                TypeLine = "Sorcery", OracleText = "Add {R}{R}.", ProducedMana = new[] { "R" },
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.Empty(deck.OneShots);
    }

    [Fact]
    public void Classify_OneShotAndTriggeredManaProducers_AreNotRocksOrDorks()
    {
        // Efficacy R2 finding H2: Scryfall sets produced_mana on Treasure-makers (the token's
        // reminder text contains "Add one mana of any color"), one-shot sacrifice mana, and
        // sac-outlets. None of these is the persistent source the 0.5/0.75 partial weights model
        // — only a repeatable front-face "<cost>: Add" ability qualifies.
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Dockside Extortionist", Quantity = 1, ManaCost = "{1}{R}", ManaValue = 2,
                TypeLine = "Creature — Goblin Pirate",
                OracleText = "When this creature enters, create X Treasure tokens, where X is the number of "
                    + "artifacts and enchantments your opponents control. (Treasure tokens are artifacts with "
                    + "\"{T}, Sacrifice this token: Add one mana of any color.\")",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
            new()
            {
                Name = "Lotus Petal", Quantity = 1, ManaCost = "{0}", ManaValue = 0,
                TypeLine = "Artifact",
                OracleText = "{T}, Sacrifice this artifact: Add one mana of any color.",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
            new()
            {
                Name = "Ashnod's Altar", Quantity = 1, ManaCost = "{3}", ManaValue = 3,
                TypeLine = "Artifact",
                OracleText = "Sacrifice a creature: Add {C}{C}.",
                ProducedMana = new[] { "C" },
            },
            new()
            {
                Name = "Sol Ring", Quantity = 1, ManaCost = "{1}", ManaValue = 1,
                TypeLine = "Artifact",
                OracleText = "{T}: Add {C}{C}.",
                ProducedMana = new[] { "C" },
                ManaAmount = 2,
            },
            new()
            {
                Name = "Birds of Paradise", Quantity = 1, ManaCost = "{G}", ManaValue = 1,
                TypeLine = "Creature — Bird",
                OracleText = "Flying\n{T}: Add one mana of any color.",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
            new()
            {
                Name = "Arcane Signet", Quantity = 1, ManaCost = "{2}", ManaValue = 2,
                TypeLine = "Artifact",
                OracleText = "{T}: Add one mana of any color in your commander's color identity.",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        // One-shot / triggered producers: not sources, and visible in the castability rows.
        Assert.DoesNotContain(deck.Sources, s => s.Name == "Dockside Extortionist");
        Assert.DoesNotContain(deck.Sources, s => s.Name == "Lotus Petal");
        Assert.DoesNotContain(deck.Sources, s => s.Name == "Ashnod's Altar");
        Assert.False(Assert.Single(deck.Spells, s => s.Name == "Dockside Extortionist").IsManaSource);
        Assert.False(Assert.Single(deck.Spells, s => s.Name == "Ashnod's Altar").IsManaSource);

        // Genuine rocks/dorks keep their Karsten partial weights and row exclusion.
        Assert.Equal(0.75, Assert.Single(deck.Sources, s => s.Name == "Sol Ring").Weight);
        Assert.Equal(0.5, Assert.Single(deck.Sources, s => s.Name == "Birds of Paradise").Weight);
        Assert.Equal(0.75, Assert.Single(deck.Sources, s => s.Name == "Arcane Signet").Weight);
        Assert.True(Assert.Single(deck.Spells, s => s.Name == "Sol Ring").IsManaSource);
    }

    [Fact]
    public void Classify_GranterOnlyEquipment_IsNotItsOwnRock()
    {
        // Efficacy R2 finding H2 (Codex review): a quoted GRANTED ability ('Equipped creature has
        // "{T}: Add one mana of any color."') lives on another permanent, not the granter — but
        // Scryfall still sets produced_mana on the granter (Paradise Mantle: all five colors).
        // The quoted line must not make the Equipment read as its own 0.75 five-color rock.
        // A card with BOTH a granted line and its own "<cost>: Add" line (Chromatic Lantern)
        // stays a rock via its own line.
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Paradise Mantle", Quantity = 1, ManaCost = "{0}", ManaValue = 0,
                TypeLine = "Artifact — Equipment",
                OracleText = "Equipped creature has \"{T}: Add one mana of any color.\"\nEquip {1}",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
            new()
            {
                Name = "Chromatic Lantern", Quantity = 1, ManaCost = "{3}", ManaValue = 3,
                TypeLine = "Artifact",
                OracleText = "Lands you control have \"{T}: Add one mana of any color.\"\n{T}: Add one mana of any color.",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.DoesNotContain(deck.Sources, s => s.Name == "Paradise Mantle");
        Assert.False(Assert.Single(deck.Spells, s => s.Name == "Paradise Mantle").IsManaSource);
        Assert.Equal(0.75, Assert.Single(deck.Sources, s => s.Name == "Chromatic Lantern").Weight);
    }

    [Fact]
    public void Classify_SelfGrantedManaAbility_StaysADork()
    {
        // Codex review rounds 2-3: quoted grants that include the card ITSELF are its own
        // (conditional) mana ability — self pronouns (Honored Hierarch "it has", Mul Daya
        // Channelers "this creature has") AND collectives naming one of the card's own types
        // (Gemhide Sliver "All Slivers have", Katilda "Human creatures you control have",
        // Citanul Hierophants "Creatures you control have"). Other-grants (Paradise Mantle)
        // stay excluded (previous test).
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Honored Hierarch", Quantity = 1, ManaCost = "{G}", ManaValue = 1,
                TypeLine = "Creature — Human Druid",
                OracleText = "Renown 1\nAs long as this creature is renowned, it has \"{T}: Add one mana of any color.\"",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
            new()
            {
                Name = "Mul Daya Channelers", Quantity = 1, ManaCost = "{2}{G}", ManaValue = 3,
                TypeLine = "Creature — Elf Druid Shaman",
                OracleText = "Play with the top card of your library revealed.\n"
                    + "As long as the top card of your library is a land card, this creature has \"{T}: Add two mana of any one color.\"",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
            new()
            {
                Name = "Gemhide Sliver", Quantity = 1, ManaCost = "{1}{G}", ManaValue = 2,
                TypeLine = "Creature — Sliver",
                OracleText = "All Slivers have \"{T}: Add one mana of any color.\"",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
            new()
            {
                Name = "Katilda, Dawnhart Prime", Quantity = 1, ManaCost = "{G}{W}", ManaValue = 2,
                TypeLine = "Legendary Creature — Human Warlock",
                OracleText = "Ward {1}\nHuman creatures you control have \"{T}: Add one mana of any color.\"",
                ProducedMana = new[] { "W", "U", "B", "R", "G" },
            },
            new()
            {
                Name = "Citanul Hierophants", Quantity = 1, ManaCost = "{3}{G}", ManaValue = 4,
                TypeLine = "Creature — Human Druid",
                OracleText = "Creatures you control have \"{T}: Add {G}.\"",
                ProducedMana = new[] { "G" },
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.Equal(0.5, Assert.Single(deck.Sources, s => s.Name == "Honored Hierarch").Weight);
        Assert.Equal(0.5, Assert.Single(deck.Sources, s => s.Name == "Mul Daya Channelers").Weight);
        Assert.Equal(0.5, Assert.Single(deck.Sources, s => s.Name == "Gemhide Sliver").Weight);
        Assert.Equal(0.5, Assert.Single(deck.Sources, s => s.Name == "Katilda, Dawnhart Prime").Weight);
        Assert.Equal(0.5, Assert.Single(deck.Sources, s => s.Name == "Citanul Hierophants").Weight);
    }

    // --- Conditional-untapped lands: bond / check / Snarl (checkLandUntapped flag) ---

    [Fact]
    public void Classify_CheckLandUntapped_BondLand_EntersUntapped()
    {
        // Bond lands are unconditional in multiplayer Commander (2+ opponents), so no matching-type
        // sources are needed.
        var cards = new List<CardFact>
        {
            Land("Sea of Clouds", "Land", new[] { "W", "U" },
                "Sea of Clouds enters the battlefield tapped unless you have two or more opponents."),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        Assert.True(Assert.Single(deck.Sources, s => s.Name == "Sea of Clouds").EntersUntapped);
    }

    [Fact]
    public void Classify_CheckLandUntapped_BondLand_FlagOff_StaysTapped()
    {
        var cards = new List<CardFact>
        {
            Land("Sea of Clouds", "Land", new[] { "W", "U" },
                "Sea of Clouds enters the battlefield tapped unless you have two or more opponents."),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: false);

        Assert.False(Assert.Single(deck.Sources, s => s.Name == "Sea of Clouds").EntersUntapped);
    }

    [Fact]
    public void Classify_CheckLandUntapped_CheckLand_EnoughMatchingSources_EntersUntapped()
    {
        var cards = new List<CardFact>
        {
            Land("Glacial Fortress", "Land", new[] { "W", "U" },
                "Glacial Fortress enters the battlefield tapped unless you control a Plains or an Island."),
            Land("Island", "Basic Land — Island", new[] { "U" }, "{T}: Add {U}.") with { Quantity = 8 },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        Assert.True(Assert.Single(deck.Sources, s => s.Name == "Glacial Fortress").EntersUntapped);
    }

    [Fact]
    public void Classify_CheckLandUntapped_CheckLand_TooFewMatchingSources_StaysTapped()
    {
        // Only 2 matching-type sources (< threshold 6): the deck can't reliably turn the check land on.
        var cards = new List<CardFact>
        {
            Land("Glacial Fortress", "Land", new[] { "W", "U" },
                "Glacial Fortress enters the battlefield tapped unless you control a Plains or an Island."),
            Land("Island", "Basic Land — Island", new[] { "U" }, "{T}: Add {U}.") with { Quantity = 2 },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        Assert.False(Assert.Single(deck.Sources, s => s.Name == "Glacial Fortress").EntersUntapped);
    }

    [Fact]
    public void Classify_CheckLandUntapped_Snarl_EnoughMatchingSources_EntersUntapped()
    {
        var cards = new List<CardFact>
        {
            Land("Frostboil Snarl", "Land", new[] { "U", "R" },
                "As Frostboil Snarl enters, you may reveal an Island or Mountain card from your hand. "
                + "If you don't, Frostboil Snarl enters the battlefield tapped."),
            Land("Mountain", "Basic Land — Mountain", new[] { "R" }, "{T}: Add {R}.") with { Quantity = 8 },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        Assert.True(Assert.Single(deck.Sources, s => s.Name == "Frostboil Snarl").EntersUntapped);
    }

    [Fact]
    public void Classify_CheckLandUntapped_SlowLand_EmitsSlowLandCountCondition()
    {
        var cards = new List<CardFact>
        {
            Land("Deserted Beach", "Land", new[] { "W", "U" },
                "Deserted Beach enters the battlefield tapped unless you control two or more other lands."),
            Land("Island", "Basic Land — Island", new[] { "U" }, "{T}: Add {U}.") with { Quantity = 8 },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        ManaSource source = Assert.Single(deck.Sources, s => s.Name == "Deserted Beach");
        Assert.Equal(CountConditionKind.SlowLand, source.CountCondition);
        Assert.Equal(2, source.CountThreshold);
        Assert.Empty(source.CountTypeFilter);
    }

    [Fact]
    public void Classify_CheckLandUntapped_FastLand_EmitsFastLandCountCondition()
    {
        var cards = new List<CardFact>
        {
            Land("Seachrome Coast", "Land", new[] { "W", "U" },
                "Seachrome Coast enters the battlefield tapped unless you control two or fewer other lands."),
            Land("Island", "Basic Land — Island", new[] { "U" }, "{T}: Add {U}.") with { Quantity = 2 },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        ManaSource source = Assert.Single(deck.Sources, s => s.Name == "Seachrome Coast");
        Assert.Equal(CountConditionKind.FastLand, source.CountCondition);
        Assert.Equal(2, source.CountThreshold);
        Assert.Empty(source.CountTypeFilter);
    }

    [Fact]
    public void Classify_CheckLandUntapped_ThresholdLandNamingBasicType_EmitsEldMetadata()
    {
        var cards = new List<CardFact>
        {
            Land("Mystic Sanctuary", "Land — Island", new[] { "U" },
                "Mystic Sanctuary enters the battlefield tapped unless you control three or more other Islands."),
            Land("Island", "Basic Land — Island", new[] { "U" }, "{T}: Add {U}.") with { Quantity = 8 },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        ManaSource source = Assert.Single(deck.Sources, s => s.Name == "Mystic Sanctuary");
        Assert.Equal(CountConditionKind.EldThreshold, source.CountCondition);
        Assert.Equal(3, source.CountThreshold);
        Assert.Equal(new[] { "Island" }, source.CountTypeFilter);
    }

    [Fact]
    public void Classify_CheckLandUntapped_Verge_WithEnoughMatchingTypes_ProducesBothColors()
    {
        var cards = new List<CardFact>
        {
            Land("Floodfarm Verge", "Land", new[] { "W", "U" },
                "{T}: Add {W}. {T}: Add {U}. Activate only if you control a Plains or an Island."),
            Land("Island", "Basic Land — Island", new[] { "U" }, "{T}: Add {U}.") with { Quantity = 6 },
            Spell("Brago", 4, "{2}{W}{U}"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        ManaSource source = Assert.Single(deck.Sources, s => s.Name == "Floodfarm Verge");
        Assert.True(source.EntersUntapped);
        Assert.Equal(new[] { ManaColor.White, ManaColor.Blue }, source.Produces);
    }

    [Fact]
    public void Classify_CheckLandUntapped_Verge_WithTooFewMatchingTypes_ProducesOnlyFixedColor()
    {
        var cards = new List<CardFact>
        {
            Land("Floodfarm Verge", "Land", new[] { "W", "U" },
                "{T}: Add {W}. {T}: Add {U}. Activate only if you control a Plains or an Island."),
            Land("Island", "Basic Land — Island", new[] { "U" }, "{T}: Add {U}.") with { Quantity = 5 },
            Spell("Brago", 4, "{2}{W}{U}"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        ManaSource source = Assert.Single(deck.Sources, s => s.Name == "Floodfarm Verge");
        Assert.True(source.EntersUntapped);
        Assert.Equal(new[] { ManaColor.White }, source.Produces);
    }

    [Fact]
    public void Classify_CheckLandUntapped_BragoRegressionGuard_NimbusMaze_DoesNotMatchVergePath()
    {
        var cards = new List<CardFact>
        {
            Land("Nimbus Maze", "Land", new[] { "C", "W", "U" },
                "{T}: Add {C}. {T}: Add {W}. Activate only if you control an Island. "
                + "{T}: Add {U}. Activate only if you control a Plains."),
            Land("Island", "Basic Land — Island", new[] { "U" }, "{T}: Add {U}.") with { Quantity = 8 },
            Land("Plains", "Basic Land — Plains", new[] { "W" }, "{T}: Add {W}.") with { Quantity = 8 },
            Spell("Brago", 4, "{2}{W}{U}"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        ManaSource source = Assert.Single(deck.Sources, s => s.Name == "Nimbus Maze");
        Assert.True(source.EntersUntapped);
        Assert.Equal(new[] { ManaColor.Colorless, ManaColor.White, ManaColor.Blue }, source.Produces);
    }

    [Fact]
    public void Classify_CheckLandUntapped_TrainingCompound_WithEnoughTrueBasics_ProducesAllColorsAndColorless()
    {
        var cards = new List<CardFact>
        {
            Land("Training Compound", "Land", new[] { "C", "R", "G" },
                "{T}: Add {C}. {T}: Add {R} or {G}. Activate only if this land entered this turn or if you control a basic land."),
            Land("Mountain", "Basic Land — Mountain", new[] { "R" }, "{T}: Add {R}.") with { Quantity = 6 },
            Spell("Gruul Spell", 2, "{R}{G}"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        ManaSource source = Assert.Single(deck.Sources, s => s.Name == "Training Compound");
        Assert.True(source.EntersUntapped);
        Assert.Equal(new[] { ManaColor.Colorless, ManaColor.Red, ManaColor.Green }, source.Produces);
    }

    [Fact]
    public void Classify_CheckLandUntapped_TrainingCompound_WithTypedNonBasics_StaysColorlessOnly()
    {
        var cards = new List<CardFact>
        {
            Land("Training Compound", "Land", new[] { "C", "R", "G" },
                "{T}: Add {C}. {T}: Add {R} or {G}. Activate only if this land entered this turn or if you control a basic land."),
            Land("Stomping Ground", "Land — Mountain Forest", new[] { "R", "G" },
                "As Stomping Ground enters, you may pay 2 life. If you don't, it enters tapped.") with { Quantity = 8 },
            Spell("Gruul Spell", 2, "{R}{G}"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        ManaSource source = Assert.Single(deck.Sources, s => s.Name == "Training Compound");
        Assert.True(source.EntersUntapped);
        Assert.Equal(new[] { ManaColor.Colorless }, source.Produces);
    }

    [Fact]
    public void Classify_CheckLandUntapped_VividLand_IsTappedAndAddsOneConditionalAnyColorSource()
    {
        var cards = new List<CardFact>
        {
            Land("Vivid Meadow", "Land", new[] { "W", "U", "B", "R", "G" },
                "Vivid Meadow enters the battlefield tapped with two charge counters on it. "
                + "{T}: Add {W}. {T}, Remove a charge counter from this land: Add one mana of any color."),
            Spell("Azorius Spell", 2, "{W}{U}"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        ManaSource land = Assert.Single(deck.Sources, s => s.Name == "Vivid Meadow");
        Assert.False(land.EntersUntapped);
        Assert.Equal(new[] { ManaColor.White }, land.Produces);

        ManaSource vivid = Assert.Single(deck.Sources, s => s.Name == "Vivid Meadow (vivid)");
        Assert.False(vivid.IsLand);
        Assert.True(vivid.IsConditional);
        Assert.Equal(0.25, vivid.Weight);
        Assert.Equal(new[] { ManaColor.White, ManaColor.Blue }, vivid.Produces);
    }

    [Fact]
    public void Classify_CheckLandUntapped_ExactlyThresholdMatchingSources_EntersUntapped()
    {
        // Boundary: >= threshold (6) is inclusive.
        var cards = new List<CardFact>
        {
            Land("Glacial Fortress", "Land", new[] { "W", "U" },
                "Glacial Fortress enters the battlefield tapped unless you control a Plains or an Island."),
            Land("Island", "Basic Land — Island", new[] { "U" }, "{T}: Add {U}.") with { Quantity = 6 },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        Assert.True(Assert.Single(deck.Sources, s => s.Name == "Glacial Fortress").EntersUntapped);
    }

    [Fact]
    public void Classify_CheckLandUntapped_DualTypeSourcesCountedOnce_NotDoubled()
    {
        // Union count: 5 dual-type (Plains AND Island) lands count as 5 toward a {Plains, Island} check
        // land, not 10 — so 5 < threshold 6 leaves it tapped. (A sum-per-type bug would read 10 and
        // wrongly flip it untapped.)
        var cards = new List<CardFact>
        {
            Land("Glacial Fortress", "Land", new[] { "W", "U" },
                "Glacial Fortress enters the battlefield tapped unless you control a Plains or an Island."),
            Land("Hallowed Fountain", "Land — Plains Island", new[] { "W", "U" },
                "As Hallowed Fountain enters, you may pay 2 life. If you don't, it enters tapped.") with { Quantity = 5 },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: true);

        Assert.False(Assert.Single(deck.Sources, s => s.Name == "Glacial Fortress").EntersUntapped);
    }

    [Fact]
    public void Classify_CheckLandUntapped_FlagOff_AllStayTapped()
    {
        // Byte-identical guard: with the flag off, bond and an otherwise-online check land both stay
        // on the historic tapped path.
        var cards = new List<CardFact>
        {
            Land("Sea of Clouds", "Land", new[] { "W", "U" },
                "Sea of Clouds enters the battlefield tapped unless you have two or more opponents."),
            Land("Glacial Fortress", "Land", new[] { "W", "U" },
                "Glacial Fortress enters the battlefield tapped unless you control a Plains or an Island."),
            Land("Island", "Basic Land — Island", new[] { "U" }, "{T}: Add {U}.") with { Quantity = 8 },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, checkLandUntapped: false);

        Assert.False(Assert.Single(deck.Sources, s => s.Name == "Sea of Clouds").EntersUntapped);
        Assert.False(Assert.Single(deck.Sources, s => s.Name == "Glacial Fortress").EntersUntapped);
    }

    [Fact]
    public void Classify_RestrictedLands_CavernInTribalDeck_UsesDominantTypeShare()
    {
        var cards = new List<CardFact>
        {
            Land(
                "Cavern of Souls",
                "Land",
                new[] { "C", "W", "U", "B", "R", "G" },
                "As Cavern of Souls enters, choose a creature type. {T}: Add {C}. {T}: Add one mana of any color. "
                + "Spend this mana only to cast a creature spell of the chosen type, and that spell can't be countered."),
            CreatureSpell("Elf One", "{G}", "Creature — Elf Druid") with { Quantity = 5 },
            CreatureSpell("Elf Two", "{1}{G}", "Creature — Elf Warrior") with { Quantity = 5 },
            CreatureSpell("Goblin Splash", "{R}", "Creature — Goblin") with { Quantity = 1 },
            NonCreatureSpell("Arcane Signet", "{2}", 2, "Artifact"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, restrictedLands: true);

        ManaSource cavern = Assert.Single(deck.Sources, s => s.Name == "Cavern of Souls");
        Assert.InRange(cavern.Weight, 0.90, 0.91);
        Assert.Contains("Cavern of Souls", deck.RestrictedSourceLandNames);
        Assert.True(deck.HasRestrictedSourceApproximation);
    }

    [Fact]
    public void Classify_RestrictedLands_CavernInMixedTypeDeck_IsHeavilyDiscounted()
    {
        var cards = new List<CardFact>
        {
            Land(
                "Cavern of Souls",
                "Land",
                new[] { "C", "W", "U", "B", "R", "G" },
                "As Cavern of Souls enters, choose a creature type. {T}: Add {C}. {T}: Add one mana of any color. "
                + "Spend this mana only to cast a creature spell of the chosen type, and that spell can't be countered."),
            CreatureSpell("Elf Captain", "{G}", "Creature — Elf Warrior") with { Quantity = 2 },
            CreatureSpell("Goblin Guide", "{R}", "Creature — Goblin Scout"),
            CreatureSpell("Merfolk Looter", "{1}{U}", "Creature — Merfolk Rogue"),
            CreatureSpell("Human Knight", "{1}{W}", "Creature — Human Knight"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, restrictedLands: true);

        ManaSource cavern = Assert.Single(deck.Sources, s => s.Name == "Cavern of Souls");
        Assert.Equal(0.4, cavern.Weight, 3);
    }

    [Fact]
    public void Classify_RestrictedLands_Ziggurat_UsesCreatureShare()
    {
        var cards = new List<CardFact>
        {
            Land(
                "Ancient Ziggurat",
                "Land",
                new[] { "W", "U", "B", "R", "G" },
                "{T}: Add one mana of any color. Spend this mana only to cast a creature spell."),
            CreatureSpell("Creature One", "{G}", "Creature — Elf") with { Quantity = 3 },
            CreatureSpell("Creature Two", "{1}{W}", "Creature — Human") with { Quantity = 3 },
            NonCreatureSpell("Ponder", "{U}", 1, "Sorcery") with { Quantity = 2 },
            NonCreatureSpell("Swords to Plowshares", "{W}", 1, "Instant") with { Quantity = 2 },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, restrictedLands: true);

        ManaSource ziggurat = Assert.Single(deck.Sources, s => s.Name == "Ancient Ziggurat");
        Assert.Equal(0.6, ziggurat.Weight, 3);
        Assert.Contains("Ancient Ziggurat", deck.RestrictedSourceLandNames);
    }

    [Fact]
    public void Classify_RestrictedLands_Nykthos_AddsLowWeightConditionalSource()
    {
        var cards = new List<CardFact>
        {
            Land(
                "Nykthos, Shrine to Nyx",
                "Legendary Land",
                new[] { "C", "G" },
                "{T}: Add {C}. {2}, {T}: Choose a color. Add an amount of mana of that color equal to your devotion to that color."),
            CreatureSpell("Llanowar Elves", "{G}", "Creature — Elf Druid"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, restrictedLands: true);

        ManaSource nykthos = Assert.Single(deck.Sources, s => s.Name == "Nykthos, Shrine to Nyx");
        Assert.Equal(new[] { ManaColor.Colorless }, nykthos.Produces);

        ManaSource devotion = Assert.Single(deck.Sources, s => s.Name == "Nykthos, Shrine to Nyx (devotion)");
        Assert.True(devotion.IsConditional);
        Assert.Equal(0.25, devotion.Weight);
        Assert.Equal(new[] { ManaColor.Green }, devotion.Produces);
    }

    [Fact]
    public void Classify_RestrictedLands_PopulatesPresentLandNames()
    {
        var cards = new List<CardFact>
        {
            Land(
                "Cavern of Souls",
                "Land",
                new[] { "C", "W", "U", "B", "R", "G" },
                "As Cavern of Souls enters, choose a creature type. {T}: Add {C}. {T}: Add one mana of any color. "
                + "Spend this mana only to cast a creature spell of the chosen type, and that spell can't be countered."),
            Land(
                "Unclaimed Territory",
                "Land",
                new[] { "C", "W", "U", "B", "R", "G" },
                "As Unclaimed Territory enters, choose a creature type. {T}: Add {C}. {T}: Add one mana of any color. "
                + "Spend this mana only to cast a creature spell of the chosen type."),
            Land(
                "Ancient Ziggurat",
                "Land",
                new[] { "W", "U", "B", "R", "G" },
                "{T}: Add one mana of any color. Spend this mana only to cast a creature spell."),
            Land(
                "Nykthos, Shrine to Nyx",
                "Legendary Land",
                new[] { "C", "G" },
                "{T}: Add {C}. {2}, {T}: Choose a color. Add an amount of mana of that color equal to your devotion to that color."),
            CreatureSpell("Elf Body", "{G}", "Creature — Elf Druid"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, restrictedLands: true);

        Assert.Equal(
            new[]
            {
                "Cavern of Souls",
                "Unclaimed Territory",
                "Ancient Ziggurat",
                "Nykthos, Shrine to Nyx",
            },
            deck.RestrictedSourceLandNames);
    }

    [Fact]
    public void Classify_RestrictedLands_NoRestrictedLandDeck_RemainsUnchangedAndNamesStayEmpty()
    {
        var cards = new List<CardFact>
        {
            Land("Island", "Basic Land — Island", new[] { "U" }, "{T}: Add {U}.") with { Quantity = 8 },
            CreatureSpell("Merfolk Trickster", "{1}{U}", "Creature — Merfolk Wizard"),
            NonCreatureSpell("Counterspell", "{U}{U}", 2, "Instant"),
        };

        ManabaseDeck off = ManabaseClassifier.Classify(cards);
        ManabaseDeck on = ManabaseClassifier.Classify(cards, restrictedLands: true);

        Assert.Equal(
            off.Sources.Select(SourceShape),
            on.Sources.Select(SourceShape));
        Assert.Equal(
            off.Spells.Select(SpellShape),
            on.Spells.Select(SpellShape));
        Assert.Equal(off.RampAndDrawUnderThree, on.RampAndDrawUnderThree);
        Assert.Empty(on.RestrictedSourceLandNames);
        Assert.False(on.HasRestrictedSourceApproximation);
    }

    [Fact]
    public void Classify_CheapNonLandScrySpell_CountsTowardScrySourceCredit()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Opt",
                Quantity = 1,
                ManaCost = "{U}",
                ManaValue = 1,
                TypeLine = "Instant",
                OracleText = "Scry 1, then draw a card.",
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.Equal(1, deck.ScrySourceCreditCopies);
    }

    [Fact]
    public void Classify_ScryLand_DoesNotCountTowardScrySourceCredit()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Temple of Epiphany",
                Quantity = 1,
                ManaCost = string.Empty,
                ManaValue = 0,
                TypeLine = "Land",
                OracleText = "Temple of Epiphany enters tapped.\nWhen Temple of Epiphany enters, scry 1.\n{T}: Add {U} or {R}.",
                ProducedMana = new[] { "U", "R" },
                HasLandFace = true,
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.Equal(0, deck.ScrySourceCreditCopies);
    }

    [Fact]
    public void Classify_ManaValueThreeScrySpell_DoesNotCountTowardScrySourceCredit()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Cryptic Speculation",
                Quantity = 1,
                ManaCost = "{2}{U}",
                ManaValue = 3,
                TypeLine = "Sorcery",
                OracleText = "Scry 3.",
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.Equal(0, deck.ScrySourceCreditCopies);
    }

    [Fact]
    public void Classify_ReminderTextOnlyScryMatch_DoesNotCountTowardScrySourceCredit()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Reminder Mirage",
                Quantity = 1,
                ManaCost = "{U}",
                ManaValue = 1,
                TypeLine = "Instant",
                OracleText = "Draw a card. (Scry 1.)",
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.Equal(0, deck.ScrySourceCreditCopies);
    }

    private static string SourceShape(ManaSource source) =>
        $"{source.Name}|{string.Join(',', source.Produces)}|{source.Weight:F3}|{source.IsLand}|{source.EntersUntapped}|"
        + $"{source.ManaAmount}|{source.IsConditional}|{source.CountCondition}|{source.CountThreshold}|"
        + $"{string.Join(',', source.CountTypeFilter)}";

    private static string SpellShape(SpellRequirement spell) =>
        $"{spell.Name}|{spell.ManaValue}|{string.Join(',', spell.Pips.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}"))}|"
        + $"{spell.IsGold}|{spell.IsManaSource}|{spell.Kinds}|{spell.PlanRoles}|{spell.IsCommander}|{spell.IsCostOverridden}";

    private static CardFact CreatureSpell(string name, string manaCost, string typeLine) => new()
    {
        Name = name,
        Quantity = 1,
        ManaCost = manaCost,
        ManaValue = ManaCostParser.Parse(manaCost).ManaValue,
        TypeLine = typeLine,
        OracleText = string.Empty,
        ProducedMana = System.Array.Empty<string>(),
    };

    private static CardFact NonCreatureSpell(string name, string manaCost, int manaValue, string typeLine) => new()
    {
        Name = name,
        Quantity = 1,
        ManaCost = manaCost,
        ManaValue = manaValue,
        TypeLine = typeLine,
        OracleText = string.Empty,
        ProducedMana = System.Array.Empty<string>(),
    };
}
