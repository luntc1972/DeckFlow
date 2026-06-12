namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// Discriminator for harvest_runs.kind. <c>Bulk</c> rows are produced by the
/// Run-Now duration-capped sweep (HARV-01); <c>Url</c> rows are produced by the
/// single-Archidekt-URL sync import (HARV-02, D-09/D-10). Bound to the lowercase
/// strings <c>"bulk"</c> and <c>"url"</c> at the SQL CHECK-constraint boundary.
/// </summary>
public enum HarvestRunKind
{
    /// <summary>Bulk Archidekt cache sweep with a 15/30/60-minute duration cap.</summary>
    Bulk,

    /// <summary>Single-Archidekt-URL on-demand import (sync, latency 1-3s).</summary>
    Url
}

/// <summary>
/// State machine for harvest_runs.state. Non-terminal: <c>Queued</c>, <c>Running</c>,
/// <c>Stopping</c>. Terminal: <c>Succeeded</c>, <c>Interrupted</c>, <c>Failed</c>,
/// <c>Cancelled</c>. The startup reaper (D-02) flips every non-terminal row to
/// <c>Failed</c> with error_message <c>"interrupted by redeploy"</c> on first call
/// to <see cref="IHarvestRunStore.EnsureSchemaAsync"/>.
/// </summary>
public enum HarvestRunState
{
    /// <summary>Row inserted, work not yet started.</summary>
    Queued,

    /// <summary>Worker has picked up the row and is processing decks.</summary>
    Running,

    /// <summary>Operator-issued cancel landed; worker is winding down.</summary>
    Stopping,

    /// <summary>Run completed normally.</summary>
    Succeeded,

    /// <summary>Run cut short by a host restart/redeploy mid-run — not a failure.</summary>
    Interrupted,

    /// <summary>Run aborted on an exception, redeploy-orphan reap, or upstream error.</summary>
    Failed,

    /// <summary>Run terminated cleanly via operator cancel (HARV-03).</summary>
    Cancelled
}

/// <summary>
/// Wire format for a single harvest_runs row. Returned by
/// <see cref="IHarvestRunStore.GetActiveAsync"/> and <see cref="IHarvestRunStore.GetRecentAsync"/>.
/// All timestamps are UTC (<see cref="DateTimeOffset"/> with zero offset). Nullable
/// fields reflect the schema: <see cref="StartedUtc"/> is null until the row transitions
/// to <see cref="HarvestRunState.Running"/>; <see cref="CompletedUtc"/> is null until
/// the row transitions to a terminal state; <see cref="ErrorMessage"/> is populated only
/// on failure / reaper / cancel paths; <see cref="Url"/> is populated only when
/// <see cref="Kind"/> is <see cref="HarvestRunKind.Url"/> (D-10).
/// </summary>
/// <param name="Id">Server-generated UUID primary key.</param>
/// <param name="Kind">Discriminator — bulk vs URL.</param>
/// <param name="State">Current state-machine position.</param>
/// <param name="RequestedUtc">Wall-clock time the row was inserted.</param>
/// <param name="StartedUtc">Wall-clock time the worker entered <see cref="HarvestRunState.Running"/>.</param>
/// <param name="CompletedUtc">Wall-clock time the row reached a terminal state.</param>
/// <param name="DurationSeconds">Operator-selected duration cap (bulk) or 0 (URL).</param>
/// <param name="DecksProcessed">Decks fully imported during the run.</param>
/// <param name="AdditionalDecksFound">Newly-discovered deck IDs added to the queue (delta vs initial).</param>
/// <param name="ErrorMessage">Failure / cancel / reaper reason; null on success.</param>
/// <param name="Url">Source URL; null when <see cref="Kind"/> is <see cref="HarvestRunKind.Bulk"/>.</param>
public sealed record HarvestRunRow(
    Guid Id,
    HarvestRunKind Kind,
    HarvestRunState State,
    DateTimeOffset RequestedUtc,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? CompletedUtc,
    int DurationSeconds,
    int DecksProcessed,
    int AdditionalDecksFound,
    string? ErrorMessage,
    string? Url);

/// <summary>
/// Snapshot of the single-row <c>harvest_schedule</c> table (D-06). The seed row
/// (id=1) is created on first call to <see cref="IHarvestScheduleStore.EnsureSchemaAsync"/>
/// with <see cref="IntervalHours"/>=null (Off), <see cref="Paused"/>=false. Allowed
/// non-null interval values: 2, 4, 8, 24 (enforced by SQL CHECK constraint).
/// </summary>
/// <param name="IntervalHours">Cron interval in hours, or null for Off.</param>
/// <param name="Paused">When true, scheduler ticks short-circuit even when interval is set.</param>
/// <param name="UpdatedUtc">Wall-clock time of the most recent SaveAsync.</param>
public sealed record HarvestScheduleSnapshot(
    int? IntervalHours,
    bool Paused,
    DateTimeOffset UpdatedUtc);
