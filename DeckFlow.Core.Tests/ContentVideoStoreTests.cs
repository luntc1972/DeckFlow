using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

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
    public async Task UpdateTranscriptStatusAsync_ChangesPendingVideoToCaptions()
    {
        var sourceId = await InsertSourceAsync("captions-status-source");
        var videoId = await _store.InsertVideoAsync(
            sourceId,
            "captions-status-video",
            null,
            "Captions Status Video",
            "https://www.youtube.com/watch?v=captions-status-video",
            null,
            TranscriptStatus.Pending);

        await _store.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.Captions);

        Assert.Equal(TranscriptStatus.Captions, await ReadTranscriptStatusAsync(videoId));
    }

    [Fact]
    public async Task UpdateTranscriptStatusAsync_ChangesPendingVideoToSkippedOverCap()
    {
        var sourceId = await InsertSourceAsync("cap-status-source");
        var videoId = await _store.InsertVideoAsync(
            sourceId,
            "cap-status-video",
            null,
            "Cap Status Video",
            "https://www.youtube.com/watch?v=cap-status-video",
            null,
            TranscriptStatus.Pending);

        await _store.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.SkippedOverCap);

        Assert.Equal(TranscriptStatus.SkippedOverCap, await ReadTranscriptStatusAsync(videoId));
    }

    [Fact]
    public async Task UpdateTranscriptStatusAsync_ChangesPendingVideoToSkippedNoCaptions()
    {
        var sourceId = await InsertSourceAsync("no-captions-status-source");
        var videoId = await _store.InsertVideoAsync(
            sourceId,
            "no-captions-status-video",
            null,
            "No Captions Status Video",
            "https://www.youtube.com/watch?v=no-captions-status-video",
            null,
            TranscriptStatus.Pending);

        await _store.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.SkippedNoCaptions);

        Assert.Equal(TranscriptStatus.SkippedNoCaptions, await ReadTranscriptStatusAsync(videoId));
    }

    [Fact]
    public async Task UpdateTranscriptStatusAsync_RejectsUnknownStatusBeforeOpeningDatabase()
    {
        var guardedDbPath = Path.Combine(Path.GetTempPath(), $"content-video-guard-{Guid.NewGuid():N}.db");
        var guardedStore = new ContentVideoStore(guardedDbPath);

        try
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                guardedStore.UpdateTranscriptStatusAsync(42, "not-a-status"));

            Assert.Contains("Unknown transcript status", exception.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(guardedDbPath));
        }
        finally
        {
            if (File.Exists(guardedDbPath))
            {
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.Delete(guardedDbPath);
            }
        }
    }

    [Fact]
    public async Task UpdateTranscriptStatusAsync_MissingVideoIdDoesNotThrow()
    {
        await _store.UpdateTranscriptStatusAsync(987_654, TranscriptStatus.Failed);
    }

    [Fact]
    public async Task GetVideoByYoutubeIdAsync_ReturnsExistingVideoWithCurrentStatus()
    {
        var sourceId = await InsertSourceAsync("resume-source");
        var videoId = await _store.InsertVideoAsync(
            sourceId,
            "abc123",
            null,
            "Resume Video",
            "https://www.youtube.com/watch?v=abc123",
            DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            TranscriptStatus.Pending);
        await _store.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.Whisper);

        var video = await _store.GetVideoByYoutubeIdAsync(sourceId, "abc123");

        Assert.NotNull(video);
        Assert.Equal(videoId, video!.Id);
        Assert.Equal(TranscriptStatus.Whisper, video.TranscriptStatus);
    }

    [Fact]
    public async Task GetVideoByYoutubeIdAsync_ReturnsNullWhenVideoIsMissing()
    {
        var sourceId = await InsertSourceAsync("missing-resume-source");

        var video = await _store.GetVideoByYoutubeIdAsync(sourceId, "never-inserted");

        Assert.Null(video);
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

    private async Task<string> ReadTranscriptStatusAsync(long videoId)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT transcript_status
              FROM content_videos
             WHERE id = @videoId;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);

        var status = await command.ExecuteScalarAsync();
        return Assert.IsType<string>(status);
    }

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
