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
    private readonly ILogger<HarvestMaintenanceRunner> _logger;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Channel<HarvestMaintenanceOperation> _queue = Channel.CreateUnbounded<HarvestMaintenanceOperation>();
    private HarvestMaintenanceStatus _status = HarvestMaintenanceStatus.Idle;
    private CancellationTokenSource? _operationCts;

    /// <summary>Initializes a maintenance runner using UTC time.</summary>
    public HarvestMaintenanceRunner(ICategoryKnowledgeStore store, ILogger<HarvestMaintenanceRunner> logger)
        : this(store, logger, () => DateTimeOffset.UtcNow)
    {
    }

    internal HarvestMaintenanceRunner(ICategoryKnowledgeStore store, ILogger<HarvestMaintenanceRunner> logger, Func<DateTimeOffset> utcNow)
    {
        _store = store;
        _logger = logger;
        _utcNow = utcNow;
    }

    /// <inheritdoc/>
    public bool TryStart(HarvestMaintenanceOperation operation)
    {
        lock (_lock)
        {
            if (_status.IsRunning) return false;

            _operationCts?.Dispose();
            _operationCts = new CancellationTokenSource();
            _status = new HarvestMaintenanceStatus(true, operation, _utcNow(), null, 0, null, false, false);
            _queue.Writer.TryWrite(operation);
        }

        _logger.LogInformation("Harvest.Maintenance.Started operation={Operation}", operation);
        return true;
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
                var outcome = await _store.RunMaintenanceAsync(operation, new Progress<int>(UpdateProgress), linkedCts.Token).ConfigureAwait(false);
                Complete(outcome.DecksAffected, outcome.StopReason, failed: false);
                _logger.LogInformation("Harvest.Maintenance.Finished operation={Operation} decksAffected={DecksAffected} stopReason={StopReason}", operation, outcome.DecksAffected, outcome.StopReason);
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
                Complete(GetStatus().DecksAffected, stopReason: null, failed: false);
            }
            catch (Exception exception)
            {
                Complete(GetStatus().DecksAffected, stopReason: null, failed: true);
                _logger.LogError(exception, "Harvest.Maintenance.Failed operation={Operation}", operation);
            }
        }
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
        RequestStop();
        return base.StopAsync(cancellationToken);
    }

    private void Complete(int decksAffected, DeckPruningStopReason? stopReason, bool failed)
    {
        lock (_lock)
        {
            _status = _status with { IsRunning = false, CompletedUtc = _utcNow(), DecksAffected = decksAffected, StopReason = stopReason, Failed = failed };
        }
    }

    private void UpdateProgress(int decksAffected)
    {
        lock (_lock)
        {
            _status = _status with { DecksAffected = decksAffected };
        }
    }
}
