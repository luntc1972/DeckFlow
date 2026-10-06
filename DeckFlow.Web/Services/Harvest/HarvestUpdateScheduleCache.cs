using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>30-second cache for the separate update schedule, preserving its prior snapshot on reload failure.</summary>
public sealed class HarvestUpdateScheduleCache : BackgroundService, IHarvestUpdateScheduleCache
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly HarvestUpdateScheduleSnapshot DefaultSnapshot = new(null, false, DateTimeOffset.MinValue);
    private readonly IHarvestUpdateScheduleStore _store;
    private readonly ILogger<HarvestUpdateScheduleCache> _logger;
    private volatile HarvestUpdateScheduleSnapshot _snapshot = DefaultSnapshot;
    private readonly object _publishGate = new();
    private long _generation;

    /// <summary>Creates the hosted cache.</summary>
    public HarvestUpdateScheduleCache(IHarvestUpdateScheduleStore store, ILogger<HarvestUpdateScheduleCache> logger)
    {
        _store = store;
        _logger = logger;
    }
    /// <summary>Test seam that avoids logging setup.</summary>
    internal HarvestUpdateScheduleCache(IHarvestUpdateScheduleStore store) : this(store, NullLogger<HarvestUpdateScheduleCache>.Instance) { }
    /// <inheritdoc />
    public HarvestUpdateScheduleSnapshot Snapshot() => _snapshot;

    /// <inheritdoc />
    public void ForcePausedSnapshot()
    {
        lock (_publishGate)
        {
            _generation++;
            _snapshot = _snapshot with { Paused = true };
        }
    }
    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            long generation;
            lock (_publishGate) { generation = _generation; }
            var fresh = await _store.GetAsync(cancellationToken).ConfigureAwait(false);
            lock (_publishGate)
            {
                // Why: a read that began before a forced pause may predate the committed pause.
                if (generation == _generation) _snapshot = fresh;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Harvest.UpdateSchedule.ReloadFailure could not refresh harvest_update_schedule snapshot; existing snapshot preserved (intervalMinutes={IntervalMinutes} paused={Paused}).", _snapshot.IntervalMinutes, _snapshot.Paused);
        }
    }
    /// <inheritdoc />
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) await ReloadAsync(stoppingToken).ConfigureAwait(false);
    }
}
