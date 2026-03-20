using Microsoft.Extensions.Logging;

namespace DeckSyncWorkbench.Web.Services;

/// <summary>
/// Provides a simple way to trigger background sweeps for the Archidekt cache.
/// </summary>
public static class CategoryHarvestScheduler
{
    /// <summary>
    /// Starts an asynchronous cache sweep without awaiting it.
    /// </summary>
    /// <param name="store">Knowledge store used for the sweep.</param>
    /// <param name="logger">Logger for reporting failures.</param>
    /// <param name="durationSeconds">Duration of the sweep in seconds.</param>
    public static void ScheduleSweep(ICategoryKnowledgeStore store, ILogger logger, int durationSeconds)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await store.RunCacheSweepAsync(logger, durationSeconds);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Extended Archidekt harvest failed.");
            }
        });
    }
}
