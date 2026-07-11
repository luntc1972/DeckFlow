using DeckFlow.Core.Manabase;

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
}
