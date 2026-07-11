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

    // ── Stage 4 — git durability (commit bodies + push; gated on _dbSuccess) ─
    private bool _gitInFlight;
    private bool _gitSuccess;
    private bool _gitNoOp;
    private bool _gitPushed;
    private string _gitError = string.Empty;
    // Why: the manual-recovery command is rendered in its own <code> element (not inlined into the
    // error prose) so the operator can read/copy it verbatim, matching how every other command on
    // the page is presented.
    private string _gitManualPushCommand = string.Empty;
    private string _gitSha = string.Empty;
    private string _gitBranch = string.Empty;
    private int _gitBodyCount;

    // ── Stage 5 — Verify Deploy & Publish (gated on _gitSuccess; SYNC-09/D-06) ─
    private bool _verifyInFlight;
    private bool _verifyRanOnce;
    private string _verifyError = string.Empty;
    private List<RowResult> _confirmedResults = new();
    private List<RowResult> _notConfirmedResults = new();

    // ── Awaiting-confirm resume bucket (D-10) ────────────────────────────────
    // Why: a mid-flight push (content upserted, not yet deploy-confirmed) is durable via the local
    // AwaitingConfirmUtc marker (Plan 90-03) and survives a page reload. ClassifyDiff would
    // reclassify a content-matching-but-hidden row as Unchanged and drop it from PublishRows
    // (90-RESEARCH Pitfall 4), so this bucket is populated independently of the diff and refreshed
    // after every stage that can change the marker set (Stage 3 sets it, Stage 5/resume clear it).
    private IReadOnlyList<ContentSiteIndexRow> _awaitingConfirmRows = Array.Empty<ContentSiteIndexRow>();
    private bool _resumeVerifyInFlight;
    private string _resumeVerifyError = string.Empty;
    private List<RowResult> _resumeConfirmedResults = new();
    private List<RowResult> _resumeNotConfirmedResults = new();

    // ── Lifecycle ──────────────────────────────────────────────────────────
    protected override async Task OnInitializedAsync()
    {
        try
        {
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

    // Why: shared by Stage 3 (sets new markers), Stage 5 and Resume (both may clear markers on
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

    // Why: shared by Stage 5 and Resume to build a display row from a confirmed/not-confirmed
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
        // Why (D-09 REVISED): also gate on IsConfirmerConfigured — a push that can never be
        // deploy-confirmed would strand every row awaiting-confirm forever (T-90-12), so refuse to
        // start the whole DirectPush flow until the confirmer's base URL + admin creds are set.
        if (_operationInFlight || _approvedCount == 0
            || !Config.IsProdConfigured || !Config.IsScpConfigured || !Config.IsConfirmerConfigured)
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
        // Why (review F1): recomputing the diff starts a FRESH batch and hides the Stage 4 card
        // (it re-gates on _dbSuccess). The Stage 4 git-result fields MUST reset here too, or a prior
        // batch's green "already committed/pushed" alert re-appears the moment the new batch's DB
        // write flips _dbSuccess back to true — the operator would think the new bodies are already
        // git-durable and skip Stage 4, losing them on the next redeploy.
        _gitSuccess = false;
        _gitNoOp = false;
        _gitPushed = false;
        _gitError = string.Empty;
        _gitManualPushCommand = string.Empty;
        _gitSha = string.Empty;
        _gitBranch = string.Empty;
        _gitBodyCount = 0;
        // Why: Stage 5 also re-gates on a fresh batch, mirroring the Stage 4 reset above — a prior
        // batch's confirm/not-confirm results must not linger against the new publish set.
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
                // Why (H4/D-06/D-07): coordinator runs the single transactional content-only batch
                // upsert + sets the local awaiting-confirm marker, all-or-nothing. Does NOT stamp
                // pushed_to_prod_utc or flip is_visible — those happen only after a deploy-confirm
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

    // ── Stage 4: Commit Bodies to Git + Push (gated on _dbSuccess) ──────────
    private async Task CommitAndPushAsync()
    {
        // Why: hard-guard — the git durability stage must never run before the prod content-only
        // write succeeded (D-06: expand's git step follows expand's content step). The rows stay
        // hidden and awaiting-confirm through this entire stage — nothing goes live here; only
        // Stage 5 (after a confirmed deploy) can do that. The disabled button alone is not
        // sufficient (mirrors the Stage 3 guard).
        if (!_dbSuccess || _operationInFlight)
        {
            return;
        }

        _operationInFlight = true;
        _gitInFlight = true;
        _gitError = string.Empty;
        _gitManualPushCommand = string.Empty;
        _gitSuccess = false;
        _gitNoOp = false;
        _gitPushed = false;

        try
        {
            await Task.Run(async () =>
            {
                // Why: coordinator copies ONLY the pushed bodies into the repo, commits exactly
                // those paths (never the seed), and pushes the current branch with [skip render].
                var result = await Coordinator
                    .CommitAndPushBodiesAsync(_publishRows, _dataRoot, Cts.Token)
                    .ConfigureAwait(false);

                await InvokeAsync(() =>
                {
                    _gitSha = result.Sha ?? string.Empty;
                    _gitBranch = result.Branch;
                    _gitBodyCount = result.BodyCount;
                    // Committed = new commit + push. PushedExistingCommits = no new commit, but our own
                    // prior durability commit(s) were pushed (catch-up). AlreadyInSync = nothing to
                    // commit AND no push occurred — the UI must NOT claim a push (review R2-3).
                    _gitNoOp = result.Outcome != DirectPushGitOutcome.Committed;
                    _gitPushed = result.Outcome != DirectPushGitOutcome.AlreadyInSync;
                    _gitSuccess = true;
                    _gitInFlight = false;
                    _operationInFlight = false;
                    SafeStateHasChanged();
                });
            }, Cts.Token);
        }
        catch (OperationCanceledException)
        {
            _gitError = "Git commit/push was cancelled. The pushed rows stay HIDDEN and " +
                        "awaiting-confirm — nothing was published. Re-run Stage 4 to commit and " +
                        "push the bodies.";
            _gitInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (DirectPushPushBlockedException ex)
        {
            // Why (review R2-1): the stage could not verify the branch was safe to auto-push (e.g.
            // origin/{branch} not fetched) and failed CLOSED — nothing was committed or pushed. The
            // Reason is secret-free by construction. Non-fatal (D-06): the pushed rows are already
            // hidden and awaiting-confirm — a git failure here cannot expose anything, it only
            // delays the deploy the confirm step needs.
            Logger.LogWarning("Direct Push git stage blocked on {Branch}: {Reason}", ex.Branch, ex.Reason);
            _gitError = $"Stage 4 stopped: {ex.Reason}. Nothing was committed or pushed. " +
                        "The pushed rows stay HIDDEN and awaiting-confirm until the bodies are " +
                        "git-durable and deploy-confirmed. Resolve that, then retry.";
            _gitInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (DirectPushUnreviewedCommitsException ex)
        {
            // Why (review F2): the branch has commits ahead of origin that this stage did not author.
            // Pushing would publish them unreviewed, so the stage refused BEFORE committing anything.
            // Non-fatal (D-06): the pushed rows stay hidden and awaiting-confirm regardless.
            Logger.LogWarning(
                "Direct Push git stage refused: {Count} unreviewed commit(s) ahead of origin/{Branch}",
                ex.ForeignCommitCount, ex.Branch);
            _gitError = $"Stage 4 stopped: {ex.ForeignCommitCount} other unpushed commit(s) on " +
                        $"'{ex.Branch}' would be published by this push and have not been reviewed. " +
                        "Nothing was committed or pushed. Review and push (or reset) them yourself " +
                        "first, then retry. The pushed rows stay HIDDEN and awaiting-confirm.";
            _gitInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (DirectPushPushException ex)
        {
            // Why: the commit LANDED locally; only the push failed. Preserve the SHA/branch and tell the
            // operator exactly how to push by hand — a blind retry would report "nothing to commit"
            // (Codex MED). Log the inner exception (may carry the remote URL) to the sink only (D-07).
            Logger.LogError(ex, "Git push failed after commit {Sha} on {Branch}", ex.Sha ?? "(none)", ex.Branch);
            _gitSha = ex.Sha ?? string.Empty;
            _gitBranch = ex.Branch;
            var committedClause = ex.Sha is null
                ? "The bodies are already committed locally"
                : $"Committed the bodies locally as {ex.Sha}";
            _gitError = $"{committedClause}, but the push to origin FAILED. " +
                        "The pushed rows stay HIDDEN and awaiting-confirm until the push succeeds " +
                        "and Stage 5 confirms the deploy. To finish the git backup, run:";
            _gitManualPushCommand = $"git push origin HEAD:refs/heads/{ex.Branch}";
            _gitInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            // Why: M3 — a git error message can carry the repo path / remote URL / credential hints
            // (D-07 / SC5); log the full exception to the sink only and surface sanitized copy. This
            // stage is NON-FATAL (D-06): the pushed rows are already hidden and awaiting-confirm in
            // prod DB, so a git backup failure delays the deploy the confirm step needs but exposes
            // nothing new.
            Logger.LogError(ex, "Git commit/push (durability stage) failed");
            _gitError = "Could not commit or push the bodies to git — check the Studio git repo and " +
                        "credentials. The pushed rows stay HIDDEN and awaiting-confirm; only the git " +
                        "backup did not complete. You can retry, or run 'git push' manually.";
            _gitInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Stage 5: Verify Deploy & Publish (gated on _gitSuccess; SYNC-09/D-06) ─
    private async Task RunVerifyAndPublishAsync()
    {
        // Why: hard-guard — verification/publish must never run before Stage 4 (git commit+push)
        // has completed; the confirm poll checks the deployed /app body, which cannot exist without
        // a completed push. The disabled button alone is not sufficient (mirrors the Stage 3/4
        // guards).
        if (!_gitSuccess || _operationInFlight)
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
                // Why (SYNC-09/D-06): polls the Plan 90-07 deployed-body-hash endpoint per row and
                // stamps+flips visible ONLY the rows that confirm (200 && hash match). Not-confirmed
                // rows stay content-upserted, hidden, and durably awaiting-confirm (D-10) — resumable,
                // never a false-positive publish.
                var result = await Coordinator.VerifyAndPublishAsync(_publishRows, Cts.Token).ConfigureAwait(false);

                var confirmed = result.Confirmed.Select(r => ToRowResult(r, true, null)).ToList();
                var notConfirmed = result.NotConfirmed
                    .Select(r => ToRowResult(
                        r,
                        false,
                        "Not yet confirmed at /app — the Render deploy may still be catching up. " +
                        "Re-run this stage once it is healthy."))
                    .ToList();

                await InvokeAsync(() =>
                {
                    _confirmedResults = confirmed;
                    _notConfirmedResults = notConfirmed;
                    _verifyRanOnce = true;
                    _verifyInFlight = false;
                    _operationInFlight = false;
                    SafeStateHasChanged();
                });
            }, Cts.Token);

            // Why: ConfirmAndPublishAsync (inside VerifyAndPublishAsync) clears the local marker for
            // every confirmed row — refresh the resume bucket so it reflects the new state at once.
            await RefreshAwaitingConfirmBucketAsync();
        }
        catch (OperationCanceledException)
        {
            _verifyError = "Verify was cancelled. The pushed rows stay HIDDEN and awaiting-confirm " +
                        "— nothing was published. Re-run this stage.";
            _verifyInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            // Why: M3 — the confirmer's HTTP failure can carry host/creds in ex.Message (D-07 / SC5);
            // log full exception to the sink only, surface sanitized copy. Non-fatal: the rows stay
            // hidden and awaiting-confirm, never a false-positive publish.
            Logger.LogError(ex, "Verify deploy & publish failed");
            _verifyError = "Could not verify the deployed body — check the deploy-confirm " +
                        "configuration and try again. The pushed rows stay HIDDEN and " +
                        "awaiting-confirm; nothing was published.";
            _verifyInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Resume: re-run verify for rows awaiting confirm from a prior/interrupted session (D-10) ──
    private async Task ResumeVerifyAsync()
    {
        if (_operationInFlight || _awaitingConfirmRows.Count == 0)
        {
            return;
        }

        _operationInFlight = true;
        _resumeVerifyInFlight = true;
        _resumeVerifyError = string.Empty;
        _resumeConfirmedResults = new();
        _resumeNotConfirmedResults = new();

        // Why: capture the current bucket up front — RefreshAwaitingConfirmBucketAsync mutates
        // _awaitingConfirmRows once the resume completes, and this run must resolve exactly the set
        // it started with.
        var rowsToResume = _awaitingConfirmRows;

        try
        {
            await Task.Run(async () =>
            {
                var result = await Coordinator.VerifyAndPublishAsync(rowsToResume, Cts.Token).ConfigureAwait(false);

                var confirmed = result.Confirmed.Select(r => ToRowResult(r, true, null)).ToList();
                var notConfirmedReason = "Still not confirmed at /app. Wait for the Render deploy to finish, then " +
                                         "resume again.";

                if (result.NotConfirmed.Count > 0)
                {
                    var redeployResult = await Coordinator.TriggerRedeployAsync(Cts.Token).ConfigureAwait(false);
                    notConfirmedReason = redeployResult.Outcome switch
                    {
                        DirectPushRedeployOutcome.RedeployTriggered
                            => "Redeploy triggered — wait for the Render deploy to go healthy, then Resume again.",
                        DirectPushRedeployOutcome.AlreadyTriggered
                            => "Redeploy already triggered — wait for the Render deploy to go healthy, then Resume again.",
                        DirectPushRedeployOutcome.Indeterminate
                            => "prod flag DB unreachable — retry when reachable; rows stay awaiting-confirm.",
                        DirectPushRedeployOutcome.BranchAheadNeedsPush
                            => "A durability commit is already ahead of origin. Let that push/redeploy settle, then Resume again.",
                        _ => notConfirmedReason,
                    };
                }

                var notConfirmed = result.NotConfirmed
                    .Select(r => ToRowResult(
                        r,
                        false,
                        notConfirmedReason))
                    .ToList();

                await InvokeAsync(() =>
                {
                    _resumeConfirmedResults = confirmed;
                    _resumeNotConfirmedResults = notConfirmed;
                    _resumeVerifyInFlight = false;
                    _operationInFlight = false;
                    SafeStateHasChanged();
                });
            }, Cts.Token);

            await RefreshAwaitingConfirmBucketAsync();
        }
        catch (OperationCanceledException)
        {
            _resumeVerifyError = "Resume verify was cancelled. The rows stay awaiting-confirm.";
            _resumeVerifyInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Resume verify and redeploy handling failed");
            _resumeVerifyError = "Could not complete resume verify/redeploy handling — check the " +
                        "deploy-confirm and git state, then retry. The rows stay awaiting-confirm.";
            _resumeVerifyInFlight = false;
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

    // Why: exercises the CommitAndPushAsync hard-guard directly — bUnit will not dispatch a click to
    // a disabled button, so the guard (no git run before prod DB success) is otherwise unreachable in
    // a test. Calls the exact production handler; no behavior is duplicated.
    internal Task InvokeCommitAndPushForTest() => CommitAndPushAsync();

    // Why: exercises the RunVerifyAndPublishAsync hard-guard directly — bUnit will not dispatch a
    // click to a disabled button, so the guard (no confirm poll before git success) is otherwise
    // unreachable in a test. Calls the exact production handler; no behavior is duplicated.
    internal Task InvokeVerifyAndPublishForTest() => RunVerifyAndPublishAsync();
}
