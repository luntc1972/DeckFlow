using DeckFlow.Core.Content;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Local, durable discrepancy store for the SYNC-11 reconciler (D-05: persistent state lives in the
/// operator's local <c>content-kb.db</c>, never a new prod table). Every dry-run (and, later, the
/// re-validated Apply pass) persists what it saw via <see cref="PersistRunAsync"/>: previously-open
/// discrepancies still present are refreshed (idempotent — zero duplicate rows on re-run), and
/// previously-open discrepancies now ABSENT are marked resolved (row retained, never deleted).
/// Scope-tagged so a partial/scoped run can never resolve discrepancies outside what it examined.
/// </summary>
public interface IContentKbReconcileStore
{
    /// <summary>
    /// Ensures the <c>content_kb_reconcile_discrepancy</c> schema exists. Idempotent; safe to call
    /// repeatedly (mirrors <see cref="IContentHarvestRunStore.EnsureSchemaAsync"/>).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists one reconcile pass in a single logical operation: (1) upserts every discrepancy in
    /// <paramref name="seen"/> — a new discrepancy is inserted with <c>first_seen_utc</c> and
    /// <c>last_seen_utc</c> set to <paramref name="now"/>; a discrepancy already stored has its
    /// <c>last_seen_utc</c> refreshed and <c>resolved_utc</c> cleared (it is open again) — and then
    /// (2) marks resolved (sets <c>resolved_utc = now</c>, never deletes) every OPEN discrepancy
    /// tagged with <paramref name="scopeTag"/> that is NOT present in <paramref name="seen"/>. Both
    /// steps are scoped to <paramref name="scopeTag"/>; a discrepancy tagged with a different scope
    /// is never touched by this call.
    /// </summary>
    /// <param name="scopeTag">
    /// Identifies what this run examined (e.g. <c>"full"</c> for a whole-catalog run, or a
    /// source-scoped tag for a partial run). Resolution-by-absence is filtered to this scope only.
    /// </param>
    /// <param name="seen">Every discrepancy this run detected (may be empty — an empty run resolves the whole scope).</param>
    /// <param name="now">The timestamp to stamp on new/refreshed/resolved rows.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PersistRunAsync(
        string scopeTag,
        IReadOnlyList<ContentKbReconcileDiscrepancy> seen,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every OPEN (<c>resolved_utc IS NULL</c>) stored discrepancy, optionally filtered to
    /// one scope tag.
    /// </summary>
    /// <param name="scopeTag">When non-null, restricts results to this scope tag; when null, returns open discrepancies across all scopes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<StoredReconcileDiscrepancy>> GetOpenAsync(
        string? scopeTag,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One persisted discrepancy row, as read back by <see cref="IContentKbReconcileStore.GetOpenAsync"/>.
/// Exposes <see cref="Kind"/> (mapped from the persisted <c>kind</c> TEXT column back to
/// <see cref="ContentKbReconcileKind"/>) so downstream consumers — most notably the 91-08
/// removal-scoped Apply — can filter the open set to the seed-drift class without re-parsing text.
/// </summary>
/// <param name="Id">The deterministic discrepancy ID (see <see cref="ContentKbReconcileDiscrepancy.BuildId"/>).</param>
/// <param name="Kind">Which of the four discrepancy classes this is.</param>
/// <param name="NaturalKeyType">Natural-key type for row-keyed kinds; <see langword="null"/> for file-orphan.</param>
/// <param name="NaturalKeyValue">Natural-key value for row-keyed kinds; <see langword="null"/> for file-orphan.</param>
/// <param name="ArtifactPath">The content-kb-relative artifact path, when known.</param>
/// <param name="Title">The row's title, when known.</param>
/// <param name="ScopeTag">The scope tag of the run that most recently saw this discrepancy.</param>
/// <param name="FirstSeenUtc">When this discrepancy was first persisted.</param>
/// <param name="LastSeenUtc">When this discrepancy was most recently confirmed present.</param>
/// <param name="ResolvedUtc">When this discrepancy was marked resolved, or <see langword="null"/> while open.</param>
public sealed record StoredReconcileDiscrepancy(
    string Id,
    ContentKbReconcileKind Kind,
    string? NaturalKeyType,
    string? NaturalKeyValue,
    string? ArtifactPath,
    string? Title,
    string ScopeTag,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    DateTimeOffset? ResolvedUtc);
