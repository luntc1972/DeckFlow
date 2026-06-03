using System;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Initializes the harvest stats aggregator with its SQL stores and cache.
    /// </summary>
    /// <param name="runStore">Harvest run store used for recent and last-success run data.</param>
    /// <param name="scheduleCache">Schedule cache used to calculate the next expected run.</param>
    /// <param name="categoryStore">Category knowledge store used for processed deck and observation totals.</param>
    /// <param name="memoryCache">Memory cache that stores the stats payload.</param>
    /// <param name="logger">Logger that records stats rebuild diagnostics.</param>
    public HarvestStatsAggregator(
        IHarvestRunStore runStore,
        IHarvestScheduleCache scheduleCache,
        ICategoryKnowledgeStore categoryStore,
        IMemoryCache memoryCache,
        ILogger<HarvestStatsAggregator> logger)
    {
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(scheduleCache);
        ArgumentNullException.ThrowIfNull(categoryStore);
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(logger);

        _runStore = runStore;
        _scheduleCache = scheduleCache;
        _categoryStore = categoryStore;
        _memoryCache = memoryCache;
        _logger = logger;
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
        var totalObservationsTask = _categoryStore.GetTotalObservationCountAsync(cancellationToken);
        var postgresStorageBytesTask = _categoryStore.GetPostgresDatabaseSizeBytesAsync(cancellationToken);
        var recentRunsTask = _runStore.GetRecentAsync(10, cancellationToken);
        var lastSuccessUtcTask = _runStore.GetLastSuccessUtcAsync(cancellationToken);

        await Task.WhenAll(
            totalDecksTask,
            totalDecks30dTask,
            totalObservationsTask,
            postgresStorageBytesTask,
            recentRunsTask,
            lastSuccessUtcTask).ConfigureAwait(false);

        var totalDecks = await totalDecksTask.ConfigureAwait(false);
        var totalDecks30d = await totalDecks30dTask.ConfigureAwait(false);
        var totalObservations = await totalObservationsTask.ConfigureAwait(false);
        var postgresStorageBytes = await postgresStorageBytesTask.ConfigureAwait(false);
        var recentRuns = await recentRunsTask.ConfigureAwait(false);
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
            totalObservations,
            recentRuns,
            postgresStorageBytes,
            lastSuccessUtc,
            nextScheduledUtc);
    }
}
