using System.Diagnostics;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for <see cref="ArchidektDeckCacheSession"/> covering harvest-run pagination,
/// per-deck import, and knowledge-cache persistence against a temporary SQLite database.
/// </summary>
public sealed class ArchidektDeckCacheSessionTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _tempDirectory;

    public ArchidektDeckCacheSessionTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    [Fact]
    public void ArchidektCacheRunResult_DecksDrained_SumsEveryDisposition()
    {
        var result = new ArchidektCacheRunResult(1, 2, 3, 4, 5, TimeSpan.Zero);

        Assert.Equal(10, result.DecksDrained);
    }

    [Fact]
    public async Task RunAsync_WaitsForFullDurationWhenQueueRunsDry()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();

        var session = new ArchidektDeckCacheSession(
            repository,
            new FakeDeckImporter(),
            new FakeRecentDecksImporter(),
            idlePollDelay: TimeSpan.FromMilliseconds(20));

        var stopwatch = Stopwatch.StartNew();
        await session.RunAsync(TimeSpan.FromMilliseconds(70), cancellationToken: CancellationToken.None);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds >= 60, $"Expected the session to stay alive near the requested duration, but it completed in {stopwatch.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public async Task RunAsync_WaitsForFullDurationWhenRecentDeckFetchFails()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();

        var session = new ArchidektDeckCacheSession(
            repository,
            new FakeDeckImporter(),
            new ThrowingRecentDecksImporter(),
            idlePollDelay: TimeSpan.FromMilliseconds(20));

        var stopwatch = Stopwatch.StartNew();
        await session.RunAsync(TimeSpan.FromMilliseconds(70), cancellationToken: CancellationToken.None);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds >= 60, $"Expected the session to keep retrying near the requested duration after recent-deck fetch errors, but it completed in {stopwatch.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public async Task RunAsync_RateLimitTripDuringListing_ThrowsAfterOneListingCall()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        var recentImporter = new ThrowingRecentDecksImporter(() => new ArchidektRateLimitedException("Simulated Archidekt rate limit.", TimeSpan.FromSeconds(120)));
        var session = new ArchidektDeckCacheSession(repository, new FakeDeckImporter(), recentImporter, idlePollDelay: TimeSpan.FromMilliseconds(1));

        var result = await session.RunAsync(TimeSpan.FromSeconds(2), fetchBatchSize: 1);

        Assert.Equal(1, recentImporter.Calls);
        Assert.True(result.RateLimited);
        Assert.Equal(TimeSpan.FromSeconds(120), result.RetryAfter);
    }

    [Fact]
    public async Task RunAsync_RateLimitTripDuringDeckImport_ThrowsAndLeavesTrippedDeckPending()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        await repository.AddDeckIdsAsync(new[] { "deck-1-ok", "deck-2-rate-limited", "deck-3-ok" });
        var importer = new ThrowingRateLimitedDeckImporter();
        var session = new ArchidektDeckCacheSession(repository, importer, new FakeRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(1));

        // Why: the exception derives from HttpRequestException, so without a dedicated catch the session's filters swallow it.
        var result = await session.RunAsync(TimeSpan.FromSeconds(5), fetchBatchSize: 3);

        Assert.Equal(new[] { "deck-1-ok", "deck-2-rate-limited" }, importer.AttemptedDeckIds);
        Assert.True(result.RateLimited);
        Assert.Equal(TimeSpan.FromMinutes(2), result.RetryAfter);
        Assert.Equal(1, result.DecksProcessed);
        Assert.True(await IsDeckProcessedAsync("deck-1-ok"));
        Assert.False(await IsDeckProcessedAsync("deck-2-rate-limited"));
        Assert.False(await IsDeckSkippedAsync("deck-2-rate-limited"));
        Assert.False(await IsDeckProcessedAsync("deck-3-ok"));
        Assert.False(await IsDeckSkippedAsync("deck-3-ok"));
    }

    [Fact]
    public async Task RunAsync_TransientDeckFailures_LeavesRowsPendingAndEndsAfterThree()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        await repository.AddDeckIdsAsync(new[] { "transient-1", "transient-2", "transient-3" });
        var importer = new ThrowingTransientDeckImporter();
        var session = new ArchidektDeckCacheSession(repository, importer, new FakeRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(1));

        var result = await session.RunAsync(TimeSpan.FromSeconds(5), fetchBatchSize: 3);

        Assert.Equal(new[] { "transient-1", "transient-2", "transient-3" }, importer.AttemptedDeckIds);
        Assert.Equal(0, result.DecksSkipped);
        Assert.True(result.EndedEarly);
        foreach (var deckId in importer.AttemptedDeckIds)
        {
            Assert.False(await IsDeckProcessedAsync(deckId));
            Assert.False(await IsDeckSkippedAsync(deckId));
        }
    }

    [Fact]
    public async Task RunAsync_OnlyAttemptedTransientDeckRemains_EndsBeforeDurationCap()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        await repository.AddDeckIdsAsync(["transient-1"]);
        var importer = new ThrowingTransientDeckImporter();
        var recentImporter = new FakeRecentDecksImporter();
        var session = new ArchidektDeckCacheSession(repository, importer, recentImporter, idlePollDelay: TimeSpan.FromMilliseconds(1));

        var stopwatch = Stopwatch.StartNew();
        await session.RunAsync(TimeSpan.FromSeconds(5), fetchBatchSize: 1);
        stopwatch.Stop();

        Assert.Equal(["transient-1"], importer.AttemptedDeckIds);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.InRange(recentImporter.Calls, 1, 5);
    }

    [Fact]
    public async Task RunAsync_UsesFetchBatchSizeForDeckProcessing()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        await repository.AddDeckIdsAsync(new[] { "100", "101", "102" });

        var importer = new FakeDeckImporter();
        var session = new ArchidektDeckCacheSession(
            repository,
            importer,
            new FakeRecentDecksImporter(),
            idlePollDelay: TimeSpan.FromMilliseconds(5));

        // Deterministic stop instead of racing a fixed wall-clock window: the old 300 ms budget made
        // this flaky under CI load (the per-deck duration check broke the loop after only 2 of 3
        // decks). RunAsync reports progress synchronously AFTER each deck is fully persisted, so
        // cancelling when the count reaches 3 means all three are done and the run breaks cleanly out
        // of the same per-deck check with the complete result (no idle Task.Delay is hit, so no throw).
        using var cancellation = new CancellationTokenSource();
        var stopWhenAllProcessed = new SynchronousProgress<int>(processed =>
        {
            if (processed >= 3)
            {
                cancellation.Cancel();
            }
        });

        // The duration is only a safety cap; the progress-driven cancel ends the run as soon as the
        // three queued decks are processed, regardless of how slow the runner is.
        var result = await session.RunAsync(
            TimeSpan.FromSeconds(5),
            queueBatchSize: 1,
            fetchBatchSize: 3,
            cancellationToken: cancellation.Token,
            progress: stopWhenAllProcessed);

        Assert.Equal(3, result.DecksProcessed);
        Assert.Equal(3, importer.ImportCalls);
    }

    [Fact]
    public async Task RunAsync_BacklogAtThreshold_CrawlsDeepPageAndAdvancesCursor()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        await repository.SetRecentDeckCrawlPageAsync(7);
        var recentImporter = new RecordingRecentDecksImporter();
        using var cancellation = new CancellationTokenSource();

        await new ArchidektDeckCacheSession(repository, new FakeDeckImporter(), recentImporter, idlePollDelay: TimeSpan.FromMilliseconds(1))
            .RunAsync(TimeSpan.FromSeconds(1), discoveryBacklogThreshold: 1, cancellationToken: cancellation.Token);

        Assert.Contains(recentImporter.RequestedPages, page => page > 1);
        Assert.True(await repository.GetRecentDeckCrawlPageAsync() > 7);
    }

    [Fact]
    public async Task RunAsync_BacklogAboveThreshold_SkipsDeepCrawlAndPreservesCursor()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        await repository.AddDeckIdsAsync(new[] { "queued" });
        await repository.SetRecentDeckCrawlPageAsync(7);
        var recentImporter = new RecordingRecentDecksImporter();
        var session = new ArchidektDeckCacheSession(
            repository,
            new FakeDeckImporter(),
            recentImporter,
            idlePollDelay: TimeSpan.FromMilliseconds(1));
        using var cancellation = new CancellationTokenSource();
        var stopAfterDeck = new SynchronousProgress<int>(_ => cancellation.Cancel());

        await session.RunAsync(
            TimeSpan.FromSeconds(1),
            discoveryBacklogThreshold: 0,
            cancellationToken: cancellation.Token,
            progress: stopAfterDeck);

        Assert.NotEmpty(recentImporter.RequestedPages);
        Assert.Contains(1, recentImporter.RequestedPages);
        Assert.Equal(7, await repository.GetRecentDeckCrawlPageAsync());
    }

    [Fact]
    public async Task RunAsync_RefreshOnlyListingPage_DrainsRefreshesWithoutCountingThemAsEnqueued()
    {
        var t0 = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var importer = new FakeDeckImporter { Metadata = CreateMetadata(t0) };
        var recentImporter = new FakeListingRecentDecksImporter
        {
            PageOneRows = new[] { new ArchidektListingDeck("K1", t0), new ArchidektListingDeck("K2", t0) }
        };
        var session = new ArchidektDeckCacheSession(repository, importer, recentImporter, idlePollDelay: TimeSpan.FromMilliseconds(1));

        await session.RunAsync(TimeSpan.FromSeconds(1));
        recentImporter.RequestedPages.Clear();
        recentImporter.PageOneRows = new[] { new ArchidektListingDeck("K1", t0.AddHours(1)), new ArchidektListingDeck("K2", t0.AddHours(1)) };

        // Why: D-05 and A7 require refreshes to drain without inflating the new-id counter.
        var result = await session.RunAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(0, result.DecksEnqueued);
        Assert.Equal(2, result.DecksDrained);
        Assert.Equal(2, result.DecksUnchanged);
        Assert.Equal(new[] { "K1", "K2", "K1", "K2" }, importer.ImportedDeckIds);
    }

    [Fact]
    public async Task RunAsync_NewIdWithNullUpdatedAt_IsInsertedAndCounted()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var importer = new FakeDeckImporter { Metadata = CreateMetadata(new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero)) };
        var recentImporter = new FakeListingRecentDecksImporter { PageOneRows = new[] { new ArchidektListingDeck("N", null) } };

        // Why: HARV-10's empty timestamp edge still discovers and counts novel deck IDs.
        var result = await new ArchidektDeckCacheSession(repository, importer, recentImporter, idlePollDelay: TimeSpan.FromMilliseconds(1)).RunAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, result.DecksEnqueued);
        Assert.Equal(1, result.DecksAdded);
        Assert.Equal(new[] { "N" }, importer.ImportedDeckIds);
    }

    [Fact]
    public async Task RunAsync_KnownDeckWithNullListingUpdatedAt_IsNotRefetched()
    {
        var t0 = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var importer = new FakeDeckImporter { Metadata = CreateMetadata(t0) };
        var recentImporter = new FakeListingRecentDecksImporter { PageOneRows = new[] { new ArchidektListingDeck("K", t0) } };
        var session = new ArchidektDeckCacheSession(repository, importer, recentImporter, idlePollDelay: TimeSpan.FromMilliseconds(1));

        await session.RunAsync(TimeSpan.FromSeconds(1));
        recentImporter.RequestedPages.Clear();
        recentImporter.PageOneRows = new[] { new ArchidektListingDeck("K", null) };

        // Why: a null HARV-10 listing timestamp cannot prove modification and must not requeue K.
        var result = await session.RunAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(new[] { "K" }, importer.ImportedDeckIds);
        Assert.Equal(0, result.DecksEnqueued);
        Assert.Equal(0, result.DecksDrained);
        Assert.True(recentImporter.RequestedPages.Count(page => page == 1) >= 2);
    }

    [Fact]
    public async Task RunAsync_LegacyRowWithNullStoredUpdatedAt_IsRefetchedExactlyOnce()
    {
        var t0 = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { "L" });
        await repository.MarkDeckProcessedAsync("L", commanderName: null);
        var importer = new FakeDeckImporter { Metadata = null };
        var recentImporter = new FakeListingRecentDecksImporter { PageOneRows = new[] { new ArchidektListingDeck("L", t0.AddHours(1)) } };

        // Why: D-03's seen baseline stops a legacy NULL/NULL row from requeueing forever.
        var result = await new ArchidektDeckCacheSession(repository, importer, recentImporter, idlePollDelay: TimeSpan.FromMilliseconds(1)).RunAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(new[] { "L" }, importer.ImportedDeckIds);
        Assert.Equal(0, result.DecksEnqueued);
        Assert.True(recentImporter.RequestedPages.Count(page => page == 1) >= 2);
        Assert.Null((await ReadDeckQueueRowAsync("L")).UpdatedUtc);
    }

    [Fact]
    public async Task RunAsync_ModifiedKnownDeckOnDeepPage_IsRefetchedAndCursorAdvances()
    {
        var t0 = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var importer = new FakeDeckImporter { Metadata = CreateMetadata(t0) };
        var recentImporter = new FakeListingRecentDecksImporter { PageOneRows = new[] { new ArchidektListingDeck("K", t0) } };
        var session = new ArchidektDeckCacheSession(repository, importer, recentImporter, idlePollDelay: TimeSpan.FromMilliseconds(1));

        await session.RunAsync(TimeSpan.FromSeconds(1));
        await repository.SetRecentDeckCrawlPageAsync(7);
        recentImporter.RequestedPages.Clear();
        recentImporter.PageOneRows = Array.Empty<ArchidektListingDeck>();
        recentImporter.DeepPageRows = new[] { new ArchidektListingDeck("K", t0.AddHours(1)) };

        // Why: A7 requires deep-page refreshes to drain, while rows advance the deep cursor.
        var result = await session.RunAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(new[] { "K", "K" }, importer.ImportedDeckIds);
        Assert.Equal(0, result.DecksEnqueued);
        Assert.Contains(7, recentImporter.RequestedPages);
        Assert.True(await repository.GetRecentDeckCrawlPageAsync() > 7);
    }

    [Fact]
    public async Task RunAsync_MetadataBearingImport_PersistsMetadata()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var metadata = new ArchidektDeckMetadata(3, 1, true, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-02T00:00:00Z"), DateTimeOffset.Parse("2026-01-03T00:00:00Z"));
        var importer = new FakeDeckImporter { Metadata = metadata };
        await repository.AddDeckIdsAsync(new[] { "metadata-deck" });

        await new ArchidektDeckCacheSession(repository, importer, new FakeRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(1)).RunAsync(TimeSpan.FromMilliseconds(30));

        var row = await ReadDeckQueueRowAsync("metadata-deck");
        Assert.Equal(1, importer.ImportCalls);
        Assert.Equal(metadata.EdhBracket, row.EdhBracket);
        Assert.Equal(metadata.DeckFormat, row.DeckFormat);
        Assert.Equal(metadata.Theorycrafted, row.Theorycrafted);
        Assert.Equal(metadata.CapturedUtc, row.CapturedUtc);
    }

    [Fact]
    public async Task RunAsync_UnexpectedDeckException_SkipsDeckAndContinues()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { "unexpected" });

        var result = await new ArchidektDeckCacheSession(
            repository,
            new UnexpectedFailureDeckImporter(),
            new FakeRecentDecksImporter(),
            idlePollDelay: TimeSpan.FromMilliseconds(1)).RunAsync(TimeSpan.FromSeconds(5), fetchBatchSize: 5);

        Assert.True(result.DecksSkipped >= 1);
    }

    [Fact]
    public async Task RunAsync_ThreeConsecutiveUnexpectedExceptions_ThrowsAndLeavesThirdDeckPending()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { "unexpected-1", "unexpected-2", "unexpected-3" });
        var session = new ArchidektDeckCacheSession(
            repository,
            new UnexpectedFailureDeckImporter(),
            new FakeRecentDecksImporter(),
            idlePollDelay: TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAsync<Exception>(() => session.RunAsync(TimeSpan.FromSeconds(1), fetchBatchSize: 3));

        Assert.True(await IsDeckSkippedAsync("unexpected-1"));
        Assert.True(await IsDeckSkippedAsync("unexpected-2"));
        Assert.False(await IsDeckSkippedAsync("unexpected-3"));
    }

    [Fact]
    public async Task RunAsync_NonConsecutiveUnexpectedExceptions_SkipsAllUnexpectedDecks()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { "unexpected-1", "success-1", "unexpected-2", "success-2", "unexpected-3" });

        var result = await new ArchidektDeckCacheSession(
            repository,
            new SelectiveFailureDeckImporter(),
            new FakeRecentDecksImporter(),
            idlePollDelay: TimeSpan.FromMilliseconds(1)).RunAsync(TimeSpan.FromSeconds(5), fetchBatchSize: 5);

        Assert.Equal(3, result.DecksSkipped);
        Assert.True(await IsDeckSkippedAsync("unexpected-1"));
        Assert.True(await IsDeckSkippedAsync("unexpected-2"));
        Assert.True(await IsDeckSkippedAsync("unexpected-3"));
        Assert.Equal(2, result.DecksProcessed);
    }

    [Fact]
    public async Task RunAsync_ExpectedSkipsDoNotResetUnexpectedFailureBreaker()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { "unexpected-1", "expected-skip-1", "unexpected-2", "expected-skip-2", "unexpected-3" });

        await Assert.ThrowsAsync<Exception>(() => new ArchidektDeckCacheSession(
            repository,
            new SelectiveFailureDeckImporter(),
            new FakeRecentDecksImporter(),
            idlePollDelay: TimeSpan.FromMilliseconds(1)).RunAsync(TimeSpan.FromSeconds(5), fetchBatchSize: 5));
    }

    [Fact]
    public async Task RunAsync_ImporterWithoutMetadataSupport_SkipsDeck()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { "metadata-unsupported-deck" });

        var result = await new ArchidektDeckCacheSession(
            repository,
            new ImportOnlyDeckImporter(),
            new FakeRecentDecksImporter(),
            idlePollDelay: TimeSpan.FromMilliseconds(1)).RunAsync(TimeSpan.FromMilliseconds(30));

        Assert.True(result.DecksSkipped >= 1);
    }

    [Fact]
    public async Task RunAsync_UnchangedCardList_RefreshesMetadataWithoutRewritingFacts()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var first = new ArchidektDeckMetadata(3, 1, true, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), null, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var second = new ArchidektDeckMetadata(4, 1, false, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-02T00:00:00Z"), DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        var importer = new FakeDeckImporter { Metadata = first };
        await repository.AddDeckIdsAsync(new[] { "unchanged-deck" });
        var session = new ArchidektDeckCacheSession(repository, importer, new FakeRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(1));
        await session.RunAsync(TimeSpan.FromMilliseconds(30));
        var before = await ReadDeckQueueRowAsync("unchanged-deck");
        await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("unchanged-deck", DateTimeOffset.Parse("2026-01-02T00:00:00Z")) });
        importer.Metadata = second;
        await session.RunAsync(TimeSpan.FromMilliseconds(30));
        var after = await ReadDeckQueueRowAsync("unchanged-deck");

        Assert.Equal(2, importer.ImportCalls);
        Assert.Equal(before.ContentHash, after.ContentHash);
        Assert.Equal(second.EdhBracket, after.EdhBracket);
        Assert.Equal(second.CapturedUtc, after.CapturedUtc);
    }

    private async Task<DeckQueueRow> ReadDeckQueueRowAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT content_hash, archidekt_edh_bracket, archidekt_deck_format, archidekt_theorycrafted, archidekt_created_utc, archidekt_updated_utc, archidekt_metadata_captured_utc FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new DeckQueueRow(reader.IsDBNull(0) ? null : reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetBoolean(3), reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4)), reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5)), reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)));
    }

    private async Task<bool> IsDeckSkippedAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT skipped FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    private async Task<bool> IsDeckProcessedAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT processed FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    private sealed record DeckQueueRow(string? ContentHash, int? EdhBracket, int? DeckFormat, bool? Theorycrafted, DateTimeOffset? CreatedUtc, DateTimeOffset? UpdatedUtc, DateTimeOffset? CapturedUtc);

    private static ArchidektDeckMetadata CreateMetadata(DateTimeOffset updatedUtc)
        => new(null, null, null, null, updatedUtc, updatedUtc);

    /// <summary>
    /// Invokes the handler synchronously on the calling thread (unlike <see cref="Progress{T}"/>,
    /// which posts asynchronously), so the test can react to each progress tick in-order and stop the
    /// run deterministically rather than on a wall clock.
    /// </summary>
    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler) => _handler = handler;

        public void Report(T value) => _handler(value);
    }

    private sealed class FakeDeckImporter : IArchidektDeckImporter
    {
        public int ImportCalls { get; private set; }

        public List<string> ImportedDeckIds { get; } = new();

        public ArchidektDeckMetadata? Metadata { get; set; }

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            ImportCalls++;
            ImportedDeckIds.Add(urlOrDeckId);
            return Task.FromResult(new List<DeckEntry>
            {
                new()
                {
                    Name = $"Card {urlOrDeckId}",
                    NormalizedName = CardNormalizer.Normalize($"Card {urlOrDeckId}"),
                    Quantity = 1,
                    Board = "mainboard",
                    Category = "Ramp"
                }
            });
        }

        public async Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => new(await ImportAsync(urlOrDeckId, cancellationToken), Metadata);
    }

    private sealed class FakeRecentDecksImporter : IArchidektRecentDecksImporter
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The bulk session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The bulk session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsPageAsync(int page, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The bulk session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<ArchidektListingDeck>> ImportRecentListingPageAsync(int page, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<ArchidektListingDeck>>(Array.Empty<ArchidektListingDeck>());
        }
    }

    /// <summary>
    /// Returns sticky listing rows while recording each bulk listing-page request.
    /// </summary>
    private sealed class FakeListingRecentDecksImporter : IArchidektRecentDecksImporter
    {
        public IReadOnlyList<ArchidektListingDeck> PageOneRows { get; set; } = Array.Empty<ArchidektListingDeck>();

        public IReadOnlyList<ArchidektListingDeck> DeepPageRows { get; set; } = Array.Empty<ArchidektListingDeck>();

        public List<int> RequestedPages { get; } = new();

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The bulk session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The bulk session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsPageAsync(int page, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The bulk session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<ArchidektListingDeck>> ImportRecentListingPageAsync(int page, CancellationToken cancellationToken = default)
        {
            RequestedPages.Add(page);
            return Task.FromResult(page == 1 ? PageOneRows : DeepPageRows);
        }
    }

    private sealed class ThrowingRateLimitedDeckImporter : IArchidektDeckImporter
    {
        private readonly List<string> _attemptedDeckIds = new();

        public IReadOnlyList<string> AttemptedDeckIds => _attemptedDeckIds;

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            _attemptedDeckIds.Add(urlOrDeckId);
            if (urlOrDeckId.Contains("rate-limited", StringComparison.Ordinal))
            {
                throw new ArchidektRateLimitedException("Simulated Archidekt rate limit.", TimeSpan.FromSeconds(120));
            }

            return Task.FromResult(new List<DeckEntry>
            {
                new()
                {
                    Name = $"Card {urlOrDeckId}",
                    NormalizedName = CardNormalizer.Normalize($"Card {urlOrDeckId}"),
                    Quantity = 1,
                    Board = "mainboard",
                    Category = "Ramp"
                }
            });
        }

        public async Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => new(await ImportAsync(urlOrDeckId, cancellationToken), null);
    }

    private sealed class ThrowingTransientDeckImporter : IArchidektDeckImporter
    {
        private readonly List<string> _attemptedDeckIds = new();

        public IReadOnlyList<string> AttemptedDeckIds => _attemptedDeckIds;

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            _attemptedDeckIds.Add(urlOrDeckId);
            throw new ArchidektTransientFailureException("Simulated transient Archidekt failure.");
        }

        public Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            _attemptedDeckIds.Add(urlOrDeckId);
            throw new ArchidektTransientFailureException("Simulated transient Archidekt failure.");
        }
    }

    private sealed class RecordingRecentDecksImporter : IArchidektRecentDecksImporter
    {
        public List<int> RequestedPages { get; } = new();

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The bulk session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The bulk session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsPageAsync(int page, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The bulk session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<ArchidektListingDeck>> ImportRecentListingPageAsync(int page, CancellationToken cancellationToken = default)
        {
            RequestedPages.Add(page);
            return Task.FromResult<IReadOnlyList<ArchidektListingDeck>>(page > 1 ? new[] { new ArchidektListingDeck("discovered-deck", null) } : Array.Empty<ArchidektListingDeck>());
        }
    }

    private sealed class ImportOnlyDeckImporter : IArchidektDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DeckEntry>());
    }

    private sealed class UnexpectedFailureDeckImporter : IArchidektDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => throw new Exception("Simulated unexpected deck failure.");

        public Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => throw new Exception("Simulated unexpected deck failure.");
    }

    private sealed class SelectiveFailureDeckImporter : IArchidektDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => urlOrDeckId.StartsWith("unexpected", StringComparison.Ordinal)
                ? throw new Exception("unexpected")
                : urlOrDeckId.StartsWith("expected-skip", StringComparison.Ordinal)
                    ? throw new HttpRequestException("expected skip")
                    : Task.FromResult(new List<DeckEntry>());

        public async Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => new(await ImportAsync(urlOrDeckId, cancellationToken), null);
    }

    /// <summary>
    /// Exception-injection double that throws <see cref="HttpRequestException"/> on every import call;
    /// used to test that <see cref="ArchidektDeckCacheSession"/> handles Archidekt fetch failures gracefully.
    /// </summary>
    private sealed class ThrowingRecentDecksImporter : IArchidektRecentDecksImporter
    {
        private readonly Func<Exception> _exceptionFactory;
        private int _calls;

        // Why: keeping one fake keeps the implementer census that 06-05 relies on.
        public ThrowingRecentDecksImporter(Func<Exception>? exceptionFactory = null)
        {
            _exceptionFactory = exceptionFactory ?? (() => new HttpRequestException("Simulated Archidekt recent deck failure."));
        }

        public int Calls => _calls;

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The bulk session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The bulk session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsPageAsync(int page, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The bulk session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<ArchidektListingDeck>> ImportRecentListingPageAsync(int page, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            throw _exceptionFactory();
        }

        private Task<IReadOnlyList<string>> Throw()
        {
            Interlocked.Increment(ref _calls);
            throw _exceptionFactory();
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // ignored
        }
    }
}
