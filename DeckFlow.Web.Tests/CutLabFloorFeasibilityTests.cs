using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for aggregate Cut Lab floor feasibility detection and overlap correction.</summary>
public sealed class CutLabFloorFeasibilityTests
{
    [Fact]
    public void Evaluate_ShippedDefaultFloors_ReturnsNull()
    {
        // Why: today's bracket-4 shipped defaults must not fire the advisory.
        // Capacity: 100 - 1 - 36 = 63.
        // Required: 10 + max(14, 6) + 7 + 3 + 4 + 6 = 44.
        // 44 <= 63, so the result must stay null.
        CutLabFloorFeasibilityResult? result = CutLabFloorFeasibility.Evaluate(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 10, 10, 10),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 7, 7, 7),
            CreateResolvedFloor("interaction-mass", 3, 3, 3),
            CreateResolvedFloor("protection", 4, 4, 4),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 6, 6, 6),
            CreateResolvedFloor("wincons", 3, 3, 3),
        ]);

        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_NaiveSumWouldFireButCorrectedSumDoesNot_ReturnsNull()
    {
        // Why: this is the exact over-firing D-06a exists to prevent.
        CutLabFloorFeasibilityResult? result = CutLabFloorFeasibility.Evaluate(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 10, 10, 10),
            CreateResolvedFloor("draw", 20, 20, 20),
            CreateResolvedFloor("interaction-targeted", 10, 10, 10),
            CreateResolvedFloor("interaction-mass", 5, 5, 5),
            CreateResolvedFloor("protection", 5, 5, 5),
            CreateResolvedFloor("engines", 20, 20, 20),
            CreateResolvedFloor("payoffs", 10, 10, 10),
            CreateResolvedFloor("wincons", 5, 5, 5),
        ]);

        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_CorrectedSumExceedsCapacity_ReturnsResult()
    {
        // Capacity: 100 - 1 - 36 = 63.
        // Required: 12 + max(14, 6) + 12 + 8 + 7 + 15 = 68.
        CutLabFloorFeasibilityResult result = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 12, 12, 12),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 12, 12, 12),
            CreateResolvedFloor("interaction-mass", 8, 8, 8),
            CreateResolvedFloor("protection", 7, 7, 7),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 15, 15, 15),
            CreateResolvedFloor("wincons", 20, 20, 20),
        ]);

        Assert.Equal(68, result.RequiredNonlandSlots);
        Assert.Equal(63, result.AvailableNonlandSlots);
        Assert.Equal(5, result.Deficit);
        Assert.Equal(result.RequiredNonlandSlots - result.AvailableNonlandSlots, result.Deficit);
    }

    [Fact]
    public void Evaluate_EnginesAndDrawCollapse_CountsOnlyTheLarger()
    {
        CutLabFloorFeasibilityResult drawDominantBaseline = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 10, 10, 10),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 7, 7, 7),
            CreateResolvedFloor("interaction-mass", 3, 3, 3),
            CreateResolvedFloor("protection", 4, 4, 4),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 30, 30, 30),
            CreateResolvedFloor("wincons", 3, 3, 3),
        ]);
        CutLabFloorFeasibilityResult drawDominantChangedSmaller = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 10, 10, 10),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 7, 7, 7),
            CreateResolvedFloor("interaction-mass", 3, 3, 3),
            CreateResolvedFloor("protection", 4, 4, 4),
            CreateResolvedFloor("engines", 1, 1, 1),
            CreateResolvedFloor("payoffs", 30, 30, 30),
            CreateResolvedFloor("wincons", 3, 3, 3),
        ]);
        CutLabFloorFeasibilityResult enginesDominantBaseline = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 10, 10, 10),
            CreateResolvedFloor("draw", 6, 6, 6),
            CreateResolvedFloor("interaction-targeted", 7, 7, 7),
            CreateResolvedFloor("interaction-mass", 3, 3, 3),
            CreateResolvedFloor("protection", 4, 4, 4),
            CreateResolvedFloor("engines", 14, 14, 14),
            CreateResolvedFloor("payoffs", 30, 30, 30),
            CreateResolvedFloor("wincons", 3, 3, 3),
        ]);
        CutLabFloorFeasibilityResult enginesDominantChangedSmaller = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 10, 10, 10),
            CreateResolvedFloor("draw", 1, 1, 1),
            CreateResolvedFloor("interaction-targeted", 7, 7, 7),
            CreateResolvedFloor("interaction-mass", 3, 3, 3),
            CreateResolvedFloor("protection", 4, 4, 4),
            CreateResolvedFloor("engines", 14, 14, 14),
            CreateResolvedFloor("payoffs", 30, 30, 30),
            CreateResolvedFloor("wincons", 3, 3, 3),
        ]);

        Assert.Equal(drawDominantBaseline.RequiredNonlandSlots, drawDominantChangedSmaller.RequiredNonlandSlots);
        Assert.Equal(68, drawDominantBaseline.RequiredNonlandSlots);
        Assert.Equal(enginesDominantBaseline.RequiredNonlandSlots, enginesDominantChangedSmaller.RequiredNonlandSlots);
        Assert.Equal(68, enginesDominantBaseline.RequiredNonlandSlots);
    }

    [Fact]
    public void Evaluate_WinconsFloor_DoesNotConsumeSlots()
    {
        CutLabFloorFeasibilityResult baseline = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 12, 12, 12),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 12, 12, 12),
            CreateResolvedFloor("interaction-mass", 8, 8, 8),
            CreateResolvedFloor("protection", 7, 7, 7),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 15, 15, 15),
            CreateResolvedFloor("wincons", 3, 3, 3),
        ]);
        CutLabFloorFeasibilityResult raisedWincons = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 12, 12, 12),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 12, 12, 12),
            CreateResolvedFloor("interaction-mass", 8, 8, 8),
            CreateResolvedFloor("protection", 7, 7, 7),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 15, 15, 15),
            CreateResolvedFloor("wincons", 23, 23, 23),
        ]);

        Assert.Equal(baseline.RequiredNonlandSlots, raisedWincons.RequiredNonlandSlots);
    }

    [Fact]
    public void Evaluate_PayoffsFloor_ConsumesSlotsOneForOne()
    {
        // Why: D-06a authorizes no payoffs correction, and max() raises payoffs harder than any
        // other role, so a payoffs-driven infeasibility must still fire.
        CutLabFloorFeasibilityResult lowerPayoffs = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 12, 12, 12),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 12, 12, 12),
            CreateResolvedFloor("interaction-mass", 8, 8, 8),
            CreateResolvedFloor("protection", 7, 7, 7),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 15, 15, 15),
            CreateResolvedFloor("wincons", 3, 3, 3),
        ]);
        CutLabFloorFeasibilityResult raisedPayoffs = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 12, 12, 12),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 12, 12, 12),
            CreateResolvedFloor("interaction-mass", 8, 8, 8),
            CreateResolvedFloor("protection", 7, 7, 7),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 35, 35, 35),
            CreateResolvedFloor("wincons", 3, 3, 3),
        ]);
        CutLabFloorFeasibilityResult payoffsOnlyOverflow = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("payoffs", 70, 70, 70),
        ]);

        Assert.Equal(lowerPayoffs.RequiredNonlandSlots + 20, raisedPayoffs.RequiredNonlandSlots);
        Assert.Equal(70, payoffsOnlyOverflow.RequiredNonlandSlots);
    }

    [Fact]
    public void Evaluate_LandsFloorReducesCapacity()
    {
        CutLabFloorFeasibilityResult baseline = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("payoffs", 70, 70, 70),
        ]);
        CutLabFloorFeasibilityResult raisedLands = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 40, 40, 40),
            CreateResolvedFloor("payoffs", 70, 70, 70),
        ]);

        Assert.Equal(baseline.AvailableNonlandSlots - 4, raisedLands.AvailableNonlandSlots);
    }

    [Fact]
    public void Evaluate_ExactlyAtCapacity_ReturnsNull()
    {
        CutLabFloorFeasibilityResult? result = CutLabFloorFeasibility.Evaluate(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 10, 10, 10),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 10, 10, 10),
            CreateResolvedFloor("interaction-mass", 8, 8, 8),
            CreateResolvedFloor("protection", 7, 7, 7),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 14, 14, 14),
            CreateResolvedFloor("wincons", 20, 20, 20),
        ]);

        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_OneOverCapacity_ReturnsResult()
    {
        CutLabFloorFeasibilityResult result = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 10, 10, 10),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 10, 10, 10),
            CreateResolvedFloor("interaction-mass", 8, 8, 8),
            CreateResolvedFloor("protection", 7, 7, 7),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 15, 15, 15),
            CreateResolvedFloor("wincons", 20, 20, 20),
        ]);

        Assert.Equal(64, result.RequiredNonlandSlots);
        Assert.Equal(63, result.AvailableNonlandSlots);
        Assert.Equal(1, result.Deficit);
    }

    [Fact]
    public void Evaluate_RelaxCandidates_AreOrderedByCommanderRaise()
    {
        CutLabFloorFeasibilityResult result = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 20, 20, 16, commanderValue: 20),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 18, 18, 15, commanderValue: 18),
            CreateResolvedFloor("interaction-mass", 8, 8, 8),
            CreateResolvedFloor("protection", 9, 9, 7, commanderValue: 9),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 25, 25, 20, commanderValue: 25),
            CreateResolvedFloor("wincons", 30, 30, 24, commanderValue: 30),
        ]);

        Assert.Equal(3, result.RelaxCandidates.Count);
        Assert.Collection(
            result.RelaxCandidates,
            candidate =>
            {
                Assert.Equal("payoffs", candidate.RoleKey);
                Assert.Equal(5, candidate.CommanderRaise);
            },
            candidate =>
            {
                Assert.Equal("ramp", candidate.RoleKey);
                Assert.Equal(4, candidate.CommanderRaise);
            },
            candidate =>
            {
                Assert.Equal("interaction-targeted", candidate.RoleKey);
                Assert.Equal(3, candidate.CommanderRaise);
            });
    }

    [Fact]
    public void Evaluate_RelaxCandidates_ExcludeWinconsOnly()
    {
        CutLabFloorFeasibilityResult result = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 18, 18, 18),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 9, 9, 9),
            CreateResolvedFloor("interaction-mass", 6, 6, 6),
            CreateResolvedFloor("protection", 6, 6, 6),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 24, 24, 14, commanderValue: 24),
            CreateResolvedFloor("wincons", 80, 80, 20, commanderValue: 80),
        ]);

        Assert.DoesNotContain(result.RelaxCandidates, candidate => candidate.RoleKey == "wincons");
        Assert.Equal("payoffs", result.RelaxCandidates[0].RoleKey);
    }

    [Fact]
    public void Evaluate_UsesEffectiveFloorNotDefault()
    {
        CutLabFloorFeasibilityResult? relievedByOverride = CutLabFloorFeasibility.Evaluate(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 10, 10, 10),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 10, 10, 10),
            CreateResolvedFloor("interaction-mass", 8, 8, 8),
            CreateResolvedFloor("protection", 7, 7, 7),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 14, 30, 14, isUserSet: true),
            CreateResolvedFloor("wincons", 20, 20, 20),
        ]);
        CutLabFloorFeasibilityResult unresolvedDefault = EvaluateNonNull(
        [
            CreateResolvedFloor("lands", 36, 36, 36),
            CreateResolvedFloor("ramp", 10, 10, 10),
            CreateResolvedFloor("draw", 14, 14, 14),
            CreateResolvedFloor("interaction-targeted", 10, 10, 10),
            CreateResolvedFloor("interaction-mass", 8, 8, 8),
            CreateResolvedFloor("protection", 7, 7, 7),
            CreateResolvedFloor("engines", 6, 6, 6),
            CreateResolvedFloor("payoffs", 30, 30, 30),
            CreateResolvedFloor("wincons", 20, 20, 20),
        ]);

        Assert.Null(relievedByOverride);
        Assert.Equal(16, unresolvedDefault.Deficit);
    }

    private static CutLabResolvedFloor CreateResolvedFloor(
        string role,
        int floor,
        int defaultValue,
        int bracketValue,
        int? commanderValue = null,
        bool isUserSet = false)
        => new()
        {
            Role = role,
            Floor = floor,
            IsUserSet = isUserSet,
            DefaultValue = defaultValue,
            BracketValue = bracketValue,
            CommanderValue = commanderValue,
            ResolvedBracket = 4,
            BracketWasFallback = false,
        };

    private static CutLabFloorFeasibilityResult EvaluateNonNull(IReadOnlyList<CutLabResolvedFloor> resolvedFloors)
    {
        CutLabFloorFeasibilityResult? result = CutLabFloorFeasibility.Evaluate(resolvedFloors);
        Assert.NotNull(result);
        return result;
    }
}
