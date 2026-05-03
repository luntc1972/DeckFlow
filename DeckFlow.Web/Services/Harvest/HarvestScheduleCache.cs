using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// Default <see cref="IHarvestScheduleCache"/> backed by <see cref="IHarvestScheduleStore"/>.
/// Mirrors <see cref="DeckFlow.Web.Services.FeatureFlags.FeatureFlagCache"/> line-for-line:
/// inherits <see cref="BackgroundService"/> for the 30-second poller, overrides
/// <see cref="StartAsync"/> to perform a synchronous initial load before the host reports
/// ready (D-07, mirrors Phase 6 D-14), and atomically replaces the snapshot reference on
/// each successful reload. Preserve-on-failure semantics ensure transient Postgres errors
/// never stomp a good snapshot with a stub default.
/// </summary>
public sealed class HarvestScheduleCache : BackgroundService, IHarvestScheduleCache
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly HarvestScheduleSnapshot DefaultSnapshot =
        new(IntervalHours: null, Paused: false, UpdatedUtc: DateTimeOffset.MinValue);

    private readonly IHarvestScheduleStore _store;
    private readonly ILogger<HarvestScheduleCache> _logger;

    /// <summary>Atomically replaced by ReloadAsync; reads are lock-free.</summary>
    private volatile HarvestScheduleSnapshot _snapshot = DefaultSnapshot;

    /// <summary>
    /// DI constructor. Registered as a singleton and as an IHostedService (see Plan 07
    /// for the dual-registration extension method that mirrors
    /// <c>FeatureFlagsServiceCollectionExtensions.AddDeckFlowFeatureFlags</c>).
    /// </summary>
    /// <param name="store">Harvest schedule persistence store (Postgres or SQLite).</param>
    /// <param name="logger">Logger for poll failures.</param>
    public HarvestScheduleCache(IHarvestScheduleStore store, ILogger<HarvestScheduleCache> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _logger = logger;
    }

    /// <summary>Test seam — bypasses logging plumbing for unit tests that drive the cache directly.</summary>
    /// <param name="store">Harvest schedule persistence store fake or stub.</param>
    internal HarvestScheduleCache(IHarvestScheduleStore store)
        : this(store, NullLogger<HarvestScheduleCache>.Instance) { }

    /// <inheritdoc />
    public HarvestScheduleSnapshot Snapshot() => _snapshot;

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var fresh = await _store.GetAsync(cancellationToken).ConfigureAwait(false);
            _snapshot = fresh;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Never replace a good snapshot with a stub on transient PG failure (S-7 mirror).
            _logger.LogError(exception,
                "Harvest.Schedule.ReloadFailure could not refresh harvest_schedule snapshot; existing snapshot preserved (intervalHours={IntervalHours} paused={Paused}).",
                _snapshot.IntervalHours, _snapshot.Paused);
        }
    }

    /// <summary>
    /// D-07 / Phase 6 D-14: synchronous initial load before the host reports ready, so the
    /// very first scheduler tick (and the very first <c>/Admin/Harvest</c> page render) sees
    /// a populated snapshot rather than the empty cold-start default.
    /// </summary>
    /// <param name="cancellationToken">Host startup cancellation token.</param>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>30s poller backstop (D-07). ReloadAsync swallows non-cancellation exceptions internally.</summary>
    /// <param name="stoppingToken">Cancellation token signaled when the host is shutting down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await ReloadAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown path.
        }
    }
}
