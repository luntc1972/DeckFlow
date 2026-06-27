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
public sealed class SftpArtifactUploader : SftpArtifactSessionBase, ISshArtifactUploader
{
    // Why: the only failure string ever surfaced to the result/UI — never ex.Message, which can
    // carry the host or remote path (D-07 / Pitfall 3).
    private const string SanitizedFailureReason =
        "SSH upload failed — check SCP configuration and Render SSH access.";

    private readonly ILogger<SftpArtifactUploader>? _logger;

    /// <summary>
    /// Initializes a new <see cref="SftpArtifactUploader"/>, reading the <c>Studio:Scp:*</c>
    /// secrets from <paramref name="configuration"/>. None of these values are ever logged (D-07).
    /// </summary>
    /// <param name="configuration">Configuration providing the <c>Studio:Scp:*</c> section.</param>
    /// <param name="logger">Optional logger; failure details are never written with secret values.</param>
    public SftpArtifactUploader(IConfiguration configuration, ILogger<SftpArtifactUploader>? logger = null)
        : base(configuration)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SshUploadResult>> UploadArtifactsAsync(
        IReadOnlyList<SshUploadRequest> uploads,
        IProgress<SshUploadResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uploads);

        return RunSftpBatchAsync(
            uploads,
            UploadOne,
            request => new SshUploadResult(
                request.LocalPath, request.RemoteRelativePath, false, SanitizedFailureReason),
            progress,
            _logger,
            cancellationToken);
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
    /// Creates each missing directory level under the remote root. SFTP <c>CreateDirectory</c> does
    /// NOT create nested parents (Pitfall 6 / MEDIUM-3), so walk each <c>/</c>-segment.
    /// </summary>
    private void EnsureRemoteDirectory(SftpClient client, string remoteDir)
    {
        if (string.IsNullOrEmpty(remoteDir))
        {
            return;
        }

        var rootSegments = RemoteArtifactRoot.Split('/', StringSplitOptions.RemoveEmptyEntries);
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
}
