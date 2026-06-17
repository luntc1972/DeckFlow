using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// In-memory test fake for <see cref="ISshArtifactUploader"/> with per-file success/fail
/// injection. References the interface only — never SSH.NET — so the test project stays
/// SSH-free.
/// </summary>
internal sealed class FakeSshArtifactUploader : ISshArtifactUploader
{
    /// <summary>Remote relative paths that should be reported as failed.</summary>
    public HashSet<string> FilesToFail { get; } = new();

    /// <summary>Records each upload request received (success or failure).</summary>
    public List<SshUploadRequest> UploadedFiles { get; } = new();

    public Task<IReadOnlyList<SshUploadResult>> UploadArtifactsAsync(
        IReadOnlyList<SshUploadRequest> uploads,
        IProgress<SshUploadResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SshUploadResult>();
        foreach (var req in uploads)
        {
            UploadedFiles.Add(req);
            var failed = FilesToFail.Contains(req.RemoteRelativePath);
            var result = new SshUploadResult(
                req.LocalPath,
                req.RemoteRelativePath,
                !failed,
                failed ? "injected" : null);
            results.Add(result);
            progress?.Report(result);
        }

        return Task.FromResult<IReadOnlyList<SshUploadResult>>(results);
    }
}
