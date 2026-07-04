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

    private static IReadOnlyDictionary<ManaColor, int> Pip(params (ManaColor Color, int Count)[] pips)
        => new Dictionary<ManaColor, int>(pips.ToDictionary(p => p.Color, p => p.Count));
}
