using DeckFlow.Core.Manabase;
using Xunit;

namespace DeckFlow.Core.Tests.Manabase;

/// <summary>
/// Verifies the drift guard that compares a candidate cEDH baseline snapshot against the
/// committed one. Thresholds are calibrated against the 2026-07-27 corruption incident.
/// </summary>
public sealed class CedhBaselineDriftCheckTests
{
    private static readonly CedhDriftThresholds Thresholds = new()
    {
        MinEstablishedN = 10,
        MinPopulousN = 20,
        MaxSampleDropPct = 40,
        MoverThresholdLands = 0.5,
        MinMoversForDirectionTest = 10,
        MaxOneSidedPct = 90,
    };

    private static CedhLandBaselineSnapshot Snapshot(
        string generated,
        params (string Name, int N, double Mean)[] commanders) =>
        new()
        {
            Generated = generated,
            SampleSize = commanders.Sum(c => c.N),
            OverallMeanLands = commanders.Length == 0 ? 0 : Math.Round(commanders.Average(c => c.Mean), 1),
            Commanders = commanders.ToDictionary(
                c => c.Name,
                c => new CedhCommanderBaselineSnapshot { N = c.N, LandsMean = c.Mean, LandsSd = 1.0 }),
        };

    [Fact]
    public void Evaluate_IdenticalSnapshots_Passes()
    {
        CedhLandBaselineSnapshot snapshot = Snapshot("2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(snapshot, snapshot, Thresholds);

        Assert.True(verdict.Passed);
        Assert.Empty(verdict.Findings);
    }

    [Fact]
    public void Evaluate_EstablishedCommanderDisappears_Fails()
    {
        // "The Cabbage Merchant" sat at n=18 and vanished entirely in the corrupt 2026-07 run.
        CedhLandBaselineSnapshot previous = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8), ("The Cabbage Merchant", 18, 24.9));
        CedhLandBaselineSnapshot candidate = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        CedhDriftFinding finding = Assert.Single(verdict.Findings);
        Assert.Equal("DroppedEstablishedCommander", finding.Rule);
        Assert.Equal("The Cabbage Merchant", finding.Commander);
    }

    [Fact]
    public void Evaluate_ThinCommanderDisappears_Passes()
    {
        // Below MinEstablishedN the sample is too small for absence to mean anything.
        CedhLandBaselineSnapshot previous = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8), ("Yusri, Fortune's Flame", 3, 25.3));
        CedhLandBaselineSnapshot candidate = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_CommanderExactlyAtEstablishedFloorDisappears_Fails()
    {
        CedhLandBaselineSnapshot previous = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8), ("Elsha of the Infinite", 10, 25.2));
        CedhLandBaselineSnapshot candidate = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        Assert.Equal("Elsha of the Infinite", Assert.Single(verdict.Findings).Commander);
    }

    [Fact]
    public void Evaluate_NewCommanderAppears_Passes()
    {
        CedhLandBaselineSnapshot previous = Snapshot("2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8));
        CedhLandBaselineSnapshot candidate = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8), ("Super-Skrull", 3, 27.0));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }
}
