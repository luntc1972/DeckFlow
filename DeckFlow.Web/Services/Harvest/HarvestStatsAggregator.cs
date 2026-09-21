using System;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// Aggregates the HARV-06 stats payload under a 60-second IMemoryCache entry.
/// </summary>
public sealed class HarvestStatsAggregator : IHarvestStatsAggregator
{
    private const string CacheKey = "admin.harvest.stats.v1";

    private readonly IHarvestRunStore _runStore;
    private readonly IHarvestScheduleCache _scheduleCache;
    private readonly ICategoryKnowledgeStore _categoryStore;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<HarvestStatsAggregator> _logger;
    private readonly IOptions<HarvestHealthOptions> _healthOptions;

    /// <summary>
    /// Initializes the harvest stats aggregator with its SQL stores and cache.
    /// </summary>
    /// <param name="runStore">Harvest run store used for recent and last-success run data.</param>
    /// <param name="scheduleCache">Schedule cache used to calculate the next expected run.</param>
    /// <param name="categoryStore">Category knowledge store used for processed deck and observation totals.</param>
    /// <param name="memoryCache">Memory cache that stores the stats payload.</param>
    /// <param name="logger">Logger that records stats rebuild diagnostics.</param>
    /// <param name="healthOptions">Options that configure backlog thresholds.</param>
    public HarvestStatsAggregator(
        IHarvestRunStore runStore,
        IHarvestScheduleCache scheduleCache,
        ICategoryKnowledgeStore categoryStore,
        IMemoryCache memoryCache,
        ILogger<HarvestStatsAggregator> logger,
        IOptions<HarvestHealthOptions> healthOptions)
    {
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(scheduleCache);
        ArgumentNullException.ThrowIfNull(categoryStore);
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(healthOptions);

        _runStore = runStore;
        _scheduleCache = scheduleCache;
        _categoryStore = categoryStore;
        _memoryCache = memoryCache;
        _logger = logger;
        _healthOptions = healthOptions;
    }

    /// <inheritdoc/>
    public Task<HarvestStatsPayload> GetAsync(CancellationToken cancellationToken = default)
        => _memoryCache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await BuildAsync(cancellationToken).ConfigureAwait(false);
        })!;

    /// <inheritdoc/>
    public void Invalidate()
    {
        _memoryCache.Remove(CacheKey);
        Log.Debug("Harvest stats cache invalidated");
    }

    private async Task<HarvestStatsPayload> BuildAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Harvest.Stats.Build rebuilding cached payload.");

        var totalDecksTask = _categoryStore.GetTotalProcessedDeckCountAsync(cancellationToken);
        var totalDecks30dTask = _categoryStore.GetTotalProcessedDeckCountSinceAsync(
            DateTime.UtcNow.AddDays(-30),
            cancellationToken);
        var queuedDeckCountTask = _categoryStore.GetUnprocessedCountAsync(cancellationToken);
        var distinctCommanderCountTask = _categoryStore.GetDistinctProcessedCommanderCountAsync(cancellationToken);
        var totalObservationsTask = _categoryStore.GetTotalObservationCountAsync(cancellationToken);
        var databaseSizeBytesTask = _categoryStore.GetDatabaseSizeBytesAsync(cancellationToken);
        var recentRunsTask = _runStore.GetRecentAsync(10, cancellationToken);
        var healthSignalRunsTask = _runStore.GetRecentHealthSignalRunsAsync(HarvestHealthOptions.RecentRunsWindow + 1, cancellationToken);
        var lastSuccessUtcTask = _runStore.GetLastSuccessUtcAsync(cancellationToken);

        await Task.WhenAll(
            totalDecksTask,
            totalDecks30dTask,
            queuedDeckCountTask,
            distinctCommanderCountTask,
            totalObservationsTask,
            databaseSizeBytesTask,
            recentRunsTask,
            healthSignalRunsTask,
            lastSuccessUtcTask).ConfigureAwait(false);

        var totalDecks = await totalDecksTask.ConfigureAwait(false);
        var totalDecks30d = await totalDecks30dTask.ConfigureAwait(false);
        var queuedDeckCount = await queuedDeckCountTask.ConfigureAwait(false);
        var distinctCommanderCount = await distinctCommanderCountTask.ConfigureAwait(false);
        var totalObservations = await totalObservationsTask.ConfigureAwait(false);
        var databaseSizeBytes = await databaseSizeBytesTask.ConfigureAwait(false);
        var recentRuns = await recentRunsTask.ConfigureAwait(false);
        var healthSignalRuns = await healthSignalRunsTask.ConfigureAwait(false);
        var health = DeriveHealthSignals(healthSignalRuns, queuedDeckCount, _healthOptions.Value);
        var lastSuccessUtc = await lastSuccessUtcTask.ConfigureAwait(false);
        var scheduleSnapshot = _scheduleCache.Snapshot();
        DateTimeOffset? nextScheduledUtc =
            lastSuccessUtc.HasValue
            && scheduleSnapshot.IntervalHours.HasValue
            && !scheduleSnapshot.Paused
                ? lastSuccessUtc.Value + TimeSpan.FromHours(scheduleSnapshot.IntervalHours.Value)
                : null;

        return new HarvestStatsPayload(
            totalDecks,
            totalDecks30d,
            queuedDeckCount,
            distinctCommanderCount,
            totalObservations,
            recentRuns,
            databaseSizeBytes,
            lastSuccessUtc,
            nextScheduledUtc,
            health);
    }

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
