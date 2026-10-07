using System;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// Recurring harvest scheduler. Wakes every 60 seconds, evaluates bulk then update snapshots,
/// and anchors each kind on its own last scheduled success. A due kind skips while any run
/// is active; bulk evaluates first and each tick enqueues at most one job. The whole loop is gated by
/// <see cref="IFeatureFlagCache.IsEnabled"/> on <c>service.harvest-cron.enabled</c>
/// (Phase 6 FLAG-04 carry-forward kill switch). Per-tick try/catch keeps the loop alive
/// across transient PG / job-service errors (T-07-14).
/// </summary>
public sealed class HarvestScheduleService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan FireDuration = TimeSpan.FromMinutes(60);
    private const string CronEnabledFlagKey = "service.harvest-cron.enabled";

    private readonly IFeatureFlagCache _flagCache;
    private readonly IHarvestScheduleCache _scheduleCache;
    private readonly IHarvestUpdateScheduleCache _updateScheduleCache;
    private readonly IHarvestRunStore _runStore;
    private readonly IArchidektCacheJobService _jobService;
    private readonly ILogger<HarvestScheduleService> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// DI constructor. Registered as an <see cref="IHostedService"/> in Plan 07.
    /// </summary>
    /// <param name="flagCache">Feature flag cache used to honor the <c>service.harvest-cron.enabled</c> kill switch.</param>
    /// <param name="scheduleCache">Cached <c>harvest_schedule</c> snapshot (no per-tick PG roundtrip).</param>
    /// <param name="updateScheduleCache">Cached update schedule snapshot.</param>
    /// <param name="runStore">Run-history store used to read <c>last_success_utc</c>.</param>
    /// <param name="jobService">Bulk-harvest job service called when a tick is due to fire.</param>
    /// <param name="logger">Structured logger for tick / fire / failure events.</param>
    /// <param name="timeProvider">Clock used for due-time checks; defaults to <see cref="TimeProvider.System"/> so tests can pin time.</param>
    public HarvestScheduleService(
        IFeatureFlagCache flagCache,
        IHarvestScheduleCache scheduleCache,
        IHarvestUpdateScheduleCache updateScheduleCache,
        IHarvestRunStore runStore,
        IArchidektCacheJobService jobService,
        ILogger<HarvestScheduleService> logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(flagCache);
        ArgumentNullException.ThrowIfNull(scheduleCache);
        ArgumentNullException.ThrowIfNull(updateScheduleCache);
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(jobService);
        ArgumentNullException.ThrowIfNull(logger);
        _flagCache = flagCache;
        _scheduleCache = scheduleCache;
        _updateScheduleCache = updateScheduleCache;
        _runStore = runStore;
        _jobService = jobService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// 60-second tick loop. Each tick is wrapped in its own try/catch so a single bad tick
    /// (transient PG failure, job service hiccup) does not exit the loop — the next tick
    /// retries. Cancellation on <paramref name="stoppingToken"/> is the normal-shutdown path.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token signaled when the host is shutting down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await TickAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception,
                        "Harvest.Schedule.Tick.Failure scheduler tick threw; loop continues. error={Message}",
                        exception.Message);
                    // Do not exit the loop — the next 60s tick will retry.
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        // FLAG-04 / D-06: kill-switch gate. Off → return without any PG/job work.
        if (!_flagCache.IsEnabled(CronEnabledFlagKey))
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var bulk = _scheduleCache.Snapshot();
        bool bulkEnqueued;
        try
        {
            bulkEnqueued = await TickKindAsync(HarvestRunKind.Bulk, bulk.IntervalHours is null ? null : TimeSpan.FromHours(bulk.IntervalHours.Value), bulk.Paused, FireDuration, now, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Harvest.Schedule.Tick.KindFailure kind={Kind}", HarvestRunKind.Bulk);
            bulkEnqueued = false;
        }
        if (bulkEnqueued) return;
        var update = _updateScheduleCache.Snapshot();
        try
        {
            await TickKindAsync(HarvestRunKind.Update, update.IntervalMinutes is null ? null : TimeSpan.FromMinutes(update.IntervalMinutes.Value), update.Paused, ArchidektCacheJobService.UpdateRunDuration, now, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Harvest.Schedule.Tick.KindFailure kind={Kind}", HarvestRunKind.Update);
        }
    }

    private async Task<bool> TickKindAsync(HarvestRunKind kind, TimeSpan? interval, bool paused, TimeSpan fireDuration, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (paused || interval is null) return false;
        var failureStreak = await _runStore.GetFailureStreakSinceLastSuccessAsync(kind, cancellationToken).ConfigureAwait(false);
        var lastSuccess = failureStreak.LastSuccessUtc;
        DateTimeOffset? nextDue = lastSuccess + interval;
        var failureDue = failureStreak.LastFailureUtc + GetFailureBackoff(failureStreak.ConsecutiveFailures, interval.Value);
        nextDue = Max(nextDue, failureDue);
        if (nextDue.HasValue && now < nextDue.Value) return false;
        var activeRun = await _runStore.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (activeRun is not null)
        {
            _logger.LogInformation("Harvest.Schedule.Tick.SkippedActiveRun kind={Kind} activeRunId={ActiveRunId} activeKind={ActiveKind}", kind, activeRun.Id, activeRun.Kind);
            return false;
        }
        _logger.LogInformation("Harvest.Schedule.Tick.Fired kind={Kind} interval={Interval} lastScheduledSuccess={LastScheduledSuccess} nextDue={NextDue}", kind, interval, lastSuccess, nextDue);
        await _jobService.EnqueueAsync(kind, fireDuration, HarvestTriggerSource.Scheduled, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static TimeSpan GetFailureBackoff(int consecutiveFailures, TimeSpan interval)
    {
        if (consecutiveFailures <= 0) return TimeSpan.Zero;
        var minutes = 15L << Math.Min(consecutiveFailures - 1, 20);
        return TimeSpan.FromMinutes(Math.Min(minutes, interval.TotalMinutes));
    }

    private static DateTimeOffset? Max(DateTimeOffset? first, DateTimeOffset? second)
        => first is null || (second.HasValue && second.Value > first.Value) ? second : first;
}
