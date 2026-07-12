using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using DeckFlow.Core.Storage;
using DeckFlow.Core.Tests.Integration;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for Content KB distillation helper methods on <see cref="ContentVideoStore"/>.
/// </summary>
public sealed class ContentVideoStoreDistillTests : IDisposable, IClassFixture<PostgresContainerFixture>
{
    private readonly string _dbPath;
    private readonly PostgresContainerFixture _fixture;
    private readonly ContentSourceStore _sourceStore;
    private readonly ContentVideoStore _videoStore;

    public ContentVideoStoreDistillTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
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
    public async Task ListVideosPendingDistillAsync_ExcludesAlreadyDistilledButKeepsRetriable()
    {
        var sourceId = await InsertSourceAsync("pending-exclusion-source");

        // Distilled: has captions + transcript, but a successful distill wrote distill_status='distilled'.
        // Must NOT appear in pending — this is the bug (distilled/approved/published videos lingered).
        var distilledVideo = await InsertVideoWithTranscriptAsync(sourceId, "distilled-video", TranscriptStatus.Captions);
        await _videoStore.SetDistillStatusAsync(distilledVideo, "distilled");

        // Failed and skipped-over-cap are retriable — they MUST remain pending so the operator can re-run.
        var failedVideo = await InsertVideoWithTranscriptAsync(sourceId, "failed-video", TranscriptStatus.Captions);
        await _videoStore.SetDistillStatusAsync(failedVideo, "failed");
        var overCapVideo = await InsertVideoWithTranscriptAsync(sourceId, "over-cap-video", TranscriptStatus.Captions);
        await _videoStore.SetDistillStatusAsync(overCapVideo, "skipped_over_cap");

        // Filtered (LLM judged not KB-worthy) has no site_index row and is intentionally left pending
        // so the operator can re-attempt; only 'distilled' is excluded.
        var filteredVideo = await InsertVideoWithTranscriptAsync(sourceId, "filtered-video", TranscriptStatus.Captions);
        await _videoStore.SetDistillStatusAsync(filteredVideo, "filtered");

        // Never distilled at all — plain pending.
        var freshVideo = await InsertVideoWithTranscriptAsync(sourceId, "fresh-video", TranscriptStatus.Captions);

        var pending = await _videoStore.ListVideosPendingDistillAsync(sourceId);
        var pendingIds = pending.Select(v => v.Id).ToHashSet();

        Assert.DoesNotContain(distilledVideo, pendingIds);
        Assert.Contains(failedVideo, pendingIds);
        Assert.Contains(overCapVideo, pendingIds);
        Assert.Contains(filteredVideo, pendingIds);
        Assert.Contains(freshVideo, pendingIds);
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
    public async Task DistillStatusAsync_FilteredStatus_IsAccepted()
    {
        var sourceId = await InsertSourceAsync("filtered-status-source");
        var videoId = await InsertVideoWithTranscriptAsync(sourceId, "filtered-status-video", TranscriptStatus.Captions);

        await _videoStore.SetDistillStatusAsync(videoId, "filtered");

        Assert.Equal("filtered", await _videoStore.GetDistillStatusAsync(videoId));
        Assert.Equal(1, await CountDistillStatusRowsAsync(videoId));
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
        await _videoStore.InsertStatedRuleAsync(videoId, CreateRule("clear-output"), 0);
        await _videoStore.SetDistillStatusAsync(videoId, "failed");

        await _videoStore.ClearDistillOutputAsync(videoId);
        await _videoStore.InsertStatedRuleAsync(videoId, CreateRule("clear-output-reinsert"), 0);

        Assert.Equal(1, await _videoStore.CountTranscriptsByVideoAsync(videoId));
        Assert.Equal(0, await _videoStore.CountSummariesByVideoAsync(videoId));
        Assert.Equal(0, await _videoStore.CountClipsByVideoAsync(videoId));
        Assert.Equal(0, await _videoStore.CountTagsByVideoAsync(videoId));
        Assert.Equal(1, await CountStatedRulesByVideoAsync(RelationalDatabaseConnection.FromSqlitePath(_dbPath), videoId));
        Assert.Equal("failed", await _videoStore.GetDistillStatusAsync(videoId));
    }

    [Fact]
    public async Task InsertStatedRuleAsync_RoundTrips_AllFields_OnSqlite()
    {
        var sourceId = await InsertSourceAsync("sqlite-rule-roundtrip");
        var videoId = await InsertVideoWithTranscriptAsync(sourceId, "sqlite-rule-video", TranscriptStatus.Captions);
        var expected = CreateRule("sqlite round-trip");

        await _videoStore.InsertStatedRuleAsync(videoId, expected, 3);

        var actual = await GetStatedRuleRowAsync(RelationalDatabaseConnection.FromSqlitePath(_dbPath), videoId);

        Assert.NotNull(actual);
        Assert.Equal(expected.Category, actual!.Category);
        Assert.Equal(expected.Metric, actual.Metric);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.ValueMin, actual.ValueMin);
        Assert.Equal(expected.ValueMax, actual.ValueMax);
        Assert.Equal(expected.Comparator, actual.Comparator);
        Assert.Equal(expected.Condition, actual.Condition);
        Assert.Equal(expected.ClipTimestampSeconds, actual.ClipTimestampSeconds);
        Assert.Equal(expected.SourceClip, actual.SourceClip);
        Assert.Equal(expected.Confidence, actual.Confidence);
        Assert.Equal(expected.CardReference, actual.CardReference);
        Assert.Equal(expected.CardGrounded, actual.CardGrounded);
        Assert.Equal(expected.VideoDateUtc, actual.VideoDateUtc);
    }

    [PostgresFact]
    public async Task InsertStatedRuleAsync_RoundTrips_AllFields_OnPostgres()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var descriptor = new RelationalDatabaseConnection(
            RelationalDatabaseProvider.Postgres,
            connectionString);
        var sourceStore = new ContentSourceStore(descriptor);
        var videoStore = new ContentVideoStore(descriptor);
        var sourceId = await InsertSourceAsync(sourceStore, "postgres-rule-roundtrip");
        var videoId = await InsertVideoWithTranscriptAsync(sourceStore, videoStore, sourceId, "postgres-rule-video", TranscriptStatus.Captions);
        var expected = CreateRule("postgres round-trip");

        await videoStore.InsertStatedRuleAsync(videoId, expected, 3);

        var actual = await GetStatedRuleRowAsync(descriptor, videoId);

        Assert.NotNull(actual);
        Assert.Equal(expected.Category, actual!.Category);
        Assert.Equal(expected.Metric, actual.Metric);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.ValueMin, actual.ValueMin);
        Assert.Equal(expected.ValueMax, actual.ValueMax);
        Assert.Equal(expected.Comparator, actual.Comparator);
        Assert.Equal(expected.Condition, actual.Condition);
        Assert.Equal(expected.ClipTimestampSeconds, actual.ClipTimestampSeconds);
        Assert.Equal(expected.SourceClip, actual.SourceClip);
        Assert.Equal(expected.Confidence, actual.Confidence);
        Assert.Equal(expected.CardReference, actual.CardReference);
        Assert.Equal(expected.CardGrounded, actual.CardGrounded);
        Assert.Equal(expected.VideoDateUtc, actual.VideoDateUtc);
    }

    private async Task<long> InsertSourceAsync(string slug)
        => await _sourceStore.InsertSourceAsync(
            slug,
            $"Source {slug}",
            ContentSourceType.Youtube,
            $"https://example.test/{slug}");

    private static async Task<long> InsertSourceAsync(ContentSourceStore sourceStore, string slug)
        => await sourceStore.InsertSourceAsync(
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

    private static async Task<long> InsertVideoWithTranscriptAsync(
        ContentSourceStore sourceStore,
        ContentVideoStore videoStore,
        long sourceId,
        string youtubeVideoId,
        string transcriptStatus)
    {
        var videoId = await InsertVideoWithoutTranscriptAsync(videoStore, sourceId, youtubeVideoId, transcriptStatus);
        await videoStore.InsertTranscriptAsync(videoId, TranscriptSource.Captions, $"Transcript for {youtubeVideoId}.");
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

    private static async Task<long> InsertVideoWithoutTranscriptAsync(
        ContentVideoStore videoStore,
        long sourceId,
        string youtubeVideoId,
        string transcriptStatus)
        => await videoStore.InsertVideoAsync(
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

    private static StatedRuleCandidate CreateRule(string sourceClip)
        => new()
        {
            Category = "ramp",
            Metric = "lands",
            Value = 37,
            ValueMin = 36,
            ValueMax = 38,
            Comparator = "range",
            Condition = "control shells",
            ClipTimestampSeconds = 134,
            SourceClip = sourceClip,
            Confidence = 0.91,
            CardReference = "Rhystic Study",
            CardGrounded = true,
            VideoDateUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
        };

    private static async Task<int> CountStatedRulesByVideoAsync(RelationalDatabaseConnection descriptor, long videoId)
    {
        await using var connection = await descriptor.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
              FROM content_stated_rules
             WHERE video_id = @videoId;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);

        var count = await command.ExecuteScalarAsync();
        return Convert.ToInt32(count);
    }

    private static async Task<StatedRuleRow?> GetStatedRuleRowAsync(RelationalDatabaseConnection descriptor, long videoId)
    {
        await using var connection = await descriptor.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT category,
                   metric,
                   value,
                   value_min,
                   value_max,
                   comparator,
                   condition,
                   clip_ts,
                   source_clip,
                   confidence,
                   card_reference,
                   card_grounded,
                   video_date_utc
              FROM content_stated_rules
             WHERE video_id = @videoId
             ORDER BY sort_order, id
             LIMIT 1;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new StatedRuleRow
        {
            Category = reader.GetString(0),
            Metric = reader.GetString(1),
            Value = reader.IsDBNull(2) ? null : reader.GetDouble(2),
            ValueMin = reader.IsDBNull(3) ? null : reader.GetDouble(3),
            ValueMax = reader.IsDBNull(4) ? null : reader.GetDouble(4),
            Comparator = reader.GetString(5),
            Condition = reader.IsDBNull(6) ? null : reader.GetString(6),
            ClipTimestampSeconds = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            SourceClip = reader.GetString(8),
            Confidence = reader.GetDouble(9),
            CardReference = reader.IsDBNull(10) ? null : reader.GetString(10),
            CardGrounded = reader.IsDBNull(11) ? null : reader.GetBoolean(11),
            VideoDateUtc = reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
        };
    }

    private sealed record StatedRuleRow
    {
        public required string Category { get; init; }

        public required string Metric { get; init; }

        public double? Value { get; init; }

        public double? ValueMin { get; init; }

        public double? ValueMax { get; init; }

        public required string Comparator { get; init; }

        public string? Condition { get; init; }

        public int? ClipTimestampSeconds { get; init; }

        public required string SourceClip { get; init; }

        public double Confidence { get; init; }

        public string? CardReference { get; init; }

        public bool? CardGrounded { get; init; }

        public DateTimeOffset? VideoDateUtc { get; init; }
    }
}
