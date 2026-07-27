using DeckFlow.Core.Research;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="RoleFloorGuards"/>.
/// </summary>
public sealed class RoleFloorGuardsTests
{
    private static readonly string[] ShippedRoleKeys =
    [
        "lands",
        "ramp",
        "draw",
        "interaction-targeted",
        "interaction-mass",
        "protection",
        "engines",
        "payoffs",
        "wincons",
    ];

    [Fact]
    public void FindTaxonomyDrift_CleanInputs_ReturnsNull()
    {
        string? result = RoleFloorGuards.FindTaxonomyDrift(
            ShippedRoleKeys,
            ShippedRoleKeys,
            ShippedRoleKeys,
            residualRoleKey: "other");

        Assert.Null(result);
    }

    [Fact]
    public void FindTaxonomyDrift_MissingProtection_ReportsIt()
    {
        string? result = RoleFloorGuards.FindTaxonomyDrift(
            ShippedRoleKeys,
            ShippedRoleKeys.Where(role => !string.Equals(role, "protection", StringComparison.Ordinal)).ToArray(),
            ShippedRoleKeys,
            residualRoleKey: "other");

        Assert.NotNull(result);
        Assert.Contains("shipped keys missing from TargetRoles: protection", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FindTaxonomyDrift_MissingWincons_ReportsIt()
    {
        string? result = RoleFloorGuards.FindTaxonomyDrift(
            ShippedRoleKeys,
            ShippedRoleKeys.Where(role => !string.Equals(role, "wincons", StringComparison.Ordinal)).ToArray(),
            ShippedRoleKeys,
            residualRoleKey: "other");

        Assert.NotNull(result);
        Assert.Contains("shipped keys missing from TargetRoles: wincons", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FindTaxonomyDrift_StaleInteractionTarget_ReportsIt()
    {
        string? result = RoleFloorGuards.FindTaxonomyDrift(
            ShippedRoleKeys,
            ["interaction"],
            ShippedRoleKeys,
            residualRoleKey: "other");

        Assert.NotNull(result);
        Assert.Contains("TargetRoles entries not shipped by Cut Lab: interaction", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FindTaxonomyDrift_UnexpectedEmittedKey_ReportsIt()
    {
        string? result = RoleFloorGuards.FindTaxonomyDrift(
            ShippedRoleKeys,
            ShippedRoleKeys,
            ShippedRoleKeys.Concat(["surprise-role"]).ToArray(),
            residualRoleKey: "other");

        Assert.NotNull(result);
        Assert.Contains("probe-emitted keys outside TargetRoles and residual 'other': surprise-role", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FindTaxonomyDrift_UnprobedShippedKey_ReportsIt()
    {
        string? result = RoleFloorGuards.FindTaxonomyDrift(
            ShippedRoleKeys,
            ShippedRoleKeys,
            ShippedRoleKeys.Where(role => !string.Equals(role, "wincons", StringComparison.Ordinal)).ToArray(),
            residualRoleKey: "other");

        Assert.NotNull(result);
        Assert.Contains("shipped keys with no probe coverage: wincons", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FindTaxonomyDrift_ResidualOtherEmission_IsAllowed()
    {
        string? result = RoleFloorGuards.FindTaxonomyDrift(
            ShippedRoleKeys,
            ShippedRoleKeys,
            ShippedRoleKeys.Concat(["other"]).ToArray(),
            residualRoleKey: "other");

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void HasNoQualifyingCommanders_ReturnsExpectedValue(int qualifyingCommanderCount, bool expected)
    {
        Assert.Equal(expected, RoleFloorGuards.HasNoQualifyingCommanders(qualifyingCommanderCount));
    }
}
