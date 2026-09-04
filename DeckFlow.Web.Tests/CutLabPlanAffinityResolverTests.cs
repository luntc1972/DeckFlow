using DeckFlow.Core.Normalization;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabPlanAffinityResolverTests
{
    [Fact]
    public void Resolve_CardInCheckedTheme_IsOnPlan()
    {
        var result = Resolve([Card("Theme Card"), Card("Unmatched")], Profile(themes: [Theme("theme-a", "Theme A")]), Lists(("theme-a", ["Theme Card"])), [Theme("theme-a", "Theme A")]);

        Assert.True(CutLabPlanAffinityResolver.For(result, "Theme Card").IsOnPlan);
        Assert.Equal(["Theme A"], CutLabPlanAffinityResolver.For(result, "Theme Card").OnPlanThemes);
        Assert.Equal(1, result.Values.Count(affinity => affinity.IsOnPlan));
        Assert.Equal(1, result.Values.Count(affinity => !affinity.IsOnPlan));
    }

    [Fact]
    public void Resolve_CardInUncheckedTheme_IsOffPlanAndScoresZero()
    {
        var result = Resolve([Card("Theme Card"), Card("Unmatched")], Profile(strategies: ["unknown"]), Lists(("theme-a", ["Theme Card"])), [Theme("theme-a", "Theme A")]);

        var affinity = CutLabPlanAffinityResolver.For(result, "Theme Card");
        Assert.False(affinity.IsOnPlan);
        Assert.Equal(["Theme A"], affinity.OffPlanThemes.Select(theme => theme.DisplayName));
        Assert.Equal(0, affinity.Score);
        Assert.Equal(0, result.Values.Count(value => value.IsOnPlan));
        Assert.Equal(2, result.Values.Count(value => !value.IsOnPlan));
    }

    [Fact]
    public void Resolve_CardMatchingStrategyCategories_IsOnPlan()
    {
        var result = Resolve([Card("Tax", "tax"), Card("Unmatched")], Profile(strategies: ["stax"]), Lists(), []);

        Assert.True(CutLabPlanAffinityResolver.For(result, "Tax").IsOnPlan);
        Assert.Equal(["Stax"], CutLabPlanAffinityResolver.For(result, "Tax").OnPlanStrategies);
        Assert.Equal(1, result.Values.Count(value => value.IsOnPlan));
        Assert.Equal(1, result.Values.Count(value => !value.IsOnPlan));
    }

    [Fact]
    public void Resolve_ThemeOnlyMatch_AndStrategyOnlyMatch_BothOnPlan()
    {
        var result = Resolve([Card("Theme Card"), Card("Tax", "tax"), Card("Unmatched")], Profile(["stax"], [Theme("theme-a", "Theme A")]), Lists(("theme-a", ["Theme Card"])), [Theme("theme-a", "Theme A")]);

        Assert.True(CutLabPlanAffinityResolver.For(result, "Theme Card").IsOnPlan);
        Assert.True(CutLabPlanAffinityResolver.For(result, "Tax").IsOnPlan);
        Assert.Equal(2, result.Values.Count(value => value.IsOnPlan));
        Assert.Equal(1, result.Values.Count(value => !value.IsOnPlan));
    }

    [Fact]
    public void Resolve_DfcName_MatchesAcrossFrontAndFullForms()
    {
        string full = "Delver of Secrets // Insectile Aberration";
        string front = CardNormalizer.Normalize(full);
        var profile = Profile(themes: [Theme("theme-a", "Theme A")]);

        var frontPool = Resolve([Card(front), Card("Unmatched")], profile, Lists(("theme-a", [full])), [Theme("theme-a", "Theme A")]);
        var fullPool = Resolve([Card(full), Card("Unmatched")], profile, Lists(("theme-a", [front])), [Theme("theme-a", "Theme A")]);

        Assert.True(CutLabPlanAffinityResolver.For(frontPool, front).IsOnPlan);
        Assert.True(CutLabPlanAffinityResolver.For(fullPool, full).IsOnPlan);
    }

    [Fact]
    public void Resolve_CurlyApostropheName_Matches()
    {
        var result = Resolve([Card("Urza’s Saga"), Card("Unmatched")], Profile(themes: [Theme("theme-a", "Theme A")]), Lists(("theme-a", ["Urza's Saga"])), [Theme("theme-a", "Theme A")]);

        Assert.True(CutLabPlanAffinityResolver.For(result, "Urza’s Saga").IsOnPlan);
        Assert.Equal(1, result.Values.Count(value => value.IsOnPlan));
        Assert.Equal(1, result.Values.Count(value => !value.IsOnPlan));
    }

    [Fact]
    public void Resolve_FourMatches_ScoreIsCappedAtOnPlanScoreCap()
    {
        var themes = new[] { Theme("theme-a", "Theme A"), Theme("theme-b", "Theme B") };
        var result = Resolve([Card("Match", "tax token"), Card("Unmatched")], Profile(["stax", "tokens"], themes), Lists(("theme-a", ["Match"]), ("theme-b", ["Match"])), themes);

        var affinity = CutLabPlanAffinityResolver.For(result, "Match");
        Assert.Equal(3, CutLabPlanAffinityResolver.OnPlanScoreCap);
        Assert.Equal(3, affinity.Score);
        Assert.True(affinity.OnPlanThemes.Count + affinity.OnPlanStrategies.Count > CutLabPlanAffinityResolver.OnPlanScoreCap);
        Assert.Equal(1, result.Values.Count(value => value.IsOnPlan));
        Assert.Equal(1, result.Values.Count(value => !value.IsOnPlan));
    }

    [Fact]
    public void Resolve_NullProfile_AllCardsNeutral()
    {
        var result = Resolve([Card("Theme Card"), Card("Tax", "tax")], null, Lists(("theme-a", ["Theme Card"])), [Theme("theme-a", "Theme A")]);

        Assert.All(result.Values, affinity => Assert.Equal(CutLabPlanAffinity.Neutral, affinity));
    }

    [Fact]
    public void Resolve_EmptyProfile_AllCardsNeutral()
    {
        var result = Resolve([Card("Theme Card"), Card("Tax", "tax")], Profile(), Lists(("theme-a", ["Theme Card"])), [Theme("theme-a", "Theme A")]);

        Assert.All(result.Values, affinity => Assert.Equal(CutLabPlanAffinity.Neutral, affinity));
    }

    [Fact]
    public void Resolve_UnknownStrategySlug_Ignored()
    {
        var result = Resolve([Card("Card"), Card("Unmatched")], Profile(strategies: ["unknown"]), Lists(), []);

        Assert.All(result.Values, affinity => Assert.Equal(CutLabPlanAffinity.Neutral, affinity));
    }

    [Fact]
    public void Resolve_CheckedThemeWithNoFetchedCardList_DoesNotThrow()
    {
        var result = Resolve([Card("Card"), Card("Unmatched")], Profile(themes: [Theme("missing", "Missing")]), Lists(), [Theme("missing", "Missing")]);

        Assert.All(result.Values, affinity => Assert.Equal(CutLabPlanAffinity.Neutral, affinity));
    }

    [Fact]
    public void Resolve_RepeatedRuns_ProduceIdenticalLists()
    {
        var profile = Profile(["stax", "tokens"], [Theme("theme-a", "Theme A")]);
        var pool = new[] { Card("Match", "tax token"), Card("Unmatched") };
        var lists = Lists(("theme-a", ["Match"]));

        var first = Resolve(pool, profile, lists, profile.CommanderThemes);
        var second = Resolve(pool, profile, lists, profile.CommanderThemes);

        Assert.Equal(first.Keys, second.Keys);
        foreach (string key in first.Keys)
        {
            Assert.Equal(first[key].OnPlanThemes, second[key].OnPlanThemes);
            Assert.Equal(first[key].OffPlanThemes, second[key].OffPlanThemes);
            Assert.Equal(first[key].OnPlanStrategies, second[key].OnPlanStrategies);
            Assert.Equal(first[key].Score, second[key].Score);
        }
    }

    private static CutLabAnalyzedCard Card(string name, params string[] categories) => new(name, 1, false, [], categories);

    private static CutLabCommanderTheme Theme(string slug, string displayName) => new() { Slug = slug, DisplayName = displayName, DeckCount = 1 };

    private static CutLabPlanProfile Profile(IReadOnlyList<string>? strategies = null, IReadOnlyList<CutLabCommanderTheme>? themes = null) => new() { GenericStrategies = strategies ?? [], CommanderThemes = themes ?? [] };

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Lists(params (string Slug, string[] Names)[] entries) => entries.ToDictionary(entry => entry.Slug, entry => (IReadOnlyList<string>)entry.Names);

    private static IReadOnlyDictionary<string, CutLabPlanAffinity> Resolve(IReadOnlyList<CutLabAnalyzedCard> pool, CutLabPlanProfile? profile, IReadOnlyDictionary<string, IReadOnlyList<string>> lists, IReadOnlyList<CutLabCommanderTheme> allThemes) => CutLabPlanAffinityResolver.ResolveAll(pool, profile, lists, allThemes);
}
