using DeckFlow.CLI;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Serilog;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for the Content KB distill command orchestration seam.
/// </summary>
public sealed class RunDistillAsyncTests : IDisposable
{
    private readonly string _artifactRoot;

    public RunDistillAsyncTests()
    {
        _artifactRoot = Path.Combine(Path.GetTempPath(), $"deckflow-distill-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_artifactRoot))
        {
            Directory.Delete(_artifactRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunDistillAsync_DropsOutOfVocabularyTagsAndWarns()
    {
        var video = CreateVideo(1, 1, "video-one");
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(1, video, "transcript body");
        var distiller = new FakeLlmDistillationService
        {
            DefaultTags = new TagsResult(
                ["combo", "not-an-archetype"],
                ["cEDH", "Bracket 9"],
                ["win-cons", "mana-rocks"],
                new TokenUsage(30, 3)),
        };

        var result = await RunAsync(videoStore, distiller: distiller);

        Assert.True(result.Success);
        Assert.Equal(
            [
                new TagWrite(1, ContentTagDimension.Archetype, "combo"),
                new TagWrite(1, ContentTagDimension.Bracket, "cEDH"),
                new TagWrite(1, ContentTagDimension.CardCategory, "win-cons"),
            ],
            videoStore.Tags);
        var row = Assert.Single(LastRunIndexStore!.Rows);
        Assert.Equal(["combo"], row.ArchetypeTags);
        Assert.Equal(["cEDH"], row.BracketTags);
        Assert.Equal(["win-cons"], row.CardCategoryTags);
        Assert.Equal(3, LastLogger!.Entries.Count(logEntry => logEntry.Level == LogLevel.Warning
            && logEntry.Message.Contains("dropped out-of-vocab tag", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task RunDistillAsync_VideoIds_DistillsOnlyRequestedNaturalKeys()
    {
        var first = CreateVideo(1, 1, "video-one");
        var second = CreateVideo(2, 1, "video-two");
        var third = CreateVideo(3, 1, "video-three");
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(1, first, "transcript 1");
        videoStore.AddPending(1, second, "transcript 2");
        videoStore.AddPending(1, third, "transcript 3");
        var distiller = new FakeLlmDistillationService();

        var result = await RunAsync(videoStore, distiller: distiller, videoIds: ["video-two"]);

        Assert.True(result.Success);
        Assert.Equal(1, distiller.SummaryCalls);
        Assert.Equal([new StatusUpdate(2, "distilled")], videoStore.StatusUpdates);
        var row = Assert.Single(LastRunIndexStore!.Rows);
        Assert.Equal("Video video-two", row.Title);
    }

    [Fact]
    public async Task RunDistillAsync_SkipsDistilledAndReattemptsFailedSkippedOverCapAndMissingStatus()
    {
        var alreadyDistilled = CreateVideo(1, 1, "already-distilled");
        var failed = CreateVideo(2, 1, "failed-video");
        var skipped = CreateVideo(3, 1, "skipped-video");
        var missing = CreateVideo(4, 1, "missing-video");
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(1, alreadyDistilled, "transcript 1", "distilled");
        videoStore.AddPending(1, failed, "transcript 2", "failed");
        videoStore.AddPending(1, skipped, "transcript 3", "skipped_over_cap");
        videoStore.AddPending(1, missing, "transcript 4");
        var distiller = new FakeLlmDistillationService();

        var result = await RunAsync(videoStore, distiller: distiller);

        Assert.True(result.Success);
        Assert.Equal(3, distiller.SummaryCalls);
        Assert.Equal([2, 3, 4], videoStore.ClearCalls);
        Assert.DoesNotContain(videoStore.StatusUpdates, update => update.VideoId == 1);
        Assert.Equal(
            [
                new StatusUpdate(2, "distilled"),
                new StatusUpdate(3, "distilled"),
                new StatusUpdate(4, "distilled"),
            ],
            videoStore.StatusUpdates);
    }

    [Fact]
    public async Task RunDistillAsync_MarksFailedVideoAndContinuesBatch()
    {
        var first = CreateVideo(1, 1, "first");
        var second = CreateVideo(2, 1, "second");
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(1, first, "transcript first");
        videoStore.AddPending(1, second, "transcript second");
        var distiller = new FakeLlmDistillationService();
        distiller.SummaryQueue.Enqueue(new InvalidOperationException("summary failed"));
        distiller.SummaryQueue.Enqueue(FakeLlmDistillationService.CreateSummary());

        var result = await RunAsync(videoStore, distiller: distiller);

        Assert.True(result.Success);
        Assert.Contains(new StatusUpdate(1, "failed"), videoStore.StatusUpdates);
        Assert.Contains(new StatusUpdate(2, "distilled"), videoStore.StatusUpdates);
        Assert.DoesNotContain(videoStore.Summaries, summary => summary.VideoId == 1);
        Assert.Contains(videoStore.Summaries, summary => summary.VideoId == 2);
        Assert.Equal(1, LastRunStore!.CompleteCalls.Single().VideosProcessed);
    }

    [Fact]
    public async Task RunDistillAsync_DryRunProjectsSpendWithoutBusinessMutations()
    {
        var video = CreateVideo(1, 1, "dry-run-video");
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(1, video, "transcript body");
        var ledger = new FakeLlmSpendLedger();
        var distiller = new FakeLlmDistillationService();

        var result = await RunAsync(videoStore, ledger, distiller, dryRun: true);

        Assert.True(result.Success);
        Assert.Empty(distiller.Calls);
        Assert.Empty(ledger.Records);
        Assert.Empty(videoStore.ClearCalls);
        Assert.Empty(videoStore.Summaries);
        Assert.Empty(videoStore.Clips);
        Assert.Empty(videoStore.Tags);
        Assert.Empty(videoStore.StatusUpdates);
        Assert.Empty(LastRunIndexStore!.Rows);
        Assert.Equal(0, LastRunStore!.StartCalls);
        Assert.Empty(LastRunStore.CompleteCalls);
        Assert.False(Directory.Exists(_artifactRoot));
        Assert.Contains(LastProgress!.Messages, message => message.Contains("dry-run-video", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunDistillAsync_SubscriptionProviderBypassesCapAndRecordsZeroSpend()
    {
        var video = CreateVideo(1, 1, "subscription-video");
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(1, video, "transcript body");
        var ledger = new FakeLlmSpendLedger();
        ledger.WouldExceedResults.Enqueue(true);
        var distiller = new FakeLlmDistillationService();

        var result = await RunAsync(videoStore, ledger, distiller, isSubscriptionProvider: true);

        Assert.True(result.Success);
        Assert.Equal(["classify:transcript body", "summary:transcript body", "clips:transcript body", "tags:transcript body"], distiller.Calls);
        Assert.Empty(ledger.WouldExceedChecks);
        Assert.Equal(3, ledger.Records.Count);
        Assert.All(ledger.Records, record => Assert.Equal(0m, record.CostUsd));
        Assert.Equal(new StatusUpdate(1, "distilled"), Assert.Single(videoStore.StatusUpdates));
        var completedRun = Assert.Single(LastRunStore!.CompleteCalls);
        Assert.Equal(3, completedRun.WhisperCalls);
        Assert.Equal(0m, completedRun.SpendUsd);
    }

    [Fact]
    public async Task RunDistillAsync_SubscriptionProviderDryRunReportsZeroSubscriptionSpendWithoutCapHit()
    {
        var video = CreateVideo(1, 1, "subscription-dry-run");
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(1, video, "transcript body");
        var ledger = new FakeLlmSpendLedger();
        ledger.WouldExceedResults.Enqueue(true);
        var distiller = new FakeLlmDistillationService();
        var result = await RunAsync(videoStore, ledger, distiller, dryRun: true, isSubscriptionProvider: true);

        Assert.True(result.Success);
        Assert.Contains(LastProgress!.Messages, message => message.Contains("WOULD distill ($0, subscription) subscription-dry-run", StringComparison.Ordinal));
        Assert.Contains(LastProgress.Messages, message => message.Contains("projected spend $0 (subscription)", StringComparison.Ordinal));
        Assert.DoesNotContain(LastProgress.Messages, message => message.Contains("cap", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(ledger.WouldExceedChecks);
        Assert.Empty(distiller.Calls);
        Assert.Empty(ledger.Records);
        Assert.Empty(videoStore.StatusUpdates);
    }

    [Fact]
    public async Task RunDistillAsync_UsesEachEnabledSourceSlugAndNeverQueriesDisabledSources()
    {
        var sourceStore = new FakeContentSourceStore(
        [
            CreateSource(1, "source-a", isEnabled: true),
            CreateSource(2, "source-b", isEnabled: true),
            CreateSource(3, "disabled-source", isEnabled: false),
        ]);
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(1, CreateVideo(1, 1, "video-a"), "transcript a");
        videoStore.AddPending(2, CreateVideo(2, 2, "video-b"), "transcript b");
        videoStore.AddPending(3, CreateVideo(3, 3, "video-c"), "transcript c");

        var result = await RunAsync(videoStore, sourceStore: sourceStore);

        Assert.True(result.Success);
        Assert.Equal([1, 2], videoStore.PendingSourceIds);
        Assert.DoesNotContain(3, videoStore.PendingSourceIds);
        Assert.Contains(LastRunIndexStore!.Rows, row => row.YoutubeVideoId == "video-a"
            && row.ArtifactPath == "content-kb/source-a/video-a.md");
        Assert.Contains(LastRunIndexStore.Rows, row => row.YoutubeVideoId == "video-b"
            && row.ArtifactPath == "content-kb/source-b/video-b.md");
        Assert.True(File.Exists(Path.Combine(_artifactRoot, "source-a", "video-a.md")));
        Assert.True(File.Exists(Path.Combine(_artifactRoot, "source-b", "video-b.md")));
        Assert.False(File.Exists(Path.Combine(_artifactRoot, "disabled-source", "video-c.md")));
    }

    [Fact]
    public async Task RunDistillAsync_ClassifierDropsVideo_VideoNotIndexed()
    {
        var video = CreateVideo(1, 1, "drop-video");
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(1, video, "transcript body");
        var distiller = new FakeLlmDistillationService();
        distiller.ClassifyQueue.Enqueue(new ClassificationResult("drop", "trivia"));

        var result = await RunAsync(videoStore, distiller: distiller, isSubscriptionProvider: true);

        Assert.True(result.Success);
        Assert.Contains(new StatusUpdate(1, "filtered"), videoStore.StatusUpdates);
        Assert.Contains(1, videoStore.ClearCalls);
        Assert.Equal(1, distiller.ClassifyCallCount);
        Assert.Equal(0, distiller.SummaryCalls);
        Assert.Empty(videoStore.Summaries);
        Assert.Empty(videoStore.Clips);
        Assert.Empty(videoStore.Tags);
        Assert.Null(await LastRunIndexStore!.GetByNaturalKeyAsync(ContentSourceType.Youtube, "drop-video"));
    }

    [Fact]
    public async Task RunDistillAsync_ClassifierDropsPreviouslyIndexedVideo_RemovesStaleIndexRow()
    {
        var video = CreateVideo(1, 1, "stale-video");
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(1, video, "transcript body");
        var distiller = new FakeLlmDistillationService();
        distiller.ClassifyQueue.Enqueue(new ClassificationResult("drop", "intro-only"));
        var staleRow = new ContentSiteIndexRow
        {
            Id = 99,
            Source = "source-one",
            Title = "Video stale-video",
            VideoUrl = "https://www.youtube.com/watch?v=stale-video",
            ArtifactPath = "content-kb/source-one/stale-video.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T00:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-27T12:34:56Z"),
            ArchetypeTags = ["combo"],
            BracketTags = ["cEDH"],
            CardCategoryTags = ["win-cons"],
            YoutubeVideoId = "stale-video",
            RssGuid = null,
        };

        LastRunIndexStore = new FakeContentSiteIndexStore();
        LastRunIndexStore.Rows.Add(staleRow);

        var result = await RunAsync(
            videoStore,
            distiller: distiller,
            isSubscriptionProvider: true,
            indexStore: LastRunIndexStore);

        Assert.True(result.Success);
        Assert.Contains(new StatusUpdate(1, "filtered"), videoStore.StatusUpdates);
        Assert.Contains(1, videoStore.ClearCalls);
        Assert.Contains(99, LastRunIndexStore.DeleteCalls);
        Assert.Null(await LastRunIndexStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, "stale-video"));
    }

    [Fact]
    public async Task RunDistillAsync_ClassifierKeepsVideo_VideoDistilledNormally()
    {
        var video = CreateVideo(1, 1, "keep-video");
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(1, video, "transcript body");
        var distiller = new FakeLlmDistillationService();
        distiller.ClassifyQueue.Enqueue(new ClassificationResult("keep", "advice"));

        var result = await RunAsync(videoStore, distiller: distiller, isSubscriptionProvider: true);

        Assert.True(result.Success);
        Assert.Equal(1, distiller.ClassifyCallCount);
        Assert.Equal(1, distiller.SummaryCalls);
        Assert.Contains(new StatusUpdate(1, "distilled"), videoStore.StatusUpdates);
        var row = Assert.Single(LastRunIndexStore!.Rows);
        Assert.Equal("keep-video", row.YoutubeVideoId);
    }

    [Fact]
    public async Task RunDistillAsync_MeteredProvider_FailsClosedWithoutClassifying()
    {
        var video = CreateVideo(1, 1, "metered-video");
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(1, video, "transcript body");
        var distiller = new FakeLlmDistillationService();

        var result = await RunAsync(videoStore, distiller: distiller, isSubscriptionProvider: false);

        Assert.False(result.Success);
        Assert.Contains("classifier requires the subscription LLM CLI", result.AbortedReason, StringComparison.Ordinal);
        Assert.Equal(0, distiller.ClassifyCallCount);
        Assert.Equal(0, distiller.SummaryCalls);
        Assert.Empty(videoStore.StatusUpdates);
        Assert.Empty(videoStore.ClearCalls);
        Assert.Empty(LastRunIndexStore!.Rows);
        Assert.Equal(0, LastRunStore!.StartCalls);
    }

    [Fact]
    public async Task RunDistillAsync_ContentSourceSetEnabledAsyncTogglesSource()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"deckflow-source-toggle-{Guid.NewGuid():N}.db");
        try
        {
            var store = new ContentSourceStore(dbPath);
            var sourceId = await store.InsertSourceAsync(
                "toggle-source",
                "Toggle Source",
                ContentSourceType.Youtube,
                "https://www.youtube.com/@toggle");

            var disableExitCode = await ContentKbCommandRunners.RunContentSourceSetEnabledAsync(
                sourceId,
                enabled: false,
                new FileInfo(dbPath),
                new LoggerConfiguration().CreateLogger(),
                CancellationToken.None);

            Assert.Equal(0, disableExitCode);
            Assert.False((await store.GetSourceAsync(sourceId))!.IsEnabled);
            Assert.DoesNotContain(await store.ListEnabledSourcesAsync(), source => source.Id == sourceId);

            var enableExitCode = await ContentKbCommandRunners.RunContentSourceSetEnabledAsync(
                sourceId,
                enabled: true,
                new FileInfo(dbPath),
                new LoggerConfiguration().CreateLogger(),
                CancellationToken.None);

            Assert.Equal(0, enableExitCode);
            Assert.True((await store.GetSourceAsync(sourceId))!.IsEnabled);
            Assert.Contains(await store.ListEnabledSourcesAsync(), source => source.Id == sourceId);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.Delete(dbPath);
            }
        }
    }

    private FakeContentSiteIndexStore? LastRunIndexStore { get; set; }

    private FakeContentHarvestRunStore? LastRunStore { get; set; }

    private RecordingOrchestratorProgress? LastProgress { get; set; }

    private RecordingLogger<ContentKbOrchestrator>? LastLogger { get; set; }

    private async Task<DistillResult> RunAsync(
        FakeContentVideoStore videoStore,
        FakeLlmSpendLedger? ledger = null,
        FakeLlmDistillationService? distiller = null,
        FakeContentSourceStore? sourceStore = null,
        FakeContentSiteIndexStore? indexStore = null,
        bool dryRun = false,
        bool isSubscriptionProvider = true,
        IReadOnlyList<string>? videoIds = null)
    {
        LastRunIndexStore = indexStore ?? new FakeContentSiteIndexStore();
        LastRunStore = new FakeContentHarvestRunStore();
        LastProgress = new RecordingOrchestratorProgress();
        LastLogger = new RecordingLogger<ContentKbOrchestrator>();
        var orchestrator = new ContentKbOrchestrator(
            sourceStore ?? new FakeContentSourceStore([CreateSource(1, "source-one", isEnabled: true)]),
            videoStore,
            LastRunIndexStore,
            new ThrowingBlockedVideoStore(),
            LastRunStore,
            ledger ?? new FakeLlmSpendLedger(),
            new ThrowingWhisperSpendLedger(),
            distiller ?? new FakeLlmDistillationService(),
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker(),
            () => new DateTimeOffset(2026, 5, 27, 12, 34, 56, TimeSpan.Zero),
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = _artifactRoot,
            },
            LastLogger);
        return await orchestrator.DistillAsync(
            limit: 10,
            dryRun: dryRun,
            isSubscriptionProvider: isSubscriptionProvider,
            videoIds: videoIds,
            progress: LastProgress,
            cancellationToken: CancellationToken.None);
    }

    private static ContentSource CreateSource(long id, string sourceSlug, bool isEnabled)
        => new()
        {
            Id = id,
            SourceSlug = sourceSlug,
            DisplayName = sourceSlug,
            SourceType = ContentSourceType.Youtube,
            SourceUrl = "https://www.youtube.com/@" + sourceSlug,
            IsEnabled = isEnabled,
            CreatedUtc = DateTimeOffset.Parse("2026-05-27T00:00:00Z"),
        };

    private static ContentVideo CreateVideo(long id, long sourceId, string youtubeVideoId)
        => new()
        {
            Id = id,
            SourceId = sourceId,
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null,
            Title = "Video " + youtubeVideoId,
            VideoUrl = "https://www.youtube.com/watch?v=" + youtubeVideoId,
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T00:00:00Z"),
            TranscriptStatus = TranscriptStatus.Captions,
            CreatedUtc = DateTimeOffset.Parse("2026-05-27T00:00:00Z"),
        };

    private sealed class FakeContentSourceStore : IContentSourceStore
    {
        private readonly IReadOnlyList<ContentSource> _sources;

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
            => Task.FromResult<IReadOnlyList<ContentSource>>(_sources.Where(source => source.IsEnabled).ToArray());
    }

    private sealed class FakeContentVideoStore : IContentVideoStore
    {
        private readonly Dictionary<long, List<ContentVideo>> _pendingBySource = [];
        private readonly Dictionary<long, ContentTranscriptBody> _transcriptsByVideoId = [];
        private readonly Dictionary<long, string?> _statusByVideoId = [];

        public List<string> Operations { get; init; } = [];

        public List<long> PendingSourceIds { get; } = [];

        public List<long> ClearCalls { get; } = [];

        public List<SummaryWrite> Summaries { get; } = [];

        public List<ClipWrite> Clips { get; } = [];

        public List<TagWrite> Tags { get; } = [];

        public List<StatusUpdate> StatusUpdates { get; } = [];

        public void AddPending(long sourceId, ContentVideo video, string transcript, string? distillStatus = null)
        {
            if (!_pendingBySource.TryGetValue(sourceId, out var videos))
            {
                videos = [];
                _pendingBySource[sourceId] = videos;
            }

            videos.Add(video);
            _transcriptsByVideoId[video.Id] = new ContentTranscriptBody
            {
                Body = transcript,
                Source = TranscriptSource.Captions,
            };
            _statusByVideoId[video.Id] = distillStatus;
        }

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
            => throw new NotImplementedException();

        public Task<ContentVideo?> GetVideoByYoutubeIdAsync(
            long sourceId,
            string youtubeVideoId,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ContentVideo>> ListVideosPendingDistillAsync(
            long sourceId,
            CancellationToken cancellationToken = default)
        {
            PendingSourceIds.Add(sourceId);
            return Task.FromResult<IReadOnlyList<ContentVideo>>(
                _pendingBySource.TryGetValue(sourceId, out var videos) ? videos : []);
        }

        public Task UpdateTranscriptStatusAsync(
            long videoId,
            string status,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertTranscriptAsync(
            long videoId,
            string source,
            string body,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentTranscriptBody?> GetLatestTranscriptAsync(long videoId, CancellationToken cancellationToken = default)
            => Task.FromResult(_transcriptsByVideoId.GetValueOrDefault(videoId));

        public Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
        {
            Summaries.Add(new SummaryWrite(videoId, body));
            return Task.FromResult((long)Summaries.Count);
        }

        public Task<long> InsertClipAsync(
            long videoId,
            int timestampS,
            string excerpt,
            int sortOrder,
            CancellationToken cancellationToken = default)
        {
            Clips.Add(new ClipWrite(videoId, timestampS, excerpt, sortOrder));
            return Task.FromResult((long)Clips.Count);
        }

        public Task<long> InsertTagAsync(
            long videoId,
            string dimension,
            string tagValue,
            CancellationToken cancellationToken = default)
        {
            Tags.Add(new TagWrite(videoId, dimension, tagValue));
            return Task.FromResult((long)Tags.Count);
        }

        public Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
        {
            var removed = 0;
            foreach (var pair in _pendingBySource)
            {
                removed += pair.Value.RemoveAll(video => string.Equals(video.YoutubeVideoId, youtubeVideoId, StringComparison.Ordinal));
            }

            return Task.FromResult(removed);
        }

        public Task ClearDistillOutputAsync(long videoId, CancellationToken cancellationToken = default)
        {
            ClearCalls.Add(videoId);
            return Task.CompletedTask;
        }

        public Task<string?> GetDistillStatusAsync(long videoId, CancellationToken cancellationToken = default)
            => Task.FromResult(_statusByVideoId.GetValueOrDefault(videoId));

        public Task SetDistillStatusAsync(
            long videoId,
            string status,
            CancellationToken cancellationToken = default)
        {
            Operations.Add($"status:{videoId}:{status}");
            _statusByVideoId[videoId] = status;
            StatusUpdates.Add(new StatusUpdate(videoId, status));
            return Task.CompletedTask;
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

    private sealed class FakeContentSiteIndexStore : IContentSiteIndexStore
    {
        public List<ContentSiteIndexRow> Rows { get; } = [];

        public List<ContentSiteIndexRow> ContentColumnsOnlyUpserts { get; } = [];

        public List<long> DeleteCalls { get; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
        {
            Rows.Add(row);
            return Task.CompletedTask;
        }

        public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
        {
            var index = Rows.FindIndex(existing => MatchesNaturalKey(existing, row));
            if (index < 0)
            {
                Rows.Add(row with { IsVisible = false, IsHidden = false });
                return Task.CompletedTask;
            }

            Rows[index] = row with
            {
                Id = Rows[index].Id,
                IsVisible = Rows[index].IsVisible,
                IsHidden = Rows[index].IsHidden
            };
            return Task.CompletedTask;
        }

        public Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
        {
            ContentColumnsOnlyUpserts.Add(row);
            Rows.Add(row);
            return Task.CompletedTask;
        }

        public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(
            string naturalKeyType,
            string naturalKeyValue,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Rows.FirstOrDefault(row => MatchesNaturalKey(row, naturalKeyType, naturalKeyValue)));

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Rows.Where(row => row.IsVisible).ToArray());

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Rows.Where(row => row.ApprovalStatus == "approved").ToArray());

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Rows);

        public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Rows.FirstOrDefault(row => row.Id == id));

        public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
        {
            var count = 0;
            for (var i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Id != id)
                {
                    continue;
                }

                Rows[i] = Rows[i] with { IsVisible = visible, IsHidden = false };
                count++;
            }

            return Task.FromResult(count);
        }

        public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
        {
            var count = 0;
            for (var i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Id != id)
                {
                    continue;
                }

                Rows[i] = Rows[i] with
                {
                    IsHidden = hidden,
                    IsVisible = hidden ? false : Rows[i].IsVisible
                };
                count++;
            }

            return Task.FromResult(count);
        }

        public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            DeleteCalls.Add(id);
            var removed = Rows.RemoveAll(row => row.Id == id);
            return Task.FromResult(removed);
        }

        public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
        {
            var count = 0;
            for (var i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Id != id)
                {
                    continue;
                }

                Rows[i] = Rows[i] with { IsEvergreen = evergreen };
                count++;
            }

            return Task.FromResult(count);
        }

        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
        {
            var count = 0;
            for (var i = 0; i < Rows.Count; i++)
            {
                if (!string.Equals(Rows[i].Source, source, StringComparison.Ordinal))
                {
                    continue;
                }

                Rows[i] = Rows[i] with { IsVisible = visible, IsHidden = false };
                count++;
            }

            return Task.FromResult(count);
        }

        public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
        {
            var count = 0;
            for (var i = 0; i < Rows.Count; i++)
            {
                if (!string.Equals(Rows[i].Source, source, StringComparison.Ordinal))
                {
                    continue;
                }

                Rows[i] = Rows[i] with
                {
                    IsHidden = hidden,
                    IsVisible = hidden ? false : Rows[i].IsVisible
                };
                count++;
            }

            return Task.FromResult(count);
        }

        private static bool MatchesNaturalKey(ContentSiteIndexRow left, ContentSiteIndexRow right)
            => MatchesNaturalKey(left, ContentSourceType.Youtube, right.YoutubeVideoId)
               || MatchesNaturalKey(left, ContentSourceType.Podcast, right.RssGuid);

        private static bool MatchesNaturalKey(ContentSiteIndexRow row, string naturalKeyType, string? naturalKeyValue)
        {
            if (string.IsNullOrWhiteSpace(naturalKeyValue))
            {
                return false;
            }

            return naturalKeyType switch
            {
                ContentSourceType.Youtube => string.Equals(row.YoutubeVideoId, naturalKeyValue, StringComparison.Ordinal),
                ContentSourceType.Podcast => string.Equals(row.RssGuid, naturalKeyValue, StringComparison.Ordinal),
                _ => false
            };
        }
    }

    private sealed class FakeContentHarvestRunStore : IContentHarvestRunStore
    {
        public int StartCalls { get; private set; }

        public List<RunComplete> CompleteCalls { get; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<long> StartRunAsync(CancellationToken cancellationToken = default)
        {
            StartCalls++;
            return Task.FromResult(42L);
        }

        public Task CompleteRunAsync(
            long runId,
            int sourcesProcessed,
            int videosProcessed,
            int transcriptsFetched,
            int whisperCalls,
            decimal spendUsd,
            string? abortedReason,
            CancellationToken cancellationToken = default)
        {
            CompleteCalls.Add(new RunComplete(
                runId,
                sourcesProcessed,
                videosProcessed,
                transcriptsFetched,
                whisperCalls,
                spendUsd,
                abortedReason));
            return Task.CompletedTask;
        }

        public Task<ContentHarvestRun?> GetRunAsync(long runId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeLlmSpendLedger : ILlmSpendLedger
    {
        public List<string> Operations { get; init; } = [];

        public Queue<bool> WouldExceedResults { get; } = [];

        public List<decimal> WouldExceedChecks { get; } = [];

        public List<LlmLedgerRecord> Records { get; } = [];

        public Task RecordCallAsync(
            long videoId,
            int inputTokens,
            int outputTokens,
            decimal costUsd,
            string monthKey,
            CancellationToken cancellationToken = default)
        {
            Operations.Add($"ledger:{videoId}:{inputTokens}:{outputTokens}");
            Records.Add(new LlmLedgerRecord(videoId, inputTokens, outputTokens, costUsd, monthKey));
            return Task.CompletedTask;
        }

        public Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default)
            => Task.FromResult(Records.Sum(record => record.CostUsd));

        public Task<bool> WouldExceedCapAsync(
            decimal projectedCallCostUsd,
            string monthKey,
            CancellationToken cancellationToken = default)
        {
            WouldExceedChecks.Add(projectedCallCostUsd);
            return Task.FromResult(WouldExceedResults.Count > 0 && WouldExceedResults.Dequeue());
        }
    }

    private sealed class FakeLlmDistillationService : ILlmDistillationService
    {
        public List<string> Operations { get; init; } = [];

        public List<string> Calls { get; } = [];

        public int ClassifyCallCount { get; private set; }

        public int SummaryCalls { get; private set; }

        public ClassificationResult DefaultClassification { get; init; } = new("keep", "default");

        public SummaryResult DefaultSummary { get; init; } = CreateSummary();

        public ClipsResult DefaultClips { get; init; } = CreateClips();

        public TagsResult DefaultTags { get; init; } = CreateTags();

        public Queue<object> ClassifyQueue { get; } = [];

        public Queue<object> SummaryQueue { get; } = [];

        public Queue<object> ClipsQueue { get; } = [];

        public Queue<object> TagsQueue { get; } = [];

        public static SummaryResult CreateSummary()
            => new("This video explains a compact combo deck plan.", new TokenUsage(100, 10));

        public static ClipsResult CreateClips()
            => new(
                [
                    new ClipItem(60, "The opening explains the deck plan."),
                    new ClipItem(null, "The middle highlights the interaction suite."),
                    new ClipItem(180, "The closing covers win conditions."),
                ],
                new TokenUsage(200, 20));

        public static TagsResult CreateTags()
            => new(
                ["combo"],
                ["cEDH"],
                ["win-cons"],
                new TokenUsage(30, 3));

        public Task<ClassificationResult> ClassifyAsync(string transcript, CancellationToken cancellationToken = default)
        {
            ClassifyCallCount++;
            Calls.Add("classify:" + transcript);
            Operations.Add("classify:" + transcript);
            return Task.FromResult(Next(ClassifyQueue, DefaultClassification));
        }

        public Task<SummaryResult> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
        {
            SummaryCalls++;
            Calls.Add("summary:" + transcript);
            Operations.Add("summary:" + transcript);
            return Task.FromResult(Next(SummaryQueue, DefaultSummary));
        }

        public Task<ClipsResult> ExtractClipsAsync(string transcript, CancellationToken cancellationToken = default)
        {
            Calls.Add("clips:" + transcript);
            Operations.Add("clips:" + transcript);
            return Task.FromResult(Next(ClipsQueue, DefaultClips));
        }

        public Task<TagsResult> InferTagsAsync(string transcript, CancellationToken cancellationToken = default)
        {
            Calls.Add("tags:" + transcript);
            Operations.Add("tags:" + transcript);
            return Task.FromResult(Next(TagsQueue, DefaultTags));
        }

        private static T Next<T>(Queue<object> queue, T fallback)
        {
            if (queue.Count == 0)
            {
                return fallback;
            }

            var next = queue.Dequeue();
            if (next is Exception exception)
            {
                throw exception;
            }

            return (T)next;
        }
    }

    private sealed record SummaryWrite(long VideoId, string Body);

    private sealed record ClipWrite(long VideoId, int TimestampS, string Excerpt, int SortOrder);

    private sealed record TagWrite(long VideoId, string Dimension, string TagValue);

    private sealed record StatusUpdate(long VideoId, string Status);

    private sealed record LlmLedgerRecord(long VideoId, int InputTokens, int OutputTokens, decimal CostUsd, string MonthKey);

    private sealed record RunComplete(
        long RunId,
        int SourcesProcessed,
        int VideosProcessed,
        int TranscriptsFetched,
        int WhisperCalls,
        decimal SpendUsd,
        string? AbortedReason);
}
