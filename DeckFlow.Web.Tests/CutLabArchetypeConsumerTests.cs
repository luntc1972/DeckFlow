using DeckFlow.Core.Analysis;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for archetype preset strategies consumed by Cut Lab engines.</summary>
public sealed class CutLabArchetypeConsumerTests
{
    [Fact]
    public void ResolvePlanDeltas_ArchetypeOnlyStax_RaisesInteractionMassAndProtection()
    {
        IReadOnlyDictionary<string, int> deltas = CutLabFloorDefaults.ResolvePlanDeltas(Profile("stax"));

        Assert.Equal(1, deltas["interaction-mass"]);
        Assert.Equal(1, deltas["protection"]);
    }

    [Fact]
    public void ResolvePlanDeltas_NoArchetype_UnchangedFromManualOnly()
    {
        var manualOnly = new CutLabPlanProfile { GenericStrategies = ["stax"] };
        var withNoArchetype = new CutLabPlanProfile { GenericStrategies = ["stax"], Archetype = null };

        Assert.Equal(CutLabFloorDefaults.ResolvePlanDeltas(manualOnly), CutLabFloorDefaults.ResolvePlanDeltas(withNoArchetype));
    }

    [Fact]
    public void ResolveAll_ArchetypeOnlyProfile_MarksPresetStrategyCardOnPlan()
    {
        IReadOnlyDictionary<string, CutLabPlanAffinity> affinities = CutLabPlanAffinityResolver.ResolveAll(
            [Card("Tax", "tax")], Profile("stax"), EmptyThemeCards, []);

        Assert.True(CutLabPlanAffinityResolver.For(affinities, "Tax").IsOnPlan);
    }

    [Fact]
    public async Task BuildAsync_ArchetypeOnlyProfile_IsNotSkippedByEarlyExit()
    {
        var result = await new CutLabPlanAffinityFactory(new FakeEdhrecCommanderThemeService { IsUnavailable = true }).BuildAsync(
            Profile("stax"), [Card("Tax", "tax")], []);

        Assert.NotNull(result.Affinities);
        Assert.True(CutLabPlanAffinityResolver.For(result.Affinities, "Tax").IsOnPlan);
    }

    [Fact]
    public void BuildQueue_ArchetypePresetCard_MovesLaterWithinItsRoundOnly()
    {
        CutLabRoundInputCard[] cards =
        [
            RoundCard("Tax", []),
            RoundCard("Round mate", []),
            RoundCard("Later", []),
        ];
        IReadOnlyDictionary<string, CutLabPlanAffinity> affinities = CutLabPlanAffinityResolver.ResolveAll(
            [Card("Tax", "tax"), Card("Round mate"), Card("Later")], Profile("stax"), EmptyThemeCards, []);

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(cards, Findings(("Tax", CutLabFindingKind.CurveCongestion), ("Tax", CutLabFindingKind.StrandedSubtheme), ("Round mate", CutLabFindingKind.CurveCongestion), ("Round mate", CutLabFindingKind.StrandedSubtheme), ("Later", CutLabFindingKind.CurveCongestion)), [], 3, planAffinities: affinities);

        Assert.Equal(["Round mate", "Tax", "Later"], plan.Queue.Select(item => item.CardName));
    }

    [Fact]
    public void BuildQueue_ArchetypeWithRampCard_LeavesInfrastructureOrderUnchanged()
    {
        CutLabRoundInputCard[] cards = [RoundCard("Ramp tax", ["ramp"]), RoundCard("Other", [])];
        IReadOnlyDictionary<string, CutLabPlanAffinity> affinities = CutLabPlanAffinityResolver.ResolveAll(
            [Card("Ramp tax", "tax"), Card("Other")], Profile("stax"), EmptyThemeCards, []);

        CutLabStructuralFindingsResult findings = Findings(("Other", CutLabFindingKind.CurveCongestion));
        CutLabRoundPlan baseline = CutLabCutRoundEngine.BuildQueue(cards, findings, [], 2);
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(cards, findings, [], 2, planAffinities: affinities);

        CutLabRoundQueueItem ramp = Assert.Single(plan.Queue, item => item.CardName == "Ramp tax");
        Assert.Equal(CutLabCutRoundEngine.InfrastructureKey, ramp.RoundKey);
        Assert.Equal(baseline.Queue.Select(item => item.CardName), plan.Queue.Select(item => item.CardName));
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyThemeCards =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    private static CutLabPlanProfile Profile(string archetype) => new() { Archetype = archetype };

    private static CutLabAnalyzedCard Card(string name, params string[] categories) => new(name, 1, false, [], categories);

    private static CutLabRoundInputCard RoundCard(string name, IReadOnlyList<string> roles) =>
        new(name, 1, "Artifact", false, false, 1, false, roles, []);

    private static CutLabStructuralFindingsResult Findings(params (string Name, CutLabFindingKind Kind)[] entries) =>
        new(entries.GroupBy(entry => entry.Kind).Select(group => new CutLabFinding(group.Key, group.Key.ToString(), group.Key.ToString(), group.Select(entry => new CutLabFindingEvidence(entry.Name, 1)).ToArray())).ToArray(), false, false);

    private sealed class FakeEdhrecCommanderThemeService : IEdhrecCommanderThemeService
    {
        public bool IsUnavailable { get; init; }

        public Task<EdhrecThemeResult> GetCommanderThemesAsync(string commanderName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EdhrecThemeResult([], IsUnavailable));

        public Task<EdhrecThemeCardNamesResult> GetThemeCardNamesAsync(string commanderName, string themeSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EdhrecThemeCardNamesResult([], IsUnavailable));
    }
}
