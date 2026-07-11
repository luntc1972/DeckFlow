using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Tests;

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Errors { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel >= LogLevel.Error)
        {
            Errors.Add(formatter(state, exception) + (exception is null ? string.Empty : " -- " + exception));
        }
    }
}

/// <summary>
/// Confirms the publish/distill path stores <c>body_sha256</c> on the upserted
/// <see cref="ContentSiteIndexRow"/>, computed by the ONE shared
/// <see cref="ContentSiteIndexContentSignature.ComputeBodySha256"/> helper over the same
/// <c>SplitHeader</c> body the render guard reads later (D-01/D-02).
/// </summary>
public sealed class ContentKbOrchestratorBodyHashTests
{
    private const long SourceId = 1;
    private const long VideoId = 42;
    private const string YoutubeVideoId = "abc123";
    private const string Transcript = "This is a test transcript for body-hash publish coverage.";
    private const string NaturalKey = YoutubeVideoId;

    private static (ContentKbOrchestrator Orchestrator, FakeContentSiteIndexStore IndexStore, string ArtifactRoot, CapturingLogger<ContentKbOrchestrator> Logger) CreateOrchestrator()
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

        var indexStore = new FakeContentSiteIndexStore();
        var videoStore = new DistillTestVideoStore();
        videoStore.AddPending(SourceId, VideoId, YoutubeVideoId, Transcript);

        var artifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-bodyhash-tests", Guid.NewGuid().ToString("N"));
        var logger = new CapturingLogger<ContentKbOrchestrator>();

        var orchestrator = new ContentKbOrchestrator(
            sourceStore,
            videoStore,
            indexStore,
            new ThrowingBlockedVideoStore(),
            new FakeContentHarvestRunStore(),
            new FakeLlmSpendLedger(),
            new ThrowingWhisperSpendLedger(),
            new FakeLlmDistillationService
            {
                // Why: ValidateClips rejects an all-zero-timestamp clip set (DistillationValidation.cs:57);
                // the default fake fixture uses timestamp 0 for every clip, so override with one real
                // timestamp to reach the publish call this test exercises.
                ClipsResult = new ClipsResult(
                    [new ClipItem(90, "first"), new ClipItem(0, "second"), new ClipItem(180, "third")],
                    new TokenUsage(200, 20)),
            },
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker(),
            () => DateTimeOffset.Parse("2026-06-15T00:00:00Z"),
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = artifactRoot,
            },
            logger);

        return (orchestrator, indexStore, artifactRoot, logger);
    }

    [Fact]
    public async Task DistillAsync_PublishesArtifact_StoresBodySha256OnUpsertedRow()
    {
        var (orchestrator, indexStore, artifactRoot, logger) = CreateOrchestrator();

        var result = await orchestrator.DistillAsync(
            limit: 10,
            dryRun: false,
            isSubscriptionProvider: true,
            videoIds: [NaturalKey],
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.DistillFailed == 0, string.Join(" | ", logger.Errors));
        var upserted = Assert.Single(indexStore.UpsertedRows);

        // BEHAVIOR: the stored hash is 64-hex (a real SHA-256), not null/empty.
        Assert.NotNull(upserted.BodySha256);
        Assert.Equal(64, upserted.BodySha256!.Length);
        Assert.Matches("^[0-9a-f]{64}$", upserted.BodySha256);

        // BEHAVIOR: the stored hash equals ComputeBodySha256 of the exact artifact text written
        // to disk for this row — i.e. the same bytes the render guard will recompute later
        // (D-01/D-02), NOT a second independently-derived hash.
        var writtenArtifactPath = Assert.Single(
            Directory.GetFiles(artifactRoot, "*.md", SearchOption.AllDirectories),
            path => !path.EndsWith(".prompt.md", StringComparison.Ordinal));
        var writtenText = await File.ReadAllTextAsync(writtenArtifactPath);
        var expectedHash = ContentSiteIndexContentSignature.ComputeBodySha256(writtenText);
        Assert.Equal(expectedHash, upserted.BodySha256);
    }
}
