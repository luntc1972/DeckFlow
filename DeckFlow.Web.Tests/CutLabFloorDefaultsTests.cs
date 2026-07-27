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
            priorFloors: []);

        Assert.Equal(CutLabFloorRules.RoleKeys, resolved.Select(floor => floor.Role).ToArray());
        AssertFloor(resolved[0], "lands", 36, false, 36, 4, false);
        AssertFloor(resolved[1], "ramp", 10, false, 10, 4, false);
        AssertFloor(resolved[2], "draw", 14, false, 14, 4, false);
        AssertFloor(resolved[3], "interaction-targeted", 7, false, 7, 4, false);
        AssertFloor(resolved[4], "interaction-mass", 3, false, 3, 4, false);
        AssertFloor(resolved[5], "protection", 4, false, 4, 4, false);
        AssertFloor(resolved[6], "engines", 6, false, 6, 4, false);
        AssertFloor(resolved[7], "payoffs", 6, false, 6, 4, false);
        AssertFloor(resolved[8], "wincons", 3, false, 3, 4, false);
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
            priorFloors: []);

        Assert.All(resolved, floor => Assert.Equal(2, floor.ResolvedBracket));
        Assert.All(resolved, floor => Assert.True(floor.BracketWasFallback));
        AssertFloor(resolved[0], "lands", 36, false, 36, 2, true);
        AssertFloor(resolved[3], "interaction-targeted", 4, false, 4, 2, true);
        AssertFloor(resolved[4], "interaction-mass", 2, false, 2, 2, true);
        AssertFloor(resolved[5], "protection", 2, false, 2, 2, true);
        AssertFloor(resolved[6], "engines", 4, false, 4, 2, true);
        AssertFloor(resolved[7], "payoffs", 4, false, 4, 2, true);
        AssertFloor(resolved[8], "wincons", 2, false, 2, 2, true);
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
            priorFloors: []);

        AssertFloor(resolved[0], "lands", 30, false, 30, 5, false);
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
            priorFloors: []);

        AssertFloor(resolved[0], "lands", 31, false, 31, 5, false);
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
            priorFloors: []);

        AssertFloor(resolved[0], "lands", 36, false, 36, 4, false);
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

        AssertFloor(resolved[3], "interaction-targeted", 15, true, 7, 4, false);
        AssertFloor(resolved[2], "draw", 14, false, 14, 4, false);
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
            priorFloors: []);

        int targeted = resolved.Single(floor => floor.Role == "interaction-targeted").DefaultValue;
        int mass = resolved.Single(floor => floor.Role == "interaction-mass").DefaultValue;

        Assert.Equal(expectedMergedFloor, targeted + mass);
    }

    private static void AssertFloor(
        CutLabResolvedFloor actual,
        string role,
        int floor,
        bool isUserSet,
        int defaultValue,
        int resolvedBracket,
        bool bracketWasFallback)
    {
        Assert.Equal(role, actual.Role);
        Assert.Equal(floor, actual.Floor);
        Assert.Equal(isUserSet, actual.IsUserSet);
        Assert.Equal(defaultValue, actual.DefaultValue);
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
}
