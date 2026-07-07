using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// MULLIGAN-01..05: <c>ManabaseAnalyzer.ComputeMulliganEvaluation</c> (exposed via the
/// <see cref="ManabaseAnalyzer.ComputeMulliganEvaluationForTest"/> seam) — the aggregator that turns
/// the already-computed per-spell castability rows into a deck-level keepable-hand band, keep-size
/// distribution, and early-row representative openers. No Monte-Carlo is driven here: rows are
/// hand-constructed with known counters so the pure aggregation math is directly testable.
/// </summary>
public sealed class ManabaseMulliganEvaluationTests
{
    [Fact]
    public void KeepableBand_HighThreshold()
    {
        ManabaseDeck deck = Deck(2.5);
        var rows = new[] { Row("Spell", mv: 2, turn: 2, isCommander: false, keepable: 850, kept7: 600, m6: 250, m5: 150) };

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(deck, rows, defaultTrials: 1000);

        Assert.Equal(85, result.KeepableHandPercent);
        Assert.Equal("high", result.KeepableBand);
    }

    [Fact]
    public void KeepableBand_MediumThreshold()
    {
        ManabaseDeck deck = Deck(2.5);
        var rows = new[] { Row("Spell", mv: 2, turn: 2, isCommander: false, keepable: 750, kept7: 500, m6: 250, m5: 250) };

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(deck, rows, defaultTrials: 1000);

        Assert.Equal(75, result.KeepableHandPercent);
        Assert.Equal("medium", result.KeepableBand);
    }

    [Fact]
    public void KeepableBand_LowThreshold()
    {
        ManabaseDeck deck = Deck(2.5);
        var rows = new[] { Row("Spell", mv: 2, turn: 2, isCommander: false, keepable: 500, kept7: 300, m6: 200, m5: 500) };

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(deck, rows, defaultTrials: 1000);

        Assert.Equal(50, result.KeepableHandPercent);
        Assert.Equal("low", result.KeepableBand);
    }

    [Fact]
    public void KeepSizePercents_DerivedFromCounters()
    {
        ManabaseDeck deck = Deck(2.5);
        var rows = new[] { Row("Spell", mv: 2, turn: 2, isCommander: false, keepable: 850, kept7: 600, m6: 250, m5: 150) };

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(deck, rows, defaultTrials: 1000);

        Assert.Equal(60, result.Kept7Percent);
        Assert.Equal(25, result.MulliganTo6Percent);
        Assert.Equal(15, result.MulliganTo5Percent);
    }

    [Fact]
    public void KeepSizePercents_AverageAcrossNonCommanderRows_ExcludingCommander()
    {
        ManabaseDeck deck = Deck(2.5);

        // The commander row's extreme counters must NOT pull the average — only non-commander rows
        // feed the figure (mirrors ComputeTapAnalysis's D1/D3 non-commander averaging).
        var rows = new[]
        {
            Row("Commander", mv: 4, turn: 4, isCommander: true, keepable: 0, kept7: 0, m6: 0, m5: 1000),
            Row("Spell A", mv: 2, turn: 2, isCommander: false, keepable: 900, kept7: 700, m6: 200, m5: 100),
            Row("Spell B", mv: 3, turn: 3, isCommander: false, keepable: 900, kept7: 700, m6: 200, m5: 100),
        };

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(deck, rows, defaultTrials: 1000);

        Assert.Equal(90, result.KeepableHandPercent);
        Assert.Equal(70, result.Kept7Percent);
    }

    [Fact]
    public void KeepSizePercents_AlwaysReconcile_UnderRoundingDivergentDistribution()
    {
        // A distribution where independently rounding each raw counter would NOT reconcile:
        // kept7=604 -> 60, to6=253 -> 25 (sum 85) but keepable trials=857 -> round(85.7)=86, and
        // to5=143 -> 14 so the three shares would sum to 99. The aggregator instead DERIVES keepable =
        // kept7 + to6 and to5 = 100 - keepable, so the pasteable artifact's numbers always add up.
        ManabaseDeck deck = Deck(2.5);
        var rows = new[]
        {
            Row("Spell", mv: 2, turn: 2, isCommander: false, keepable: 857, kept7: 604, m6: 253, m5: 143),
        };

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(deck, rows, defaultTrials: 1000);

        Assert.Equal(60, result.Kept7Percent);
        Assert.Equal(25, result.MulliganTo6Percent);
        Assert.Equal(85, result.KeepableHandPercent);
        Assert.Equal(15, result.MulliganTo5Percent);
        // Headline reconciles with the keep-size breakdown, and the three shares partition 100%.
        Assert.Equal(result.Kept7Percent + result.MulliganTo6Percent, result.KeepableHandPercent);
        Assert.Equal(100, result.Kept7Percent + result.MulliganTo6Percent + result.MulliganTo5Percent);
    }

    [Fact]
    public void RepresentativeOpeners_SelectedFromEarliestRows_NeverALateBomb()
    {
        ManabaseDeck deck = Deck(2.5);

        var earlyA = Row("Early A", mv: 1, turn: 1, isCommander: false, keepable: 800, kept7: 600, m6: 150, m5: 250,
            openers: new[] { Sample("keep 7", "Early A", 1, hasPlan: true) });
        var earlyB = Row("Early B", mv: 2, turn: 2, isCommander: false, keepable: 800, kept7: 600, m6: 150, m5: 250,
            openers: new[]
            {
                Sample("mulligan to 6", "Early B", 2, hasPlan: false),
                Sample("mulligan to 5", "Early B", 2, hasPlan: false),
            });
        var lateBomb = Row("Late Bomb", mv: 9, turn: 9, isCommander: false, keepable: 800, kept7: 600, m6: 150, m5: 250,
            openers: new[]
            {
                Sample("keep 7", "Late Bomb", 9, hasPlan: true),
                Sample("mulligan to 6", "Late Bomb", 9, hasPlan: false),
                Sample("mulligan to 5", "Late Bomb", 9, hasPlan: false),
            });

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(
            deck, new[] { lateBomb, earlyA, earlyB }, defaultTrials: 1000);

        Assert.Equal(3, result.RepresentativeOpeners.Count);
        Assert.All(result.RepresentativeOpeners, o => Assert.NotEqual("Late Bomb", o.TrackedSpellName));
        Assert.All(result.RepresentativeOpeners, o => Assert.NotEmpty(o.TrackedSpellName));
        Assert.All(result.RepresentativeOpeners, o => Assert.True(o.TrackedOnCurveTurn > 0));
    }

    [Fact]
    public void RepresentativeOpeners_ExcludeFreeZeroCostSpells_NeverNamedAsEarlyPlay()
    {
        ManabaseDeck deck = Deck(2.5);

        // Deflecting Swat / Fierce Guardianship auto-reduce to effective 0 (DetectSelfCost), so their
        // castability row lands at ManaValue 0 — the lowest, which would otherwise pull it to the front
        // of the earliest-row ordering. A free spell is trivially castable turn 1 and is not a genuine
        // early play, so it must never be surfaced as the representative opener.
        var freeSpell = Row("Deflecting Swat", mv: 0, turn: 1, isCommander: false, keepable: 800, kept7: 600, m6: 150, m5: 250,
            openers: new[]
            {
                Sample("keep 7", "Deflecting Swat", 1, hasPlan: true),
                Sample("mulligan to 6", "Deflecting Swat", 1, hasPlan: true),
                Sample("mulligan to 5", "Deflecting Swat", 1, hasPlan: true),
            });
        var realEarly = Row("Ponder", mv: 1, turn: 1, isCommander: false, keepable: 800, kept7: 600, m6: 150, m5: 250,
            openers: new[]
            {
                Sample("keep 7", "Ponder", 1, hasPlan: true),
                Sample("mulligan to 6", "Ponder", 1, hasPlan: false),
                Sample("mulligan to 5", "Ponder", 1, hasPlan: false),
            });

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(
            deck, new[] { freeSpell, realEarly }, defaultTrials: 1000);

        Assert.Equal(3, result.RepresentativeOpeners.Count);
        Assert.All(result.RepresentativeOpeners, o => Assert.NotEqual("Deflecting Swat", o.TrackedSpellName));
        Assert.All(result.RepresentativeOpeners, o => Assert.Equal("Ponder", o.TrackedSpellName));
    }

    [Fact]
    public void RepresentativeOpeners_AllFreeSpells_FallsBackRatherThanEmpty()
    {
        ManabaseDeck deck = Deck(2.5);

        // Degenerate case: every non-commander tracked spell is free (ManaValue 0). Rather than silently
        // emptying the opener read, fall back to the full non-commander pool so the panel still renders.
        var free = Row("Force of Negation", mv: 0, turn: 1, isCommander: false, keepable: 800, kept7: 600, m6: 150, m5: 250,
            openers: new[] { Sample("keep 7", "Force of Negation", 1, hasPlan: true) });

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(
            deck, new[] { free }, defaultTrials: 1000);

        Assert.NotEmpty(result.RepresentativeOpeners);
        Assert.All(result.RepresentativeOpeners, o => Assert.Equal("Force of Negation", o.TrackedSpellName));
    }

    [Fact]
    public void RepresentativeOpeners_TruncatedToThree_EvenWithManyDuplicateDecisionRows()
    {
        ManabaseDeck deck = Deck(2.5);

        var rows = Enumerable.Range(0, 5).Select(i => Row($"Spell {i}", mv: i + 1, turn: i + 1, isCommander: false,
            keepable: 800, kept7: 600, m6: 150, m5: 250,
            openers: new[]
            {
                Sample("keep 7", $"Spell {i}", i + 1, hasPlan: true),
                Sample("mulligan to 6", $"Spell {i}", i + 1, hasPlan: false),
                Sample("mulligan to 5", $"Spell {i}", i + 1, hasPlan: false),
            })).ToArray();

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(deck, rows, defaultTrials: 1000);

        Assert.Equal(3, result.RepresentativeOpeners.Count);
        Assert.All(result.RepresentativeOpeners, o => Assert.Equal("Spell 0", o.TrackedSpellName));
    }

    [Fact]
    public void EmptyCastabilityRows_ReturnsSafeZero_NoThrow()
    {
        ManabaseDeck deck = Deck(2.5);

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(
            deck, System.Array.Empty<CardCastability>(), defaultTrials: CastabilitySimulator.DefaultTrials);

        Assert.Equal(0, result.KeepableHandPercent);
        Assert.Equal("low", result.KeepableBand);
        Assert.Equal(0, result.Kept7Percent);
        Assert.Equal(0, result.MulliganTo6Percent);
        Assert.Equal(0, result.MulliganTo5Percent);
        Assert.Empty(result.RepresentativeOpeners);
    }

    [Fact]
    public void AllCommanderRows_FallsBackToAllRows_NoThrow()
    {
        ManabaseDeck deck = Deck(2.5);
        var rows = new[] { Row("Commander", mv: 4, turn: 4, isCommander: true, keepable: 900, kept7: 700, m6: 200, m5: 100) };

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(deck, rows, defaultTrials: 1000);

        // Fallback to ALL rows (D1/D3 pattern) when no non-commander rows exist — no throw, no
        // divide-by-zero, the sole commander row's counters feed the figure.
        Assert.Equal(90, result.KeepableHandPercent);
    }

    [Fact]
    public void ColorCountAndAverageManaValue_ReusedFromDeck_NoNewSim()
    {
        ManabaseDeck deck = new()
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = new List<ManaSource>(),
            Spells = new List<SpellRequirement>
            {
                new() { Name = "A", ManaValue = 1, Pips = new Dictionary<ManaColor, int> { { ManaColor.White, 1 } } },
                new() { Name = "B", ManaValue = 2, Pips = new Dictionary<ManaColor, int> { { ManaColor.Blue, 1 } } },
            },
            AverageManaValue = 3.4,
            IsSingleton = true,
        };
        var rows = new[] { Row("A", mv: 1, turn: 1, isCommander: false, keepable: 800, kept7: 600, m6: 150, m5: 250) };

        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(deck, rows, defaultTrials: 1000);

        Assert.Equal(2, result.ColorCount);
        Assert.Equal(3.4, result.AverageManaValue);
    }

    // ---- golden / never-contradict / no-second-Simulate (Task 3) ----------------------------

    [Fact]
    public void Analyze_PopulatesMulliganEvaluation_WithPositiveKeepableAndBand()
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(MonoBlueDeck(), ManabaseMode.Casual);

        Assert.NotNull(report.MulliganEvaluation);
        Assert.True(report.MulliganEvaluation!.KeepableHandPercent > 0);
        Assert.NotEmpty(report.MulliganEvaluation.KeepableBand);
    }

    [Fact]
    public void Analyze_MonoColorFixture_NeverContradictsCastRate_NonForcedOpenersAlwaysMeetKeepFloor()
    {
        // Mono deck: ColorKeepCap is a no-op, so the keepable read uses the SAME land-band keep rule
        // the cast rate itself relies on (LondonMulligan). The keepable figure must therefore be
        // stable across repeated runs of the same fixture, and every NON-FORCED representative opener
        // (keep 7 / mulligan to 6, whose schedule enforces Lo=2) must show >= 2 lands — the keep
        // floor. (Only the FORCED final mulligan-to-5 legitimately accepts a hand with as few as 1
        // land, by design — London mulligan always returns a hand at the last depth.)
        ManabaseDeck deck = MonoBlueDeck();

        ManabaseReport a = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual);
        ManabaseReport b = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual);

        Assert.Equal(a.MulliganEvaluation!.KeepableHandPercent, b.MulliganEvaluation!.KeepableHandPercent);
        Assert.All(
            a.MulliganEvaluation.RepresentativeOpeners.Where(o => o.KeptCards != 5),
            o => Assert.True(o.Lands >= 2, $"non-forced opener (KeptCards={o.KeptCards}) should show >= 2 lands, got {o.Lands}"));
    }

    [Fact]
    public void Analyze_ColorScrewedFixture_LowerKeepableHandPercent_ThanWellFixedFixture()
    {
        // Same 2-color spell demand (Blue One + White One), color-aware mulligan on for both: the
        // color-starved fixture (30 Plains / 5 Islands) fails the color-keep gate far more often than
        // the well-fixed fixture (18/17 split), forcing more trials down to the forced final keep —
        // the SAME ColorKeepCap gate that lowers cast% also lowers KeepableHandPercent, so the two
        // reads can never disagree about a color-starved base.
        ManabaseReport skewed = ManabaseAnalyzer.Analyze(SkewedWhiteBlueDeck(), ManabaseMode.Casual, colorAwareMulligan: true);
        ManabaseReport wellFixed = ManabaseAnalyzer.Analyze(WellFixedWhiteBlueDeck(), ManabaseMode.Casual, colorAwareMulligan: true);

        Assert.True(
            skewed.MulliganEvaluation!.KeepableHandPercent < wellFixed.MulliganEvaluation!.KeepableHandPercent,
            $"expected the color-screwed fixture to show a lower keepable%% (skewed={skewed.MulliganEvaluation.KeepableHandPercent}, wellFixed={wellFixed.MulliganEvaluation.KeepableHandPercent})");
    }

    [Fact]
    public void Analyze_MulliganEvaluation_AddsNoSimulateCallsBeyondThePerSpellCastabilityRows()
    {
        // ComputeMulliganEvaluation only ever reads the `castability` list ALREADY built by the
        // single BuildCastability pass (it takes no ManabaseDeck-simulation dependency of its own).
        // Structural proof (not a count proxy): re-run the SAME aggregator over the report's already-
        // computed castability rows via the pure ComputeMulliganEvaluationForTest seam — which cannot
        // simulate anything, it only has the passed-in rows — and assert it reproduces the live
        // KeepableHandPercent byte-for-byte. If Analyze's mulligan figure had come from any extra
        // Simulate pass (fresh RNG draws), the seam over the frozen rows could not reproduce it.
        ManabaseDeck deck = SkewedWhiteBlueDeck();

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, colorAwareMulligan: true);

        Assert.NotNull(report.MulliganEvaluation);
        ManabaseMulliganEvaluation rederived = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(
            deck, report.Castability, defaultTrials: CastabilitySimulator.DefaultTrials);
        Assert.Equal(report.MulliganEvaluation!.KeepableHandPercent, rederived.KeepableHandPercent);
        Assert.Equal(report.MulliganEvaluation.Kept7Percent, rederived.Kept7Percent);
        Assert.Equal(report.MulliganEvaluation.MulliganTo6Percent, rederived.MulliganTo6Percent);
        Assert.Equal(report.MulliganEvaluation.MulliganTo5Percent, rederived.MulliganTo5Percent);
    }

    // ---- golden fixture builders --------------------------------------------------------------

    private static SpellRequirement GoldenSpell(string name, int mv, params (ManaColor Color, int Count)[] pips) => new()
    {
        Name = name,
        ManaValue = mv,
        Pips = pips.ToDictionary(p => p.Color, p => p.Count),
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
            Spells = new List<SpellRequirement> { GoldenSpell("Blue Spell", 3, (ManaColor.Blue, 1)) },
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck SkewedWhiteBlueDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 30; i++)
        {
            sources.Add(new ManaSource { Name = $"Plains {i}", Produces = new[] { ManaColor.White } });
        }

        for (int i = 0; i < 5; i++)
        {
            sources.Add(new ManaSource { Name = $"Island {i}", Produces = new[] { ManaColor.Blue } });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                GoldenSpell("Blue One", 1, (ManaColor.Blue, 1)),
                GoldenSpell("White One", 1, (ManaColor.White, 1)),
            },
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck WellFixedWhiteBlueDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 18; i++)
        {
            sources.Add(new ManaSource { Name = $"Plains {i}", Produces = new[] { ManaColor.White } });
        }

        for (int i = 0; i < 17; i++)
        {
            sources.Add(new ManaSource { Name = $"Island {i}", Produces = new[] { ManaColor.Blue } });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                GoldenSpell("Blue One", 1, (ManaColor.Blue, 1)),
                GoldenSpell("White One", 1, (ManaColor.White, 1)),
            },
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }

    // ---- builders ---------------------------------------------------------------------------

    private static ManabaseDeck Deck(double averageManaValue) => new()
    {
        TotalCards = 99,
        CommanderCount = 0,
        Sources = new List<ManaSource>(),
        Spells = new List<SpellRequirement>(),
        AverageManaValue = averageManaValue,
        IsSingleton = true,
    };

    private static CardCastability Row(
        string name, int mv, int turn, bool isCommander,
        int keepable, int kept7, int m6, int m5,
        IReadOnlyList<OpeningHandSample>? openers = null) => new()
        {
            Name = name,
            ManaValue = mv,
            OnCurveTurn = turn,
            CastPercent = 80,
            LimitingFactor = "mana",
            IsCommander = isCommander,
            KeepableTrials = keepable,
            Kept7Trials = kept7,
            MulliganTo6Trials = m6,
            MulliganTo5Trials = m5,
            RepresentativeOpeners = openers ?? System.Array.Empty<OpeningHandSample>(),
        };

    private static OpeningHandSample Sample(string decision, string trackedSpell, int trackedTurn, bool hasPlan) => new()
    {
        Lands = 3,
        Colors = 1,
        RampPieces = 0,
        OtherCards = decision == "keep 7" ? 4 : decision == "mulligan to 6" ? 3 : 2,
        KeptCards = decision switch { "keep 7" => 7, "mulligan to 6" => 6, _ => 5 },
        Decision = decision,
        TrackedSpellName = trackedSpell,
        TrackedOnCurveTurn = trackedTurn,
        OnCurveCastable = hasPlan,
        HasPlan = hasPlan,
    };
}
