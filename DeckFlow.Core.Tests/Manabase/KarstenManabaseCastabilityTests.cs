using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates the seeded Monte-Carlo <see cref="CastabilitySimulator"/> (FINDING-3: it replaced the
/// pessimistic <c>P_mana × P_color</c> independence product) and
/// <see cref="KarstenManabase.CedhLandTarget(int, int, double, double, double)"/>.
/// </summary>
/// <remarks>
/// The simulator is seeded (per-spell stable hash), so its output is deterministic across runs — but
/// it is a Monte-Carlo estimate, so assertions use a documented ±4-point tolerance and lean on
/// ORDERING rather than exact equality. Cast %s are calibrated against the Salubrious Snail
/// calculator on the real Brago deck (mean |Δ| ≈ 3 pts; see 64-VALIDATION.md).
/// </remarks>
public sealed class KarstenManabaseCastabilityTests
{
    [Fact]
    public void Simulate_SinglePipOneDrop_IsHighlyCastable()
    {
        // A blue one-drop on a mono-blue base (36 Islands) should be cast very reliably.
        ManabaseDeck deck = MonoBlueDeck(islands: 36);
        var spell = Spell("Brainstorm", manaValue: 1, (ManaColor.Blue, 1));

        CardCastability row = Simulate(deck, spell, effectiveTurn: 1);

        // With a London mulligan and a short grace window a single-pip one-drop is ~95%+.
        Assert.InRange(row.CastPercent, 88, 100);
        Assert.Equal(1, row.OnCurveTurn);
    }

    [Fact]
    public void Simulate_DoublePipFiveDrop_LowerThanSinglePipOneDrop()
    {
        ManabaseDeck deck = MonoBlueDeck(islands: 36);
        var oneDrop = Spell("Brainstorm", 1, (ManaColor.Blue, 1));
        var fiveDrop = Spell("Consecrated Sphinx", 5, (ManaColor.Blue, 2));

        CardCastability one = Simulate(deck, oneDrop, 1);
        CardCastability five = Simulate(deck, fiveDrop, 5);

        Assert.True(five.CastPercent < one.CastPercent,
            $"5-drop UU ({five.CastPercent}%) should be harder than 1-drop U ({one.CastPercent}%)");
    }

    [Fact]
    public void Simulate_ColorlessSpell_LimitingFactorMana()
    {
        // Ugin-like 6-MV colorless payoff: no pips → only the mana count gates it.
        ManabaseDeck deck = MonoBlueDeck(islands: 36);
        var ugin = new SpellRequirement
        {
            Name = "Ugin, the Spirit Dragon",
            ManaValue = 6,
            Pips = new Dictionary<ManaColor, int>(),
        };

        CardCastability row = Simulate(deck, ugin, effectiveTurn: 6);

        Assert.Equal("mana", row.LimitingFactor);
        // A 6-drop on a 36-land, no-ramp mono base, on the play with grace (Snail ~50% WITH ramp;
        // lower here because this fixture has none).
        Assert.InRange(row.CastPercent, 25, 65);
    }

    [Fact]
    public void Simulate_RampPool_RaisesHighDropCastChance()
    {
        var fiveDrop = Spell("Consecrated Sphinx", 5, (ManaColor.Blue, 2));
        ManabaseDeck noRamp = MonoBlueDeck(islands: 36);
        ManabaseDeck withRamp = MonoBlueDeck(islands: 36, rocks: 6);

        CardCastability slow = Simulate(noRamp, fiveDrop, 5);
        CardCastability fast = Simulate(withRamp, fiveDrop, 5);

        Assert.True(fast.CastPercent >= slow.CastPercent,
            $"ramp should not lower a 5-drop's castability ({slow.CastPercent}% -> {fast.CastPercent}%)");
    }

    [Fact]
    public void Simulate_SharedSource_UUvsWU_OrderingHolds()
    {
        // Dual-heavy two-color base. A double-pip single color (UU) is the tighter colored
        // requirement than a balanced WU split at the same mana value.
        ManabaseDeck deck = AzoriusDualDeck(islands: 14, plains: 14, duals: 8);
        var uu = Spell("Cryptic Command", 4, (ManaColor.Blue, 2));
        var wu = Spell("Teferi", 4, (ManaColor.White, 1), (ManaColor.Blue, 1));

        CardCastability uuRow = Simulate(deck, uu, 4);
        CardCastability wuRow = Simulate(deck, wu, 4);

        Assert.True(uuRow.CastPercent <= wuRow.CastPercent + 4,
            $"UU ({uuRow.CastPercent}%) should not score materially higher than WU ({wuRow.CastPercent}%)");
        Assert.InRange(uuRow.CastPercent, 1, 100);
        Assert.InRange(wuRow.CastPercent, 1, 100);
    }

    [Fact]
    public void Simulate_IsDeterministic_AcrossRuns()
    {
        ManabaseDeck deck = MonoBlueDeck(islands: 36);
        var spell = Spell("Consecrated Sphinx", 5, (ManaColor.Blue, 2));

        CardCastability a = Simulate(deck, spell, 5);
        CardCastability b = Simulate(deck, spell, 5);

        // Seeded by the spell name: identical inputs must reproduce exactly.
        Assert.Equal(a.CastPercent, b.CastPercent);
    }

    [Fact]
    public void Simulate_TappedLand_LowersCastVsUntapped()
    {
        // FINDING-1 (HIGH): an ETB-tapped land must NOT produce mana the turn it is played; it comes
        // online next turn (modeled like ramp's OnlineTurn). An all-tapped base must therefore be
        // materially LESS castable than an equivalent all-untapped base, because every land is a turn
        // late. The effect is invisible on cheap single-pip spells (the wide grace window absorbs a
        // one-turn slip), so we probe a multi-pip mid-curve spell (UU on turn 4) where the compounding
        // tapped delay outruns the grace window — a 2-source colored requirement that the tapped base
        // reaches a full turn later.
        ManabaseDeck untapped = MonoBlueDeck(islands: 36);
        ManabaseDeck tapped = MonoBlueTappedDeck(tapLands: 36);

        var uuFourDrop = Spell("Cryptic-ish", manaValue: 4, (ManaColor.Blue, 2));

        CardCastability fast = Simulate(untapped, uuFourDrop, effectiveTurn: 4);
        CardCastability slow = Simulate(tapped, uuFourDrop, effectiveTurn: 4);

        // Tolerance: the measured gap is ~8 pts (see 64 probe); require at least 4 so a noise-sized
        // wobble can't pass it spuriously, while leaving headroom under the true delta.
        Assert.True(slow.CastPercent <= fast.CastPercent - 4,
            $"all-tapped base ({slow.CastPercent}%) should be >=4 pts below all-untapped ({fast.CastPercent}%) for UU on turn 4");
    }

    [Fact]
    public void Simulate_DeployableDork_EntersAtFullValueVsNone()
    {
        // Phase-64 ramp-value fix: a DEPLOYABLE dork/rock is a card you draw and play. It must enter the
        // sim at FULL value (one card, always live) — its analytic weight (0.5/0.25) is a PROXY for the
        // deploy-cost + summoning-sickness friction the sim ALREADY models, so it must NOT be re-applied
        // as a per-trial Bernoulli activation (that double-discounts). With no dork the black pip is
        // uncastable (no black source); WITH the dork it becomes reliably castable. Because deployable
        // dorks are full-value, the 0.25 and 0.5 weights now produce the SAME (full) contribution — the
        // weight no longer gates activation, so the two must be within Monte-Carlo noise of each other.
        ManabaseDeck noBlack = MonoBlueDeck(islands: 36);
        ManabaseDeck withQuarterDork = MonoBlueWithBlackDork(islands: 36, dorkWeight: 0.25);
        ManabaseDeck withHalfDork = MonoBlueWithBlackDork(islands: 36, dorkWeight: 0.5);

        var blackSpell = Spell("Black Payoff", manaValue: 3, (ManaColor.Black, 1));

        CardCastability without = Simulate(noBlack, blackSpell, effectiveTurn: 3);
        CardCastability quarter = Simulate(withQuarterDork, blackSpell, effectiveTurn: 3);
        CardCastability half = Simulate(withHalfDork, blackSpell, effectiveTurn: 3);

        // No black source → 0%. A deployable dork (full value) lifts the pip well above zero.
        Assert.Equal(0, without.CastPercent);
        Assert.True(quarter.CastPercent > 0,
            $"a deployable black dork must lift black castability above 0, was {quarter.CastPercent}%");

        // Full-value: the dork's analytic weight no longer gates its sim activation, so 0.25 vs 0.5
        // deployable dorks contribute identically (within Monte-Carlo noise).
        Assert.True(Math.Abs(half.CastPercent - quarter.CastPercent) <= 4,
            $"deployable dork is full-value regardless of analytic weight; 0.25 ({quarter.CastPercent}%) and "
            + $"0.5 ({half.CastPercent}%) should match within noise");
    }

    [Fact]
    public void Simulate_ConditionalGrantedSource_ContributesLessThanFullRock()
    {
        // Phase-64: ENABLER-CONDITIONAL granted sources (the 0.25 any-color sources from Cryptolith Rite
        // / Relic of Legends) stay speculative — they KEEP a per-trial Bernoulli activation at their 0.25
        // weight (the granter must be on board AND the creature survive). So the ONLY black access being a
        // single 0.25 *conditional* source must contribute STRICTLY LESS than the same black access being
        // a full deployable rock. This is the guard the fix must preserve: conditional 0.25 < full source.
        ManabaseDeck conditional = MonoBlueWithBlackSource(islands: 36, weight: 0.25, isConditional: true);
        ManabaseDeck fullRock = MonoBlueWithBlackSource(islands: 36, weight: 1.0, isConditional: false);

        var blackSpell = Spell("Black Payoff", manaValue: 3, (ManaColor.Black, 1));

        CardCastability cond = Simulate(conditional, blackSpell, effectiveTurn: 3);
        CardCastability full = Simulate(fullRock, blackSpell, effectiveTurn: 3);

        // The 0.25 conditional source fires in only ~25% of the games it is drawn, so it must trail a
        // full source materially. Require a clear gap so noise can't flip it.
        Assert.True(cond.CastPercent > 0,
            $"a 0.25 conditional black source must still contribute something, was {cond.CastPercent}%");
        Assert.True(cond.CastPercent <= full.CastPercent - 5,
            $"a 0.25 conditional source ({cond.CastPercent}%) must contribute clearly less than a full rock ({full.CastPercent}%)");
    }

    [Fact]
    public void Simulate_MulliganKeepsPlayableHand_FloodedBaseStillCasts()
    {
        // Mulligan keep/bottom: a deck that is mostly lands (would routinely draw an unkeepable
        // all-land or near-all-land 7) should still cast a cheap colored spell reliably, because the
        // London mulligan rejects out-of-band hands and bottoms toward a playable keep. A 21-Island /
        // 78-filler shell (heavy land density) on a turn-2 single pip should still land high.
        ManabaseDeck deck = MonoBlueDeck(islands: 30);
        var twoDrop = Spell("Remand-ish", manaValue: 2, (ManaColor.Blue, 1));

        CardCastability row = Simulate(deck, twoDrop, effectiveTurn: 2);

        // The mulligan keeps a 2..hiCap-land hand; a turn-2 single pip off 30 Islands clears ~85%+.
        // Tolerance band documented: floor 80 leaves Monte-Carlo + mulligan-band headroom.
        Assert.InRange(row.CastPercent, 80, 100);
    }

    [Fact]
    public void CedhLandTarget_IsLowerThanCasual_AndAboveTheFloor()
    {
        // Turbo (low curve, heavy ramp) sits near the 28 floor; cEDH always < casual.
        double turboCasual = KarstenManabase.SingletonLandTarget(100, 1, 1.5, 14);
        double turboCedh = KarstenManabase.CedhLandTarget(100, 1, 1.5, 14);
        Assert.True(turboCedh < turboCasual);
        Assert.InRange(turboCedh, 28.0, 30.0);

        // Midrange cEDH lands in the ~29–31 band.
        double midCedh = KarstenManabase.CedhLandTarget(100, 1, 2.2, 12, fastMana: 2);
        Assert.InRange(midCedh, 28.0, 31.5);
        Assert.True(midCedh < KarstenManabase.SingletonLandTarget(100, 1, 2.2, 12, fastMana: 2));

        // A normal casual shell, run as cEDH, is still lower than casual but never below 28.
        double normalCasual = KarstenManabase.SingletonLandTarget(100, 1, 3.2, 6);
        double normalCedh = KarstenManabase.CedhLandTarget(100, 1, 3.2, 6);
        Assert.True(normalCedh < normalCasual);
        Assert.True(normalCedh >= 28.0);
    }

    [Fact]
    public void SixtyCardLandTarget_UsesKarstensPublished60CardRegression()
    {
        // Efficacy R2 finding H5: the function shipped the 100-card-scaled constants
        // (32.65 + 3.16·MV = the 60-card interior pre-multiplied by 5/3), recommending
        // ~38 lands for a normal 60-card midrange deck. Karsten's published 60-card fit
        // is 19.59 + 1.90·MV — avg MV 2.5 with no credits is 24.34, in the ~22-26 band
        // his tables give for real 60-card decks.
        double target = KarstenManabase.SixtyCardLandTarget(averageManaValue: 2.5, rampAndDrawUnderThree: 0);
        Assert.Equal(24.34, target, precision: 2);

        // 8 cheap ramp/draw pieces credit -0.28 each, exactly as in the singleton form.
        double withRamp = KarstenManabase.SixtyCardLandTarget(averageManaValue: 2.5, rampAndDrawUnderThree: 8);
        Assert.Equal(24.34 - 2.24, withRamp, precision: 2);

        // Sanity: a 60-card deck always needs fewer lands than the same curve at 99 cards.
        double singleton = KarstenManabase.SingletonLandTarget(100, 1, 2.5, 0);
        Assert.True(target < singleton);
    }

    // ---- helpers --------------------------------------------------------------------------

    private static CardCastability Simulate(ManabaseDeck deck, SpellRequirement spell, int effectiveTurn)
        => CastabilitySimulator.Simulate(deck, deck.TotalCards - deck.CommanderCount, spell, effectiveTurn, genericReduction: 0);

    private static SpellRequirement Spell(string name, int manaValue, params (ManaColor Color, int Count)[] pips) => new()
    {
        Name = name,
        ManaValue = manaValue,
        Pips = pips.ToDictionary(p => p.Color, p => p.Count),
        IsGold = pips.Count(p => p.Color != ManaColor.Colorless) >= 2,
    };

    private static ManabaseDeck MonoBlueDeck(int islands, int rocks = 0)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < islands; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        var spells = new List<SpellRequirement>();
        for (int i = 0; i < rocks; i++)
        {
            // 2-mana blue rock (e.g. a Signet); both a source AND a flagged mana-source spell so the
            // simulator can find its deploy cost.
            sources.Add(new ManaSource { Name = $"Signet{i}", Produces = new[] { ManaColor.Blue }, Weight = 0.75, IsLand = false });
            spells.Add(new SpellRequirement
            {
                Name = $"Signet{i}",
                ManaValue = 2,
                Pips = new Dictionary<ManaColor, int>(),
                IsManaSource = true,
            });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = spells,
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck MonoBlueTappedDeck(int tapLands)
    {
        // All blue sources, but every one enters tapped (EntersUntapped = false) — so each is a turn
        // late versus a basic Island. Proves the FINDING-1 tapped-land fix.
        var sources = new List<ManaSource>();
        for (int i = 0; i < tapLands; i++)
        {
            sources.Add(new ManaSource
            {
                Name = "Tapped Blue",
                Produces = new[] { ManaColor.Blue },
                EntersUntapped = false,
            });
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

    private static ManabaseDeck MonoBlueWithBlackDork(int islands, double dorkWeight)
    {
        // A mono-blue land base plus ONE partial black source (a fragile mana dork at < 1 weight). The
        // only access to black is this sub-1 source; it must survive the integer-copy modeling and
        // contribute on ~dorkWeight of games. Proves the FINDING-2 partial-source fix.
        var sources = new List<ManaSource>();
        for (int i = 0; i < islands; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        // 1-cost black dork at the given fractional weight; both a source AND a flagged mana-source
        // spell so the simulator can find its (turn-1) deploy cost.
        sources.Add(new ManaSource
        {
            Name = "Black Dork",
            Produces = new[] { ManaColor.Black },
            Weight = dorkWeight,
            IsLand = false,
        });

        var spells = new List<SpellRequirement>
        {
            new()
            {
                Name = "Black Dork",
                ManaValue = 1,
                Pips = new Dictionary<ManaColor, int>(),
                IsManaSource = true,
            },
        };

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = spells,
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck MonoBlueWithBlackSource(int islands, double weight, bool isConditional)
    {
        // A mono-blue land base plus ONE black non-land source at the given weight/conditionality. Both
        // carry a flagged 1-cost mana-source spell row so the simulator finds the same (turn-1) deploy
        // cost — isolating the ONLY difference to the IsConditional Bernoulli gate.
        var sources = new List<ManaSource>();
        for (int i = 0; i < islands; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        sources.Add(new ManaSource
        {
            Name = "Black Source",
            Produces = new[] { ManaColor.Black },
            Weight = weight,
            IsLand = false,
            IsConditional = isConditional,
        });

        var spells = new List<SpellRequirement>
        {
            new()
            {
                Name = "Black Source",
                ManaValue = 1,
                Pips = new Dictionary<ManaColor, int>(),
                IsManaSource = true,
            },
        };

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = spells,
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck AzoriusDualDeck(int islands, int plains, int duals)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < islands; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        for (int i = 0; i < plains; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
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
}
