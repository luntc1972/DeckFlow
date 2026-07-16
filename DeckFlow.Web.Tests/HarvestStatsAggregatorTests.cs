using DeckFlow.Core.Reporting;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="HarvestStatsAggregator"/> covering cold-path query fan-out behavior.
/// </summary>
public sealed class HarvestStatsAggregatorTests
{
    [Fact]
    public async Task GetAsync_StartsIndependentQueriesBeforeAwaitingResults()
    {
        var categoryStore = new BlockingCategoryKnowledgeStore();
        var runStore = new BlockingHarvestRunStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var aggregator = CreateAggregator(runStore, categoryStore, cache);

        var statsTask = aggregator.GetAsync();
        await Task.Delay(150);
        var startedBeforeRelease = categoryStore.StartedCalls + runStore.StartedCalls;

        categoryStore.Release();
        runStore.Release();
        var payload = await statsTask;

        Assert.Equal(6, startedBeforeRelease);
        Assert.Equal(42, payload.TotalDecks);
        Assert.Equal(7, payload.TotalDecks30d);
        Assert.Equal(99, payload.TotalObservations);
        Assert.Equal(4096L, payload.PostgresStorageBytes);
        Assert.Equal(runStore.LastSuccessUtc, payload.LastSuccessUtc);
        Assert.Equal(runStore.LastSuccessUtc + TimeSpan.FromHours(4), payload.NextScheduledUtc);
    }

    [Fact]
    public async Task GetAsync_DoesNotCallTopCommandersForStatsPayload()
    {
        var categoryStore = new ImmediateCategoryKnowledgeStore();
        var runStore = new ImmediateHarvestRunStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var aggregator = CreateAggregator(runStore, categoryStore, cache);

        var payload = await aggregator.GetAsync();

        Assert.Equal(0, categoryStore.TopCommandersCalls);
        Assert.Equal(42, payload.TotalDecks);
        Assert.Empty(payload.RecentRuns);
    }

    private static HarvestStatsAggregator CreateAggregator(
        IHarvestRunStore runStore,
        ICategoryKnowledgeStore categoryStore,
        IMemoryCache cache)
        => new(
            runStore,
            new FakeHarvestScheduleCache(),
            categoryStore,
            cache,
            NullLogger<HarvestStatsAggregator>.Instance);

    private sealed class BlockingCategoryKnowledgeStore : ICategoryKnowledgeStore
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _startedCalls;

        public int StartedCalls => Volatile.Read(ref _startedCalls);

        public void Release() => _release.TrySetResult();

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());

        public Task<IReadOnlyList<CategoryDeckMembership>> GetCategoryDeckMembershipForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryDeckMembership>>(Array.Empty<CategoryDeckMembership>());

        public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
            => Task.FromResult(0);

        public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyDictionary<string, int>> GetCategoryDeckCountsAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>(StringComparer.Ordinal));

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetCategoriesForNamesAsync(IReadOnlyCollection<string> cardNames, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(new Dictionary<string, IReadOnlyList<string>>());

        public Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => BlockAsync(42);

        public Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
            => BlockAsync(7);

        public Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default)
            => BlockAsync(99);

        public Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopCommanderRow>>(Array.Empty<TopCommanderRow>());

        public Task<IReadOnlyList<HarvestedCommanderRow>> GetPagedProcessedCommandersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestedCommanderRow>>(Array.Empty<HarvestedCommanderRow>());

        public Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default)
            => BlockAsync<long?>(4096L);

        public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult(CardDeckTotals.Empty);

        private async Task<T> BlockAsync<T>(T value)
        {
            Interlocked.Increment(ref _startedCalls);
            await _release.Task;
            return value;
        }
    }

    private sealed class ImmediateCategoryKnowledgeStore : ICategoryKnowledgeStore
    {
        public int TopCommandersCalls { get; private set; }

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());

        public Task<IReadOnlyList<CategoryDeckMembership>> GetCategoryDeckMembershipForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryDeckMembership>>(Array.Empty<CategoryDeckMembership>());

        public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
            => Task.FromResult(0);

        public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyDictionary<string, int>> GetCategoryDeckCountsAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>(StringComparer.Ordinal));

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetCategoriesForNamesAsync(IReadOnlyCollection<string> cardNames, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(new Dictionary<string, IReadOnlyList<string>>());

        public Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(42);

        public Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(7);

        public Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(99);

        public Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default)
        {
            TopCommandersCalls++;
            return Task.FromResult<IReadOnlyList<TopCommanderRow>>(Array.Empty<TopCommanderRow>());
        }

        public Task<IReadOnlyList<HarvestedCommanderRow>> GetPagedProcessedCommandersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestedCommanderRow>>(Array.Empty<HarvestedCommanderRow>());

        public Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(4096L);

        public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult(CardDeckTotals.Empty);
    }

    private sealed class BlockingHarvestRunStore : IHarvestRunStore
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _startedCalls;

        public DateTimeOffset LastSuccessUtc { get; } = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        public int StartedCalls => Volatile.Read(ref _startedCalls);

        public void Release() => _release.TrySetResult();

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Guid> InsertQueuedAsync(HarvestRunKind kind, int durationSeconds, string? url, DateTimeOffset now, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task UpdateStateAsync(Guid id, HarvestRunState state, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc, int decksProcessed, int additionalDecksFound, string? errorMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateProgressAsync(Guid id, int decksProcessed, int additionalDecksFound, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<HarvestRunRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default)
            => BlockAsync<IReadOnlyList<HarvestRunRow>>(Array.Empty<HarvestRunRow>());

        public Task<string> GetRecentRevisionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("0");

        public Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken cancellationToken = default)
            => BlockAsync<DateTimeOffset?>(LastSuccessUtc);

        public Task<long> GetTotalSucceededCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0L);

        private async Task<T> BlockAsync<T>(T value)
        {
            Interlocked.Increment(ref _startedCalls);
            await _release.Task;
            return value;
        }
    }

    private sealed class ImmediateHarvestRunStore : IHarvestRunStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Guid> InsertQueuedAsync(HarvestRunKind kind, int durationSeconds, string? url, DateTimeOffset now, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task UpdateStateAsync(Guid id, HarvestRunState state, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc, int decksProcessed, int additionalDecksFound, string? errorMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateProgressAsync(Guid id, int decksProcessed, int additionalDecksFound, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<HarvestRunRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestRunRow>>(Array.Empty<HarvestRunRow>());

        public Task<string> GetRecentRevisionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("0");

        public Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        public Task<long> GetTotalSucceededCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0L);
    }

    private sealed class FakeHarvestScheduleCache : IHarvestScheduleCache
    {
        public HarvestScheduleSnapshot Snapshot()
            => new(4, Paused: false, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        public Task ReloadAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
