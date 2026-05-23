namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// Persistence contract for the <c>harvest_runs</c> table (Phase 7, HARV-07).
/// Implementations bootstrap schema + indexes lazily on first call and run the
/// D-02 startup reaper (UPDATE non-terminal rows to <c>Failed</c>) inside the
/// same gate so orphaned redeploy rows are reconciled before any HTTP request
/// lands. Single source of truth for harvest job state — replaces the prior
/// in-memory <c>ConcurrentDictionary</c> in <c>ArchidektCacheJobService</c>.
/// </summary>
public interface IHarvestRunStore
{
    /// <summary>
    /// Idempotent. On first call: creates the <c>harvest_runs</c> table and indexes,
    /// then runs the D-02 startup reaper (UPDATE non-terminal rows to
    /// <see cref="HarvestRunState.Failed"/> with error_message
    /// <c>"interrupted by redeploy"</c>). Subsequent calls return immediately on the
    /// lock-free <c>_schemaReady</c> fast-path.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the bootstrap.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new <c>harvest_runs</c> row in <see cref="HarvestRunState.Queued"/>
    /// and returns the generated UUID. <paramref name="url"/> is null for
    /// <see cref="HarvestRunKind.Bulk"/>, populated for
    /// <see cref="HarvestRunKind.Url"/> (D-10). Implementations MUST call
    /// <c>_stats?.Invalidate()</c> after the write succeeds (D-13).
    /// </summary>
    /// <param name="kind">Bulk vs URL discriminator.</param>
    /// <param name="durationSeconds">Operator-selected cap (bulk) or 0 (URL).</param>
    /// <param name="url">Source URL for URL-kind runs; null for bulk runs.</param>
    /// <param name="now">Wall-clock time stamped into <c>requested_utc</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>Server-generated UUID primary key for the new row.</returns>
    Task<Guid> InsertQueuedAsync(
        HarvestRunKind kind,
        int durationSeconds,
        string? url,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing run row to the new state. Pass <paramref name="startedUtc"/>
    /// only when transitioning to <see cref="HarvestRunState.Running"/>; pass
    /// <paramref name="completedUtc"/> only when transitioning to a terminal state.
    /// Null timestamp parameters preserve the existing column value via SQL COALESCE.
    /// Implementations MUST call <c>_stats?.Invalidate()</c> after the write
    /// succeeds (D-13).
    /// </summary>
    /// <param name="id">UUID primary key of the row to update.</param>
    /// <param name="state">New state to write.</param>
    /// <param name="startedUtc">Time the worker began processing; null preserves the existing value.</param>
    /// <param name="completedUtc">Terminal-state time; null preserves the existing value.</param>
    /// <param name="decksProcessed">Decks imported so far during the run.</param>
    /// <param name="additionalDecksFound">Newly-discovered deck IDs added to the queue.</param>
    /// <param name="errorMessage">Failure / cancel / reaper reason; null clears.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task UpdateStateAsync(
        Guid id,
        HarvestRunState state,
        DateTimeOffset? startedUtc,
        DateTimeOffset? completedUtc,
        int decksProcessed,
        int additionalDecksFound,
        string? errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates ONLY <c>decks_processed</c> + <c>additional_decks_found</c> on an
    /// existing <c>harvest_runs</c> row. Does NOT touch <c>state</c>,
    /// <c>started_utc</c>, <c>completed_utc</c>, or <c>error_message</c>. Used by
    /// the background harvest worker to surface incremental progress to the AJAX
    /// status endpoint without disturbing the state machine. Implementations MUST
    /// call <c>_stats?.Invalidate()</c> after the write succeeds.
    /// </summary>
    /// <param name="id">UUID primary key of the row to update.</param>
    /// <param name="decksProcessed">Decks imported so far during the run.</param>
    /// <param name="additionalDecksFound">Newly-discovered deck IDs added to the queue.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task UpdateProgressAsync(
        Guid id,
        int decksProcessed,
        int additionalDecksFound,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent non-terminal row (<c>state IN (Queued, Running, Stopping)</c>)
    /// or null when no active row exists. Used by EnqueueAsync dedup check (D-01) and the
    /// AJAX status poll (D-08).
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the run row with the matching <paramref name="id"/>, or null when no
    /// such row exists. Unlike <see cref="GetActiveAsync"/>, this returns terminal-state
    /// rows (Succeeded / Failed / Cancelled) so callers can re-fetch the row after the
    /// background worker has cleared it from the active set. Used by
    /// <c>ArchidektCacheJobService.GetJob(Guid)</c> so completed jobs remain
    /// queryable by id from admin/API surfaces.
    /// </summary>
    /// <param name="id">UUID primary key of the row to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    Task<HarvestRunRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent <paramref name="n"/> rows ordered by
    /// <c>started_utc DESC NULLS LAST</c> (D-16 #5). Powers the recent-runs panel.
    /// </summary>
    /// <param name="n">Maximum number of rows to return.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a token derived from <c>MAX(started_utc)</c>, <c>MAX(completed_utc)</c>,
    /// and <c>COUNT(1)</c> over <c>harvest_runs</c>. Powers the AJAX poller's
    /// revision-change auto-reload (B2). Cheap single-statement read; safe at
    /// sub-second cadence.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    Task<string> GetRecentRevisionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>MAX(completed_utc) FROM harvest_runs WHERE state='Succeeded'</c>
    /// (D-16 #7), or null when no successful run has ever completed. Single source
    /// of truth — both the stats aggregator and the schedule tick service MUST
    /// call this method (W5).
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>COUNT(1) FROM harvest_runs WHERE state='Succeeded'</c> — the
    /// lifetime total of successful bulk + URL runs surfaced in the stats panel.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    Task<long> GetTotalSucceededCountAsync(CancellationToken cancellationToken = default);
}
