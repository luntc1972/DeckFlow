using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Manabase;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for Cut Lab role-floor default derivation, fallbacks, and user-override merge.</summary>
public sealed class CutLabFloorDefaultsTests
{
    [Fact]
    public void ResolveDefaults_BracketFourAndCommanderManaValueThree_UsesBracketRowBudgetAndStaticColumn()
    {
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 4, AvgLands = 36.4, DeckCount = 1000 }),
            cedhBaseline: null,
            roleFloorBaseline: null,
            priorFloors: []);

        Assert.Equal(CutLabFloorRules.RoleKeys, resolved.Select(floor => floor.Role).ToArray());
        AssertFloor(resolved[0], "lands", 36, false, 36, 36, null, 4, false);
        AssertFloor(resolved[1], "ramp", 10, false, 10, 10, null, 4, false);
        AssertFloor(resolved[2], "draw", 14, false, 14, 14, null, 4, false);
        AssertFloor(resolved[3], "interaction-targeted", 7, false, 7, 7, null, 4, false);
        AssertFloor(resolved[4], "interaction-mass", 3, false, 3, 3, null, 4, false);
        AssertFloor(resolved[5], "protection", 4, false, 4, 4, null, 4, false);
        AssertFloor(resolved[6], "engines", 6, false, 6, 6, null, 4, false);
        AssertFloor(resolved[7], "payoffs", 6, false, 6, 6, null, 4, false);
        AssertFloor(resolved[8], "wincons", 3, false, 3, 3, null, 4, false);
    }

    [Fact]
    public void ResolveBracket_BracketOneAndDerivedCedh_UseDocumentedFallbacks()
    {
        bool b1WasFallback;
        bool cedhWasFallback;

        var resolvedB1 = CutLabFloorDefaults.ResolveBracket(1, "Casual", out b1WasFallback);
        var resolvedCedh = CutLabFloorDefaults.ResolveBracket(null, "cEDH", out cedhWasFallback);

        Assert.Equal(2, resolvedB1);
        Assert.True(b1WasFallback);
        Assert.Equal(5, resolvedCedh);
        Assert.True(cedhWasFallback);
    }

    [Theory]
    [InlineData("Focused", 3)]
    [InlineData("Casual", 2)]
    [InlineData("Anything else", 2)]
    public void ResolveBracket_MissingBracket_MapsPlayExperienceToExpectedBracket(string playExperience, int expectedBracket)
    {
        var resolved = CutLabFloorDefaults.ResolveBracket(null, playExperience, out var wasFallback);

        Assert.Equal(expectedBracket, resolved);
        Assert.True(wasFallback);
    }

    [Fact]
    public void ResolveDefaults_BracketOne_FallsThroughToBracketTwoDefaults()
    {
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 1,
            playExperience: "Casual",
            commanderManaValue: 2.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 2, AvgLands = 35.6, DeckCount = 1000 }),
            cedhBaseline: null,
            roleFloorBaseline: null,
            priorFloors: []);

        Assert.All(resolved, floor => Assert.Equal(2, floor.ResolvedBracket));
        Assert.All(resolved, floor => Assert.True(floor.BracketWasFallback));
        AssertFloor(resolved[0], "lands", 36, false, 36, 36, null, 2, true);
        AssertFloor(resolved[3], "interaction-targeted", 4, false, 4, 4, null, 2, true);
        AssertFloor(resolved[4], "interaction-mass", 2, false, 2, 2, null, 2, true);
        AssertFloor(resolved[5], "protection", 2, false, 2, 2, null, 2, true);
        AssertFloor(resolved[6], "engines", 4, false, 4, 4, null, 2, true);
        AssertFloor(resolved[7], "payoffs", 4, false, 4, 4, null, 2, true);
        AssertFloor(resolved[8], "wincons", 2, false, 2, 2, null, 2, true);
    }

    [Fact]
    public void ResolveDefaults_BracketFive_UsesCedhCommanderBaselineWhenAvailable()
    {
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 5,
            playExperience: "cEDH",
            commanderManaValue: 3.0,
            commanderNames: ["Tymna the Weaver", "Kraum, Ludevic's Opus"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 5, AvgLands = 31.2, DeckCount = 1000 }),
            cedhBaseline: new FakeCedhBaselineProvider(mean: 29.6),
            roleFloorBaseline: null,
            priorFloors: []);

        AssertFloor(resolved[0], "lands", 30, false, 30, 30, null, 5, false);
    }

    [Fact]
    public void ResolveDefaults_BracketFiveWithoutCedhCommanderBaseline_FallsBackToBracketRow()
    {
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 5,
            playExperience: "cEDH",
            commanderManaValue: 3.0,
            commanderNames: ["Tymna the Weaver", "Kraum, Ludevic's Opus"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 5, AvgLands = 31.2, DeckCount = 1000 }),
            cedhBaseline: new FakeCedhBaselineProvider(),
            roleFloorBaseline: null,
            priorFloors: []);

        AssertFloor(resolved[0], "lands", 31, false, 31, 31, null, 5, false);
    }

    [Fact]
    public void ResolveDefaults_MissingBaselineRows_UsesFallbackLands()
    {
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 4.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: new FakeBaselineProvider(),
            cedhBaseline: null,
            roleFloorBaseline: null,
            priorFloors: []);

        AssertFloor(resolved[0], "lands", 36, false, 36, 36, null, 4, false);
    }

    [Fact]
    public void ResolveDefaults_UserSetPriorFloor_WinsOverFreshDefault()
    {
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 4, AvgLands = 36.4, DeckCount = 1000 }),
            cedhBaseline: null,
            roleFloorBaseline: null,
            priorFloors:
            [
                new CutLabRoleFloor
                {
                    Role = "interaction-targeted",
                    Floor = 15,
                    IsUserSet = true,
                },
                new CutLabRoleFloor
                {
                    Role = "draw",
                    Floor = 99,
                    IsUserSet = false,
                },
            ]);

        AssertFloor(resolved[3], "interaction-targeted", 15, true, 7, 7, null, 4, false);
        AssertFloor(resolved[2], "draw", 14, false, 14, 14, null, 4, false);
    }

    [Theory]
    [InlineData(2, 6)]
    [InlineData(3, 8)]
    [InlineData(4, 10)]
    [InlineData(5, 12)]
    public void ResolveDefaults_InteractionSplitPreservesMergedBudgetPerBracket(int bracket, int expectedMergedFloor)
    {
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: bracket,
            playExperience: bracket == 5 ? "cEDH" : "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = bracket, AvgLands = 36.4, DeckCount = 1000 }),
            cedhBaseline: null,
            roleFloorBaseline: null,
            priorFloors: []);

        int targeted = resolved.Single(floor => floor.Role == "interaction-targeted").DefaultValue;
        int mass = resolved.Single(floor => floor.Role == "interaction-mass").DefaultValue;

        Assert.Equal(expectedMergedFloor, targeted + mass);
    }

    [Fact]
    public void ResolveDefaults_CommanderFloorAboveBracket_RaisesTheDefault()
    {
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 4, AvgLands = 36.4, DeckCount = 1000 }),
            cedhBaseline: null,
            roleFloorBaseline: new FakeRoleFloorBaselineProvider(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["engines"] = 9,
            }),
            priorFloors: []);

        AssertFloor(
            resolved.Single(floor => floor.Role == "engines"),
            "engines",
            9,
            false,
            9,
            6,
            9,
            4,
            false);
    }

    [Fact]
    public void ResolveDefaults_CommanderFloorBelowBracket_KeepsBracketButStillReportsCommander()
    {
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 4, AvgLands = 36.4, DeckCount = 1000 }),
            cedhBaseline: null,
            roleFloorBaseline: new FakeRoleFloorBaselineProvider(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["payoffs"] = 2,
            }),
            priorFloors: []);

        // Why: RFLR-08 requires the commander number on screen at every bracket even when it loses the max.
        AssertFloor(
            resolved.Single(floor => floor.Role == "payoffs"),
            "payoffs",
            6,
            false,
            6,
            6,
            2,
            4,
            false);
    }

    [Fact]
    public void ResolveDefaults_CommanderFloorEqualsBracket_IsStable()
    {
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 4, AvgLands = 36.4, DeckCount = 1000 }),
            cedhBaseline: null,
            roleFloorBaseline: new FakeRoleFloorBaselineProvider(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["engines"] = 6,
            }),
            priorFloors: []);

        AssertFloor(
            resolved.Single(floor => floor.Role == "engines"),
            "engines",
            6,
            false,
            6,
            6,
            6,
            4,
            false);
    }

    [Fact]
    public void ResolveDefaults_NoCommanderMatch_IsIdenticalToBracketOnly()
    {
        var baseline = new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 4, AvgLands = 36.4, DeckCount = 1000 });
        var bracketOnly = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: baseline,
            cedhBaseline: null,
            roleFloorBaseline: null,
            priorFloors: []);
        var emptyProvider = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: baseline,
            cedhBaseline: null,
            roleFloorBaseline: new FakeRoleFloorBaselineProvider(),
            priorFloors: []);

        Assert.Equal(bracketOnly.Count, emptyProvider.Count);
        for (int i = 0; i < bracketOnly.Count; i++)
        {
            Assert.Equal(bracketOnly[i].Role, emptyProvider[i].Role);
            Assert.Equal(bracketOnly[i].Floor, emptyProvider[i].Floor);
            Assert.Equal(bracketOnly[i].DefaultValue, emptyProvider[i].DefaultValue);
            Assert.Equal(bracketOnly[i].BracketValue, emptyProvider[i].BracketValue);
            Assert.Null(bracketOnly[i].CommanderValue);
            Assert.Null(emptyProvider[i].CommanderValue);
        }
    }

    [Fact]
    public void ResolveDefaults_OutOfScopeRoles_AreNeverQueried()
    {
        var roleFloorBaseline = new FakeRoleFloorBaselineProvider(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["lands"] = 40,
            ["interaction-mass"] = 9,
            ["protection"] = 8,
        });
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 4, AvgLands = 36.4, DeckCount = 1000 }),
            cedhBaseline: null,
            roleFloorBaseline: roleFloorBaseline,
            priorFloors: []);

        Assert.DoesNotContain("lands", roleFloorBaseline.QueriedRoles, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("interaction-mass", roleFloorBaseline.QueriedRoles, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("protection", roleFloorBaseline.QueriedRoles, StringComparer.OrdinalIgnoreCase);

        foreach (string role in new[] { "lands", "interaction-mass", "protection" })
        {
            CutLabResolvedFloor row = resolved.Single(floor => floor.Role == role);
            Assert.Null(row.CommanderValue);
            Assert.Equal(row.BracketValue, row.Floor);
        }
    }

    [Fact]
    public void ResolveDefaults_RampAndDrawResolveIndependently_MaySumPastTwentyFour()
    {
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 4, AvgLands = 36.4, DeckCount = 1000 }),
            cedhBaseline: null,
            roleFloorBaseline: new FakeRoleFloorBaselineProvider(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["ramp"] = 15,
            }),
            priorFloors: []);

        CutLabResolvedFloor ramp = resolved.Single(floor => floor.Role == "ramp");
        CutLabResolvedFloor draw = resolved.Single(floor => floor.Role == "draw");

        Assert.Equal(15, ramp.Floor);
        Assert.Equal(14, draw.Floor);
        Assert.True(ramp.Floor + draw.Floor > 24);
    }

    [Fact]
    public void ResolveDefaults_UserOverride_StillWinsOverTheMax()
    {
        var resolved = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 4, AvgLands = 36.4, DeckCount = 1000 }),
            cedhBaseline: null,
            roleFloorBaseline: new FakeRoleFloorBaselineProvider(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["engines"] = 9,
            }),
            priorFloors:
            [
                new CutLabRoleFloor
                {
                    Role = "engines",
                    Floor = 8,
                    IsUserSet = true,
                },
            ]);

        AssertFloor(
            resolved.Single(floor => floor.Role == "engines"),
            "engines",
            8,
            true,
            9,
            6,
            9,
            4,
            false);
    }

    [Fact]
    public void ResolveDefaults_AllSixGoRoles_AreQueried()
    {
        var roleFloorBaseline = new FakeRoleFloorBaselineProvider();
        _ = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Atraxa, Praetors' Voice"],
            baseline: new FakeBaselineProvider(new ManabaseBracketBaseline { Bracket = 4, AvgLands = 36.4, DeckCount = 1000 }),
            cedhBaseline: null,
            roleFloorBaseline: roleFloorBaseline,
            priorFloors: []);

        Assert.Equal(
            ["ramp", "draw", "interaction-targeted", "engines", "payoffs", "wincons"],
            roleFloorBaseline.QueriedRoles);
    }

    private static void AssertFloor(
        CutLabResolvedFloor actual,
        string role,
        int floor,
        bool isUserSet,
        int defaultValue,
        int bracketValue,
        int? commanderValue,
        int resolvedBracket,
        bool bracketWasFallback)
    {
        Assert.Equal(role, actual.Role);
        Assert.Equal(floor, actual.Floor);
        Assert.Equal(isUserSet, actual.IsUserSet);
        Assert.Equal(defaultValue, actual.DefaultValue);
        Assert.Equal(bracketValue, actual.BracketValue);
        Assert.Equal(commanderValue, actual.CommanderValue);
        Assert.Equal(resolvedBracket, actual.ResolvedBracket);
        Assert.Equal(bracketWasFallback, actual.BracketWasFallback);
    }

    private sealed class FakeBaselineProvider(params ManabaseBracketBaseline[] rows) : IManabaseBaselineProvider
    {
        private readonly IReadOnlyDictionary<int, ManabaseBracketBaseline> _rows = rows.ToDictionary(row => row.Bracket);

        public void EnsureLoaded()
        {
        }

        public ManabaseBracketBaseline? TryGetBracketBaseline(int bracket)
            => _rows.TryGetValue(bracket, out var row) ? row : null;

        public ManabaseCommanderBaseline? TryGetCommanderBaseline(IReadOnlyList<string> commanderNames)
            => null;
    }

    private sealed class FakeCedhBaselineProvider(double? mean = null) : ICedhLandBaselineProvider
    {
        public void EnsureLoaded()
        {
        }

        public bool TryGetBaseline(IReadOnlyList<string> commanderNames, out double resolvedMean, out int n, out double sd, out string? generated)
        {
            if (mean is double baselineMean)
            {
                resolvedMean = baselineMean;
                n = 500;
                sd = 1.2;
                generated = "2026-07-19";
                return true;
            }

            resolvedMean = 0;
            n = 0;
            sd = 0;
            generated = null;
            return false;
        }
    }

    private sealed class FakeRoleFloorBaselineProvider(IReadOnlyDictionary<string, int>? floorsByRole = null) : IRoleFloorBaselineProvider
    {
        private readonly IReadOnlyDictionary<string, int> _floorsByRole =
            floorsByRole ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _queriedRoles = [];

        internal IReadOnlyList<string> QueriedRoles => _queriedRoles;

        public void EnsureLoaded()
        {
        }

        public bool TryGetRoleFloor(IReadOnlyList<string> commanderNames, string role, out int floor)
        {
            _queriedRoles.Add(role);
            return _floorsByRole.TryGetValue(role, out floor);
        }
    }
}
