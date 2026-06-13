using DeckFlow.CLI;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for the harvest command orchestration seam.
/// </summary>
public sealed class CommandRunnerHarvestTests
{
    private RecordingOrchestratorProgress? LastProgress { get; set; }

    private RecordingLogger<ContentKbOrchestrator>? LastLogger { get; set; }

    [Fact]
    public async Task RunHarvestAsync_WhisperSuccessWritesTranscriptStatusAndLedgerWithSameMonthKey()
    {
        var duration = TimeSpan.FromMinutes(42);
        var video = CreateListedVideo("video-1", duration);
        var videoStore = new FakeContentVideoStore();
        var ledger = new FakeWhisperSpendLedger();
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.FromWhisper("whisper body", 2520, 0.252m));

        var result = await RunAsync(videoStore, ledger, transcriptSource, [video]);

        Assert.True(result.Success);
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

        var result = await RunAsync(videoStore, ledger, transcriptSource, [CreateListedVideo("video-1", TimeSpan.FromMinutes(42))]);

        Assert.True(result.Success);
        var ledgerRecord = Assert.Single(ledger.Records);
        Assert.Equal(10, ledgerRecord.VideoId);
        Assert.Equal(2520, ledgerRecord.SecondsBilled);
        Assert.Equal(0.252m, ledgerRecord.CostUsd);
        Assert.Equal("2026-05", ledgerRecord.MonthKey);
        Assert.Empty(videoStore.Transcripts);
    }

    [Fact]
    public async Task RunHarvestAsync_ShortVideoSkipsBeforeStorageOrTranscriptFetch()
    {
        var videoStore = new FakeContentVideoStore();
        var ledger = new FakeWhisperSpendLedger();
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.FromCaptions("caption body", true));

        var result = await RunAsync(videoStore, ledger, transcriptSource, [CreateListedVideo("video-short", TimeSpan.FromSeconds(60))]);

        Assert.True(result.Success);
        Assert.Empty(videoStore.InsertedVideos);
        Assert.Empty(videoStore.Transcripts);
        Assert.Empty(videoStore.StatusUpdates);
        Assert.Empty(ledger.Records);
        Assert.Empty(transcriptSource.NaturalKeys);
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

        var result = await RunAsync(videoStore, new FakeWhisperSpendLedger(), transcriptSource, [failed, alreadyDone]);

        Assert.True(result.Success);
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

        var result = await RunAsync(videoStore, new FakeWhisperSpendLedger(), transcriptSource, [CreateListedVideo("video-skipped", TimeSpan.FromMinutes(5))]);

        Assert.True(result.Success);
        Assert.Empty(videoStore.StatusUpdates);
    }

    [Fact]
    public async Task RunHarvestAsync_SkippedOverCapUpdatesDistinctStatusWithoutTranscriptOrLedger()
    {
        var videoStore = new FakeContentVideoStore();
        var ledger = new FakeWhisperSpendLedger();
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.SkippedOverCap());

        var result = await RunAsync(videoStore, ledger, transcriptSource, [CreateListedVideo("video-1", TimeSpan.FromMinutes(12))]);

        Assert.True(result.Success);
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

        var result = await RunAsync(videoStore, ledger, transcriptSource, [CreateListedVideo("video-1", TimeSpan.FromMinutes(12))]);

        Assert.True(result.Success);
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

        var result = await RunAsync(
            videoStore,
            new FakeWhisperSpendLedger(),
            transcriptSource,
            [CreateListedVideo("video-1", TimeSpan.FromMinutes(7))],
            chunker);

        Assert.True(result.Success);
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

        var result = await RunAsync(
            videoStore,
            new FakeWhisperSpendLedger(),
            new FakeTranscriptSource(TranscriptFetchResult.FromCaptions("caption body", false)),
            [],
            chunker: new FakeFfmpegAudioChunker(),
            sourceStore: sourceStore,
            lister: lister);

        Assert.True(result.Success);
        var insertedVideo = Assert.Single(videoStore.InsertedVideos);
        Assert.Equal(2, insertedVideo.SourceId);
        Assert.Equal("video-live", insertedVideo.YoutubeVideoId);
        Assert.Equal("captions", Assert.Single(videoStore.StatusUpdates).Status);
    }

    [Fact]
    public async Task RunHarvestAsync_VideoIds_HarvestsExactlyRequestedFromSingleEnabledSource()
    {
        var videos = new[]
        {
            CreateListedVideo("video-a", TimeSpan.FromMinutes(5)),
            CreateListedVideo("video-b", TimeSpan.FromMinutes(6)),
            CreateListedVideo("video-c", TimeSpan.FromMinutes(7)),
        };
        var videoStore = new FakeContentVideoStore();
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.FromCaptions("caption body", isAutoGenerated: true));

        var result = await RunAsync(videoStore, new FakeWhisperSpendLedger(), transcriptSource, videos, videoIds: ["video-a", "video-c"]);

        Assert.True(result.Success);
        Assert.Equal(["video-a", "video-c"], videoStore.InsertedVideos.Select(video => video.YoutubeVideoId));
        Assert.Equal(2, videoStore.Transcripts.Count);
    }

    [Fact]
    public async Task RunHarvestAsync_VideoIds_SkipsBlockedId_ButIngestsNonBlockedSibling()
    {
        var videos = new[]
        {
            CreateListedVideo("blocked-video", TimeSpan.FromMinutes(5)),
            CreateListedVideo("normal-video", TimeSpan.FromMinutes(6)),
        };
        var videoStore = new FakeContentVideoStore();
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.FromCaptions("caption body", isAutoGenerated: true));
        var blockedVideoStore = new FakeBlockedVideoStore(["blocked-video"]);

        var result = await RunAsync(
            videoStore,
            new FakeWhisperSpendLedger(),
            transcriptSource,
            videos,
            videoIds: ["blocked-video", "normal-video"],
            blockedVideoStore: blockedVideoStore);

        Assert.True(result.Success);
        Assert.Equal(["blocked-video", "normal-video"], blockedVideoStore.IsBlockedChecks);
        var insertedVideo = Assert.Single(videoStore.InsertedVideos);
        Assert.Equal("normal-video", insertedVideo.YoutubeVideoId);
        Assert.Equal(["normal-video"], transcriptSource.NaturalKeys);
    }

    [Fact]
    public async Task RunHarvestAsync_VideoIds_NonBlockedId_IsIngested()
    {
        var videoStore = new FakeContentVideoStore();
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.FromCaptions("caption body", isAutoGenerated: true));

        var result = await RunAsync(
            videoStore,
            new FakeWhisperSpendLedger(),
            transcriptSource,
            [CreateListedVideo("video-a", TimeSpan.FromMinutes(5))],
            videoIds: ["video-a"],
            blockedVideoStore: new FakeBlockedVideoStore([]));

        Assert.True(result.Success);
        Assert.Equal("video-a", Assert.Single(videoStore.InsertedVideos).YoutubeVideoId);
        Assert.Equal(["video-a"], transcriptSource.NaturalKeys);
    }

    [Fact]
    public async Task RunHarvestAsync_VideoIds_MultipleEnabledSourcesWithoutSourceIdFails()
    {
        var videoStore = new FakeContentVideoStore();
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.FromCaptions("caption body", isAutoGenerated: true));
        var sourceStore = new FakeContentSourceStore(
        [
            CreateSource(1, "channel-one", "https://www.youtube.com/@one"),
            CreateSource(2, "channel-two", "https://www.youtube.com/@two"),
        ]);

        var result = await RunAsync(
            videoStore,
            new FakeWhisperSpendLedger(),
            transcriptSource,
            [CreateListedVideo("video-a", TimeSpan.FromMinutes(5))],
            videoIds: ["video-a"],
            sourceStore: sourceStore);

        Assert.False(result.Success);
        Assert.Contains("single target source", result.Message, StringComparison.Ordinal);
        Assert.Empty(videoStore.InsertedVideos);
    }

    [Fact]
    public async Task RunHarvestAsync_VideoIds_SourceIdSelectsTargetAmongMultipleSources()
    {
        var videoStore = new FakeContentVideoStore();
        var transcriptSource = new FakeTranscriptSource(TranscriptFetchResult.FromCaptions("caption body", isAutoGenerated: true));
        var sourceStore = new FakeContentSourceStore(
        [
            CreateSource(1, "channel-one", "https://www.youtube.com/@one"),
            CreateSource(2, "channel-two", "https://www.youtube.com/@two"),
        ]);

        var result = await RunAsync(
            videoStore,
            new FakeWhisperSpendLedger(),
            transcriptSource,
            [CreateListedVideo("video-a", TimeSpan.FromMinutes(5))],
            videoIds: ["video-a"],
            sourceId: 2,
            sourceStore: sourceStore);

        Assert.True(result.Success);
        var inserted = Assert.Single(videoStore.InsertedVideos);
        Assert.Equal(2, inserted.SourceId);
        Assert.Equal("video-a", inserted.YoutubeVideoId);
    }

    [Fact]
    public void ParseVideoIds_SplitsTrimsAndDeduplicates()
    {
        Assert.Equal(["abc", "def"], ContentKbCommandRunners.ParseVideoIds(" abc, def ,abc,, "));
    }

    [Fact]
    public void ParseVideoIds_NullOrBlankYieldsNull()
    {
        Assert.Null(ContentKbCommandRunners.ParseVideoIds(null));
        Assert.Null(ContentKbCommandRunners.ParseVideoIds("   "));
        Assert.Null(ContentKbCommandRunners.ParseVideoIds(" , ,"));
    }

    private HarvestResult? LastResult { get; set; }

    private async Task<HarvestResult> RunAsync(
        FakeContentVideoStore videoStore,
        FakeWhisperSpendLedger ledger,
        ITranscriptSource transcriptSource,
        IReadOnlyList<YouTubeChannelVideo> videos,
        FakeFfmpegAudioChunker? chunker = null,
        IReadOnlyList<string>? videoIds = null,
        long? sourceId = null,
        FakeContentSourceStore? sourceStore = null,
        IBlockedVideoStore? blockedVideoStore = null,
        IYouTubeChannelVideoLister? lister = null)
    {
        LastProgress = new RecordingOrchestratorProgress();
        LastLogger = new RecordingLogger<ContentKbOrchestrator>();
        var orchestrator = new ContentKbOrchestrator(
            sourceStore ?? new FakeContentSourceStore(),
            videoStore,
            new ThrowingContentSiteIndexStore(),
            blockedVideoStore ?? new FakeBlockedVideoStore([]),
            new ThrowingContentHarvestRunStore(),
            new ThrowingLlmSpendLedger(),
            ledger,
            new ThrowingLlmDistillationService(),
            lister ?? new FakeYouTubeChannelVideoLister(videos),
            transcriptSource,
            chunker ?? new FakeFfmpegAudioChunker(),
            () => new DateTimeOffset(2026, 5, 27, 23, 59, 59, TimeSpan.Zero),
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-harvest-tests"),
            },
            LastLogger);
        LastResult = await orchestrator.HarvestAsync(
            limit: 5,
            videoIds: videoIds,
            sourceId: sourceId,
            progress: LastProgress,
            cancellationToken: CancellationToken.None);
        return LastResult;
    }

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

        public Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
        {
            var removed = ExistingVideos.Remove(youtubeVideoId);
            return Task.FromResult(removed ? 1 : 0);
        }

        public Task<int> CountTranscriptsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountSummariesByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountClipsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountTagsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeBlockedVideoStore : IBlockedVideoStore
    {
        private readonly HashSet<string> _blockedVideoIds;

        public FakeBlockedVideoStore(IEnumerable<string> blockedVideoIds)
        {
            _blockedVideoIds = new HashSet<string>(blockedVideoIds, StringComparer.Ordinal);
        }

        public List<string> IsBlockedChecks { get; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddBlockAsync(string youtubeVideoId, string? reason, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> RemoveBlockAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> IsBlockedAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
        {
            IsBlockedChecks.Add(youtubeVideoId);
            return Task.FromResult(_blockedVideoIds.Contains(youtubeVideoId));
        }

        public Task<IReadOnlyList<BlockedVideo>> ListBlockedAsync(CancellationToken cancellationToken = default)
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

        public Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsAsync(
            IReadOnlyList<string> videoIds,
            CancellationToken ct = default)
        {
            GetByIdsRequests.Add(videoIds);
            IReadOnlyList<YouTubeChannelVideo> matched = _videos
                .Where(video => videoIds.Contains(video.VideoId, StringComparer.Ordinal))
                .ToList();
            return Task.FromResult(matched);
        }

        public List<IReadOnlyList<string>> GetByIdsRequests { get; } = [];
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
