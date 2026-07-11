using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Orchestration for the Publish-to-Git workflow, extracted from the <c>Publish</c> page code-behind
/// (H1 god-component split). Owns the git repo-info load, the export / artifact-copy / diff / change
/// classification sequence, and the stage-and-commit + local stamp. This type performs no rendering
/// and holds no per-page UI state — the page keeps all busy guards, error-copy mapping, progress
/// wiring, cancellation, and <c>StateHasChanged</c>. Behavior is identical to the prior inline
/// implementation.
/// </summary>
public sealed class PublishCoordinator
{
    private readonly IGitRepository _git;
    private readonly IContentKbOrchestrator _orchestrator;
    private readonly IContentSiteIndexStore _indexStore;
    private readonly ContentKbOrchestratorOptions _options;
    private readonly PublishStateDeriver _deriver;

    // ── Pinned serializer options for canonical per-row JSON comparison ──────
    // Why: ContentIndexExportRow tag props are IReadOnlyList<string>; record == compares list
    // references not contents, miscounting unchanged rows as Updated. Compare canonical per-row
    // JSON instead. Options match the Phase 42 byte-shape (camelCase + indented).
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>Creates the coordinator with the git repository, orchestrator, store, options, and state deriver.</summary>
    public PublishCoordinator(
        IGitRepository git,
        IContentKbOrchestrator orchestrator,
        IContentSiteIndexStore indexStore,
        ContentKbOrchestratorOptions options,
        PublishStateDeriver deriver)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(indexStore);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(deriver);
        _git = git;
        _orchestrator = orchestrator;
        _indexStore = indexStore;
        _options = options;
        _deriver = deriver;
    }

    /// <summary>
    /// Resolves the repo root + current branch, reads the approved rows, derives the publish-state
    /// summary, and resolves the data root (parent of <c>ArtifactRoot</c>, which already carries the
    /// content-kb/ segment — D-01/D-03/D-10).
    /// </summary>
    public async Task<PublishInitData> LoadInitDataAsync(CancellationToken cancellationToken)
    {
        var repoRoot = await _git.ResolveRepoRootAsync(StudioRepoLocator.ResolveStartDirectory(), cancellationToken).ConfigureAwait(false);
        var branch = await _git.GetCurrentBranchAsync(repoRoot, cancellationToken).ConfigureAwait(false);
        var rows = await _indexStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false);
        var summary = rows
            .GroupBy(r => _deriver.Derive(r.PushedToProdUtc, r.IsVisible, r.IndexedUtc))
            .Select(g => (State: g.Key, Count: g.Count()))
            .OrderBy(x => x.State)
            .ToList();
        var dataRoot = Path.GetDirectoryName(_options.ArtifactRoot) ?? _options.ArtifactRoot;
        return new PublishInitData(repoRoot, branch, rows.Count, dataRoot, summary);
    }

    /// <summary>
    /// Stage 1: writes the approved-only seed into the repo tree, copies approved artifacts into the
    /// repo, computes the raw git diff for the staged paths, and classifies added / updated / removed
    /// rows via canonical per-row JSON. Returns a status discriminating seed-export failure and
    /// artifact-copy failure from success so the page can surface the matching operator copy. Throws
    /// <see cref="OperationCanceledException"/> on cancellation; other exceptions propagate to the
    /// page's generic handler.
    /// </summary>
    public async Task<PublishExportResult> ExportAndDiffAsync(
        string repoRoot,
        string dataRoot,
        IOrchestratorProgress progress,
        CancellationToken cancellationToken)
    {
        // SEED must land in the REPO tree (NOT the data dir): ArtifactPath already carries
        // content-kb/; the seed is written under repoRoot so the committed seed is the repo's file
        // (D-01/D-03/D-10).
        var seedAbsPath = Path.GetFullPath(Path.Combine(repoRoot, ContentKbSeedPaths.SeedRelativePath));

        // Step 1: Write the approved-only LF seed into the repo tree.
        var exportResult = await _orchestrator.ExportIndexToFileAsync(seedAbsPath, progress, cancellationToken).ConfigureAwait(false);
        if (!exportResult.Success)
        {
            return PublishExportResult.SeedExportFailure(exportResult.Message ?? string.Empty);
        }

        // Step 2: COPY APPROVED ARTIFACTS INTO REPO (D-03). Copy from the Studio data root (parent of
        // ArtifactRoot) into repoRoot/content-kb so the commit ships real files.
        // CopyApprovedArtifactsToRepoAsync containment-guards both ends and throws (publish-blocking)
        // on a missing source (D-01/D-03/D-10).
        IReadOnlyList<string> copiedArtifactPaths;
        try
        {
            copiedArtifactPaths = await _orchestrator.CopyApprovedArtifactsToRepoAsync(
                dataRoot, repoRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return PublishExportResult.ArtifactCopyFailure();
        }

        // Step 3: Build the staged repo-relative path list = [seedRelative] + copiedArtifactPaths.
        var staged = new List<string> { ContentKbSeedPaths.SeedRelativePath };
        staged.AddRange(copiedArtifactPaths);
        var stagedReadOnly = staged.AsReadOnly();

        // Step 4: Get the raw git diff for the staged paths.
        var rawDiff = await _git.DiffAsync(repoRoot, stagedReadOnly, cancellationToken).ConfigureAwait(false);

        // Step 5: In-memory counts via CANONICAL PER-ROW JSON (never record/list-reference equality).
        var headSeedText = await _git.CatHeadSeedAsync(repoRoot, ContentKbSeedPaths.SeedRelativePath, cancellationToken).ConfigureAwait(false);
        List<ContentIndexExportRow> headRows;
        if (string.IsNullOrEmpty(headSeedText))
        {
            headRows = new List<ContentIndexExportRow>();
        }
        else
        {
            headRows = JsonSerializer.Deserialize<List<ContentIndexExportRow>>(headSeedText, CanonicalJsonOptions)
                ?? new List<ContentIndexExportRow>();
        }

        // Build new rows from the export result (same approved set that was just written).
        var newRows = exportResult.Rows;
        var exportedKeys = newRows
            .Select(r => (r.NaturalKeyType, r.NaturalKeyValue))
            .ToList();

        // Key both sets by (NaturalKeyType, NaturalKeyValue) for comparison.
        var headByKey = headRows.ToDictionary(
            r => (r.NaturalKeyType, r.NaturalKeyValue),
            Canonical);
        var newByKey = newRows.ToDictionary(
            r => (r.NaturalKeyType, r.NaturalKeyValue),
            Canonical);

        int added = 0;
        int updated = 0;
        int removed = 0;

        foreach (var key in newByKey.Keys)
        {
            if (!headByKey.ContainsKey(key))
            {
                added++;
            }
            else if (newByKey[key] != headByKey[key])
            {
                updated++;
            }
        }

        foreach (var key in headByKey.Keys)
        {
            if (!newByKey.ContainsKey(key))
            {
                removed++;
            }
        }

        // Why: report the DELTA (what this commit changes), not the full seed size — a cumulative
        // "{N} entries" total reads as if N were published when only a few changed.
        var commitMessage = $"content: publish KB seed ({added} added, {updated} updated, {removed} removed)";

        return PublishExportResult.SuccessResult(
            stagedReadOnly,
            rawDiff,
            added,
            updated,
            removed,
            exportedKeys,
            commitMessage);
    }

    /// <summary>
    /// Stage 2: stages ONLY the supplied repo-relative paths (never <c>-A</c>) and commits with the
    /// given message, then stamps the exported keys as pushed-to-prod locally. Commit success is the
    /// publish boundary (PUB-01/HIGH-2); a failed/cancelled local stamp is non-fatal and reported via
    /// <see cref="PublishCommitResult.LocalStampFailed"/>. Git failures
    /// (<see cref="GitForeignStagedChangesException"/>, <see cref="GitCommandException"/>,
    /// <see cref="OperationCanceledException"/>) propagate to the page for sanitized copy mapping.
    /// </summary>
    public async Task<PublishCommitResult> CommitAsync(
        string repoRoot,
        IReadOnlyList<string> stagedPaths,
        string commitMessage,
        IReadOnlyList<(string Type, string Value)> exportedKeys,
        CancellationToken cancellationToken)
    {
        var sha = await _git.StageAndCommitAsync(repoRoot, stagedPaths, commitMessage, cancellationToken).ConfigureAwait(false);

        var localStampFailed = false;
        if (exportedKeys.Count > 0)
        {
            try
            {
                await _indexStore.StampPushedToProdAsync(
                    exportedKeys,
                    DateTimeOffset.UtcNow,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Why: commit already landed; a failed/cancelled local stamp is non-fatal (WR-01/WR-02).
                // Surface only a boolean; the page maps it to sanitized copy (WR-03 hygiene).
                localStampFailed = true;
            }
        }

        return new PublishCommitResult(sha, localStampFailed);
    }

    private static string Canonical(ContentIndexExportRow row) =>
        JsonSerializer.Serialize(row, CanonicalJsonOptions);
}

/// <summary>Repository info + approved-row summary resolved for the Publish page on load.</summary>
/// <param name="RepoRoot">Absolute path to the git repository root.</param>
/// <param name="Branch">Current git branch name.</param>
/// <param name="ApprovedCount">Count of approved rows ready to publish.</param>
/// <param name="DataRoot">Studio data root (parent of <c>ArtifactRoot</c>).</param>
/// <param name="StateSummary">Per-publish-state counts across the approved rows, ordered by state.</param>
public sealed record PublishInitData(
    string RepoRoot,
    string Branch,
    int ApprovedCount,
    string DataRoot,
    IReadOnlyList<(PublishState State, int Count)> StateSummary);

/// <summary>Discriminates the outcome of the Publish export-and-diff stage.</summary>
public enum PublishExportStatus
{
    /// <summary>Seed export, artifact copy, diff, and classification all completed.</summary>
    Success,

    /// <summary>The orchestrator reported the seed export itself failed.</summary>
    SeedExportFailed,

    /// <summary>An approved artifact was missing or unreadable during the repo copy.</summary>
    ArtifactCopyFailed,
}

/// <summary>
/// Result of <see cref="PublishCoordinator.ExportAndDiffAsync"/>. On <see cref="PublishExportStatus.Success"/>
/// the staged paths, raw diff, change counts, exported keys, and commit message are populated; on a
/// failure status only the relevant message is meaningful.
/// </summary>
/// <param name="Status">Outcome discriminator.</param>
/// <param name="SeedExportMessage">Orchestrator message when <see cref="PublishExportStatus.SeedExportFailed"/>.</param>
/// <param name="StagedPaths">Repo-relative staged paths (seed + copied artifacts) on success.</param>
/// <param name="RawDiff">Raw git diff text for the staged paths on success.</param>
/// <param name="AddedCount">Rows present in the new seed but not at HEAD.</param>
/// <param name="UpdatedCount">Rows present in both but with changed canonical content.</param>
/// <param name="RemovedCount">Rows present at HEAD but absent from the new seed.</param>
/// <param name="ExportedKeys">Natural keys of the exported rows, for the post-commit stamp.</param>
/// <param name="CommitMessage">Delta-describing commit message.</param>
public sealed record PublishExportResult(
    PublishExportStatus Status,
    string SeedExportMessage,
    IReadOnlyList<string> StagedPaths,
    string RawDiff,
    int AddedCount,
    int UpdatedCount,
    int RemovedCount,
    IReadOnlyList<(string Type, string Value)> ExportedKeys,
    string CommitMessage)
{
    /// <summary>Builds a seed-export-failure result carrying the orchestrator message.</summary>
    public static PublishExportResult SeedExportFailure(string message) => new(
        PublishExportStatus.SeedExportFailed,
        message,
        Array.Empty<string>(),
        string.Empty,
        0,
        0,
        0,
        Array.Empty<(string, string)>(),
        string.Empty);

    /// <summary>Builds an artifact-copy-failure result.</summary>
    public static PublishExportResult ArtifactCopyFailure() => new(
        PublishExportStatus.ArtifactCopyFailed,
        string.Empty,
        Array.Empty<string>(),
        string.Empty,
        0,
        0,
        0,
        Array.Empty<(string, string)>(),
        string.Empty);

    /// <summary>Builds a success result with the computed diff, counts, keys, and commit message.</summary>
    public static PublishExportResult SuccessResult(
        IReadOnlyList<string> stagedPaths,
        string rawDiff,
        int addedCount,
        int updatedCount,
        int removedCount,
        IReadOnlyList<(string Type, string Value)> exportedKeys,
        string commitMessage) => new(
        PublishExportStatus.Success,
        string.Empty,
        stagedPaths,
        rawDiff,
        addedCount,
        updatedCount,
        removedCount,
        exportedKeys,
        commitMessage);
}

/// <summary>
/// Result of <see cref="PublishCoordinator.CommitAsync"/>.
/// </summary>
/// <param name="Sha">The commit SHA produced by the stage-and-commit.</param>
/// <param name="LocalStampFailed">True when the commit landed but the local pushed-to-prod stamp did not complete.</param>
public sealed record PublishCommitResult(string Sha, bool LocalStampFailed);
