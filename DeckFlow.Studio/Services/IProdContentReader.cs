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
}
