namespace DeckFlow.Studio.Services;

/// <summary>
/// Uploads local Content-KB artifact files to the configured remote Render <c>/data</c>
/// path via SFTP. Each request carries the row's relative artifact path so the
/// implementation can build and traversal-validate the remote target under the configured
/// remote artifact root — the absolute local path is never used to guess the remote path.
/// </summary>
public interface ISshArtifactUploader
{
    /// <summary>
    /// Uploads a set of local artifact files to the configured remote path.
    /// Returns per-file results; does not throw on individual file failure.
    /// </summary>
    /// <param name="uploads">
    /// The artifact uploads to perform. Each entry pairs an absolute local file path with the
    /// row's relative artifact path; the implementation validates the relative path resolves
    /// under the configured remote artifact root (no rooted paths, no <c>..</c>) before any
    /// upload.
    /// </param>
    /// <param name="progress">Optional per-file progress sink, reported once per upload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per-file upload results, in the same order as <paramref name="uploads"/>.</returns>
    Task<IReadOnlyList<SshUploadResult>> UploadArtifactsAsync(
        IReadOnlyList<SshUploadRequest> uploads,
        IProgress<SshUploadResult>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>A single artifact upload request.</summary>
/// <param name="LocalPath">Absolute local path of the artifact file to upload.</param>
/// <param name="RemoteRelativePath">
/// The row's relative artifact path (e.g. <c>content-kb/{slug}/{id}.md</c>). The implementation
/// validates this resolves under the configured remote artifact root — it rejects rooted paths
/// and any path containing <c>..</c> before building the remote target — to prevent writing
/// outside the artifact root (path-traversal guard).
/// </param>
public sealed record SshUploadRequest(string LocalPath, string RemoteRelativePath);

/// <summary>Per-file result of an SFTP upload attempt.</summary>
/// <param name="LocalPath">Absolute local path of the file.</param>
/// <param name="RemoteRelativePath">The relative artifact path that was uploaded.</param>
/// <param name="Success">Whether the upload succeeded.</param>
/// <param name="FailureReason">
/// Sanitized failure reason; <c>null</c> on success. Sanitized; never contains host/key/path
/// secrets.
/// </param>
public sealed record SshUploadResult(
    string LocalPath,
    string RemoteRelativePath,
    bool Success,
    string? FailureReason);
