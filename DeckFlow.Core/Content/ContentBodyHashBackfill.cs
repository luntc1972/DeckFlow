using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Content;

/// <summary>
/// One-time deterministic backfill (D-08) that computes <c>body_sha256</c> for every existing
/// <c>content_site_index</c> row whose stored hash is <see langword="null"/>. Host-agnostic: it
/// depends only on <see cref="IContentSiteIndexStore"/>, an injected <see cref="IContentArtifactBodyResolver"/>,
/// and the shared <see cref="ContentSiteIndexContentSignature.ComputeBodySha256"/> helper, so both
/// the web app (prod + local-web store) and Studio (local <c>content-kb.db</c> store) can run it at
/// startup after their own schema-ensure. Idempotent — a row that already carries a hash is never
/// read or overwritten; re-running <see cref="RunAsync"/> after a full pass performs zero writes.
/// Issues no DDL and no direct SQL; every write flows through
/// <see cref="IContentSiteIndexStore.SetBodySha256IfNullAsync"/>, a null-only UPDATE.
/// </summary>
public sealed class ContentBodyHashBackfill
{
    private readonly IContentSiteIndexStore _store;
    private readonly IContentArtifactBodyResolver _resolver;
    private readonly ILogger<ContentBodyHashBackfill> _logger;

    /// <summary>
    /// Creates a new host-agnostic body-hash backfill service.
    /// </summary>
    /// <param name="store">Content site-index store to enumerate and update.</param>
    /// <param name="resolver">Host-supplied artifact-body resolver.</param>
    /// <param name="logger">Structured logger.</param>
    public ContentBodyHashBackfill(
        IContentSiteIndexStore store,
        IContentArtifactBodyResolver resolver,
        ILogger<ContentBodyHashBackfill> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _resolver = resolver;
        _logger = logger;
    }

    /// <summary>
    /// Enumerates every site-index row and hashes each row whose <c>body_sha256</c> is currently
    /// <see langword="null"/>. Rows that already carry a hash are left untouched (never read via
    /// the resolver, never written). A row whose artifact cannot be resolved <em>or read</em> (the
    /// resolver returns <see langword="null"/> or throws a read failure) is skipped with a
    /// structured warning naming the row id — the resolve/read step never propagates, so a single
    /// locked/permission-denied artifact cannot crash host startup. Cancellation still propagates;
    /// a store-write failure also propagates, since it signals a systemic DB problem rather than a
    /// per-row content issue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _store.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);

        var hashedCount = 0;
        var skippedCount = 0;
        var alreadyHashedCount = 0;

        foreach (var row in rows)
        {
            if (row.BodySha256 is not null)
            {
                alreadyHashedCount++;
                continue;
            }

            string? rawArtifactText;
            try
            {
                rawArtifactText = await _resolver
                    .TryReadArtifactTextAsync(row.ArtifactPath, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation (from this token or any linked one) is never a per-row content
                // failure — always propagate it rather than skipping the row.
                throw;
            }
            catch (Exception ex)
            {
                // D-08: this backfill runs during host startup, so a single unreadable artifact
                // (locked, permission-denied, path-too-long) must never crash the host even if a
                // host adapter fails to swallow the read exception. Skip the row and continue.
                skippedCount++;
                _logger.LogWarning(
                    ex,
                    "Content KB body-hash backfill skipped row {ContentKbRowId}: artifact read failed.",
                    row.Id);
                continue;
            }

            if (rawArtifactText is null)
            {
                skippedCount++;
                _logger.LogWarning(
                    "Content KB body-hash backfill skipped row {ContentKbRowId}: artifact unresolved.",
                    row.Id);
                continue;
            }

            var bodySha256 = ContentSiteIndexContentSignature.ComputeBodySha256(rawArtifactText);
            await _store.SetBodySha256IfNullAsync(row.Id, bodySha256, cancellationToken).ConfigureAwait(false);
            hashedCount++;
        }

        _logger.LogInformation(
            "Content KB body-hash backfill complete: {HashedCount} hashed, {SkippedCount} skipped, {AlreadyHashedCount} already hashed.",
            hashedCount,
            skippedCount,
            alreadyHashedCount);
    }
}
