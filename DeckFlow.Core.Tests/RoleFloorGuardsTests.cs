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

    private sealed class StubAssignerWithPrivateStaticRoleKeys
    {
        private static readonly string[] RoleKeys = ["lands", "draw"];

        public static string[] ExpectedRoleKeys => RoleKeys;
    }

    private sealed class StubAssignerWithoutRoleKeys
    {
    }

    private sealed class StubAssignerWithWrongRoleKeyType
    {
        private static readonly int[] RoleKeys = [1, 2, 3];
    }

    private sealed class StubAssignerWithNullRoleKeys
    {
#pragma warning disable CS8618
#pragma warning disable CS0414
        private static readonly string[] RoleKeys = null!;
#pragma warning restore CS0414
#pragma warning restore CS8618
    }

    [Fact]
    public void TryReadShippedRoleKeys_PrivateStaticStringArray_ReturnsKeys()
    {
        string? result = RoleFloorGuards.TryReadShippedRoleKeys(
            typeof(StubAssignerWithPrivateStaticRoleKeys),
            "RoleKeys",
            out string[]? shippedRoleKeys);

        Assert.Null(result);
        Assert.Equal(StubAssignerWithPrivateStaticRoleKeys.ExpectedRoleKeys, shippedRoleKeys);
    }

    [Fact]
    public void TryReadShippedRoleKeys_MissingField_ReportsTypeAndField()
    {
        string? result = RoleFloorGuards.TryReadShippedRoleKeys(
            typeof(StubAssignerWithoutRoleKeys),
            "RoleKeys",
            out string[]? shippedRoleKeys);

        Assert.NotNull(result);
        Assert.Null(shippedRoleKeys);
        Assert.Contains("StubAssignerWithoutRoleKeys.RoleKeys", result, StringComparison.Ordinal);
        Assert.Contains("expected a static string[] field named RoleKeys", result, StringComparison.Ordinal);
    }

    [Fact]
    public void TryReadShippedRoleKeys_WrongFieldType_ReportsTypeAndField()
    {
        string? result = RoleFloorGuards.TryReadShippedRoleKeys(
            typeof(StubAssignerWithWrongRoleKeyType),
            "RoleKeys",
            out string[]? shippedRoleKeys);

        Assert.NotNull(result);
        Assert.Null(shippedRoleKeys);
        Assert.Contains("StubAssignerWithWrongRoleKeyType.RoleKeys", result, StringComparison.Ordinal);
        Assert.Contains("to be a string[] but was System.Int32[]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void TryReadShippedRoleKeys_NullFieldValue_ReportsTypeAndField()
    {
        string? result = RoleFloorGuards.TryReadShippedRoleKeys(
            typeof(StubAssignerWithNullRoleKeys),
            "RoleKeys",
            out string[]? shippedRoleKeys);

        Assert.NotNull(result);
        Assert.Null(shippedRoleKeys);
        Assert.Contains("StubAssignerWithNullRoleKeys.RoleKeys", result, StringComparison.Ordinal);
        Assert.Contains("to hold a non-null string[]", result, StringComparison.Ordinal);
    }

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
