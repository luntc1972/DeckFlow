using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration-style tests for the <see cref="ContentKbOrchestrator"/> redistill flag
/// using in-memory fake stores (no SQLite, no mocking library).
/// </summary>
public sealed class ContentKbOrchestratorDistillTests
{
    private const long SourceId = 1;
    private const long VideoId = 42;
    private const string YoutubeVideoId = "abc123";
    private const string Transcript = "This is a test transcript for re-distillation.";

    // The natural key the orchestrator derives: youtube_video_id when set.
    private const string NaturalKey = YoutubeVideoId;

    private static ContentKbOrchestrator CreateOrchestrator(
        DistillTestVideoStore videoStore)
    {
        var sourceStore = new FakeContentSourceStore(
        [
            new ContentSource
            {
                Id = SourceId,
                SourceSlug = "test-source",
                DisplayName = "Test Source",
                SourceType = ContentSourceType.Youtube,
                SourceUrl = "https://www.youtube.com/@test",
                IsEnabled = true,
                CreatedUtc = DateTimeOffset.Parse("2026-06-15T00:00:00Z"),
            }
        ]);

        return new ContentKbOrchestrator(
            sourceStore,
            videoStore,
            new FakeContentSiteIndexStore(),
            new ThrowingBlockedVideoStore(),
            new FakeContentHarvestRunStore(),
            new FakeLlmSpendLedger(),
            new ThrowingWhisperSpendLedger(),
            new FakeLlmDistillationService(),
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker(),
            () => DateTimeOffset.Parse("2026-06-15T00:00:00Z"),
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-redistill-tests"),
            });
    }

    [Fact]
    public async Task DistillAsync_DefaultRedistillFalse_SkipsAlreadyDistilledTargetedVideo()
    {
        var videoStore = new DistillTestVideoStore();
        videoStore.AddPending(SourceId, VideoId, YoutubeVideoId, Transcript);
        videoStore.SetDistillStatus(VideoId, DistillationValidation.DistillStatusDistilled);

        // redistill omitted (defaults to false) — the video is in videoIds but already distilled
        var result = await CreateOrchestrator(videoStore).DistillAsync(
            limit: 10,
            dryRun: true,
            isSubscriptionProvider: true,
            videoIds: [NaturalKey],
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        // Why: WouldRun=0 proves the already-distilled skip was NOT bypassed.
        Assert.Equal(0, result.WouldRun);
        Assert.False(videoStore.ClearDistillOutputCalled, "ClearDistillOutputAsync must not be called when redistill=false");
    }

    [Fact]
    public async Task DistillAsync_RedistillTrue_ReprocessesTargetedDistilledVideo()
    {
        var videoStore = new DistillTestVideoStore();
        videoStore.AddPending(SourceId, VideoId, YoutubeVideoId, Transcript);
        videoStore.SetDistillStatus(VideoId, DistillationValidation.DistillStatusDistilled);

        // redistill=true with the video in videoIds — the already-distilled skip is bypassed
        var result = await CreateOrchestrator(videoStore).DistillAsync(
            limit: 10,
            dryRun: true,
            isSubscriptionProvider: true,
            redistill: true,
            videoIds: [NaturalKey],
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        // Why: WouldRun=1 proves the skip was bypassed and the video was counted for re-distillation.
        Assert.Equal(1, result.WouldRun);
    }
}

/// <summary>
/// Minimal <see cref="IContentVideoStore"/> supporting distill status control and
/// ClearDistillOutput tracking for the redistill tests.
/// </summary>
internal sealed class DistillTestVideoStore : IContentVideoStore
{
    private readonly Dictionary<long, List<ContentVideo>> _pendingBySource = [];
    private readonly Dictionary<long, ContentTranscriptBody> _transcriptsByVideoId = [];
    private readonly Dictionary<long, string> _distillStatusByVideoId = [];

    /// <summary>Gets whether <see cref="ClearDistillOutputAsync"/> was called for any video.</summary>
    public bool ClearDistillOutputCalled { get; private set; }

    /// <summary>Seeds a pending video with a transcript so the orchestrator can find it.</summary>
    public void AddPending(long sourceId, long videoId, string youtubeVideoId, string transcriptBody)
    {
        var video = new ContentVideo
        {
            Id = videoId,
            SourceId = sourceId,
            YoutubeVideoId = youtubeVideoId,
            Title = $"Test Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
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

    /// <summary>Pre-sets the distill status to simulate an already-distilled video.</summary>
    public void SetDistillStatus(long videoId, string status) => _distillStatusByVideoId[videoId] = status;

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<ContentVideo>> ListVideosPendingDistillAsync(long sourceId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ContentVideo>>(_pendingBySource.GetValueOrDefault(sourceId) ?? []);

    public Task<IReadOnlyList<PendingDistillProjection>> ListPendingDistillDisplayAsync(long sourceId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PendingDistillProjection>>(
            (_pendingBySource.GetValueOrDefault(sourceId) ?? [])
            .Select(video => new PendingDistillProjection
            {
                YoutubeVideoId = video.YoutubeVideoId,
                Title = video.Title,
                VideoUrl = video.VideoUrl,
                PublishedUtc = video.PublishedUtc,
                DistillStatus = null,
            })
            .ToArray());

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
        ClearDistillOutputCalled = true;
        _distillStatusByVideoId.Remove(videoId);
        return Task.CompletedTask;
    }

    // Distill path writes — no-op for test assertions
    public Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
        => Task.FromResult(1L);

    public Task<long> InsertClipAsync(long videoId, int timestampS, string excerpt, int sortOrder, CancellationToken cancellationToken = default)
        => Task.FromResult(1L);

    public Task<long> InsertTagAsync(long videoId, string dimension, string tagValue, CancellationToken cancellationToken = default)
        => Task.FromResult(1L);

    // Remaining interface members not used in distill path
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
