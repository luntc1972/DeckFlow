namespace DeckFlow.Studio.Services;

/// <summary>
/// Downloads remote Render <c>/data</c> Content-KB artifact files to a local staging directory via
/// SFTP. Each request carries the row's relative artifact path so the implementation can build and
/// traversal-validate both the remote source (under the configured remote artifact root) and the
/// local destination (under the staging root) — neither side trusts the raw prod-DB path.
/// </summary>
public interface ISshArtifactDownloader
{
    /// <summary>
    /// Downloads a set of remote artifact files into <paramref name="localStagingRoot"/>.
    /// Returns per-file results; does not throw on individual file failure.
    /// </summary>
    /// <param name="downloads">
    /// The artifact downloads to perform. Each entry pairs the row's relative remote artifact path
    /// (e.g. <c>content-kb/{slug}/{id}.md</c>) with the relative local destination path; the
    /// implementation validates both resolve under their respective roots (no rooted paths, no
    /// <c>..</c>) before any file write.
    /// </param>
    /// <param name="localStagingRoot">Absolute local staging directory the downloads land under.</param>
    /// <param name="progress">Optional per-file progress sink, reported once per download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per-file download results, in the same order as <paramref name="downloads"/>.</returns>
    Task<IReadOnlyList<SshDownloadResult>> DownloadArtifactsAsync(
        IReadOnlyList<SshDownloadRequest> downloads,
        string localStagingRoot,
        IProgress<SshDownloadResult>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>A single artifact download request.</summary>
/// <param name="RemoteRelativePath">
/// The row's relative artifact path resolved under the configured remote artifact root. The
/// implementation rejects rooted paths and any path containing <c>..</c> before building the remote
/// source (path-traversal guard).
/// </param>
/// <param name="LocalRelativePath">
/// The relative local destination path (combined with the staging root). The implementation
/// rejects rooted / <c>..</c> values and confirms the resolved path stays under the staging root.
/// </param>
public sealed record SshDownloadRequest(string RemoteRelativePath, string LocalRelativePath);

/// <summary>Per-file result of an SFTP download attempt.</summary>
/// <param name="RemoteRelativePath">The relative artifact path that was downloaded.</param>
/// <param name="LocalPath">Absolute local path the file was written to (empty on pre-write failure).</param>
/// <param name="Success">Whether the download succeeded.</param>
/// <param name="FailureReason">
/// Sanitized failure reason; <c>null</c> on success. Sanitized; never contains host/key/path
/// secrets or <c>ex.Message</c>.
/// </param>
public sealed record SshDownloadResult(
    string RemoteRelativePath,
    string LocalPath,
    bool Success,
    string? FailureReason);
