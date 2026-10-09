using System;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// Aggregates HARV-06 stats; counts are cached for 60 seconds while run state and next runs are live.
/// </summary>
public sealed class HarvestStatsAggregator : IHarvestStatsAggregator
{
    private const string CacheKey = "admin.harvest.stats.v1";

    private readonly IHarvestRunStore _runStore;
    private readonly IHarvestScheduleCache _scheduleCache;
    private readonly IHarvestUpdateScheduleCache _updateScheduleCache;
    private readonly ICategoryKnowledgeStore _categoryStore;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<HarvestStatsAggregator> _logger;
    private readonly IOptions<HarvestHealthOptions> _healthOptions;
    private readonly TimeProvider _timeProvider;
    private readonly object _rebuildGate = new();
    // Why: the gate makes increment-plus-mark and compare-plus-publish atomic; Interlocked leaves a lost-mark window.
    private long _countsGeneration;
    private Task? _rebuildTask;

    /// <summary>
    /// Initializes the harvest stats aggregator with its SQL stores and cache.
    /// </summary>
    /// <param name="runStore">Harvest run store used for recent and last-success run data.</param>
    /// <param name="scheduleCache">Schedule cache used to calculate the next expected run.</param>
    /// <param name="updateScheduleCache">Update schedule cache used to calculate the update next run.</param>
    /// <param name="categoryStore">Category knowledge store used for processed deck and observation totals.</param>
    /// <param name="memoryCache">Memory cache that stores the cached deck counts.</param>
    /// <param name="logger">Logger that records stats rebuild diagnostics.</param>
    /// <param name="healthOptions">Options that configure backlog thresholds.</param>
    public HarvestStatsAggregator(
        IHarvestRunStore runStore,
        IHarvestScheduleCache scheduleCache,
        IHarvestUpdateScheduleCache updateScheduleCache,
        ICategoryKnowledgeStore categoryStore,
        IMemoryCache memoryCache,
        ILogger<HarvestStatsAggregator> logger,
        IOptions<HarvestHealthOptions> healthOptions)
        : this(runStore, scheduleCache, updateScheduleCache, categoryStore, memoryCache, logger, healthOptions, TimeProvider.System)
    {
    }

    internal HarvestStatsAggregator(
        IHarvestRunStore runStore,
        IHarvestScheduleCache scheduleCache,
        IHarvestUpdateScheduleCache updateScheduleCache,
        ICategoryKnowledgeStore categoryStore,
        IMemoryCache memoryCache,
        ILogger<HarvestStatsAggregator> logger,
        IOptions<HarvestHealthOptions> healthOptions,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(scheduleCache);
        ArgumentNullException.ThrowIfNull(updateScheduleCache);
        ArgumentNullException.ThrowIfNull(categoryStore);
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(healthOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _runStore = runStore;
        _scheduleCache = scheduleCache;
        _updateScheduleCache = updateScheduleCache;
        _categoryStore = categoryStore;
        _memoryCache = memoryCache;
        _logger = logger;
        _healthOptions = healthOptions;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<HarvestStatsPayload> GetAsync(CancellationToken cancellationToken = default)
    {
        var countsTask = GetCountsAsync();
        var recentRunsTask = ReadLiveAsync(
            () => _runStore.GetRecentAsync(10, cancellationToken),
            Array.Empty<HarvestRunRow>(),
            "recent runs",
            cancellationToken);
        var healthSignalRunsTask = ReadLiveAsync(
            () => _runStore.GetRecentHealthSignalRunsAsync(HarvestHealthOptions.RecentRunsWindow + 1, cancellationToken),
            Array.Empty<HarvestRunRow>(),
            "health signal runs",
            cancellationToken);
        var lastBulkScheduledSuccessUtcTask = ReadLiveAsync(
            () => _runStore.GetLastScheduledSuccessUtcAsync(HarvestRunKind.Bulk, cancellationToken),
            null,
            "last bulk scheduled success",
            cancellationToken);
        var lastUpdateScheduledSuccessUtcTask = ReadLiveAsync(
            () => _runStore.GetLastScheduledSuccessUtcAsync(HarvestRunKind.Update, cancellationToken),
            null,
            "last update scheduled success",
            cancellationToken);
        await Task.WhenAll(countsTask, recentRunsTask, healthSignalRunsTask, lastBulkScheduledSuccessUtcTask, lastUpdateScheduledSuccessUtcTask).ConfigureAwait(false);
        return ComposePayload(await countsTask.ConfigureAwait(false), await recentRunsTask.ConfigureAwait(false), await healthSignalRunsTask.ConfigureAwait(false), await lastBulkScheduledSuccessUtcTask.ConfigureAwait(false), await lastUpdateScheduledSuccessUtcTask.ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
        lock (_rebuildGate)
        {
            _countsGeneration++;
            if (_memoryCache.TryGetValue(CacheKey, out CachedHarvestStats? cached) && cached is not null)
            {
                _memoryCache.Set(CacheKey, cached with { CachedAtUtc = DateTimeOffset.MinValue });
            }
        }

        _logger.LogDebug("Harvest stats cache marked stale");
    }

    private async Task<CountSlice> GetCountsAsync()
    {
        if (_memoryCache.TryGetValue(CacheKey, out CachedHarvestStats? cached) && cached is not null)
        {
            if (_timeProvider.GetUtcNow() - cached.CachedAtUtc < TimeSpan.FromSeconds(60))
            {
                return cached.Counts;
            }

            _ = StartRebuild();
            return cached.Counts;
        }

        return await StartRebuild().ConfigureAwait(false);
    }

    private Task<CountSlice> StartRebuild()
    {
        TaskCompletionSource<CountSlice> completion;
        long generation;
        lock (_rebuildGate)
        {
            if (_rebuildTask is Task<CountSlice> rebuildTask)
            {
                return rebuildTask;
            }

            completion = new TaskCompletionSource<CountSlice>(TaskCreationOptions.RunContinuationsAsynchronously);
            _rebuildTask = completion.Task;
            generation = _countsGeneration;
        }

        _ = completion.Task.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        _ = CompleteRebuildAsync(completion, generation);
        return completion.Task;
    }

    private async Task<T> ReadLiveAsync<T>(Func<Task<T>> readAsync, T fallback, string readName, CancellationToken cancellationToken)
    {
        try
        {
            return await readAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Harvest stats live {ReadName} read failed; using fallback.", readName);
            return fallback;
        }
    }

    private async Task CompleteRebuildAsync(TaskCompletionSource<CountSlice> completion, long generation)
    {
        try
        {
            var counts = await BuildCountsAsync(CancellationToken.None).ConfigureAwait(false);
            lock (_rebuildGate)
            {
                var stamp = _countsGeneration == generation ? _timeProvider.GetUtcNow() : DateTimeOffset.MinValue;
                _memoryCache.Set(CacheKey, new CachedHarvestStats(counts, stamp));
            }

            completion.TrySetResult(counts);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Harvest stats refresh failed; retaining the last good payload.");
            completion.TrySetException(exception);
        }
        finally
        {
            lock (_rebuildGate)
            {
                if (ReferenceEquals(_rebuildTask, completion.Task))
                {
                    _rebuildTask = null;
                }
            }
        }
    }

    private async Task<CountSlice> BuildCountsAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Harvest.Stats.Build rebuilding cached payload.");

        var totalDecksTask = _categoryStore.GetTotalProcessedDeckCountAsync(cancellationToken);
        var totalDecks30dTask = _categoryStore.GetTotalProcessedDeckCountSinceAsync(
            _timeProvider.GetUtcNow().UtcDateTime.AddDays(-30),
            cancellationToken);
        var queuedDeckCountTask = _categoryStore.GetUnprocessedCountAsync(cancellationToken);
        var distinctCommanderCountTask = _categoryStore.GetDistinctProcessedCommanderCountAsync(cancellationToken);
        var totalObservationsTask = _categoryStore.GetTotalObservationCountAsync(cancellationToken);
        var databaseSizeBytesTask = _categoryStore.GetDatabaseSizeBytesAsync(cancellationToken);

        await Task.WhenAll(
            totalDecksTask,
            totalDecks30dTask,
            queuedDeckCountTask,
            distinctCommanderCountTask,
            totalObservationsTask,
            databaseSizeBytesTask).ConfigureAwait(false);

        var totalDecks = await totalDecksTask.ConfigureAwait(false);
        var totalDecks30d = await totalDecks30dTask.ConfigureAwait(false);
        var queuedDeckCount = await queuedDeckCountTask.ConfigureAwait(false);
        var distinctCommanderCount = await distinctCommanderCountTask.ConfigureAwait(false);
        var totalObservations = await totalObservationsTask.ConfigureAwait(false);
        var databaseSizeBytes = await databaseSizeBytesTask.ConfigureAwait(false);
        return new CountSlice(totalDecks, totalDecks30d, queuedDeckCount, distinctCommanderCount, totalObservations, databaseSizeBytes);
    }

    private HarvestStatsPayload ComposePayload(
        CountSlice counts,
        IReadOnlyList<HarvestRunRow> recentRuns,
        IReadOnlyList<HarvestRunRow> healthSignalRuns,
        DateTimeOffset? lastBulkScheduledSuccessUtc,
        DateTimeOffset? lastUpdateScheduledSuccessUtc)
    {
        var scheduleSnapshot = _scheduleCache.Snapshot();
        var updateScheduleSnapshot = _updateScheduleCache.Snapshot();
        var now = _timeProvider.GetUtcNow();
        var health = DeriveHealthSignals(healthSignalRuns, counts.QueuedDeckCount, _healthOptions.Value);
        // D-08 must match the scheduler: manual completions never move a kind's displayed next run.
        return new HarvestStatsPayload(
            counts.TotalDecks, counts.TotalDecks30d, counts.QueuedDeckCount, counts.DistinctCommanderCount,
            counts.TotalObservations, recentRuns, counts.DatabaseSizeBytes, lastBulkScheduledSuccessUtc,
            NextScheduledUtc(lastBulkScheduledSuccessUtc, scheduleSnapshot.IntervalHours is int hours ? TimeSpan.FromHours(hours) : null, scheduleSnapshot.Paused, now),
            lastUpdateScheduledSuccessUtc,
            NextScheduledUtc(lastUpdateScheduledSuccessUtc, updateScheduleSnapshot.IntervalMinutes is int minutes ? TimeSpan.FromMinutes(minutes) : null, updateScheduleSnapshot.Paused, now), health);
    }

    private static DateTimeOffset? NextScheduledUtc(DateTimeOffset? anchor, TimeSpan? interval, bool paused, DateTimeOffset now)
        => interval is null || paused ? null : anchor + interval ?? now;

    private sealed record CountSlice(int TotalDecks, int TotalDecks30d, int QueuedDeckCount, int DistinctCommanderCount, int TotalObservations, long? DatabaseSizeBytes);

    /// <summary>Pairs cached harvest counts with their cache time for freshness checks.</summary>
    private sealed record CachedHarvestStats(CountSlice Counts, DateTimeOffset CachedAtUtc);

    /// <summary>
    /// Derives health only from persisted sweep counts (see plan 01-05 and
    /// <see cref="IHarvestRunStore.SetSweepCountsAsync"/>): processed and additional-found
    /// counts cannot reconstruct queue growth, including its accepted requeue-reset understatement.
    /// </summary>
    private static HarvestHealthSignals DeriveHealthSignals(
        IReadOnlyList<HarvestRunRow> qualifyingRuns,
        int queuedDeckCount,
        HarvestHealthOptions options)
    {
        var windowCount = Math.Min(qualifyingRuns.Count, HarvestHealthOptions.RecentRunsWindow);
        var window = qualifyingRuns.Take(windowCount).ToList();
        var truncated = qualifyingRuns.Count > windowCount;
        var sentinel = truncated ? qualifyingRuns[windowCount] : null;
        // Both sweep counts are non-null due to the store query predicate; do not coalesce unknowns to zero.
        var growing = window.Count >= options.BacklogGrowthRunCount
            && window.Take(options.BacklogGrowthRunCount).All(run => run.DecksEnqueued!.Value - run.DecksDrained!.Value > 0);
        var aboveFloor = queuedDeckCount > options.BacklogFloor;
        var reason = (aboveFloor, growing) switch
        {
            (true, true) => HarvestBacklogReason.AboveFloorAndGrowing,
            (true, false) => HarvestBacklogReason.AboveFloor,
            (false, true) => HarvestBacklogReason.Growing,
            _ => HarvestBacklogReason.None
        };
        var zeroStreak = window.TakeWhile(run => run.DecksEnqueued == 0).Count();
        var capped = truncated && zeroStreak == window.Count && sentinel!.DecksEnqueued == 0;
        return new HarvestHealthSignals(
            reason != HarvestBacklogReason.None,
            reason,
            options.BacklogFloor,
            options.BacklogGrowthRunCount,
            zeroStreak,
            capped);
    }
}
