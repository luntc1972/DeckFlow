using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Behavioural regression for the <c>manabase.p1-grace-strict</c> flag (threaded through
/// <see cref="ManabaseAnalyzer.Analyze(ManabaseDeck, ManabaseMode, CommanderImportance, IReadOnlyList{CostReducer}?, bool, bool, bool, bool, bool, bool)"/>
/// as <c>strictP1Grace</c>). Strict-P1 removes the uniform +1 grace for turn-1 spells only:
/// a one-drop must be castable exactly on turn 1, while turns 2+ keep the +1 tolerance.
/// So strict can only LOWER a color-screwable one-drop's cast %, never raise it, and must leave
/// every 2+ drop byte-identical. Mono / always-on-color one-drops are unaffected.
/// The simulator is seeded (stable per-spell RNG), so these comparisons are deterministic.
/// </summary>
public sealed class P1GraceStrictTests
{
    [Fact]
    public void StrictP1_LowersOffColorOneDrop_OnColorSkewedDeck()
    {
        ManabaseDeck deck = SkewedWhiteBlueDeck();

        int off = CastOf(deck, strict: false, "Blue 1-drop");
        int on = CastOf(deck, strict: true, "Blue 1-drop");

        // Only ~5 blue sources: many hands first make blue on turn 2, which the +1 grace credited and
        // strict-P1 no longer does → strictly lower.
        Assert.True(on < off, $"strict-P1 should lower the color-screwable one-drop (off={off}, on={on})");
    }

    [Fact]
    public void StrictP1_LowersOneDrop_OnBalancedTwoColorDeck()
    {
        ManabaseDeck deck = BalancedWhiteBlueDeck();

        int off = CastOf(deck, strict: false, "Blue 1-drop");
        int on = CastOf(deck, strict: true, "Blue 1-drop");

        Assert.True(on < off, $"strict-P1 should lower the one-drop even on a balanced deck (off={off}, on={on})");
    }

    [Fact]
    public void StrictP1_LeavesThreeDropUnchanged()
    {
        // Turn-3 spell: GraceWindow is untouched for turn > 1, so strict-P1 is byte-identical.
        foreach (ManabaseDeck deck in new[] { SkewedWhiteBlueDeck(), BalancedWhiteBlueDeck(), MonoBlueDeck() })
        {
            int off = CastOf(deck, strict: false, "Blue 3-drop");
            int on = CastOf(deck, strict: true, "Blue 3-drop");
            Assert.Equal(off, on);
        }
    }

    [Fact]
    public void StrictP1_LeavesAlwaysOnColorOneDropUnchanged()
    {
        // Mono-blue: every turn-1 land makes blue, so the one-drop is always castable on turn 1 and
        // there is no turn-2 credit to remove → strict-P1 has no effect.
        ManabaseDeck deck = MonoBlueDeck();

        int off = CastOf(deck, strict: false, "Blue 1-drop");
        int on = CastOf(deck, strict: true, "Blue 1-drop");

        Assert.Equal(off, on);
    }

    [Fact]
    public void StrictP1_NeverRaisesCastPercent()
    {
        // Invariant across every spell of every fixture: removing grace can only lower or hold.
        foreach (ManabaseDeck deck in new[] { SkewedWhiteBlueDeck(), BalancedWhiteBlueDeck(), MonoBlueDeck() })
        {
            foreach (string name in deck.Spells.Select(s => s.Name))
            {
                int off = CastOf(deck, strict: false, name);
                int on = CastOf(deck, strict: true, name);
                Assert.True(on <= off, $"strict-P1 must never raise cast% ({name}: off={off}, on={on})");
            }
        }
    }

    [Fact]
    public void GraceWindowForTest_StrictZeroesTurnOneOnly()
    {
        // The pure window: strict gives 0 to turn <= 1 and keeps +1 for turns 2+; off is uniform +1.
        Assert.Equal(1, CastabilitySimulator.GraceWindowForTest(1, strictP1Grace: false));
        Assert.Equal(0, CastabilitySimulator.GraceWindowForTest(1, strictP1Grace: true));
        Assert.Equal(0, CastabilitySimulator.GraceWindowForTest(0, strictP1Grace: true));
        Assert.Equal(1, CastabilitySimulator.GraceWindowForTest(2, strictP1Grace: true));
        Assert.Equal(1, CastabilitySimulator.GraceWindowForTest(6, strictP1Grace: true));
    }

    private static int CastOf(ManabaseDeck deck, bool strict, string spellName)
        => ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, strictP1Grace: strict)
            .Castability.First(c => c.Name == spellName).CastPercent;

    private static SpellRequirement Spell(string name, int mv, params (ManaColor Color, int Count)[] pips) => new()
    {
        Name = name,
        ManaValue = mv,
        Pips = pips.ToDictionary(p => p.Color, p => p.Count),
    };

    private static ManabaseDeck Deck(IEnumerable<ManaSource> sources, params SpellRequirement[] spells) => new()
    {
        TotalCards = 99,
        CommanderCount = 0,
        Sources = sources.ToList(),
        Spells = spells.ToList(),
        AverageManaValue = 2.5,
        IsSingleton = true,
    };

    private static IEnumerable<ManaSource> Lands(string name, ManaColor color, int n)
        => Enumerable.Range(0, n).Select(_ => new ManaSource { Name = name, Produces = new[] { color } });

    private static ManabaseDeck SkewedWhiteBlueDeck() => Deck(
        Lands("Plains", ManaColor.White, 30).Concat(Lands("Island", ManaColor.Blue, 5)),
        Spell("Blue 1-drop", 1, (ManaColor.Blue, 1)),
        Spell("Blue 3-drop", 3, (ManaColor.Blue, 1)));

    private static ManabaseDeck BalancedWhiteBlueDeck() => Deck(
        Lands("Plains", ManaColor.White, 18).Concat(Lands("Island", ManaColor.Blue, 18)),
        Spell("Blue 1-drop", 1, (ManaColor.Blue, 1)),
        Spell("Blue 3-drop", 3, (ManaColor.Blue, 1)));

    private static ManabaseDeck MonoBlueDeck() => Deck(
        Lands("Island", ManaColor.Blue, 36),
        Spell("Blue 1-drop", 1, (ManaColor.Blue, 1)),
        Spell("Blue 3-drop", 3, (ManaColor.Blue, 1)));
}
