using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;

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
        var artifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-validate-clips-tests");
        var orchestrator = new ContentKbOrchestrator(
            sourceStore,
            videoStore,
            indexStore,
            new ThrowingBlockedVideoStore(),
            new FakeContentHarvestRunStore(),
            new FakeLlmSpendLedger(),
            new ThrowingWhisperSpendLedger(),
            distiller,
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker(),
            () => new DateTimeOffset(2026, 5, 27, 12, 34, 56, TimeSpan.Zero),
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = artifactRoot,
            });

        var result = await orchestrator.DistillAsync(
            limit: 10,
            dryRun: false,
            isSubscriptionProvider: true,
            videoIds: null,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal([new StatusUpdate(10, "failed")], videoStore.StatusUpdates);
        Assert.Empty(videoStore.Summaries);
        Assert.Empty(videoStore.Clips);
        Assert.Empty(indexStore.UpsertedRows);
    }
}
