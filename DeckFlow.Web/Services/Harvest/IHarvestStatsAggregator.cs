using System.Threading;
using System.Threading.Tasks;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// HARV-06 stats panel data source. Cached for 60 seconds under
/// <c>admin.harvest.stats.v1</c>; stale payloads are returned while one refresh runs.
/// </summary>
public interface IHarvestStatsAggregator
{
    /// <summary>Returns fresh, stale-while-refreshing, or cold-start-computed harvest stats.</summary>
    Task<HarvestStatsPayload> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// D-13 / B1: marks the cached payload stale so the next GetAsync returns it and refreshes from SQL.
    /// Called by IHarvestRunStore write methods after successful state changes.
    /// </summary>
    void Invalidate();
}
