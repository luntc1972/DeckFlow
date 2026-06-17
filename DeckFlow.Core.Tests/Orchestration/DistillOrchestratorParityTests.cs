using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Core.Tests;

public sealed class DistillOrchestratorParityTests
{
    private const string MeteredRefusalMessage = "classifier requires the subscription LLM CLI (set DECKFLOW_LLM_PROVIDER to a subscription provider); refusing to run an unmetered classifier on a metered provider.";

    [Fact]
    public async Task DistillAsync_MeteredProviderRefusal_ReturnsExactAbortReason_WithoutWrites()
    {
        var sourceStore = new FakeContentSourceStore(
        [
            new ContentSource
            {
                Id = 5,
                SourceSlug = "play-to-win",
                DisplayName = "Play to Win",
                SourceType = ContentSourceType.Youtube,
                SourceUrl = "https://www.youtube.com/@playtowinmtg",
                IsEnabled = true,
                CreatedUtc = DateTimeOffset.Parse("2026-06-12T00:00:00Z")
            }
        ]);
        var videoStore = new FakeContentVideoStore();
        var indexStore = new FakeContentSiteIndexStore();

        var result = await CreateOrchestrator(sourceStore, videoStore, indexStore)
            .DistillAsync(
                limit: 10,
                dryRun: false,
                isSubscriptionProvider: false,
                videoIds: null,
                progress: null,
                cancellationToken: CancellationToken.None);

        Assert.False(result.Success); // CLI maps metered-provider refusal -> exit 1.
        Assert.False(result.DryRun);
        Assert.Equal(MeteredRefusalMessage, result.AbortedReason);
        Assert.Empty(videoStore.Summaries);
        Assert.Empty(videoStore.Clips);
        Assert.Empty(videoStore.StatusUpdates);
        Assert.Empty(indexStore.UpsertedRows);
    }

    private static ContentKbOrchestrator CreateOrchestrator(
        FakeContentSourceStore sourceStore,
        FakeContentVideoStore videoStore,
        FakeContentSiteIndexStore indexStore)
        => new(
            sourceStore,
            videoStore,
            indexStore,
            new ThrowingBlockedVideoStore(),
            new ThrowingContentHarvestRunStore(),
            new ThrowingLlmSpendLedger(),
            new ThrowingWhisperSpendLedger(),
            new FakeLlmDistillationService(),
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker(),
            () => DateTimeOffset.Parse("2026-06-13T00:00:00Z"),
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-distill-parity"),
            });
}
