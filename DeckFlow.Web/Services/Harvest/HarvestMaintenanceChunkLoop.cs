using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Runs maintenance in chunks while allowing other sweep work between chunks.</summary>
internal static class HarvestMaintenanceChunkLoop
{
    internal static async Task<HarvestMaintenanceOutcome> RunAsync(SemaphoreSlim gate, int chunkDecks, Func<CancellationToken, Task<DeckPruningResult>> runChunkAsync, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkDecks, 1);
        ArgumentNullException.ThrowIfNull(runChunkAsync);

        var decksAffected = 0;
        var batches = 0;
        while (true)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            DeckPruningResult result;
            try
            {
                result = await runChunkAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }

            decksAffected += result.DecksRemoved;
            batches += result.Batches;
            progress?.Report(decksAffected);
            if (result.StopReason is DeckPruningStopReason.Completed or DeckPruningStopReason.DiskFloor or DeckPruningStopReason.DiskProbeUnavailable || result.DecksRemoved == 0)
            {
                return new HarvestMaintenanceOutcome(decksAffected, batches, result.StopReason);
            }
        }
    }
}
