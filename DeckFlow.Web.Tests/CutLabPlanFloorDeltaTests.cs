using DeckFlow.Core.Analysis;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for strategy-plan role-floor deltas.</summary>
public sealed class CutLabPlanFloorDeltaTests
{
    public static IEnumerable<object[]> StrategyCases() => DeckPlanStrategyCatalog.Entries.Select(entry => new object[] { entry.Slug, ExpectedDeltas(entry.Slug) });

    [Fact]
    public void ResolvePlanDeltas_NullProfile_AllRolesZero()
    {
        Assert.All(CutLabFloorDefaults.ResolvePlanDeltas(null).Values, delta => Assert.Equal(0, delta));
    }

    [Fact]
    public void ResolvePlanDeltas_EmptyProfile_AllRolesZero()
    {
        Assert.All(CutLabFloorDefaults.ResolvePlanDeltas(new CutLabPlanProfile()).Values, delta => Assert.Equal(0, delta));
    }

    [Theory]
    [MemberData(nameof(StrategyCases))]
    public void ResolvePlanDeltas_SingleStrategy_RaisesOnlyItsNamedRoles(string slug, IReadOnlyDictionary<string, int> expectedDeltas)
    {
        var deltas = CutLabFloorDefaults.ResolvePlanDeltas(new CutLabPlanProfile { GenericStrategies = [slug] });

        Assert.All(expectedDeltas, expected => Assert.Equal(expected.Value, deltas[expected.Key]));
        Assert.All(
            CutLabFloorRules.RoleKeys.Where(role => !expectedDeltas.ContainsKey(role)),
            role => Assert.Equal(0, deltas[role]));
    }

    [Fact]
    public void ResolvePlanDeltas_OverlappingStrategies_TakesMaxNotSum()
    {
        var deltas = CutLabFloorDefaults.ResolvePlanDeltas(new CutLabPlanProfile { GenericStrategies = ["combo", "voltron"] });

        Assert.Equal(2, deltas["protection"]);
    }

    [Fact]
    public void ResolvePlanDeltas_NoStrategyEverRaisesLands()
    {
        foreach (var entry in DeckPlanStrategyCatalog.Entries)
        {
            var deltas = CutLabFloorDefaults.ResolvePlanDeltas(new CutLabPlanProfile { GenericStrategies = [entry.Slug] });
            Assert.Equal(0, deltas["lands"]);
        }
    }

    [Fact]
    public void ResolvePlanDeltas_UnknownSlug_Ignored()
    {
        var deltas = CutLabFloorDefaults.ResolvePlanDeltas(new CutLabPlanProfile { GenericStrategies = ["unknown"] });

        Assert.All(deltas.Values, delta => Assert.Equal(0, delta));
    }

    [Fact]
    public void PlanFloorDeltas_CatalogEntries_UseOnlyCanonicalRoleKeys()
    {
        foreach (DeckPlanStrategyEntry strategy in DeckPlanStrategyCatalog.Entries)
        {
            if (!CutLabFloorDefaults.PlanFloorDeltas.TryGetValue(strategy.Slug, out IReadOnlyDictionary<string, int>? deltas))
            {
                continue;
            }

            Assert.All(deltas.Keys, role => Assert.Contains(role, CutLabFloorRules.RoleKeys, StringComparer.Ordinal));
        }
    }

    [Fact]
    public void PlanFloorDeltas_EveryStrategyConsequenceNamesARaisedRole()
    {
        foreach (DeckPlanStrategyEntry strategy in DeckPlanStrategyCatalog.Entries)
        {
            Assert.True(
                CutLabFloorDefaults.PlanFloorDeltas.TryGetValue(strategy.Slug, out IReadOnlyDictionary<string, int>? deltas));
            Assert.All(deltas!.Keys, role =>
            {
                // Why: role keys can be plural ("engines", "payoffs") while prose may use
                // the singular ("raises the engine floor"); check every raised role without
                // treating a grammar difference as category drift.
                string roleWord = role.Split('-')[^1];
                string stem = role.Split('-')[0];
                Assert.True(
                    strategy.Consequence.Contains(roleWord, StringComparison.OrdinalIgnoreCase)
                    || strategy.Consequence.Contains(Singularize(stem), StringComparison.OrdinalIgnoreCase),
                    $"{strategy.Slug} raises '{role}' but its consequence copy never names it.");
            });
        }
    }

    [Fact]
    public void Deserialize_PlanProfileWithCommanderThemes_PreservesNonBlankUniqueSlugs()
    {
        CutLabState state = new()
        {
            Intent = new CutLabIntent
            {
                PlanProfile = new CutLabPlanProfile
                {
                    CommanderThemes =
                    [
                        new CutLabCommanderTheme { Slug = "stax" },
                        new CutLabCommanderTheme { Slug = "voltron" },
                    ],
                    CommanderThemesUnavailable = true,
                },
            },
        };

        CutLabState roundTripped = CutLabStateSerializer.Deserialize(CutLabStateSerializer.Serialize(state));

        Assert.Equal(["stax", "voltron"], roundTripped.Intent.PlanProfile!.CommanderThemes.Select(theme => theme.Slug));
    }

    [Fact]
    public void Deserialize_PlanProfile_BoundsAndDeduplicatesUntrustedCollections()
    {
        CutLabState state = new()
        {
            Intent = new CutLabIntent
            {
                PlanProfile = new CutLabPlanProfile
                {
                    GenericStrategies = [.. Enumerable.Range(0, 13).Select(index => $"strategy-{index}"), "STRATEGY-0", " "],
                    CommanderThemes = [.. Enumerable.Range(0, 51).Select(index => new CutLabCommanderTheme { Slug = $"theme-{index}" }), new CutLabCommanderTheme { Slug = "THEME-0" }, new CutLabCommanderTheme { Slug = " " }],
                },
            },
        };

        CutLabPlanProfile profile = CutLabStateSerializer.Deserialize(CutLabStateSerializer.Serialize(state)).Intent.PlanProfile!;

        Assert.Equal(12, profile.GenericStrategies.Count);
        Assert.Equal(50, profile.CommanderThemes.Count);
        Assert.Equal(profile.GenericStrategies.Count, profile.GenericStrategies.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(profile.CommanderThemes.Count, profile.CommanderThemes.Select(theme => theme.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ResolveDefaults_NoProfile_ByteIdenticalToTodaysRows()
    {
        var current = Resolve();
        var explicitEmpty = Resolve(new CutLabPlanProfile());

        Assert.Equal(current.Count, explicitEmpty.Count);
        foreach ((CutLabResolvedFloor before, CutLabResolvedFloor after) in current.Zip(explicitEmpty))
        {
            Assert.Equal(before.Role, after.Role);
            Assert.Equal(before.Floor, after.Floor);
            Assert.Equal(before.IsUserSet, after.IsUserSet);
            Assert.Equal(before.DefaultValue, after.DefaultValue);
            Assert.Equal(before.BracketValue, after.BracketValue);
            Assert.Equal(before.CommanderValue, after.CommanderValue);
            Assert.Equal(before.ResolvedBracket, after.ResolvedBracket);
            Assert.Equal(before.BracketWasFallback, after.BracketWasFallback);
            Assert.Equal(before.PlanDelta, after.PlanDelta);
        }
    }

    [Fact]
    public void ResolveDefaults_DeltaApplied_RaisesDefaultValueButNotBracketOrCommander()
    {
        var withoutPlan = Resolve();
        var withPlan = Resolve(new CutLabPlanProfile { GenericStrategies = ["combo"] });
        var before = withoutPlan.Single(floor => floor.Role == "protection");
        var after = withPlan.Single(floor => floor.Role == "protection");

        Assert.Equal(before.DefaultValue + 1, after.DefaultValue);
        Assert.Equal(before.BracketValue, after.BracketValue);
        Assert.Equal(before.CommanderValue, after.CommanderValue);
        Assert.Equal(1, after.PlanDelta);
    }

    [Fact]
    public void ResolveDefaults_DeltaAtCeiling_ClampedToMaxFloor()
    {
        var rows = Resolve(new CutLabPlanProfile { GenericStrategies = ["combo"] }, roleFloorBaseline: new FakeRoleFloorBaselineProvider(new Dictionary<string, int> { ["wincons"] = CutLabFloorRules.MaxFloor }));
        var floor = rows.Single(row => row.Role == "wincons");

        Assert.Equal(CutLabFloorRules.MaxFloor, floor.DefaultValue);
    }

    [Fact]
    public void ResolveDefaults_UserSetOverride_StillWinsOverDeltaRaisedDefault()
    {
        var rows = Resolve(new CutLabPlanProfile { GenericStrategies = ["combo"] }, [new CutLabRoleFloor { Role = "protection", Floor = 99, IsUserSet = true }]);

        Assert.Equal(99, rows.Single(row => row.Role == "protection").Floor);
    }

    [Fact]
    public void PlanFloorDeltas_MutationGuard_VoltronProtectionDeltaDrivesComposition()
    {
        var combo = CutLabFloorDefaults.ResolvePlanDeltas(new CutLabPlanProfile { GenericStrategies = ["combo"] })["protection"];
        var voltron = CutLabFloorDefaults.ResolvePlanDeltas(new CutLabPlanProfile { GenericStrategies = ["voltron"] })["protection"];
        var combined = CutLabFloorDefaults.ResolvePlanDeltas(new CutLabPlanProfile { GenericStrategies = ["combo", "voltron"] })["protection"];

        Assert.True(combined > combo);
        Assert.Equal(voltron, combined);
    }

    private static string Singularize(string word) => word.EndsWith('s') ? word[..^1] : word;

    private static IReadOnlyList<CutLabResolvedFloor> Resolve(CutLabPlanProfile? planProfile = null, IReadOnlyList<CutLabRoleFloor>? priorFloors = null, IRoleFloorBaselineProvider? roleFloorBaseline = null) =>
        CutLabFloorDefaults.ResolveDefaults(3, "Focused", 3, [], null, null, roleFloorBaseline, priorFloors ?? [], planProfile);

    private static IReadOnlyDictionary<string, int> ExpectedDeltas(string slug) => slug switch
    {
        "combo" => new Dictionary<string, int> { ["protection"] = 1, ["wincons"] = 1 },
        "aristocrats" => new Dictionary<string, int> { ["engines"] = 1, ["payoffs"] = 1 },
        "voltron" => new Dictionary<string, int> { ["protection"] = 2 },
        "tokens" or "lifegain" or "counters" => new Dictionary<string, int> { ["payoffs"] = 1 },
        "spellslinger" => new Dictionary<string, int> { ["draw"] = 1 },
        "stax" => new Dictionary<string, int> { ["interaction-mass"] = 1, ["protection"] = 1 },
        "reanimator" => new Dictionary<string, int> { ["engines"] = 1 },
        "landfall" => new Dictionary<string, int> { ["ramp"] = 1 },
        "combat" => new Dictionary<string, int> { ["wincons"] = 1 },
        "control" => new Dictionary<string, int> { ["interaction-targeted"] = 1, ["interaction-mass"] = 1 },
        _ => new Dictionary<string, int>(),
    };

    private sealed class FakeRoleFloorBaselineProvider(IReadOnlyDictionary<string, int> floors) : IRoleFloorBaselineProvider
    {
        public void EnsureLoaded()
        {
        }

        public bool TryGetRoleFloor(IReadOnlyList<string> commanderNames, string role, out int floor) => floors.TryGetValue(role, out floor);
    }
}
