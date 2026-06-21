using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// In-memory test fake for <see cref="ISshArtifactDownloader"/> with per-file success/fail
/// injection. On a successful download it writes a placeholder file into the staging root so the
/// page's artifact-promotion <c>File.Move</c> has a real file to move. References the interface
/// only — never SSH.NET — so the test project stays SSH-free.
/// </summary>
internal sealed class FakeSshArtifactDownloader : ISshArtifactDownloader
{
    /// <summary>Remote relative paths that should be reported as failed (no file written).</summary>
    public HashSet<string> FilesToFail { get; } = new();

    /// <summary>Records each download request received (success or failure).</summary>
    public List<SshDownloadRequest> DownloadedFiles { get; } = new();

    /// <summary>Failure reason returned for failed files; may carry a sentinel for the leak test.</summary>
    public string FailureReason { get; set; } = "injected";

    public Task<IReadOnlyList<SshDownloadResult>> DownloadArtifactsAsync(
        IReadOnlyList<SshDownloadRequest> downloads,
        string localStagingRoot,
        IProgress<SshDownloadResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SshDownloadResult>();
        foreach (var req in downloads)
        {
            DownloadedFiles.Add(req);
            var failed = FilesToFail.Contains(req.RemoteRelativePath);

            string localPath = string.Empty;
            if (!failed)
            {
                localPath = Path.Combine(localStagingRoot, req.LocalRelativePath);
                var dir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(localPath, "staged artifact placeholder");
            }

            var result = new SshDownloadResult(
                req.RemoteRelativePath,
                localPath,
                !failed,
                failed ? FailureReason : null);
            results.Add(result);
            progress?.Report(result);
        }

        return Task.FromResult<IReadOnlyList<SshDownloadResult>>(results);
    }
}
