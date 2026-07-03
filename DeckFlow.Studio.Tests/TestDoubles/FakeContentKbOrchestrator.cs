using DeckFlow.Core.Orchestration;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Minimal fake for <see cref="IContentKbOrchestrator"/>.
/// ExportIndexToFileAsync, CopyApprovedArtifactsToRepoAsync, ExportIndexAsync, and the three
/// content maintenance methods are wired to canned returns; all other inherited interface
/// members throw NotImplementedException.
/// </summary>
internal sealed class FakeContentKbOrchestrator : IContentKbOrchestrator
{
    // ── Canned returns ──────────────────────────────────────────────────────
    public ContentIndexExportResult CannedExportResult { get; set; } = new()
    {
        Success = true,
        RowCount = 1,
        Rows = Array.Empty<ContentIndexExportRow>(),
    };

    public IReadOnlyList<string> CannedCopiedArtifactPaths { get; set; } =
        new[] { "content-kb/youtube_channel/abc123.md" };

    public BlockedVideoListResult CannedBlockedResult { get; set; } = new();

    public ContentMaintenanceResult CannedMaintenanceResult { get; set; } = new() { Success = true };

    // ── Call recording ──────────────────────────────────────────────────────
    public List<string> ExportToFilePaths { get; } = new();
    public int CopyApprovedCallCount { get; private set; }
    public List<IReadOnlyList<string>> CopyArtifactsCalls { get; } = new();
    public List<string> UnblockCalls { get; } = new();
    public List<string> BlockCalls { get; } = new();

    // ── Fault injection ─────────────────────────────────────────────────────
    public Exception? ThrowOnCopy { get; set; }

    // ── IContentIndexExporter (exercised by Publish.razor) ──────────────────
    public Task<ContentIndexExportResult> ExportIndexToFileAsync(
        string seedPath,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        ExportToFilePaths.Add(seedPath);
        return Task.FromResult(CannedExportResult);
    }

    public Task<IReadOnlyList<string>> CopyApprovedArtifactsToRepoAsync(
        string dataRoot,
        string repoRoot,
        CancellationToken cancellationToken = default)
    {
        CopyApprovedCallCount++;
        if (ThrowOnCopy is not null)
        {
            throw ThrowOnCopy;
        }

        return Task.FromResult(CannedCopiedArtifactPaths);
    }

    public Task<IReadOnlyList<string>> CopyArtifactsToRepoAsync(
        string dataRoot,
        string repoRoot,
        IReadOnlyList<string> artifactPaths,
        CancellationToken cancellationToken = default)
    {
        CopyArtifactsCalls.Add(artifactPaths);
        if (ThrowOnCopy is not null)
        {
            throw ThrowOnCopy;
        }

        // Why: echo the requested paths so tests can assert the commit stages exactly the pushed
        // bodies (not a canned set) — the real orchestrator returns the copied paths verbatim.
        return Task.FromResult(artifactPaths);
    }

    public Task<ContentIndexExportResult> ExportIndexAsync(
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(CannedExportResult);

    // ── IHarvestOrchestrator ────────────────────────────────────────────────
    public Task<HarvestResult> HarvestAsync(
        int limit,
        IReadOnlyList<string>? videoIds = null,
        long? sourceId = null,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    // ── IDistillOrchestrator ────────────────────────────────────────────────
    public Task<DistillResult> DistillAsync(
        int limit,
        bool dryRun,
        bool isSubscriptionProvider,
        bool redistill = false,
        IReadOnlyList<string>? videoIds = null,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    // ── IContentMaintenanceOrchestrator ─────────────────────────────────────
    public Task<ContentMaintenanceResult> BlockVideoAsync(
        string youtubeVideoId,
        string? reason,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        BlockCalls.Add(youtubeVideoId);
        return Task.FromResult(CannedMaintenanceResult);
    }

    public Task<ContentMaintenanceResult> UnblockVideoAsync(
        string youtubeVideoId,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        UnblockCalls.Add(youtubeVideoId);
        return Task.FromResult(CannedMaintenanceResult);
    }

    public Task<ContentMaintenanceResult> ResetCorpusAsync(
        bool dryRun,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<BlockedVideoListResult> ListBlockedAsync(
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(CannedBlockedResult);

    // ── IContentSourceManager ───────────────────────────────────────────────
    public Task<ContentSourceResult> AddSourceAsync(
        string url,
        string name,
        string type,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ContentSourceResult> SetSourceEnabledAsync(
        long id,
        bool enabled,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ContentSourceResult> EnsureYoutubeSourceAsync(
        string url,
        string name,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
