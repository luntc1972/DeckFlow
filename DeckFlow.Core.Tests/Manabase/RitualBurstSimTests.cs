using System.Collections.Generic;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class RitualBurstSimTests
{
    [Fact]
    public void DarkRitual_LiftsTripleBlackSpell()
    {
        SpellRequirement spell = TripleBlackSpell();
        ManabaseDeck deck = BuildDeck(
            blackLands: 10,
            redLands: 23,
            spell,
            new OneShotMana
            {
                Name = "Dark Ritual",
                ProducedColors = new[] { ManaColor.Black },
                ProducedAmount = 3,
                OwnPips = Pip((ManaColor.Black, 1)),
                OwnManaValue = 1,
            });

        CardCastability off = CastabilitySimulator.Simulate(deck, 99, spell, effectiveTurn: 3, genericReduction: 0, ritualBurst: false);
        CardCastability on = CastabilitySimulator.Simulate(deck, 99, spell, effectiveTurn: 3, genericReduction: 0, ritualBurst: true);

        Assert.True(on.CastPercent > off.CastPercent, $"ritual burst should raise castability (off={off.CastPercent}, on={on.CastPercent})");
        Assert.True(on.CastPercent >= off.CastPercent + 5, $"ritual lift should be material (off={off.CastPercent}, on={on.CastPercent})");
    }

    [Fact]
    public void DarkRitual_WithoutBlackSource_DoesNotLift()
    {
        SpellRequirement spell = TripleBlackSpell();
        ManabaseDeck deck = BuildDeck(
            blackLands: 0,
            redLands: 33,
            spell,
            new OneShotMana
            {
                Name = "Dark Ritual",
                ProducedColors = new[] { ManaColor.Black },
                ProducedAmount = 3,
                OwnPips = Pip((ManaColor.Black, 1)),
                OwnManaValue = 1,
            });

        CardCastability off = CastabilitySimulator.Simulate(deck, 99, spell, effectiveTurn: 3, genericReduction: 0, ritualBurst: false);
        CardCastability on = CastabilitySimulator.Simulate(deck, 99, spell, effectiveTurn: 3, genericReduction: 0, ritualBurst: true);

        Assert.Equal(0, off.CastPercent);
        Assert.Equal(off.CastPercent, on.CastPercent);
    }

    [Fact]
    public void RedRitual_DoesNotHelpDoubleBlueSpell()
    {
        SpellRequirement spell = new()
        {
            Name = "Counter Tide",
            ManaValue = 2,
            Pips = Pip((ManaColor.Blue, 2)),
        };
        ManabaseDeck deck = BuildDeck(
            blackLands: 0,
            redLands: 14,
            spell,
            new OneShotMana
            {
                Name = "Rite of Flame",
                ProducedColors = new[] { ManaColor.Red },
                ProducedAmount = 2,
                OwnPips = Pip((ManaColor.Red, 1)),
                OwnManaValue = 1,
            },
            blueLands: 8,
            fillerCount: 76);

        CardCastability off = CastabilitySimulator.Simulate(deck, 99, spell, effectiveTurn: 2, genericReduction: 0, ritualBurst: false);
        CardCastability on = CastabilitySimulator.Simulate(deck, 99, spell, effectiveTurn: 2, genericReduction: 0, ritualBurst: true);

        Assert.Equal(off.CastPercent, on.CastPercent);
    }

    [Fact]
    public void RitualBurstOff_IgnoresDeckOneShotsData()
    {
        SpellRequirement spell = TripleBlackSpell();
        OneShotMana ritual = new()
        {
            Name = "Dark Ritual",
            ProducedColors = new[] { ManaColor.Black },
            ProducedAmount = 3,
            OwnPips = Pip((ManaColor.Black, 1)),
            OwnManaValue = 1,
        };
        ManabaseDeck baseline = BuildDeck(blackLands: 10, redLands: 23, spell, oneShot: null);
        ManabaseDeck withOneShots = BuildDeck(blackLands: 10, redLands: 23, spell, ritual);

        CardCastability clean = CastabilitySimulator.Simulate(baseline, 99, spell, effectiveTurn: 3, genericReduction: 0, ritualBurst: false);
        CardCastability carryingData = CastabilitySimulator.Simulate(withOneShots, 99, spell, effectiveTurn: 3, genericReduction: 0, ritualBurst: false);

        Assert.Equal(clean.RepresentativeOpeners, carryingData.RepresentativeOpeners);
        Assert.Equal(
            clean with { RepresentativeOpeners = System.Array.Empty<OpeningHandSample>() },
            carryingData with { RepresentativeOpeners = System.Array.Empty<OpeningHandSample>() });
    }

    [Fact]
    public void ReserveGenericForRamp_RitualBridgeDelayIsObservable()
    {
        SpellRequirement spell = new()
        {
            Name = "BBBB? No, Just Four",
            ManaValue = 4,
            Pips = Pip((ManaColor.Black, 1)),
        };

        var sources = new List<ManaSource>();
        for (int i = 0; i < 24; i++)
        {
            sources.Add(new ManaSource
            {
                Name = $"Swamp {i}",
                Produces = new[] { ManaColor.Black },
                IsLand = true,
            });
        }

        for (int i = 0; i < 4; i++)
        {
            sources.Add(new ManaSource
            {
                Name = "Charcoal Diamond",
                Produces = new[] { ManaColor.Black },
                IsLand = false,
                Weight = 0.75,
                DeployCost = 2,
            });
        }

        ManabaseDeck deck = new()
        {
            TotalCards = 60,
            CommanderCount = 0,
            AverageManaValue = 2.0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                spell,
                new() { Name = "Charcoal Diamond", ManaValue = 2, Pips = Pip(), IsManaSource = true },
            },
            OneShots = Enumerable.Range(0, 4)
                .Select(_ => new OneShotMana
                {
                    Name = "Dark Ritual",
                    ProducedColors = new[] { ManaColor.Black },
                    ProducedAmount = 3,
                    OwnPips = Pip((ManaColor.Black, 1)),
                    OwnManaValue = 1,
                })
                .ToList(),
            IsSingleton = false,
        };

        CardCastability withoutReserve = CastabilitySimulator.Simulate(
            deck, 60, spell, effectiveTurn: 2, genericReduction: 0, gateRampOnCastable: false, ritualBurst: true);
        CardCastability withReserve = CastabilitySimulator.Simulate(
            deck, 60, spell, effectiveTurn: 2, genericReduction: 0, gateRampOnCastable: true, ritualBurst: true);

        Assert.True(withReserve.CastPercent < withoutReserve.CastPercent,
            $"reserve should lower cast% when same-turn ramp plus ritual would otherwise double-spend mana (off={withoutReserve.CastPercent}, on={withReserve.CastPercent})");
        Assert.True(withReserve.AverageDelay > withoutReserve.AverageDelay,
            $"reserve should delay the payoff when the ritual only bridges the gap after same-turn ramp (off={withoutReserve.AverageDelay}, on={withReserve.AverageDelay})");
    }

    [Fact]
    public void CedhMode_CreditsRitualBurst()
    {
        SpellRequirement spell = TripleBlackSpell();
        ManabaseDeck deck = BuildDeck(
            blackLands: 10,
            redLands: 23,
            spell,
            new OneShotMana
            {
                Name = "Dark Ritual",
                ProducedColors = new[] { ManaColor.Black },
                ProducedAmount = 3,
                OwnPips = Pip((ManaColor.Black, 1)),
                OwnManaValue = 1,
            });

        ManabaseReport off = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Cedh, CommanderImportance.Standard, ritualBurst: false);
        ManabaseReport on = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Cedh, CommanderImportance.Standard, ritualBurst: true);

        int offCast = off.Castability.Single(c => c.Name == spell.Name).CastPercent;
        int onCast = on.Castability.Single(c => c.Name == spell.Name).CastPercent;

        Assert.True(onCast > offCast, $"cEDH ritual burst should raise castability (off={offCast}, on={onCast})");
    }

    [Fact]
    public void CasualMode_SuppressesRitualBurst()
    {
        SpellRequirement spell = TripleBlackSpell();
        ManabaseDeck deck = BuildDeck(
            blackLands: 10,
            redLands: 23,
            spell,
            new OneShotMana
            {
                Name = "Dark Ritual",
                ProducedColors = new[] { ManaColor.Black },
                ProducedAmount = 3,
                OwnPips = Pip((ManaColor.Black, 1)),
                OwnManaValue = 1,
            });

        ManabaseReport off = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard, ritualBurst: false);
        ManabaseReport on = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard, ritualBurst: true);

        int offCast = off.Castability.Single(c => c.Name == spell.Name).CastPercent;
        int onCast = on.Castability.Single(c => c.Name == spell.Name).CastPercent;

        Assert.Equal(offCast, onCast);
    }

    private static SpellRequirement TripleBlackSpell() => new()
    {
        Name = "Necro Burst",
        ManaValue = 3,
        Pips = Pip((ManaColor.Black, 3)),
    };

    private static ManabaseDeck BuildDeck(
        int blackLands,
        int redLands,
        SpellRequirement spell,
        OneShotMana? oneShot,
        int blueLands = 0,
        int fillerCount = 65)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < blackLands; i++)
        {
            sources.Add(new ManaSource { Name = $"Swamp {i}", Produces = new[] { ManaColor.Black }, IsLand = true });
        }

        for (int i = 0; i < redLands; i++)
        {
            sources.Add(new ManaSource { Name = $"Mountain {i}", Produces = new[] { ManaColor.Red }, IsLand = true });
        }

        for (int i = 0; i < blueLands; i++)
        {
            sources.Add(new ManaSource { Name = $"Island {i}", Produces = new[] { ManaColor.Blue }, IsLand = true });
        }

        var spells = new List<SpellRequirement> { spell };
        for (int i = 0; i < fillerCount; i++)
        {
            spells.Add(new SpellRequirement
            {
                Name = $"Filler {i}",
                ManaValue = 3,
                Pips = Pip(),
            });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            AverageManaValue = 3.0,
            Sources = sources,
            Spells = spells,
            OneShots = oneShot is null ? System.Array.Empty<OneShotMana>() : new[] { oneShot },
        };
    }

    private static IReadOnlyDictionary<ManaColor, int> Pip(params (ManaColor Color, int Count)[] pips)
    {
        var result = new Dictionary<ManaColor, int>();
        foreach ((ManaColor color, int count) in pips)
        {
            result[color] = count;
        }

        return result;
    }
}
