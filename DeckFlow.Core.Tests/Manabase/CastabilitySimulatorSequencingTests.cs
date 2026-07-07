using System.Collections.Generic;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Land-sequencing realism (efficacy R2 finding M2): on a slack turn (before the spell's cast turn)
/// the simulator should develop a tapped fixer that adds a still-missing color rather than a
/// color-useless untapped land. The ETB-tapped land comes online next turn — still on or before the
/// cast turn — so the tapped delay is free on a slack turn, whereas saving the fixer for the cast
/// turn would enter tapped and miss the color.
/// </summary>
public sealed class CastabilitySimulatorSequencingTests
{
    // The tracked spell needs one W and one U on turn 3; W comes from untapped Plains, U comes ONLY
    // from the WU duals. When the duals are tapped, the only way to have U online by turn 3 is to lay
    // a dual on a slack turn (1 or 2). With the M2 fix the sim does exactly that, so the tapped-dual
    // deck casts nearly as often as the untapped-dual control; without it, the duals get deferred to
    // the cast turn, enter tapped, and U is never online in time — a large castability gap.
    [Fact]
    public void Simulate_TappedFixerDevelopedOnSlackTurns_MatchesUntappedControl()
    {
        CardCastability tapped = Simulate(TwoColorDeck(dualsEnterTapped: true));
        CardCastability untapped = Simulate(TwoColorDeck(dualsEnterTapped: false));

        // The tapped-dual deck must stay within a few points of the untapped-dual control: the only
        // difference is ETB timing, which M2 neutralizes on the slack turns. A regression of the slack
        // sequencing would drop `tapped` far below `untapped` (the duals would arrive tapped on the
        // cast turn and miss U).
        Assert.True(
            tapped.CastPercent >= untapped.CastPercent - 5,
            $"tapped-dual castability {tapped.CastPercent}% should track the untapped control {untapped.CastPercent}% "
            + "(within 5 pts) once slack-turn tapped-fixer development (M2) is active");

        // And it must be genuinely high, not just 'close because both are bad'.
        Assert.True(tapped.CastPercent >= 80, $"expected a healthy cast rate, got {tapped.CastPercent}%");
    }

    private static CardCastability Simulate(ManabaseDeck deck)
        => CastabilitySimulator.Simulate(deck, deck.TotalCards - deck.CommanderCount, deck.Spells[0], effectiveTurn: 3, genericReduction: 0);

    private static ManabaseDeck TwoColorDeck(bool dualsEnterTapped)
    {
        var sources = new List<ManaSource>();

        // 18 untapped Plains: white only.
        for (int i = 0; i < 18; i++)
        {
            sources.Add(new ManaSource { Name = $"Plains {i}", Produces = new[] { ManaColor.White } });
        }

        // 18 WU duals — the ONLY blue sources. Tapped or untapped per the scenario.
        for (int i = 0; i < 18; i++)
        {
            sources.Add(new ManaSource
            {
                Name = $"Azorius Dual {i}",
                Produces = new[] { ManaColor.White, ManaColor.Blue },
                EntersUntapped = !dualsEnterTapped,
            });
        }

        var spell = new SpellRequirement
        {
            Name = "Azorius Payoff",
            ManaValue = 3,
            Pips = new Dictionary<ManaColor, int> { { ManaColor.White, 1 }, { ManaColor.Blue, 1 } },
        };

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement> { spell },
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }
}
