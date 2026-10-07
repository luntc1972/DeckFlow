namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// Persistence contract for the single-row <c>harvest_schedule</c> table (Phase 7,
/// D-06 / HARV-05). Implementations bootstrap schema and seed the id=1 row with
/// <c>interval_hours=NULL, paused=FALSE, updated_utc=now()</c> via
/// <c>ON CONFLICT (id) DO NOTHING</c> so a fresh DB always has the row in place
/// (eliminates a null-row branch on every page render).
/// </summary>
public interface IHarvestScheduleStore
{
    /// <summary>
    /// Idempotent. Creates the <c>harvest_schedule</c> table and seeds the id=1
    /// row (interval_hours=NULL, paused=FALSE, updated_utc=now()) via
    /// <c>ON CONFLICT (id) DO NOTHING</c> (D-06, planner discretion #6).
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the bootstrap.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the single-row snapshot. Always returns a snapshot — the schema seed
    /// guarantees the id=1 row exists. Implementations throw
    /// <see cref="InvalidOperationException"/> defensively if the row is missing.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    Task<HarvestScheduleSnapshot> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// UPSERT of the id=1 row with new <paramref name="intervalHours"/> /
    /// <paramref name="paused"/> / <paramref name="now"/> (D-07). Allowed
    /// <paramref name="intervalHours"/> values: null (Off), 2, 4, 8, 24 — the
    /// SQL CHECK constraint enforces the whitelist as a second line of defense.
    /// </summary>
    /// <param name="intervalHours">Cron interval in hours, or null for Off.</param>
    /// <param name="paused">Whether scheduler ticks should short-circuit.</param>
    /// <param name="now">Wall-clock time stamped into <c>updated_utc</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task SaveAsync(int? intervalHours, bool paused, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Saves the interval without changing the existing pause state.</summary>
    /// <param name="intervalHours">Interval in hours, or null to disable scheduled runs.</param>
    /// <param name="now">Wall-clock time stamped into <c>updated_utc</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task SaveIntervalAsync(int? intervalHours, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Sets the pause state without changing the existing interval.</summary>
    /// <param name="paused">Whether scheduler ticks should short-circuit.</param>
    /// <param name="now">Wall-clock time stamped into <c>updated_utc</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task SetPausedAsync(bool paused, DateTimeOffset now, CancellationToken cancellationToken = default);
}
