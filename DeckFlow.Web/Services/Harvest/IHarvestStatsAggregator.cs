using System.Threading;
using System.Threading.Tasks;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// HARV-06 stats panel data source. The deck, commander, queue, observation and database-size counts are cached for 60 seconds under
/// <c>admin.harvest.stats.v1</c> and served stale while one background refresh runs. Recent runs, last scheduled successes, next scheduled runs and health signals are read on every call.
/// </summary>
public interface IHarvestStatsAggregator
{
    /// <summary>Returns cached (or cold-start-computed) counts combined with run state and next-run times read on every call.</summary>
    Task<HarvestStatsPayload> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// D-13 / B1: marks cached counts stale, including counts of a refresh already in progress, so the next GetAsync refreshes them in the background; run state is read on every call and never waits for that refresh.
    /// Called by IHarvestRunStore write methods after successful state changes.
    /// </summary>
    void Invalidate();
}
