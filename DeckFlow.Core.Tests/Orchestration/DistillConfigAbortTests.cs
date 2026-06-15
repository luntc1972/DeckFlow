using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Verifies that the orchestrator converts a <see cref="LlmCliConfigurationException"/>
/// thrown by the distiller into a single run abort (AbortedReason set, DistillFailed not
/// inflated, videos not marked Failed) — quick task 260615-c9e.
/// </summary>
public sealed class DistillConfigAbortTests
{
    private const long SourceId = 1;

    private static ContentKbOrchestrator CreateOrchestrator(
        ILlmDistillationService distiller,
        IContentVideoStore videoStore)
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
            distiller,
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker(),
            () => DateTimeOffset.Parse("2026-06-15T00:00:00Z"),
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-config-abort-tests"),
            });
    }

    [Fact]
    public async Task DistillAsync_DistillerThrowsConfigException_AbortedReasonSetDistillFailedZero()
    {
        var videoStore = new DistillTestVideoStore();
        videoStore.AddPending(SourceId, 10, "vid1", "transcript one");
        videoStore.AddPending(SourceId, 11, "vid2", "transcript two");

        var distiller = new ConfigErrorLlmDistillationService("DECKFLOW_LLM_CLI_COMMAND must be set");
        var orchestrator = CreateOrchestrator(distiller, videoStore);

        var result = await orchestrator.DistillAsync(
            limit: 10,
            dryRun: false,
            isSubscriptionProvider: true,
            progress: null,
            cancellationToken: CancellationToken.None);

        // The run should still return Success=true with the AbortedReason populated.
        // (The outer distill loop catches the abort-reason and breaks, then returns the
        // DistillResult with AbortedReason set. Success=true is preserved because the
        // abort reason is surfaced via AbortedReason, not by throwing.)
        Assert.NotNull(result.AbortedReason);
        Assert.Contains("not configured", result.AbortedReason, StringComparison.OrdinalIgnoreCase);

        // DistillFailed must NOT be inflated — config errors are not the video's fault.
        Assert.Equal(0, result.DistillFailed);

        // No videos were successfully distilled.
        Assert.Equal(0, result.VideosDistilled);
    }

    [Fact]
    public async Task DistillAsync_DistillerThrowsConfigException_VideoNotMarkedFailed()
    {
        var videoStore = new TrackingDistillTestVideoStore();
        videoStore.AddPending(SourceId, 20, "vid3", "transcript three");
        videoStore.AddPending(SourceId, 21, "vid4", "transcript four");

        var distiller = new ConfigErrorLlmDistillationService("DECKFLOW_LLM_CLI_COMMAND must be set");
        var orchestrator = CreateOrchestrator(distiller, videoStore);

        await orchestrator.DistillAsync(
            limit: 10,
            dryRun: false,
            isSubscriptionProvider: true,
            progress: null,
            cancellationToken: CancellationToken.None);

        // Verify no video was marked Failed (the config-abort path must not call
        // SetDistillStatusAsync with the "failed" status value).
        var failedStatuses = videoStore.SetStatusCalls
            .Where(call => string.Equals(call.Status, DistillationValidation.DistillStatusFailed, StringComparison.Ordinal))
            .ToList();
        Assert.Empty(failedStatuses);
    }

    [Fact]
    public async Task DistillAsync_DistillerThrowsConfigException_AbortReasonContainsExceptionMessage()
    {
        const string configMessage = "DECKFLOW_LLM_CLI_COMMAND must be a JSON array";
        var videoStore = new DistillTestVideoStore();
        videoStore.AddPending(SourceId, 30, "vid5", "transcript five");

        var distiller = new ConfigErrorLlmDistillationService(configMessage);
        var orchestrator = CreateOrchestrator(distiller, videoStore);

        var result = await orchestrator.DistillAsync(
            limit: 10,
            dryRun: false,
            isSubscriptionProvider: true,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result.AbortedReason);
        Assert.Contains(configMessage, result.AbortedReason, StringComparison.Ordinal);
    }
}

/// <summary>
/// Extends <see cref="DistillTestVideoStore"/> by recording every
/// <see cref="IContentVideoStore.SetDistillStatusAsync"/> call so tests can assert
/// that the config-abort path never marks a video as Failed.
/// </summary>
internal sealed class TrackingDistillTestVideoStore : IContentVideoStore
{
    private readonly Dictionary<long, List<ContentVideo>> _pendingBySource = [];
    private readonly Dictionary<long, ContentTranscriptBody> _transcriptsByVideoId = [];
    private readonly Dictionary<long, string> _distillStatusByVideoId = [];

    public List<(long VideoId, string Status)> SetStatusCalls { get; } = [];

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
        SetStatusCalls.Add((videoId, status));
        return Task.CompletedTask;
    }

    public Task ClearDistillOutputAsync(long videoId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

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

/// <summary>
/// Distillation service stub that throws <see cref="LlmCliConfigurationException"/>
/// on the first call, simulating a misconfigured CLI distiller.
/// </summary>
internal sealed class ConfigErrorLlmDistillationService : ILlmDistillationService
{
    private readonly string _message;

    public ConfigErrorLlmDistillationService(string message)
    {
        _message = message;
    }

    public Task<SummaryResult> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
        => throw new LlmCliConfigurationException(_message);

    public Task<ClassificationResult> ClassifyAsync(string transcript, CancellationToken cancellationToken = default)
        => throw new LlmCliConfigurationException(_message);

    public Task<ClipsResult> ExtractClipsAsync(string transcript, CancellationToken cancellationToken = default)
        => throw new LlmCliConfigurationException(_message);

    public Task<TagsResult> InferTagsAsync(string transcript, CancellationToken cancellationToken = default)
        => throw new LlmCliConfigurationException(_message);
}
