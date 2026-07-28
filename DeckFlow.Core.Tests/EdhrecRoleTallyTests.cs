using DeckFlow.Core.Research;

namespace DeckFlow.Core.Tests;

public sealed class EdhrecRoleTallyTests
{
    [Fact]
    public void TallyRoleCounts_AddsQuantityRatherThanOneForSingleRole()
    {
        IReadOnlyDictionary<string, int> counts = EdhrecRoleTally.TallyRoleCounts(
            ["lands", "ramp"],
            [(Roles("lands"), 9)]);

        Assert.Equal(9, counts["lands"]);
        Assert.Equal(0, counts["ramp"]);
    }

    [Fact]
    public void TallyRoleCounts_SumsMixedQuantitiesPerRole()
    {
        IReadOnlyDictionary<string, int> counts = EdhrecRoleTally.TallyRoleCounts(
            ["lands", "ramp", "draw"],
            [
                (Roles("lands"), 4),
                (Roles("lands", "ramp"), 3),
                (Roles("draw"), 2),
                (Roles("ramp"), 1),
            ]);

        Assert.Equal(7, counts["lands"]);
        Assert.Equal(4, counts["ramp"]);
        Assert.Equal(2, counts["draw"]);
    }

    [Fact]
    public void TallyRoleCounts_AddsQuantityToEachEmittedRole()
    {
        IReadOnlyDictionary<string, int> counts = EdhrecRoleTally.TallyRoleCounts(
            ["lands", "engines", "payoffs"],
            [(Roles("lands", "engines"), 5)]);

        Assert.Equal(5, counts["lands"]);
        Assert.Equal(5, counts["engines"]);
        Assert.Equal(0, counts["payoffs"]);
    }

    [Fact]
    public void TallyRoleCounts_KeepsZeroForTargetRolesThatNeverAppear()
    {
        IReadOnlyDictionary<string, int> counts = EdhrecRoleTally.TallyRoleCounts(
            ["lands", "ramp", "draw"],
            [(Roles("lands"), 2)]);

        Assert.Equal(0, counts["ramp"]);
        Assert.Equal(0, counts["draw"]);
    }

    [Fact]
    public void TallyRoleCounts_IgnoresRolesOutsideTargets()
    {
        IReadOnlyDictionary<string, int> counts = EdhrecRoleTally.TallyRoleCounts(
            ["lands", "ramp"],
            [(Roles("other", "lands"), 6)]);

        Assert.Equal(6, counts["lands"]);
        Assert.Equal(0, counts["ramp"]);
        Assert.False(counts.ContainsKey("other"));
    }

    [Fact]
    public void TallyRoleCounts_ReturnsAllZeroCountsForEmptySequence()
    {
        IReadOnlyDictionary<string, int> counts = EdhrecRoleTally.TallyRoleCounts(
            ["lands", "ramp", "draw"],
            Array.Empty<(IReadOnlyList<string> Roles, int Quantity)>());

        Assert.Equal(0, counts["lands"]);
        Assert.Equal(0, counts["ramp"]);
        Assert.Equal(0, counts["draw"]);
    }

    [Fact]
    public void TallyRoleCounts_LeavesCountsUnchangedForZeroQuantity()
    {
        IReadOnlyDictionary<string, int> counts = EdhrecRoleTally.TallyRoleCounts(
            ["lands", "ramp"],
            [(Roles("lands", "ramp"), 0)]);

        Assert.Equal(0, counts["lands"]);
        Assert.Equal(0, counts["ramp"]);
    }

    [Fact]
    public void TallyRoleCounts_AllowsNegativeQuantityAsArithmeticDelta()
    {
        IReadOnlyDictionary<string, int> counts = EdhrecRoleTally.TallyRoleCounts(
            ["lands", "ramp"],
            [
                (Roles("lands"), 5),
                (Roles("lands", "ramp"), -2),
            ]);

        Assert.Equal(3, counts["lands"]);
        Assert.Equal(-2, counts["ramp"]);
    }

    private static IReadOnlyList<string> Roles(params string[] roles) => roles;
}
