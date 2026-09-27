using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Exports approved content to the configured private KB root.
/// </summary>
public sealed class PublishCoordinator
{
    private readonly IPrivateKbRootProvider _privateKbRootProvider;
    private readonly IContentKbOrchestrator _orchestrator;
    private readonly IContentSiteIndexStore _indexStore;
    private readonly ContentKbOrchestratorOptions _options;
    private readonly PublishStateDeriver _deriver;
    private readonly CreatorSuppressionRowFilter _suppressionFilter;

    /// <summary>Creates the coordinator with a lazy private-root resolver.</summary>
    public PublishCoordinator(
        IPrivateKbRootProvider privateKbRootProvider,
        IContentKbOrchestrator orchestrator,
        IContentSiteIndexStore indexStore,
        ContentKbOrchestratorOptions options,
        PublishStateDeriver deriver,
        CreatorSuppressionRowFilter suppressionFilter)
    {
        ArgumentNullException.ThrowIfNull(privateKbRootProvider);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(indexStore);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(deriver);
        _privateKbRootProvider = privateKbRootProvider;
        _orchestrator = orchestrator;
        _indexStore = indexStore;
        _options = options;
        _deriver = deriver;
        _suppressionFilter = suppressionFilter;
    }

    /// <summary>
    /// Reads approved rows, derives the publish-state
    /// summary, and resolves the data root (parent of <c>ArtifactRoot</c>, which already carries the
    /// content-kb/ segment — D-01/D-03/D-10).
    /// </summary>
    public async Task<PublishInitData> LoadInitDataAsync(CancellationToken cancellationToken)
    {
        var rows = await _suppressionFilter.GetAllowedAsync(await _indexStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var summary = rows
            .GroupBy(r => _deriver.Derive(r.PushedToProdUtc, r.IsVisible, r.IndexedUtc))
            .Select(g => (State: g.Key, Count: g.Count()))
            .OrderBy(x => x.State)
            .ToList();
        var dataRoot = Path.GetDirectoryName(_options.ArtifactRoot) ?? _options.ArtifactRoot;
        return new PublishInitData(rows.Count, dataRoot, summary);
    }

    /// <summary>
    /// Writes the approved-only seed and copies approved artifacts into the private KB root. Returns
    /// a status discriminating seed-export failure and artifact-copy failure from success so the page
    /// can surface the matching operator copy. Throws
    /// <see cref="OperationCanceledException"/> on cancellation; other exceptions propagate to the
    /// page's generic handler.
    /// </summary>
    public async Task<PublishExportResult> ExportToPrivateRootAsync(
        string dataRoot,
        IOrchestratorProgress progress,
        CancellationToken cancellationToken)
    {
        var privateKbRoot = _privateKbRootProvider.GetRoot();
        var seedAbsPath = privateKbRoot.SeedFile;

        // Step 1: Write the approved-only LF seed into the private root.
        var exportResult = await _orchestrator.ExportIndexToFileAsync(seedAbsPath, progress, cancellationToken).ConfigureAwait(false);
        if (!exportResult.Success)
        {
            return PublishExportResult.SeedExportFailure(exportResult.Message ?? string.Empty);
        }

        // Step 2: Copy approved artifacts from the Studio data root into the private root.
        // CopyApprovedArtifactsToRepoAsync containment-guards both ends and throws (publish-blocking)
        // on a missing source (D-01/D-03/D-10).
        IReadOnlyList<string> copiedArtifactPaths;
        try
        {
            copiedArtifactPaths = await _orchestrator.CopyApprovedArtifactsToRepoAsync(
                dataRoot, privateKbRoot.Root, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return PublishExportResult.ArtifactCopyFailure();
        }

        var writtenPaths = new List<string> { Path.GetRelativePath(privateKbRoot.Root, privateKbRoot.SeedFile) };
        writtenPaths.AddRange(copiedArtifactPaths);
        return PublishExportResult.SuccessResult(writtenPaths.AsReadOnly());
    }

}

/// <summary>Approved-row summary resolved for the Publish page on load.</summary>
/// <param name="ApprovedCount">Count of approved rows ready to publish.</param>
/// <param name="DataRoot">Studio data root (parent of <c>ArtifactRoot</c>).</param>
/// <param name="StateSummary">Per-publish-state counts across the approved rows, ordered by state.</param>
public sealed record PublishInitData(
    int ApprovedCount,
    string DataRoot,
    IReadOnlyList<(PublishState State, int Count)> StateSummary);

/// <summary>Discriminates the outcome of the Publish export-and-diff stage.</summary>
public enum PublishExportStatus
{
    /// <summary>Seed export and artifact copy completed.</summary>
    Success,

    /// <summary>The orchestrator reported the seed export itself failed.</summary>
    SeedExportFailed,

    /// <summary>An approved artifact was missing or unreadable during the private-root copy.</summary>
    ArtifactCopyFailed,
}

/// <summary>
/// Result of <see cref="PublishCoordinator.ExportToPrivateRootAsync"/>.
/// </summary>
/// <param name="Status">Outcome discriminator.</param>
/// <param name="SeedExportMessage">Orchestrator message when <see cref="PublishExportStatus.SeedExportFailed"/>.</param>
/// <param name="WrittenPaths">Private-root-relative paths written on success.</param>
public sealed record PublishExportResult(
    PublishExportStatus Status,
    string SeedExportMessage,
    IReadOnlyList<string> WrittenPaths)
{
    /// <summary>Builds a seed-export-failure result carrying the orchestrator message.</summary>
    public static PublishExportResult SeedExportFailure(string message) => new(
        PublishExportStatus.SeedExportFailed,
        message,
        Array.Empty<string>());

    /// <summary>Builds an artifact-copy-failure result.</summary>
    public static PublishExportResult ArtifactCopyFailure() => new(
        PublishExportStatus.ArtifactCopyFailed,
        string.Empty,
        Array.Empty<string>());

    /// <summary>Builds a success result listing every private-root-relative output.</summary>
    public static PublishExportResult SuccessResult(IReadOnlyList<string> writtenPaths) => new(
        PublishExportStatus.Success,
        string.Empty,
        writtenPaths);
}
