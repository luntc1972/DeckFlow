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
public sealed class SftpArtifactDownloader : ISshArtifactDownloader
{
    // Why: the only failure string ever surfaced to the result/UI — never ex.Message, which can
    // carry the host or remote path (D-07 / Pitfall 3).
    private const string SanitizedFailureReason =
        "SSH download failed — check SCP configuration and Render SSH access.";

    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _keyFile;
    private readonly string? _keyPassphrase;
    private readonly string _remoteArtifactRoot;
    private readonly ILogger<SftpArtifactDownloader>? _logger;

    /// <summary>
    /// Initializes a new <see cref="SftpArtifactDownloader"/>, reading the <c>Studio:Scp:*</c>
    /// secrets from <paramref name="configuration"/>. None of these values are ever logged (D-07).
    /// </summary>
    /// <param name="configuration">Configuration providing the <c>Studio:Scp:*</c> section.</param>
    /// <param name="logger">Optional logger; failure details are never written with secret values.</param>
    public SftpArtifactDownloader(IConfiguration configuration, ILogger<SftpArtifactDownloader>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _host = configuration["Studio:Scp:Host"] ?? string.Empty;
        _port = int.TryParse(configuration["Studio:Scp:Port"], out var port) ? port : 22;
        _username = configuration["Studio:Scp:Username"] ?? string.Empty;
        _keyFile = configuration["Studio:Scp:KeyFile"] ?? string.Empty;
        _keyPassphrase = configuration["Studio:Scp:KeyPassphrase"]; // optional; null = no passphrase
        _remoteArtifactRoot = NormalizeRoot(configuration["Studio:Scp:RemoteArtifactRoot"] ?? string.Empty);
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
        var results = new List<SshDownloadResult>(downloads.Count);

        // Why: SftpClient is not thread-safe across concurrent calls — open ONE client per
        // DownloadArtifactsAsync invocation, download sequentially, then disconnect (Pitfall 5).
        SftpClient? client = null;
        var connected = false;
        try
        {
            using var privateKey = string.IsNullOrEmpty(_keyPassphrase)
                ? new PrivateKeyFile(_keyFile)
                : new PrivateKeyFile(_keyFile, _keyPassphrase);
            client = new SftpClient(_host, _port, _username, privateKey);
            client.Connect();
            connected = true;

            foreach (var request in downloads)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = DownloadOne(client, request, stagingRootFull);
                results.Add(result);
                progress?.Report(result);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is SshException or IOException)
        {
            // Why: connect-level failure aborts the batch — mark every not-yet-attempted request
            // failed with the sanitized reason; never surface ex.Message (D-07 / Pitfall 3).
            _logger?.LogWarning("SFTP connection failed; marking remaining artifacts as failed.");
            for (var i = results.Count; i < downloads.Count; i++)
            {
                var request = downloads[i];
                var failed = new SshDownloadResult(
                    request.RemoteRelativePath, string.Empty, false, SanitizedFailureReason);
                results.Add(failed);
                progress?.Report(failed);
            }
        }
        finally
        {
            if (client is not null)
            {
                if (connected)
                {
                    client.Disconnect();
                }

                client.Dispose();
            }
        }

        return Task.FromResult<IReadOnlyList<SshDownloadResult>>(results);
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
    /// Builds the absolute remote path from <see cref="_remoteArtifactRoot"/> + the relative
    /// artifact path, rejecting rooted paths, <c>..</c> traversal, and any resolved path that does
    /// not stay under the root.
    /// </summary>
    private bool TryBuildRemotePath(string remoteRelativePath, out string remotePath)
    {
        remotePath = string.Empty;

        if (string.IsNullOrWhiteSpace(remoteRelativePath))
        {
            return false;
        }

        var normalizedRelative = remoteRelativePath.Replace('\\', '/');

        // Reject rooted paths (leading '/' or a Windows drive root) outright.
        if (normalizedRelative.StartsWith('/') || Path.IsPathRooted(remoteRelativePath))
        {
            return false;
        }

        // Reject any '..' segment (traversal) before joining.
        var segments = normalizedRelative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(static segment => segment == ".."))
        {
            return false;
        }

        if (segments.Length == 0)
        {
            return false;
        }

        var candidate = CollapseSlashes($"{_remoteArtifactRoot}/{string.Join('/', segments)}");

        // Confirm the resolved path stays strictly under the root (boundary-safe, not loose prefix).
        if (candidate != _remoteArtifactRoot && !candidate.StartsWith(_remoteArtifactRoot + "/", StringComparison.Ordinal))
        {
            return false;
        }

        remotePath = candidate;
        return true;
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

    private static string NormalizeRoot(string root)
    {
        var collapsed = CollapseSlashes(root.Replace('\\', '/'));
        return collapsed.Length > 1 ? collapsed.TrimEnd('/') : collapsed;
    }

    private static string CollapseSlashes(string value)
    {
        var hadLeadingSlash = value.StartsWith('/');
        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var joined = string.Join('/', segments);
        return hadLeadingSlash ? "/" + joined : joined;
    }
}
