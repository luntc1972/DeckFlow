using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for Cut Lab role-floor clamping and floor-break evaluation rules.</summary>
public sealed class CutLabFloorRulesTests
{
    [Fact]
    public void ClampFloors_NegativeAndAbsurdFloors_ClampsIntoSupportedRange()
    {
        var state = CreateState(
            new CutLabRoleFloor { Role = "wincons", Floor = -5, IsUserSet = true },
            new CutLabRoleFloor { Role = "ramp", Floor = 1_000_000_000, IsUserSet = true });

        var result = CutLabFloorRules.ClampFloors(state);

        Assert.Equal(0, result.RoleFloors.Single(floor => floor.Role == "wincons").Floor);
        Assert.Equal(151, result.RoleFloors.Single(floor => floor.Role == "ramp").Floor);
    }

    [Fact]
    public void ClampFloors_UnknownRoleKey_DropsEntry()
    {
        var state = CreateState(
            new CutLabRoleFloor { Role = "battlecruiser", Floor = 4, IsUserSet = true },
            new CutLabRoleFloor { Role = "draw", Floor = 9, IsUserSet = true });

        var result = CutLabFloorRules.ClampFloors(state);

        Assert.DoesNotContain(result.RoleFloors, floor => floor.Role == "battlecruiser");
        Assert.Equal("draw", Assert.Single(result.RoleFloors).Role);
    }

    [Fact]
    public void ClampFloors_DuplicateRoleKeys_KeepsFirstOccurrence()
    {
        var state = CreateState(
            new CutLabRoleFloor { Role = "Interaction-Targeted", Floor = 7, IsUserSet = true },
            new CutLabRoleFloor { Role = "interaction-targeted", Floor = 12, IsUserSet = false });

        var result = CutLabFloorRules.ClampFloors(state);

        var floor = Assert.Single(result.RoleFloors);
        Assert.Equal("interaction-targeted", floor.Role);
        Assert.Equal(7, floor.Floor);
        Assert.True(floor.IsUserSet);
    }

    [Fact]
    public void ClampFloors_ValidFloors_ReturnsEqualState()
    {
        var state = CreateState(
            new CutLabRoleFloor { Role = "lands", Floor = 36, IsUserSet = false },
            new CutLabRoleFloor { Role = "interaction-targeted", Floor = 9, IsUserSet = true });

        var result = CutLabFloorRules.ClampFloors(state);

        Assert.Equal(state, result);
    }

    [Fact]
    public void Evaluate_CutBreaksSingleFloor_ReturnsExactWarningMessage()
    {
        var warnings = CutLabFloorRules.Evaluate(
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["interaction-targeted"] = 7,
            },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["interaction-targeted"] = 7,
            },
            ["interaction-targeted"],
            "Swords to Plowshares");

        var warning = Assert.Single(warnings);
        Assert.Equal("interaction-targeted", warning.Role);
        Assert.Equal(6, warning.NewCount);
        Assert.Equal(7, warning.Floor);
        Assert.Equal("Cutting Swords to Plowshares drops interaction-targeted to 6, below your floor of 7.", warning.Message);
    }

    [Fact]
    public void Evaluate_CutStaysAboveFloors_ReturnsNoWarnings()
    {
        var warnings = CutLabFloorRules.Evaluate(
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["ramp"] = 10,
            },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["ramp"] = 7,
            },
            ["ramp"],
            "Cultivate");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Evaluate_MultiRoleCutBreakingTwoFloors_ReturnsOneWarningPerRole()
    {
        var warnings = CutLabFloorRules.Evaluate(
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["interaction-targeted"] = 4,
                ["protection"] = 3,
            },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["interaction-targeted"] = 4,
                ["protection"] = 3,
            },
            ["interaction-targeted", "protection"],
            "Teferi's Protection");

        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, warning => warning.Role == "interaction-targeted" && warning.NewCount == 3 && warning.Floor == 4);
        Assert.Contains(warnings, warning => warning.Role == "protection" && warning.NewCount == 2 && warning.Floor == 3);
    }

    [Fact]
    public void ClampFloors_LegacyUserSetInteraction_MigratesUsingResolvedBracketRatio()
    {
        var state = CreateState(new CutLabRoleFloor { Role = "interaction", Floor = 14, IsUserSet = true });

        var result = CutLabFloorRules.ClampFloors(state);

        CutLabRoleFloor targeted = Assert.Single(result.RoleFloors, floor => floor.Role == "interaction-targeted");
        CutLabRoleFloor mass = Assert.Single(result.RoleFloors, floor => floor.Role == "interaction-mass");
        Assert.Equal(14, targeted.Floor + mass.Floor);
        Assert.Equal(10, targeted.Floor);
        Assert.Equal(4, mass.Floor);
        Assert.All(result.RoleFloors, floor => Assert.True(floor.IsUserSet));
    }

    [Fact]
    public void ClampFloors_WithoutBracketOverride_UsesPersistedIntentBracket()
    {
        var state = CreateState(new CutLabRoleFloor { Role = "interaction", Floor = 15, IsUserSet = true }) with
        {
            Intent = new CutLabIntent
            {
                Bracket = 2,
                PlayExperience = "Focused",
            },
        };

        var result = CutLabFloorRules.ClampFloors(state);

        CutLabRoleFloor targeted = Assert.Single(result.RoleFloors, floor => floor.Role == "interaction-targeted");
        CutLabRoleFloor mass = Assert.Single(result.RoleFloors, floor => floor.Role == "interaction-mass");
        Assert.Equal(10, targeted.Floor);
        Assert.Equal(5, mass.Floor);
    }

    [Fact]
    public void ClampFloors_BracketOverride_UsesOverrideInsteadOfPersistedIntentBracket()
    {
        var state = CreateState(new CutLabRoleFloor { Role = "interaction", Floor = 15, IsUserSet = true }) with
        {
            Intent = new CutLabIntent
            {
                Bracket = 2,
                PlayExperience = "Focused",
            },
        };

        var result = CutLabFloorRules.ClampFloors(state, bracketOverride: 4);

        CutLabRoleFloor targeted = Assert.Single(result.RoleFloors, floor => floor.Role == "interaction-targeted");
        CutLabRoleFloor mass = Assert.Single(result.RoleFloors, floor => floor.Role == "interaction-mass");
        Assert.Equal(11, targeted.Floor);
        Assert.Equal(4, mass.Floor);
    }

    [Fact]
    public void ClampFloors_LegacyNonUserSetInteraction_DropsWithoutMigrating()
    {
        var state = CreateState(new CutLabRoleFloor { Role = "interaction", Floor = 14, IsUserSet = false });

        var result = CutLabFloorRules.ClampFloors(state);

        Assert.Empty(result.RoleFloors);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClampFloors_LegacyAndUserSetNewKey_ExplicitNewKeyWinsInEitherOrder(bool legacyFirst)
    {
        CutLabRoleFloor legacy = new() { Role = "interaction", Floor = 14, IsUserSet = true };
        CutLabRoleFloor targeted = new() { Role = "interaction-targeted", Floor = 8, IsUserSet = true };
        var state = legacyFirst ? CreateState(legacy, targeted) : CreateState(targeted, legacy);

        var result = CutLabFloorRules.ClampFloors(state);

        var floor = Assert.Single(result.RoleFloors);
        Assert.Equal("interaction-targeted", floor.Role);
        Assert.Equal(8, floor.Floor);
        Assert.True(floor.IsUserSet);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClampFloors_LegacyAndNonUserSetNewKeys_LegacyMigratesAndWinsInEitherOrder(bool legacyFirst)
    {
        CutLabRoleFloor legacy = new() { Role = "interaction", Floor = 14, IsUserSet = true };
        CutLabRoleFloor staleTargeted = new() { Role = "interaction-targeted", Floor = 2, IsUserSet = false };
        CutLabRoleFloor staleMass = new() { Role = "interaction-mass", Floor = 1, IsUserSet = false };
        var state = legacyFirst
            ? CreateState(legacy, staleTargeted, staleMass)
            : CreateState(staleTargeted, staleMass, legacy);

        var result = CutLabFloorRules.ClampFloors(state);

        CutLabRoleFloor targeted = Assert.Single(result.RoleFloors, floor => floor.Role == "interaction-targeted");
        CutLabRoleFloor mass = Assert.Single(result.RoleFloors, floor => floor.Role == "interaction-mass");
        Assert.Equal(14, targeted.Floor + mass.Floor);
        Assert.Equal(10, targeted.Floor);
        Assert.Equal(4, mass.Floor);
        Assert.All(result.RoleFloors, floor => Assert.True(floor.IsUserSet));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ClampFloors_BracketOverride_PreservesLegacyFloorSumAcrossSupportedBrackets(int bracketOverride)
    {
        const int legacyFloor = 15;
        var state = CreateState(new CutLabRoleFloor { Role = "interaction", Floor = legacyFloor, IsUserSet = true }) with
        {
            Intent = new CutLabIntent
            {
                PlayExperience = "Focused",
            },
        };

        var result = CutLabFloorRules.ClampFloors(state, bracketOverride);

        CutLabRoleFloor targeted = Assert.Single(result.RoleFloors, floor => floor.Role == "interaction-targeted");
        CutLabRoleFloor mass = Assert.Single(result.RoleFloors, floor => floor.Role == "interaction-mass");
        Assert.Equal(legacyFloor, targeted.Floor + mass.Floor);
    }

    [Fact]
    public void ClampFloors_AlreadyMigratedState_IsIdempotent()
    {
        var state = CreateState(
            new CutLabRoleFloor { Role = "interaction-targeted", Floor = 10, IsUserSet = true },
            new CutLabRoleFloor { Role = "interaction-mass", Floor = 4, IsUserSet = true });

        var once = CutLabFloorRules.ClampFloors(state);
        var twice = CutLabFloorRules.ClampFloors(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void Evaluate_RoleWithoutFloor_IgnoresThatRole()
    {
        var warnings = CutLabFloorRules.Evaluate(
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["engines"] = 5,
                ["draw"] = 6,
            },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["engines"] = 4,
            },
            ["engines", "draw"],
            "Mystic Remora");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Evaluate_QuantityCut_UsesProvidedQuantityAndClampsAtZero()
    {
        var warnings = CutLabFloorRules.Evaluate(
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["lands"] = 36,
            },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["lands"] = 36,
            },
            ["lands"],
            "Plains",
            quantity: 36);

        var warning = Assert.Single(warnings);
        Assert.Equal("lands", warning.Role);
        Assert.Equal(0, warning.NewCount);
        Assert.Equal(36, warning.Floor);
        Assert.Equal("Cutting Plains drops lands to 0, below your floor of 36.", warning.Message);
    }

    private static CutLabState CreateState(params CutLabRoleFloor[] roleFloors)
        => new()
        {
            Commander = "Atraxa, Praetors' Voice",
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = "Atraxa, Praetors' Voice",
                    Quantity = 1,
                    TypeLine = "Legendary Creature — Phyrexian Angel Horror",
                    IsCommander = true,
                    IsLocked = true,
                },
            ],
            Intent = new CutLabIntent { PrimaryPlan = "Counters", Bracket = 4, PlayExperience = "Focused" },
            RoleFloors = roleFloors,
        };
}
