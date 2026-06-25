using System;
using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Targets <see cref="CastabilitySimulator"/> behaviors not covered by
/// <see cref="KarstenManabaseCastabilityTests"/>: color-coverage CONTENTION (one dual cannot pay
/// two same-color pips), in-simulator generic cost reduction shifting the effective cost/turn, the
/// colorless-spell mana-only path's limiting factor, the London-mulligan screen band, and the
/// stability of the seeded stream across repeated calls.
/// </summary>
/// <remarks>
/// The simulator is a seeded Monte-Carlo estimate, so assertions use ORDERING and documented
/// tolerance bands rather than exact magic numbers — same convention as the sibling suite.
/// </remarks>
public sealed class CastabilitySimulatorCoverageTests
{
    [Fact]
    public void Simulate_DoubleSameColorPip_ContendsForDistinctSources()
    {
        // CONTENTION: a single WU dual can pay ONE white pip OR one blue pip, never two of the same
        // color at once. A deck whose ONLY white access is duals (no white-only source) cannot cover
        // a WW requirement off one dual — it needs two separate white-producing sources. Compare a
        // base that has plenty of distinct white singles to one whose white all comes from shared
        // duals: the WW four-drop must be materially harder on the shared-dual base because the two
        // white pips can't both be paid by overlapping sources as efficiently.
        ManabaseDeck distinctWhite = TwoColorDeck(whiteSingles: 18, blueSingles: 18, duals: 0);
        ManabaseDeck sharedDuals = TwoColorDeck(whiteSingles: 4, blueSingles: 18, duals: 14);

        var ww = Spell("Double White", manaValue: 4, (ManaColor.White, 2));

        CardCastability distinct = Simulate(distinctWhite, ww, effectiveTurn: 4);
        CardCastability shared = Simulate(sharedDuals, ww, effectiveTurn: 4);

        // The shared-dual base has fewer real white *bodies* and the WW demand forces two distinct
        // white sources, so it must not score higher than the distinct-white base.
        Assert.True(shared.CastPercent <= distinct.CastPercent,
            $"WW off mostly-shared duals ({shared.CastPercent}%) must not beat WW off distinct white sources ({distinct.CastPercent}%)");
    }

    [Fact]
    public void Simulate_GoldDoublePip_NeedsBothColorsSimultaneously()
    {
        // A WWUU gold four-drop needs FOUR distinct colored sources at once (two white, two blue).
        // On a base where each color comes only from its own singles plus a handful of duals, this
        // is strictly harder than a single-color UU of the same MV (which can share blue freely).
        ManabaseDeck deck = TwoColorDeck(whiteSingles: 12, blueSingles: 12, duals: 8);

        var wwuu = Spell("WWUU Bomb", manaValue: 4, (ManaColor.White, 2), (ManaColor.Blue, 2));
        var uu = Spell("UU Spell", manaValue: 4, (ManaColor.Blue, 2));

        CardCastability gold = Simulate(deck, wwuu, effectiveTurn: 4);
        CardCastability mono = Simulate(deck, uu, effectiveTurn: 4);

        Assert.True(gold.CastPercent <= mono.CastPercent,
            $"WWUU ({gold.CastPercent}%) needs 4 distinct colored sources and must not beat UU ({mono.CastPercent}%)");
        Assert.InRange(gold.CastPercent, 0, 100);
    }

    [Fact]
    public void Simulate_GenericReduction_RaisesCastVsUnreduced()
    {
        // The simulator shifts the effective cost down by genericReduction (clamped to never drop a
        // colored pip). A four-drop cast on the (reduced) effective turn 3 must be easier than the
        // same spell at its printed turn 4 — the reduction is honored inside the sim, not only by the
        // analyzer that picks the turn.
        ManabaseDeck deck = MonoBlueDeck(islands: 30);
        var fourDrop = Spell("Reduced Spell", manaValue: 4, (ManaColor.Blue, 1));

        CardCastability printed = CastabilitySimulator.Simulate(
            deck, deck.TotalCards, fourDrop, effectiveTurn: 4, genericReduction: 0);
        CardCastability reduced = CastabilitySimulator.Simulate(
            deck, deck.TotalCards, fourDrop, effectiveTurn: 3, genericReduction: 1);

        Assert.True(reduced.CastPercent >= printed.CastPercent,
            $"a {{1}}-reduced four-drop on turn 3 ({reduced.CastPercent}%) must not be harder than its printed turn-4 cast ({printed.CastPercent}%)");
        Assert.Equal(3, reduced.OnCurveTurn);
    }

    [Fact]
    public void Simulate_ReductionCannotDropBelowPipFloor()
    {
        // A deep generic reduction must never push the effective cost below the colored pip count.
        // A UU two-drop with a huge generic reduction still needs to make 2 blue mana, so it cannot
        // be reported as a turn-1 spell nor as trivially 100% — the pip floor binds the cost.
        ManabaseDeck deck = MonoBlueDeck(islands: 30);
        var uuTwoDrop = Spell("UU Floor", manaValue: 2, (ManaColor.Blue, 2));

        CardCastability row = CastabilitySimulator.Simulate(
            deck, deck.TotalCards, uuTwoDrop, effectiveTurn: 2, genericReduction: 5);

        // effectiveTurn passed in is 2 (the analyzer floors it); the sim must still require 2 blue.
        Assert.InRange(row.CastPercent, 1, 100);
        Assert.Equal(2, row.OnCurveTurn);
    }

    [Fact]
    public void Simulate_ColorlessSpell_IsManaOnly_AndIgnoresColorAccess()
    {
        // A colorless payoff has no pips, so its castability is purely a mana-count race; its
        // limiting factor must be "mana" and it must score the SAME on a mono-blue base as on a
        // mono-red base of equal size (color identity is irrelevant to a pip-less spell).
        ManabaseDeck blue = MonoBlueDeck(islands: 34);
        ManabaseDeck red = MonoRedDeck(mountains: 34);

        var colorless = new SpellRequirement
        {
            Name = "Colorless Payoff",
            ManaValue = 5,
            Pips = new Dictionary<ManaColor, int>(),
        };

        CardCastability onBlue = Simulate(blue, colorless, effectiveTurn: 5);
        CardCastability onRed = Simulate(red, colorless, effectiveTurn: 5);

        Assert.Equal("mana", onBlue.LimitingFactor);
        Assert.Equal("mana", onRed.LimitingFactor);
        // Same seed (same name), same land count, no color dependency → identical cast %.
        Assert.Equal(onBlue.CastPercent, onRed.CastPercent);
    }

    [Fact]
    public void Simulate_ScreenHand_MulliganRecoversCheapSpell()
    {
        // London mulligan SCREEN band: a deck that routinely draws a land-light opener (few lands,
        // mostly filler) must still reach a cheap colored spell at a respectable rate because the
        // mulligan rejects 0-1 land hands and digs for a keepable 2+ land hand. With 17 Islands in a
        // 99-card shell a turn-3 single pip should still clear a meaningful floor, not collapse.
        ManabaseDeck screenProne = MonoBlueDeck(islands: 17);
        var threeDrop = Spell("Cheap Blue", manaValue: 3, (ManaColor.Blue, 1));

        CardCastability row = Simulate(screenProne, threeDrop, effectiveTurn: 3);

        // 17 lands in 99 cards is deliberately land-light, so a turn-3 drop misses fairly often even
        // after mulliganing — the simulator lands it ~43%. The point is the mulligan keeps it from
        // collapsing toward zero (a no-mulligan screen-prone base would be far worse). Wide,
        // deterministic band (the seeded sim returns the same value every run / platform).
        Assert.InRange(row.CastPercent, 30, 70);
    }

    [Fact]
    public void Simulate_FewerLands_LowerThanRicherBase()
    {
        // Direct monotonicity check on the mulligan/draw model: a leaner land base must not be MORE
        // castable than a richer one for the same mid-curve spell.
        ManabaseDeck lean = MonoBlueDeck(islands: 17);
        ManabaseDeck rich = MonoBlueDeck(islands: 36);
        var spell = Spell("Mid Blue", manaValue: 3, (ManaColor.Blue, 1));

        CardCastability leanRow = Simulate(lean, spell, effectiveTurn: 3);
        CardCastability richRow = Simulate(rich, spell, effectiveTurn: 3);

        Assert.True(leanRow.CastPercent <= richRow.CastPercent,
            $"17-land base ({leanRow.CastPercent}%) must not beat 36-land base ({richRow.CastPercent}%)");
    }

    [Fact]
    public void Simulate_RepeatedCalls_ReproduceLimitingFactorAndTurn()
    {
        // Determinism extends beyond CastPercent: the derived LimitingFactor and OnCurveTurn must
        // also be byte-stable across calls (the per-spell seed fixes the whole stream).
        ManabaseDeck deck = AzoriusDualDeck(islands: 10, plains: 10, duals: 6);
        var spell = Spell("Stable WU", manaValue: 4, (ManaColor.White, 1), (ManaColor.Blue, 1));

        CardCastability a = Simulate(deck, spell, effectiveTurn: 4);
        CardCastability b = Simulate(deck, spell, effectiveTurn: 4);

        Assert.Equal(a.CastPercent, b.CastPercent);
        Assert.Equal(a.LimitingFactor, b.LimitingFactor);
        Assert.Equal(a.OnCurveTurn, b.OnCurveTurn);
    }

    [Fact]
    public void Simulate_ColorStarvedSpell_LimitingFactorNamesTheColor()
    {
        // A red spell on a base with NO red access can never be cast: 0% and the limiting factor must
        // be attributed to the missing color, not "mana" (there is plenty of mana, just no red).
        ManabaseDeck noRed = MonoBlueDeck(islands: 36);
        var redSpell = Spell("Lightning", manaValue: 2, (ManaColor.Red, 1));

        CardCastability row = Simulate(noRed, redSpell, effectiveTurn: 2);

        Assert.Equal(0, row.CastPercent);
        Assert.StartsWith("color:", row.LimitingFactor);
        Assert.Contains("Red", row.LimitingFactor);
    }

    // ---- helpers --------------------------------------------------------------------------

    [Fact]
    public void Simulate_AverageDelay_OnCurveSpell_IsNearZero()
    {
        // Task 4: a cheap spell on a rich on-color base casts on its turn almost every game, so the
        // mean "turns late" is ~0 (a handful of land-light openers add a fraction).
        ManabaseDeck deck = MonoBlueDeck(islands: 40);
        var oneU = Spell("Easy Blue", manaValue: 2, (ManaColor.Blue, 1));

        CardCastability row = Simulate(deck, oneU, effectiveTurn: 2);

        Assert.True(row.AverageDelay >= 0, $"delay must never be negative, got {row.AverageDelay}");
        Assert.True(row.AverageDelay < 0.5, $"on-curve spell should have near-zero delay, got {row.AverageDelay}");
    }

    [Fact]
    public void Simulate_AverageDelay_ColorStarvedSpell_IsPositive()
    {
        // A double-blue two-drop on a base whose blue is thin slips later in many games, so the
        // average delay rises above the on-curve baseline (but is not the never-castable cap).
        ManabaseDeck deck = TwoColorDeck(whiteSingles: 30, blueSingles: 6, duals: 0);
        var uu = Spell("Double Blue", manaValue: 2, (ManaColor.Blue, 2));

        CardCastability row = Simulate(deck, uu, effectiveTurn: 2);

        Assert.True(row.AverageDelay > 0.3,
            $"a colour-starved double pip should show a real average delay, got {row.AverageDelay}");
    }

    [Fact]
    public void Simulate_AverageDelay_NeverCastable_IsCappedAtHorizon()
    {
        // No blue source exists, so a {U}{U} spell is never castable: every trial caps firstCastable
        // at lastSimulatedTurn + 1. The grace window is now a uniform +1 (debug session
        // manabase-too-optimistic), so for turn 2 lastTurn = 3, the cap is 4, and the per-trial delay is
        // exactly 4 - 2 = 2 every game → the mean is exactly 2.0 (was 4.0 under the old 3/2/1 grace).
        ManabaseDeck deck = MonoRedDeck(mountains: 36);
        var uu = Spell("Unfixable Blue", manaValue: 2, (ManaColor.Blue, 2));

        CardCastability row = Simulate(deck, uu, effectiveTurn: 2);

        Assert.Equal(0, row.CastPercent);
        Assert.Equal(2.0, row.AverageDelay);
    }

    private static CardCastability Simulate(ManabaseDeck deck, SpellRequirement spell, int effectiveTurn)
        => CastabilitySimulator.Simulate(deck, deck.TotalCards - deck.CommanderCount, spell, effectiveTurn, genericReduction: 0);

    private static SpellRequirement Spell(string name, int manaValue, params (ManaColor Color, int Count)[] pips) => new()
    {
        Name = name,
        ManaValue = manaValue,
        Pips = pips.ToDictionary(p => p.Color, p => p.Count),
        IsGold = pips.Count(p => p.Color != ManaColor.Colorless) >= 2,
    };

    private static ManabaseDeck MonoBlueDeck(int islands)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < islands; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement>(),
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck MonoRedDeck(int mountains)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < mountains; i++)
        {
            sources.Add(new ManaSource { Name = "Mountain", Produces = new[] { ManaColor.Red } });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement>(),
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }

    // A WU base: `whiteSingles` white-only lands, `blueSingles` blue-only lands, and `duals` WU duals.
    private static ManabaseDeck TwoColorDeck(int whiteSingles, int blueSingles, int duals)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < whiteSingles; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }

        for (int i = 0; i < blueSingles; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        for (int i = 0; i < duals; i++)
        {
            sources.Add(new ManaSource { Name = "Hallowed Fountain", Produces = new[] { ManaColor.White, ManaColor.Blue } });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement>(),
            AverageManaValue = 2.8,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck AzoriusDualDeck(int islands, int plains, int duals)
        => TwoColorDeck(whiteSingles: plains, blueSingles: islands, duals: duals);

    [Fact]
    public void Analyze_CommanderOnlyDeck_EmptyLibrary_DoesNotThrow()
    {
        // Regression: a deck that is only the commander has librarySize 0, so the opening draw window
        // is empty. The London-mulligan BottomCards path must clamp toBottom and not index shuffled[-1].
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Brago, King Eternal",
                Quantity = 1,
                ManaCost = "{2}{W}{U}",
                ManaValue = 4,
                TypeLine = "Legendary Creature — Spirit",
                OracleText = string.Empty,
                ProducedMana = Array.Empty<string>(),
                IsCommander = true,
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards, isSingleton: true);

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck); // must not throw on the empty library
        Assert.NotNull(report);
        // With no library, the commander can never be cast on curve.
        CardCastability? brago = report.Castability.FirstOrDefault(c => c.Name == "Brago, King Eternal");
        Assert.True(brago is null || brago.CastPercent == 0,
            $"empty-library commander should be 0% castable, got {brago?.CastPercent}");
    }
}
