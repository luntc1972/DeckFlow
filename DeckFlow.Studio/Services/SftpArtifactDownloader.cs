using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DeckFlow.Studio.Services;

/// <summary>
/// SSH.NET <see cref="SftpClient"/>-backed implementation of <see cref="ISshArtifactDownloader"/>.
/// Reads the SSH/SFTP target from the <c>Studio:Scp:*</c> configuration section and downloads each
/// requested artifact from under the configured remote artifact root into a local staging dir.
/// Per-file failures are captured in the returned results (never thrown) and failure reasons are
/// sanitized so no host, key, or path value can leak to logs or UI (D-07). Read-only toward the
/// remote: it never creates remote directories or writes any remote path.
/// </summary>
public sealed class SftpArtifactDownloader : SftpArtifactSessionBase, ISshArtifactDownloader
{
    // Why: the only failure string ever surfaced to the result/UI — never ex.Message, which can
    // carry the host or remote path (D-07 / Pitfall 3).
    private const string SanitizedFailureReason =
        "SSH download failed — check SCP configuration and Render SSH access.";

    private readonly ILogger<SftpArtifactDownloader>? _logger;

    /// <summary>
    /// Initializes a new <see cref="SftpArtifactDownloader"/>, reading the <c>Studio:Scp:*</c>
    /// secrets from <paramref name="configuration"/>. None of these values are ever logged (D-07).
    /// </summary>
    /// <param name="configuration">Configuration providing the <c>Studio:Scp:*</c> section.</param>
    /// <param name="logger">Optional logger; failure details are never written with secret values.</param>
    public SftpArtifactDownloader(IConfiguration configuration, ILogger<SftpArtifactDownloader>? logger = null)
        : base(configuration)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SshDownloadResult>> DownloadArtifactsAsync(
        IReadOnlyList<SshDownloadRequest> downloads,
        string localStagingRoot,
        IProgress<SshDownloadResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(downloads);
        ArgumentException.ThrowIfNullOrWhiteSpace(localStagingRoot);

        var stagingRootFull = Path.GetFullPath(localStagingRoot);

        return RunSftpBatchAsync(
            downloads,
            (client, request) => DownloadOne(client, request, stagingRootFull),
            request => new SshDownloadResult(
                request.RemoteRelativePath, string.Empty, false, SanitizedFailureReason),
            progress,
            _logger,
            cancellationToken);
    }

    private SshDownloadResult DownloadOne(SftpClient client, SshDownloadRequest request, string stagingRootFull)
    {
        // Path-safety: reject rooted / traversal paths on BOTH sides. The remote path is validated
        // under RemoteArtifactRoot; the local destination is validated under the staging root and
        // re-confirmed with Path.GetFullPath containment — prod-DB ArtifactPath values are untrusted
        // (T-60-04 / T-60-05, Pitfall 5).
        if (!TryBuildRemotePath(request.RemoteRelativePath, out var remotePath))
        {
            return new SshDownloadResult(request.RemoteRelativePath, string.Empty, false, SanitizedFailureReason);
        }

        if (!TryBuildLocalPath(stagingRootFull, request.LocalRelativePath, out var localDest))
        {
            return new SshDownloadResult(request.RemoteRelativePath, string.Empty, false, SanitizedFailureReason);
        }

        try
        {
            var localDir = Path.GetDirectoryName(localDest);
            if (!string.IsNullOrEmpty(localDir))
            {
                Directory.CreateDirectory(localDir);
            }

            using var fileStream = File.Create(localDest);
            client.DownloadFile(remotePath, fileStream);

            return new SshDownloadResult(request.RemoteRelativePath, localDest, true, null);
        }
        catch (Exception ex) when (ex is SshException or IOException)
        {
            // Why: never surface ex.Message — it can carry the remote path or host (D-07 / Pitfall 3).
            _logger?.LogWarning("SFTP download of one artifact failed.");
            return new SshDownloadResult(request.RemoteRelativePath, string.Empty, false, SanitizedFailureReason);
        }
    }

    /// <summary>
    /// Builds the absolute local destination from the staging root + the relative path, rejecting
    /// rooted paths and <c>..</c> traversal and confirming (via <see cref="Path.GetFullPath(string)"/>)
    /// that the resolved path stays strictly under the staging root.
    /// </summary>
    private static bool TryBuildLocalPath(string stagingRootFull, string localRelativePath, out string localPath)
    {
        localPath = string.Empty;

        if (string.IsNullOrWhiteSpace(localRelativePath))
        {
            return false;
        }

        // Reject rooted paths and any '..' segment before combining.
        if (Path.IsPathRooted(localRelativePath))
        {
            return false;
        }

        var segments = localRelativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(static segment => segment == ".."))
        {
            return false;
        }

        var combined = Path.GetFullPath(Path.Combine(stagingRootFull, Path.Combine(segments)));

        // Confirm the resolved path stays strictly under the staging root (defense in depth).
        var rootWithSep = stagingRootFull.EndsWith(Path.DirectorySeparatorChar)
            ? stagingRootFull
            : stagingRootFull + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootWithSep, StringComparison.Ordinal))
        {
            return false;
        }

        localPath = combined;
        return true;
    }
}
