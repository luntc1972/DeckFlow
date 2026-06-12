using DeckFlow.CLI;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using Serilog;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CommandRunnerValidateClipsTests
{
    [Fact]
    public async Task RunDistillAsync_AllZeroClipTimestamps_RejectsBeforeStoringRows()
    {
        var sourceStore = new FakeContentSourceStore(
        [
            new ContentSource
            {
                Id = 1,
                SourceSlug = "source-one",
                DisplayName = "Source One",
                SourceType = ContentSourceType.Youtube,
                SourceUrl = "https://www.youtube.com/@source-one",
                IsEnabled = true,
                CreatedUtc = DateTimeOffset.Parse("2026-05-27T00:00:00Z"),
            },
        ]);
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(
            1,
            new ContentVideo
            {
                Id = 10,
                SourceId = 1,
                YoutubeVideoId = "all-zero-video",
                Title = "All Zero Video",
                VideoUrl = "https://www.youtube.com/watch?v=all-zero-video",
                PublishedUtc = DateTimeOffset.Parse("2026-05-26T00:00:00Z"),
                TranscriptStatus = TranscriptStatus.Captions,
                CreatedUtc = DateTimeOffset.Parse("2026-05-27T00:00:00Z"),
            },
            "timestamped transcript body");
        var indexStore = new FakeContentSiteIndexStore();
        var runStore = new FakeContentHarvestRunStore();
        var distiller = new FakeLlmDistillationService
        {
            ClipsResult = new ClipsResult(
            [
                new ClipItem(0, "first"),
                new ClipItem(0, "second"),
                new ClipItem(0, "third"),
            ],
            new TokenUsage(200, 20)),
        };

        var exitCode = await ContentKbCommandRunners.RunDistillAsync(
            sourceStore,
            videoStore,
            indexStore,
            runStore,
            new FakeLlmSpendLedger(),
            distiller,
            Path.Combine(Path.GetTempPath(), "deckflow-validate-clips-tests"),
            limit: 10,
            dryRun: false,
            new LoggerConfiguration().CreateLogger(),
            () => new DateTimeOffset(2026, 5, 27, 12, 34, 56, TimeSpan.Zero),
            CancellationToken.None,
            isSubscriptionProvider: true);

        Assert.Equal(0, exitCode);
        Assert.Equal([new StatusUpdate(10, "failed")], videoStore.StatusUpdates);
        Assert.Empty(videoStore.Summaries);
        Assert.Empty(videoStore.Clips);
        Assert.Empty(indexStore.UpsertedRows);
    }

    private sealed class FakeContentSourceStore : IContentSourceStore
    {
        private readonly IReadOnlyList<ContentSource> _sources;

        public FakeContentSourceStore(IReadOnlyList<ContentSource> sources)
        {
            _sources = sources;
        }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<long> InsertSourceAsync(string sourceSlug, string displayName, string sourceType, string sourceUrl, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentSource?> GetSourceAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ContentSource>> ListEnabledSourcesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentSource>>(_sources);
    }

    private sealed class FakeContentVideoStore : IContentVideoStore
    {
        private readonly Dictionary<long, List<ContentVideo>> _pendingBySource = [];
        private readonly Dictionary<long, ContentTranscriptBody> _transcriptsByVideoId = [];

        public List<StatusUpdate> StatusUpdates { get; } = [];

        public List<SummaryWrite> Summaries { get; } = [];

        public List<ClipWrite> Clips { get; } = [];

        public void AddPending(long sourceId, ContentVideo video, string transcript)
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
        }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<long> InsertVideoAsync(long sourceId, string? youtubeVideoId, string? rssGuid, string title, string videoUrl, DateTimeOffset? publishedUtc, string transcriptStatus, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentVideo?> GetVideoByYoutubeIdAsync(long sourceId, string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ContentVideo>> ListVideosPendingDistillAsync(long sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentVideo>>(_pendingBySource.GetValueOrDefault(sourceId) ?? []);

        public Task UpdateTranscriptStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertTranscriptAsync(long videoId, string source, string body, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentTranscriptBody?> GetLatestTranscriptAsync(long videoId, CancellationToken cancellationToken = default)
            => Task.FromResult(_transcriptsByVideoId.GetValueOrDefault(videoId));

        public Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
        {
            Summaries.Add(new SummaryWrite(videoId, body));
            return Task.FromResult((long)Summaries.Count);
        }

        public Task<long> InsertClipAsync(long videoId, int timestampS, string excerpt, int sortOrder, CancellationToken cancellationToken = default)
        {
            Clips.Add(new ClipWrite(videoId, timestampS, excerpt, sortOrder));
            return Task.FromResult((long)Clips.Count);
        }

        public Task<long> InsertTagAsync(long videoId, string dimension, string tagValue, CancellationToken cancellationToken = default)
            => Task.FromResult(1L);

        public Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ClearDistillOutputAsync(long videoId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> GetDistillStatusAsync(long videoId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task SetDistillStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
        {
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
        public List<ContentSiteIndexRow> UpsertedRows { get; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
        {
            UpsertedRows.Add(row);
            return Task.CompletedTask;
        }

        public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeContentHarvestRunStore : IContentHarvestRunStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<long> StartRunAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1L);

        public Task CompleteRunAsync(long runId, int sourcesProcessed, int videosProcessed, int transcriptsFetched, int whisperCalls, decimal spendUsd, string? abortedReason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ContentHarvestRun?> GetRunAsync(long runId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeLlmSpendLedger : ILlmSpendLedger
    {
        public Task RecordCallAsync(long videoId, int inputTokens, int outputTokens, decimal costUsd, string monthKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default)
            => Task.FromResult(0m);

        public Task<bool> WouldExceedCapAsync(decimal projectedCallCostUsd, string monthKey, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeLlmDistillationService : ILlmDistillationService
    {
        public ClipsResult ClipsResult { get; init; } = new(
        [
            new ClipItem(0, "first"),
            new ClipItem(0, "second"),
            new ClipItem(0, "third"),
        ],
        new TokenUsage(200, 20));

        public Task<SummaryResult> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
            => Task.FromResult(new SummaryResult("summary", new TokenUsage(100, 10)));

        public Task<ClassificationResult> ClassifyAsync(string transcript, CancellationToken cancellationToken = default)
            => Task.FromResult(new ClassificationResult("keep", "test"));

        public Task<ClipsResult> ExtractClipsAsync(string transcript, CancellationToken cancellationToken = default)
            => Task.FromResult(ClipsResult);

        public Task<TagsResult> InferTagsAsync(string transcript, CancellationToken cancellationToken = default)
            => Task.FromResult(new TagsResult(["combo"], ["cEDH"], ["win-cons"], new TokenUsage(30, 3)));
    }

    private sealed record SummaryWrite(long VideoId, string Body);

    private sealed record ClipWrite(long VideoId, int TimestampSeconds, string Excerpt, int SortOrder);

    private sealed record StatusUpdate(long VideoId, string Status);
}
