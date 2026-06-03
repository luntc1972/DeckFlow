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
    public async Task RunHarvestAsync_WhisperInsertFailureAfterLedgerWriteKeepsLedgerRecord()
    {
        var videoStore = new FakeContentVideoStore { ThrowOnInsertTranscript = true };
        var ledger = new FakeWhisperSpendLedger();
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.FromWhisper("whisper body", 2520, 0.252m));

        var exitCode = await RunAsync(videoStore, ledger, transcriptSource, [CreateListedVideo("video-1", TimeSpan.FromMinutes(42))]);

        Assert.Equal(0, exitCode);
        var ledgerRecord = Assert.Single(ledger.Records);
        Assert.Equal(10, ledgerRecord.VideoId);
        Assert.Equal(2520, ledgerRecord.SecondsBilled);
        Assert.Equal(0.252m, ledgerRecord.CostUsd);
        Assert.Equal("2026-05", ledgerRecord.MonthKey);
        Assert.Empty(videoStore.Transcripts);
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
    public async Task RunHarvestAsync_ExistingSkippedOverCapVideoIsNotDowngradedToFailedOnRetryException()
    {
        var videoStore = new FakeContentVideoStore
        {
            ExistingVideos =
            {
                ["video-skipped"] = CreateExistingVideo(20, "video-skipped", TranscriptStatus.SkippedOverCap),
            },
        };
        var transcriptSource = new FakeTranscriptSource(new InvalidOperationException("retry failed"));

        var exitCode = await RunAsync(videoStore, new FakeWhisperSpendLedger(), transcriptSource, [CreateListedVideo("video-skipped", TimeSpan.FromMinutes(5))]);

        Assert.Equal(0, exitCode);
        Assert.Empty(videoStore.StatusUpdates);
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
    public async Task RunHarvestAsync_NoCaptionsWithWhisperDisabledUpdatesSkippedNoCaptionsWithoutAudioOrWhisper()
    {
        var videoStore = new FakeContentVideoStore();
        var ledger = new FakeWhisperSpendLedger();
        var fetcher = new FakeYouTubeTranscriptFetcher(YouTubeCaptionResult.NoCaptions());
        var audioSource = new FakeYouTubeAudioSource();
        var whisper = new FakeWhisperTranscriptionService();
        var transcriptSource = new YouTubeTranscriptSource(fetcher, audioSource, whisper, whisperEnabled: false);

        var exitCode = await RunAsync(videoStore, ledger, transcriptSource, [CreateListedVideo("video-1", TimeSpan.FromMinutes(12))]);

        Assert.Equal(0, exitCode);
        Assert.Equal(TranscriptStatus.SkippedNoCaptions, Assert.Single(videoStore.StatusUpdates).Status);
        Assert.Empty(videoStore.Transcripts);
        Assert.Empty(ledger.Records);
        Assert.Equal(1, fetcher.Calls);
        Assert.Equal(0, audioSource.Calls);
        Assert.Equal(0, whisper.Calls);
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

    [Fact]
    public async Task RunHarvestAsync_SourceListerFailureContinuesWithNextSource()
    {
        const string failedUrl = "https://www.youtube.com/@dead";
        const string succeedingUrl = "https://www.youtube.com/@live";
        var video = CreateListedVideo("video-live", TimeSpan.FromMinutes(9));
        var videoStore = new FakeContentVideoStore();
        var sourceStore = new FakeContentSourceStore(
        [
            CreateSource(1, "dead", failedUrl),
            CreateSource(2, "live", succeedingUrl),
        ]);
        var lister = new FakeYouTubeChannelVideoLister([])
        {
            ExceptionsByChannelUrl =
            {
                [failedUrl] = new InvalidOperationException("Playlist 'dead' is not available."),
            },
            VideosByChannelUrl =
            {
                [succeedingUrl] = [video],
            },
        };

        var exitCode = await CommandRunners.RunHarvestAsync(
            sourceStore,
            videoStore,
            new FakeWhisperSpendLedger(),
            lister,
            new FakeTranscriptSource(TranscriptFetchResult.FromCaptions("caption body", false)),
            new FakeFfmpegAudioChunker(),
            limit: 5,
            logger: new LoggerConfiguration().CreateLogger(),
            utcNow: () => new DateTimeOffset(2026, 5, 27, 23, 59, 59, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        var insertedVideo = Assert.Single(videoStore.InsertedVideos);
        Assert.Equal(2, insertedVideo.SourceId);
        Assert.Equal("video-live", insertedVideo.YoutubeVideoId);
        Assert.Equal("captions", Assert.Single(videoStore.StatusUpdates).Status);
    }

    private static Task<int> RunAsync(
        FakeContentVideoStore videoStore,
        FakeWhisperSpendLedger ledger,
        ITranscriptSource transcriptSource,
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

    private static ContentSource CreateSource(long id, string sourceSlug, string sourceUrl)
        => new()
        {
            Id = id,
            SourceSlug = sourceSlug,
            DisplayName = sourceSlug,
            SourceType = ContentSourceType.Youtube,
            SourceUrl = sourceUrl,
            IsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        };

    private sealed class FakeContentSourceStore : IContentSourceStore
    {
        private readonly IReadOnlyList<ContentSource> _sources;

        public FakeContentSourceStore()
            : this(
            [
                CreateSource(1, "mtggoldfish", "https://www.youtube.com/@MTGGoldfish"),
            ])
        {
        }

        public FakeContentSourceStore(IReadOnlyList<ContentSource> sources)
        {
            _sources = sources;
        }

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
            => Task.FromResult(_sources);
    }

    private sealed class FakeContentVideoStore : IContentVideoStore
    {
        private long _nextId = 10;

        public Dictionary<string, ContentVideo> ExistingVideos { get; } = [];

        public bool ThrowOnInsertTranscript { get; init; }

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
            if (ThrowOnInsertTranscript)
            {
                throw new InvalidOperationException("transcript insert failed");
            }

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

        public Dictionary<string, Exception> ExceptionsByChannelUrl { get; } = [];

        public Dictionary<string, IReadOnlyList<YouTubeChannelVideo>> VideosByChannelUrl { get; } = [];

        public Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(
            string channelUrl,
            int limit,
            CancellationToken ct = default)
        {
            if (ExceptionsByChannelUrl.TryGetValue(channelUrl, out var exception))
            {
                throw exception;
            }

            if (VideosByChannelUrl.TryGetValue(channelUrl, out var videos))
            {
                return Task.FromResult(videos);
            }

            return Task.FromResult(_videos);
        }
    }

    private sealed class FakeTranscriptSource : ITranscriptSource
    {
        private readonly Exception? _exception;
        private readonly TranscriptFetchResult? _result;

        public FakeTranscriptSource(TranscriptFetchResult result)
        {
            _result = result;
        }

        public FakeTranscriptSource(Exception exception)
        {
            _exception = exception;
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
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_result!);
        }
    }

    private sealed class FakeYouTubeTranscriptFetcher : IYouTubeTranscriptFetcher
    {
        private readonly YouTubeCaptionResult _result;

        public FakeYouTubeTranscriptFetcher(YouTubeCaptionResult result)
        {
            _result = result;
        }

        public int Calls { get; private set; }

        public Task<YouTubeCaptionResult> FetchCaptionsAsync(string videoId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeYouTubeAudioSource : IYouTubeAudioSource
    {
        public int Calls { get; private set; }

        public Task<AudioDownloadResult> DownloadAudioAsync(string videoUrlOrId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new AudioDownloadResult
            {
                TempFilePath = Path.Combine(Path.GetTempPath(), "deckflow-audio-not-used.webm"),
                FileName = "audio.webm",
                SizeBytes = 123,
                DurationSeconds = 0,
            });
        }
    }

    private sealed class FakeWhisperTranscriptionService : IWhisperTranscriptionService
    {
        public int Calls { get; private set; }

        public Task<WhisperTranscriptionResult> TranscribeAsync(
            AudioDownloadResult audio,
            TimeSpan? knownDuration,
            string monthKey,
            CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new WhisperTranscriptionResult
            {
                Outcome = TranscriptOutcome.Failed,
                FailureReason = "not configured",
                MonthKey = monthKey,
            });
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
