using System.Threading;
using System.Threading.Tasks;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// HARV-06 stats panel data source. Cached for 60 seconds under
/// <c>admin.harvest.stats.v1</c> with an explicit invalidation hook (D-13).
/// </summary>
public interface IHarvestStatsAggregator
{
    /// <summary>Returns the cached or freshly-computed harvest stats payload.</summary>
    Task<HarvestStatsPayload> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// D-13 / B1: drops the cached payload so the next GetAsync rebuilds from SQL.
    /// Called by IHarvestRunStore write methods after successful state changes.
    /// </summary>
    void Invalidate();
}
