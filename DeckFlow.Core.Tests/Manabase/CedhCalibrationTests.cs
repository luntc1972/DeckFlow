using DeckFlow.Core.Manabase;

using System.Globalization;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>Tests the pure cEDH calibration aggregation helper promoted from the throwaway harness.</summary>
public sealed class CedhCalibrationTests
{
    [Fact]
    public void Build_ComputesOverallSegmentAndCommanderRollups()
    {
        var rows = new List<CedhCalibrationRow>
        {
            new("Alpha", 25, 28, 24, true),
            new("Alpha", 25, 28, 24, true),
            new("Alpha", 25, 28, 24, true),
            new("Alpha", 25, 28, 24, true),
            new("Alpha", 25, 28, 24, true),
            new("Alpha", 25, 28, 24, true),
            new("Alpha", 25, 28, 24, true),
            new("Alpha", 25, 28, 24, true),
            new("Alpha", 25, 28, 24, true),
            new("Alpha", 25, 28, 24, true),
            new("Beta", 26, 28, 27, false),
            new("Beta", 30, 28, 31, false),
        };

        CedhCalibrationReport report = CedhCalibration.Build(rows);

        Assert.Equal(12, report.SampleSize);
        Assert.Equal(25.5, report.ActualLandsMean, 1);
        Assert.Equal(28.0, report.OldTargetMean, 3);
        Assert.Equal(24.833333333333332, report.NewTargetMean, 6);
        Assert.Equal(11, report.UnderOldCount);
        Assert.Equal(91.66666666666667, report.UnderOldPercent, 6);
        Assert.Equal(2, report.UnderNewCount);
        Assert.Equal(16.666666666666668, report.UnderNewPercent, 6);
        Assert.Equal(10, report.UnflaggedByNewCount);
        Assert.Equal(1, report.NewlyUnderCount);
        Assert.Equal(10, report.BaselineBackedCount);
        Assert.Equal(2, report.NoBaselineCount);
        Assert.Equal(0, report.SafetyFloorHitCount);
        Assert.Equal(0, report.CeilingHitCount);

        CedhCalibrationSegmentStats baseline = Assert.Single(report.Segments, segment => segment.Label == "Baseline N>=10");
        Assert.Equal(10, baseline.SampleSize);
        Assert.Equal(100.0, baseline.UnderOldPercent, 3);
        Assert.Equal(0.0, baseline.UnderNewPercent, 3);
        Assert.Equal(10, baseline.UnflaggedByNewCount);
        Assert.Equal(0, baseline.NewlyUnderCount);

        CedhCalibrationSegmentStats noBaseline = Assert.Single(report.Segments, segment => segment.Label == "No baseline");
        Assert.Equal(2, noBaseline.SampleSize);
        Assert.Equal(50.0, noBaseline.UnderOldPercent, 3);
        Assert.Equal(100.0, noBaseline.UnderNewPercent, 3);
        Assert.Equal(0, noBaseline.UnflaggedByNewCount);
        Assert.Equal(1, noBaseline.NewlyUnderCount);

        CedhCalibrationCommanderRollup alpha = Assert.Single(report.Commanders);
        Assert.Equal("Alpha", alpha.CommanderKey);
        Assert.Equal(10, alpha.SampleSize);
        Assert.Equal(25.0, alpha.ActualLandsMean, 3);
        Assert.Equal(28.0, alpha.OldTargetMean, 3);
        Assert.Equal(24.0, alpha.NewTargetMean, 3);
        Assert.Equal(100.0, alpha.UnderOldPercent, 3);
        Assert.Equal(0.0, alpha.UnderNewPercent, 3);
    }

    [Fact]
    public void Build_CountsFloorAndCeilingHits()
    {
        var rows = new[]
        {
            new CedhCalibrationRow("Floor", 22, 28, 22, false),
            new CedhCalibrationRow("Ceiling", 40, 28, 45, false),
        };

        CedhCalibrationReport report = CedhCalibration.Build(rows);

        Assert.Equal(1, report.SafetyFloorHitCount);
        Assert.Equal(1, report.CeilingHitCount);
    }

    [Fact]
    public void Build_RitualCreditColumns_DivergentFromNewTarget()
    {
        List<CedhCalibrationRow> rows =
        [
            new("Alpha", 24, 30, 28, 26, true),
            new("Alpha", 27, 30, 28, 26, true),
            new("Alpha", 29, 30, 28, 26, true),
            new("Alpha", 25, 30, 28, 26, true),
            new("Alpha", 26, 30, 28, 26, true),
            new("Alpha", 31, 30, 28, 26, true),
            new("Alpha", 28, 30, 28, 26, true),
            new("Alpha", 24, 30, 28, 26, true),
            new("Alpha", 27, 30, 28, 26, true),
            new("Alpha", 29, 30, 28, 26, true),
            new("Beta", 25, 29, 27, 25, false),
            new("Gamma", 25, 31, 24, 26, false),
        ];

        CedhCalibrationReport report = CedhCalibration.Build(rows);

        Assert.Equal(4, report.UnderRitualCreditCount);
        Assert.Equal(33.333333333333336, report.UnderRitualCreditPercent, 6);
        Assert.Equal(4, report.UnflaggedByRitualCreditCount);
        Assert.Equal(1, report.NewlyUnderRitualCreditCount);
        Assert.Equal(25.916666666666668, report.NewTargetWithRitualCreditMean, 6);
        Assert.Equal(25.0, report.NewTargetWithRitualCreditMin, 3);
        Assert.Equal(26.0, report.NewTargetWithRitualCreditMax, 3);

        CedhCalibrationSegmentStats baseline = Assert.Single(report.Segments, segment => segment.Label == "Baseline N>=10");
        Assert.Equal(3, baseline.UnderRitualCreditCount);
        Assert.Equal(30.0, baseline.UnderRitualCreditPercent, 3);
        Assert.Equal(26.0, baseline.NewTargetWithRitualCreditMean, 3);

        CedhCalibrationCommanderRollup alpha = Assert.Single(report.Commanders);
        Assert.Equal("Alpha", alpha.CommanderKey);
        Assert.Equal(3, alpha.UnderRitualCreditCount);
        Assert.Equal(30.0, alpha.UnderRitualCreditPercent, 3);
        Assert.Equal(26.0, alpha.NewTargetWithRitualCreditMean, 3);
    }

    [Fact]
    public void Build_MinMaxFields()
    {
        CedhCalibrationRow[] rows =
        [
            new("A", 24, 31, 28, 27, true),
            new("B", 25, 29, 23, 21, false),
            new("C", 26, 33, 30, 32, true),
        ];

        CedhCalibrationReport report = CedhCalibration.Build(rows);

        Assert.Equal(29.0, report.OldTargetMin, 3);
        Assert.Equal(33.0, report.OldTargetMax, 3);
        Assert.Equal(23.0, report.NewTargetMin, 3);
        Assert.Equal(30.0, report.NewTargetMax, 3);
        Assert.Equal(21.0, report.NewTargetWithRitualCreditMin, 3);
        Assert.Equal(32.0, report.NewTargetWithRitualCreditMax, 3);
    }

    [Fact]
    public void Build_EmptyRows_AllZeroesNoThrow()
    {
        CedhCalibrationReport report = CedhCalibration.Build(Enumerable.Empty<CedhCalibrationRow>());

        Assert.Equal(0, report.SampleSize);
        Assert.Equal(0.0, report.ActualLandsMean);
        Assert.Equal(0.0, report.OldTargetMean);
        Assert.Equal(0.0, report.NewTargetMean);
        Assert.Equal(0.0, report.NewTargetWithRitualCreditMean);
        Assert.Equal(0.0, report.OldTargetMin);
        Assert.Equal(0.0, report.OldTargetMax);
        Assert.Equal(0.0, report.NewTargetMin);
        Assert.Equal(0.0, report.NewTargetMax);
        Assert.Equal(0.0, report.NewTargetWithRitualCreditMin);
        Assert.Equal(0.0, report.NewTargetWithRitualCreditMax);
        Assert.Equal(0.0, report.UnderOldPercent);
        Assert.Equal(0.0, report.UnderNewPercent);
        Assert.Equal(0.0, report.UnderRitualCreditPercent);

        Assert.Collection(
            report.Segments,
            baseline =>
            {
                Assert.Equal("Baseline N>=10", baseline.Label);
                Assert.Equal(0, baseline.SampleSize);
            },
            noBaseline =>
            {
                Assert.Equal("No baseline", noBaseline.Label);
                Assert.Equal(0, noBaseline.SampleSize);
            });

        Assert.Empty(report.Commanders);
    }

    [Fact]
    public void NullGuards()
    {
        Assert.Throws<ArgumentNullException>(() => CedhCalibration.Build(null!));
        Assert.Throws<ArgumentNullException>(() => CedhCalibration.RenderMarkdown(null!));
        Assert.Throws<ArgumentNullException>(() => CedhCalibration.RenderHeadline(null!));
    }

    [Fact]
    public void Build_CommanderFilterBoundary()
    {
        List<CedhCalibrationRow> rows = [];
        rows.AddRange(CreateRows("NineRows", 9, 25, 30, 28, 26, true));
        rows.AddRange(CreateRows("TenRows", 10, 25, 30, 28, 26, true));

        CedhCalibrationReport report = CedhCalibration.Build(rows);

        CedhCalibrationCommanderRollup commander = Assert.Single(report.Commanders);
        Assert.Equal("TenRows", commander.CommanderKey);
        Assert.Equal(10, commander.SampleSize);
    }

    [Fact]
    public void Build_CommanderOrdering()
    {
        List<CedhCalibrationRow> rows = [];
        rows.AddRange(CreateRows("B", 10, 25, 30, 28, 26, true));
        rows.AddRange(CreateRows("C", 10, 25, 30, 28, 26, true));
        rows.AddRange(CreateRows("A", 12, 25, 30, 28, 26, true));

        CedhCalibrationReport report = CedhCalibration.Build(rows);

        Assert.Collection(
            report.Commanders,
            first => Assert.Equal("A", first.CommanderKey),
            second => Assert.Equal("B", second.CommanderKey),
            third => Assert.Equal("C", third.CommanderKey));
    }

    [Fact]
    public void Build_FiveArgCtor_DefaultsRitualTargetToNewTarget()
    {
        CedhCalibrationReport report = CedhCalibration.Build(
        [
            new CedhCalibrationRow("Solo", 24, 30, 26, true),
        ]);

        Assert.Equal(report.NewTargetMean, report.NewTargetWithRitualCreditMean);
        Assert.Equal(report.NewTargetMin, report.NewTargetWithRitualCreditMin);
        Assert.Equal(report.NewTargetMax, report.NewTargetWithRitualCreditMax);
        Assert.Equal(report.UnderNewCount, report.UnderRitualCreditCount);
    }

    [Fact]
    public void Build_CeilingCountsRitualColumn_FloorCountsNewTarget()
    {
        CedhCalibrationRow[] rows =
        [
            new("Floor", 21, 30, 22, 20, false),
            new("NewTargetCeilingOnly", 40, 30, 45, 42, false),
            new("RitualCreditCeiling", 40, 30, 44, 45, false),
        ];

        CedhCalibrationReport report = CedhCalibration.Build(rows);

        Assert.Equal(1, report.SafetyFloorHitCount);
        Assert.Equal(1, report.CeilingHitCount);
    }

    [Fact]
    public void RenderMarkdown_EscapesPipesAndTruncatesLongCommanderKeys()
    {
        const string commanderKey = "Commander|WithAnExcessivelyLongIdentifierForRollupTableOutput";
        CedhCalibrationReport report = CedhCalibration.Build(CreateRows(commanderKey, 10, 27, 30, 28, 26, true));

        string markdown = CedhCalibration.RenderMarkdown(report);

        Assert.Contains("Commander\\|WithAnExcessivelyLongIdentifierFor", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderMarkdown_PinsSummaryLinesAndSegmentTableHeader()
    {
        CedhCalibrationReport report = CedhCalibration.Build(CreateRows("Alpha", 10, 27, 30, 28, 26, true));

        string markdown = CedhCalibration.RenderMarkdown(report);

        // Characterization: the runbook artifact's summary lines and table header, pinned verbatim.
        Assert.Contains("**Under-target (actual < target):** OLD 10/10 = **100.0%** → NEW 10/10 = **100.0%**", markdown, StringComparison.Ordinal);
        Assert.Contains("RitualCredit delta vs NEW: un-flags **10** | newly flagged under: 0", markdown, StringComparison.Ordinal);
        Assert.Contains("| Segment | N | actual mean | old target | new target | RitualCredit | under OLD% | under NEW% | under RitualCredit% |", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderMarkdown_UsesInvariantFormatting()
    {
        CedhCalibrationReport report = CedhCalibration.Build(
        [
            new CedhCalibrationRow("A", 25, 28, 24, 24, true),
            new CedhCalibrationRow("B", 26, 28, 25, 25, false),
        ]);

        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");

            string markdown = CedhCalibration.RenderMarkdown(report);

            Assert.Contains("actual lands mean 25.5", markdown, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void RenderHeadline_FormatsExactly()
    {
        CedhCalibrationReport report = CedhCalibration.Build(
        [
            new CedhCalibrationRow("A", 24, 30, 26, 25, true),
            new CedhCalibrationRow("B", 27, 30, 26, 28, false),
            new CedhCalibrationRow("C", 31, 30, 26, 29, false),
            new CedhCalibrationRow("D", 25, 30, 26, 24, true),
        ]);

        string headline = CedhCalibration.RenderHeadline(report);

        Assert.Equal("SampleSize=4, UnderTarget=75.0% -> 50.0% -> RitualCredit 50.0%", headline);
    }

    private static IEnumerable<CedhCalibrationRow> CreateRows(
        string commanderKey,
        int count,
        int actualLands,
        double oldTarget,
        double newTarget,
        double newTargetWithRitualCredit,
        bool hasBaseline)
    {
        for (int index = 0; index < count; index++)
        {
            yield return new CedhCalibrationRow(
                commanderKey,
                actualLands,
                oldTarget,
                newTarget,
                newTargetWithRitualCredit,
                hasBaseline);
        }
    }
}
