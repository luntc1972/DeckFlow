using DeckFlow.Core.Content;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Read-only <see cref="IGitBodyCoverageAudit"/> implementation. Depends ONLY on the structurally
/// read-only <see cref="IProdContentReader"/> (single SELECT, no DDL) — it never references
/// <c>IProdStoreFactory</c> or any write/upsert/visibility/delete/schema-ensure API, so it is
/// structurally incapable of writing to production (90-CONTEXT.md D-11 / T-90-04). Body-path
/// resolution reuses the ONE shared <see cref="ArtifactPathSafety"/> guard — no second
/// path-validation routine is introduced (T-90-05).
/// </summary>
public sealed class GitBodyCoverageAudit : IGitBodyCoverageAudit
{
    private readonly IProdContentReader _prodReader;

    /// <summary>Creates the audit over the given read-only prod content reader.</summary>
    public GitBodyCoverageAudit(IProdContentReader prodReader)
    {
        ArgumentNullException.ThrowIfNull(prodReader);
        _prodReader = prodReader;
    }

    /// <inheritdoc />
    public async Task<GitBodyCoverageReport> RunAsync(
        string prodConnectionString,
        string repoRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        var prodRows = await _prodReader
            .ReadAllAsync(prodConnectionString ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        var missing = new List<GitBodyCoverageMissingRow>();
        foreach (var row in prodRows)
        {
            // Only approved + visible rows are in scope — this is the SYNC-07 flip precondition
            // ("every visible row's body is actually in git"), not a full-corpus report.
            if (!string.Equals(row.ApprovalStatus, "approved", StringComparison.Ordinal) || !row.IsVisible)
            {
                continue;
            }

            // Best-effort identity for the report; a row with no derivable natural key still gets
            // reported (title + expected path are enough to act on) rather than silently dropped.
            var hasKey = ContentNaturalKey.TryDerive(row, out var naturalKey);
            var keyType = hasKey ? naturalKey.Type : string.Empty;
            var keyValue = hasKey ? naturalKey.Value : string.Empty;

            // Reuse the ONE shared Studio path-safety guard (Task 1) — an unsafe/uncontained
            // artifact path is reported as missing/invalid, never probed outside the content-kb
            // root (T-90-05).
            var isPresent = ArtifactPathSafety.TryBuildContainedPath(repoRoot, row.ArtifactPath, out var fullPath)
                && File.Exists(fullPath);

            if (!isPresent)
            {
                missing.Add(new GitBodyCoverageMissingRow(keyType, keyValue, row.Title, row.ArtifactPath));
            }
        }

        return new GitBodyCoverageReport(missing);
    }
}
