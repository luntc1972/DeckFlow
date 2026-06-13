using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Core.Tests;

public sealed class ContentSourceOrchestratorParityTests
{
    private const string UnsupportedTypeMessage = "Unsupported content source type 'podcast'. Use youtube_channel or podcast_rss.";

    [Fact]
    public async Task AddSourceAsync_InvalidType_ReturnsExactCliMessage_WithoutStoreInsert()
    {
        var sourceStore = new RecordingContentSourceStore();

        var result = await CreateOrchestrator(sourceStore)
            .AddSourceAsync(
                "https://example.com/feed.xml",
                "The Brewcast",
                "podcast",
                progress: null,
                cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ContentSourceResult.ContentSourceOutcome.InvalidType, result.Outcome); // CLI maps InvalidType -> exit 2.
        Assert.Equal(UnsupportedTypeMessage, result.Message);
        Assert.False(sourceStore.InsertCalled);
    }

    [Fact]
    public async Task AddSourceAsync_AlreadyExistsSameUrl_ReturnsSuccessOutcome()
    {
        const string url = "https://www.youtube.com/@playtowinmtg";
        var sourceStore = new RecordingContentSourceStore
        {
            InsertException = new InvalidOperationException("UNIQUE constraint failed: content_sources.source_url"),
            EnabledSources =
            [
                new ContentSource
                {
                    Id = 12,
                    SourceSlug = "play-to-win",
                    DisplayName = "Play to Win",
                    SourceType = ContentSourceType.Youtube,
                    SourceUrl = url,
                    IsEnabled = true,
                    CreatedUtc = DateTimeOffset.Parse("2026-06-12T00:00:00Z")
                }
            ]
        };

        var result = await CreateOrchestrator(sourceStore)
            .AddSourceAsync(
                url,
                "Play to Win",
                ContentSourceType.Youtube,
                progress: null,
                cancellationToken: CancellationToken.None);

        Assert.True(result.Success); // CLI maps AlreadyExistsSameUrl -> exit 0.
        Assert.Equal(ContentSourceResult.ContentSourceOutcome.AlreadyExistsSameUrl, result.Outcome);
        Assert.Equal("play-to-win", result.Slug);
        Assert.Equal("source already exists (same url)", result.Message);
        Assert.True(sourceStore.InsertCalled);
    }

    [Fact]
    public async Task AddSourceAsync_SlugConflict_ReturnsConflictOutcome()
    {
        var sourceStore = new RecordingContentSourceStore
        {
            InsertException = new InvalidOperationException("UNIQUE constraint failed: content_sources.source_slug"),
            EnabledSources =
            [
                new ContentSource
                {
                    Id = 27,
                    SourceSlug = "play-to-win",
                    DisplayName = "Play to Win",
                    SourceType = ContentSourceType.Youtube,
                    SourceUrl = "https://www.youtube.com/@playtowinmtg",
                    IsEnabled = true,
                    CreatedUtc = DateTimeOffset.Parse("2026-06-12T00:00:00Z")
                }
            ]
        };

        var result = await CreateOrchestrator(sourceStore)
            .AddSourceAsync(
                "https://www.youtube.com/@playtowinpodcast",
                "Play to Win",
                ContentSourceType.Youtube,
                progress: null,
                cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ContentSourceResult.ContentSourceOutcome.SlugConflict, result.Outcome); // CLI maps SlugConflict -> exit 3.
        Assert.Equal("play-to-win", result.Slug);
        Assert.Equal("slug 'play-to-win' already used by a different url - pass a distinct --name", result.Message);
    }

    [Fact]
    public async Task AddSourceAsync_Added_ReturnsInsertedIdAndSlug()
    {
        var sourceStore = new RecordingContentSourceStore
        {
            InsertResult = 41,
        };

        var result = await CreateOrchestrator(sourceStore)
            .AddSourceAsync(
                "https://www.youtube.com/@cedhtv",
                "cEDH TV",
                ContentSourceType.Youtube,
                progress: null,
                cancellationToken: CancellationToken.None);

        Assert.True(result.Success); // CLI maps Added -> exit 0.
        Assert.Equal(ContentSourceResult.ContentSourceOutcome.Added, result.Outcome);
        Assert.Equal(41, result.Id);
        Assert.Equal("cedh-tv", result.Slug);
        Assert.Equal("Added content source 41: cedh-tv", result.Message);
        Assert.Equal(("cedh-tv", "cEDH TV", ContentSourceType.Youtube, "https://www.youtube.com/@cedhtv"), sourceStore.LastInsert);
    }

    private static ContentKbOrchestrator CreateOrchestrator(IContentSourceStore sourceStore)
        => new(
            sourceStore,
            new ThrowingContentVideoStore(),
            new ThrowingContentSiteIndexStore(),
            new ThrowingBlockedVideoStore(),
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
                ArtifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-content-source-parity"),
            });

    private sealed class RecordingContentSourceStore : IContentSourceStore
    {
        public IReadOnlyList<ContentSource> EnabledSources { get; init; } = [];

        public bool InsertCalled { get; private set; }

        public long InsertResult { get; init; } = 1;

        public Exception? InsertException { get; init; }

        public (string Slug, string Name, string Type, string Url)? LastInsert { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<long> InsertSourceAsync(
            string sourceSlug,
            string displayName,
            string sourceType,
            string sourceUrl,
            CancellationToken cancellationToken = default)
        {
            InsertCalled = true;
            LastInsert = (sourceSlug, displayName, sourceType, sourceUrl);

            if (InsertException is not null)
            {
                throw InsertException;
            }

            return Task.FromResult(InsertResult);
        }

        public Task<ContentSource?> GetSourceAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ContentSource>> ListEnabledSourcesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(EnabledSources);
    }
}
