using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Services.Analytics;

/// <summary>
/// Hosted background service that drains <see cref="RequestMetricsBuffer"/> and persists
/// batches to <see cref="IRequestMetricsStore"/> (D-09, D-14). Flush is triggered by
/// whichever fires first: 100 records accumulated OR 5 seconds elapsed. The store is
/// resolved lazily per flush via <c>IServiceProvider.CreateScope()</c> to prevent
/// a circular DI dependency (D-14, Phase 7.1 errata — MS DI optional+default does NOT
/// break singleton cycles; use CreateScope instead).
/// </summary>
/// <remarks>
/// Registration (Wave 5 / Plan 08-03): register as both <c>Singleton</c> and
/// <c>IHostedService</c>, mirroring <c>ArchidektCacheJobService</c>.
/// </remarks>
public sealed class RequestMetricsFlusher : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DropLogInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ShutdownDrainCeiling = TimeSpan.FromSeconds(2);

    private readonly RequestMetricsBuffer _buffer;
    private readonly IServiceProvider _services;
    private readonly ILogger<RequestMetricsFlusher> _logger;

    private DateTimeOffset _lastDropLog = DateTimeOffset.MinValue;

    /// <summary>
    /// DI constructor. <see cref="IRequestMetricsStore"/> is NOT taken here — it is
    /// resolved lazily per flush via <c>IServiceProvider.CreateScope()</c> to
    /// avoid a circular singleton dependency (D-14).
    /// </summary>
    /// <param name="buffer">Singleton buffer that receives events from the request hot path.</param>
    /// <param name="services">Root service provider used to create a per-flush DI scope.</param>
    /// <param name="logger">Structured logger for flush / drop / failure events.</param>
    public RequestMetricsFlusher(
        RequestMetricsBuffer buffer,
        IServiceProvider services,
        ILogger<RequestMetricsFlusher> logger)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);
        _buffer = buffer;
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Main drain loop. Each tick awaits the buffer reader with a <see cref="FlushInterval"/>
    /// deadline so the loop wakes on whichever fires first: <see cref="BatchSize"/> records
    /// available OR 5 seconds elapsed (D-09). Per-tick try/catch ensures a transient store
    /// error never exits the loop (T-08-06).
    /// </summary>
    /// <param name="stoppingToken">Cancellation token signaled on host shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _buffer.Reader;
        var batch = new List<RequestMetricEvent>(BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Linked CTS: wake after FlushInterval even if no events arrive.
                using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                flushCts.CancelAfter(FlushInterval);

                try
                {
                    await reader.WaitToReadAsync(flushCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // The 5s timer fired — fall through to flush whatever is buffered.
                }

                // Drain up to BatchSize synchronously.
                while (batch.Count < BatchSize && reader.TryRead(out var evt))
                {
                    batch.Add(evt);
                }

                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch, stoppingToken).ConfigureAwait(false);
                    batch.Clear();
                }

                MaybeLogDrops();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Analytics.Flusher.TickFailure flusher tick threw; loop continues. error={Message}",
                    ex.Message);
                // Do not exit the loop — the next tick will retry.
            }
        }
    }

    /// <summary>
    /// Best-effort shutdown drain. Attempts to flush all remaining buffered events within
    /// a <see cref="ShutdownDrainCeiling"/> (2 s) before calling
    /// <see cref="BackgroundService.StopAsync"/>. Errors are logged as WARN and do not
    /// propagate — shutdown must not throw (Claude's discretion per CONTEXT.md).
    /// </summary>
    /// <param name="cancellationToken">Host shutdown cancellation token.</param>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(ShutdownDrainCeiling);
        var batch = new List<RequestMetricEvent>(BatchSize);

        try
        {
            while (_buffer.Reader.TryRead(out var evt))
            {
                batch.Add(evt);
                if (batch.Count >= BatchSize)
                {
                    try
                    {
                        await FlushBatchAsync(batch, deadline.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Analytics.Flusher.ShutdownDrainAborted some events not persisted.");
                    }

                    batch.Clear();
                }
            }

            // Residual partial batch (<BatchSize). Without this, anything queued at shutdown
            // that didn't reach a full 100-event chunk would be silently dropped on the floor.
            if (batch.Count > 0)
            {
                try
                {
                    await FlushBatchAsync(batch, deadline.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Analytics.Flusher.ShutdownDrainAborted residual partial batch not persisted.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Analytics.Flusher.ShutdownDrainAborted drain loop threw; some events not persisted.");
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Persists <paramref name="batch"/> via a short-lived DI scope (D-14).
    /// The scope and the <see cref="IRequestMetricsStore"/> it contains are disposed
    /// after each flush so connection lifetimes stay bounded.
    /// </summary>
    /// <param name="batch">Events to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task FlushBatchAsync(List<RequestMetricEvent> batch, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IRequestMetricsStore>();
        await store.UpsertBatchAsync(batch, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Emits one Serilog WARN per <see cref="DropLogInterval"/> (~60 s) when the buffer
    /// has dropped events since the last check (D-10, T-08-10). Never logs per-drop.
    /// </summary>
    private void MaybeLogDrops()
    {
        if (DateTimeOffset.UtcNow - _lastDropLog < DropLogInterval)
        {
            return;
        }

        var dropped = _buffer.ConsumeDropCount();
        if (dropped > 0)
        {
            _logger.LogWarning(
                "Analytics.Buffer.Drops dropped={Dropped} interval={Interval}s",
                dropped, DropLogInterval.TotalSeconds);
        }

        _lastDropLog = DateTimeOffset.UtcNow;
    }
}
