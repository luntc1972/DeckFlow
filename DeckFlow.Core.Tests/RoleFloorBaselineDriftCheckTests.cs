using DeckFlow.Core.Research;
using System.Text.Json;

namespace DeckFlow.Core.Tests;

public sealed class RoleFloorBaselineDriftCheckTests
{
    [Fact]
    public void Evaluate_EmptyPreviousSnapshot_Fails()
    {
        RoleFloorBaselineSnapshot previous = Snapshot("2026-07-28");
        RoleFloorBaselineSnapshot candidate = Snapshot("2026-07-29", ("Fire Lord Azula", 50, Floors(("ramp", 7))));

        RoleFloorDriftVerdict verdict = RoleFloorBaselineDriftCheck.Evaluate(previous, candidate, Thresholds());

        Assert.False(verdict.Passed);
        Assert.Equal("EmptyPreviousSnapshot", Assert.Single(verdict.Findings).Rule);
    }

    [Fact]
    public void Evaluate_EstablishedCommanderDisappears_Fails()
    {
        RoleFloorBaselineSnapshot previous = Snapshot(
            "2026-07-28",
            ("Commander Kept", 50, Floors(("ramp", 7), ("draw", 5))),
            ("Commander Dropped", 10, Floors(("draw", 6))));
        RoleFloorBaselineSnapshot candidate = Snapshot(
            "2026-07-29",
            ("Commander Kept", 50, Floors(("ramp", 7), ("draw", 5))));

        RoleFloorDriftVerdict verdict = RoleFloorBaselineDriftCheck.Evaluate(previous, candidate, Thresholds());

        Assert.False(verdict.Passed);
        RoleFloorDriftFinding finding = Assert.Single(verdict.Findings);
        Assert.Equal("DroppedEstablishedCommander", finding.Rule);
        Assert.Equal("Commander Dropped", finding.Commander);
    }

    [Fact]
    public void Evaluate_EstablishedCommanderDisappears_BelowEstablishedN_Passes()
    {
        RoleFloorBaselineSnapshot previous = Snapshot(
            "2026-07-28",
            ("Commander Kept", 50, Floors(("ramp", 7), ("draw", 5))),
            ("Commander Thin", 9, Floors(("draw", 6))));
        RoleFloorBaselineSnapshot candidate = Snapshot(
            "2026-07-29",
            ("Commander Kept", 50, Floors(("ramp", 7), ("draw", 5))));

        RoleFloorDriftVerdict verdict = RoleFloorBaselineDriftCheck.Evaluate(previous, candidate, Thresholds());

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_EstablishedCommanderLosesARole_Fails()
    {
        RoleFloorBaselineSnapshot previous = Snapshot(
            "2026-07-28",
            ("Fire Lord Azula", 50, Floors(("ramp", 7), ("draw", 5), ("engines", 3))));
        RoleFloorBaselineSnapshot candidate = Snapshot(
            "2026-07-29",
            ("Fire Lord Azula", 50, Floors(("draw", 5), ("engines", 3))));

        RoleFloorDriftVerdict verdict = RoleFloorBaselineDriftCheck.Evaluate(previous, candidate, Thresholds());

        Assert.False(verdict.Passed);
        RoleFloorDriftFinding finding = Assert.Single(verdict.Findings);
        Assert.Equal("DroppedEstablishedRole", finding.Rule);
        Assert.Equal("Fire Lord Azula", finding.Commander);
        Assert.Equal("ramp", finding.Role);
    }

    [Fact]
    public void Evaluate_PopulousCommanderSampleCollapses_Fails()
    {
        RoleFloorBaselineSnapshot previous = Snapshot(
            "2026-07-28",
            ("Fire Lord Azula", 100, Floors(("ramp", 7))));
        RoleFloorBaselineSnapshot candidate = Snapshot(
            "2026-07-29",
            ("Fire Lord Azula", 59, Floors(("ramp", 7))));

        RoleFloorDriftVerdict verdict = RoleFloorBaselineDriftCheck.Evaluate(previous, candidate, Thresholds());

        Assert.False(verdict.Passed);
        Assert.Equal("SampleCollapse", Assert.Single(verdict.Findings).Rule);
    }

    [Fact]
    public void Evaluate_PopulousCommanderSampleCollapses_WithinDropLimit_Passes()
    {
        RoleFloorBaselineSnapshot previous = Snapshot(
            "2026-07-28",
            ("Fire Lord Azula", 100, Floors(("ramp", 7))));
        RoleFloorBaselineSnapshot candidate = Snapshot(
            "2026-07-29",
            ("Fire Lord Azula", 60, Floors(("ramp", 7))));

        RoleFloorDriftVerdict verdict = RoleFloorBaselineDriftCheck.Evaluate(previous, candidate, Thresholds());

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_AdoptedPairsCollapse_Fails()
    {
        RoleFloorBaselineSnapshot previous = Snapshot(
            "2026-07-28",
            ("Commander A", 50, Floors(("ramp", 7), ("draw", 5))),
            ("Commander B", 9, Floors(("engines", 3), ("payoffs", 4), ("wincons", 2), ("interaction-targeted", 6))));
        RoleFloorBaselineSnapshot candidate = Snapshot(
            "2026-07-29",
            ("Commander A", 50, Floors(("ramp", 7), ("draw", 5))),
            ("Commander B", 50, Floors(("engines", 3))));

        RoleFloorDriftVerdict verdict = RoleFloorBaselineDriftCheck.Evaluate(previous, candidate, Thresholds());

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, finding => finding.Rule == "AdoptedPairCollapse");
    }

    [Fact]
    public void Evaluate_MoversAllMoveTheSameWay_Fails()
    {
        RoleFloorBaselineSnapshot previous = MoverSnapshot(10, 0);
        RoleFloorBaselineSnapshot candidate = MoverSnapshot(10, -1);

        RoleFloorDriftVerdict verdict = RoleFloorBaselineDriftCheck.Evaluate(previous, candidate, Thresholds());

        Assert.False(verdict.Passed);
        Assert.Equal("OneSidedDrift", Assert.Single(verdict.Findings).Rule);
    }

    [Fact]
    public void Evaluate_MoversBelowDirectionTestCount_Passes()
    {
        RoleFloorBaselineSnapshot previous = MoverSnapshot(9, 0);
        RoleFloorBaselineSnapshot candidate = MoverSnapshot(9, -1);

        RoleFloorDriftVerdict verdict = RoleFloorBaselineDriftCheck.Evaluate(previous, candidate, Thresholds());

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_IdenticalSnapshots_Passes()
    {
        RoleFloorBaselineSnapshot snapshot = Snapshot(
            "2026-07-29",
            ("Fire Lord Azula", 50, Floors(("ramp", 7), ("draw", 5))));

        RoleFloorDriftVerdict verdict = RoleFloorBaselineDriftCheck.Evaluate(snapshot, snapshot, Thresholds());

        Assert.True(verdict.Passed);
        Assert.Empty(verdict.Findings);
    }

    [Fact]
    public void FromJson_MissingThresholdField_Throws()
    {
        // Why: the guard cannot be disabled by a typo, so a missing threshold field is fatal.
        const string Json = """
            {
              "minEstablishedN": 10,
              "minPopulousN": 20,
              "maxSampleDropPct": 40,
              "moverThresholdFloors": 1,
              "minMoversForDirectionTest": 10,
              "maxOneSidedPct": 90
            }
            """;

        Assert.Throws<JsonException>(() => RoleFloorDriftThresholds.FromJson(Json));
    }

    private static RoleFloorDriftThresholds Thresholds()
    {
        return new RoleFloorDriftThresholds
        {
            MinEstablishedN = 10,
            MinPopulousN = 20,
            MaxSampleDropPct = 40,
            MoverThresholdFloors = 1,
            MinMoversForDirectionTest = 10,
            MaxOneSidedPct = 90,
            MaxAdoptedPairDropPct = 40,
        };
    }

    private static RoleFloorBaselineSnapshot Snapshot(
        string generated,
        params (string Name, int N, IReadOnlyDictionary<string, int> Floors)[] commanders)
    {
        return new RoleFloorBaselineSnapshot
        {
            Generated = generated,
            SampleSize = commanders.Length,
            AdoptedPairs = commanders.Sum(commander => commander.Floors.Count),
            Commanders = commanders.ToDictionary(
                commander => commander.Name,
                commander => new RoleFloorCommanderSnapshot
                {
                    N = commander.N,
                    Floors = commander.Floors,
                },
                StringComparer.Ordinal),
        };
    }

    private static RoleFloorBaselineSnapshot MoverSnapshot(int count, int delta)
    {
        return Snapshot(
            "2026-07-29",
            Enumerable.Range(0, count)
                .Select(index => ($"Commander {index}", 50, Floors(("ramp", 5 + delta))))
                .ToArray());
    }

    private static IReadOnlyDictionary<string, int> Floors(params (string RoleKey, int Floor)[] floors)
    {
        return floors.ToDictionary(
            floor => floor.RoleKey,
            floor => floor.Floor,
            StringComparer.Ordinal);
    }
}
