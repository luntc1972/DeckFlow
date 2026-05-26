using System.IO;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Services.Content;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Integration tests for <see cref="ContentVideoStore"/> using a temporary SQLite content KB database.
/// </summary>
public sealed class ContentVideoStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSourceStore _sourceStore;
    private readonly ContentVideoStore _store;

    public ContentVideoStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-video-test-{Guid.NewGuid():N}.db");
        _sourceStore = new ContentSourceStore(_dbPath);
        _store = new ContentVideoStore(_dbPath);
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
    public async Task EnsureSchemaAsync_IsIdempotent()
    {
        await _store.EnsureSchemaAsync();
        await _store.EnsureSchemaAsync();
    }

    [Fact]
    public async Task EnsureSchemaAsync_CreatesContentSourcesBeforeVideoTables()
    {
        await _store.EnsureSchemaAsync();

        var sourceId = await InsertSourceRowDirectlyAsync("bootstrap-source");
        var videoId = await _store.InsertVideoAsync(
            sourceId,
            "bootstrap-video",
            null,
            "Bootstrap Video",
            "https://www.youtube.com/watch?v=bootstrap-video",
            DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            TranscriptStatus.Pending);

        Assert.True(videoId > 0);
    }

    [Fact]
    public async Task DeleteVideoAsync_CascadesAllChildTables()
    {
        var sourceId = await InsertSourceAsync("cascade-source");
        var videoId = await _store.InsertVideoAsync(
            sourceId,
            "cascade-video",
            null,
            "Cascade Video",
            "https://www.youtube.com/watch?v=cascade-video",
            null,
            TranscriptStatus.Captions);
        await _store.InsertTranscriptAsync(videoId, TranscriptSource.Captions, "Full transcript body.");
        await _store.InsertSummaryAsync(videoId, "Summary body.");
        await _store.InsertClipAsync(videoId, 93, "A useful clip excerpt.", 1);
        await _store.InsertTagAsync(videoId, ContentTagDimension.Archetype, "Turbo Naus");

        Assert.Equal(1, await _store.CountTranscriptsByVideoAsync(videoId));
        Assert.Equal(1, await _store.CountSummariesByVideoAsync(videoId));
        Assert.Equal(1, await _store.CountClipsByVideoAsync(videoId));
        Assert.Equal(1, await _store.CountTagsByVideoAsync(videoId));

        await _store.DeleteVideoAsync(videoId);

        Assert.Equal(0, await _store.CountTranscriptsByVideoAsync(videoId));
        Assert.Equal(0, await _store.CountSummariesByVideoAsync(videoId));
        Assert.Equal(0, await _store.CountClipsByVideoAsync(videoId));
        Assert.Equal(0, await _store.CountTagsByVideoAsync(videoId));
    }

    [Fact]
    public async Task InsertVideoAsync_RequiresExactlyOneNaturalKey()
    {
        var sourceId = await InsertSourceAsync("natural-key-source");

        await Assert.ThrowsAsync<ArgumentException>(() => _store.InsertVideoAsync(
            sourceId,
            null,
            null,
            "No Natural Key",
            "https://example.test/no-key",
            null,
            TranscriptStatus.Pending));

        await Assert.ThrowsAsync<ArgumentException>(() => _store.InsertVideoAsync(
            sourceId,
            "both-video",
            "both-guid",
            "Both Natural Keys",
            "https://example.test/both",
            null,
            TranscriptStatus.Pending));

        var youtubeOnlyId = await _store.InsertVideoAsync(
            sourceId,
            "youtube-only-video",
            null,
            "YouTube Only",
            "https://www.youtube.com/watch?v=youtube-only-video",
            null,
            TranscriptStatus.Pending);
        var rssOnlyId = await _store.InsertVideoAsync(
            sourceId,
            null,
            "rss-only-guid",
            "RSS Only",
            "https://example.test/rss-only",
            null,
            TranscriptStatus.Pending);

        Assert.True(youtubeOnlyId > 0);
        Assert.True(rssOnlyId > 0);
    }

    [Fact]
    public async Task ContentVideosTable_RejectsBothNaturalKeys()
    {
        await _store.EnsureSchemaAsync();
        var sourceId = await InsertSourceAsync("direct-natural-key-source");

        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO content_videos (
              source_id,
              youtube_video_id,
              rss_guid,
              title,
              video_url,
              transcript_status)
            VALUES (
              @sourceId,
              @youtubeVideoId,
              @rssGuid,
              @title,
              @videoUrl,
              @transcriptStatus);
            """;
        RelationalDatabaseConnection.AddParameter(command, "@sourceId", sourceId);
        RelationalDatabaseConnection.AddParameter(command, "@youtubeVideoId", "direct-both-video");
        RelationalDatabaseConnection.AddParameter(command, "@rssGuid", "direct-both-guid");
        RelationalDatabaseConnection.AddParameter(command, "@title", "Direct Both Natural Keys");
        RelationalDatabaseConnection.AddParameter(command, "@videoUrl", "https://example.test/direct-both");
        RelationalDatabaseConnection.AddParameter(command, "@transcriptStatus", TranscriptStatus.Pending);

        await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task InsertTagAsync_RejectsDuplicateDimensionValueForVideo()
    {
        var sourceId = await InsertSourceAsync("tag-source");
        var videoId = await _store.InsertVideoAsync(
            sourceId,
            null,
            "tag-guid",
            "Tagged Episode",
            "https://example.test/tagged",
            null,
            TranscriptStatus.Pending);
        await _store.InsertTagAsync(videoId, ContentTagDimension.Bracket, "Bracket 4");

        await Assert.ThrowsAsync<SqliteException>(() => _store.InsertTagAsync(
            videoId,
            ContentTagDimension.Bracket,
            "Bracket 4"));
    }

    private async Task<long> InsertSourceAsync(string slug)
        => await _sourceStore.InsertSourceAsync(
            slug,
            $"Source {slug}",
            ContentSourceType.Youtube,
            $"https://example.test/{slug}");

    private async Task<long> InsertSourceRowDirectlyAsync(string slug)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO content_sources (source_slug, display_name, source_type, source_url)
            VALUES (@sourceSlug, @displayName, @sourceType, @sourceUrl)
            RETURNING id;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@sourceSlug", slug);
        RelationalDatabaseConnection.AddParameter(command, "@displayName", $"Source {slug}");
        RelationalDatabaseConnection.AddParameter(command, "@sourceType", ContentSourceType.Podcast);
        RelationalDatabaseConnection.AddParameter(command, "@sourceUrl", $"https://example.test/{slug}.xml");

        var id = await command.ExecuteScalarAsync();
        return Convert.ToInt64(id);
    }
}
