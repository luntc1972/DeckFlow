using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// MULLIGAN-01..05: the two-stage pure-observation opening-hand instrumentation added to
/// <see cref="CastabilitySimulator.Simulate"/> — keep-size counters bucketed by the RETURNED keep
/// value (not the mulligan-depth index) plus up to 3 representative openers attributed to the row's
/// tracked spell. Every assertion here proves the instrumentation is PURE OBSERVATION: no new rng
/// draw, no second <c>Simulate</c>, and cast % stays byte-identical to the pre-change behavior.
/// </summary>
public sealed class CastabilitySimulatorMulliganTests
{
    [Fact]
    public void Simulate_CastPercentUnchanged_PinnedFixture()
    {
        // Pinned value: a fixed seed (spell name "Blue Spell") over this exact fixture. This rose
        // from 89% to 93% when the sim started drawing on turn 1 (Commander is multiplayer, so the
        // starting player draws their first turn — the extra card lifts the cast rate), then to 94%
        // with the M1 mulligan-bottoming fix: a mull to 6 used to park the bottomed card at the exact
        // slot the turn-1 draw reads, so it deterministically redrew the dead filler it just bottomed;
        // bottoming to the physical library tail lets those hands draw a genuine random card (~36%
        // an Island here) instead. The mulligan INSTRUMENTATION still touches neither `successes` nor
        // the rng stream — this delta is the bottoming fix alone. Rose to 95% when the opening keep band
        // tightened (sweet spot 3 lands; mulligan 4-5 land floods instead of keeping them), so fewer
        // flooded keeps dilute the cast rate.
        CardCastability row = Simulate(MonoBlueDeck(), BlueSpell());

        Assert.Equal(95, row.CastPercent);
    }

    [Fact]
    public void Simulate_KeepSizeCountersSumToTrialsAndAgreeWithKeepable()
    {
        CardCastability row = Simulate(MonoBlueDeck(), BlueSpell());

        Assert.Equal(CastabilitySimulator.DefaultTrials, row.Kept7Trials + row.MulliganTo6Trials + row.MulliganTo5Trials);
        Assert.Equal(row.KeepableTrials, row.Kept7Trials + row.MulliganTo6Trials);
        Assert.InRange(row.KeepableTrials, 0, CastabilitySimulator.DefaultTrials);
    }

    [Fact]
    public void Simulate_RepresentativeOpenersCompositionSumsToKeptSize()
    {
        CardCastability row = Simulate(MonoBlueDeck(), BlueSpell());

        Assert.NotEmpty(row.RepresentativeOpeners);
        foreach (OpeningHandSample sample in row.RepresentativeOpeners)
        {
            Assert.Equal(sample.KeptCards, sample.Lands + sample.RampPieces + sample.OtherCards);
            Assert.InRange(sample.Colors, 0, 5);
            Assert.Contains(sample.KeptCards, new[] { 7, 6, 5 });
        }
    }

    [Fact]
    public void Simulate_IsDeterministic_AcrossTwoCalls()
    {
        ManabaseDeck deck = MonoBlueDeck();
        SpellRequirement spell = BlueSpell();

        CardCastability first = Simulate(deck, spell);
        CardCastability second = Simulate(deck, spell);

        Assert.Equal(first.CastPercent, second.CastPercent);
        Assert.Equal(first.KeepableTrials, second.KeepableTrials);
        Assert.Equal(first.Kept7Trials, second.Kept7Trials);
        Assert.Equal(first.MulliganTo6Trials, second.MulliganTo6Trials);
        Assert.Equal(first.MulliganTo5Trials, second.MulliganTo5Trials);
        Assert.Equal(first.RepresentativeOpeners, second.RepresentativeOpeners);
    }

    [Fact]
    public void Simulate_SingletonFreeMulligan_CreditsExtraKept7NotMulliganTo6()
    {
        // Both decks share the SAME spell name ("Commander") so the seeded rng stream is identical
        // between the two runs — the only difference is IsSingleton (which grants a FREE fresh-7
        // mulligan at depth 1 before the schedule starts bottoming toward 6). If keep-size buckets
        // were assigned by mulligan-DEPTH INDEX instead of the RETURNED keep value, the singleton's
        // depth-1 free-mulligan-kept-7 hands would land in MulliganTo6Trials instead — so a materially
        // higher Kept7Trials for the singleton over the non-singleton (same land composition, same
        // seed) proves the bucketing reads the returned keep value, not the depth index.
        CardCastability singleton = Simulate(SparseBlueDeck(isSingleton: true), CommanderSpell(isCommander: true));
        CardCastability nonSingleton = Simulate(SparseBlueDeck(isSingleton: false), CommanderSpell(isCommander: false));

        Assert.True(
            singleton.Kept7Trials > nonSingleton.Kept7Trials + 1000,
            $"singleton free-mulligan should credit materially more Kept7Trials (singleton={singleton.Kept7Trials}, nonSingleton={nonSingleton.Kept7Trials})");
    }

    [Fact]
    public void Simulate_RepresentativeOpeners_AttributedToTrackedSpell()
    {
        SpellRequirement spell = BlueSpell();
        CardCastability row = Simulate(MonoBlueDeck(), spell, effectiveTurn: 3);

        Assert.NotEmpty(row.RepresentativeOpeners);
        foreach (OpeningHandSample sample in row.RepresentativeOpeners)
        {
            Assert.Equal(spell.Name, sample.TrackedSpellName);
            Assert.Equal(3, sample.TrackedOnCurveTurn);
        }
    }

    [Fact]
    public void Simulate_UncastableColorHand_NeverReportsHasPlan()
    {
        // The deck runs only Red lands; the tracked spell needs a Blue pip the manabase can never
        // supply. Every kept hand has lands + inert filler only (no line to the tracked play), so
        // OnCurveCastable — and therefore HasPlan ("workable line") — must be false for every sample,
        // never merely "a non-land card is in hand."
        SpellRequirement spell = new()
        {
            Name = "Blue Bomb",
            ManaValue = 3,
            Pips = new Dictionary<ManaColor, int> { { ManaColor.Blue, 1 } },
        };
        var sources = new List<ManaSource>();
        for (int i = 0; i < 40; i++)
        {
            sources.Add(new ManaSource { Name = $"Mountain {i}", Produces = new[] { ManaColor.Red } });
        }

        var deck = new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement> { spell },
            AverageManaValue = 2.5,
            IsSingleton = true,
        };

        CardCastability row = CastabilitySimulator.Simulate(deck, deck.TotalCards, spell, effectiveTurn: 3, genericReduction: 0);

        Assert.Equal(0, row.CastPercent);
        Assert.NotEmpty(row.RepresentativeOpeners);
        Assert.All(row.RepresentativeOpeners, sample =>
        {
            Assert.False(sample.OnCurveCastable);
            Assert.False(sample.HasPlan);
        });
    }

    [Fact]
    public void Simulate_Keep7Openers_RespectTightenedLandBand()
    {
        // Opening keep band: keep 3 lands (2 only with >=1 ramp), mulligan 4-5 land floods on a normal
        // curve (MonoBlueDeck avg MV 2.5). A kept 7 therefore never shows 4+ lands, and — since this deck
        // carries no ramp — a 2-land 7 is mulliganed, so every kept 7 holds exactly 3 lands.
        CardCastability row = Simulate(MonoBlueDeck(), BlueSpell());

        var keep7 = row.RepresentativeOpeners.Where(o => o.KeptCards == 7).ToList();
        Assert.NotEmpty(keep7);
        Assert.All(keep7, o =>
        {
            Assert.InRange(o.Lands, 2, 3);
            if (o.Lands == 2)
            {
                Assert.True(o.RampPieces >= 1, "a 2-land keep must hold a ramp piece");
            }
        });
    }

    [Fact]
    public void Simulate_HighCurveDeck_KeepsMoreLandHeavy7s_ThanNormalCurve()
    {
        // A high-curve deck (avg MV >= 3) genuinely wants more lands, so it keeps its wider band (up to
        // 5) where a normal-curve deck mulligans the same 4-5 land floods. On a land-heavy deck that
        // shows up as materially more kept-at-7 trials for the high-curve deck.
        CardCastability normal = Simulate(LandHeavyDeck(avgMv: 2.5), BlueSpell());
        CardCastability high = Simulate(LandHeavyDeck(avgMv: 3.5), BlueSpell());

        Assert.True(
            high.Kept7Trials > normal.Kept7Trials,
            $"high-curve should keep more land-heavy 7s (high {high.Kept7Trials} vs normal {normal.Kept7Trials})");
    }

    // ---- builders ---------------------------------------------------------------------------

    private static CardCastability Simulate(ManabaseDeck deck, SpellRequirement spell, int effectiveTurn = 3)
        => CastabilitySimulator.Simulate(deck, deck.TotalCards - deck.CommanderCount, spell, effectiveTurn, genericReduction: 0);

    private static SpellRequirement BlueSpell() => new()
    {
        Name = "Blue Spell",
        ManaValue = 3,
        Pips = new Dictionary<ManaColor, int> { { ManaColor.Blue, 1 } },
    };

    private static SpellRequirement CommanderSpell(bool isCommander) => new()
    {
        Name = "Commander",
        ManaValue = 3,
        Pips = new Dictionary<ManaColor, int> { { ManaColor.Blue, 1 } },
        IsCommander = isCommander,
    };

    private static ManabaseDeck MonoBlueDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 36; i++)
        {
            sources.Add(new ManaSource { Name = $"Island {i}", Produces = new[] { ManaColor.Blue } });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement> { BlueSpell() },
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }

    // Deliberately land-heavy (45 of 99) so a fresh 7 frequently holds 4-5 lands — the exact hands the
    // normal-curve keep band now mulligans but the high-curve band keeps. The avg MV knob flips the
    // upper keep bound (>= 3 keeps up to 5, else up to 3) without changing anything else.
    private static ManabaseDeck LandHeavyDeck(double avgMv)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 45; i++)
        {
            sources.Add(new ManaSource { Name = $"Island {i}", Produces = new[] { ManaColor.Blue } });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement> { BlueSpell() },
            AverageManaValue = avgMv,
            IsSingleton = true,
        };
    }

    // Deliberately land-sparse (20 of 99) so a fresh 7 fails the [2,4] keep band most of the time —
    // this forces both decks through mulligans, and lets the singleton's extra depth-1 free-7 shot
    // show up as a materially larger Kept7Trials than the non-singleton's immediate mull-to-6.
    private static ManabaseDeck SparseBlueDeck(bool isSingleton)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource { Name = $"Island {i}", Produces = new[] { ManaColor.Blue } });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = isSingleton ? 1 : 0,
            Sources = sources,
            Spells = new List<SpellRequirement> { CommanderSpell(isCommander: isSingleton) },
            AverageManaValue = 2.5,
            IsSingleton = isSingleton,
        };
    }
}
