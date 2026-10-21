using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeckFlow.Web.Tests.Services;

public sealed class HarvestMaintenanceChunkLoopTests
{
    [Fact]
    public async Task RunAsync_ReleasesGateBeforeReportingEachChunk()
    {
        using var gate = new SemaphoreSlim(1, 1);
        var entered = new List<int>();
        var reported = new List<int>();
        var calls = 0;
        var progress = new RecordingProgress(reported, gate);

        var outcome = await HarvestMaintenanceChunkLoop.RunAsync(HarvestMaintenanceOperation.Drain, gate, 3, (chunkDecks, cancellationToken) =>
        {
            entered.Add(gate.CurrentCount);
            return Task.FromResult(++calls == 1
                ? new DeckPruningResult(3, 2, DeckPruningStopReason.MaxDecksReached)
                : new DeckPruningResult(1, 1, DeckPruningStopReason.Completed));
        }, progress, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, default);

        Assert.Equal([0, 0], entered);
        Assert.Equal([1, 1], reported);
        Assert.Equal(HarvestMaintenanceOperation.Drain, outcome.Operation);
        Assert.Equal(4, outcome.DecksAffected);
        Assert.Equal(3, outcome.Batches);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
    }

    [Fact]
    public async Task RunAsync_ChunkThrows_ReleasesGateAndRethrows()
    {
        using var gate = new SemaphoreSlim(1, 1);
        var progress = new List<int>();
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => HarvestMaintenanceChunkLoop.RunAsync(HarvestMaintenanceOperation.Drain, gate, 3, (chunkDecks, cancellationToken) =>
            ++calls == 1
                ? Task.FromResult(new DeckPruningResult(3, 2, DeckPruningStopReason.MaxDecksReached))
                : throw new InvalidOperationException(), new Progress<int>(progress.Add), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, default));

        Assert.Equal(1, gate.CurrentCount);
    }

    [Fact]
    public async Task RunAsync_CancelledInsideChunk_ReleasesGate()
    {
        using var gate = new SemaphoreSlim(1, 1);
        using var cancelled = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HarvestMaintenanceChunkLoop.RunAsync(HarvestMaintenanceOperation.Drain, gate, 3, (chunkDecks, cancellationToken) =>
        {
            cancelled.Cancel();
            return Task.FromCanceled<DeckPruningResult>(cancelled.Token);
        }, null, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, cancelled.Token));

        Assert.Equal(1, gate.CurrentCount);
    }

    [Fact]
    public async Task RunAsync_CancelledWhileWaitingForGate_DoesNotRelease()
    {
        using var gate = new SemaphoreSlim(1, 1);
        using var cancelled = new CancellationTokenSource();
        gate.Wait();

        var run = HarvestMaintenanceChunkLoop.RunAsync(HarvestMaintenanceOperation.Drain, gate, 3, (chunkDecks, cancellationToken) => Task.FromResult(new DeckPruningResult(0, 0, DeckPruningStopReason.Completed)), null, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, cancelled.Token);
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
        var outcome = await HarvestMaintenanceChunkLoop.RunAsync(HarvestMaintenanceOperation.Drain, gate, 3, (chunkDecks, cancellationToken) =>
        {
            calls++;
            return Task.FromResult(new DeckPruningResult(0, 1, DeckPruningStopReason.MaxDecksReached));
        }, null, logger, default);

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
        var progress = new List<int>();

        var outcome = await HarvestMaintenanceChunkLoop.RunAsync(HarvestMaintenanceOperation.Drain, gate, 3, (chunkDecks, cancellationToken) =>
        {
            calls++;
            return Task.FromResult(new DeckPruningResult(3, 2, DeckPruningStopReason.DiskProbeUnavailable));
        }, new Progress<int>(progress.Add), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, default);

        Assert.Equal(1, calls);
        Assert.Equal(3, outcome.DecksAffected);
        Assert.Equal(2, outcome.Batches);
        Assert.Equal(DeckPruningStopReason.DiskProbeUnavailable, outcome.StopReason);
        Assert.Equal([3], progress);
        Assert.Equal(1, gate.CurrentCount);
    }

    private sealed class RecordingProgress(List<int> values, SemaphoreSlim gate) : IProgress<int>
    {
        public void Report(int value)
        {
            values.Add(gate.CurrentCount);
        }
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
