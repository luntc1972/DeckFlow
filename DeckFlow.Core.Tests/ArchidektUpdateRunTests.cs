using System.Net;
using System.Diagnostics;
using System.Text.Json;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;
using RestSharp;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Traces refresh-only Archidekt updates through the real listing importer and SQLite queue.
/// </summary>
[Collection(ArchidektThrottleCollection.Name)]
public sealed class ArchidektUpdateRunTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public ArchidektUpdateRunTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
        ArchidektThrottle.ResetForTests();
        ArchidektThrottle.ConfigureForTests(static () => DateTimeOffset.UtcNow, static (_, _) => Task.CompletedTask);
    }

    [Fact]
    public async Task UpdateRun_ListingShowsKnownDeckModified_DrainsOnlyRefreshAndLeavesNewIdsForBulk()
    {
        // Why: D-04, D-05, and HARV-11 require this tracer to make no live request.
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddListingRowsAsync([new ArchidektListingDeck("101", DateTimeOffset.Parse("2026-01-01T00:00:00Z"))]);
        await repository.MarkDeckProcessedAsync("101", commanderName: null, metadata: new ArchidektDeckMetadata(null, null, null, null, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.UtcNow));
        await repository.AddDeckIdsAsync(["pending-1"]);
        await repository.SetRecentDeckCrawlPageAsync(7);

        var handler = new UpdateListingFixtureHandler();
        var client = new RestClient(new RestClientOptions { BaseUrl = new Uri("https://archidekt.com"), UserAgent = null, ConfigureMessageHandler = _ => handler });
        var deckImporter = new ScriptedDeckImporter();
        var progressValues = new List<int>();
        var session = new ArchidektDeckCacheSession(repository, deckImporter, new ArchidektRecentDecksImporter(client), idlePollDelay: TimeSpan.FromMilliseconds(1));

        var result = await session.RunUpdateAsync(TimeSpan.FromSeconds(30), progress: new SynchronousProgress<int>(progressValues.Add));

        Assert.Equal(["101"], deckImporter.ImportedDeckIds);
        Assert.Equal(10, result.PagesPolled);
        Assert.Equal(1, result.RefreshesRequeued);
        Assert.Equal(1, result.RefreshesDrained);
        Assert.Equal(2, result.NewIdsSeen);
        Assert.Equal(0, result.DecksSkipped);
        Assert.True(result.Duration < TimeSpan.FromSeconds(30));
        Assert.Equal(Enumerable.Range(1, 10), handler.RequestPages);
        Assert.All(handler.RequestPaths, path => Assert.StartsWith("/api/decks/v3/?orderBy=-updatedAt&page=", path, StringComparison.Ordinal));
        Assert.Equal(7, await repository.GetRecentDeckCrawlPageAsync());
        Assert.True(await IsProcessedAsync("101"));
        Assert.Null(await GetRefreshRequestedUtcAsync("101"));
        Assert.Empty(await repository.GetNextRefreshDeckIdsAsync(10));
        Assert.Equal(["pending-1", "303", "505"], await repository.GetNextUnprocessedDeckIdsAsync(10));
        Assert.Equal(1, progressValues.Last());
    }

    [Fact]
    public async Task RunUpdateAsync_ListingPageFailsWithHttpError_ContinuesWithNextPage()
    {
        // Why: D-10 continues the cheap poll instead of retrying a transient listing failure.
        var listingImporter = new ScriptedListingImporter(page => [new ArchidektListingDeck($"n-{page}", DateTimeOffset.Parse("2026-01-03T00:00:00Z"))], page => page == 2 ? new HttpRequestException("fixture failure", null, HttpStatusCode.InternalServerError) : null);
        var session = new ArchidektDeckCacheSession(new CategoryKnowledgeRepository(_databasePath), new ScriptedDeckImporter(), listingImporter);

        var result = await session.RunUpdateAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(Enumerable.Range(1, 10), listingImporter.RequestedPages);
        Assert.Equal(9, result.PagesPolled);
        Assert.Equal(9, result.NewIdsSeen);
    }

    [Fact]
    public async Task RunUpdateAsync_ListingPageFailsWithJsonError_ContinuesWithNextPage()
    {
        // Why: D-10 continues polling when the listing endpoint returns a non-JSON success body.
        var listingImporter = new ScriptedListingImporter(page => [new ArchidektListingDeck($"n-{page}", DateTimeOffset.Parse("2026-01-03T00:00:00Z"))], page => page == 2 ? new JsonException("fixture JSON failure") : null);
        var session = new ArchidektDeckCacheSession(new CategoryKnowledgeRepository(_databasePath), new ScriptedDeckImporter(), listingImporter);

        var result = await session.RunUpdateAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(Enumerable.Range(1, 10), listingImporter.RequestedPages);
        Assert.Equal(9, result.PagesPolled);
        Assert.Equal(9, result.NewIdsSeen);
    }

    [Fact]
    public async Task RunUpdateAsync_EmptyListingPage_StopsPollingFurtherPages()
    {
        // Why: D-04 ends the refresh-only listing poll at the first empty page.
        var listingImporter = new ScriptedListingImporter(page => page == 2 ? [] : [new ArchidektListingDeck($"n-{page}", DateTimeOffset.Parse("2026-01-03T00:00:00Z"))]);
        var session = new ArchidektDeckCacheSession(new CategoryKnowledgeRepository(_databasePath), new ScriptedDeckImporter(), listingImporter);

        var result = await session.RunUpdateAsync(TimeSpan.FromSeconds(5));

        Assert.Equal([1, 2], listingImporter.RequestedPages);
        Assert.Equal(2, result.PagesPolled);
        Assert.Equal(1, result.NewIdsSeen);
    }

    [Fact]
    public async Task RunUpdateAsync_RateLimitedDuringListingPoll_RethrowsAndPollsNoFurtherPage()
    {
        // Why: the 06-03 carry-forward means a limiter trip ends the session and never silently skips.
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedRefreshDeckAsync(repository, "refresh-1");
        var listingImporter = new ScriptedListingImporter(page => [new ArchidektListingDeck("n-1", DateTimeOffset.Parse("2026-01-03T00:00:00Z"))], page => page == 2 ? new ArchidektRateLimitedException("fixture limit", TimeSpan.FromSeconds(1)) : null);
        var deckImporter = new ScriptedDeckImporter();
        var session = new ArchidektDeckCacheSession(repository, deckImporter, listingImporter);

        var result = await session.RunUpdateAsync(TimeSpan.FromSeconds(5));

        Assert.Equal([1, 2], listingImporter.RequestedPages);
        Assert.True(result.RateLimited);
        Assert.Empty(deckImporter.AttemptedDeckIds);
        Assert.NotNull((await ReadQueueRowAsync("refresh-1")).RefreshRequestedUtc);
    }

    [Fact]
    public async Task RunUpdateAsync_RateLimitedDuringRefreshDrain_RethrowsAndLeavesDeckQueuedForRefresh()
    {
        // Why: the 06-03 carry-forward preserves a refresh row after a limiter trip.
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedRefreshDeckAsync(repository, "rate-limited-1");
        var deckImporter = new ScriptedDeckImporter();
        var session = new ArchidektDeckCacheSession(repository, deckImporter, new ScriptedListingImporter(_ => []));

        var result = await session.RunUpdateAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["rate-limited-1"], deckImporter.AttemptedDeckIds);
        Assert.True(result.RateLimited);
        var row = await ReadQueueRowAsync("rate-limited-1");
        Assert.False(row.Processed);
        Assert.NotNull(row.RefreshRequestedUtc);
        Assert.Equal(["rate-limited-1"], await repository.GetNextRefreshDeckIdsAsync(10));
    }

    [Fact]
    public async Task RunUpdateAsync_OnlyAttemptedTransientDeckRemains_EndsBeforeDurationCap()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedRefreshDeckAsync(repository, "transient-1");
        var deckImporter = new ScriptedDeckImporter();
        var listingImporter = new ScriptedListingImporter(_ => []);
        var session = new ArchidektDeckCacheSession(repository, deckImporter, listingImporter);

        var stopwatch = Stopwatch.StartNew();
        await session.RunUpdateAsync(TimeSpan.FromSeconds(5), fetchBatchSize: 1);
        stopwatch.Stop();

        Assert.Equal(["transient-1"], deckImporter.AttemptedDeckIds);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.InRange(listingImporter.RequestedPages.Count, 1, 3);
    }

    [Fact]
    public async Task RunUpdateAsync_TransientDeckAheadOfRefreshDeck_DrainsRefreshDeckAndEndsBeforeDurationCap()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedRefreshDeckAsync(repository, "a-transient-1");
        await SeedRefreshDeckAsync(repository, "b-refresh-1");
        Assert.Equal(["a-transient-1", "b-refresh-1"], await repository.GetNextRefreshDeckIdsAsync(2));
        var deckImporter = new ScriptedDeckImporter();
        var session = new ArchidektDeckCacheSession(repository, deckImporter, new ScriptedListingImporter(_ => []));

        var stopwatch = Stopwatch.StartNew();
        var result = await session.RunUpdateAsync(TimeSpan.FromSeconds(5), fetchBatchSize: 1);
        stopwatch.Stop();

        Assert.Equal(["a-transient-1", "b-refresh-1"], deckImporter.AttemptedDeckIds);
        var refreshed = await ReadQueueRowAsync("b-refresh-1");
        Assert.True(refreshed.Processed);
        Assert.False(refreshed.Skipped);
        Assert.Null(refreshed.RefreshRequestedUtc);
        var transient = await ReadQueueRowAsync("a-transient-1");
        Assert.False(transient.Processed);
        Assert.False(transient.Skipped);
        Assert.NotNull(transient.RefreshRequestedUtc);
        Assert.Equal(1, result.RefreshesDrained);
        Assert.Equal(0, result.DecksSkipped);
        Assert.False(result.EndedEarly);
        Assert.False(result.RateLimited);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RunUpdateAsync_RefreshDeckImportFails_IsSkippedCountedAndLeavesRefreshQueue()
    {
        // Why: D-04 drains refresh-only rows and the 06-03 carry-forward counts a failed import as skipped.
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedProcessedDeckAsync(repository, "broken-1", DateTimeOffset.Parse("2026-01-03T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "ok-1", DateTimeOffset.Parse("2026-01-03T00:00:00Z"));
        var listingImporter = new ScriptedListingImporter(page => page == 1 ? [new ArchidektListingDeck("broken-1", DateTimeOffset.Parse("2026-01-03T05:00:00Z")), new ArchidektListingDeck("ok-1", DateTimeOffset.Parse("2026-01-03T05:00:00Z"))] : []);
        var session = new ArchidektDeckCacheSession(repository, new ScriptedDeckImporter(), listingImporter);

        var result = await session.RunUpdateAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, result.RefreshesRequeued);
        Assert.Equal(2, result.RefreshesDrained);
        Assert.Equal(1, result.DecksSkipped);
        var broken = await ReadQueueRowAsync("broken-1");
        Assert.True(broken.Skipped);
        Assert.Null(broken.RefreshRequestedUtc);
        var ok = await ReadQueueRowAsync("ok-1");
        Assert.True(ok.Processed);
        Assert.Null(ok.RefreshRequestedUtc);
        Assert.Empty(await repository.GetNextRefreshDeckIdsAsync(10));
    }

    [Fact]
    public async Task RunUpdateAsync_RefreshRowQueuedBeforeRun_IsDrainedWhilePendingNewRowsAreLeftForBulk()
    {
        // Why: D-04 drains only refresh rows and leaves new rows pending for bulk.
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedRefreshDeckAsync(repository, "refresh-1");
        await repository.AddDeckIdsAsync(["pending-1"]);
        var deckImporter = new ScriptedDeckImporter();
        var session = new ArchidektDeckCacheSession(repository, deckImporter, new ScriptedListingImporter(_ => []));

        var result = await session.RunUpdateAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["refresh-1"], deckImporter.AttemptedDeckIds);
        Assert.Equal(0, result.RefreshesRequeued);
        Assert.Equal(1, result.RefreshesDrained);
        Assert.Equal(["pending-1"], await repository.GetNextUnprocessedDeckIdsAsync(10));
    }

    [Fact]
    public async Task RunUpdateAsync_ZeroDuration_PollsNothingAndReturnsZeroCounters()
    {
        // Why: D-04 requires a zero-duration refresh-only run to do no work.
        var listingImporter = new ScriptedListingImporter(_ => [new ArchidektListingDeck("n-1", DateTimeOffset.Parse("2026-01-03T00:00:00Z"))]);
        var deckImporter = new ScriptedDeckImporter();
        var session = new ArchidektDeckCacheSession(new CategoryKnowledgeRepository(_databasePath), deckImporter, listingImporter);

        var result = await session.RunUpdateAsync(TimeSpan.Zero);

        Assert.Empty(listingImporter.RequestedPages);
        Assert.Empty(deckImporter.AttemptedDeckIds);
        Assert.Equal(0, result.PagesPolled);
        Assert.Equal(0, result.RefreshesRequeued);
        Assert.Equal(0, result.RefreshesDrained);
        Assert.Equal(0, result.NewIdsSeen);
        Assert.Equal(0, result.DecksSkipped);
    }

    [Fact]
    public async Task RunUpdateAsync_ListingUpsertFails_PageNotPolledAndPollingContinues()
    {
        // Why: D-10 continues the cheap poll when a listing upsert fails, without retrying it.
        var listingImporter = new ScriptedListingImporter(page => page == 2 ? new ThrowingListingRows() : [new ArchidektListingDeck($"n-{page}", DateTimeOffset.Parse("2026-01-03T00:00:00Z"))]);
        var session = new ArchidektDeckCacheSession(new CategoryKnowledgeRepository(_databasePath), new ScriptedDeckImporter(), listingImporter);

        var result = await session.RunUpdateAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(Enumerable.Range(1, 10), listingImporter.RequestedPages);
        Assert.Equal(9, result.PagesPolled);
        Assert.Equal(9, result.NewIdsSeen);
    }

    public void Dispose()
    {
        ArchidektThrottle.ResetForTests();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, recursive: true);
    }

    private async Task<bool> IsProcessedAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT processed FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    private async Task<string?> GetRefreshRequestedUtcAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT refresh_requested_utc FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        return await command.ExecuteScalarAsync() as string;
    }

    private async Task SeedRefreshDeckAsync(CategoryKnowledgeRepository repository, string deckId)
    {
        await SeedProcessedDeckAsync(repository, deckId, DateTimeOffset.Parse("2026-01-03T00:00:00Z"));
        await repository.AddListingRowsAsync([new ArchidektListingDeck(deckId, DateTimeOffset.Parse("2026-01-03T05:00:00Z"))]);
    }

    private static async Task SeedProcessedDeckAsync(CategoryKnowledgeRepository repository, string deckId, DateTimeOffset updatedUtc)
    {
        await repository.AddDeckIdsAsync([deckId]);
        await repository.MarkDeckProcessedAsync(deckId, commanderName: null, metadata: new ArchidektDeckMetadata(null, null, null, null, updatedUtc, DateTimeOffset.UtcNow));
    }

    private async Task<(bool Processed, bool Skipped, string? RefreshRequestedUtc)> ReadQueueRowAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT processed, skipped, refresh_requested_utc FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetBoolean(0), reader.GetBoolean(1), reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private sealed class UpdateListingFixtureHandler : HttpMessageHandler
    {
        public List<string> RequestPaths { get; } = [];
        public List<int> RequestPages { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            RequestPaths.Add(path);
            var page = int.Parse(request.RequestUri.Query.Split("page=")[1], System.Globalization.CultureInfo.InvariantCulture);
            RequestPages.Add(page);
            var body = "{\"results\":[{\"id\":101,\"updatedAt\":\"2026-01-02T00:00:00.123456Z\"},{\"id\":303,\"updatedAt\":\"2026-01-02T00:00:00.123456Z\"},{\"id\":505,\"updatedAt\":\"2026-01-02T00:00:00.123456Z\"}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler) => _handler = handler;

        public void Report(T value) => _handler(value);
    }

    private sealed class ScriptedListingImporter : IArchidektRecentDecksImporter
    {
        private readonly Func<int, IReadOnlyList<ArchidektListingDeck>> _rowsForPage;
        private readonly Func<int, Exception?> _failureForPage;

        public ScriptedListingImporter(Func<int, IReadOnlyList<ArchidektListingDeck>> rowsForPage, Func<int, Exception?>? failureForPage = null)
        {
            _rowsForPage = rowsForPage;
            _failureForPage = failureForPage ?? (_ => null);
        }

        public List<int> RequestedPages { get; } = [];

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The update session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The update session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsPageAsync(int page, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The update session must call ImportRecentListingPageAsync.");

        public Task<IReadOnlyList<ArchidektListingDeck>> ImportRecentListingPageAsync(int page, CancellationToken cancellationToken = default)
        {
            RequestedPages.Add(page);
            var exception = _failureForPage(page);
            return exception is null ? Task.FromResult(_rowsForPage(page)) : Task.FromException<IReadOnlyList<ArchidektListingDeck>>(exception);
        }
    }

    private sealed class ThrowingListingRows : IReadOnlyList<ArchidektListingDeck>
    {
        public int Count => 1;

        // Why: the fetch succeeds, so the fixture surfaces the upsert-side failure in AddListingRowsAsync.
        public ArchidektListingDeck this[int index] => throw new SqliteException("fixture upsert failure", 1);

        public IEnumerator<ArchidektListingDeck> GetEnumerator() => throw new SqliteException("fixture upsert failure", 1);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ScriptedDeckImporter : IArchidektDeckImporter
    {
        public List<string> ImportedDeckIds { get; } = [];

        public IReadOnlyList<string> AttemptedDeckIds => ImportedDeckIds;

        public Task<List<DeckEntry>> ImportAsync(string deckId, CancellationToken cancellationToken = default)
        {
            ImportedDeckIds.Add(deckId);
            if (deckId.Contains("rate-limited", StringComparison.Ordinal))
            {
                throw new ArchidektRateLimitedException("fixture limit", TimeSpan.FromSeconds(1));
            }

            if (deckId.Contains("transient", StringComparison.Ordinal))
            {
                throw new ArchidektTransientFailureException("fixture transient failure");
            }

            if (deckId.Contains("broken", StringComparison.Ordinal))
            {
                throw new HttpRequestException("fixture failure");
            }

            return Task.FromResult(new List<DeckEntry> { new() { Name = $"Card {deckId}", NormalizedName = CardNormalizer.Normalize($"Card {deckId}"), Quantity = 1, Board = "mainboard", Category = "Ramp" } });
        }

        public async Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string deckId, CancellationToken cancellationToken = default)
            => new(await ImportAsync(deckId, cancellationToken), new ArchidektDeckMetadata(null, null, null, null, DateTimeOffset.Parse("2026-01-02T00:00:00Z"), DateTimeOffset.UtcNow));
    }
}
