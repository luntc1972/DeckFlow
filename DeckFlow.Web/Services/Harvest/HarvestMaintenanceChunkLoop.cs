using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Runs maintenance in chunks while allowing other sweep work between chunks.</summary>
internal static class HarvestMaintenanceChunkLoop
{
    internal static async Task<HarvestMaintenanceOutcome> RunAsync(HarvestMaintenanceOperation operation, SemaphoreSlim gate, int chunkDecks, Func<int, CancellationToken, Task<DeckPruningResult>> runChunk, IProgress<int>? progress, ILogger logger, CancellationToken cancellationToken)
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
                result = await runChunk(chunkDecks, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }

            decksAffected += result.DecksRemoved;
            batches += result.Batches;
            // Why: progress runs after Release so a slow consumer never extends the hold and the between-chunk window is where harvest gets the gate.
            progress?.Report(decksAffected);
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
}
