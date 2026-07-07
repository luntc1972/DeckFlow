using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Verifies <see cref="DistillResult.DistilledVideos"/> surfaces the natural key (YouTube OR
/// podcast) + clip count for every successfully distilled video, while filtered/failed/dry-run
/// videos produce no entry (D-01, D-11).
/// </summary>
public sealed class DistillResultClipCountTests
{
    private const long SourceId = 1;
    private const string Transcript = "This is a test transcript for clip-count surfacing.";

    private static ContentKbOrchestrator CreateOrchestrator(
        ClipCountTestVideoStore videoStore,
        ConfigurableDistillationService distiller,
        string sourceType = ContentSourceType.Youtube)
    {
        var sourceStore = new FakeContentSourceStore(
        [
            new ContentSource
            {
                Id = SourceId,
                SourceSlug = "test-source",
                DisplayName = "Test Source",
                SourceType = sourceType,
                SourceUrl = "https://www.youtube.com/@test",
                IsEnabled = true,
                CreatedUtc = DateTimeOffset.Parse("2026-06-15T00:00:00Z"),
            }
        ]);

        return new ContentKbOrchestrator(
            sourceStore,
            videoStore,
            new ClipCountTestIndexStore(),
            new ThrowingBlockedVideoStore(),
            new FakeContentHarvestRunStore(),
            new FakeLlmSpendLedger(),
            new ThrowingWhisperSpendLedger(),
            distiller,
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker(),
            () => DateTimeOffset.Parse("2026-06-15T00:00:00Z"),
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-clipcount-tests"),
            });
    }

    [Fact]
    public async Task DistillAsync_YouTubeVideo_SurfacesNaturalKeyAndClipCount()
    {
        var videoStore = new ClipCountTestVideoStore();
        videoStore.AddPendingYoutube(SourceId, videoId: 10, youtubeVideoId: "yt-aaa", Transcript);
        var distiller = new ConfigurableDistillationService(clipCount: 6);

        var result = await CreateOrchestrator(videoStore, distiller).DistillAsync(
            limit: 10,
            dryRun: false,
            isSubscriptionProvider: true,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        var entry = Assert.Single(result.DistilledVideos);
        Assert.Equal(ContentSourceType.Youtube, entry.NaturalKeyType);
        Assert.Equal("yt-aaa", entry.NaturalKeyValue);
        Assert.Equal(6, entry.ClipCount);
    }

    [Fact]
    public async Task DistillAsync_PodcastVideo_SurfacesPodcastNaturalKeyNotYoutubeId()
    {
        var videoStore = new ClipCountTestVideoStore();
        videoStore.AddPendingPodcast(SourceId, videoId: 20, rssGuid: "rss-guid-xyz", Transcript);
        var distiller = new ConfigurableDistillationService(clipCount: 5);

        var result = await CreateOrchestrator(videoStore, distiller, ContentSourceType.Podcast).DistillAsync(
            limit: 10,
            dryRun: false,
            isSubscriptionProvider: true,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        var entry = Assert.Single(result.DistilledVideos);
        // Proves the DTO carries whichever key GetContentNaturalKeyInfo returns — podcast/RssGuid here.
        Assert.Equal(ContentSourceType.Podcast, entry.NaturalKeyType);
        Assert.Equal("rss-guid-xyz", entry.NaturalKeyValue);
        Assert.Equal(5, entry.ClipCount);
    }

    [Fact]
    public async Task DistillAsync_FilteredVideo_ProducesNoEntry()
    {
        var videoStore = new ClipCountTestVideoStore();
        videoStore.AddPendingYoutube(SourceId, videoId: 30, youtubeVideoId: "yt-drop", Transcript);
        var distiller = new ConfigurableDistillationService(clipCount: 6, verdict: "drop");

        var result = await CreateOrchestrator(videoStore, distiller).DistillAsync(
            limit: 10,
            dryRun: false,
            isSubscriptionProvider: true,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.DistilledVideos);
        Assert.Equal(1, result.VideosFiltered);
    }

    [Fact]
    public async Task DistillAsync_FailedVideo_ProducesNoEntryAndStaysInFailedIds()
    {
        var videoStore = new ClipCountTestVideoStore();
        videoStore.AddPendingYoutube(SourceId, videoId: 40, youtubeVideoId: "yt-fail", Transcript);
        var distiller = new ConfigurableDistillationService(clipCount: 6, throwOnClips: true);

        var result = await CreateOrchestrator(videoStore, distiller).DistillAsync(
            limit: 10,
            dryRun: false,
            isSubscriptionProvider: true,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.DistilledVideos);
        Assert.Contains("yt-fail", result.FailedVideoIds);
    }

    [Fact]
    public async Task DistillAsync_DryRun_HasEmptyDistilledVideos()
    {
        var videoStore = new ClipCountTestVideoStore();
        videoStore.AddPendingYoutube(SourceId, videoId: 50, youtubeVideoId: "yt-dry", Transcript);
        var distiller = new ConfigurableDistillationService(clipCount: 6);

        var result = await CreateOrchestrator(videoStore, distiller).DistillAsync(
            limit: 10,
            dryRun: true,
            isSubscriptionProvider: true,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.DryRun);
        Assert.Empty(result.DistilledVideos);
    }

    [Fact]
    public async Task DistillAsync_MultipleVideos_OrderingMatchesProcessingOrder()
    {
        var videoStore = new ClipCountTestVideoStore();
        videoStore.AddPendingYoutube(SourceId, videoId: 60, youtubeVideoId: "yt-first", Transcript);
        videoStore.AddPendingYoutube(SourceId, videoId: 61, youtubeVideoId: "yt-second", Transcript);
        var distiller = new ConfigurableDistillationService(clipCount: 5);

        var result = await CreateOrchestrator(videoStore, distiller).DistillAsync(
            limit: 10,
            dryRun: false,
            isSubscriptionProvider: true,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Collection(
            result.DistilledVideos,
            first => Assert.Equal("yt-first", first.NaturalKeyValue),
            second => Assert.Equal("yt-second", second.NaturalKeyValue));
    }
}

/// <summary>
/// Configurable <see cref="ILlmDistillationService"/> for clip-count tests: controls clip count,
/// keep/drop verdict, and an injectable clip-extraction failure.
/// </summary>
internal sealed class ConfigurableDistillationService : ILlmDistillationService
{
    private readonly int _clipCount;
    private readonly string _verdict;
    private readonly bool _throwOnClips;

    public ConfigurableDistillationService(int clipCount, string verdict = "keep", bool throwOnClips = false)
    {
        _clipCount = clipCount;
        _verdict = verdict;
        _throwOnClips = throwOnClips;
    }

    public Task<SummaryResult> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
        => Task.FromResult(new SummaryResult("summary", new TokenUsage(100, 10)));

    public Task<ClassificationResult> ClassifyAsync(string transcript, CancellationToken cancellationToken = default)
        => Task.FromResult(new ClassificationResult(_verdict, "test"));

    public Task<ClipsResult> ExtractClipsAsync(string transcript, CancellationToken cancellationToken = default)
    {
        if (_throwOnClips)
        {
            throw new InvalidOperationException("clip extraction failed (test injection)");
        }

        // Why: ValidateClips rejects an all-zero-timestamp clip set, so use ascending timestamps.
        var clips = Enumerable.Range(0, _clipCount)
            .Select(i => new ClipItem((i + 1) * 30, $"clip-{i}"))
            .ToArray();
        return Task.FromResult(new ClipsResult(clips, new TokenUsage(200, 20)));
    }

    public Task<TagsResult> InferTagsAsync(string transcript, CancellationToken cancellationToken = default)
        => Task.FromResult(new TagsResult(["combo"], ["cEDH"], ["win-cons"], new TokenUsage(30, 3)));
}

/// <summary>
/// In-memory <see cref="IContentSiteIndexStore"/> for the clip-count tests. Unlike the shared
/// fake it returns <c>null</c> from <see cref="GetByNaturalKeyAsync"/> so the drop path's
/// "delete existing row" branch is exercised without throwing.
/// </summary>
internal sealed class ClipCountTestIndexStore : IContentSiteIndexStore
{
    public List<ContentSiteIndexRow> UpsertedRows { get; } = [];

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        UpsertedRows.Add(row);
        return Task.CompletedTask;
    }

    public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        UpsertedRows.Add(row);
        return Task.CompletedTask;
    }

    public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default)
        => Task.FromResult<ContentSiteIndexRow?>(null);

    public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ContentSiteIndexRow?> GetPublishedByIdAsync(long id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> SetApprovalStatusAsync(string naturalKeyType, string naturalKeyValue, string status, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> SetApprovalStatusAsync(IReadOnlyList<(string Type, string Value)> keys, string status, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> StampPushedToProdAsync(IReadOnlyList<(string Type, string Value)> keys, DateTimeOffset pushedUtc, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<int> SetVisibilityAsync(IReadOnlyList<(string Type, string Value)> keys, bool visible, CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

/// <summary>
/// In-memory <see cref="IContentVideoStore"/> supporting both YouTube- and podcast-keyed pending
/// videos for the clip-count surfacing tests.
/// </summary>
internal sealed class ClipCountTestVideoStore : IContentVideoStore
{
    private readonly Dictionary<long, List<ContentVideo>> _pendingBySource = [];
    private readonly Dictionary<long, ContentTranscriptBody> _transcriptsByVideoId = [];
    private readonly Dictionary<long, string> _distillStatusByVideoId = [];

    /// <summary>Seeds a pending YouTube-keyed video with a transcript.</summary>
    public void AddPendingYoutube(long sourceId, long videoId, string youtubeVideoId, string transcriptBody)
        => Add(sourceId, videoId, youtubeVideoId, rssGuid: null, transcriptBody, $"https://www.youtube.com/watch?v={youtubeVideoId}");

    /// <summary>Seeds a pending podcast-keyed video (RssGuid set, YoutubeVideoId null) with a transcript.</summary>
    public void AddPendingPodcast(long sourceId, long videoId, string rssGuid, string transcriptBody)
        => Add(sourceId, videoId, youtubeVideoId: null, rssGuid, transcriptBody, $"https://example.com/podcast/{rssGuid}");

    private void Add(long sourceId, long videoId, string? youtubeVideoId, string? rssGuid, string transcriptBody, string videoUrl)
    {
        var video = new ContentVideo
        {
            Id = videoId,
            SourceId = sourceId,
            YoutubeVideoId = youtubeVideoId,
            RssGuid = rssGuid,
            Title = $"Test Video {videoId}",
            VideoUrl = videoUrl,
            TranscriptStatus = TranscriptStatus.Captions,
            CreatedUtc = DateTimeOffset.Parse("2026-06-15T00:00:00Z"),
        };

        if (!_pendingBySource.TryGetValue(sourceId, out var videos))
        {
            videos = [];
            _pendingBySource[sourceId] = videos;
        }

        videos.Add(video);
        _transcriptsByVideoId[videoId] = new ContentTranscriptBody
        {
            Body = transcriptBody,
            Source = TranscriptSource.Captions,
        };
    }

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<ContentVideo>> ListVideosPendingDistillAsync(long sourceId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ContentVideo>>(_pendingBySource.GetValueOrDefault(sourceId) ?? []);

    public Task<ContentTranscriptBody?> GetLatestTranscriptAsync(long videoId, CancellationToken cancellationToken = default)
        => Task.FromResult(_transcriptsByVideoId.GetValueOrDefault(videoId));

    public Task<string?> GetDistillStatusAsync(long videoId, CancellationToken cancellationToken = default)
        => Task.FromResult(_distillStatusByVideoId.GetValueOrDefault(videoId));

    public Task SetDistillStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
    {
        _distillStatusByVideoId[videoId] = status;
        return Task.CompletedTask;
    }

    public Task ClearDistillOutputAsync(long videoId, CancellationToken cancellationToken = default)
    {
        _distillStatusByVideoId.Remove(videoId);
        return Task.CompletedTask;
    }

    public Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
        => Task.FromResult(1L);

    public Task<long> InsertClipAsync(long videoId, int timestampS, string excerpt, int sortOrder, CancellationToken cancellationToken = default)
        => Task.FromResult(1L);

    public Task<long> InsertTagAsync(long videoId, string dimension, string tagValue, CancellationToken cancellationToken = default)
        => Task.FromResult(1L);

    public Task<long> InsertVideoAsync(long sourceId, string? youtubeVideoId, string? rssGuid, string title, string videoUrl, DateTimeOffset? publishedUtc, string transcriptStatus, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ContentVideo?> GetVideoByYoutubeIdAsync(long sourceId, string youtubeVideoId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task UpdateTranscriptStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<long> InsertTranscriptAsync(long videoId, string source, string body, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
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
