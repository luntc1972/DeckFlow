namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Exports published Content KB site-index rows for host-owned JSON serialization and file output.
/// </summary>
public interface IContentIndexExporter
{
    /// <summary>
    /// Exports published site-index rows.
    /// </summary>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured exported rows and status information.</returns>
    Task<ContentIndexExportResult> ExportIndexAsync(
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes the approved-only site-index rows to <paramref name="seedPath"/> with guaranteed
    /// LF line endings (no CRLF) and the same JSON byte-shape as the CLI export (camelCase,
    /// 2-space indent, trailing newline). Parent directories are created when absent.
    /// </summary>
    /// <param name="seedPath">Absolute path to the seed JSON file to write.</param>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="ContentIndexExportResult.Success"/> is <see langword="true"/> on success and
    /// the <see cref="ContentIndexExportResult.RowCount"/> reflects the approved row count;
    /// <see langword="false"/> with <see cref="ContentIndexExportResult.Message"/> on I/O failure.
    /// </returns>
    Task<ContentIndexExportResult> ExportIndexToFileAsync(
        string seedPath,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Materializes approved markdown artifacts from the Studio data root into the repo
    /// working tree so they can be staged and committed. Returns the copied repo-relative
    /// paths (= <c>row.ArtifactPath</c> for each approved row) for the caller to pass to
    /// <see cref="DeckFlow.Core.Integration.IGitRepository.StageAndCommitAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Source derivation: <paramref name="dataRoot"/> is the PARENT of <c>ArtifactRoot</c>
    /// (i.e. the Studio data directory). Because <see cref="ContentIndexExportRow.ArtifactPath"/>
    /// already begins with <c>content-kb/</c>, the on-disk source is
    /// <c>Path.Combine(dataRoot, row.ArtifactPath)</c> — never a double <c>content-kb</c>.
    /// </para>
    /// <para>
    /// A missing or unreadable source file is a publish-blocking error (D-10). The method
    /// throws rather than silently skipping — callers must never commit a seed that references
    /// files absent from the repo.
    /// </para>
    /// <para>
    /// Both the source and destination paths are containment-guarded: rooted paths, <c>..</c>
    /// traversals, and git pathspec-magic (leading <c>:</c>) are rejected with an
    /// <see cref="ArgumentException"/>.
    /// </para>
    /// </remarks>
    /// <param name="dataRoot">
    /// The Studio data directory — the PARENT of <c>ArtifactRoot</c>
    /// (e.g. <c>/data</c> when <c>ArtifactRoot = /data/content-kb</c>).
    /// </param>
    /// <param name="repoRoot">Absolute path to the git working tree root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The list of repo-relative artifact paths that were copied (each is
    /// <c>row.ArtifactPath</c>) for use as pathspecs in the publish commit.
    /// </returns>
    Task<IReadOnlyList<string>> CopyApprovedArtifactsToRepoAsync(
        string dataRoot,
        string repoRoot,
        CancellationToken cancellationToken = default);
}
