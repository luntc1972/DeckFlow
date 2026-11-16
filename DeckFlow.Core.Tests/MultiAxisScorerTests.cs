using DeckFlow.Core.Analysis;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for <see cref="MultiAxisScorer"/> — covers band-label mapping (SCORE-01),
/// per-axis derivations (SCORE-02), the golden cEDH-vs-battlecruiser separation (SCORE-03),
/// the bracket cross-check, and the combo-unavailable disclosure (null-vs-empty semantics).
/// </summary>
public sealed class MultiAxisScorerTests
{
    // -----------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------

    private static IReadOnlyDictionary<string, int> Curve() => new Dictionary<string, int>
    {
        ["0-1"] = 30,
        ["2"] = 20,
        ["3"] = 15,
        ["4"] = 10,
        ["5+"] = 5,
    };

    /// <summary>A cEDH-proxy deck: low curve, heavy fast mana, dense tutors/interaction/counters.</summary>
    private static DeckStatSummary CedhStats() => new DeckStatSummary(
        Cards: 99, Lands: 31, Creatures: 25,
        AverageManaValue: 1.8m,
        ManaCurve: Curve(),
        Ramp: 15, Draw: 12, Interaction: 18, Wipes: 2, Recursion: 3, ClosingPower: 8)
    {
        Tutors = 12,
        FastMana = 10,
        RampDrawUnderThreeMv = 18,
        Counters = 8,
    };

    /// <summary>A battlecruiser-proxy deck: high curve, almost no acceleration or tutoring.</summary>
    private static DeckStatSummary CasualStats() => new DeckStatSummary(
        Cards: 99, Lands: 38, Creatures: 28,
        AverageManaValue: 3.8m,
        ManaCurve: Curve(),
        Ramp: 8, Draw: 5, Interaction: 4, Wipes: 1, Recursion: 2, ClosingPower: 5)
    {
        Tutors = 1,
        FastMana = 1,
        RampDrawUnderThreeMv = 4,
        Counters = 1,
    };

    /// <summary>A dense-interaction control shell for the Control-axis derivation test.</summary>
    private static DeckStatSummary ControlStats() => new DeckStatSummary(
        Cards: 99, Lands: 36, Creatures: 12,
        AverageManaValue: 2.8m,
        ManaCurve: Curve(),
        Ramp: 9, Draw: 9, Interaction: 15, Wipes: 4, Recursion: 2, ClosingPower: 4)
    {
        Tutors = 4,
        FastMana = 3,
        RampDrawUnderThreeMv = 7,
        Counters = 5,
    };

    /// <summary>A tutor-heavy, combo-redundant deck for the Consistency-axis derivation test.</summary>
    private static DeckStatSummary ConsistencyStats() => new DeckStatSummary(
        Cards: 99, Lands: 33, Creatures: 20,
        AverageManaValue: 2.3m,
        ManaCurve: Curve(),
        Ramp: 11, Draw: 9, Interaction: 9, Wipes: 1, Recursion: 3, ClosingPower: 6)
    {
        Tutors = 9,
        FastMana = 5,
        RampDrawUnderThreeMv = 10,
        Counters = 3,
    };

    // -----------------------------------------------------------------------
    // BandLabel mapping (SCORE-01)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, "None")]
    [InlineData(1, "Low")]
    [InlineData(2, "Modest")]
    [InlineData(3, "Moderate")]
    [InlineData(4, "High")]
    [InlineData(5, "Extreme")]
    [InlineData(6, "Extreme")]   // out-of-range clamps to Extreme
    public void BandLabel_MapsCorrectly(int band, string expected)
    {
        Assert.Equal(expected, MultiAxisScorer.BandLabel(band));
    }

    // -----------------------------------------------------------------------
    // Golden test: cEDH vs battlecruiser (SCORE-03)
    // -----------------------------------------------------------------------

    [Fact]
    public void Score_CedhDeck_ScoresPowerAndSpeedHigh()
    {
        var score = MultiAxisScorer.Score(
            CedhStats(),
            gameChangerCount: 8,
            twoCardComboCount: 3,
            comboDetectionAvailable: true,
            bracketNumber: 5);

        Assert.True(score.PowerBand >= 4, $"Expected PowerBand >= 4, got {score.PowerBand}");
        Assert.True(score.SpeedBand >= 4, $"Expected SpeedBand >= 4, got {score.SpeedBand}");
    }

    [Fact]
    public void Score_CasualDeck_ScoresPowerAndSpeedLow()
    {
        var score = MultiAxisScorer.Score(
            CasualStats(),
            gameChangerCount: 0,
            twoCardComboCount: 0,
            comboDetectionAvailable: true,
            bracketNumber: 2);

        Assert.True(score.PowerBand <= 2, $"Expected PowerBand <= 2, got {score.PowerBand}");
        Assert.True(score.SpeedBand <= 2, $"Expected SpeedBand <= 2, got {score.SpeedBand}");
    }

    [Fact]
    public void Score_CedhVsCasual_PowerAndSpeedSeparate()
    {
        var cedh = MultiAxisScorer.Score(CedhStats(), 8, 3, true, 5);
        var casual = MultiAxisScorer.Score(CasualStats(), 0, 0, true, 2);

        Assert.True(cedh.PowerBand > casual.PowerBand, "cEDH Power should exceed casual Power");
        Assert.True(cedh.SpeedBand > casual.SpeedBand, "cEDH Speed should exceed casual Speed");
    }

    // -----------------------------------------------------------------------
    // Per-axis derivations (SCORE-02)
    // -----------------------------------------------------------------------

    [Fact]
    public void Score_DenseInteraction_ScoresControlHigh()
    {
        var score = MultiAxisScorer.Score(ControlStats(), 2, 1, true, 3);

        Assert.True(score.ControlBand >= 4, $"Expected ControlBand >= 4, got {score.ControlBand}");
    }

    [Fact]
    public void Score_ManyTutorsAndCombos_ScoresConsistencyHigh()
    {
        var score = MultiAxisScorer.Score(ConsistencyStats(), 3, 2, true, 4);

        Assert.True(
            score.ConsistencyBand >= 4,
            $"Expected ConsistencyBand >= 4, got {score.ConsistencyBand}");
    }

    [Fact]
    public void Score_ConsistencyRationale_DoesNotClaimSmoothCurve()
    {
        var score = MultiAxisScorer.Score(CasualStats(), 0, 0, true, 2);

        Assert.Contains("3.8", score.ConsistencyRationale.SignalText, StringComparison.Ordinal);
        Assert.DoesNotContain("smooth", score.ConsistencyRationale.SignalText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Score_AllBandsWithinZeroToFive()
    {
        var score = MultiAxisScorer.Score(CedhStats(), 25, 9, true, 5);

        Assert.InRange(score.PowerBand, 0, 5);
        Assert.InRange(score.SpeedBand, 0, 5);
        Assert.InRange(score.ControlBand, 0, 5);
        Assert.InRange(score.ConsistencyBand, 0, 5);
    }

    // -----------------------------------------------------------------------
    // Rationale + combo-unavailable disclosure (null-vs-empty semantics)
    // -----------------------------------------------------------------------

    [Fact]
    public void Score_ComboUnavailable_DisclosesInRationale()
    {
        var score = MultiAxisScorer.Score(
            CedhStats(),
            gameChangerCount: 8,
            twoCardComboCount: 0,
            comboDetectionAvailable: false,
            bracketNumber: 5);

        Assert.Contains("combo data unavailable", score.PowerRationale.SignalText, StringComparison.Ordinal);
        Assert.DoesNotContain("0 two-card combos", score.PowerRationale.SignalText, StringComparison.Ordinal);
    }

    [Fact]
    public void Score_RationaleCarriesSignalValues()
    {
        var score = MultiAxisScorer.Score(CedhStats(), 8, 3, true, 5);

        Assert.Contains("8 Game Changers", score.PowerRationale.SignalText, StringComparison.Ordinal);
        Assert.Contains("3 two-card combos", score.PowerRationale.SignalText, StringComparison.Ordinal);
        Assert.Contains("10 fast-mana", score.PowerRationale.SignalText, StringComparison.Ordinal);
        Assert.Contains("18 interaction pieces", score.ControlRationale.SignalText, StringComparison.Ordinal);
        Assert.Contains("12 tutors", score.ConsistencyRationale.SignalText, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Bracket cross-check (SCORE-03)
    // -----------------------------------------------------------------------

    [Fact]
    public void Score_BandsConsistentWithBracket_AlignsTrue()
    {
        var score = MultiAxisScorer.Score(CedhStats(), 8, 3, true, 5);

        Assert.True(score.ScoreAlignsBracket);
        Assert.Contains("aligns with the Bracket 5", score.BracketCrossCheckText, StringComparison.Ordinal);
    }

    [Fact]
    public void Score_HighPowerLowBracket_DivergesAndNamesContradiction()
    {
        // High-power signals but bracketNumber forced to 2 → divergence.
        var score = MultiAxisScorer.Score(CedhStats(), 12, 3, true, 2);

        Assert.False(score.ScoreAlignsBracket);
        Assert.Contains("disagree", score.BracketCrossCheckText, StringComparison.Ordinal);
        Assert.Contains("Bracket 2", score.BracketCrossCheckText, StringComparison.Ordinal);
    }

    [Fact]
    public void Score_NullStats_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => MultiAxisScorer.Score(null!, 0, 0, true, 1));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, "4", 0, 0, 0, 0, 0)]
    [InlineData(1, 0, 0, 1, "4", 1, 1, 0, 0, 1)]
    [InlineData(2, 1, 0, 0, "3.5", 3, 4, 0, 0, 2)]
    [InlineData(3, 1, 1, 3, "3", 5, 8, 1, 0, 5)]
    [InlineData(4, 4, 2, 5, "2.5", 8, 13, 0, 3, 8)]
    [InlineData(5, 10, 3, 8, "1.9", 12, 19, 5, 6, 12)]
    public void Score_EachAxis_ReportsFiredRule(int band, int gc, int combos, int fast,
        string mv, int early, int interaction, int wipes, int counters, int tutors)
    {
        var stats = new DeckStatSummary(99, 30, 20,
            decimal.Parse(mv, System.Globalization.CultureInfo.InvariantCulture), Curve(),
            0, 0, interaction, wipes, 0, 0)
        {
            FastMana = fast,
            RampDrawUnderThreeMv = early,
            Counters = counters,
            Tutors = tutors,
        };
        var score = MultiAxisScorer.Score(stats, gc, combos, true, 3);
        Assert.Equal(new[] { band, band, band, band },
            new[] { score.PowerBand, score.SpeedBand, score.ControlBand, score.ConsistencyBand });
        foreach (var rationale in new[] { score.PowerRationale, score.SpeedRationale,
            score.ControlRationale, score.ConsistencyRationale })
        {
            Assert.False(string.IsNullOrWhiteSpace(rationale.RuleText));
            Assert.All(rationale.RuleText!, c => Assert.InRange((int)c, 32, 126));
        }
    }

    [Theory]
    [InlineData("Power", 10, 5, "10")]
    [InlineData("Speed", 8, 5, "8")]
    [InlineData("Control", 19, 5, "19")]
    [InlineData("Consistency", 12, 5, "12")]
    public void Score_RuleText_CarriesBoundaryThreshold(string axis, int threshold, int band, string digits)
    {
        DeckMultiAxisScore At(int value)
        {
            var stats = new DeckStatSummary(99, 30, 20, 1.9m, Curve(), 0, 0,
                axis == "Control" ? value : 0, 5, 0, 0)
            {
                FastMana = axis == "Speed" ? value : 0,
                RampDrawUnderThreeMv = 12,
                Counters = 6,
                Tutors = axis == "Consistency" ? value : 0,
            };
            return MultiAxisScorer.Score(stats, axis == "Power" ? value : 0, 3, true, 3);
        }
        (int Band, DeckScoreRationale Rationale) Axis(DeckMultiAxisScore score) => axis switch
        {
            "Power" => (score.PowerBand, score.PowerRationale),
            "Speed" => (score.SpeedBand, score.SpeedRationale),
            "Control" => (score.ControlBand, score.ControlRationale),
            _ => (score.ConsistencyBand, score.ConsistencyRationale),
        };
        var exact = Axis(At(threshold));
        Assert.Equal(band, exact.Band);
        Assert.Contains(digits, exact.Rationale.RuleText);
        Assert.True(Axis(At(threshold - 1)).Band < band);
    }
}
