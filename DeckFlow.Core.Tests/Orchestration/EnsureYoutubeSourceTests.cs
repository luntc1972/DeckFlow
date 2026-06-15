using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="IContentSourceManager.EnsureYoutubeSourceAsync"/> covering the
/// new-source, already-exists-disabled, and already-exists-enabled paths.
/// </summary>
public sealed class EnsureYoutubeSourceTests
{
    // ── new channel ─────────────────────────────────────────────────────────

    [Fact]
    public async Task EnsureYoutubeSourceAsync_NewChannel_ReturnsAddedIdAndEnablesSource()
    {
        const string url = "https://www.youtube.com/@cedhtv";
        const long insertedId = 7;

        var store = new EnsureSourceStore
        {
            InsertResult = insertedId,
        };

        var result = await CreateOrchestrator(store)
            .EnsureYoutubeSourceAsync(url, "cEDH TV");

        Assert.True(result.Success);
        Assert.Equal(ContentSourceResult.ContentSourceOutcome.Added, result.Outcome);
        Assert.Equal(insertedId, result.Id);
        Assert.True(store.SetEnabledCalledFor.Contains(insertedId));
    }

    // ── already-exists, currently disabled ──────────────────────────────────

    [Fact]
    public async Task EnsureYoutubeSourceAsync_AlreadyExistsDisabled_ResolvesIdViaUrlAndEnables()
    {
        const string url = "https://www.youtube.com/@playtowinmtg";
        const long existingId = 12;

        // Why: InsertException simulates the UNIQUE constraint violation AddSourceAsync handles.
        // EnabledSources is empty so ListEnabledSourcesAsync (used inside AddSourceAsync's
        // HandleContentSourceUniqueViolationAsync) returns the source-exists-same-url branch.
        // ByUrlSource is the disabled row GetSourceByUrlAsync will return (Task 1).
        var store = new EnsureSourceStore
        {
            InsertException = new InvalidOperationException("UNIQUE constraint failed: content_sources.source_url"),
            EnabledSources =
            [
                new ContentSource
                {
                    Id = existingId,
                    SourceSlug = "play-to-win",
                    DisplayName = "Play to Win",
                    SourceType = ContentSourceType.Youtube,
                    SourceUrl = url,
                    IsEnabled = true,
                    CreatedUtc = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
                },
            ],
            ByUrlSource = new ContentSource
            {
                Id = existingId,
                SourceSlug = "play-to-win",
                DisplayName = "Play to Win",
                SourceType = ContentSourceType.Youtube,
                SourceUrl = url,
                IsEnabled = false,
                CreatedUtc = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            },
        };

        var result = await CreateOrchestrator(store)
            .EnsureYoutubeSourceAsync(url, "Play to Win");

        Assert.True(result.Success);
        Assert.Equal(ContentSourceResult.ContentSourceOutcome.AlreadyExistsSameUrl, result.Outcome);
        Assert.Equal(existingId, result.Id);
        // Why: must enable the source even when it was previously disabled (idempotent enable).
        Assert.True(store.SetEnabledCalledFor.Contains(existingId));
    }

    // ── already-exists, already enabled ─────────────────────────────────────

    [Fact]
    public async Task EnsureYoutubeSourceAsync_AlreadyExistsEnabled_ReturnsExistingId_Idempotent()
    {
        const string url = "https://www.youtube.com/@saltysue";
        const long existingId = 3;

        var enabledSource = new ContentSource
        {
            Id = existingId,
            SourceSlug = "salty-sue",
            DisplayName = "Salty Sue",
            SourceType = ContentSourceType.Youtube,
            SourceUrl = url,
            IsEnabled = true,
            CreatedUtc = DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
        };

        var store = new EnsureSourceStore
        {
            InsertException = new InvalidOperationException("UNIQUE constraint failed: content_sources.source_url"),
            EnabledSources = [enabledSource],
            ByUrlSource = enabledSource,
        };

        var result = await CreateOrchestrator(store)
            .EnsureYoutubeSourceAsync(url, "Salty Sue");

        Assert.True(result.Success);
        Assert.Equal(existingId, result.Id);
        // Why: SetEnabledAsync(id, true) is idempotent — calling it on an already-enabled source
        // is harmless and must not cause an error.
        Assert.True(store.SetEnabledCalledFor.Contains(existingId));
    }

    // ── by-url store integration tests ──────────────────────────────────────

    [Fact]
    public async Task GetSourceByUrlAsync_KnownUrl_ReturnsSource()
    {
        const string url = "https://www.youtube.com/@cedhtv";
        var store = new EnsureSourceStore
        {
            ByUrlSource = new ContentSource
            {
                Id = 1,
                SourceSlug = "cedh-tv",
                DisplayName = "cEDH TV",
                SourceType = ContentSourceType.Youtube,
                SourceUrl = url,
                IsEnabled = true,
                CreatedUtc = DateTimeOffset.Parse("2026-06-15T00:00:00Z"),
            },
        };

        // Why: test the store interface method directly — this is distinct from orchestrator logic.
        var source = await store.GetSourceByUrlAsync(url);

        Assert.NotNull(source);
        Assert.Equal(1, source!.Id);
        Assert.Equal("cedh-tv", source.SourceSlug);
    }

    [Fact]
    public async Task GetSourceByUrlAsync_UnknownUrl_ReturnsNull()
    {
        var store = new EnsureSourceStore();

        var source = await store.GetSourceByUrlAsync("https://www.youtube.com/@nobody");

        Assert.Null(source);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

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
            () => DateTimeOffset.Parse("2026-06-15T00:00:00Z"),
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-ensure-youtube-source-tests"),
            });

    /// <summary>
    /// Minimal fake store supporting the operations exercised by EnsureYoutubeSourceAsync.
    /// </summary>
    private sealed class EnsureSourceStore : IContentSourceStore
    {
        public IReadOnlyList<ContentSource> EnabledSources { get; init; } = [];

        public long InsertResult { get; init; } = 1;

        public Exception? InsertException { get; init; }

        public ContentSource? ByUrlSource { get; init; }

        public List<long> SetEnabledCalledFor { get; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<long> InsertSourceAsync(
            string sourceSlug,
            string displayName,
            string sourceType,
            string sourceUrl,
            CancellationToken cancellationToken = default)
        {
            if (InsertException is not null)
            {
                throw InsertException;
            }

            return Task.FromResult(InsertResult);
        }

        public Task<ContentSource?> GetSourceAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentSource?> GetSourceByUrlAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult(
                string.Equals(ByUrlSource?.SourceUrl, url, StringComparison.Ordinal) ? ByUrlSource : null);

        public Task SetEnabledAsync(long id, bool isEnabled, CancellationToken cancellationToken = default)
        {
            SetEnabledCalledFor.Add(id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ContentSource>> ListEnabledSourcesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(EnabledSources);
    }
}
