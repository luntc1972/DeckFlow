using DeckFlow.Core.Manabase;
using System.Text.Json;
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

    private static CedhLandBaselineSnapshot MoverSnapshot(int count, double meanDelta, int startAt = 0)
    {
        (string, int, double)[] rows = Enumerable.Range(startAt, count)
            .Select(i => ($"Commander {i}", 50, 26.0 + meanDelta))
            .ToArray();
        return Snapshot("2026-07", rows);
    }

    [Fact]
    public void Evaluate_IdenticalSnapshots_Passes()
    {
        CedhLandBaselineSnapshot snapshot = Snapshot("2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(snapshot, snapshot, Thresholds);

        Assert.True(verdict.Passed);
        Assert.Empty(verdict.Findings);
    }

    [Fact]
    public void Evaluate_EmptyPreviousSnapshot_Fails()
    {
        CedhLandBaselineSnapshot previous = Snapshot("2026-07");
        CedhLandBaselineSnapshot candidate = Snapshot("2026-08", ("Kinnan, Bonder Prodigy", 337, 25.8));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        CedhDriftFinding finding = Assert.Single(verdict.Findings);
        Assert.Equal("EmptyPreviousSnapshot", finding.Rule);
        Assert.Null(finding.Commander);
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

    [Fact]
    public void Evaluate_PopulousCommanderSampleCollapses_Fails()
    {
        // Ral, Monsoon Mage fell 105 -> 7 (-93.3%) in the corrupt 2026-07 run because its own
        // card is a DFC and failed to resolve, so its decks could not be keyed to it.
        CedhLandBaselineSnapshot previous = Snapshot(
            "2026-07", ("Ral, Monsoon Mage // Ral, Leyline Prodigy", 105, 21.6));
        CedhLandBaselineSnapshot candidate = Snapshot(
            "2026-07", ("Ral, Monsoon Mage // Ral, Leyline Prodigy", 7, 17.9));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Rule == "SampleCollapse");
    }

    [Fact]
    public void Evaluate_OrdinaryWindowSlide_Passes()
    {
        // The corrected 2026-07 refresh's worst drop among populous commanders was -9.5%.
        CedhLandBaselineSnapshot previous = Snapshot("2026-07", ("Glarb, Calamity's Augur", 22, 28.5));
        CedhLandBaselineSnapshot candidate = Snapshot("2026-07", ("Glarb, Calamity's Augur", 20, 27.8));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_SampleDropExactlyAtLimit_Passes()
    {
        // Rule fires above the limit, not at it: 100 -> 60 is exactly 40%.
        CedhLandBaselineSnapshot previous = Snapshot("2026-07", ("Tivit, Seller of Secrets", 100, 28.3));
        CedhLandBaselineSnapshot candidate = Snapshot("2026-07", ("Tivit, Seller of Secrets", 60, 28.3));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_SampleDropJustPastLimit_Fails()
    {
        CedhLandBaselineSnapshot previous = Snapshot("2026-07", ("Tivit, Seller of Secrets", 100, 28.3));
        CedhLandBaselineSnapshot candidate = Snapshot("2026-07", ("Tivit, Seller of Secrets", 59, 28.3));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        Assert.Equal("SampleCollapse", Assert.Single(verdict.Findings).Rule);
    }

    [Fact]
    public void Evaluate_ThinCommanderSampleCollapses_Passes()
    {
        // Below MinPopulousN the swing is noise, not signal.
        CedhLandBaselineSnapshot previous = Snapshot("2026-07", ("Kaalia of the Vast", 19, 25.8));
        CedhLandBaselineSnapshot candidate = Snapshot("2026-07", ("Kaalia of the Vast", 3, 25.3));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_ManyMoversAllSameDirection_Fails()
    {
        // The corrupt 2026-07 run moved 42 commanders by >=0.5 lands and every single one moved
        // down. Metagame drift scatters; systematic corruption pushes one way.
        CedhLandBaselineSnapshot previous = MoverSnapshot(12, 0.0);
        CedhLandBaselineSnapshot candidate = MoverSnapshot(12, -1.0);

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        CedhDriftFinding finding = Assert.Single(verdict.Findings, f => f.Rule == "OneSidedDrift");
        Assert.Null(finding.Commander);
    }

    [Fact]
    public void Evaluate_ManyMoversMixedDirections_Passes()
    {
        var previousRows = Enumerable.Range(0, 12).Select(i => ($"Commander {i}", 50, 26.0)).ToArray();
        var candidateRows = Enumerable.Range(0, 12)
            .Select(i => ($"Commander {i}", 50, i % 2 == 0 ? 27.0 : 25.0))
            .ToArray();

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(
            Snapshot("2026-07", previousRows), Snapshot("2026-07", candidateRows), Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_FewMoversAllSameDirection_Passes()
    {
        // Below MinMoversForDirectionTest the rule is inert: the corrected 2026-07 refresh had
        // only 4 movers (1 up, 3 down), which is 75% one-sided by chance.
        CedhLandBaselineSnapshot previous = MoverSnapshot(4, 0.0);
        CedhLandBaselineSnapshot candidate = MoverSnapshot(4, -1.0);

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_MoversExactlyAtFloorAllSameDirection_Fails()
    {
        CedhLandBaselineSnapshot previous = MoverSnapshot(10, 0.0);
        CedhLandBaselineSnapshot candidate = MoverSnapshot(10, -1.0);

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        Assert.Equal("OneSidedDrift", Assert.Single(verdict.Findings).Rule);
    }

    [Fact]
    public void Evaluate_OneBelowMoverFloorAllSameDirection_Passes()
    {
        CedhLandBaselineSnapshot previous = MoverSnapshot(9, 0.0);
        CedhLandBaselineSnapshot candidate = MoverSnapshot(9, -1.0);

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_MovementExactlyAtThresholdCountsAsMover_Fails()
    {
        CedhLandBaselineSnapshot previous = MoverSnapshot(10, 0.0);
        CedhLandBaselineSnapshot candidate = MoverSnapshot(10, -0.5);

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        Assert.Equal("OneSidedDrift", Assert.Single(verdict.Findings).Rule);
    }

    [Fact]
    public void Evaluate_SubThresholdMovementIsNotAMover()
    {
        // 0.4 lands is below MoverThresholdLands, so these do not count even though all 20 shift
        // the same way.
        CedhLandBaselineSnapshot previous = MoverSnapshot(20, 0.0);
        CedhLandBaselineSnapshot candidate = MoverSnapshot(20, -0.4);

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void FromJson_CompleteDocument_BindsEveryThreshold()
    {
        const string Json = """
            {
              "minEstablishedN": 10,
              "minPopulousN": 20,
              "maxSampleDropPct": 40,
              "moverThresholdLands": 0.5,
              "minMoversForDirectionTest": 10,
              "maxOneSidedPct": 90
            }
            """;

        CedhDriftThresholds thresholds = CedhDriftThresholds.FromJson(Json);

        Assert.Equal(10, thresholds.MinEstablishedN);
        Assert.Equal(20, thresholds.MinPopulousN);
        Assert.Equal(40, thresholds.MaxSampleDropPct);
        Assert.Equal(0.5, thresholds.MoverThresholdLands);
        Assert.Equal(10, thresholds.MinMoversForDirectionTest);
        Assert.Equal(90, thresholds.MaxOneSidedPct);
    }

    [Fact]
    public void FromJson_MissingProperty_Throws()
    {
        // A typo must not silently disable the guard, so there are no code-side defaults.
        const string Json = """
            {
              "minEstablishedN": 10,
              "minPopulousN": 20,
              "maxSampleDropPct": 40,
              "moverThresholdLands": 0.5,
              "minMoversForDirectionTest": 10
            }
            """;

        Assert.Throws<JsonException>(() => CedhDriftThresholds.FromJson(Json));
    }

    [Fact]
    public void FromJson_Garbage_Throws()
    {
        Assert.Throws<JsonException>(() => CedhDriftThresholds.FromJson("{ nope"));
    }
}
