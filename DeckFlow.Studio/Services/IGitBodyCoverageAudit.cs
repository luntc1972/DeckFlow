namespace DeckFlow.Studio.Services;

/// <summary>
/// Read-only pre-flip audit (90-CONTEXT.md D-11 / SYNC-07 rollout precondition): for every
/// approved + visible production <c>content_site_index</c> row, checks whether its body <c>.md</c>
/// exists in the local git content-kb tree (what will become <c>/app</c> after deploy) and reports
/// the rows whose body is MISSING. This is reporting only — it never deletes, reconciles, or
/// writes anything to prod or the local store (that is Phase 91's reconciler).
/// </summary>
public interface IGitBodyCoverageAudit
{
    /// <summary>
    /// Reads all production rows via the read-only <c>IProdContentReader</c>, filters to approved +
    /// visible, and cross-references each row's body against the local git tree rooted at
    /// <paramref name="repoRoot"/>. Performs no writes.
    /// </summary>
    /// <param name="prodConnectionString">Raw prod Postgres connection string (ephemeral, never stored).</param>
    /// <param name="repoRoot">Local git repository root (the checkout that becomes <c>/app</c> after deploy).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A report listing every approved+visible row whose body is missing from the git tree.</returns>
    Task<GitBodyCoverageReport> RunAsync(
        string prodConnectionString,
        string repoRoot,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One approved+visible production row whose body <c>.md</c> could not be found in the local git
/// content-kb tree (or whose stored artifact path failed the shared path-safety guard).
/// </summary>
/// <param name="NaturalKeyType">Stored natural-key type vocabulary (e.g. <c>youtube_channel</c>), or empty when the row has no derivable natural key.</param>
/// <param name="NaturalKeyValue">Natural-key value, or empty when the row has no derivable natural key.</param>
/// <param name="Title">Row title, for operator identification.</param>
/// <param name="ExpectedPath">The row's stored <c>ArtifactPath</c> — the repo-relative location the body is expected to occupy.</param>
public sealed record GitBodyCoverageMissingRow(
    string NaturalKeyType,
    string NaturalKeyValue,
    string Title,
    string ExpectedPath);

/// <summary>
/// Result of a <see cref="IGitBodyCoverageAudit"/> run: the approved+visible prod rows whose body
/// is missing from the local git tree, plus the convenience count.
/// </summary>
/// <param name="MissingRows">Every approved+visible row whose body is missing (or path-unsafe).</param>
public sealed record GitBodyCoverageReport(IReadOnlyList<GitBodyCoverageMissingRow> MissingRows)
{
    /// <summary>Count of rows whose body is missing from the git tree.</summary>
    public int MissingCount => MissingRows.Count;
}
