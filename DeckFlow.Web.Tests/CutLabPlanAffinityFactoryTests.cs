using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabPlanAffinityFactoryTests
{
    [Fact]
    public async Task BuildAsync_CheckedThemeFetchSucceeds_ReportsThemesAvailableAndMarksOnlyThemeMembers()
    {
        var themeService = new FakeEdhrecCommanderThemeService
        {
            ThemesResult = new EdhrecThemeResult([Theme("goblins", "Goblins")], false),
            CardsBySlug = Lists(("goblins", ["Theme Card"])),
        };

        var result = await new CutLabPlanAffinityFactory(themeService).BuildAsync(
            Profile(strategies: ["combo"], themes: [Theme("goblins", "Goblins")]),
            [Card("Theme Card"), Card("Strategy Only", "combo")],
            ["Krenko, Mob Boss"]);

        Assert.True(result.CheckedCommanderThemesAvailable);
        Assert.True(CutLabPlanAffinityResolver.For(result.Affinities!, "Theme Card").IsInCheckedCommanderTheme);
        CutLabPlanAffinity strategyOnly = CutLabPlanAffinityResolver.For(result.Affinities!, "Strategy Only");
        Assert.True(strategyOnly.IsOnPlan);
        Assert.False(strategyOnly.IsInCheckedCommanderTheme);
    }

    [Fact]
    public async Task BuildAsync_NullProfile_ReturnsNull_AndIssuesNoRequests()
    {
        var themeService = new FakeEdhrecCommanderThemeService();
        var factory = new CutLabPlanAffinityFactory(themeService);

        var result = await factory.BuildAsync(null, [Card("Card")], ["Krenko, Mob Boss"]);

        Assert.Null(result.Affinities);
        Assert.False(result.CheckedCommanderThemesAvailable);
        Assert.Empty(themeService.CommanderThemeCalls);
        Assert.Empty(themeService.ThemeCardCalls);
    }

    [Fact]
    public async Task BuildAsync_EmptyProfile_ReturnsNull_AndIssuesNoRequests()
    {
        var themeService = new FakeEdhrecCommanderThemeService();
        var factory = new CutLabPlanAffinityFactory(themeService);

        var result = await factory.BuildAsync(Profile(), [Card("Card")], ["Krenko, Mob Boss"]);

        Assert.Null(result.Affinities);
        Assert.False(result.CheckedCommanderThemesAvailable);
        Assert.Empty(themeService.CommanderThemeCalls);
        Assert.Empty(themeService.ThemeCardCalls);
    }

    [Fact]
    public async Task BuildAsync_NoCheckedThemes_DoesNotFetchUncheckedThemeCards()
    {
        var themeService = new FakeEdhrecCommanderThemeService
        {
            ThemesResult = new EdhrecThemeResult([Theme("theme-01", "Theme 1"), Theme("theme-02", "Theme 2")], false),
        };
        var factory = new CutLabPlanAffinityFactory(themeService);

        var result = await factory.BuildAsync(Profile(themes: [Theme("unknown", "Unknown")]), [Card("Card")], ["Krenko, Mob Boss"]);

        Assert.NotNull(result.Affinities);
        Assert.False(result.CheckedCommanderThemesAvailable);
        Assert.Empty(themeService.ThemeCardCalls);
    }

    [Fact]
    public async Task BuildAsync_StrategiesOnly_ReturnsMap_AndIssuesNoThemeCardRequests()
    {
        var themeService = new FakeEdhrecCommanderThemeService();
        var factory = new CutLabPlanAffinityFactory(themeService);
        var profile = Profile(strategies: ["combo"]);

        var result = await factory.BuildAsync(profile, [Card("Tutor", "tutor"), Card("Unmatched")], ["Krenko, Mob Boss"]);

        Assert.NotNull(result.Affinities);
        Assert.False(result.CheckedCommanderThemesAvailable);
        Assert.True(CutLabPlanAffinityResolver.For(result.Affinities!, "Tutor").IsOnPlan);
        Assert.False(CutLabPlanAffinityResolver.For(result.Affinities!, "Unmatched").IsOnPlan);
        Assert.Empty(themeService.CommanderThemeCalls);
        Assert.Empty(themeService.ThemeCardCalls);
    }

    [Fact]
    public async Task BuildAsync_NoCommander_ReturnsAffinitiesWithThemesUnavailable()
    {
        var result = await new CutLabPlanAffinityFactory(new FakeEdhrecCommanderThemeService()).BuildAsync(
            Profile(themes: [Theme("goblins", "Goblins")]),
            [Card("Card")],
            []);

        Assert.NotNull(result.Affinities);
        Assert.False(result.CheckedCommanderThemesAvailable);
    }

    [Fact]
    public async Task BuildAsync_CheckedAndUncheckedThemes_FetchMembershipForOffPlanDetection()
    {
        var themeA = Theme("theme-a", "Theme A");
        var themeB = Theme("theme-b", "Theme B");
        var themeService = new FakeEdhrecCommanderThemeService
        {
            ThemesResult = new EdhrecThemeResult([themeA, themeB], false),
            CardsBySlug = Lists(("theme-a", ["On Plan Card"]), ("theme-b", ["Off Plan Card"])),
        };
        var factory = new CutLabPlanAffinityFactory(themeService);
        var profile = Profile(themes: [themeA]);

        var result = await factory.BuildAsync(profile, [Card("On Plan Card"), Card("Off Plan Card")], ["Krenko, Mob Boss"]);

        Assert.NotNull(result.Affinities);
        Assert.True(CutLabPlanAffinityResolver.For(result.Affinities!, "On Plan Card").IsOnPlan);
        Assert.Contains(CutLabPlanAffinityResolver.For(result.Affinities!, "Off Plan Card").OffPlanThemes, theme => theme.DisplayName == "Theme B");
        Assert.Equal(["Krenko, Mob Boss"], themeService.CommanderThemeCalls);
        Assert.Equal(2, themeService.ThemeCardCalls.Count);
        Assert.Contains(("Krenko, Mob Boss", "theme-a"), themeService.ThemeCardCalls);
        Assert.Contains(("Krenko, Mob Boss", "theme-b"), themeService.ThemeCardCalls);
    }

    [Fact]
    public async Task BuildAsync_CraftedDuplicateOrUnknownThemeSlugs_AreBoundedDeduplicatedBeforeFetch()
    {
        CutLabCommanderTheme[] knownThemes = Enumerable.Range(1, 15)
            .Select(index => Theme($"theme-{index:00}", $"Theme {index}"))
            .ToArray();
        var themeService = new FakeEdhrecCommanderThemeService
        {
            ThemesResult = new EdhrecThemeResult(knownThemes, false),
        };
        var factory = new CutLabPlanAffinityFactory(themeService);
        // Duplicated checked slug plus a slug absent from the known-theme list — both must be
        // filtered before any card-list request, and the exposed known-theme count (15) is
        // strictly more than the profile's checked count (1 distinct + 1 unknown).
        var profile = Profile(themes:
        [
            Theme("theme-01", "Duplicate A"),
            Theme("theme-01", "Duplicate A Again"),
            Theme("unknown-slug", "Unknown"),
        ]);

        var result = await factory.BuildAsync(profile, [Card("Card")], ["Krenko, Mob Boss"]);

        Assert.NotNull(result.Affinities);
        Assert.Equal(1 + CutLabPlanAffinityFactory.MaxOffPlanProbeFetches, themeService.ThemeCardCalls.Count);
        string[] requestedSlugs = themeService.ThemeCardCalls.Select(call => call.ThemeSlug).ToArray();
        Assert.Equal(requestedSlugs.Length, requestedSlugs.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("theme-01", requestedSlugs);
        Assert.DoesNotContain("unknown-slug", requestedSlugs);
    }

    [Fact]
    public async Task BuildAsync_CheckedThemesExceedingCap_FetchesOnlyCappedCheckedThemesAndOffPlanProbes()
    {
        CutLabCommanderTheme[] knownThemes = Enumerable.Range(1, CutLabPlanAffinityFactory.MaxCheckedThemeFetches + 3)
            .Select(index => Theme($"theme-{index:00}", $"Theme {index}"))
            .ToArray();
        var themeService = new FakeEdhrecCommanderThemeService
        {
            ThemesResult = new EdhrecThemeResult(knownThemes, false),
        };
        var factory = new CutLabPlanAffinityFactory(themeService);
        var profile = Profile(themes: knownThemes);

        var result = await factory.BuildAsync(profile, [Card("Card")], ["Krenko, Mob Boss"]);

        Assert.NotNull(result.Affinities);
        Assert.Equal(
            CutLabPlanAffinityFactory.MaxCheckedThemeFetches + CutLabPlanAffinityFactory.MaxOffPlanProbeFetches,
            themeService.ThemeCardCalls.Count);
    }

    [Fact]
    public async Task BuildAsync_CommanderThemesUnavailable_StrategyLayerStillResolves_AndNoOffPlanThemes()
    {
        var themeService = new FakeEdhrecCommanderThemeService
        {
            ThemesResult = new EdhrecThemeResult([], true),
        };
        var factory = new CutLabPlanAffinityFactory(themeService);
        var profile = Profile(strategies: ["combo"], themes: [Theme("theme-a", "Theme A")]);

        var result = await factory.BuildAsync(profile, [Card("Tutor", "tutor"), Card("Unmatched")], ["Krenko, Mob Boss"]);

        Assert.NotNull(result.Affinities);
        Assert.False(result.CheckedCommanderThemesAvailable);
        Assert.True(CutLabPlanAffinityResolver.For(result.Affinities!, "Tutor").IsOnPlan);
        Assert.Empty(CutLabPlanAffinityResolver.For(result.Affinities!, "Unmatched").OffPlanThemes);
        Assert.Empty(themeService.ThemeCardCalls);
        Assert.Equal(["Krenko, Mob Boss"], themeService.CommanderThemeCalls);
    }

    [Fact]
    public async Task BuildAsync_OneThemeFetchFails_OtherThemeStillResolves()
    {
        var themeA = Theme("theme-a", "Theme A");
        var themeB = Theme("theme-b", "Theme B");
        var themeService = new FakeEdhrecCommanderThemeService
        {
            ThemesResult = new EdhrecThemeResult([themeA, themeB], false),
            CardsBySlug = Lists(("theme-b", ["Good Card"])),
            ThrowForSlug = "theme-a",
        };
        var factory = new CutLabPlanAffinityFactory(themeService);
        var profile = Profile(themes: [themeA, themeB]);

        var result = await factory.BuildAsync(profile, [Card("Good Card")], ["Krenko, Mob Boss"]);

        Assert.NotNull(result.Affinities);
        Assert.False(result.CheckedCommanderThemesAvailable);
        Assert.True(CutLabPlanAffinityResolver.For(result.Affinities!, "Good Card").IsOnPlan);
        Assert.Equal(2, themeService.ThemeCardCalls.Count);
    }

    [Fact]
    public async Task BuildAsync_CheckedThemeFetchReturnsFailure_ReportsThemesUnavailable()
    {
        var themeService = new FakeEdhrecCommanderThemeService
        {
            ThemesResult = new EdhrecThemeResult([Theme("goblins", "Goblins")], false),
            FailedSlug = "goblins",
        };

        var result = await new CutLabPlanAffinityFactory(themeService).BuildAsync(
            Profile(themes: [Theme("goblins", "Goblins")]), [Card("Goblin")], ["Krenko, Mob Boss"]);

        Assert.False(result.CheckedCommanderThemesAvailable);
    }

    [Fact]
    public async Task BuildAsync_Cancellation_Throws()
    {
        var themeService = new FakeEdhrecCommanderThemeService();
        var factory = new CutLabPlanAffinityFactory(themeService);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            factory.BuildAsync(Profile(strategies: ["combo"]), [Card("Card")], ["Krenko, Mob Boss"], cts.Token));
    }

    [Fact]
    public async Task BuildAsync_ThemeFetchBudgetExpiresAfterCheckedSlugResolved_KeepsResolvedMembership()
    {
        TimeSpan originalBudget = CutLabPlanAffinityFactory.TotalThemeFetchBudget;
        CutLabPlanAffinityFactory.TotalThemeFetchBudget = TimeSpan.FromMilliseconds(50);
        try
        {
            var themeService = new FakeEdhrecCommanderThemeService
            {
                ThemesResult = new EdhrecThemeResult([Theme("theme-a", "Theme A"), Theme("theme-b", "Theme B")], false),
                CardsBySlug = Lists(("theme-a", ["On Plan Card"])),
                BlockAfterFirstThemeFetch = true,
            };
            var result = await new CutLabPlanAffinityFactory(themeService).BuildAsync(
                Profile(themes: [Theme("theme-a", "Theme A")]),
                [Card("On Plan Card")],
                ["Krenko, Mob Boss"]);

            Assert.NotNull(result.Affinities);
            Assert.True(result.CheckedCommanderThemesAvailable);
            Assert.True(CutLabPlanAffinityResolver.For(result.Affinities!, "On Plan Card").IsOnPlan);
            Assert.Empty(CutLabPlanAffinityResolver.For(result.Affinities!, "On Plan Card").OffPlanThemes);
        }
        finally
        {
            CutLabPlanAffinityFactory.TotalThemeFetchBudget = originalBudget;
        }
    }

    [Fact]
    public async Task BuildAsync_ThemeFetchBudgetExpiresOnCheckedTheme_ReportsThemesUnavailable()
    {
        TimeSpan originalBudget = CutLabPlanAffinityFactory.TotalThemeFetchBudget;
        CutLabPlanAffinityFactory.TotalThemeFetchBudget = TimeSpan.FromMilliseconds(1);
        try
        {
            var themeService = new FakeEdhrecCommanderThemeService
            {
                ThemesResult = new EdhrecThemeResult([Theme("goblins", "Goblins")], false),
                BlockThemeFetch = true,
            };

            var result = await new CutLabPlanAffinityFactory(themeService).BuildAsync(
                Profile(themes: [Theme("goblins", "Goblins")]), [Card("Goblin")], ["Krenko, Mob Boss"]);

            Assert.False(result.CheckedCommanderThemesAvailable);
        }
        finally
        {
            CutLabPlanAffinityFactory.TotalThemeFetchBudget = originalBudget;
        }
    }

    [Fact]
    public async Task BuildAsync_ResultKeys_ResolveThroughPlanAffinityResolverFor()
    {
        var themeService = new FakeEdhrecCommanderThemeService();
        var factory = new CutLabPlanAffinityFactory(themeService);
        var profile = Profile(strategies: ["combo"]);

        var result = await factory.BuildAsync(profile, [Card("Tutor", "tutor")], ["Krenko, Mob Boss"]);

        Assert.NotNull(result.Affinities);
        // CutLabPlanAffinityResolver.For normalizes the lookup name, proving the factory's returned
        // dictionary is keyed identically to CutLabPlanAffinityResolver.ResolveAll's own output.
        Assert.True(CutLabPlanAffinityResolver.For(result.Affinities!, "TUTOR").IsOnPlan);
    }

    private static CutLabAnalyzedCard Card(string name, params string[] categories) => new(name, 1, false, [], categories);

    private static CutLabCommanderTheme Theme(string slug, string displayName) => new() { Slug = slug, DisplayName = displayName, DeckCount = 1 };

    private static CutLabPlanProfile Profile(IReadOnlyList<string>? strategies = null, IReadOnlyList<CutLabCommanderTheme>? themes = null) =>
        new() { GenericStrategies = strategies ?? [], CommanderThemes = themes ?? [] };

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Lists(params (string Slug, string[] Names)[] entries) =>
        entries.ToDictionary(entry => entry.Slug, entry => (IReadOnlyList<string>)entry.Names);
}

/// <summary>
/// Stateful double for <see cref="IEdhrecCommanderThemeService"/> that records every commander-theme
/// and theme-card call it receives, so fetch-discipline tests can assert on the exact request log
/// rather than merely on the returned map.
/// </summary>
internal sealed class FakeEdhrecCommanderThemeService : IEdhrecCommanderThemeService
{
    public List<string> CommanderThemeCalls { get; } = [];

    public List<(string CommanderName, string ThemeSlug)> ThemeCardCalls { get; } = [];

    public EdhrecThemeResult ThemesResult { get; set; } = new([], false);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> CardsBySlug { get; set; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>When set, <see cref="GetThemeCardNamesAsync"/> throws for this one slug (case-insensitive), simulating a single EDHREC failure.</summary>
    public string? ThrowForSlug { get; set; }

    public string? FailedSlug { get; set; }

    public bool BlockAfterFirstThemeFetch { get; set; }

    public bool BlockThemeFetch { get; set; }

    public Task<EdhrecThemeResult> GetCommanderThemesAsync(string commanderName, CancellationToken cancellationToken = default)
    {
        CommanderThemeCalls.Add(commanderName);
        return Task.FromResult(ThemesResult);
    }

    public async Task<EdhrecThemeCardNamesResult> GetThemeCardNamesAsync(string commanderName, string themeSlug, CancellationToken cancellationToken = default)
    {
        ThemeCardCalls.Add((commanderName, themeSlug));
        if (BlockThemeFetch || (BlockAfterFirstThemeFetch && ThemeCardCalls.Count > 1))
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        if (string.Equals(ThrowForSlug, themeSlug, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Simulated EDHREC failure for theme '{themeSlug}'.");
        }

        IReadOnlyList<string> names = CardsBySlug.TryGetValue(themeSlug, out IReadOnlyList<string>? found) ? found : [];
        return new(names, !string.Equals(FailedSlug, themeSlug, StringComparison.OrdinalIgnoreCase));
    }
}
