using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Runs maintenance in chunks while allowing other sweep work between chunks.</summary>
internal static class HarvestMaintenanceChunkLoop
{
    internal static async Task<HarvestMaintenanceOutcome> RunAsync(HarvestMaintenanceOperation operation, SemaphoreSlim gate, int chunkDecks, Func<int, IProgress<DeckPruningProgress>, Func<bool>?, CancellationToken, Task<DeckPruningResult>> runChunk, IProgress<DeckPruningProgress>? progress, ILogger logger, Func<bool>? stopBeforeBatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkDecks, 1);
        ArgumentNullException.ThrowIfNull(runChunk);
        ArgumentNullException.ThrowIfNull(logger);

        var decksAffected = 0;
        var batches = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            DeckPruningResult result;
            try
            {
                // Why: reports run inside the sweep gate; consumers must be synchronous and non-blocking.
                var adapter = new BatchProgress(progress, decksAffected, batches);
                result = await runChunk(chunkDecks, adapter, stopBeforeBatch, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }

            decksAffected += result.DecksRemoved;
            batches += result.Batches;
            if (result.StopReason != DeckPruningStopReason.MaxDecksReached)
            {
                return new HarvestMaintenanceOutcome(operation, decksAffected, batches, result.StopReason);
            }

            if (result.DecksRemoved == 0)
            {
                logger.LogWarning("Harvest.Maintenance.NoProgress operation={Operation}", operation);
                return new HarvestMaintenanceOutcome(operation, decksAffected, batches, DeckPruningStopReason.MaxDecksReached);
            }
        }
    }

    private sealed class BatchProgress(IProgress<DeckPruningProgress>? progress, int decksAffected, int batches) : IProgress<DeckPruningProgress>
    {
        public void Report(DeckPruningProgress value) => progress?.Report(new(decksAffected + value.DecksAffected, batches + value.Batches));
    }

}
