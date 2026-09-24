using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using DeckFlow.Core.Models;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

public sealed class CreatorPurgeStoreTests : IDisposable
{
    private static readonly CreatorIdentity Target = new(
        "elk-canon",
        [],
        ["3/3 Elk"],
        ["3-3-elk-old"]);
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"creator-purge-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(_dbPath)}"));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task ContentSiteIndexStore_DeleteBySourceAsync_RemovesDisplayNameAndFolderRowsOnly()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertRowAsync(CreateIndexRow("3/3 Elk", "content-kb/other/elk-display.md", "display"));
        await store.UpsertRowAsync(CreateIndexRow("Other Source", "content-kb/3-3-elk-old/elk-folder.md", "folder"));
        await store.UpsertRowAsync(CreateIndexRow("Control Person", "content-kb/control-old/3-3-elk-old.md", "control"));

        var deleted = await store.DeleteBySourceAsync(Target);
        var remaining = await store.GetAllRowsAsync();

        Assert.Equal(2, deleted);
        Assert.Single(remaining);
        Assert.Equal("control", remaining[0].YoutubeVideoId);
    }

    [Fact]
    public async Task ContentSiteIndexStore_DeleteBySourceAsync_MatchesFolderExactlyAndCaseSensitively()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        var identity = new CreatorIdentity("a_b", [], [], ["a_b"]);
        await store.UpsertRowAsync(CreateIndexRow("Other Source", "content-kb/a_b/target.md", "target"));
        await store.UpsertRowAsync(CreateIndexRow("Other Source", "content-kb/a-b/control.md", "hyphen-control"));
        await store.UpsertRowAsync(CreateIndexRow("Other Source", "content-kb/A_B/control.md", "case-control"));

        var deleted = await store.DeleteBySourceAsync(identity);
        var remaining = await store.GetAllRowsAsync();

        Assert.Equal(1, deleted);
        Assert.Equal(["case-control", "hyphen-control"], remaining.Select(row => row.YoutubeVideoId!).Order().ToArray());
    }

    [Fact]
    public async Task ContentVideoStore_DeleteByCreatorAsync_RemovesTargetVideosAndClipsOnly()
    {
        var sourceStore = new ContentSourceStore(_dbPath);
        var videoStore = new ContentVideoStore(_dbPath);
        var targetSourceId = await sourceStore.InsertSourceAsync("elk-canon", "3/3 Elk", "youtube_channel", "https://example.test/elk");
        var controlSourceId = await sourceStore.InsertSourceAsync("control-canon", "Control Person", "youtube_channel", "https://example.test/control");
        var targetVideoId = await videoStore.InsertVideoAsync(targetSourceId, "elk-video", null, "Elk", "https://example.test/elk-video", null, TranscriptStatus.Pending);
        var controlVideoId = await videoStore.InsertVideoAsync(controlSourceId, "control-video", null, "Control", "https://example.test/control-video", null, TranscriptStatus.Pending);
        await videoStore.InsertClipAsync(targetVideoId, 10, "first elk clip", 1);
        await videoStore.InsertClipAsync(targetVideoId, 20, "second elk clip", 2);
        await videoStore.InsertClipAsync(controlVideoId, 10, "control clip", 1);

        var deleted = await videoStore.DeleteByCreatorAsync(Target with { SourceIds = [targetSourceId] });

        Assert.Equal(3, deleted);
        Assert.Null(await videoStore.GetVideoByYoutubeIdAsync(targetSourceId, "elk-video"));
        Assert.Equal(0, await videoStore.CountClipsByVideoAsync(targetVideoId));
        Assert.NotNull(await videoStore.GetVideoByYoutubeIdAsync(controlSourceId, "control-video"));
        Assert.Equal(1, await videoStore.CountClipsByVideoAsync(controlVideoId));
    }

    [Fact]
    public async Task ContentVideoStore_DeleteByCreatorAsync_WhenVideoDeleteFails_RollsBackClips()
    {
        var sourceStore = new ContentSourceStore(_dbPath);
        var videoStore = new ContentVideoStore(_dbPath);
        var targetSourceId = await sourceStore.InsertSourceAsync("elk-canon", "3/3 Elk", "youtube_channel", "https://example.test/elk");
        var targetVideoId = await videoStore.InsertVideoAsync(targetSourceId, "elk-video", null, "Elk", "https://example.test/elk-video", null, TranscriptStatus.Pending);
        await videoStore.InsertClipAsync(targetVideoId, 10, "elk clip", 1);
        await using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(_dbPath)}");
        await connection.OpenAsync();
        await using var trigger = connection.CreateCommand();
        trigger.CommandText = $"CREATE TRIGGER abort_target_video BEFORE DELETE ON content_videos WHEN OLD.id = {targetVideoId} BEGIN SELECT RAISE(ABORT, 'boom'); END;";
        await trigger.ExecuteNonQueryAsync();

        var exception = await Assert.ThrowsAsync<SqliteException>(() => videoStore.DeleteByCreatorAsync(Target with { SourceIds = [targetSourceId] }));

        Assert.Contains("boom", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, await videoStore.CountClipsByVideoAsync(targetVideoId));
    }

    [Fact]
    public async Task CreatorSourceStore_DeleteByCreatorAsync_RemovesSlugAndContentSourceMatchesOnly()
    {
        var sourceStore = new ContentSourceStore(_dbPath);
        var store = new CreatorSourceStore(_dbPath);
        var targetSourceId = await sourceStore.InsertSourceAsync("source-elk", "Source Elk", "youtube_channel", "https://example.test/source-elk");
        var controlSourceId = await sourceStore.InsertSourceAsync("source-control", "Source Control", "youtube_channel", "https://example.test/source-control");
        await store.AddAsync("Slug Match", "https://youtube.com/@slug-match");
        await store.AddAsync("Source Match", "https://youtube.com/@source-match");
        await store.AddAsync("3/3 Elk", "https://youtube.com/@display-match");
        await store.AddAsync("Control Person", "https://youtube.com/@control");
        var creators = await store.ListAsync();
        await store.LinkContentSourceAsync(creators.Single(row => row.DisplayName == "Slug Match").Id, controlSourceId, "elk-canon");
        await store.LinkContentSourceAsync(creators.Single(row => row.DisplayName == "Source Match").Id, targetSourceId, "other-slug");
        await store.LinkContentSourceAsync(creators.Single(row => row.DisplayName == "Control Person").Id, controlSourceId, "control-canon");

        var deleted = await store.DeleteByCreatorAsync(Target with { SourceIds = [targetSourceId] });
        var remaining = await store.ListAsync();

        Assert.Equal(3, deleted);
        Assert.Single(remaining);
        Assert.Equal("Control Person", remaining[0].DisplayName);
    }

    [Fact]
    public async Task CreatorStyleStatedRuleStore_DeleteByCreatorAsync_RemovesCanonicalAndHistoricalSlugsOnly()
    {
        var store = new CreatorStyleStatedRuleStore(_dbPath);
        await store.UpsertAsync(CreateRule(), "elk-canon");
        await store.UpsertAsync(CreateRule(), "3-3-elk-old");
        await store.UpsertAsync(CreateRule(), "control-canon");

        var deleted = await store.DeleteByCreatorAsync(Target);

        Assert.Equal(2, deleted);
        Assert.Empty(await store.GetBySlugAsync("elk-canon"));
        Assert.Empty(await store.GetBySlugAsync("3-3-elk-old"));
        Assert.Single(await store.GetBySlugAsync("control-canon"));
    }

    [Fact]
    public async Task CreatorStyleProfileStore_DeleteByCreatorAsync_RemovesCanonicalAndHistoricalSlugsOnly()
    {
        var store = new CreatorStyleProfileStore(_dbPath);
        await store.UpsertAsync(CreatorStyleProfileTestData.CreateFullProfile("elk-canon"));
        await store.UpsertAsync(CreatorStyleProfileTestData.CreateFullProfile("3-3-elk-old"));
        await store.UpsertAsync(CreatorStyleProfileTestData.CreateFullProfile("control-canon"));

        var deleted = await store.DeleteByCreatorAsync(Target);

        Assert.Equal(2, deleted);
        Assert.Null(await store.GetBySlugAsync("elk-canon"));
        Assert.Null(await store.GetBySlugAsync("3-3-elk-old"));
        Assert.NotNull(await store.GetBySlugAsync("control-canon"));
    }

    [Fact]
    public async Task CreatorDeckCacheStore_DeleteByCreatorAsync_RemovesCanonicalAndHistoricalSlugsOnly()
    {
        var store = new CreatorDeckCacheStore(_dbPath);
        await store.UpsertAsync(CreateDeckCacheEntry("elk-canon", "target-canonical"));
        await store.UpsertAsync(CreateDeckCacheEntry("3-3-elk-old", "target-historical"));
        await store.UpsertAsync(CreateDeckCacheEntry("control-canon", "control"));

        var deleted = await store.DeleteByCreatorAsync(Target);

        Assert.Equal(2, deleted);
        Assert.Empty(await store.GetByCreatorAsync("elk-canon"));
        Assert.Empty(await store.GetByCreatorAsync("3-3-elk-old"));
        Assert.Single(await store.GetByCreatorAsync("control-canon"));
    }

    [Fact]
    public async Task CreatorProfileSourceStore_DeleteByCreatorAsync_RemovesCanonicalAndHistoricalSlugsOnly()
    {
        var store = new CreatorProfileSourceStore(_dbPath);
        await store.UpsertAsync(CreateProfileSource("elk-canon"));
        await store.UpsertAsync(CreateProfileSource("3-3-elk-old"));
        await store.UpsertAsync(CreateProfileSource("control-canon"));

        var deleted = await store.DeleteByCreatorAsync(Target);

        Assert.Equal(2, deleted);
        Assert.Null(await store.GetBySlugAsync("elk-canon"));
        Assert.Null(await store.GetBySlugAsync("3-3-elk-old"));
        Assert.NotNull(await store.GetBySlugAsync("control-canon"));
    }

    private static ContentSiteIndexRow CreateIndexRow(string source, string artifactPath, string youtubeVideoId)
        => new()
        {
            Id = 0,
            Source = source,
            Title = youtubeVideoId,
            VideoUrl = $"https://example.test/{youtubeVideoId}",
            ArtifactPath = artifactPath,
            IndexedUtc = DateTimeOffset.Parse("2026-09-23T00:00:00Z"),
            ArchetypeTags = [],
            BracketTags = [],
            CardCategoryTags = [],
            YoutubeVideoId = youtubeVideoId
        };

    private static StatedRuleCandidate CreateRule()
        => new()
        {
            Category = "curve",
            Metric = "avg_cmc",
            Comparator = "<=",
            SourceClip = "Keep the curve low.",
            Confidence = 0.9,
            VideoDateUtc = DateTimeOffset.Parse("2026-09-23T00:00:00Z")
        };

    private static CreatorDeckCacheEntry CreateDeckCacheEntry(string creatorSlug, string deckId)
        => new()
        {
            CreatorSlug = creatorSlug,
            DeckId = deckId,
            ContentHash = $"hash-{deckId}",
            Size = 100,
            ConfidenceMarker = "measured",
            Entries = [],
            CachedUtc = DateTimeOffset.Parse("2026-09-23T00:00:00Z")
        };

    private static CreatorProfileSource CreateProfileSource(string slug)
        => new()
        {
            Slug = slug,
            Platform = "archidekt",
            ProfileUsername = slug,
            ProfileUrl = $"https://archidekt.com/u/{slug}",
            UpdatedUtc = DateTimeOffset.Parse("2026-09-23T00:00:00Z")
        };
}
