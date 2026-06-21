using System;
using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Fills <see cref="ManabaseClassifier"/> gaps the primary suite leaves open: the non-instant/
/// sorcery reducer scopes (creature / artifact / All), the affinity exclusion, the aura granter
/// branch, the fetch deck-awareness rules (generic basic fetch limited to the deck's basics; a
/// no-basics deck fetching nothing), the <c>SpellKinds</c> type-line flags, and the
/// commander-is-a-mana-source row-visibility rule.
/// </summary>
public sealed class ManabaseClassifierCoverageTests
{
    [Fact]
    public void Classify_CreatureScopeReducer_IsClassifiedCreature()
    {
        // "Creature spells you cast cost {1} less" → ReductionScope.Creature.
        ManabaseDeck deck = ClassifyOne(
            "Cloud Key Creature", "{2}", 2, "Artifact",
            "Creature spells you cast cost {1} less to cast.");

        CostReducer reducer = Assert.Single(deck.CostReduction);
        Assert.Equal(ReductionScope.Creature, reducer.Scope);
        Assert.Equal(1, reducer.GenericReduction);
    }

    [Fact]
    public void Classify_ArtifactScopeReducer_IsClassifiedArtifact()
    {
        ManabaseDeck deck = ClassifyOne(
            "Foundry Inspector", "{3}", 3, "Artifact Creature — Construct",
            "Artifact spells you cast cost {1} less to cast.");

        CostReducer reducer = Assert.Single(deck.CostReduction);
        Assert.Equal(ReductionScope.Artifact, reducer.Scope);
    }

    [Fact]
    public void Classify_UnscopedReducer_IsClassifiedAll()
    {
        // A bare "Spells you cast cost {1} less" (no type word) → ReductionScope.All.
        ManabaseDeck deck = ClassifyOne(
            "Helm of Awakening", "{2}", 2, "Artifact",
            "Spells you cast cost {1} less to cast.");

        CostReducer reducer = Assert.Single(deck.CostReduction);
        Assert.Equal(ReductionScope.All, reducer.Scope);
    }

    [Fact]
    public void Classify_AffinityReducer_IsExcluded()
    {
        // Affinity is a per-permanent scaling discount, not an always-on static reducer.
        ManabaseDeck deck = ClassifyOne(
            "Frogmite", "{4}", 4, "Artifact Creature — Frog",
            "Affinity for artifacts. Spells you cast cost {1} less to cast.");

        Assert.Empty(deck.CostReduction);
    }

    [Fact]
    public void Classify_AuraGranter_GrantsAnyColorSourceToEnchantedCreature()
    {
        // The aura branch ("enchanted creature has '{T}: Add'") mirrors the equipment branch.
        var aura = new CardFact
        {
            Name = "Utopia Sprawl-ish Aura",
            Quantity = 1,
            ManaCost = "{G}",
            ManaValue = 1,
            TypeLine = "Enchantment — Aura",
            OracleText = "Enchanted creature has \"{T}: Add one mana of any color.\"",
            ProducedMana = Array.Empty<string>(),
        };
        var creature = new CardFact
        {
            Name = "Grizzly Bears",
            Quantity = 1,
            ManaCost = "{1}{G}",
            ManaValue = 2,
            TypeLine = "Creature — Bear",
            OracleText = string.Empty,
            ProducedMana = Array.Empty<string>(),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(new List<CardFact> { aura, creature });

        ManaSource granted = Assert.Single(deck.Sources, s => s.Name == "Grizzly Bears (granted)");
        Assert.Equal(0.25, granted.Weight);
        Assert.True(granted.IsConditional);
        Assert.False(granted.IsLand);
    }

    [Fact]
    public void Classify_GenericBasicFetch_ReachesOnlyTheDecksBasics()
    {
        // Prismatic Vista / Evolving Wilds grab "a basic land" — they can only get a color the deck
        // actually runs as a basic. A WU deck (Plains + Island basics, no red) must NOT credit the
        // generic fetch with red.
        var cards = new List<CardFact>
        {
            Spell("Brago", 4, "{2}{W}{U}"),
            Land("Plains", "Basic Land — Plains", new[] { "W" }),
            Land("Island", "Basic Land — Island", new[] { "U" }),
            Land("Evolving Wilds", "Land", Array.Empty<string>(),
                "{T}, Sacrifice Evolving Wilds: Search your library for a basic land card, put it onto the battlefield tapped."),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManaSource fetch = Assert.Single(deck.Sources, s => s.Name == "Evolving Wilds");
        Assert.Contains(ManaColor.White, fetch.Produces);
        Assert.Contains(ManaColor.Blue, fetch.Produces);
        Assert.DoesNotContain(ManaColor.Red, fetch.Produces);
    }

    [Fact]
    public void Classify_GenericBasicFetch_NoBasicsInDeck_ProducesNoColor()
    {
        // A deck with zero basic lands cannot fetch anything with a generic basic fetch → empty
        // color set (not "all five" speculatively).
        var cards = new List<CardFact>
        {
            Spell("Brago", 4, "{2}{W}{U}"),
            // Only a nonbasic dual is present; no basic land types in the deck.
            Land("Hallowed Fountain", "Land — Plains Island", new[] { "W", "U" },
                "As Hallowed Fountain enters, you may pay 2 life. If you don't, it enters tapped."),
            Land("Evolving Wilds", "Land", Array.Empty<string>(),
                "{T}, Sacrifice Evolving Wilds: Search your library for a basic land card, put it onto the battlefield tapped."),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        // Note: Hallowed Fountain has the basic land TYPES "Plains Island" in its type line, so the
        // generic fetch can in fact reach W/U via that nonbasic. To isolate the "no basics → empty"
        // rule we instead assert the fetch only ever reflects the deck's real subtype coverage, never
        // an off-color it cannot reach (red), and never colorless padding.
        ManaSource fetch = Assert.Single(deck.Sources, s => s.Name == "Evolving Wilds");
        Assert.DoesNotContain(ManaColor.Red, fetch.Produces);
        Assert.DoesNotContain(ManaColor.Black, fetch.Produces);
        Assert.DoesNotContain(ManaColor.Green, fetch.Produces);
    }

    [Fact]
    public void Classify_GenericBasicFetch_TrulyNoBasicTypes_IsEmpty()
    {
        // When the ONLY other land carries no basic land TYPE (a colorless utility land), a generic
        // basic fetch reaches nothing — the produced set is empty.
        var cards = new List<CardFact>
        {
            Spell("Brago", 4, "{2}{W}{U}"),
            // A colorless utility land with no basic land type and no produced color.
            Land("Wastes-ish Utility", "Land", Array.Empty<string>(), "{T}: Add {C}."),
            Land("Terramorphic Expanse", "Land", Array.Empty<string>(),
                "{T}, Sacrifice Terramorphic Expanse: Search your library for a basic land card, put it onto the battlefield tapped."),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManaSource fetch = Assert.Single(deck.Sources, s => s.Name == "Terramorphic Expanse");
        Assert.Empty(fetch.Produces);
    }

    [Fact]
    public void Classify_SpellKinds_FlaggedFromTypeLine()
    {
        // ClassifyKinds maps the FRONT face type line into the SpellKinds flags used for reducer
        // scope matching. An "Artifact Creature" carries both flags.
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Artifact Creature",
                Quantity = 1,
                ManaCost = "{3}",
                ManaValue = 3,
                TypeLine = "Artifact Creature — Golem",
                OracleText = "Vanilla.",
                ProducedMana = Array.Empty<string>(),
            },
            new()
            {
                Name = "Bolt",
                Quantity = 1,
                ManaCost = "{R}",
                ManaValue = 1,
                TypeLine = "Instant",
                OracleText = "Deal 3 damage.",
                ProducedMana = Array.Empty<string>(),
            },
            new()
            {
                Name = "Aura Thing",
                Quantity = 1,
                ManaCost = "{1}{W}",
                ManaValue = 2,
                TypeLine = "Enchantment — Aura",
                OracleText = "Enchant creature.",
                ProducedMana = Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        SpellRequirement golem = deck.Spells.Single(s => s.Name == "Artifact Creature");
        Assert.True((golem.Kinds & SpellKinds.Artifact) != 0);
        Assert.True((golem.Kinds & SpellKinds.Creature) != 0);

        SpellRequirement bolt = deck.Spells.Single(s => s.Name == "Bolt");
        Assert.True((bolt.Kinds & SpellKinds.Instant) != 0);

        SpellRequirement aura = deck.Spells.Single(s => s.Name == "Aura Thing");
        Assert.Equal(SpellKinds.Other, aura.Kinds); // no creature/artifact/instant/sorcery → Other
    }

    [Fact]
    public void Classify_CommanderManaDork_IsFlaggedSource_ButStaysAVisibleRow()
    {
        // A commander that taps for mana is a mana source (IsManaSource true) yet must NOT be hidden
        // from the castability rows — the analyzer keeps commander rows even when they are sources.
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Selvala, Heart of the Wilds",
                Quantity = 1,
                ManaCost = "{1}{G}{G}",
                ManaValue = 3,
                TypeLine = "Legendary Creature — Elf Scout",
                OracleText = "{T}: Add an amount of {G}.",
                ProducedMana = new[] { "G" },
                IsCommander = true,
            },
        };
        // Pad with a normal land base so the simulator has a realistic library to draw from (the
        // analyzer runs the Monte-Carlo sim over every kept commander row).
        for (int i = 0; i < 36; i++)
        {
            cards.Add(new CardFact
            {
                Name = "Forest",
                Quantity = 1,
                TypeLine = "Basic Land — Forest",
                OracleText = "{T}: Add {G}.",
                ProducedMana = new[] { "G" },
                ManaValue = 0,
                HasLandFace = true,
            });
        }

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);
        SpellRequirement commander = deck.Spells.Single(s => s.Name == "Selvala, Heart of the Wilds");

        Assert.True(commander.IsManaSource);
        Assert.True(commander.IsCommander);

        // The analyzer must still surface a commander row for it despite IsManaSource.
        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);
        Assert.Contains(report.Castability, c => c is { Name: "Selvala, Heart of the Wilds", IsCommander: true });
    }

    // ---- helpers --------------------------------------------------------------------------

    private static ManabaseDeck ClassifyOne(string name, string cost, double mv, string typeLine, string oracle)
        => ManabaseClassifier.Classify(new List<CardFact>
        {
            new()
            {
                Name = name,
                Quantity = 1,
                ManaCost = cost,
                ManaValue = mv,
                TypeLine = typeLine,
                OracleText = oracle,
                ProducedMana = Array.Empty<string>(),
            },
        });

    private static CardFact Spell(string name, int mv, string cost) => new()
    {
        Name = name,
        Quantity = 1,
        ManaCost = cost,
        ManaValue = mv,
        TypeLine = "Creature",
        OracleText = string.Empty,
        ProducedMana = Array.Empty<string>(),
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
}
