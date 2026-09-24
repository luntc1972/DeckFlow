using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using DeckFlow.Core.Models;
using DeckFlow.Core.Storage;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>PostgreSQL integration coverage for creator-purge store deletes.</summary>
public sealed class CreatorPurgeStorePostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public CreatorPurgeStorePostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task ContentVideoStore_DeleteByCreatorAsync_RemovesTargetVideosAndClipsOnly()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var connection = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString);
        var sourceStore = new ContentSourceStore(connection);
        var videoStore = new ContentVideoStore(connection);
        var suffix = Guid.NewGuid().ToString("N");
        var targetSlug = $"purge-target-{suffix}";
        var controlSlug = $"purge-control-{suffix}";
        var targetSourceId = await sourceStore.InsertSourceAsync(targetSlug, "Purge Target", "youtube_channel", $"https://example.test/{targetSlug}");
        var controlSourceId = await sourceStore.InsertSourceAsync(controlSlug, "Purge Control", "youtube_channel", $"https://example.test/{controlSlug}");
        var targetVideoId = await videoStore.InsertVideoAsync(targetSourceId, $"target-{suffix}", null, "Target", "https://example.test/target", null, TranscriptStatus.Pending);
        var controlVideoId = await videoStore.InsertVideoAsync(controlSourceId, $"control-{suffix}", null, "Control", "https://example.test/control", null, TranscriptStatus.Pending);
        await videoStore.InsertClipAsync(targetVideoId, 1, "target clip", 1);
        await videoStore.InsertClipAsync(controlVideoId, 1, "control clip", 1);

        var deleted = await videoStore.DeleteByCreatorAsync(new CreatorIdentity(targetSlug, [targetSourceId], ["Purge Target"], []));

        Assert.Equal(2, deleted);
        Assert.Equal(0, await videoStore.CountClipsByVideoAsync(targetVideoId));
        Assert.Equal(1, await videoStore.CountClipsByVideoAsync(controlVideoId));
    }

    [PostgresFact]
    public async Task ContentSiteIndexStore_DeleteBySourceAsync_RemovesExactFolderAndDisplayNameRowsOnly()
    {
        var store = new ContentSiteIndexStore(await GetConnectionAsync());
        var identity = NewIdentity();
        await store.UpsertRowAsync(CreateIndexRow(identity.DisplayNames[0], "content-kb/other/display.md", "display"));
        await store.UpsertRowAsync(CreateIndexRow("Other", $"content-kb/{identity.FolderSlugs[0]}/target.md", "target"));
        await store.UpsertRowAsync(CreateIndexRow("Other", "content-kb/a-b/control.md", "hyphen-control"));
        await store.UpsertRowAsync(CreateIndexRow("Other", "content-kb/A_B/control.md", "case-control"));

        Assert.Equal(2, await store.DeleteBySourceAsync(identity));
        Assert.Equal(["case-control", "hyphen-control"], (await store.GetAllRowsAsync()).Where(row => row.YoutubeVideoId is "case-control" or "hyphen-control").Select(row => row.YoutubeVideoId!).Order().ToArray());
    }

    [PostgresFact]
    public async Task CreatorSourceStore_DeleteByCreatorAsync_RemovesDisplayNameAndTwoChannelRowsOnly()
    {
        var connection = await GetConnectionAsync();
        var sourceStore = new ContentSourceStore(connection);
        var store = new CreatorSourceStore(connection);
        var identity = NewIdentity();
        var targetSourceId = await sourceStore.InsertSourceAsync(identity.CanonicalSlug, identity.DisplayNames[0], "youtube_channel", $"https://example.test/{identity.CanonicalSlug}");
        var controlSourceId = await sourceStore.InsertSourceAsync($"control-{Guid.NewGuid():N}", "Control", "youtube_channel", "https://example.test/control");
        await store.AddAsync(identity.DisplayNames[0], "https://youtube.com/@display-only");
        await store.AddAsync("Two Channel", "https://youtube.com/@two-channel");
        await store.AddAsync("Control", "https://youtube.com/@control");
        var rows = await store.ListAsync();
        await store.LinkContentSourceAsync(rows.Single(row => row.DisplayName == "Two Channel").Id, targetSourceId, "other");
        await store.LinkContentSourceAsync(rows.Single(row => row.DisplayName == "Control").Id, controlSourceId, "control");

        Assert.Equal(2, await store.DeleteByCreatorAsync(identity with { SourceIds = [targetSourceId] }));
        Assert.Single(await store.ListAsync(), row => row.DisplayName == "Control");
    }

    [PostgresFact]
    public async Task CreatorDeckCacheStore_DeleteByCreatorAsync_RemovesTargetRowsOnly()
    {
        var store = new CreatorDeckCacheStore(await GetConnectionAsync());
        var identity = NewIdentity();
        await store.UpsertAsync(CreateDeckCache(identity.CanonicalSlug, "target"));
        await store.UpsertAsync(CreateDeckCache("control", Guid.NewGuid().ToString("N")));

        Assert.Equal(1, await store.DeleteByCreatorAsync(identity));
        Assert.Empty(await store.GetByCreatorAsync(identity.CanonicalSlug));
        Assert.NotEmpty(await store.GetByCreatorAsync("control"));
    }

    [PostgresFact]
    public async Task CreatorProfileSourceStore_DeleteByCreatorAsync_RemovesTargetRowsOnly()
    {
        var store = new CreatorProfileSourceStore(await GetConnectionAsync());
        var identity = NewIdentity();
        await store.UpsertAsync(CreateProfileSource(identity.CanonicalSlug));
        await store.UpsertAsync(CreateProfileSource("control"));

        Assert.Equal(1, await store.DeleteByCreatorAsync(identity));
        Assert.Null(await store.GetBySlugAsync(identity.CanonicalSlug));
        Assert.NotNull(await store.GetBySlugAsync("control"));
    }

    [PostgresFact]
    public async Task CreatorStyleProfileStore_DeleteByCreatorAsync_RemovesTargetRowsOnly()
    {
        var store = new CreatorStyleProfileStore(await GetConnectionAsync());
        var identity = NewIdentity();
        await store.UpsertAsync(CreateStyleProfile(identity.CanonicalSlug));
        await store.UpsertAsync(CreateStyleProfile("control"));

        Assert.Equal(1, await store.DeleteByCreatorAsync(identity));
        Assert.Null(await store.GetBySlugAsync(identity.CanonicalSlug));
        Assert.NotNull(await store.GetBySlugAsync("control"));
    }

    [PostgresFact]
    public async Task CreatorStyleStatedRuleStore_DeleteByCreatorAsync_RemovesTargetRowsOnly()
    {
        var store = new CreatorStyleStatedRuleStore(await GetConnectionAsync());
        var identity = NewIdentity();
        await store.UpsertAsync(CreateRule(), identity.CanonicalSlug);
        await store.UpsertAsync(CreateRule(), "control");

        Assert.Equal(1, await store.DeleteByCreatorAsync(identity));
        Assert.Empty(await store.GetBySlugAsync(identity.CanonicalSlug));
        Assert.NotEmpty(await store.GetBySlugAsync("control"));
    }

    private async Task<RelationalDatabaseConnection> GetConnectionAsync()
        => new(RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync());

    private static CreatorIdentity NewIdentity()
        => new($"purge-{Guid.NewGuid():N}", [], ["Purge Display"], ["a_b"]);

    private static ContentSiteIndexRow CreateIndexRow(string source, string artifactPath, string videoId)
        => new()
        {
            Id = 0,
            Source = source,
            Title = videoId,
            VideoUrl = $"https://example.test/{videoId}",
            ArtifactPath = artifactPath,
            IndexedUtc = DateTimeOffset.UtcNow,
            ArchetypeTags = [],
            BracketTags = [],
            CardCategoryTags = [],
            YoutubeVideoId = videoId
        };

    private static CreatorDeckCacheEntry CreateDeckCache(string slug, string deckId)
        => new() { CreatorSlug = slug, DeckId = deckId, ContentHash = $"hash-{deckId}", Size = 100, ConfidenceMarker = "measured", Entries = [], CachedUtc = DateTimeOffset.UtcNow };

    private static CreatorProfileSource CreateProfileSource(string slug)
        => new() { Slug = slug, Platform = "archidekt", ProfileUsername = slug, ProfileUrl = $"https://archidekt.com/u/{slug}", UpdatedUtc = DateTimeOffset.UtcNow };

    private static CreatorStyleProfile CreateStyleProfile(string slug)
        => new() { Slug = slug, Platform = "youtube", MinDecks = CreatorStyleProfile.MinDeckFloor, UpdatedUtc = DateTimeOffset.UtcNow };

    private static StatedRuleCandidate CreateRule()
        => new() { Category = "curve", Metric = "avg_cmc", Comparator = "<=", SourceClip = "Keep curve low.", Confidence = 0.9, VideoDateUtc = DateTimeOffset.UtcNow };
}
