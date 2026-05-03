using System.Threading;
using System.Threading.Tasks;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// In-memory cache of the <c>harvest_schedule</c> single-row state (Phase 7, D-06 / D-07).
/// Mirrors <see cref="DeckFlow.Web.Services.FeatureFlags.IFeatureFlagCache"/>: lock-free
/// reads, hot-reloaded on admin write (Plan 04 calls <see cref="ReloadAsync"/> after a
/// schedule UPSERT), and refreshed by a 30-second BackgroundService poller as a backstop.
/// Snapshot is always non-null; the default before first reload is Off / unpaused.
/// </summary>
public interface IHarvestScheduleCache
{
    /// <summary>
    /// Returns the current snapshot. Always non-null — callers receive
    /// <c>(IntervalHours: null, Paused: false, UpdatedUtc: DateTimeOffset.MinValue)</c>
    /// before the first reload completes, never <c>null</c>.
    /// </summary>
    /// <returns>The current in-memory schedule snapshot.</returns>
    HarvestScheduleSnapshot Snapshot();

    /// <summary>
    /// Forces a synchronous re-read of the <c>harvest_schedule</c> row (D-07). Called by
    /// <c>AdminHarvestController</c> after every schedule write so the new value is visible
    /// to the next scheduler tick without waiting for the 30s poller. Failures preserve
    /// the prior snapshot — never replace a good snapshot with a stub on transient PG
    /// failure (S-7 mirror).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token; if signaled, the reload is aborted.</param>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
