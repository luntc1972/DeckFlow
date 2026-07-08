using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Phase 3 (plan-presence): <see cref="CastabilitySimulator.SimulatePlanPresence"/> — the dedicated
/// deck-level pass that measures the share of keepable openers holding a win-directed card castable on
/// curve. A smaller trial count keeps the tests fast; the assertions are coarse (bands / high-vs-low),
/// not exact percentages.
/// </summary>
public sealed class CastabilitySimulatorPlanPresenceTests
{
    private const int Trials = 3000;

    private static IReadOnlyDictionary<ManaColor, int> Pip(ManaColor color, int count)
        => new Dictionary<ManaColor, int> { [color] = count };

    private static ManabaseDeck Deck(int lands, IEnumerable<SpellRequirement> spells)
    {
        var spellList = spells.ToList();
        var sources = new List<ManaSource>();
        for (int i = 0; i < lands; i++)
        {
            sources.Add(new ManaSource { Name = $"Forest {i}", Produces = new[] { ManaColor.Green }, EntersUntapped = true });
        }

        return new ManabaseDeck
        {
            TotalCards = lands + spellList.Count,
            CommanderCount = 0,
            AverageManaValue = 2.0,
            Sources = sources,
            Spells = spellList,
            IsSingleton = true,
        };
    }

    private static SpellRequirement Payoff(int index, int mv, ManaColor color)
        => new()
        {
            Name = $"Payoff {index}",
            ManaValue = mv,
            Pips = Pip(color, 1),
            PlanRoles = PlanRole.Payoff,
        };

    private static SpellRequirement Filler(int index)
        => new() { Name = $"Filler {index}", ManaValue = 2, Pips = Pip(ManaColor.Green, 1) };

    private static ManabasePlanPresence Run(ManabaseDeck deck)
        => CastabilitySimulator.SimulatePlanPresence(deck, deck.TotalCards, Trials);

    [Fact]
    public void ManyCheapCastablePayoffs_ReadHigh()
    {
        // Green deck, 25 one-mana green payoffs among ~99 cards: most keepable openers hold one that is
        // castable on turn 1.
        var spells = Enumerable.Range(0, 25).Select(i => Payoff(i, mv: 1, ManaColor.Green))
            .Concat(Enumerable.Range(0, 36).Select(Filler));
        ManabasePlanPresence result = Run(Deck(lands: 38, spells));

        Assert.Equal("high", result.Band);
        Assert.True(result.PlanPresencePercent >= 65, $"expected high, got {result.PlanPresencePercent}%");
        Assert.True(result.RolePercents[PlanRole.Payoff] > 0);
        Assert.Equal(0, result.RolePercents[PlanRole.Interaction]);

        // Payoff coverage is the headline read: 25 castable payoffs read high.
        Assert.Equal(result.RolePercents[PlanRole.Payoff], result.PayoffPercent);
        Assert.True(result.PayoffPercent >= 20, $"expected payoff high, got {result.PayoffPercent}%");
        Assert.Equal("high", result.PayoffBand);
    }

    [Fact]
    public void NoPlanTaggedSpells_ReadsZero()
    {
        var spells = Enumerable.Range(0, 61).Select(Filler); // ramp/filler only — no PlanRoles
        ManabasePlanPresence result = Run(Deck(lands: 38, spells));

        Assert.Equal(0, result.PlanPresencePercent);
        Assert.Equal("low", result.Band);
        Assert.Equal(0, result.PayoffPercent);
        Assert.Equal("low", result.PayoffBand);
        Assert.Equal(0, result.KeepableTrials); // short-circuits before simulating when nothing is tagged
    }

    [Fact]
    public void PayoffsPresentButUncastableColor_DoNotCount()
    {
        // 25 BLUE one-mana payoffs in an all-GREEN deck: they land in openers but can never be cast,
        // so plan-presence must stay near zero (the on-curve castability gate is load-bearing).
        var spells = Enumerable.Range(0, 25).Select(i => Payoff(i, mv: 1, ManaColor.Blue))
            .Concat(Enumerable.Range(0, 36).Select(Filler));
        ManabasePlanPresence result = Run(Deck(lands: 38, spells));

        Assert.True(result.PlanPresencePercent <= 5, $"uncastable payoffs should not count, got {result.PlanPresencePercent}%");
    }

    [Fact]
    public void UnreachableManaValue_DoesNotCount()
    {
        // A single 12-mana payoff is essentially never castable on curve in a 99-card deck.
        var spells = new[] { Payoff(0, mv: 12, ManaColor.Green) }
            .Concat(Enumerable.Range(0, 60).Select(Filler));
        ManabasePlanPresence result = Run(Deck(lands: 38, spells));

        Assert.True(result.PlanPresencePercent <= 5, $"unreachable payoff should not count, got {result.PlanPresencePercent}%");
    }
}
