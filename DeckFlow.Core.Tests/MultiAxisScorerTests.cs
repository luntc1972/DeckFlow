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

    /// <summary>Pins each threshold, precedence, fallback, and invariant rule description.</summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    public void Score_BoundaryGrid_PreservesBandsAndExactRuleText(string cultureName)
    {
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(cultureName);
            var cases = 0;
            // Rows follow the outer value; columns follow the two inner values.
            string[] powerBands =
            [
                "01122222222222", // gameChangerCount = 0
                "22223333333333", // gameChangerCount = 1
                "22223333333333", // gameChangerCount = 3
                "33333344444444", // gameChangerCount = 4
                "33333344444444", // gameChangerCount = 9
                "55555555555555", // gameChangerCount = 10
            ];
            string[] powerRules =
            [
                "None of the higher-band rules matched.",
                "1+ fast mana",
                "1+ Game Changers or 1+ two-card combos or 3+ fast mana",
                "4+ Game Changers or (1+ Game Changers and (1+ two-card combos or 4+ fast mana))",
                "4+ Game Changers and (1+ two-card combos or 6+ fast mana)",
                "10+ Game Changers",
            ];
            int[] powerOuterValues = [0, 1, 3, 4, 9, 10];
            int[] powerMiddleValues = [0, 1];
            int[] powerInnerValues = [0, 1, 2, 3, 4, 5, 6];
            for (var row = 0; row < powerOuterValues.Length; row++)
            {
                var column = 0;
                foreach (var middle in powerMiddleValues)
                {
                    foreach (var inner in powerInnerValues)
                    {
                        var stats = new DeckStatSummary(99, 30, 20, 4m, Curve(),
                            0, 0, 0, 0, 0, 0)
                        {
                            FastMana = inner,
                            RampDrawUnderThreeMv = 0,
                            Counters = 0,
                            Tutors = 0,
                        };
                        var score = MultiAxisScorer.Score(stats, powerOuterValues[row], middle, true, 3);
                        var expectedBand = powerBands[row][column++] - '0';
                        Assert.Equal(expectedBand, score.PowerBand);
                        Assert.Equal(powerRules[expectedBand], score.PowerRationale.RuleText);
                        cases++;
                    }
                }
            }

            // Rows follow the outer value; columns follow the two inner values.
            string[] speedBands =
            [
                "01122222221112222222111222222211122333331112233333111223344411122334441112233445", // avgMv = 1.9
                "01122222221112222222111222222211122333331112233333111223344411122334441112233444", // avgMv = 2
                "01122222221112222222111222222211122333331112233333111223344411122334441112233444", // avgMv = 2.4
                "01122222221112222222111222222211122333331112233333111223344411122334441112233444", // avgMv = 2.5
                "01122222221112222222111222222211122333331112233333111223333311122333331112233333", // avgMv = 2.9
                "01122222221112222222111222222211122333331112233333111223333311122333331112233333", // avgMv = 3
                "01122222221112222222111222222211122222221112222222111222222211122222221112222222", // avgMv = 3.4
                "01122222221112222222111222222211122222221112222222111222222211122222221112222222", // avgMv = 3.5
                "01111111111111111111111111111111111111111111111111111111111111111111111111111111", // avgMv = 4
            ];
            string[] speedRules =
            [
                "None of the higher-band rules matched.",
                "1+ fast mana or 1+ ramp/draw under 3 MV",
                "average mana value <= 3.5 and 3+ ramp/draw under 3 MV",
                "average mana value <= 3.0 and 3+ fast mana and 5+ ramp/draw under 3 MV",
                "average mana value <= 2.5 and 5+ fast mana and 8+ ramp/draw under 3 MV",
                "average mana value < 2.0 and 8+ fast mana and 12+ ramp/draw under 3 MV",
            ];
            decimal[] speedOuterValues = [1.9m, 2m, 2.4m, 2.5m, 2.9m, 3m, 3.4m, 3.5m, 4m];
            int[] speedMiddleValues = [0, 1, 2, 3, 4, 5, 7, 8];
            int[] speedInnerValues = [0, 1, 2, 3, 4, 5, 7, 8, 11, 12];
            for (var row = 0; row < speedOuterValues.Length; row++)
            {
                var column = 0;
                foreach (var middle in speedMiddleValues)
                {
                    foreach (var inner in speedInnerValues)
                    {
                        var stats = new DeckStatSummary(99, 30, 20, speedOuterValues[row], Curve(),
                            0, 0, 0, 0, 0, 0)
                        {
                            FastMana = middle,
                            RampDrawUnderThreeMv = inner,
                            Counters = 0,
                            Tutors = 0,
                        };
                        var score = MultiAxisScorer.Score(stats, 0, 0, true, 3);
                        var expectedBand = speedBands[row][column++] - '0';
                        Assert.Equal(expectedBand, score.SpeedBand);
                        Assert.Equal(speedRules[expectedBand], score.SpeedRationale.RuleText);
                        cases++;
                    }
                }
            }

            // Rows follow the outer value; columns follow the two inner values.
            string[] controlBands =
            [
                "011111111111111111111111", // Interaction = 0
                "111111111111111111111111", // Interaction = 1
                "111111111111111111111111", // Interaction = 3
                "222222222222222222222222", // Interaction = 4
                "222222222222222222222222", // Interaction = 7
                "222222333333333333333333", // Interaction = 8
                "222222333333333333333333", // Interaction = 12
                "222444333444333444333444", // Interaction = 13
                "222444333444333444333444", // Interaction = 18
                "222444333444333444333445", // Interaction = 19
            ];
            string[] controlRules =
            [
                "None of the higher-band rules matched.",
                "1+ interaction pieces or 1+ board wipes or 1+ counters",
                "4+ interaction pieces",
                "8+ interaction pieces and 1+ board wipes",
                "13+ interaction pieces and 3+ counters",
                "19+ interaction pieces and 5+ board wipes and 6+ counters",
            ];
            int[] controlOuterValues = [0, 1, 3, 4, 7, 8, 12, 13, 18, 19];
            int[] controlMiddleValues = [0, 1, 4, 5];
            int[] controlInnerValues = [0, 1, 2, 3, 5, 6];
            for (var row = 0; row < controlOuterValues.Length; row++)
            {
                var column = 0;
                foreach (var middle in controlMiddleValues)
                {
                    foreach (var inner in controlInnerValues)
                    {
                        var stats = new DeckStatSummary(99, 30, 20, 4m, Curve(),
                            0, 0, controlOuterValues[row], middle, 0, 0)
                        {
                            FastMana = 0,
                            RampDrawUnderThreeMv = 0,
                            Counters = inner,
                            Tutors = 0,
                        };
                        var score = MultiAxisScorer.Score(stats, 0, 0, true, 3);
                        var expectedBand = controlBands[row][column++] - '0';
                        Assert.Equal(expectedBand, score.ControlBand);
                        Assert.Equal(controlRules[expectedBand], score.ControlRationale.RuleText);
                        cases++;
                    }
                }
            }

            // Rows follow the outer value; columns follow the two inner values.
            string[] consistencyBands =
            [
                "00111111", // Tutors = 0
                "11111111", // Tutors = 1
                "22222222", // Tutors = 2
                "22222222", // Tutors = 4
                "22333333", // Tutors = 5
                "22333333", // Tutors = 7
                "22334444", // Tutors = 8
                "22334444", // Tutors = 11
                "22334454", // Tutors = 12
            ];
            string[] consistencyRules =
            [
                "None of the higher-band rules matched.",
                "1+ tutors or 1+ two-card combos",
                "2+ tutors",
                "5+ tutors and 1+ two-card combos",
                "8+ tutors and 2+ two-card combos",
                "12+ tutors and 3+ two-card combos and average mana value < 2.0",
            ];
            int[] consistencyOuterValues = [0, 1, 2, 4, 5, 7, 8, 11, 12];
            int[] consistencyMiddleValues = [0, 1, 2, 3];
            decimal[] consistencyInnerValues = [1.9m, 2m];
            for (var row = 0; row < consistencyOuterValues.Length; row++)
            {
                var column = 0;
                foreach (var middle in consistencyMiddleValues)
                {
                    foreach (var inner in consistencyInnerValues)
                    {
                        var stats = new DeckStatSummary(99, 30, 20, inner, Curve(),
                            0, 0, 0, 0, 0, 0)
                        {
                            FastMana = 0,
                            RampDrawUnderThreeMv = 0,
                            Counters = 0,
                            Tutors = consistencyOuterValues[row],
                        };
                        var score = MultiAxisScorer.Score(stats, 0, middle, true, 3);
                        var expectedBand = consistencyBands[row][column++] - '0';
                        Assert.Equal(expectedBand, score.ConsistencyBand);
                        Assert.Equal(consistencyRules[expectedBand], score.ConsistencyRationale.RuleText);
                        cases++;
                    }
                }
            }

            Assert.Equal(1116, cases);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previousCulture;
        }
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

    [Fact]
    public void Score_PowerRuleText_CarriesBoundaryThreshold()
    {
        DeckMultiAxisScore At(int value)
        {
            var stats = new DeckStatSummary(99, 30, 20, 1.9m, Curve(), 0, 0,
                0, 5, 0, 0)
            {
                FastMana = 0,
                RampDrawUnderThreeMv = 12,
                Counters = 6,
                Tutors = 0,
            };
            return MultiAxisScorer.Score(stats, value, 3, true, 3);
        }
        var exact = At(10);
        Assert.Equal(5, exact.PowerBand);
        Assert.Contains("10", exact.PowerRationale.RuleText);
        Assert.True(At(9).PowerBand < 5);
    }

    [Fact]
    public void Score_SpeedRuleText_CarriesBoundaryThreshold()
    {
        DeckMultiAxisScore At(int value)
        {
            var stats = new DeckStatSummary(99, 30, 20, 1.9m, Curve(), 0, 0,
                0, 5, 0, 0)
            {
                FastMana = value,
                RampDrawUnderThreeMv = 12,
                Counters = 6,
                Tutors = 0,
            };
            return MultiAxisScorer.Score(stats, 0, 3, true, 3);
        }
        var exact = At(8);
        Assert.Equal(5, exact.SpeedBand);
        Assert.Contains("8", exact.SpeedRationale.RuleText);
        Assert.True(At(7).SpeedBand < 5);
    }

    [Fact]
    public void Score_ControlRuleText_CarriesBoundaryThreshold()
    {
        DeckMultiAxisScore At(int value)
        {
            var stats = new DeckStatSummary(99, 30, 20, 1.9m, Curve(), 0, 0,
                value, 5, 0, 0)
            {
                FastMana = 0,
                RampDrawUnderThreeMv = 12,
                Counters = 6,
                Tutors = 0,
            };
            return MultiAxisScorer.Score(stats, 0, 3, true, 3);
        }
        var exact = At(19);
        Assert.Equal(5, exact.ControlBand);
        Assert.Contains("19", exact.ControlRationale.RuleText);
        Assert.True(At(18).ControlBand < 5);
    }

    [Fact]
    public void Score_ConsistencyRuleText_CarriesBoundaryThreshold()
    {
        DeckMultiAxisScore At(int value)
        {
            var stats = new DeckStatSummary(99, 30, 20, 1.9m, Curve(), 0, 0,
                0, 5, 0, 0)
            {
                FastMana = 0,
                RampDrawUnderThreeMv = 12,
                Counters = 6,
                Tutors = value,
            };
            return MultiAxisScorer.Score(stats, 0, 3, true, 3);
        }
        var exact = At(12);
        Assert.Equal(5, exact.ConsistencyBand);
        Assert.Contains("12", exact.ConsistencyRationale.RuleText);
        Assert.True(At(11).ConsistencyBand < 5);
    }
}
