using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>
/// Persists the slim Render-bound Content KB site index.
/// </summary>
public interface IContentSiteIndexStore
{
    /// <summary>
    /// Ensures the content site-index schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a slim site-index row keyed by its normalized natural key.
    /// </summary>
    /// <param name="row">Site-index row to insert or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a slim site-index row without changing visibility on existing rows.
    /// </summary>
    /// <param name="row">Site-index row to insert or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates content/nav columns and mirrors the source row's <c>approval_status</c>
    /// on insert and update (D-01/D-02), never touching operator-owned fields
    /// (is_visible, is_hidden, is_evergreen) on existing rows.
    /// </summary>
    /// <param name="row">Site-index row to insert or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a slim site-index row by normalized natural key.
    /// </summary>
    /// <param name="naturalKeyType">Natural key type, such as <see cref="ContentSourceType.Youtube"/> or <see cref="ContentSourceType.Podcast"/>.</param>
    /// <param name="naturalKeyValue">Natural key value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The site-index row when found; otherwise <see langword="null"/>.</returns>
    Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(
        string naturalKeyType,
        string naturalKeyValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets visible site-index rows ordered for deterministic browse surfaces.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Visible site-index rows.</returns>
    Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets site-index rows where approval_status='approved', ordered for deterministic export.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Approved site-index rows.</returns>
    Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all site-index rows ordered for deterministic curation surfaces.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All site-index rows.</returns>
    Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a slim site-index row by surrogate identifier.
    /// </summary>
    /// <param name="id">Site-index row identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The site-index row when found; otherwise <see langword="null"/>.</returns>
    Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a slim site-index row by surrogate identifier for the PUBLIC detail route — returned only
    /// when the row is both <c>is_visible</c> and <c>approval_status='approved'</c>. Defense-in-depth so a
    /// drifted visible-but-pending row can never render at <c>/content-kb/{id}</c>. Admin/Studio use
    /// <see cref="GetByIdAsync(long, CancellationToken)"/> which stays unfiltered.
    /// </summary>
    /// <param name="id">Site-index row identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The approved+visible row when found; otherwise <see langword="null"/>.</returns>
    Task<ContentSiteIndexRow?> GetPublishedByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets visibility for a single site-index row.
    /// </summary>
    /// <param name="id">Site-index row identifier.</param>
    /// <param name="visible">Whether the row should be visible.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets hidden state for a single site-index row.
    /// </summary>
    /// <param name="id">Site-index row identifier.</param>
    /// <param name="hidden">Whether the row should be hidden.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a single site-index row.
    /// </summary>
    /// <param name="id">Site-index row identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all site-index rows.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    Task<int> DeleteAllRowsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This content site-index store does not support deleting all rows.");

    /// <summary>
    /// Sets evergreen flag for a single site-index row.
    /// </summary>
    /// <param name="id">Site-index row identifier.</param>
    /// <param name="evergreen">Whether the row should be evergreen.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets visibility for all site-index rows from a source.
    /// </summary>
    /// <param name="source">Source name or discriminator.</param>
    /// <param name="visible">Whether matching rows should be visible.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets hidden state for all site-index rows from a source.
    /// </summary>
    /// <param name="source">Source name or discriminator.</param>
    /// <param name="hidden">Whether matching rows should be hidden.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the approval status for a single site-index row, keyed by natural key.
    /// Only <c>approval_status</c> is mutated; <c>is_visible</c>, <c>is_hidden</c>, and <c>is_evergreen</c> are unchanged.
    /// </summary>
    /// <param name="naturalKeyType">Natural key type, such as <see cref="ContentSourceType.Youtube"/> or <see cref="ContentSourceType.Podcast"/>.</param>
    /// <param name="naturalKeyValue">Natural key value.</param>
    /// <param name="status">Approval status to set; must be one of <c>pending</c>, <c>approved</c>, or <c>rejected</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows updated (1 on match, 0 when the natural key is not found).</returns>
    Task<int> SetApprovalStatusAsync(
        string naturalKeyType,
        string naturalKeyValue,
        string status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the approval status for a batch of site-index rows, keyed by natural key, inside a single atomic transaction.
    /// All rows are updated to the same <paramref name="status"/> or none are (all-or-nothing).
    /// Only <c>approval_status</c> is mutated; <c>is_visible</c>, <c>is_hidden</c>, and <c>is_evergreen</c> are unchanged.
    /// D-06: the batch runs in one transaction — one logical round-trip; partial approvals are forbidden.
    /// </summary>
    /// <param name="keys">Natural-key pairs to update.</param>
    /// <param name="status">Approval status to set; must be one of <c>pending</c>, <c>approved</c>, or <c>rejected</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of rows updated.</returns>
    Task<int> SetApprovalStatusAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        string status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stamps <c>pushed_to_prod_utc</c> = <paramref name="pushedUtc"/> for the given natural keys inside one transaction.
    /// The ONLY writer of <c>pushed_to_prod_utc</c>; no upsert touches the column. Local fact (PUB-01).
    /// </summary>
    /// <param name="keys">Natural-key pairs to update.</param>
    /// <param name="pushedUtc">UTC instant to stamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of rows updated.</returns>
    Task<int> StampPushedToProdAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        DateTimeOffset pushedUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets <c>is_visible</c> = <paramref name="visible"/> for the given natural keys inside one transaction.
    /// <c>is_hidden</c> is always cleared to <c>FALSE</c>, exactly mirroring the single-row
    /// <see cref="SetVisibilityAsync(long, bool, CancellationToken)"/>. Used by DirectPush so an
    /// operator-confirmed push publishes its rows visible in the same batch (both prod and local stores).
    /// </summary>
    /// <param name="keys">Natural-key pairs to update.</param>
    /// <param name="visible">Whether the matching rows should be visible.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of rows updated.</returns>
    Task<int> SetVisibilityAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        bool visible,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates content/nav columns only for every row in <paramref name="rows"/>
    /// inside a single <see cref="System.Data.Common.DbTransaction"/> — all-or-nothing.
    /// Mirrors the source row's <c>approval_status</c> on insert and update (D-01/D-02); never touches
    /// operator-owned <c>is_visible</c>, <c>is_hidden</c>, or <c>is_evergreen</c> on existing rows (T-qyc-03).
    /// </summary>
    /// <param name="rows">Rows to upsert; empty list is a no-op.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ContentSiteIndexBatchUpsertException">
    /// Thrown after rolling back the transaction when any row fails validation or the DB
    /// upsert throws. Carries the failing row's title and natural key (non-secret) with the
    /// underlying DB exception as <see cref="Exception.InnerException"/> for the log sink.
    /// </exception>
    Task UpsertContentColumnsOnlyBatchAsync(
        IReadOnlyList<ContentSiteIndexRow> rows,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This content site-index store does not support batch content upsert.");

    /// <summary>
    /// Sets <c>body_sha256</c> for a single row ONLY when it is currently <see langword="null"/> —
    /// safe to call repeatedly (D-08 backfill): a row that already carries a hash is left untouched
    /// (never overwrites an existing hash). Real-implemented on <see cref="ContentSiteIndexStore"/>;
    /// this default interface method mirrors <see cref="DeleteAllRowsAsync"/>'s throwing-escape-hatch
    /// idiom so the ~13 hand-written test doubles that don't need backfill semantics compile unchanged.
    /// </summary>
    /// <param name="id">Site-index row identifier.</param>
    /// <param name="bodySha256">Lowercase hex SHA-256 to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows updated (1 when the row existed with a null hash; otherwise 0).</returns>
    Task<int> SetBodySha256IfNullAsync(long id, string bodySha256, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This content site-index store does not support body-hash backfill.");

    /// <summary>
    /// Sets <c>awaiting_confirm_utc</c> = <paramref name="whenUtc"/> for the given natural keys inside
    /// one transaction — the durable "pushed, awaiting deploy-confirm" marker (D-10). Keyed ONLY on
    /// <c>(natural_key_type, natural_key_value)</c>; no WHERE filters on any timestamp column
    /// (F-51-PG-01 avoided). Real-implemented on <see cref="ContentSiteIndexStore"/>; this default
    /// interface method mirrors <see cref="SetBodySha256IfNullAsync"/>'s throwing-escape-hatch idiom
    /// so existing hand-written test doubles compile unchanged.
    /// </summary>
    /// <param name="keys">Natural-key pairs to update.</param>
    /// <param name="whenUtc">UTC instant to stamp as awaiting-confirm.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of rows updated.</returns>
    Task<int> SetAwaitingConfirmAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        DateTimeOffset whenUtc,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This content site-index store does not support the awaiting-confirm marker.");

    /// <summary>
    /// Clears <c>awaiting_confirm_utc</c> (sets it <see langword="null"/>) for the given natural keys
    /// inside one transaction (D-10). Keyed ONLY on <c>(natural_key_type, natural_key_value)</c>; no
    /// WHERE filters on any timestamp column (F-51-PG-01 avoided). Real-implemented on
    /// <see cref="ContentSiteIndexStore"/>; this default interface method mirrors
    /// <see cref="SetBodySha256IfNullAsync"/>'s throwing-escape-hatch idiom so existing hand-written
    /// test doubles compile unchanged.
    /// </summary>
    /// <param name="keys">Natural-key pairs to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of rows updated.</returns>
    Task<int> ClearAwaitingConfirmAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This content site-index store does not support the awaiting-confirm marker.");
}
