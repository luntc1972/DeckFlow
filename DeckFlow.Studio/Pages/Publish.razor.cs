using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using Microsoft.AspNetCore.Components;

namespace DeckFlow.Studio.Pages;

/// <summary>
/// Code-behind for the Publish-to-Git page. The git repo-info load, export / artifact-copy / diff /
/// change classification, and stage-and-commit orchestration live in
/// <see cref="PublishCoordinator"/> (H1 split); this page keeps only UI state, busy guards,
/// progress wiring, sanitized error copy mapping, cancellation, and re-render marshalling.
/// Behavior is identical to the prior inline implementation.
/// </summary>
public partial class Publish
{
    // ── Injected services ───────────────────────────────────────────────────
    // Why: all git/orchestrator/store I/O is delegated to the coordinator so this page is thin UI
    // glue and the export/diff classification is unit-testable without bUnit (H1).
    [Inject]
    private PublishCoordinator Coordinator { get; set; } = default!;

    // ── Init state ──────────────────────────────────────────────────────────
    private bool _initInFlight = true;
    private string? _initError;
    private string _repoRoot = string.Empty;
    private string _branch = string.Empty;
    private int _approvedCount;
    // Phase 56 PUB-03: publish-state summary for approved rows.
    private List<(PublishState State, int Count)> _publishStateSummary = new();

    // dataRoot = parent of ArtifactRoot = {studioDataDirectory}; resolved by the coordinator.
    private string _dataRoot = string.Empty;

    // ── Stage 1 state ───────────────────────────────────────────────────────
    private bool _exportInFlight;
    private string _exportError = string.Empty;
    private bool _diffReady;
    private string _rawDiff = string.Empty;
    private int _addedCount;
    private int _updatedCount;
    private int _removedCount;
    private IReadOnlyList<(string Type, string Value)> _exportedKeys = Array.Empty<(string Type, string Value)>();

    // Persisted staged paths for the commit handler ([seedRelative] + copied artifact paths).
    private IReadOnlyList<string> _stagedPaths = Array.Empty<string>();

    // ── Stage 2 state ───────────────────────────────────────────────────────
    private bool _diffReviewed;
    private bool _commitInFlight;
    private bool _commitSuccess;
    private string _commitSha = string.Empty;
    private string _commitError = string.Empty;
    private string _commitMessage = string.Empty;

    // ── Shared in-flight guard ──────────────────────────────────────────────
    private bool _operationInFlight;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Why: Task.Run moves git + store calls off the Blazor sync context (Pitfall 7).
            var initData = await Task.Run(() => Coordinator.LoadInitDataAsync(Cts.Token), Cts.Token);

            _repoRoot = initData.RepoRoot;
            _branch = initData.Branch;
            _approvedCount = initData.ApprovedCount;
            _dataRoot = initData.DataRoot;
            _publishStateSummary = initData.StateSummary.ToList();
            // Placeholder until Export computes the actual delta; commit is disabled until then.
            _commitMessage = "content: publish KB seed";
        }
        catch (OperationCanceledException)
        {
            // Component disposed mid-load — swallow.
        }
        catch (Exception)
        {
            // Why: ResolveRepoRootAsync throws when Studio is not running from inside a git repo
            // (GitCommandException). Surface a clear message and disable export.
            _initError = "Could not compute git diff — check that Studio is running from the repo root.";
        }
        finally
        {
            _initInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Stage 1: Export, Copy Artifacts & Preview Diff ─────────────────────
    private async Task ExportAndDiffAsync()
    {
        if (_operationInFlight || _approvedCount == 0 || _initError is not null)
        {
            return;
        }

        _operationInFlight = true;
        _exportInFlight = true;
        _exportError = string.Empty;
        _diffReady = false;
        _rawDiff = string.Empty;
        _diffReviewed = false;
        _commitSuccess = false;
        _commitError = string.Empty;
        _stagedPaths = Array.Empty<string>();
        _exportedKeys = Array.Empty<(string Type, string Value)>();

        // Why: progress sink uses disposal-safe InvokeAsync pattern (Harvest.razor T-45-18).
        var progress = new ActionOrchestratorProgress(msg =>
            InvokeAsync(() =>
            {
                SafeStateHasChanged();
            }));

        try
        {
            var result = await Task.Run(
                () => Coordinator.ExportAndDiffAsync(_repoRoot, _dataRoot, progress, Cts.Token),
                Cts.Token);

            switch (result.Status)
            {
                case PublishExportStatus.SeedExportFailed:
                    _exportError = $"Seed export failed — {result.SeedExportMessage}";
                    break;
                case PublishExportStatus.ArtifactCopyFailed:
                    _exportError = "Cannot publish — an approved artifact is missing or unreadable. Re-distill or remove that entry, then retry.";
                    break;
                default:
                    _stagedPaths = result.StagedPaths;
                    _rawDiff = result.RawDiff;
                    _addedCount = result.AddedCount;
                    _updatedCount = result.UpdatedCount;
                    _removedCount = result.RemovedCount;
                    _exportedKeys = result.ExportedKeys;
                    _diffReady = true;
                    _commitMessage = result.CommitMessage;
                    break;
            }

            _exportInFlight = false;
            _operationInFlight = false;
            await SafeStateHasChangedAsync();
        }
        catch (OperationCanceledException)
        {
            _exportError = "Export was cancelled.";
            _exportInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception)
        {
            _exportError = "Export failed — check the Studio logs and retry.";
            _exportInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Stage 2: Commit ─────────────────────────────────────────────────────
    private async Task CommitAsync()
    {
        if (!_diffReviewed || _operationInFlight || string.IsNullOrWhiteSpace(_rawDiff) || string.IsNullOrWhiteSpace(_commitMessage))
        {
            return;
        }

        _operationInFlight = true;
        _commitInFlight = true;
        _commitError = string.Empty;
        _commitSuccess = false;

        try
        {
            // Why: the coordinator stages ONLY the repo-relative seed + copied artifact paths
            // (never -A) and throws GitForeignStagedChangesException if unrelated paths are already
            // staged. The pathspec-scoped commit is defense-in-depth. (D-01/D-02/D-03).
            var result = await Task.Run(
                () => Coordinator.CommitAsync(_repoRoot, _stagedPaths, _commitMessage, _exportedKeys, Cts.Token),
                Cts.Token);

            _commitSha = result.Sha;
            _commitSuccess = true;

            // After a successful commit, disable the Commit button until a new export cycle runs.
            _diffReady = false;
            _diffReviewed = false;
            _stagedPaths = Array.Empty<string>();
        }
        catch (OperationCanceledException)
        {
            _commitError = "Commit was cancelled.";
        }
        catch (GitForeignStagedChangesException ex)
        {
            // Why: catch the foreign-staged guard FIRST before the broader GitCommandException
            // (GitForeignStagedChangesException : GitCommandException). Surface the specific
            // "unrelated changes" message so the operator knows to unstage and retry.
            _commitError = $"Cannot commit — unrelated changes are already staged: {ex.Message}. Unstage them (git reset) and retry. No files were changed.";
        }
        catch (GitCommandException ex)
        {
            _commitError = $"Commit failed — {ex.Message}. No files were changed.";
        }
        finally
        {
            _commitInFlight = false;
            _operationInFlight = false;
            await SafeStateHasChangedAsync();
        }
    }
}
