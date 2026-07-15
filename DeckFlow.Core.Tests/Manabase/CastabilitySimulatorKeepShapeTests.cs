using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CastabilitySimulatorKeepShapeTests
{
    [Fact]
    public void ShapeA_CreditsInHandAcceleration_PayoffByTurn3()
    {
        ManabasePlanPresence result = Run(ExplosivePayoffDeck(includeRamp: true), keepShapes: true);

        Assert.Equal(100, result.PlanKeepablePercent);
        Assert.Equal("high", result.PlanKeepableBand);
        Assert.Equal(100, result.ShapeExplosivePercent);
        Assert.Equal(0, result.ShapeEnginePercent);
        Assert.Equal(0, result.ShapeBridgePercent);
    }

    [Fact]
    public void ShapeA_SlowPayoffTurn5_DoesNotCount()
    {
        ManabasePlanPresence result = Run(SlowPayoffDeck(), keepShapes: true);

        Assert.Equal(0, result.PlanKeepablePercent);
        Assert.Equal("low", result.PlanKeepableBand);
        Assert.Equal(0, result.ShapeExplosivePercent);
    }

    [Fact]
    public void ShapeB_EngineByTurn2_Counts()
    {
        ManabasePlanPresence result = Run(EngineDeck(manaValue: 2, landCount: 2), keepShapes: true);

        Assert.Equal(100, result.PlanKeepablePercent);
        Assert.Equal(100, result.ShapeEnginePercent);
        Assert.Equal(0, result.ShapeExplosivePercent);
    }

    [Fact]
    public void ShapeB_EngineTurn3_DoesNotCount()
    {
        ManabasePlanPresence result = Run(EngineDeck(manaValue: 3, landCount: 3), keepShapes: true);

        Assert.Equal(0, result.PlanKeepablePercent);
        Assert.Equal(0, result.ShapeEnginePercent);
    }

    [Fact]
    public void ShapeC_TwoInteractionPlusDevelopment_Counts()
    {
        ManabasePlanPresence result = Run(BridgeDeck(interactionCount: 2), keepShapes: true);

        Assert.Equal(100, result.PlanKeepablePercent);
        Assert.Equal(100, result.ShapeBridgePercent);
        Assert.Equal(0, result.ShapeExplosivePercent);
        Assert.Equal(0, result.ShapeEnginePercent);
    }

    [Fact]
    public void ShapeC_OneInteraction_DoesNotCount()
    {
        ManabasePlanPresence result = Run(BridgeDeck(interactionCount: 1), keepShapes: true);

        Assert.Equal(0, result.PlanKeepablePercent);
        Assert.Equal(0, result.ShapeBridgePercent);
    }

    [Fact]
    public void ShapeC_NonPermanentCounterspells_Count()
    {
        ManabaseDeck deck = BridgeDeck(interactionCount: 2);
        ManabasePlanPresence result = Run(deck, keepShapes: true);

        Assert.Equal(100, result.ShapeBridgePercent);
        Assert.DoesNotContain(deck.Spells, spell => spell.PlanRoles.HasFlag(PlanRole.Interaction));
    }

    [Fact]
    public void Commander_NotDrawnAsLibraryFiller()
    {
        ManabaseDeck deck = CommanderDeck();
        IReadOnlyList<object> library = BuildLibraryForTest(deck);

        Assert.DoesNotContain(library, card => string.Equals(
            (string?)card.GetType().GetProperty("PlanName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(card),
            "Winota, Joiner of Forces",
            StringComparison.Ordinal));
    }

    [Fact]
    public void PlanKeepable_NeverExceedsManaKeepable()
    {
        const int trials = 1;
        ManabasePlanPresence result = Run(ExplosivePayoffDeck(includeRamp: true), keepShapes: true, trials: trials);
        int manaKeepablePercent = (int)Math.Round(100.0 * result.KeepableTrials / trials);

        Assert.True(result.PlanKeepablePercent <= manaKeepablePercent);
    }

    [Fact]
    public void KeepShapesOff_LeavesPlanFieldsAtDefault()
    {
        ManabasePlanPresence result = Run(ExplosivePayoffDeck(includeRamp: true), keepShapes: false);

        Assert.Equal(0, result.PlanKeepablePercent);
        Assert.Equal(string.Empty, result.PlanKeepableBand);
        Assert.Equal(0, result.ShapeExplosivePercent);
        Assert.Equal(0, result.ShapeEnginePercent);
        Assert.Equal(0, result.ShapeBridgePercent);
    }

    private static ManabasePlanPresence Run(ManabaseDeck deck, bool keepShapes, int trials = 1)
        => CastabilitySimulator.SimulatePlanPresence(
            deck,
            deck.TotalCards - deck.CommanderCount,
            trials,
            mode: ManabaseMode.Cedh,
            keepShapes: keepShapes);

    private static IReadOnlyList<object> BuildLibraryForTest(ManabaseDeck deck)
    {
        MethodInfo method = typeof(CastabilitySimulator).GetMethod(
            "BuildLibrary",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        object library = method.Invoke(null, new object?[]
        {
            deck,
            deck.TotalCards - deck.CommanderCount,
            false,
            false,
            false,
            false,
            null,
            false,
            false,
        })!;
        return ((IEnumerable)library).Cast<object>().ToList();
    }

    private static ManabaseDeck ExplosivePayoffDeck(bool includeRamp)
    {
        var sources = new List<ManaSource>
        {
            Land("Forest A", ManaColor.Green),
            Land("Forest B", ManaColor.Green),
            Land("Forest C", ManaColor.Green),
        };

        var spells = new List<SpellRequirement>();
        if (includeRamp)
        {
            sources.Add(new ManaSource { Name = "Sol Ring", Produces = new[] { ManaColor.Green }, IsLand = false, Weight = 0.75 });
            spells.Add(new SpellRequirement { Name = "Sol Ring", ManaValue = 1, Pips = new Dictionary<ManaColor, int>(), IsManaSource = true });
        }

        spells.Add(new SpellRequirement
        {
            Name = "Payoff",
            ManaValue = 4,
            Pips = Pip(ManaColor.Green, 1),
            PlanRoles = PlanRole.Payoff,
        });

        spells.AddRange(FillerSpells(7 - sources.Count - spells.Count));
        return Deck(sources, spells);
    }

    private static ManabaseDeck SlowPayoffDeck()
        => ExplosivePayoffDeck(includeRamp: false);

    private static ManabaseDeck EngineDeck(int manaValue, int landCount)
    {
        var sources = Enumerable.Range(0, landCount)
            .Select(i => Land($"Island {i}", ManaColor.Blue))
            .ToList();
        var spells = new List<SpellRequirement>
        {
            new()
            {
                Name = "Engine",
                ManaValue = manaValue,
                Pips = Pip(ManaColor.Blue, 1),
                PlanRoles = PlanRole.Engine,
            },
        };
        spells.AddRange(FillerSpells(7 - sources.Count - spells.Count));
        return Deck(sources, spells);
    }

    private static ManabaseDeck BridgeDeck(int interactionCount)
    {
        var sources = new List<ManaSource>
        {
            Land("Island A", ManaColor.Blue),
            Land("Island B", ManaColor.Blue),
        };
        var spells = new List<SpellRequirement>();
        for (int i = 0; i < interactionCount; i++)
        {
            spells.Add(new SpellRequirement
            {
                Name = $"Counterspell {i}",
                ManaValue = 1,
                Pips = Pip(ManaColor.Blue, 1),
                IsInteractionSpell = true,
            });
        }

        spells.AddRange(FillerSpells(7 - sources.Count - spells.Count));
        return Deck(sources, spells);
    }

    private static ManabaseDeck CommanderDeck()
    {
        var sources = new List<ManaSource>
        {
            Land("Mountain A", ManaColor.Red),
            Land("Mountain B", ManaColor.Red),
        };
        var spells = new List<SpellRequirement>
        {
            new()
            {
                Name = "Winota, Joiner of Forces",
                ManaValue = 4,
                Pips = Pip(ManaColor.Red, 1),
                PlanRoles = PlanRole.Payoff,
                IsCommander = true,
            },
        };
        spells.AddRange(FillerSpells(7 - sources.Count - spells.Count));
        return new ManabaseDeck
        {
            TotalCards = 8,
            CommanderCount = 1,
            AverageManaValue = 2.0,
            Sources = sources,
            Spells = spells,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck Deck(IReadOnlyList<ManaSource> sources, IReadOnlyList<SpellRequirement> spells)
        => new()
        {
            TotalCards = 7,
            CommanderCount = 0,
            AverageManaValue = 2.0,
            Sources = sources,
            Spells = spells,
            IsSingleton = true,
        };

    private static ManaSource Land(string name, ManaColor color)
        => new() { Name = name, Produces = new[] { color }, EntersUntapped = true };

    private static IReadOnlyDictionary<ManaColor, int> Pip(ManaColor color, int count)
        => new Dictionary<ManaColor, int> { [color] = count };

    private static IEnumerable<SpellRequirement> FillerSpells(int count)
        => Enumerable.Range(0, count).Select(i => new SpellRequirement
        {
            Name = $"Filler {i}",
            ManaValue = 1,
            Pips = Pip(ManaColor.Green, 1),
        });
}
