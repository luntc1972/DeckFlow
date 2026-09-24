using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using DeckFlow.Core.Knowledge.CardGrounding;
using DeckFlow.Core.Models;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CreatorStyle;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using DeckFlow.Web.Tests.TestDoubles;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class AdminCreatorSuppressionTests
{
    [Fact]
    public async Task SuppressCreator_RealSqlite_HidesIndex_BumpsRevision_AndPersistsAliases()
    {
        using var fixture = await SuppressionFixture.CreateAsync();
        await fixture.MakeSuppressionTargetsVisibleAsync();
        var visibleBeforeSuppression = await fixture.Index.GetAllRowsAsync();
        Assert.All(visibleBeforeSuppression.Where(row => row.Source == "3/3 Elk" || row.YoutubeVideoId == "folder-only" || row.YoutubeVideoId == "folder-slug-only"), row => Assert.True(row.IsVisible));
        var before = await fixture.Suppressions.GetRevisionAsync();
        await fixture.Controller.SuppressCreator("3/3 Elk", "admin.suppress", "note", default);
        var row = Assert.Single(await fixture.Suppressions.ListAsync());
        Assert.Equal("elk-canon", row.Slug);
        Assert.Contains("3/3 Elk", row.Aliases);
        Assert.Contains("3-3-elk-old", row.Aliases);
        Assert.True(await fixture.Suppressions.GetRevisionAsync() > before);
        Assert.All((await fixture.Index.GetAllRowsAsync()).Where(row => row.Source == "3/3 Elk"), row => Assert.False(row.IsVisible));
        Assert.False((await fixture.Index.GetAllRowsAsync()).Single(row => row.YoutubeVideoId == "folder-only").IsVisible);
        Assert.False((await fixture.Index.GetAllRowsAsync()).Single(row => row.YoutubeVideoId == "folder-slug-only").IsVisible);
    }

    [Fact]
    public async Task SuppressCreator_UnrelatedFolderControl_RemainsVisible()
    {
        using var fixture = await SuppressionFixture.CreateAsync();
        await fixture.Index.SetVisibilityByCreatorAsync(new CreatorIdentity("unrelated-creator", [], ["Unrelated creator"], ["unrelated-creator"]), true);
        var before = (await fixture.Index.GetAllRowsAsync()).Single(row => row.ArtifactPath == "content-kb/unrelated-creator/control.md");
        Assert.True(before.IsVisible);
        await fixture.Controller.SuppressCreator("3/3 Elk", "admin.suppress", "note", default);

        var control = (await fixture.Index.GetAllRowsAsync()).Single(row => row.ArtifactPath == "content-kb/unrelated-creator/control.md");
        Assert.True(control.IsVisible);
    }

    [Fact]
    public async Task SuppressCreator_DisplayNameClearsExistingHiddenState()
    {
        using var fixture = await SuppressionFixture.CreateAsync();
        await fixture.Index.SetHiddenBySourceAsync("3/3 Elk", true);
        var hiddenBeforeSuppression = (await fixture.Index.GetAllRowsAsync()).Where(row => row.Source == "3/3 Elk");
        Assert.All(hiddenBeforeSuppression, row => Assert.True(row.IsHidden));

        await fixture.Controller.SuppressCreator("3/3 Elk", "admin.suppress", "note", default);

        var displayRows = (await fixture.Index.GetAllRowsAsync()).Where(row => row.Source == "3/3 Elk");
        Assert.All(displayRows, row => Assert.False(row.IsHidden));
    }

    [Fact]
    public async Task PurgeCreator_CrossRepresentation_AllSeven_RemovesTargetAndPreservesControl()
    {
        using var fixture = await SuppressionFixture.CreateAsync();
        await fixture.Controller.PurgeCreator("elk-canon", "elk-canon", default);
        await fixture.AssertTargetPurgedAsync();
        await fixture.AssertControlPresentAsync();
    }

    [Fact]
    public async Task PurgeCreator_WhenConfirmSlugDiffersOnlyByCase_DoesNotPurge()
    {
        using var fixture = await SuppressionFixture.CreateAsync();
        await fixture.Controller.PurgeCreator("elk-canon", "ELK-CANON", default);

        await fixture.AssertTargetPresentAsync();
    }

    [Fact]
    public async Task PurgeCreator_InvalidatesCachedWhitelistPool()
    {
        using var fixture = await SuppressionFixture.CreateAsync();
        var before = await fixture.Whitelist.BuildWithDiagnosticsAsync("elk-canon", DeckContext());
        Assert.Contains("Sol Ring", before.AcceptedNames);

        await fixture.Controller.PurgeCreator("elk-canon", "elk-canon", default);

        var after = await fixture.Whitelist.BuildWithDiagnosticsAsync("elk-canon", DeckContext());
        Assert.Empty(after.AcceptedNames);
    }

    [Fact]
    public async Task PurgeCreator_ByEachRepresentation_RemovesTargetFromAllSeven()
    {
        foreach (var representation in new[] { "3/3 Elk", "3-3-elk-old", "elk-canon" })
        {
            using var fixture = await SuppressionFixture.CreateAsync();
            await fixture.Controller.PurgeCreator(representation, "elk-canon", default);
            await fixture.AssertTargetPurgedAsync();
        }
    }

    [Fact]
    public async Task AdminAuditLog_RecordsStructuredSuppressAndPurgeEntries()
    {
        using var fixture = await SuppressionFixture.CreateAsync();
        await fixture.Controller.SuppressCreator("3/3 Elk", "admin.suppress", null, default);
        await fixture.Controller.PurgeCreator("elk-canon", "elk-canon", default);
        var entries = fixture.Logger.Entries.Where(entry => entry.Any(pair => pair.Key == "Action")).ToArray();
        Assert.Contains(entries, entry => Equals(State(entry, "Action"), "suppress") && Equals(State(entry, "Slug"), "elk-canon") && State(entry, "Utc") is DateTimeOffset);
        Assert.Contains(entries, entry => Equals(State(entry, "Action"), "purge") && Equals(State(entry, "Slug"), "elk-canon") && State(entry, "Utc") is DateTimeOffset);
    }

    [Fact]
    public async Task PurgeCreator_ArtifactFoldersListed_ForEveryIdentityFolderSlug()
    {
        using var fixture = await SuppressionFixture.CreateAsync();
        await fixture.Controller.PurgeCreator("elk-canon", "elk-canon", default);
        var banner = fixture.Controller.TempData["AdminContentKbBanner"]!.ToString()!;
        Assert.Contains("content-kb/3-3-elk-old", banner);
    }

    [Fact]
    public Task PurgeCreator_FailureSiteIndex_ReportsFailureAndPreservesTarget() => AssertPurgeFailureAsync("site-index");

    [Fact]
    public Task PurgeCreator_FailureVideos_ReportsFailureAndPreservesTarget() => AssertPurgeFailureAsync("videos");

    [Fact]
    public Task PurgeCreator_FailureStatedRules_ReportsFailureAndPreservesTarget() => AssertPurgeFailureAsync("stated-rules");

    [Fact]
    public Task PurgeCreator_FailureProfile_ReportsFailureAndPreservesTarget() => AssertPurgeFailureAsync("profile");

    [Fact]
    public Task PurgeCreator_FailureDeckCache_ReportsFailureAndPreservesTarget() => AssertPurgeFailureAsync("deck-cache");

    [Fact]
    public Task PurgeCreator_FailureCreatorSources_ReportsFailureAndPreservesTarget() => AssertPurgeFailureAsync("creator-sources");

    [Fact]
    public Task PurgeCreator_FailureProfileSource_ReportsFailureAndPreservesTarget() => AssertPurgeFailureAsync("profile-source");

    private static async Task AssertPurgeFailureAsync(string failedStore)
    {
        using var fixture = await SuppressionFixture.CreateAsync();
        fixture.ReplacePurgeStepWithFailure(failedStore);
        await fixture.Controller.PurgeCreator("elk-canon", "elk-canon", default);
        Assert.Contains($"{failedStore}: failed (boom-{failedStore})", fixture.Controller.TempData["AdminContentKbBanner"]!.ToString());
        await fixture.AssertOnlyFailedTargetRemainsAsync(failedStore);
    }

    private static object? State(IReadOnlyList<KeyValuePair<string, object?>> values, string key)
        => values.Single(pair => pair.Key == key).Value;

    private static CardGroundingDeckContext DeckContext() => new()
    {
        CommanderColorIdentity = new HashSet<string>(),
        DeckProducedColors = new HashSet<char>(),
        DeckCardNames = new HashSet<string>(),
    };
    [Fact]
    public async Task PurgeCreator_WrongConfirmation_DoesNotInvokePurge()
    {
        using var fixture = await SuppressionFixture.CreateAsync();
        var result = await fixture.Controller.PurgeCreator("elk-canon", "wrong", default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains("type the canonical slug elk-canon", fixture.Controller.TempData["AdminContentKbBanner"]!.ToString());
    }

    private sealed class TempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class SuppressionFixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"admin-suppression-{Guid.NewGuid():N}.db");
        public ContentSiteIndexStore Index { get; private set; } = null!;
        public ContentVideoStore Videos { get; private set; } = null!;
        public CreatorWhitelistPoolBuilder Whitelist { get; private set; } = null!;
        public long TargetVideoId { get; private set; }
        public long ControlVideoId { get; private set; }
        public long ControlSourceId { get; private set; }
        public CreatorStyleStatedRuleStore Rules { get; private set; } = null!;
        public CreatorStyleProfileStore Profiles { get; private set; } = null!;
        public CreatorDeckCacheStore Decks { get; private set; } = null!;
        public CreatorSourceStore Sources { get; private set; } = null!;
        public CreatorProfileSourceStore ProfileSources { get; private set; } = null!;
        public CreatorSuppressionStore Suppressions { get; private set; } = null!;
        public AdminContentKbController Controller { get; private set; } = null!;
        public CapturingLogger<AdminContentKbController> Logger { get; } = new();

        public static async Task<SuppressionFixture> CreateAsync()
        {
            var fixture = new SuppressionFixture();
            fixture.Suppressions = new CreatorSuppressionStore(RelationalDatabaseConnection.FromSqlitePath(fixture._path));
            fixture.Index = new ContentSiteIndexStore(fixture._path, fixture.Suppressions);
            var contentSources = new ContentSourceStore(fixture._path);
            fixture.Videos = new ContentVideoStore(fixture._path, fixture.Suppressions);
            fixture.Rules = new CreatorStyleStatedRuleStore(fixture._path);
            fixture.Profiles = new CreatorStyleProfileStore(fixture._path);
            fixture.Decks = new CreatorDeckCacheStore(fixture._path);
            fixture.Sources = new CreatorSourceStore(fixture._path);
            fixture.ProfileSources = new CreatorProfileSourceStore(fixture._path);
            var resolver = new CreatorIdentityResolver(fixture.Suppressions, contentSources, fixture.Index);
            await fixture.SeedAsync(contentSources);
            var purge = new CreatorPurgeService(fixture.Index, fixture.Videos, fixture.Rules, fixture.Profiles, fixture.Decks, fixture.Sources, fixture.ProfileSources);
            fixture.Whitelist = new CreatorWhitelistPoolBuilder(fixture.Decks, new NoopGroundingGuard(), new MemoryCache(new MemoryCacheOptions()));
            fixture.Controller = CreateController(fixture.Index, fixture.Suppressions, resolver, purge, fixture.Logger, fixture.Whitelist);
            return fixture;
        }

        public async Task MakeSuppressionTargetsVisibleAsync()
        {
            await Index.SetVisibilityBySourceAsync("3/3 Elk", true);
            await Index.SetVisibilityByCreatorAsync(new CreatorIdentity("folder-target", [], [], ["3-3-elk-old"]), true);
        }

        public async Task AssertTargetPurgedAsync()
        {
            Assert.DoesNotContain((await Index.GetAllRowsAsync()), row => row.Source == "3/3 Elk" || row.ArtifactPath.Contains("3-3-elk-old", StringComparison.Ordinal));
            Assert.Empty(await Rules.GetBySlugAsync("elk-canon"));
            Assert.Empty(await Rules.GetBySlugAsync("3-3-elk-old"));
            Assert.Null(await Profiles.GetBySlugAsync("elk-canon"));
            Assert.Empty(await Decks.GetByCreatorAsync("elk-canon"));
            Assert.DoesNotContain(await Sources.ListAsync(), row => row.DisplayName.Contains("Elk", StringComparison.Ordinal));
            Assert.Null(await ProfileSources.GetBySlugAsync("elk-canon"));
            Assert.Null(await Videos.GetVideoByYoutubeIdAsync(1, "elk-video"));
            Assert.Equal(0, await Videos.CountClipsByVideoAsync(TargetVideoId));
        }

        public async Task AssertTargetPresentAsync()
        {
            Assert.Contains(await Index.GetAllRowsAsync(), row => row.Source == "3/3 Elk");
            Assert.NotEmpty(await Rules.GetBySlugAsync("elk-canon"));
            Assert.NotNull(await Profiles.GetBySlugAsync("elk-canon"));
            Assert.NotEmpty(await Decks.GetByCreatorAsync("elk-canon"));
            Assert.Contains(await Sources.ListAsync(), row => row.DisplayName == "3/3 Elk");
            Assert.NotNull(await ProfileSources.GetBySlugAsync("elk-canon"));
            Assert.NotNull(await Videos.GetVideoByYoutubeIdAsync(1, "elk-video"));
            Assert.Equal(1, await Videos.CountClipsByVideoAsync(TargetVideoId));
        }

        public async Task AssertControlPresentAsync()
        {
            Assert.Contains((await Index.GetAllRowsAsync()), row => row.Source == "Control Person");
            Assert.Single(await Rules.GetBySlugAsync("control-canon"));
            Assert.NotNull(await Profiles.GetBySlugAsync("control-canon"));
            Assert.Single(await Decks.GetByCreatorAsync("control-canon"));
            Assert.Contains((await Sources.ListAsync()), row => row.DisplayName == "Control Person");
            Assert.NotNull(await ProfileSources.GetBySlugAsync("control-canon"));
            Assert.NotNull(await Videos.GetVideoByYoutubeIdAsync(ControlSourceId, "control-video"));
            Assert.Equal(1, await Videos.CountClipsByVideoAsync(ControlVideoId));
        }

        public void ReplacePurgeStepWithFailure(string failedStore)
        {
            var steps = new CreatorPurgeStep[]
            {
                Step("site-index", Index.DeleteBySourceAsync), Step("videos", Videos.DeleteByCreatorAsync),
                Step("stated-rules", Rules.DeleteByCreatorAsync), Step("profile", Profiles.DeleteByCreatorAsync),
                Step("deck-cache", Decks.DeleteByCreatorAsync), Step("creator-sources", Sources.DeleteByCreatorAsync),
                Step("profile-source", ProfileSources.DeleteByCreatorAsync),
            };
            var purge = new CreatorPurgeService(steps.Select(step => step.StoreName == failedStore
                ? new CreatorPurgeStep(step.StoreName, (_, _) => throw new InvalidOperationException($"boom-{failedStore}")) : step).ToArray());
            Controller = CreateController(Index, Suppressions, new CreatorIdentityResolver(Suppressions, new ContentSourceStore(_path), Index), purge, Logger,
                new CreatorWhitelistPoolBuilder(Decks, new NoopGroundingGuard(), new MemoryCache(new MemoryCacheOptions())));
        }

        public async Task AssertOnlyFailedTargetRemainsAsync(string failedStore)
        {
            if (failedStore == "site-index") Assert.Contains(await Index.GetAllRowsAsync(), row => row.Source == "3/3 Elk");
            else Assert.DoesNotContain(await Index.GetAllRowsAsync(), row => row.Source == "3/3 Elk");
            if (failedStore == "stated-rules") Assert.NotEmpty(await Rules.GetBySlugAsync("elk-canon")); else Assert.Empty(await Rules.GetBySlugAsync("elk-canon"));
            if (failedStore == "profile") Assert.NotNull(await Profiles.GetBySlugAsync("elk-canon")); else Assert.Null(await Profiles.GetBySlugAsync("elk-canon"));
            if (failedStore == "deck-cache") Assert.NotEmpty(await Decks.GetByCreatorAsync("elk-canon")); else Assert.Empty(await Decks.GetByCreatorAsync("elk-canon"));
            if (failedStore == "creator-sources") Assert.Contains(await Sources.ListAsync(), row => row.DisplayName == "3/3 Elk"); else Assert.DoesNotContain(await Sources.ListAsync(), row => row.DisplayName.Contains("Elk", StringComparison.Ordinal));
            if (failedStore == "profile-source") Assert.NotNull(await ProfileSources.GetBySlugAsync("elk-canon")); else Assert.Null(await ProfileSources.GetBySlugAsync("elk-canon"));
            if (failedStore == "videos")
            {
                Assert.NotNull(await Videos.GetVideoByYoutubeIdAsync(1, "elk-video"));
                Assert.Equal(1, await Videos.CountClipsByVideoAsync(TargetVideoId));
            }
            else
            {
                Assert.Null(await Videos.GetVideoByYoutubeIdAsync(1, "elk-video"));
                Assert.Equal(0, await Videos.CountClipsByVideoAsync(TargetVideoId));
            }
        }

        private static CreatorPurgeStep Step(string name, Func<CreatorIdentity, CancellationToken, Task<int>> deleteAsync) => new(name, deleteAsync);

        private async Task SeedAsync(ContentSourceStore contentSources)
        {
            await Suppressions.SuppressAsync("elk-canon", ["3/3 Elk", "3-3-elk-old"], "seed", DateTimeOffset.UtcNow, null);
            await Index.UpsertRowAsync(IndexRow("3/3 Elk", "content-kb/3-3-elk-old/elk.md", "elk"));
            await Index.UpsertRowAsync(IndexRow("3/3 Elk", "content-kb/a_b/folder-only.md", "folder-only"));
            await Index.UpsertRowAsync(IndexRow("3/3 ELK", "content-kb/3-3-elk-old/case-variant.md", "folder-slug-only"));
            await Index.UpsertRowAsync(IndexRow("Folder control", "content-kb/a-b/folder-control.md", "folder-hyphen-control"));
            await Index.UpsertRowAsync(IndexRow("Unrelated creator", "content-kb/unrelated-creator/control.md", "unrelated-control"));
            await Index.UpsertRowAsync(IndexRow("Control Person", "content-kb/control-old/control.md", "control"));
            var elkId = await contentSources.InsertSourceAsync("elk-canon", "3/3 Elk", "youtube_channel", "https://example.test/elk");
            var elkSecondId = await contentSources.InsertSourceAsync("second-elk-source", "3/3 Elk", "youtube_channel", "https://example.test/elk-second");
            ControlSourceId = await contentSources.InsertSourceAsync("control-canon", "Control Person", "youtube_channel", "https://example.test/control");
            TargetVideoId = await Videos.InsertVideoAsync(elkId, "elk-video", null, "Elk", "https://example.test/elk-video", null, TranscriptStatus.Pending);
            ControlVideoId = await Videos.InsertVideoAsync(ControlSourceId, "control-video", null, "Control", "https://example.test/control-video", null, TranscriptStatus.Pending);
            await Videos.InsertClipAsync(TargetVideoId, 1, "target clip", 1);
            await Videos.InsertClipAsync(ControlVideoId, 1, "control clip", 1);
            await Rules.UpsertAsync(Rule(), "elk-canon"); await Rules.UpsertAsync(Rule(), "3-3-elk-old"); await Rules.UpsertAsync(Rule(), "control-canon");
            await Profiles.UpsertAsync(Profile("elk-canon")); await Profiles.UpsertAsync(Profile("control-canon"));
            await Decks.UpsertAsync(Deck("elk-canon", "elk")); await Decks.UpsertAsync(Deck("control-canon", "control"));
            await Sources.AddAsync("3/3 Elk", "https://youtube.com/@elk"); await Sources.AddAsync("Elk Second", "https://youtube.com/@elk2"); await Sources.AddAsync("Control Person", "https://youtube.com/@control");
            var creators = await Sources.ListAsync();
            await Sources.LinkContentSourceAsync(creators.Single(row => row.DisplayName == "3/3 Elk").Id, elkId, "elk-canon");
            await Sources.LinkContentSourceAsync(creators.Single(row => row.DisplayName == "Elk Second").Id, elkSecondId, "other-slug");
            await Sources.LinkContentSourceAsync(creators.Single(row => row.DisplayName == "Control Person").Id, ControlSourceId, "control-canon");
            await ProfileSources.UpsertAsync(ProfileSource("elk-canon")); await ProfileSources.UpsertAsync(ProfileSource("control-canon"));
        }

        private static AdminContentKbController CreateController(ContentSiteIndexStore index, ICreatorSuppressionStore suppressions, ICreatorIdentityResolver resolver, CreatorPurgeService purge, CapturingLogger<AdminContentKbController> logger, CreatorWhitelistPoolBuilder whitelist)
        {
            var controller = new AdminContentKbController(index, new FakeContentKbSeedLoader(), new FakeFeatureFlagCache(new Dictionary<string, bool>()), new PublishStateDeriver(), logger, suppressions, resolver, purge, whitelist);
            var context = new DefaultHttpContext();
            context.Request.Scheme = "https"; context.Request.Host = new HostString("deckflow.test"); context.Request.Headers.Origin = "https://deckflow.test";
            controller.ControllerContext = new ControllerContext { HttpContext = context };
            controller.TempData = new TempDataDictionary(context, new TempDataProvider());
            return controller;
        }

        private static ContentSiteIndexRow IndexRow(string source, string path, string id) => new() { Id = 0, Source = source, Title = id, VideoUrl = $"https://example.test/{id}", ArtifactPath = path, IndexedUtc = DateTimeOffset.Parse("2026-09-23T00:00:00Z"), ArchetypeTags = [], BracketTags = [], CardCategoryTags = [], YoutubeVideoId = id, IsVisible = true };
        private static StatedRuleCandidate Rule() => new() { Category = "curve", Metric = "avg_cmc", Comparator = "<=", SourceClip = "Keep curve low.", Confidence = .9, VideoDateUtc = DateTimeOffset.Parse("2026-09-23T00:00:00Z") };
        private static CreatorStyleProfile Profile(string slug) => new() { Slug = slug, Platform = "archidekt", MinDecks = 5, UpdatedUtc = DateTimeOffset.Parse("2026-09-23T00:00:00Z") };
        private static CreatorDeckCacheEntry Deck(string slug, string id) => new() { CreatorSlug = slug, DeckId = id, ContentHash = $"hash-{id}", Size = 100, ConfidenceMarker = "measured", Entries = [new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard" }], CachedUtc = DateTimeOffset.Parse("2026-09-23T00:00:00Z") };
        private static CreatorProfileSource ProfileSource(string slug) => new() { Slug = slug, Platform = "archidekt", ProfileUsername = slug, ProfileUrl = $"https://archidekt.com/u/{slug}", UpdatedUtc = DateTimeOffset.Parse("2026-09-23T00:00:00Z") };

        public void Dispose()
        {
            if (File.Exists(_path)) { SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(_path)}")); GC.Collect(); GC.WaitForPendingFinalizers(); File.Delete(_path); }
        }

        private sealed class NoopGroundingGuard : ICardGroundingGuard
        {
            public Task<CardGroundingVerdict> TryValidateAsync(string candidateName, CardGroundingDeckContext deckContext, CancellationToken cancellationToken = default) => Task.FromResult(new CardGroundingVerdict { Accepted = true, CanonicalName = candidateName, RejectReason = CardGroundingRejectReason.None });
            public Task<CardGroundingBatchResult> ValidateAllAsync(IReadOnlyList<string> candidateNames, CardGroundingDeckContext deckContext, CancellationToken cancellationToken = default) => Task.FromResult(new CardGroundingBatchResult { Verdicts = candidateNames.Select(candidateName => new CardGroundingVerdict { Accepted = true, CanonicalName = candidateName, RejectReason = CardGroundingRejectReason.None }).ToArray(), HasUpstreamFailure = false });
        }
    }
}
