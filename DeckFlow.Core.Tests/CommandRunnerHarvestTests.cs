using DeckFlow.CLI;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using Serilog;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for the harvest command orchestration seam.
/// </summary>
public sealed class CommandRunnerHarvestTests
{
    [Fact]
    public async Task RunHarvestAsync_WhisperSuccessWritesTranscriptStatusAndLedgerWithSameMonthKey()
    {
        var duration = TimeSpan.FromMinutes(42);
        var video = CreateListedVideo("video-1", duration);
        var videoStore = new FakeContentVideoStore();
        var ledger = new FakeWhisperSpendLedger();
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.FromWhisper("whisper body", 2520, 0.252m));

        var exitCode = await RunAsync(videoStore, ledger, transcriptSource, [video]);

        Assert.Equal(0, exitCode);
        Assert.Equal(duration, transcriptSource.LastKnownDuration);
        Assert.Equal("2026-05", transcriptSource.LastMonthKey);
        var transcript = Assert.Single(videoStore.Transcripts);
        Assert.Equal("whisper", transcript.Source);
        Assert.Equal("whisper body", transcript.Body);
        Assert.Equal("whisper", Assert.Single(videoStore.StatusUpdates).Status);
        var ledgerRecord = Assert.Single(ledger.Records);
        Assert.Equal(2520, ledgerRecord.SecondsBilled);
        Assert.Equal(0.252m, ledgerRecord.CostUsd);
        Assert.Equal("2026-05", ledgerRecord.MonthKey);
    }

    [Fact]
    public async Task RunHarvestAsync_ExistingFailedVideoResumesAndExistingSuccessSkips()
    {
        var failed = CreateListedVideo("video-failed", TimeSpan.FromMinutes(5));
        var alreadyDone = CreateListedVideo("video-done", TimeSpan.FromMinutes(6));
        var videoStore = new FakeContentVideoStore
        {
            ExistingVideos =
            {
                ["video-failed"] = CreateExistingVideo(20, "video-failed", TranscriptStatus.Failed),
                ["video-done"] = CreateExistingVideo(21, "video-done", TranscriptStatus.Captions),
            },
        };
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.FromCaptions("caption body", true));

        var exitCode = await RunAsync(videoStore, new FakeWhisperSpendLedger(), transcriptSource, [failed, alreadyDone]);

        Assert.Equal(0, exitCode);
        Assert.Equal(["video-failed"], transcriptSource.NaturalKeys);
        Assert.Empty(videoStore.InsertedVideos);
        Assert.Equal(20, Assert.Single(videoStore.Transcripts).VideoId);
        Assert.Equal("captions", Assert.Single(videoStore.StatusUpdates).Status);
    }

    [Fact]
    public async Task RunHarvestAsync_SkippedOverCapUpdatesDistinctStatusWithoutTranscriptOrLedger()
    {
        var videoStore = new FakeContentVideoStore();
        var ledger = new FakeWhisperSpendLedger();
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.SkippedOverCap());

        var exitCode = await RunAsync(videoStore, ledger, transcriptSource, [CreateListedVideo("video-1", TimeSpan.FromMinutes(12))]);

        Assert.Equal(0, exitCode);
        Assert.Equal("skipped_over_cap", Assert.Single(videoStore.StatusUpdates).Status);
        Assert.Empty(videoStore.Transcripts);
        Assert.Empty(ledger.Records);
    }

    [Fact]
    public async Task RunHarvestAsync_FfmpegUnavailableContinuesRun()
    {
        var videoStore = new FakeContentVideoStore();
        var chunker = new FakeFfmpegAudioChunker { IsAvailable = false };
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.FromCaptions("caption body", false));

        var exitCode = await RunAsync(
            videoStore,
            new FakeWhisperSpendLedger(),
            transcriptSource,
            [CreateListedVideo("video-1", TimeSpan.FromMinutes(7))],
            chunker);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, chunker.IsAvailableCalls);
        Assert.Equal(["video-1"], transcriptSource.NaturalKeys);
    }

    private static Task<int> RunAsync(
        FakeContentVideoStore videoStore,
        FakeWhisperSpendLedger ledger,
        FakeTranscriptSource transcriptSource,
        IReadOnlyList<YouTubeChannelVideo> videos,
        FakeFfmpegAudioChunker? chunker = null)
        => CommandRunners.RunHarvestAsync(
            new FakeContentSourceStore(),
            videoStore,
            ledger,
            new FakeYouTubeChannelVideoLister(videos),
            transcriptSource,
            chunker ?? new FakeFfmpegAudioChunker(),
            limit: 5,
            logger: new LoggerConfiguration().CreateLogger(),
            utcNow: () => new DateTimeOffset(2026, 5, 27, 23, 59, 59, TimeSpan.Zero),
            CancellationToken.None);

    private static YouTubeChannelVideo CreateListedVideo(string videoId, TimeSpan duration)
        => new()
        {
            VideoId = videoId,
            Url = "https://www.youtube.com/watch?v=" + videoId,
            Title = "Video " + videoId,
            Duration = duration,
            PublishedUtc = null,
        };

    private static ContentVideo CreateExistingVideo(long id, string videoId, string transcriptStatus)
        => new()
        {
            Id = id,
            SourceId = 1,
            YoutubeVideoId = videoId,
            RssGuid = null,
            Title = "Existing " + videoId,
            VideoUrl = "https://www.youtube.com/watch?v=" + videoId,
            PublishedUtc = null,
            TranscriptStatus = transcriptStatus,
            CreatedUtc = DateTimeOffset.UtcNow,
        };

    private sealed class FakeContentSourceStore : IContentSourceStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<long> InsertSourceAsync(
            string sourceSlug,
            string displayName,
            string sourceType,
            string sourceUrl,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentSource?> GetSourceAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ContentSource>> ListEnabledSourcesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentSource>>(
            [
                new()
                {
                    Id = 1,
                    SourceSlug = "mtggoldfish",
                    DisplayName = "MTGGoldfish",
                    SourceType = ContentSourceType.Youtube,
                    SourceUrl = "https://www.youtube.com/@MTGGoldfish",
                    IsEnabled = true,
                    CreatedUtc = DateTimeOffset.UtcNow,
                },
            ]);
    }

    private sealed class FakeContentVideoStore : IContentVideoStore
    {
        private long _nextId = 10;

        public Dictionary<string, ContentVideo> ExistingVideos { get; } = [];

        public List<ContentVideo> InsertedVideos { get; } = [];

        public List<TranscriptWrite> Transcripts { get; } = [];

        public List<StatusUpdate> StatusUpdates { get; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<long> InsertVideoAsync(
            long sourceId,
            string? youtubeVideoId,
            string? rssGuid,
            string title,
            string videoUrl,
            DateTimeOffset? publishedUtc,
            string transcriptStatus,
            CancellationToken cancellationToken = default)
        {
            var id = _nextId++;
            InsertedVideos.Add(new ContentVideo
            {
                Id = id,
                SourceId = sourceId,
                YoutubeVideoId = youtubeVideoId,
                RssGuid = rssGuid,
                Title = title,
                VideoUrl = videoUrl,
                PublishedUtc = publishedUtc,
                TranscriptStatus = transcriptStatus,
                CreatedUtc = DateTimeOffset.UtcNow,
            });
            return Task.FromResult(id);
        }

        public Task<ContentVideo?> GetVideoByYoutubeIdAsync(
            long sourceId,
            string youtubeVideoId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingVideos.GetValueOrDefault(youtubeVideoId));

        public Task UpdateTranscriptStatusAsync(
            long videoId,
            string status,
            CancellationToken cancellationToken = default)
        {
            StatusUpdates.Add(new StatusUpdate(videoId, status));
            return Task.CompletedTask;
        }

        public Task<long> InsertTranscriptAsync(
            long videoId,
            string source,
            string body,
            CancellationToken cancellationToken = default)
        {
            Transcripts.Add(new TranscriptWrite(videoId, source, body));
            return Task.FromResult((long)Transcripts.Count);
        }

        public Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertClipAsync(long videoId, int timestampS, string excerpt, int sortOrder, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertTagAsync(long videoId, string dimension, string tagValue, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountTranscriptsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountSummariesByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountClipsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountTagsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeWhisperSpendLedger : IWhisperSpendLedger
    {
        public List<LedgerRecord> Records { get; } = [];

        public Task RecordCallAsync(
            long videoId,
            int secondsBilled,
            decimal costUsd,
            string monthKey,
            CancellationToken cancellationToken = default)
        {
            Records.Add(new LedgerRecord(videoId, secondsBilled, costUsd, monthKey));
            return Task.CompletedTask;
        }

        public Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default)
            => Task.FromResult(0m);

        public Task<bool> WouldExceedCapAsync(
            decimal projectedCallCostUsd,
            string monthKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeYouTubeChannelVideoLister : IYouTubeChannelVideoLister
    {
        private readonly IReadOnlyList<YouTubeChannelVideo> _videos;

        public FakeYouTubeChannelVideoLister(IReadOnlyList<YouTubeChannelVideo> videos)
        {
            _videos = videos;
        }

        public Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(
            string channelUrl,
            int limit,
            CancellationToken ct = default)
            => Task.FromResult(_videos);
    }

    private sealed class FakeTranscriptSource : ITranscriptSource
    {
        private readonly TranscriptFetchResult _result;

        public FakeTranscriptSource(TranscriptFetchResult result)
        {
            _result = result;
        }

        public string SourceType => ContentSourceType.Youtube;

        public List<string> NaturalKeys { get; } = [];

        public TimeSpan? LastKnownDuration { get; private set; }

        public string? LastMonthKey { get; private set; }

        public Task<TranscriptFetchResult> FetchTranscriptAsync(
            string naturalKey,
            TimeSpan? knownDuration,
            string monthKey,
            CancellationToken ct = default)
        {
            NaturalKeys.Add(naturalKey);
            LastKnownDuration = knownDuration;
            LastMonthKey = monthKey;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeFfmpegAudioChunker : IFfmpegAudioChunker
    {
        public bool IsAvailable { get; init; } = true;

        public int IsAvailableCalls { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        {
            IsAvailableCalls++;
            return Task.FromResult(IsAvailable);
        }

        public Task<IReadOnlyList<string>> ChunkAsync(
            string inputPath,
            string outputDirectory,
            int segmentSeconds = 300,
            CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed record TranscriptWrite(long VideoId, string Source, string Body);

    private sealed record StatusUpdate(long VideoId, string Status);

    private sealed record LedgerRecord(long VideoId, int SecondsBilled, decimal CostUsd, string MonthKey);
}
