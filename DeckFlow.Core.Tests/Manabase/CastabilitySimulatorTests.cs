using System.Collections.Generic;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CastabilitySimulatorTests
{
    [Fact]
    public void SimulateCompanion_HigherPreTaxManaValueLowersCastability()
    {
        ManabaseDeck deck = BuildCompanionDeck();
        SpellRequirement printed = new()
        {
            Name = "Jegantha, the Wellspring",
            ManaValue = 5,
            Pips = Pip((ManaColor.Red, 1), (ManaColor.Green, 1)),
        };
        SpellRequirement companionTaxed = printed with { ManaValue = 8 };

        CardCastability printedRow = ManabaseAnalyzer.SimulateCompanion(deck, printed);
        CardCastability companionRow = ManabaseAnalyzer.SimulateCompanion(deck, companionTaxed);

        Assert.True(companionRow.CastPercent < printedRow.CastPercent,
            $"taxed companion castability {companionRow.CastPercent} should be below printed-cost {printedRow.CastPercent}");
    }

    [Fact]
    public void SimulateCompanion_UsesDeckLibrarySizeExcludingCommanders()
    {
        ManabaseDeck deck = BuildCompanionDeck() with { CommanderCount = 2 };
        SpellRequirement companionSpell = new()
        {
            Name = "Kaheera, the Orphanguard",
            ManaValue = 6,
            Pips = Pip((ManaColor.White, 1)),
        };

        CardCastability viaHelper = ManabaseAnalyzer.SimulateCompanion(deck, companionSpell);
        CardCastability direct = CastabilitySimulator.Simulate(
            deck,
            deck.TotalCards - deck.CommanderCount,
            companionSpell,
            companionSpell.ManaValue,
            genericReduction: 0);

        // MULLIGAN-01: RepresentativeOpeners is a List<T> (reference-equality field on the record), so
        // it is compared separately via xUnit's structural IEnumerable<T> comparison; both calls are the
        // same deterministic seeded run and their contents match element-for-element. The rest of the
        // record still compares by full value equality.
        Assert.Equal(direct.RepresentativeOpeners, viaHelper.RepresentativeOpeners);
        Assert.Equal(
            direct with { RepresentativeOpeners = Array.Empty<OpeningHandSample>() },
            viaHelper with { RepresentativeOpeners = Array.Empty<OpeningHandSample>() });
    }

    [Fact]
    public void Simulate_ByTurn3HoldableTrials_AreDeterministicAndRespectUntappedCoverage()
    {
        const int trials = 2000;

        SpellRequirement monoBlueInteraction = new()
        {
            Name = "Counterspell",
            ManaValue = 2,
            Pips = Pip((ManaColor.Blue, 2)),
            PlanRoles = PlanRole.Interaction,
        };
        SpellRequirement scarceWhiteInteraction = new()
        {
            Name = "Dovin's Veto",
            ManaValue = 2,
            Pips = Pip((ManaColor.White, 1), (ManaColor.Blue, 1)),
            PlanRoles = PlanRole.Interaction,
        };
        SpellRequirement colorlessInteraction = new()
        {
            Name = "Warping Wail",
            ManaValue = 2,
            Pips = Pip(),
            PlanRoles = PlanRole.Interaction,
        };

        ManabaseDeck monoBlueDeck = BuildInteractionDeck(
            ("Island", 60, new[] { ManaColor.Blue }, true),
            ("Mystic Sanctuary", 4, new[] { ManaColor.Blue }, false));
        ManabaseDeck scarceWhiteDeck = BuildInteractionDeck(
            ("Island", 24, new[] { ManaColor.Blue }, true),
            ("Plains", 4, new[] { ManaColor.White }, true),
            ("Azorius Guildgate", 12, new[] { ManaColor.White, ManaColor.Blue }, false));
        ManabaseDeck colorlessDeck = BuildInteractionDeck(
            ("Wastes", 99, Array.Empty<ManaColor>(), true));

        CardCastability monoBlueA = CastabilitySimulator.Simulate(
            monoBlueDeck,
            monoBlueDeck.TotalCards - monoBlueDeck.CommanderCount,
            monoBlueInteraction,
            monoBlueInteraction.ManaValue,
            genericReduction: 0,
            trials: trials);
        CardCastability monoBlueB = CastabilitySimulator.Simulate(
            monoBlueDeck,
            monoBlueDeck.TotalCards - monoBlueDeck.CommanderCount,
            monoBlueInteraction,
            monoBlueInteraction.ManaValue,
            genericReduction: 0,
            trials: trials);
        CardCastability scarceWhite = CastabilitySimulator.Simulate(
            scarceWhiteDeck,
            scarceWhiteDeck.TotalCards - scarceWhiteDeck.CommanderCount,
            scarceWhiteInteraction,
            scarceWhiteInteraction.ManaValue,
            genericReduction: 0,
            trials: trials);
        CardCastability colorless = CastabilitySimulator.Simulate(
            colorlessDeck,
            colorlessDeck.TotalCards - colorlessDeck.CommanderCount,
            colorlessInteraction,
            colorlessInteraction.ManaValue,
            genericReduction: 0,
            trials: trials);

        int monoBlueHoldablePercent = Percent(monoBlueA.ByTurn3HoldableTrials, trials);
        int scarceWhiteHoldablePercent = Percent(scarceWhite.ByTurn3HoldableTrials, trials);
        int colorlessHoldablePercent = Percent(colorless.ByTurn3HoldableTrials, trials);

        Assert.Equal(monoBlueA.ByTurn3HoldableTrials, monoBlueB.ByTurn3HoldableTrials);
        Assert.InRange(monoBlueHoldablePercent, 98, 100);
        Assert.True(
            scarceWhiteHoldablePercent < monoBlueHoldablePercent,
            $"scarce-white holdable {scarceWhiteHoldablePercent}% should be below mono-blue {monoBlueHoldablePercent}%");
        Assert.Equal(100, colorlessHoldablePercent);
    }

    [Fact]
    public void Simulate_CommanderEarlyCastPercents_LengthMatchesOnCurveMinusOne()
    {
        ManabaseDeck deck = BuildCommanderEarlyCastDeck(includeFastMana: false);
        SpellRequirement commander = deck.Spells.Single(s => s.IsCommander);

        CardCastability row = CastabilitySimulator.Simulate(
            deck,
            deck.TotalCards - deck.CommanderCount,
            commander,
            commander.ManaValue,
            genericReduction: 0,
            trials: 2000,
            useManaQuantity: true);

        Assert.Equal(commander.ManaValue - 1, row.EarlyCastPercents.Count);
    }

    [Fact]
    public void Simulate_CommanderWithoutRamp_HasZeroEarlyCastPercents()
    {
        ManabaseDeck deck = BuildCommanderEarlyCastDeck(includeFastMana: false);
        SpellRequirement commander = deck.Spells.Single(s => s.IsCommander);

        CardCastability row = CastabilitySimulator.Simulate(
            deck,
            deck.TotalCards - deck.CommanderCount,
            commander,
            commander.ManaValue,
            genericReduction: 0,
            trials: 2000,
            useManaQuantity: true);

        Assert.Equal(new[] { 0, 0, 0 }, row.EarlyCastPercents);
    }

    [Fact]
    public void Simulate_CommanderWithFastMana_CanBeCastBeforeOnCurveTurn()
    {
        ManabaseDeck deck = BuildCommanderEarlyCastDeck(includeFastMana: true);
        SpellRequirement commander = deck.Spells.Single(s => s.IsCommander);

        CardCastability row = CastabilitySimulator.Simulate(
            deck,
            deck.TotalCards - deck.CommanderCount,
            commander,
            commander.ManaValue,
            genericReduction: 0,
            trials: 5000,
            useManaQuantity: true);

        Assert.Equal(commander.ManaValue - 1, row.EarlyCastPercents.Count);
        Assert.True(
            row.EarlyCastPercents[2] > 0,
            $"expected some turn-3 early commander casts, got [{string.Join(", ", row.EarlyCastPercents)}]");
    }

    [Fact]
    public void Simulate_NonCommanderRows_HaveEmptyEarlyCastPercents()
    {
        ManabaseDeck deck = BuildCommanderEarlyCastDeck(includeFastMana: true);
        SpellRequirement spell = new()
        {
            Name = "Cultivate",
            ManaValue = 3,
            Pips = Pip((ManaColor.Green, 1)),
        };

        CardCastability row = CastabilitySimulator.Simulate(
            deck,
            deck.TotalCards - deck.CommanderCount,
            spell,
            spell.ManaValue,
            genericReduction: 0,
            trials: 2000,
            useManaQuantity: true);

        Assert.Empty(row.EarlyCastPercents);
    }

    private static ManabaseDeck BuildCompanionDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 18; i++)
        {
            sources.Add(new ManaSource { Name = $"Mountain {i}", Produces = new[] { ManaColor.Red }, IsLand = true });
        }

        for (int i = 0; i < 18; i++)
        {
            sources.Add(new ManaSource { Name = $"Forest {i}", Produces = new[] { ManaColor.Green }, IsLand = true });
        }

        for (int i = 0; i < 6; i++)
        {
            sources.Add(new ManaSource { Name = $"Command Tower {i}", Produces = new[] { ManaColor.Red, ManaColor.Green, ManaColor.White }, IsLand = true });
        }

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 3.0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Commander", ManaValue = 4, Pips = Pip((ManaColor.Red, 1), (ManaColor.Green, 1)), IsCommander = true },
            },
            IsSingleton = true,
        };
    }

    private static ManabaseDeck BuildInteractionDeck(params (string Name, int Count, ManaColor[] Colors, bool Untapped)[] lands)
    {
        var sources = new List<ManaSource>();
        foreach ((string name, int count, ManaColor[] colors, bool untapped) in lands)
        {
            for (int i = 0; i < count; i++)
            {
                sources.Add(new ManaSource
                {
                    Name = $"{name} {i}",
                    Produces = colors,
                    IsLand = true,
                    EntersUntapped = untapped,
                });
            }
        }

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new()
                {
                    Name = "Commander",
                    ManaValue = 3,
                    Pips = Pip((ManaColor.Blue, 1)),
                    IsCommander = true,
                },
            },
            IsSingleton = true,
        };
    }

    private static ManabaseDeck BuildCommanderEarlyCastDeck(bool includeFastMana)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 18; i++)
        {
            sources.Add(new ManaSource
            {
                Name = $"Mountain {i}",
                Produces = new[] { ManaColor.Red },
                IsLand = true,
            });
        }

        for (int i = 0; i < 17; i++)
        {
            sources.Add(new ManaSource
            {
                Name = $"Forest {i}",
                Produces = new[] { ManaColor.Green },
                IsLand = true,
            });
        }

        if (includeFastMana)
        {
            sources.Add(new ManaSource
            {
                Name = "Mana Crypt",
                Produces = Array.Empty<ManaColor>(),
                IsLand = false,
                ProducesColorless = true,
                ManaAmount = 2,
            });
            sources.Add(new ManaSource
            {
                Name = "Sol Ring",
                Produces = Array.Empty<ManaColor>(),
                IsLand = false,
                ProducesColorless = true,
                ManaAmount = 2,
            });
            sources.Add(new ManaSource
            {
                Name = "Arcane Signet",
                Produces = new[] { ManaColor.Red, ManaColor.Green },
                IsLand = false,
            });
            sources.Add(new ManaSource
            {
                Name = "Talisman of Impulse",
                Produces = new[] { ManaColor.Red, ManaColor.Green },
                IsLand = false,
            });
        }

        var spells = new List<SpellRequirement>
        {
            new()
            {
                Name = "Commander",
                ManaValue = 4,
                Pips = Pip((ManaColor.Red, 1), (ManaColor.Green, 1)),
                IsCommander = true,
            },
        };

        if (includeFastMana)
        {
            spells.Add(new SpellRequirement
            {
                Name = "Mana Crypt",
                ManaValue = 0,
                Pips = Pip(),
                IsManaSource = true,
            });
            spells.Add(new SpellRequirement
            {
                Name = "Sol Ring",
                ManaValue = 1,
                Pips = Pip(),
                IsManaSource = true,
            });
            spells.Add(new SpellRequirement
            {
                Name = "Arcane Signet",
                ManaValue = 2,
                Pips = Pip(),
                IsManaSource = true,
            });
            spells.Add(new SpellRequirement
            {
                Name = "Talisman of Impulse",
                ManaValue = 2,
                Pips = Pip(),
                IsManaSource = true,
            });
        }

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.5,
            Sources = sources,
            Spells = spells,
            IsSingleton = true,
        };
    }

    private static int Percent(int numerator, int denominator)
        => (int)Math.Round(100.0 * numerator / denominator);

    private static IReadOnlyDictionary<ManaColor, int> Pip(params (ManaColor Color, int Count)[] pips)
        => new Dictionary<ManaColor, int>(pips.ToDictionary(p => p.Color, p => p.Count));
}
