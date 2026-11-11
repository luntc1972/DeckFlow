using System.Threading.Channels;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Runs harvest maintenance operations on a host-managed background loop.</summary>
public sealed class HarvestMaintenanceRunner : BackgroundService, IHarvestMaintenanceRunner
{
    private readonly object _lock = new();
    private readonly ICategoryKnowledgeStore _store;
    private readonly IHarvestMaintenanceRunStore _runStore;
    private readonly TimeSpan _persistInterval;
    private readonly ILogger<HarvestMaintenanceRunner> _logger;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Channel<HarvestMaintenanceOperation> _queue = Channel.CreateUnbounded<HarvestMaintenanceOperation>();
    private HarvestMaintenanceStatus _status = HarvestMaintenanceStatus.Idle;
    private CancellationTokenSource? _operationCts;

    /// <summary>Initializes a maintenance runner using UTC time.</summary>
    public HarvestMaintenanceRunner(ICategoryKnowledgeStore store, IHarvestMaintenanceRunStore runStore, ILogger<HarvestMaintenanceRunner> logger)
        : this(store, runStore, logger, () => DateTimeOffset.UtcNow)
    {
    }

    internal HarvestMaintenanceRunner(ICategoryKnowledgeStore store, IHarvestMaintenanceRunStore runStore, ILogger<HarvestMaintenanceRunner> logger, Func<DateTimeOffset> utcNow, TimeSpan? persistInterval = null, TimeSpan? stopGrace = null)
    {
        _store = store;
        _runStore = runStore;
        _persistInterval = persistInterval ?? TimeSpan.FromSeconds(15);
        _logger = logger;
        _utcNow = utcNow;
    }

    /// <inheritdoc/>
    public async Task<HarvestMaintenanceStartResult> TryStartAsync(HarvestMaintenanceOperation operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HarvestMaintenanceStatus previous;
        HarvestMaintenanceStatus starting;
        lock (_lock)
        {
            if (_status.IsRunning) return HarvestMaintenanceStartResult.AlreadyActive;
            previous = _status;
            _operationCts?.Dispose();
            _operationCts = new CancellationTokenSource();
            starting = _status = new HarvestMaintenanceStatus { RunId = Guid.NewGuid(), State = HarvestMaintenanceRunState.Starting, Operation = operation, StartedUtc = _utcNow() };
        }
        try
        {
            // Why: browser disconnects must not erase an accepted start.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _runStore.InsertAsync(starting, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Harvest.Maintenance.PersistFailed operation={Operation} state={State}", operation, starting.State);
            lock (_lock) { _status = previous; _operationCts?.Dispose(); _operationCts = null; }
            return HarvestMaintenanceStartResult.NotRecorded;
        }
        lock (_lock) { _queue.Writer.TryWrite(operation); }
        _logger.LogInformation("Harvest.Maintenance.Started operation={Operation}", operation);
        return HarvestMaintenanceStartResult.Started;
    }

    /// <inheritdoc/>
    public bool RequestStop()
    {
        lock (_lock)
        {
            if (!_status.IsRunning || _operationCts is null) return false;

            _status = _status with { StopRequested = true };
            _operationCts.Cancel();
            return true;
        }
    }

    /// <inheritdoc/>
    public HarvestMaintenanceStatus GetStatus()
    {
        lock (_lock)
        {
            return _status;
        }
    }

    /// <inheritdoc/>
    public async Task<HarvestMaintenanceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = GetStatus();
        if (status.State is not null) return status;
        try { return await _runStore.GetLatestAsync(cancellationToken).ConfigureAwait(false) ?? HarvestMaintenanceStatus.Idle; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { _logger.LogWarning(exception, "Harvest.Maintenance.StatusUnavailable"); return HarvestMaintenanceStatus.Idle; }
    }

    // Why: a hosted service, not a fire-and-forget task, so host shutdown cancels the run and waits for the loop.
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var operation in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            CancellationToken operationToken;
            lock (_lock)
            {
                operationToken = _operationCts?.Token ?? CancellationToken.None;

            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, operationToken);
            try
            {
                linkedCts.Token.ThrowIfCancellationRequested();
                lock (_lock) { _status = _status with { State = HarvestMaintenanceRunState.Running }; }
                await PersistAsync().ConfigureAwait(false);
                var lastSaved = GetStatus();
                var work = _store.RunMaintenanceAsync(operation, new InlineProgress(UpdateProgress), linkedCts.Token);
                while (!work.IsCompleted)
                {
                    using var tick = new CancellationTokenSource();
                    var delay = Task.Delay(_persistInterval, tick.Token);
                    if (await Task.WhenAny(work, delay).ConfigureAwait(false) == work) { tick.Cancel(); break; }
                    var current = GetStatus();
                    if (current != lastSaved) { await PersistAsync().ConfigureAwait(false); lastSaved = current; }
                }
                var outcome = await work.ConfigureAwait(false);
                Complete(operation, outcome.DecksAffected, outcome.StopReason, operationToken, stoppingToken, null);
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
                Complete(operation, null, null, operationToken, stoppingToken, null);
            }
            catch (Exception exception)
            {
                Complete(operation, null, null, operationToken, stoppingToken, exception);
            }
            await PersistAsync().ConfigureAwait(false);
        }
    }

    private async Task PersistAsync()
    {
        var status = GetStatus();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _runStore.SaveAsync(status, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Harvest.Maintenance.PersistFailed operation={Operation} state={State}", status.Operation, status.State);
        }
    }

    private sealed class InlineProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        lock (_lock)
        {
            _operationCts?.Dispose();
        }

        base.Dispose();
    }

    /// <inheritdoc/>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        // Why: shutdown is Interrupted; only an operator Stop is Stopped.
        return base.StopAsync(cancellationToken);
    }

    private void Complete(HarvestMaintenanceOperation operation, int? decksAffected, DeckPruningStopReason? stopReason, CancellationToken operationToken, CancellationToken stoppingToken, Exception? exception)
    {
        HarvestMaintenanceStatus status;
        lock (_lock)
        {
            var state = operationToken.IsCancellationRequested && _status.StopRequested ? HarvestMaintenanceRunState.Stopped
                : stoppingToken.IsCancellationRequested && !_status.StopRequested ? HarvestMaintenanceRunState.Interrupted
                : exception is not null ? HarvestMaintenanceRunState.Failed : HarvestMaintenanceRunState.Finished;
            status = _status = _status with { State = state, EndedUtc = _utcNow(), DecksAffected = decksAffected ?? _status.DecksAffected, StopReason = stopReason };
        }

        switch (status.State)
        {
            case HarvestMaintenanceRunState.Stopped:
                _logger.LogInformation("Harvest.Maintenance.Stopped operation={Operation} decksAffected={DecksAffected}", operation, status.DecksAffected);
                break;
            case HarvestMaintenanceRunState.Interrupted:
                _logger.LogWarning("Harvest.Maintenance.Interrupted operation={Operation} decksAffected={DecksAffected}", operation, status.DecksAffected);
                break;
            case HarvestMaintenanceRunState.Failed:
                _logger.LogError(exception, "Harvest.Maintenance.Failed operation={Operation}", operation);
                break;
            default:
                _logger.LogInformation("Harvest.Maintenance.Finished operation={Operation} decksAffected={DecksAffected} stopReason={StopReason}", operation, status.DecksAffected, stopReason);
                break;
        }
    }

    private void UpdateProgress(int decksAffected)
    {
        lock (_lock)
        {
            if (_status.IsRunning) _status = _status with { DecksAffected = decksAffected, LastProgressUtc = _utcNow() };
        }
    }
}
