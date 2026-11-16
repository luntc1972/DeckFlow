using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeckFlow.Web.Tests.Services;

public sealed class HarvestMaintenanceChunkLoopTests
{
    [Fact]
    public async Task RunAsync_BatchReports_AreCumulativeAcrossChunksAndInsideGate()
    {
        using var gate = new SemaphoreSlim(1, 1);
        var entered = new List<int>();
        var reported = new List<int>();
        var calls = 0;
        var values = new List<DeckPruningProgress>();
        var progress = new RecordingProgress(reported, gate, values);

        var outcome = await HarvestMaintenanceChunkLoop.RunAsync(HarvestMaintenanceOperation.Drain, gate, 3, (chunkDecks, batchProgress, stopBeforeBatch, cancellationToken) =>
        {
            entered.Add(gate.CurrentCount);
            batchProgress.Report(new(1, 1));
            if (calls == 0) batchProgress.Report(new(3, 2));
            return Task.FromResult(++calls == 1
                ? new DeckPruningResult(3, 2, DeckPruningStopReason.MaxDecksReached)
                : new DeckPruningResult(1, 1, DeckPruningStopReason.Completed));
        }, progress, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, null, default);

        Assert.Equal([0, 0], entered);
        Assert.Equal([0, 0, 0], reported);
        Assert.Equal([new DeckPruningProgress(1, 1), new(3, 2), new(4, 3)], values);
        Assert.Equal(HarvestMaintenanceOperation.Drain, outcome.Operation);
        Assert.Equal(4, outcome.DecksAffected);
        Assert.Equal(3, outcome.Batches);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
    }

    [Fact]
    public async Task RunAsync_ChunkThrows_ReleasesGateAndRethrows()
    {
        using var gate = new SemaphoreSlim(1, 1);
        var progress = new List<DeckPruningProgress>();
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => HarvestMaintenanceChunkLoop.RunAsync(HarvestMaintenanceOperation.Drain, gate, 3, (chunkDecks, batchProgress, stopBeforeBatch, cancellationToken) =>
            ++calls == 1
                ? Task.FromResult(new DeckPruningResult(3, 2, DeckPruningStopReason.MaxDecksReached))
                : throw new InvalidOperationException(), new BatchProgress(progress.Add), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, null, default));

        Assert.Equal(1, gate.CurrentCount);
    }

    [Fact]
    public async Task RunAsync_CancelledInsideChunk_ReleasesGate()
    {
        using var gate = new SemaphoreSlim(1, 1);
        using var cancelled = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HarvestMaintenanceChunkLoop.RunAsync(HarvestMaintenanceOperation.Drain, gate, 3, (chunkDecks, batchProgress, stopBeforeBatch, cancellationToken) =>
        {
            cancelled.Cancel();
            return Task.FromCanceled<DeckPruningResult>(cancelled.Token);
        }, null, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, null, cancelled.Token));

        Assert.Equal(1, gate.CurrentCount);
    }

    [Fact]
    public async Task RunAsync_CancelledWhileWaitingForGate_DoesNotRelease()
    {
        using var gate = new SemaphoreSlim(1, 1);
        using var cancelled = new CancellationTokenSource();
        gate.Wait();

        var run = HarvestMaintenanceChunkLoop.RunAsync(HarvestMaintenanceOperation.Drain, gate, 3, (chunkDecks, batchProgress, stopBeforeBatch, cancellationToken) => Task.FromResult(new DeckPruningResult(0, 0, DeckPruningStopReason.Completed)), null, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, null, cancelled.Token);
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(0, gate.CurrentCount);
        gate.Release();
        Assert.Equal(1, gate.CurrentCount);
    }

    [Fact]
    public async Task RunAsync_MaxDecksReachedWithNoRemovals_StopsAfterOneChunk()
    {
        using var gate = new SemaphoreSlim(1, 1);
        var calls = 0;

        var logger = new RecordingLogger();
        var outcome = await HarvestMaintenanceChunkLoop.RunAsync(HarvestMaintenanceOperation.Drain, gate, 3, (chunkDecks, batchProgress, stopBeforeBatch, cancellationToken) =>
        {
            calls++;
            return Task.FromResult(new DeckPruningResult(0, 1, DeckPruningStopReason.MaxDecksReached));
        }, null, logger, null, default);

        Assert.Equal(1, calls);
        Assert.Equal(0, outcome.DecksAffected);
        Assert.Equal(DeckPruningStopReason.MaxDecksReached, outcome.StopReason);
        Assert.Equal(LogLevel.Warning, logger.Level);
        Assert.Equal("Harvest.Maintenance.NoProgress operation=Drain", logger.Message);
    }

    [Fact]
    public async Task RunAsync_DiskProbeUnavailableChunk_EndsLoopWithThatReason()
    {
        using var gate = new SemaphoreSlim(1, 1);
        var calls = 0;
        var progress = new List<DeckPruningProgress>();

        var outcome = await HarvestMaintenanceChunkLoop.RunAsync(HarvestMaintenanceOperation.Drain, gate, 3, (chunkDecks, batchProgress, stopBeforeBatch, cancellationToken) =>
        {
            calls++;
            batchProgress.Report(new(3, 2));
            return Task.FromResult(new DeckPruningResult(3, 2, DeckPruningStopReason.DiskProbeUnavailable));
        }, new BatchProgress(progress.Add), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, null, default);

        Assert.Equal(1, calls);
        Assert.Equal(3, outcome.DecksAffected);
        Assert.Equal(2, outcome.Batches);
        Assert.Equal(DeckPruningStopReason.DiskProbeUnavailable, outcome.StopReason);
        Assert.Equal([new DeckPruningProgress(3, 2)], progress);
        Assert.Equal(1, gate.CurrentCount);
    }

    private sealed class RecordingProgress(List<int> values, SemaphoreSlim gate, List<DeckPruningProgress> reports) : IProgress<DeckPruningProgress>
    {
        public void Report(DeckPruningProgress value)
        {
            values.Add(gate.CurrentCount);
            reports.Add(value);
        }
    }

    private sealed class BatchProgress(Action<DeckPruningProgress> report) : IProgress<DeckPruningProgress>
    {
        public void Report(DeckPruningProgress value) => report(value);
    }

    private sealed class RecordingLogger : ILogger
    {
        public LogLevel Level { get; private set; }
        public string? Message { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Level = level;
            Message = formatter(state, exception);
        }
    }
}
