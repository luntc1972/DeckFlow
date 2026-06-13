using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Core.Tests;

public sealed class ContentMaintenanceOrchestratorParityTests
{
    [Fact]
    public async Task ListBlockedAsync_ReturnsItemsInStoreOrder_WithCliTabProjection()
    {
        var blockedStore = new StubBlockedVideoStore(
        [
            new BlockedVideo
            {
                YoutubeVideoId = "vid-001",
                BlockedUtc = DateTimeOffset.Parse("2026-06-10T12:00:00Z"),
                Reason = "spam"
            },
            new BlockedVideo
            {
                YoutubeVideoId = "vid-002",
                BlockedUtc = DateTimeOffset.Parse("2026-06-11T13:15:30Z"),
                Reason = null
            }
        ]);

        var result = await CreateOrchestrator(
            blockedStore,
            new RecordingDeleteAllContentVideoStore(),
            new RecordingDeleteAllContentSiteIndexStore())
            .ListBlockedAsync(progress: null, cancellationToken: CancellationToken.None);

        Assert.Collection(
            result.Items,
            item =>
            {
                Assert.Equal("vid-001", item.YoutubeVideoId);
                Assert.Equal(DateTimeOffset.Parse("2026-06-10T12:00:00Z"), item.BlockedUtc);
                Assert.Equal("spam", item.Reason);
            },
            item =>
            {
                Assert.Equal("vid-002", item.YoutubeVideoId);
                Assert.Equal(DateTimeOffset.Parse("2026-06-11T13:15:30Z"), item.BlockedUtc);
                Assert.Null(item.Reason);
            });

        var projection = string.Join(
            "\n",
            result.Items.Select(item => $"{item.YoutubeVideoId}\t{item.BlockedUtc:O}\t{item.Reason ?? string.Empty}"));

        Assert.Equal(
            "vid-001\t2026-06-10T12:00:00.0000000+00:00\tspam\nvid-002\t2026-06-11T13:15:30.0000000+00:00\t",
            projection); // CLI maps ListBlocked -> exit 0 with this tab projection.
    }

    [Fact]
    public async Task ResetCorpusAsync_DryRun_ReturnsZeroDeletes_WithoutStoreMutation()
    {
        var videoStore = new RecordingDeleteAllContentVideoStore();
        var siteIndexStore = new RecordingDeleteAllContentSiteIndexStore();

        var result = await CreateOrchestrator(
            new StubBlockedVideoStore([]),
            videoStore,
            siteIndexStore)
            .ResetCorpusAsync(
                dryRun: true,
                progress: null,
                cancellationToken: CancellationToken.None);

        Assert.True(result.Success); // CLI maps successful ResetCorpus dry-run -> exit 0.
        Assert.True(result.DryRun);
        Assert.Equal(0, result.DeletedContentRows);
        Assert.Equal(0, result.DeletedSiteIndexRows);
        Assert.Equal(0, result.DeletedVideos);
        Assert.Equal(0, videoStore.DeleteAllVideosCalls);
        Assert.Equal(0, siteIndexStore.DeleteAllRowsCalls);
    }

    private static ContentKbOrchestrator CreateOrchestrator(
        IBlockedVideoStore blockedStore,
        IContentVideoStore videoStore,
        IContentSiteIndexStore siteIndexStore)
        => new(
            new ThrowingContentSourceStore(),
            videoStore,
            siteIndexStore,
            blockedStore,
            new ThrowingContentHarvestRunStore(),
            new ThrowingLlmSpendLedger(),
            new ThrowingWhisperSpendLedger(),
            new ThrowingLlmDistillationService(),
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker(),
            () => DateTimeOffset.Parse("2026-06-13T00:00:00Z"),
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-content-maintenance-parity"),
            });

    private sealed class StubBlockedVideoStore : IBlockedVideoStore
    {
        private readonly IReadOnlyList<BlockedVideo> _rows;

        public StubBlockedVideoStore(IReadOnlyList<BlockedVideo> rows)
        {
            _rows = rows;
        }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddBlockAsync(string youtubeVideoId, string? reason, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> RemoveBlockAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> IsBlockedAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<BlockedVideo>> ListBlockedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_rows);
    }

    private sealed class RecordingDeleteAllContentVideoStore : IContentVideoStore
    {
        public int DeleteAllVideosCalls { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<long> InsertVideoAsync(long sourceId, string? youtubeVideoId, string? rssGuid, string title, string videoUrl, DateTimeOffset? publishedUtc, string transcriptStatus, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentVideo?> GetVideoByYoutubeIdAsync(long sourceId, string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ContentVideo>> ListVideosPendingDistillAsync(long sourceId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateTranscriptStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertTranscriptAsync(long videoId, string source, string body, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentTranscriptBody?> GetLatestTranscriptAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertClipAsync(long videoId, int timestampS, string excerpt, int sortOrder, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertTagAsync(long videoId, string dimension, string tagValue, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> DeleteAllVideosAsync(CancellationToken cancellationToken = default)
        {
            DeleteAllVideosCalls++;
            return Task.FromResult(3);
        }

        public Task ClearDistillOutputAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<string?> GetDistillStatusAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SetDistillStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
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

    private sealed class RecordingDeleteAllContentSiteIndexStore : IContentSiteIndexStore
    {
        public int DeleteAllRowsCalls { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

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

        public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> DeleteAllRowsAsync(CancellationToken cancellationToken = default)
        {
            DeleteAllRowsCalls++;
            return Task.FromResult(2);
        }

        public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
