using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for <see cref="BlockedVideoStore"/> using a temporary SQLite content KB database.
/// </summary>
public sealed class BlockedVideoStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly BlockedVideoStore _store;

    public BlockedVideoStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"blocked-video-test-{Guid.NewGuid():N}.db");
        _store = new BlockedVideoStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task AddBlockAsync_AddsRow_AndTracksBlockedState()
    {
        await _store.AddBlockAsync("vid-1", "spam");

        Assert.True(await _store.IsBlockedAsync("vid-1"));
        Assert.False(await _store.IsBlockedAsync("vid-2"));
    }

    [Fact]
    public async Task AddBlockAsync_DuplicateId_IsIdempotent_AndListsOnce()
    {
        await _store.AddBlockAsync("vid-1", "spam");
        await _store.AddBlockAsync("vid-1", "duplicate");

        var blocked = await _store.ListBlockedAsync();

        var row = Assert.Single(blocked);
        Assert.Equal("vid-1", row.YoutubeVideoId);
    }

    [Fact]
    public async Task RemoveBlockAsync_RemovesExistingRow()
    {
        await _store.AddBlockAsync("vid-1", "spam");

        var removed = await _store.RemoveBlockAsync("vid-1");

        Assert.True(removed);
        Assert.False(await _store.IsBlockedAsync("vid-1"));
    }

    [Fact]
    public async Task ListBlockedAsync_ReturnsReason_AndBlockedUtc()
    {
        await _store.AddBlockAsync("vid-1", "spam");
        await _store.AddBlockAsync("vid-2", null);

        var blocked = await _store.ListBlockedAsync();

        Assert.Collection(
            blocked.OrderBy(row => row.YoutubeVideoId),
            first =>
            {
                Assert.Equal("vid-1", first.YoutubeVideoId);
                Assert.Equal("spam", first.Reason);
                Assert.NotEqual(default, first.BlockedUtc);
            },
            second =>
            {
                Assert.Equal("vid-2", second.YoutubeVideoId);
                Assert.Null(second.Reason);
                Assert.NotEqual(default, second.BlockedUtc);
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddBlockAsync_BlankId_ThrowsArgumentException(string? youtubeVideoId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _store.AddBlockAsync(youtubeVideoId!, "spam"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_UsesBlockedVideosTableName_ForPhase37Point5ResetCarveOut()
    {
        await _store.EnsureSchemaAsync();

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
              FROM sqlite_master
             WHERE type = 'table'
               AND name = 'blocked_videos';
            """;

        var result = await command.ExecuteScalarAsync();

        Assert.Equal(1L, (long)result!);
    }

    [Fact]
    public async Task DeleteVideoByYoutubeIdAsync_DeletesDisabledSourceVideo_AndReturnsZeroForUnknownId()
    {
        var sourceStore = new ContentSourceStore(_dbPath);
        var videoStore = new ContentVideoStore(_dbPath);
        var sourceId = await sourceStore.InsertSourceAsync(
            "disabled-source",
            "Disabled Source",
            ContentSourceType.Youtube,
            "https://www.youtube.com/@disabled-source");
        await sourceStore.SetEnabledAsync(sourceId, isEnabled: false);
        await videoStore.InsertVideoAsync(
            sourceId,
            "disabled-video",
            rssGuid: null,
            "Disabled Video",
            "https://www.youtube.com/watch?v=disabled-video",
            DateTimeOffset.Parse("2026-06-10T12:00:00Z"),
            TranscriptStatus.Pending);

        var deleted = await videoStore.DeleteVideoByYoutubeIdAsync("disabled-video");
        var missingDeleted = await videoStore.DeleteVideoByYoutubeIdAsync("missing-video");
        var video = await videoStore.GetVideoByYoutubeIdAsync(sourceId, "disabled-video");

        Assert.Equal(1, deleted);
        Assert.Equal(0, missingDeleted);
        Assert.Null(video);
    }

    [Fact]
    public async Task DeleteByIdAsync_RemovesSiteIndexRow_AndReturnsZeroForMissingId()
    {
        var siteIndexStore = new ContentSiteIndexStore(_dbPath);
        await siteIndexStore.UpsertRowAsync(CreateYoutubeRow("yt-delete"));
        var row = await siteIndexStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-delete");

        var deleted = await siteIndexStore.DeleteByIdAsync(row!.Id);
        var missingDeleted = await siteIndexStore.DeleteByIdAsync(987_654);
        var deletedRow = await siteIndexStore.GetByIdAsync(row.Id);

        Assert.Equal(1, deleted);
        Assert.Equal(0, missingDeleted);
        Assert.Null(deletedRow);
    }

    private static ContentSiteIndexRow CreateYoutubeRow(string youtubeVideoId)
        => new()
        {
            Id = 0,
            Source = "The Command Zone",
            Title = $"Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath = $"content-kb/command-zone/{youtubeVideoId}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = new[] { "combo", "control" },
            BracketTags = new[] { "cEDH", "Optimized" },
            CardCategoryTags = new[] { "win-cons", "counter" },
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null
        };
}
