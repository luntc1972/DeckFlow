using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for Content KB distillation helper methods on <see cref="ContentVideoStore"/>.
/// </summary>
public sealed class ContentVideoStoreDistillTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSourceStore _sourceStore;
    private readonly ContentVideoStore _videoStore;

    public ContentVideoStoreDistillTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-video-distill-{Guid.NewGuid():N}.db");
        _sourceStore = new ContentSourceStore(_dbPath);
        _videoStore = new ContentVideoStore(_dbPath);
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
    public async Task ListVideosPendingDistillAsync_IsScopedToSingleSource()
    {
        var sourceA = await InsertSourceAsync("source-a");
        var sourceB = await InsertSourceAsync("source-b");
        var disabledSource = await InsertSourceAsync("disabled-source");
        await _sourceStore.SetEnabledAsync(disabledSource, false);

        var sourceAVideo = await InsertVideoWithTranscriptAsync(sourceA, "video-a", TranscriptStatus.Captions);
        var sourceBVideo = await InsertVideoWithTranscriptAsync(sourceB, "video-b", TranscriptStatus.Captions);
        var disabledVideo = await InsertVideoWithTranscriptAsync(disabledSource, "disabled-video", TranscriptStatus.Captions);
        await InsertVideoWithTranscriptAsync(sourceA, "pending-video", TranscriptStatus.Pending);
        await InsertVideoWithoutTranscriptAsync(sourceA, "captions-without-transcript", TranscriptStatus.Captions);

        var sourceAPending = await _videoStore.ListVideosPendingDistillAsync(sourceA);
        var sourceBPending = await _videoStore.ListVideosPendingDistillAsync(sourceB);
        var disabledPending = await _videoStore.ListVideosPendingDistillAsync(disabledSource);

        Assert.Equal(sourceAVideo, Assert.Single(sourceAPending).Id);
        Assert.Equal(sourceBVideo, Assert.Single(sourceBPending).Id);
        Assert.Equal(disabledVideo, Assert.Single(disabledPending).Id);
    }

    [Fact]
    public async Task DistillStatusAsync_RoundTripsAndUpsertsPerVideoStatus()
    {
        var sourceId = await InsertSourceAsync("distill-status-source");
        var videoId = await InsertVideoWithTranscriptAsync(sourceId, "distill-status-video", TranscriptStatus.Captions);

        Assert.Null(await _videoStore.GetDistillStatusAsync(videoId));

        await _videoStore.SetDistillStatusAsync(videoId, "distilled");

        Assert.Equal("distilled", await _videoStore.GetDistillStatusAsync(videoId));
        Assert.Equal(1, await CountDistillStatusRowsAsync(videoId));

        await _videoStore.SetDistillStatusAsync(videoId, "failed");

        Assert.Equal("failed", await _videoStore.GetDistillStatusAsync(videoId));
        Assert.Equal(1, await CountDistillStatusRowsAsync(videoId));
        await Assert.ThrowsAsync<ArgumentException>(() => _videoStore.SetDistillStatusAsync(videoId, "not-a-distill-status"));
    }

    [Fact]
    public async Task GetLatestTranscriptAsync_ReturnsMostRecentTranscriptBody()
    {
        var sourceId = await InsertSourceAsync("latest-transcript-source");
        var videoId = await InsertVideoWithoutTranscriptAsync(sourceId, "latest-transcript-video", TranscriptStatus.Whisper);
        await _videoStore.InsertTranscriptAsync(videoId, TranscriptSource.Captions, "older transcript");
        await _videoStore.InsertTranscriptAsync(videoId, TranscriptSource.Whisper, "newer transcript");

        var transcript = await _videoStore.GetLatestTranscriptAsync(videoId);

        Assert.NotNull(transcript);
        Assert.Equal("newer transcript", transcript!.Body);
        Assert.Equal(TranscriptSource.Whisper, transcript.Source);
    }

    [Fact]
    public async Task ClearDistillOutputAsync_RemovesPriorSummaryClipAndTagRowsOnly()
    {
        var sourceId = await InsertSourceAsync("clear-output-source");
        var videoId = await InsertVideoWithTranscriptAsync(sourceId, "clear-output-video", TranscriptStatus.Captions);
        await _videoStore.InsertSummaryAsync(videoId, "summary");
        await _videoStore.InsertClipAsync(videoId, 42, "clip", 1);
        await _videoStore.InsertTagAsync(videoId, ContentTagDimension.Archetype, "combo");
        await _videoStore.SetDistillStatusAsync(videoId, "failed");

        await _videoStore.ClearDistillOutputAsync(videoId);

        Assert.Equal(1, await _videoStore.CountTranscriptsByVideoAsync(videoId));
        Assert.Equal(0, await _videoStore.CountSummariesByVideoAsync(videoId));
        Assert.Equal(0, await _videoStore.CountClipsByVideoAsync(videoId));
        Assert.Equal(0, await _videoStore.CountTagsByVideoAsync(videoId));
        Assert.Equal("failed", await _videoStore.GetDistillStatusAsync(videoId));
    }

    private async Task<long> InsertSourceAsync(string slug)
        => await _sourceStore.InsertSourceAsync(
            slug,
            $"Source {slug}",
            ContentSourceType.Youtube,
            $"https://example.test/{slug}");

    private async Task<long> InsertVideoWithTranscriptAsync(long sourceId, string youtubeVideoId, string transcriptStatus)
    {
        var videoId = await InsertVideoWithoutTranscriptAsync(sourceId, youtubeVideoId, transcriptStatus);
        await _videoStore.InsertTranscriptAsync(videoId, TranscriptSource.Captions, $"Transcript for {youtubeVideoId}.");
        return videoId;
    }

    private async Task<long> InsertVideoWithoutTranscriptAsync(long sourceId, string youtubeVideoId, string transcriptStatus)
        => await _videoStore.InsertVideoAsync(
            sourceId,
            youtubeVideoId,
            null,
            $"Video {youtubeVideoId}",
            $"https://www.youtube.com/watch?v={youtubeVideoId}",
            DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            transcriptStatus);

    private async Task<int> CountDistillStatusRowsAsync(long videoId)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
              FROM content_distill_status
             WHERE video_id = @videoId;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);

        var count = await command.ExecuteScalarAsync();
        return Convert.ToInt32(count);
    }
}
