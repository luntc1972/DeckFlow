using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Studio;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;

namespace DeckFlow.Studio.Pages;

/// <summary>
/// Code-behind for the Direct Push (publish-to-production) page. The prod read / diff / upload /
/// transactional write orchestration lives in <see cref="DirectPushCoordinator"/> (H1 split); this
/// page keeps only UI state, busy guards, sanitized error copy, exception logging, cancellation,
/// and re-render marshalling. Behavior is identical to the prior inline implementation.
/// </summary>
public partial class DirectPush
{
    // ── Injected services ───────────────────────────────────────────────────
    // Why: all prod/SCP/store I/O is delegated to the coordinator so this page is thin UI glue and
    // the orchestration is unit-testable without bUnit (H1).
    [Inject]
    private DirectPushCoordinator Coordinator { get; set; } = default!;

    [Inject]
    private StudioConfig Config { get; set; } = default!;

    // Why: M3 — logs caught exceptions to the Serilog file/console sink so "see logs" guidance
    // in sanitized UI messages is actually true. Never log ex.Message to the UI (D-07 / SC5).
    [Inject]
    private ILogger<DirectPush> Logger { get; set; } = default!;

    // ── Init state ──────────────────────────────────────────────────────────
    private bool _initInFlight = true;
    private string? _initError;
    private int _approvedCount;

    // dataRoot = parent of ArtifactRoot = {studioDataDirectory}; resolved by the coordinator.
    private string _dataRoot = string.Empty;

    // ── Shared in-flight guard ──────────────────────────────────────────────
    private bool _operationInFlight;

    // ── Stage 1 — compute diff ──────────────────────────────────────────────
    private bool _diffComputeInFlight;
    private string _diffError = string.Empty;
    private bool _diffReady;
    private int _newCount;
    private int _updatedCount;
    private int _unchangedCount;

    // Why (M2): only New + Updated rows are uploaded and written; Unchanged rows are skipped
    // entirely. _publishRows and _diffRows are parallel (same set, same order) — both come from the
    // coordinator's classification in a single pass.
    private IReadOnlyList<ContentSiteIndexRow> _publishRows = Array.Empty<ContentSiteIndexRow>();

    // Per-row diff display rows (New + Updated only, shown in the diff table). Read-only: built once
    // by the coordinator, parallel to _publishRows; never mutated here.
    private IReadOnlyList<DirectPushDiffRow> _diffRows = Array.Empty<DirectPushDiffRow>();

    // ── Confirmation gate (D-09) ────────────────────────────────────────────
    private bool _prodReviewed;

    // ── Stage 2 — SCP upload ────────────────────────────────────────────────
    private bool _scpInFlight;
    private bool _scpSuccess;
    private string _scpError = string.Empty;
    private List<SshUploadResult> _fileResults = new();

    // ── Stage 3 — DB upsert (gated on _scpSuccess) ─────────────────────────
    private bool _dbInFlight;
    private bool _dbSuccess;
    private string _dbError = string.Empty;
    private List<RowResult> _rowResults = new();

    private sealed record RowResult(string Title, string KeyType, string KeyValue, bool Success, string? Reason);

    // ── Lifecycle ──────────────────────────────────────────────────────────
    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Why: Task.Run moves store calls off the Blazor sync context (Pitfall 7).
            var initData = await Task.Run(() => Coordinator.LoadInitDataAsync(Cts.Token), Cts.Token);
            _approvedCount = initData.ApprovedCount;
            _dataRoot = initData.DataRoot;
        }
        catch (OperationCanceledException)
        {
            // Component disposed mid-load — swallow.
        }
        catch (Exception)
        {
            // Why: never include any secret value in the init error (SC5 / D-07).
            _initError = "Initialization failed — check Studio configuration and retry.";
        }
        finally
        {
            _initInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Stage 1: Compute Prod Diff ──────────────────────────────────────────
    private async Task ComputeDiffAsync()
    {
        if (_operationInFlight || _approvedCount == 0
            || !Config.IsProdConfigured || !Config.IsScpConfigured)
        {
            return;
        }

        _operationInFlight = true;
        _diffComputeInFlight = true;
        _diffError = string.Empty;
        _diffReady = false;
        _prodReviewed = false;
        _scpSuccess = false;
        _scpError = string.Empty;
        _fileResults = new();
        _dbSuccess = false;
        _dbError = string.Empty;
        _rowResults = new();
        _publishRows = Array.Empty<ContentSiteIndexRow>();
        _unchangedCount = 0;

        try
        {
            await Task.Run(async () =>
            {
                // Why (M2/H3): coordinator reads local + prod (read-only, no DDL) and runs the
                // content-aware classification; Unchanged rows are excluded from the publish set.
                var diff = await Coordinator.ComputeDiffAsync(Cts.Token).ConfigureAwait(false);

                await InvokeAsync(() =>
                {
                    _publishRows = diff.PublishRows;
                    _diffRows = diff.DiffRows;
                    _newCount = diff.NewCount;
                    _updatedCount = diff.UpdatedCount;
                    _unchangedCount = diff.UnchangedCount;
                    _diffReady = true;
                    _diffComputeInFlight = false;
                    _operationInFlight = false;
                    SafeStateHasChanged();
                });
            }, Cts.Token);
        }
        catch (OperationCanceledException)
        {
            _diffError = "Diff was cancelled.";
            _diffComputeInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            // Why: M3 — log full exception to the Serilog sink BEFORE setting the sanitized UI
            // message; ex.Message can carry host/db/credentials (D-07 / SC5 / Codex HIGH-2).
            Logger.LogError(ex, "Compute Prod Diff failed");
            _diffError = "Could not read production — check the prod connection configuration and try again. Nothing was written.";
            _diffComputeInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Stage 2: Upload Artifacts (SCP) ─────────────────────────────────────
    private async Task UploadArtifactsAsync()
    {
        if (!_prodReviewed || _operationInFlight || !_diffReady)
        {
            return;
        }

        _operationInFlight = true;
        _scpInFlight = true;
        _scpError = string.Empty;
        _scpSuccess = false;
        _fileResults = new();

        try
        {
            await Task.Run(async () =>
            {
                // Progress streams per-file results into _fileResults via disposal-safe InvokeAsync.
                var progress = new Progress<SshUploadResult>(result =>
                {
                    _ = InvokeAsync(() =>
                    {
                        _fileResults.Add(result);
                        SafeStateHasChanged();
                    });
                });

                var results = await Coordinator
                    .UploadArtifactsAsync(_publishRows, _dataRoot, progress, Cts.Token)
                    .ConfigureAwait(false);

                var allOk = results.All(r => r.Success);

                await InvokeAsync(() =>
                {
                    _fileResults = results.ToList();
                    _scpSuccess = allOk;
                    if (!allOk)
                    {
                        _scpError = "Artifact upload finished with failures — see the per-file list below. " +
                                    "The database step stays locked. Fix the failed files and re-run upload.";
                    }

                    _scpInFlight = false;
                    _operationInFlight = false;
                    SafeStateHasChanged();
                });
            }, Cts.Token);
        }
        catch (OperationCanceledException)
        {
            _scpError = "Upload was cancelled.";
            _scpInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            // Why: M3 — SshException.Message may contain hostname / remote path (D-07);
            // log to sink only, never surface ex.Message to the UI.
            Logger.LogError(ex, "Artifact SCP upload failed");
            _scpError = "SSH connection failed — check SCP configuration and Render SSH access.";
            _scpInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Stage 3: Write Approved Rows to Prod DB (gated on _scpSuccess) ───────
    private async Task WriteRowsAsync()
    {
        // Why: hard-guard before any prod write — a stale render, test invocation, or future
        // refactor must never reach the upsert before full SCP success (Codex MEDIUM-1). The
        // disabled button alone is not sufficient.
        if (!_scpSuccess || _operationInFlight || !_diffReady)
        {
            return;
        }

        _operationInFlight = true;
        _dbInFlight = true;
        _dbError = string.Empty;
        _dbSuccess = false;
        _rowResults = new();

        try
        {
            await Task.Run(async () =>
            {
                // Why (H4): coordinator runs the single transactional batch upsert + prod-first
                // stamp/visibility, all-or-nothing. Throws on any row failure (rolled back).
                await Coordinator.WritePublishAsync(_publishRows, Cts.Token).ConfigureAwait(false);

                // All rows succeeded — _diffRows is parallel to _publishRows (New + Updated set).
                var successResults = _diffRows
                    .Select(d => new RowResult(d.Title, d.KeyType, d.KeyValue, true, null))
                    .ToList();

                await InvokeAsync(() =>
                {
                    _rowResults = successResults;
                    _dbSuccess = true;
                    _dbInFlight = false;
                    _operationInFlight = false;
                    SafeStateHasChanged();
                });
            }, Cts.Token);
        }
        catch (ContentSiteIndexBatchUpsertException ex)
        {
            // Why: ContentSiteIndexBatchUpsertException carries only the failing row's non-secret
            // title; the underlying DB exception (host/db/credentials) stays in InnerException and
            // goes only to the log sink (D-07 / SC5 / T-qyc-02). The entire batch was rolled back
            // on any row failure — stamp/visibility MUST NOT run (PUB-01 / T-qyc-04).
            Logger.LogError(ex, "Prod batch upsert rolled back at row {Title}", ex.FailedRowTitle);
            var rollbackResults = _diffRows
                .Select(d => new RowResult(d.Title, d.KeyType, d.KeyValue, false, "Rolled back — not written"))
                .ToList();
            _rowResults = rollbackResults;
            _dbError = $"Row '{ex.FailedRowTitle}' failed — the entire batch was rolled back. " +
                       "NOTHING was written to production. See logs.";
            _dbInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            _dbError = "DB write was cancelled.";
            _dbInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            // Why: M3 — Npgsql can carry host/db/user in ex.Message (D-07 / SC5); log full
            // exception to the sink only, surface sanitized copy to the UI.
            Logger.LogError(ex, "Prod DB write failed");
            _dbError = "Database write failed — check the prod connection configuration and try again.";
            _dbInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Test seam (Codex MEDIUM-1) ──────────────────────────────────────────
    // Why: exercises the WriteRowsAsync hard-guard directly. bUnit will not dispatch a click to a
    // disabled button, so the guard (which protects against a stale render / future refactor
    // reaching the prod upsert before SCP success) is unreachable through the UI in a test. This
    // internal method lets the test invoke the handler in the pre-SCP state and assert no upsert
    // ran. It calls the exact production handler — no behavior is duplicated.
    internal Task InvokeWriteRowsForTest() => WriteRowsAsync();
}
