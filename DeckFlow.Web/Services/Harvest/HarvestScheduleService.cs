using System;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// Recurring harvest scheduler (Phase 7, D-06 / HARV-04 / HARV-05). Wakes every 60 seconds,
/// reads the cached <c>harvest_schedule</c> snapshot, and fires a 60-minute bulk harvest
/// when <c>now &gt;= last_success_utc + interval_hours</c> AND the schedule is not paused
/// AND <c>interval_hours</c> is set. The whole loop is gated by
/// <see cref="IFeatureFlagCache.IsEnabled"/> on <c>harvest.cron.enabled</c>
/// (Phase 6 FLAG-04 carry-forward kill switch). Per-tick try/catch keeps the loop alive
/// across transient PG / job-service errors (T-07-14).
/// </summary>
public sealed class HarvestScheduleService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan FireDuration = TimeSpan.FromMinutes(60);
    private const string CronEnabledFlagKey = "harvest.cron.enabled";

    private readonly IFeatureFlagCache _flagCache;
    private readonly IHarvestScheduleCache _scheduleCache;
    private readonly IHarvestRunStore _runStore;
    private readonly IArchidektCacheJobService _jobService;
    private readonly ILogger<HarvestScheduleService> _logger;

    /// <summary>
    /// DI constructor. Registered as an <see cref="IHostedService"/> in Plan 07.
    /// </summary>
    /// <param name="flagCache">Feature flag cache used to honor the <c>harvest.cron.enabled</c> kill switch.</param>
    /// <param name="scheduleCache">Cached <c>harvest_schedule</c> snapshot (no per-tick PG roundtrip).</param>
    /// <param name="runStore">Run-history store used to read <c>last_success_utc</c>.</param>
    /// <param name="jobService">Bulk-harvest job service called when a tick is due to fire.</param>
    /// <param name="logger">Structured logger for tick / fire / failure events.</param>
    public HarvestScheduleService(
        IFeatureFlagCache flagCache,
        IHarvestScheduleCache scheduleCache,
        IHarvestRunStore runStore,
        IArchidektCacheJobService jobService,
        ILogger<HarvestScheduleService> logger)
    {
        ArgumentNullException.ThrowIfNull(flagCache);
        ArgumentNullException.ThrowIfNull(scheduleCache);
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(jobService);
        ArgumentNullException.ThrowIfNull(logger);
        _flagCache = flagCache;
        _scheduleCache = scheduleCache;
        _runStore = runStore;
        _jobService = jobService;
        _logger = logger;
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

        var snapshot = _scheduleCache.Snapshot();

        // Off (interval_hours IS NULL) or operator-paused short-circuits before any PG read.
        if (snapshot.Paused || snapshot.IntervalHours is null)
        {
            return;
        }

        // Single PG read per tick at 60s cadence (T-07-11 mitigation accepted).
        var lastSuccess = await _runStore.GetLastSuccessUtcAsync(cancellationToken).ConfigureAwait(false);

        // No prior successful run yet — fire immediately so enabling cron doesn't have to
        // wait an entire interval for the first sweep.
        DateTimeOffset? nextDue = lastSuccess.HasValue
            ? lastSuccess.Value + TimeSpan.FromHours(snapshot.IntervalHours.Value)
            : null;

        var now = DateTimeOffset.UtcNow;
        if (nextDue.HasValue && now < nextDue.Value)
        {
            return;
        }

        _logger.LogInformation(
            "Harvest.Schedule.Tick.Fired intervalHours={IntervalHours} lastSuccess={LastSuccess} nextDue={NextDue}",
            snapshot.IntervalHours, lastSuccess, nextDue);

        await _jobService.EnqueueAsync(FireDuration, cancellationToken).ConfigureAwait(false);
    }
}
