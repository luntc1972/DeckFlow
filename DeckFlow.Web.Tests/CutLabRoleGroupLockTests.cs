using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for Cut Lab bulk role-group lock behavior and the shared land-type predicate.</summary>
public sealed class CutLabRoleGroupLockTests
{
    [Fact]
    public void BulkLockRoleGroup_Lands_LocksExactlyLandFrontFaceCards()
    {
        var state = new CutLabState
        {
            Commander = "Tatyova, Benthic Druid",
            Pool =
            [
                new CutLabPoolCard { Name = "Forest", Quantity = 1, TypeLine = "Basic Land — Forest", IsLocked = false },
                new CutLabPoolCard { Name = "Breeding Pool", Quantity = 1, TypeLine = "Land", IsLocked = false },
                new CutLabPoolCard { Name = "Jwari Disruption", Quantity = 1, TypeLine = "Instant // Land", IsLocked = false },
                new CutLabPoolCard { Name = "Llanowar Elves", Quantity = 1, TypeLine = "Creature — Elf Druid", IsLocked = false },
            ],
            Packages = [],
            Intent = new CutLabIntent { PrimaryPlan = "Ramp and draw" },
        };

        var result = CutLabLockRules.BulkLockRoleGroup(state, "lands");

        Assert.True(result.Pool.Single(card => card.Name == "Forest").IsLocked);
        Assert.True(result.Pool.Single(card => card.Name == "Breeding Pool").IsLocked);
        Assert.False(result.Pool.Single(card => card.Name == "Jwari Disruption").IsLocked);
        Assert.False(result.Pool.Single(card => card.Name == "Llanowar Elves").IsLocked);
    }

    [Fact]
    public void BulkLockRoleGroup_UnknownRoleGroup_IsNoOp()
    {
        var state = new CutLabState
        {
            Commander = "Tatyova, Benthic Druid",
            Pool = [new CutLabPoolCard { Name = "Forest", Quantity = 1, TypeLine = "Basic Land — Forest", IsLocked = false }],
            Packages = [],
            Intent = new CutLabIntent { PrimaryPlan = "Ramp and draw" },
        };

        var result = CutLabLockRules.BulkLockRoleGroup(state, "draw");

        Assert.Equal(state, result);
    }

    [Theory]
    [InlineData("Basic Land — Forest", true)]
    [InlineData("Land", true)]
    [InlineData("Instant // Land", false)]
    [InlineData("Creature — Elf", false)]
    public void IsLand_TypeLineRow_ReturnsExpectedResult(string typeLine, bool expected)
    {
        var result = CutLabLockRules.IsLand(typeLine);

        Assert.Equal(expected, result);
    }
}
