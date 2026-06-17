using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DeckFlow.Studio.Services;

/// <summary>
/// SSH.NET <see cref="SftpClient"/>-backed implementation of <see cref="ISshArtifactUploader"/>.
/// Reads the SSH/SFTP target from the <c>Studio:Scp:*</c> configuration section and uploads each
/// requested artifact under the configured remote artifact root. Per-file failures are captured
/// in the returned results (never thrown) and failure reasons are sanitized so no host, key, or
/// remote-path value can leak to logs or UI (D-07).
/// </summary>
public sealed class SftpArtifactUploader : ISshArtifactUploader
{
    // Why: the only failure string ever surfaced to the result/UI — never ex.Message, which can
    // carry the host or remote path (D-07 / Pitfall 3).
    private const string SanitizedFailureReason =
        "SSH upload failed — check SCP configuration and Render SSH access.";

    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _keyFile;
    private readonly string? _keyPassphrase;
    private readonly string _remoteArtifactRoot;
    private readonly ILogger<SftpArtifactUploader>? _logger;

    /// <summary>
    /// Initializes a new <see cref="SftpArtifactUploader"/>, reading the <c>Studio:Scp:*</c>
    /// secrets from <paramref name="configuration"/>. None of these values are ever logged (D-07).
    /// </summary>
    /// <param name="configuration">Configuration providing the <c>Studio:Scp:*</c> section.</param>
    /// <param name="logger">Optional logger; failure details are never written with secret values.</param>
    public SftpArtifactUploader(IConfiguration configuration, ILogger<SftpArtifactUploader>? logger = null)
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
    public Task<IReadOnlyList<SshUploadResult>> UploadArtifactsAsync(
        IReadOnlyList<SshUploadRequest> uploads,
        IProgress<SshUploadResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uploads);

        var results = new List<SshUploadResult>(uploads.Count);

        // Why: SftpClient is not thread-safe across concurrent calls — open ONE client per
        // UploadArtifactsAsync invocation, upload sequentially, then disconnect (Pitfall 5).
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

            foreach (var request in uploads)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = UploadOne(client, request);
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
            for (var i = results.Count; i < uploads.Count; i++)
            {
                var request = uploads[i];
                var failed = new SshUploadResult(
                    request.LocalPath, request.RemoteRelativePath, false, SanitizedFailureReason);
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

        return Task.FromResult<IReadOnlyList<SshUploadResult>>(results);
    }

    private SshUploadResult UploadOne(SftpClient client, SshUploadRequest request)
    {
        // Path-safety (V5 / threat T-47-02c): reject rooted / traversal paths and confirm the
        // resolved remote path stays under RemoteArtifactRoot before any remote write. The relative
        // path was already validated upstream by ContentSiteIndexStore.ValidateArtifactPath — this
        // is defense in depth (Codex HIGH-1).
        if (!TryBuildRemotePath(request.RemoteRelativePath, out var remotePath))
        {
            return new SshUploadResult(
                request.LocalPath, request.RemoteRelativePath, false, SanitizedFailureReason);
        }

        try
        {
            var remoteDir = GetRemoteParent(remotePath);
            EnsureRemoteDirectory(client, remoteDir);

            using var fileStream = File.OpenRead(request.LocalPath);
            client.UploadFile(fileStream, remotePath);

            return new SshUploadResult(request.LocalPath, request.RemoteRelativePath, true, null);
        }
        catch (Exception ex) when (ex is SshException or IOException)
        {
            // Why: never surface ex.Message — it can carry the remote path or host (D-07 / Pitfall 3).
            _logger?.LogWarning("SFTP upload of one artifact failed.");
            return new SshUploadResult(
                request.LocalPath, request.RemoteRelativePath, false, SanitizedFailureReason);
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
    /// Creates each missing directory level under the remote root. SFTP <c>CreateDirectory</c> does
    /// NOT create nested parents (Pitfall 6 / MEDIUM-3), so walk each <c>/</c>-segment.
    /// </summary>
    private void EnsureRemoteDirectory(SftpClient client, string remoteDir)
    {
        if (string.IsNullOrEmpty(remoteDir))
        {
            return;
        }

        var rootSegments = _remoteArtifactRoot.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var allSegments = remoteDir.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Why: only create levels at or below the configured root; never attempt to create the
        // ancestors of the root (they are expected to exist on /data).
        var current = "/" + string.Join('/', rootSegments);
        if (!client.Exists(current))
        {
            client.CreateDirectory(current);
        }

        for (var i = rootSegments.Length; i < allSegments.Length; i++)
        {
            current = CollapseSlashes($"{current}/{allSegments[i]}");
            if (!client.Exists(current))
            {
                client.CreateDirectory(current);
            }
        }
    }

    private static string GetRemoteParent(string remotePath)
    {
        var lastSlash = remotePath.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : remotePath[..lastSlash];
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
