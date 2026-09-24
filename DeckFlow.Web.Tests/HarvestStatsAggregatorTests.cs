using DeckFlow.Core.Reporting;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

        Assert.Equal(9, startedBeforeRelease);
        Assert.Equal(42, payload.TotalDecks);
        Assert.Equal(7, payload.TotalDecks30d);
        Assert.Equal(99, payload.TotalObservations);
        Assert.Equal(4096L, payload.DatabaseSizeBytes);
        Assert.Equal(runStore.LastSuccessUtc, payload.LastSuccessUtc);
        Assert.Equal(runStore.LastSuccessUtc + TimeSpan.FromHours(4), payload.NextScheduledUtc);
    }

    [Fact]
    public async Task GetAsync_TransfersQueuedDeckCountFromStore()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var runStore = new BlockingHarvestRunStore();
        var aggregator = CreateAggregator(runStore, new ImmediateCategoryKnowledgeStore(), cache);

        var payloadTask = aggregator.GetAsync();
        runStore.Release();
        var payload = await payloadTask;

        Assert.Equal(12, payload.QueuedDeckCount);
    }

    [Fact]
    public async Task GetAsync_QueuedDeckCountAboveFloor_FlagsBacklogAboveFloor()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var runStore = new BlockingHarvestRunStore();
        var categoryStore = new ImmediateCategoryKnowledgeStore(101);
        var aggregator = CreateAggregator(runStore, categoryStore, cache);

        var payloadTask = aggregator.GetAsync();
        runStore.Release();
        var payload = await payloadTask;

        Assert.True(payload.Health.BacklogFlagged);
        Assert.Equal(HarvestBacklogReason.AboveFloor, payload.Health.BacklogReason);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(99, false)]
    [InlineData(101, true)]
    public async Task GetAsync_QueuedDeckCountAtOrAroundFloor_UsesStrictlyGreaterThan(int queuedDeckCount, bool expectedFlagged)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var aggregator = CreateAggregator(new ImmediateHarvestRunStore(), new ImmediateCategoryKnowledgeStore(queuedDeckCount), cache);

        var payload = await aggregator.GetAsync();

        Assert.Equal(expectedFlagged, payload.Health.BacklogFlagged);
    }

    [Fact]
    public async Task GetAsync_ThreeGrowingRuns_FlagsGrowingAndPassesThresholdsThrough()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var runs = new[] { HealthRun(3, 2), HealthRun(2, 1), HealthRun(1, 0) };
        var aggregator = CreateAggregator(new ImmediateHarvestRunStore(runs), new ImmediateCategoryKnowledgeStore(99), cache);

        var payload = await aggregator.GetAsync();

        Assert.Equal(HarvestBacklogReason.Growing, payload.Health.BacklogReason);
        Assert.Equal(100, payload.Health.BacklogFloor);
        Assert.Equal(3, payload.Health.BacklogGrowthRunCount);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(1, 2)]
    public async Task GetAsync_NonGrowingRun_PreventsGrowingFlag(int enqueued, int drained)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var runs = new[] { HealthRun(3, 2), HealthRun(2, 1), HealthRun(enqueued, drained) };
        var aggregator = CreateAggregator(new ImmediateHarvestRunStore(runs), new ImmediateCategoryKnowledgeStore(99), cache);

        var payload = await aggregator.GetAsync();

        Assert.Equal(HarvestBacklogReason.None, payload.Health.BacklogReason);
    }

    [Fact]
    public async Task GetAsync_AboveFloorAndGrowingRuns_UsesCombinedReason()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var runs = new[] { HealthRun(3, 2), HealthRun(2, 1), HealthRun(1, 0) };
        var aggregator = CreateAggregator(new ImmediateHarvestRunStore(runs), new ImmediateCategoryKnowledgeStore(101), cache);

        var payload = await aggregator.GetAsync();

        Assert.Equal(HarvestBacklogReason.AboveFloorAndGrowing, payload.Health.BacklogReason);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(10, false)]
    [InlineData(11, true)]
    public async Task GetAsync_ZeroDiscoveryRuns_DerivesExactOrCappedStreak(int count, bool expectedCapped)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var runs = Enumerable.Range(0, count).Select(_ => HealthRun(0, 1)).ToArray();
        var aggregator = CreateAggregator(new ImmediateHarvestRunStore(runs), new ImmediateCategoryKnowledgeStore(), cache);

        var payload = await aggregator.GetAsync();

        Assert.Equal(Math.Min(count, HarvestHealthOptions.RecentRunsWindow), payload.Health.ZeroDiscoveryStreak);
        Assert.Equal(expectedCapped, payload.Health.ZeroDiscoveryStreakCapped);
    }

    [Fact]
    public async Task GetAsync_FreshCachedPayload_ReturnsWithoutRebuilding()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var categoryStore = new ImmediateCategoryKnowledgeStore();
        var aggregator = CreateAggregator(new ImmediateHarvestRunStore(), categoryStore, cache, new TestTimeProvider());

        await aggregator.GetAsync();
        var payload = await aggregator.GetAsync();

        Assert.Equal(1, categoryStore.BuildCount);
        Assert.Equal(42, payload.TotalDecks);
    }

    [Fact]
    public async Task GetAsync_StaleCachedPayload_ReturnsImmediatelyAndStartsOneRebuild()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var categoryStore = new ImmediateCategoryKnowledgeStore();
        var timeProvider = new TestTimeProvider();
        var aggregator = CreateAggregator(new ImmediateHarvestRunStore(), categoryStore, cache, timeProvider);
        var cached = await aggregator.GetAsync();
        categoryStore.BlockNextBuild();
        timeProvider.Advance(TimeSpan.FromSeconds(61));

        var payloads = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => aggregator.GetAsync()));
        await categoryStore.WaitForBlockedBuildAsync();

        Assert.All(payloads, payload => Assert.Equal(cached, payload));
        Assert.Equal(2, categoryStore.BuildCount);
        categoryStore.ReleaseBlockedBuild();
    }

    [Fact]
    public async Task GetAsync_ConcurrentColdCallers_ShareOneBuild()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var categoryStore = new ImmediateCategoryKnowledgeStore();
        categoryStore.BlockNextBuild();
        var aggregator = CreateAggregator(new ImmediateHarvestRunStore(), categoryStore, cache, new TestTimeProvider());

        var payloadTasks = Enumerable.Range(0, 5).Select(_ => aggregator.GetAsync()).ToArray();
        await categoryStore.WaitForBlockedBuildAsync();

        Assert.Equal(1, categoryStore.BuildCount);
        categoryStore.ReleaseBlockedBuild();
        Assert.All(await Task.WhenAll(payloadTasks), payload => Assert.Equal(42, payload.TotalDecks));
    }

    [Fact]
    public async Task Invalidate_KeepsCachedPayloadAndStartsRefresh()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var categoryStore = new ImmediateCategoryKnowledgeStore();
        var aggregator = CreateAggregator(new ImmediateHarvestRunStore(), categoryStore, cache, new TestTimeProvider());
        var cached = await aggregator.GetAsync();
        categoryStore.BlockNextBuild();

        aggregator.Invalidate();
        var payload = await aggregator.GetAsync();
        await categoryStore.WaitForBlockedBuildAsync();

        Assert.Equal(cached, payload);
        Assert.Equal(2, categoryStore.BuildCount);
        categoryStore.ReleaseBlockedBuild();
    }

    [Fact]
    public async Task GetAsync_FailedBackgroundRebuild_KeepsLastGoodPayload()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var categoryStore = new ImmediateCategoryKnowledgeStore();
        var aggregator = CreateAggregator(new ImmediateHarvestRunStore(), categoryStore, cache, new TestTimeProvider());
        var cached = await aggregator.GetAsync();
        categoryStore.ThrowOnNextBuild();

        aggregator.Invalidate();
        Assert.Equal(cached, await aggregator.GetAsync());
        await categoryStore.WaitForFailedBuildAsync();

        Assert.Equal(cached, await aggregator.GetAsync());
    }

    [Fact]
    public async Task GetAsync_CancelledCaller_DoesNotCancelBackgroundRebuild()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var categoryStore = new ImmediateCategoryKnowledgeStore();
        var aggregator = CreateAggregator(new ImmediateHarvestRunStore(), categoryStore, cache, new TestTimeProvider());
        await aggregator.GetAsync();
        categoryStore.BlockNextBuild();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        aggregator.Invalidate();
        await aggregator.GetAsync(cancellation.Token);
        await categoryStore.WaitForBlockedBuildAsync();

        Assert.False(categoryStore.BlockedBuildToken.CanBeCanceled);
        categoryStore.ReleaseBlockedBuild();
    }

    private static HarvestRunRow HealthRun(int enqueued, int drained)
        => new(Guid.NewGuid(), HarvestRunKind.Bulk, HarvestRunState.Succeeded, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, 0, 0, 0, enqueued, drained, null, null);

    private static HarvestStatsAggregator CreateAggregator(
        IHarvestRunStore runStore,
        ICategoryKnowledgeStore categoryStore,
        IMemoryCache cache,
        int queuedDeckCount = 12)
        => new(
            runStore,
            new FakeHarvestScheduleCache(),
            categoryStore,
            cache,
            NullLogger<HarvestStatsAggregator>.Instance,
            Options.Create(new HarvestHealthOptions { BacklogFloor = 100 }));

    private static HarvestStatsAggregator CreateAggregator(
        IHarvestRunStore runStore,
        ICategoryKnowledgeStore categoryStore,
        IMemoryCache cache,
        TimeProvider timeProvider)
        => new(
            runStore,
            new FakeHarvestScheduleCache(),
            categoryStore,
            cache,
            NullLogger<HarvestStatsAggregator>.Instance,
            Options.Create(new HarvestHealthOptions { BacklogFloor = 100 }),
            timeProvider);

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

        public Task<ArchidektCacheRunResult> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
            => Task.FromResult(new ArchidektCacheRunResult(0, 0, 0, 0, 0, TimeSpan.Zero));

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

        public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, DeckFlow.Core.Integration.ArchidektDeckMetadata? metadata, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<HarvestedCommanderRow>> GetFilteredProcessedCommandersAsync(int page, int pageSize, CommanderGridQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HarvestedCommanderRow>>(Array.Empty<HarvestedCommanderRow>());
        public Task<IReadOnlyList<HarvestedCommanderRow>> GetAllFilteredProcessedCommandersAsync(CommanderGridQuery query, int maxRows, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HarvestedCommanderRow>>(Array.Empty<HarvestedCommanderRow>());
        public Task<int> GetFilteredProcessedCommanderCountAsync(CommanderGridQuery query, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => BlockAsync(42);

        public Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
            => BlockAsync(7);

        public Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default)
            => BlockAsync(99);

        public Task<int> GetUnprocessedCountAsync(CancellationToken cancellationToken = default)
            => BlockAsync(12);

        public Task<IReadOnlyList<HarvestedCommanderRow>> GetPagedProcessedCommandersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestedCommanderRow>>(Array.Empty<HarvestedCommanderRow>());

        public Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default)
            => BlockAsync(3);

        public Task<long?> GetDatabaseSizeBytesAsync(CancellationToken cancellationToken = default)
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
        private readonly int _queuedDeckCount;
        private TaskCompletionSource? _blockedBuildRelease;
        private TaskCompletionSource? _blockedBuildStarted;
        private TaskCompletionSource? _failedBuild;
        private CancellationToken _blockedBuildToken;
        private int _buildCount;
        private int _throwNextBuild;

        public ImmediateCategoryKnowledgeStore(int queuedDeckCount = 12) => _queuedDeckCount = queuedDeckCount;

        public int BuildCount => Volatile.Read(ref _buildCount);

        public CancellationToken BlockedBuildToken => _blockedBuildToken;

        public void BlockNextBuild()
        {
            _blockedBuildRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _blockedBuildStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task WaitForBlockedBuildAsync()
            => _blockedBuildStarted?.Task ?? throw new InvalidOperationException("No blocked build was configured.");

        public void ReleaseBlockedBuild() => _blockedBuildRelease?.TrySetResult();

        public void ThrowOnNextBuild()
        {
            _failedBuild = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Exchange(ref _throwNextBuild, 1);
        }

        public Task WaitForFailedBuildAsync()
            => _failedBuild?.Task ?? throw new InvalidOperationException("No failing build was configured.");
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

        public Task<ArchidektCacheRunResult> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
            => Task.FromResult(new ArchidektCacheRunResult(0, 0, 0, 0, 0, TimeSpan.Zero));

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

        public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, DeckFlow.Core.Integration.ArchidektDeckMetadata? metadata, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<HarvestedCommanderRow>> GetFilteredProcessedCommandersAsync(int page, int pageSize, CommanderGridQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HarvestedCommanderRow>>(Array.Empty<HarvestedCommanderRow>());
        public Task<IReadOnlyList<HarvestedCommanderRow>> GetAllFilteredProcessedCommandersAsync(CommanderGridQuery query, int maxRows, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HarvestedCommanderRow>>(Array.Empty<HarvestedCommanderRow>());
        public Task<int> GetFilteredProcessedCommanderCountAsync(CommanderGridQuery query, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _buildCount);
            if (Interlocked.Exchange(ref _throwNextBuild, 0) == 1)
            {
                _failedBuild?.TrySetResult();
                return Task.FromException<int>(new InvalidOperationException("Simulated rebuild failure."));
            }

            if (_blockedBuildRelease is null)
            {
                return Task.FromResult(42);
            }

            _blockedBuildToken = cancellationToken;
            _blockedBuildStarted?.TrySetResult();
            return WaitForBlockedBuildAsync(cancellationToken);
        }

        public Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(7);

        public Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(99);

        public Task<int> GetUnprocessedCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_queuedDeckCount);

        public Task<IReadOnlyList<HarvestedCommanderRow>> GetPagedProcessedCommandersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestedCommanderRow>>(Array.Empty<HarvestedCommanderRow>());

        public Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<long?> GetDatabaseSizeBytesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(4096L);

        public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult(CardDeckTotals.Empty);

        private async Task<int> WaitForBlockedBuildAsync(CancellationToken cancellationToken)
        {
            await _blockedBuildRelease!.Task.WaitAsync(cancellationToken);
            return 42;
        }
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan elapsed) => _utcNow += elapsed;
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

        public Task UpdateStateAsync(Guid id, HarvestRunState state, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc, int? decksProcessed, int? additionalDecksFound, string? errorMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateProgressAsync(Guid id, int decksProcessed, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SetSweepCountsAsync(Guid id, int decksEnqueued, int decksDrained, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<HarvestRunRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default)
            => BlockAsync<IReadOnlyList<HarvestRunRow>>(Array.Empty<HarvestRunRow>());

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentHealthSignalRunsAsync(int n, CancellationToken cancellationToken = default)
            => BlockAsync<IReadOnlyList<HarvestRunRow>>(Array.Empty<HarvestRunRow>());

        public Task<string> GetRecentRevisionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("0");

        public Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken cancellationToken = default)
            => BlockAsync<DateTimeOffset?>(LastSuccessUtc);

        public Task<HarvestFailureStreak> GetFailureStreakSinceLastSuccessAsync(CancellationToken cancellationToken = default)
            => BlockAsync(new HarvestFailureStreak(0, null, null));

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
        private readonly IReadOnlyList<HarvestRunRow> _healthRuns;

        public ImmediateHarvestRunStore(IReadOnlyList<HarvestRunRow>? healthRuns = null)
            => _healthRuns = healthRuns ?? Array.Empty<HarvestRunRow>();
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Guid> InsertQueuedAsync(HarvestRunKind kind, int durationSeconds, string? url, DateTimeOffset now, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task UpdateStateAsync(Guid id, HarvestRunState state, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc, int? decksProcessed, int? additionalDecksFound, string? errorMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateProgressAsync(Guid id, int decksProcessed, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SetSweepCountsAsync(Guid id, int decksEnqueued, int decksDrained, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<HarvestRunRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestRunRow>>(Array.Empty<HarvestRunRow>());

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentHealthSignalRunsAsync(int n, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestRunRow>>(_healthRuns.Take(n).ToList());

        public Task<string> GetRecentRevisionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("0");

        public Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        public Task<HarvestFailureStreak> GetFailureStreakSinceLastSuccessAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HarvestFailureStreak(0, null, null));

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
