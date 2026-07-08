using DeckFlow.Core.Knowledge;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Reads all production <c>content_site_index</c> rows for the pull-from-prod reconcile flow.
/// This contract is deliberately read-only: it exposes a single SELECT method and no upsert,
/// delete, set, or schema-ensure operation, so the production side is structurally incapable of
/// being written through it (R1/R2). The implementation runs a plain <c>SELECT</c> only — it never
/// runs <c>CREATE</c>/<c>ALTER</c> DDL or any schema-ensure against prod.
/// </summary>
public interface IProdContentReader
{
    /// <summary>
    /// Reads every row from the production <c>content_site_index</c> via a single plain SELECT.
    /// Runs NO schema-ensure / DDL — if the table is absent the SELECT fails and the caller surfaces
    /// a sanitized error; this method never attempts to create or alter anything in production.
    /// </summary>
    /// <param name="connectionString">Raw prod Postgres connection string (URL or key-value form).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All production content index rows.</returns>
    Task<IReadOnlyList<ContentSiteIndexRow>> ReadAllAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a single feature-flag's enabled state from the production <c>feature_flags</c> table via
    /// ONE plain SELECT (D-04) — structurally read-only, no DDL/write/schema-ensure, mirroring
    /// <see cref="ReadAllAsync"/>'s connection setup exactly. Real-implemented ONLY on
    /// <see cref="ProdContentReader"/>; declared here as a THROWING default interface method (the
    /// 89-02 / 90-03 throwing-escape-hatch idiom) so the existing hand-written
    /// <c>FakeProdContentReader</c> test double keeps compiling unchanged (no CS0535).
    /// </summary>
    /// <param name="connectionString">Raw prod Postgres connection string (URL or key-value form).</param>
    /// <param name="key">Feature-flag key to read (e.g. <c>sync.directpush-gitbody</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> only when the flag row exists AND is enabled; <see langword="false"/> on
    /// a missing row, a null <c>enabled</c> value, or a caught connection/query failure. Fails CLOSED
    /// (D-04) — the inverse of the web-side <c>IFeatureFlagCache</c> default-on: Studio must never
    /// assume a brand-new, riskier flag is ON when it cannot confirm the value.
    /// </returns>
    Task<bool> ReadFlagAsync(string connectionString, string key, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This prod content reader does not support flag reads.");

    /// <summary>
    /// Tri-state feature-flag read (Codex re-review HIGH): distinguishes a DEFINITIVE flag value from an
    /// indeterminate read so a caller can fail to the SAFE side. Unlike <see cref="ReadFlagAsync"/> —
    /// which collapses "definitely OFF" and "could not read" into a single <see langword="false"/>
    /// (correct for the fail-closed <c>[skip render]</c> decision) — this returns <see langword="null"/>
    /// ONLY on a caught connection/query failure, so the DirectPush publish gate can treat "unknown" as
    /// "must verify the deployed body" rather than "publish immediately". A missing/absent flag row is a
    /// DEFINITIVE OFF (<see langword="false"/>), not indeterminate. Same single plain read-only SELECT as
    /// <see cref="ReadFlagAsync"/>; declared as a THROWING default interface method (89-02 / 90-03 idiom)
    /// so the hand-written <c>FakeProdContentReader</c> double keeps compiling unchanged.
    /// </summary>
    /// <param name="connectionString">Raw prod Postgres connection string (URL or key-value form).</param>
    /// <param name="key">Feature-flag key to read (e.g. <c>sync.directpush-gitbody</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the flag row exists AND is enabled; <see langword="false"/> on a
    /// missing row or a null <c>enabled</c> value (definitive OFF); <see langword="null"/> ONLY when the
    /// read itself failed (connection/query error) — the indeterminate signal.
    /// </returns>
    Task<bool?> TryReadFlagAsync(string connectionString, string key, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This prod content reader does not support flag reads.");
}
