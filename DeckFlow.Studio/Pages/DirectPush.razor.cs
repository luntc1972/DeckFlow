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
    private CreatorSuppressionSyncCoordinator SuppressionSync { get; set; } = default!;

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

    /// <summary>Tracks one content row's publish-stage result for Direct Push status lists.</summary>
    private sealed record RowResult(string Title, string KeyType, string KeyValue, bool Success, string? Reason);

    // ── Publish to private KB root (gated on SCP and DB success) ───────────
    private bool _verifyInFlight;
    private bool _verifyRanOnce;
    private string _verifyError = string.Empty;
    private List<RowResult> _confirmedResults = new();
    private List<RowResult> _notConfirmedResults = new();

    // ── Awaiting-confirm resume bucket (D-10) ────────────────────────────────
    // Why: a mid-flight publish (content upserted, not yet published) is durable via the local
    // AwaitingConfirmUtc marker (Plan 90-03) and survives a page reload. ClassifyDiff would
    // reclassify a content-matching-but-hidden row as Unchanged and drop it from PublishRows
    // (90-RESEARCH Pitfall 4), so this bucket is populated independently of the diff and refreshed
    // after every stage that can change the marker set (Stage 3 sets it, Publish/resume clear it).
    private IReadOnlyList<ContentSiteIndexRow> _awaitingConfirmRows = Array.Empty<ContentSiteIndexRow>();
    private bool _resumeVerifyInFlight;
    private string _resumeVerifyError = string.Empty;
    private List<RowResult> _resumeConfirmedResults = new();
    private List<RowResult> _resumeNotConfirmedResults = new();

    // ── Lifecycle ──────────────────────────────────────────────────────────
    /// <summary>Loads suppression state, local index data, and pending confirmations when Direct Push starts.</summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            await SuppressionSync.EnsureCurrentAsync();
            // Why: Task.Run moves store calls off the Blazor sync context (Pitfall 7).
            var initData = await Task.Run(() => Coordinator.LoadInitDataAsync(Cts.Token), Cts.Token);
            _approvedCount = initData.ApprovedCount;
            _dataRoot = initData.DataRoot;

            // Why (D-10/Plan 90-06): surface any rows left awaiting-confirm from a prior/interrupted
            // session BEFORE the operator does anything else — a marker-set row must never look
            // indistinguishable from "never pushed" (90-RESEARCH Pitfall 4).
            _awaitingConfirmRows = await Task.Run(() => Coordinator.GetAwaitingConfirmRowsAsync(Cts.Token), Cts.Token);
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

    // Why: shared by Stage 3 (sets new markers), Publish and Resume (both may clear markers on
    // confirm) — a single refresh point keeps the bucket honest without a re-query on a timestamp
    // column (Pitfall 3; the coordinator method filters in memory).
    private async Task RefreshAwaitingConfirmBucketAsync()
    {
        try
        {
            var rows = await Task.Run(() => Coordinator.GetAwaitingConfirmRowsAsync(Cts.Token), Cts.Token)
                .ConfigureAwait(false);

            await InvokeAsync(() =>
            {
                _awaitingConfirmRows = rows;
                SafeStateHasChanged();
            });
        }
        catch (OperationCanceledException)
        {
            // Component disposed — swallow.
        }
        catch (Exception ex)
        {
            // Why: non-fatal — the bucket simply won't refresh this cycle; the prior in-memory list
            // stays shown rather than the page crashing on a secondary read.
            Logger.LogWarning(ex, "Failed to refresh the awaiting-confirm resume bucket");
        }
    }

    // Why: shared by Publish and Resume to build a display row from a published/not-published
    // ContentSiteIndexRow without duplicating the natural-key derivation.
    private static RowResult ToRowResult(ContentSiteIndexRow row, bool success, string? reason)
    {
        var hasKey = ContentNaturalKey.TryDerive(row, out var key);
        return new RowResult(row.Title, hasKey ? key.Type : string.Empty, hasKey ? key.Value : string.Empty, success, reason);
    }

    // Why: the resume bucket table renders a row's natural key before any RowResult exists for it —
    // shares the same TryDerive call so the label can never diverge from ToRowResult's derivation.
    private static string ResumeKeyLabel(ContentSiteIndexRow row)
    {
        var hasKey = ContentNaturalKey.TryDerive(row, out var key);
        return hasKey ? $"{key.Type}:{key.Value}" : string.Empty;
    }

    // ── Stage 1: Compute Prod Diff ──────────────────────────────────────────
    private async Task ComputeDiffAsync()
    {
        if (_initError is not null || _operationInFlight || _approvedCount == 0
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
        // A fresh diff starts a fresh publish batch.
        _verifyRanOnce = false;
        _verifyError = string.Empty;
        _confirmedResults = new();
        _notConfirmedResults = new();

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

            // Why (D-10/Plan 90-06): a fresh diff is also a natural point to re-surface any rows left
            // awaiting-confirm from an earlier session (90-RESEARCH Pitfall 4) — refresh the bucket
            // alongside the diff rather than only at page load.
            await RefreshAwaitingConfirmBucketAsync();
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
        if (_initError is not null || !_prodReviewed || _operationInFlight || !_diffReady)
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
        if (_initError is not null || !_scpSuccess || _operationInFlight || !_diffReady)
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
                // Why (H4/D-06/D-07): coordinator runs the single transactional content-only batch
                // upsert + sets the local awaiting-confirm marker, all-or-nothing. Does NOT stamp
                // pushed_to_prod_utc or flip is_visible — those happen only after final publishing
                // (SYNC-09), a later stage. Throws on any row failure (rolled back).
                await Coordinator.WriteContentAsync(_publishRows, Cts.Token).ConfigureAwait(false);

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

            // Why (D-10/Plan 90-06): WriteContentAsync just set a new awaiting-confirm marker for
            // every pushed row — refresh the bucket immediately so the resume section reflects it
            // (test a: "after expand, a row shows in the awaiting-confirm bucket").
            await RefreshAwaitingConfirmBucketAsync();
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

    // ── Publish to private KB root ───────────────────────────────────────────
    private async Task RunExportAndPublishAsync()
    {
        if (_initError is not null || !_scpSuccess || !_dbSuccess || _operationInFlight)
        {
            return;
        }

        _operationInFlight = true;
        _verifyInFlight = true;
        _verifyError = string.Empty;
        _confirmedResults = new();
        _notConfirmedResults = new();
        try
        {
            await Task.Run(async () =>
            {
                await ExportAndConfirmAsync(_publishRows).ConfigureAwait(false);
                await InvokeAsync(() =>
                {
                    _confirmedResults = _publishRows.Select(row => ToRowResult(row, true, null)).ToList();
                    _verifyRanOnce = true;
                    _verifyInFlight = false;
                    _operationInFlight = false;
                    SafeStateHasChanged();
                });
            }, Cts.Token);
            await RefreshAwaitingConfirmBucketAsync();
        }
        catch (OperationCanceledException)
        {
            _verifyError = "Publishing was cancelled. The rows remain hidden and awaiting confirmation.";
            _verifyInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Publish to private KB root failed");
            _verifyError = "Could not publish to the private KB root. The rows remain hidden and awaiting confirmation.";
            _verifyInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Resume: publish rows awaiting confirmation from a prior/interrupted session (D-10) ──
    private async Task ResumeVerifyAsync()
    {
        if (_initError is not null || _operationInFlight || _awaitingConfirmRows.Count == 0)
        {
            return;
        }

        _operationInFlight = true;
        _resumeVerifyInFlight = true;
        _resumeVerifyError = string.Empty;
        try
        {
            await ExportAndConfirmAsync(_awaitingConfirmRows).ConfigureAwait(false);
            await RefreshAwaitingConfirmBucketAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Resume publish to private KB root failed");
            _resumeVerifyError = "Could not publish to the private KB root. The rows remain hidden and awaiting confirmation.";
        }
        finally
        {
            _resumeVerifyInFlight = false;
            _operationInFlight = false;
        }
    }

    private async Task ExportAndConfirmAsync(IReadOnlyList<ContentSiteIndexRow> rows)
    {
        await Coordinator.ExportBodiesToPrivateKbRootAsync(rows, _dataRoot, Cts.Token).ConfigureAwait(false);
        await Coordinator.ConfirmAndPublishAsync(rows, Cts.Token).ConfigureAwait(false);
    }

    internal Task InvokeWriteRowsForTest() => WriteRowsAsync();

}
