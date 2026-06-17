using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="ContentKbOrchestrator.ListPendingDistillAsync"/> covering the union
/// across enabled sources, null/empty-id skipping, and de-duplication by YouTube video id.
/// </summary>
public sealed class ListPendingDistillTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-15T00:00:00Z");

    [Fact]
    public async Task ListPendingDistillAsync_UnionsAcrossEnabledSources()
    {
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(sourceId: 1, Video(10, "vid-a", "Title A", "https://youtu.be/vid-a"), "transcript a");
        videoStore.AddPending(sourceId: 2, Video(20, "vid-b", "Title B", "https://youtu.be/vid-b"), "transcript b");

        var result = await CreateOrchestrator(videoStore, EnabledSource(1), EnabledSource(2))
            .ListPendingDistillAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        var a = Assert.Single(result, p => p.YoutubeVideoId == "vid-a");
        Assert.Equal("Title A", a.Title);
        Assert.Equal("https://youtu.be/vid-a", a.VideoUrl);
        Assert.Contains(result, p => p.YoutubeVideoId == "vid-b");
    }

    [Fact]
    public async Task ListPendingDistillAsync_SkipsVideosWithNullYoutubeId()
    {
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(sourceId: 1, Video(10, youtubeVideoId: null, "No Id", "https://example.test/no-id"), "transcript");
        videoStore.AddPending(sourceId: 1, Video(11, "vid-c", "Has Id", "https://youtu.be/vid-c"), "transcript c");

        var result = await CreateOrchestrator(videoStore, EnabledSource(1))
            .ListPendingDistillAsync(CancellationToken.None);

        var only = Assert.Single(result);
        Assert.Equal("vid-c", only.YoutubeVideoId);
    }

    [Fact]
    public async Task ListPendingDistillAsync_DedupsSameYoutubeIdAcrossSources()
    {
        var videoStore = new FakeContentVideoStore();
        videoStore.AddPending(sourceId: 1, Video(10, "dupe", "From Source 1", "https://youtu.be/dupe"), "transcript 1");
        videoStore.AddPending(sourceId: 2, Video(20, "dupe", "From Source 2", "https://youtu.be/dupe"), "transcript 2");

        var result = await CreateOrchestrator(videoStore, EnabledSource(1), EnabledSource(2))
            .ListPendingDistillAsync(CancellationToken.None);

        var only = Assert.Single(result);
        Assert.Equal("dupe", only.YoutubeVideoId);
        // Why: first occurrence (source 1) is preserved.
        Assert.Equal("From Source 1", only.Title);
    }

    private static ContentVideo Video(long id, string? youtubeVideoId, string title, string videoUrl)
        => new()
        {
            Id = id,
            SourceId = 0,
            YoutubeVideoId = youtubeVideoId,
            Title = title,
            VideoUrl = videoUrl,
            PublishedUtc = Now,
            TranscriptStatus = TranscriptStatus.Captions,
            CreatedUtc = Now,
        };

    private static ContentSource EnabledSource(long id)
        => new()
        {
            Id = id,
            SourceSlug = $"source-{id}",
            DisplayName = $"Source {id}",
            SourceType = ContentSourceType.Youtube,
            SourceUrl = $"https://www.youtube.com/@source{id}",
            IsEnabled = true,
            CreatedUtc = Now,
        };

    private static ContentKbOrchestrator CreateOrchestrator(
        FakeContentVideoStore videoStore,
        params ContentSource[] sources)
        => new(
            new FakeContentSourceStore(sources),
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
            () => Now,
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-pending-distill-tests"),
            });
}
