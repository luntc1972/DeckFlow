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
    /// the resolver, never written). A row whose artifact cannot be resolved is skipped with a
    /// structured warning naming the row id — the pass continues, it never throws.
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

            var rawArtifactText = await _resolver
                .TryReadArtifactTextAsync(row.ArtifactPath, cancellationToken)
                .ConfigureAwait(false);
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
