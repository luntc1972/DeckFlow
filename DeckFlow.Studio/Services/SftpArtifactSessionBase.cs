using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Shared SSH/SFTP session infrastructure for the artifact uploader and downloader. Owns the
/// <c>Studio:Scp:*</c> configuration read, the single-client connect/sequential/disconnect batch
/// lifecycle, and the remote-path-safety helpers so both directions share one implementation and
/// cannot drift (D-07 / Pitfall 5). None of the configured values are ever logged.
/// </summary>
public abstract class SftpArtifactSessionBase
{
    /// <summary>SSH host (never logged).</summary>
    protected string Host { get; }

    /// <summary>SSH port; defaults to 22 when unset or unparseable.</summary>
    protected int Port { get; }

    /// <summary>SSH username (never logged).</summary>
    protected string Username { get; }

    /// <summary>Path to the private key file (never logged).</summary>
    protected string KeyFile { get; }

    /// <summary>Optional private-key passphrase; <c>null</c> means no passphrase.</summary>
    protected string? KeyPassphrase { get; }

    /// <summary>Normalized remote artifact root that all transfers stay under.</summary>
    protected string RemoteArtifactRoot { get; }

    /// <summary>
    /// Reads the <c>Studio:Scp:*</c> secrets from <paramref name="configuration"/>. None of these
    /// values are ever logged (D-07).
    /// </summary>
    /// <param name="configuration">Configuration providing the <c>Studio:Scp:*</c> section.</param>
    protected SftpArtifactSessionBase(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Host = configuration["Studio:Scp:Host"] ?? string.Empty;
        Port = int.TryParse(configuration["Studio:Scp:Port"], out var port) ? port : 22;
        Username = configuration["Studio:Scp:Username"] ?? string.Empty;
        KeyFile = configuration["Studio:Scp:KeyFile"] ?? string.Empty;
        KeyPassphrase = configuration["Studio:Scp:KeyPassphrase"]; // optional; null = no passphrase
        RemoteArtifactRoot = NormalizeRoot(configuration["Studio:Scp:RemoteArtifactRoot"] ?? string.Empty);
    }

    /// <summary>
    /// Opens ONE <see cref="SftpClient"/> for the whole batch, processes each request sequentially
    /// (<see cref="SftpClient"/> is not thread-safe — Pitfall 5), and reports each result via
    /// <paramref name="progress"/>. A connect-level failure aborts the batch and marks every
    /// not-yet-attempted request failed via <paramref name="buildConnectFailure"/>; per-request
    /// failures are the responsibility of <paramref name="processOne"/>. Never throws except for
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    protected Task<IReadOnlyList<TResult>> RunSftpBatchAsync<TRequest, TResult>(
        IReadOnlyList<TRequest> requests,
        Func<SftpClient, TRequest, TResult> processOne,
        Func<TRequest, TResult> buildConnectFailure,
        IProgress<TResult>? progress,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var results = new List<TResult>(requests.Count);

        SftpClient? client = null;
        var connected = false;
        try
        {
            using var privateKey = string.IsNullOrEmpty(KeyPassphrase)
                ? new PrivateKeyFile(KeyFile)
                : new PrivateKeyFile(KeyFile, KeyPassphrase);
            client = new SftpClient(Host, Port, Username, privateKey);
            client.Connect();
            connected = true;

            foreach (var request in requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = processOne(client, request);
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
            logger?.LogWarning("SFTP connection failed; marking remaining artifacts as failed.");
            for (var i = results.Count; i < requests.Count; i++)
            {
                var failed = buildConnectFailure(requests[i]);
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

        return Task.FromResult<IReadOnlyList<TResult>>(results);
    }

    /// <summary>
    /// Builds the absolute remote path from <see cref="RemoteArtifactRoot"/> + the relative artifact
    /// path, rejecting rooted paths, <c>..</c> traversal, and any resolved path that does not stay
    /// under the root.
    /// </summary>
    protected bool TryBuildRemotePath(string remoteRelativePath, out string remotePath)
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

        var candidate = CollapseSlashes($"{RemoteArtifactRoot}/{string.Join('/', segments)}");

        // Confirm the resolved path stays strictly under the root (boundary-safe, not loose prefix).
        if (candidate != RemoteArtifactRoot && !candidate.StartsWith(RemoteArtifactRoot + "/", StringComparison.Ordinal))
        {
            return false;
        }

        remotePath = candidate;
        return true;
    }

    /// <summary>Normalizes a remote root: forward slashes, collapsed, no trailing slash.</summary>
    protected static string NormalizeRoot(string root)
    {
        var collapsed = CollapseSlashes(root.Replace('\\', '/'));
        return collapsed.Length > 1 ? collapsed.TrimEnd('/') : collapsed;
    }

    /// <summary>Collapses repeated slashes, preserving a single leading slash if present.</summary>
    protected static string CollapseSlashes(string value)
    {
        var hadLeadingSlash = value.StartsWith('/');
        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var joined = string.Join('/', segments);
        return hadLeadingSlash ? "/" + joined : joined;
    }
}
