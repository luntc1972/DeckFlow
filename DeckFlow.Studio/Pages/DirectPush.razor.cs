using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Core.Storage;
using DeckFlow.Studio;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Pages;

/// <summary>
/// Code-behind for the Direct Push (publish-to-production) page. Extracted from
/// the inline @code block (H1 god-component split). Behavior unchanged. The M2
/// composite-key delimiter now uses the U+0000 escape instead of a raw NUL
/// source byte - identical resulting string, keeps this file valid UTF-8 text.
/// </summary>
public partial class DirectPush
{
    // ── Injected services ───────────────────────────────────────────────────
    [Inject]
    private IContentSiteIndexStore IndexStore { get; set; } = default!;

    [Inject]
    private ISshArtifactUploader SshUploader { get; set; } = default!;

    [Inject]
    private IProdStoreFactory ProdStoreFactory { get; set; } = default!;

    [Inject]
    private StudioConfig Config { get; set; } = default!;

    // Why: IConfiguration injected (not a singleton string holder) so the prod conn
    // string is ephemeral in the publish action, never materialized into DI state (D-03/D-07).
    [Inject]
    private IConfiguration Configuration { get; set; } = default!;

    [Inject]
    private ContentKbOrchestratorOptions Options { get; set; } = default!;

    // Why: M3 — logs caught exceptions to the Serilog file/console sink so "see logs" guidance
    // in sanitized UI messages is actually true. Never log ex.Message to the UI (D-07 / SC5).
    [Inject]
    private ILogger<DirectPush> Logger { get; set; } = default!;

    // ── Init state ──────────────────────────────────────────────────────────
    private bool _initInFlight = true;
    private string? _initError;
    private int _approvedCount;

    // dataRoot = parent of ArtifactRoot = {studioDataDirectory}
    // Why: ArtifactPath already carries content-kb/; combining with ArtifactRoot would
    // double the segment. The data root is the correct base (D-01/D-03/D-10).
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

    // All approved local rows (reference; SCP + DB operate on _publishRows only).
    private IReadOnlyList<ContentSiteIndexRow> _approvedRows = Array.Empty<ContentSiteIndexRow>();

    // Why (M2): only New + Updated rows are uploaded and written; Unchanged rows are skipped
    // entirely — their artifacts were uploaded on a prior push and their content signature is
    // identical to what is already in prod.
    private IReadOnlyList<ContentSiteIndexRow> _publishRows = Array.Empty<ContentSiteIndexRow>();

    // Per-row diff display rows (New + Updated only, shown in the diff table)
    private List<DiffRow> _diffRows = new();

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

    private sealed record DiffRow(string Title, string KeyType, string KeyValue, bool IsNew, string ArtifactFile);

    private sealed record RowResult(string Title, string KeyType, string KeyValue, bool Success, string? Reason);

    // Why: the diff loop and the push loop derive the same (display keyType, key value) pair.
    // KeyType is the local diff label ("youtube"/"podcast"), intentionally NOT the store's
    // youtube_channel/podcast_rss discriminator — matching is on the key value, not the type.
    private static (string KeyType, string KeyValue) DeriveNaturalKey(ContentSiteIndexRow row)
        => (row.YoutubeVideoId is not null ? "youtube" : "podcast",
            row.YoutubeVideoId ?? row.RssGuid ?? string.Empty);

    // ── Lifecycle ──────────────────────────────────────────────────────────
    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Why: Task.Run moves store calls off the Blazor sync context (Pitfall 7).
            var (approvedCount, dataRoot) = await Task.Run(async () =>
            {
                var rows = await IndexStore.GetApprovedRowsAsync(Cts.Token).ConfigureAwait(false);
                var dr = Path.GetDirectoryName(Options.ArtifactRoot) ?? Options.ArtifactRoot;
                return (rows.Count, dr);
            }, Cts.Token);

            _approvedCount = approvedCount;
            _dataRoot = dataRoot;
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
                // Read local approved rows
                var localRows = await IndexStore.GetApprovedRowsAsync(Cts.Token).ConfigureAwait(false);

                // Build on-demand prod store (D-03) — never at DI startup
                var rawConnStr = Configuration["Studio:ProdConnectionString"] ?? string.Empty;
                var prodStore = ProdStoreFactory.Create(rawConnStr);
                // Why: the diff is strictly read-only — no DDL against prod (H3).
                // Prod schema is managed by the DeckFlow.Web app startup (DeckFlow.Web/Program.cs:256).
                // ProdContentReader also runs zero DDL by design; this path mirrors that precedent.

                // Read prod rows (all rows regardless of visibility) — diff base
                var prodRows = await prodStore.GetAllRowsAsync(Cts.Token).ConfigureAwait(false);

                // Why (M2): content-aware diff — compare actual content columns so unchanged
                // rows are excluded from both SCP upload and DB write, stopping no-op re-writes
                // and reducing prod write surface. A HashSet presence check is insufficient
                // because it classifies every existing key as "Updated" even if nothing changed.
                // Why: key the diff map on the FULL natural key (type + value), not the bare value.
                // Keying on value alone (youtube id OR rss guid) lets a prod podcast row and a local
                // youtube row that share a value collide; under M2 a collision whose content also
                // matches would misclassify the local row as Unchanged and silently SKIP its publish
                // (data loss). The composite key makes "Unchanged" provably the same record.
                var prodByKey = new Dictionary<string, ContentSiteIndexRow>(
                    prodRows.Count,
                    StringComparer.Ordinal);
                foreach (var r in prodRows)
                {
                    var (prodKeyType, prodKeyValue) = DeriveNaturalKey(r);
                    if (!string.IsNullOrEmpty(prodKeyValue))
                    {
                        prodByKey[$"{prodKeyType}\u0000{prodKeyValue}"] = r;
                    }
                }

                int newCount = 0, updatedCount = 0, unchangedCount = 0;
                var diffRows = new List<DiffRow>();
                var publishRows = new List<ContentSiteIndexRow>();
                foreach (var row in localRows)
                {
                    var (keyType, key) = DeriveNaturalKey(row);
                    if (!prodByKey.TryGetValue($"{keyType}\u0000{key}", out var prodRow))
                    {
                        newCount++;
                        publishRows.Add(row);
                        diffRows.Add(new DiffRow(row.Title, keyType, key, true, Path.GetFileName(row.ArtifactPath)));
                    }
                    else if (!ContentSiteIndexContentSignature.AreContentEqual(row, prodRow))
                    {
                        updatedCount++;
                        publishRows.Add(row);
                        diffRows.Add(new DiffRow(row.Title, keyType, key, false, Path.GetFileName(row.ArtifactPath)));
                    }
                    else
                    {
                        // Unchanged: content signature matches — skip SCP and DB write entirely.
                        unchangedCount++;
                    }
                }

                await InvokeAsync(() =>
                {
                    _approvedRows = localRows;
                    _publishRows = publishRows;
                    _diffRows = diffRows;
                    _newCount = newCount;
                    _updatedCount = updatedCount;
                    _unchangedCount = unchangedCount;
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
                // Why (M2): only _publishRows (New + Updated) are uploaded; Unchanged rows
                // already have identical artifacts in prod from a prior push.
                var requests = _publishRows
                    .Select(r => new SshUploadRequest(
                        Path.GetFullPath(Path.Combine(_dataRoot, r.ArtifactPath)),
                        r.ArtifactPath))
                    .ToList();

                // Progress streams per-file results into _fileResults via disposal-safe InvokeAsync.
                var progress = new Progress<SshUploadResult>(result =>
                {
                    _ = InvokeAsync(() =>
                    {
                        _fileResults.Add(result);
                        SafeStateHasChanged();
                    });
                });

                var results = await SshUploader.UploadArtifactsAsync(requests, progress, Cts.Token)
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
                var rawConnStr = Configuration["Studio:ProdConnectionString"] ?? string.Empty;
                var prodStore = ProdStoreFactory.Create(rawConnStr);

                // Why (H4): single transactional batch call — one connection, one DbTransaction,
                // all-or-nothing. Replaces the per-row loop that left prod partially written on
                // mid-batch failure (T-qyc-01). Prod schema is web-app-managed (H3); no DDL here.
                // SC3 / D-08: only the content-columns-only upsert may run on prod — never a
                // full-row upsert (preserves is_visible / is_evergreen on existing rows).
                await prodStore.UpsertContentColumnsOnlyBatchAsync(_publishRows, Cts.Token).ConfigureAwait(false);

                // All rows succeeded — build result list and run stamp/visibility.
                var successResults = _publishRows
                    .Select(row =>
                    {
                        var (keyType, keyValue) = DeriveNaturalKey(row);
                        return new RowResult(row.Title, keyType, keyValue, true, null);
                    })
                    .ToList();

                var keys = _publishRows
                    .Select(row => ContentIndexExportRow.From(row))
                    .Select(row => (
                        Type: row.NaturalKeyType,
                        Value: row.NaturalKeyValue))
                    .ToList();
                var pushedUtc = DateTimeOffset.UtcNow;

                // Why: write PRODUCTION state first (stamp + publish-visible), then advance the
                // local store. If a prod write fails, the local row stays behind (un-stamped /
                // hidden) rather than deriving Published while prod is not — local must never
                // over-report prod (PUB-01/HIGH-3). DirectPush publishes its rows visible so both
                // the prod /Admin and Studio surfaces derive Published once local catches up.
                await prodStore.StampPushedToProdAsync(keys, pushedUtc, Cts.Token).ConfigureAwait(false);
                await prodStore.SetVisibilityAsync(keys, true, Cts.Token).ConfigureAwait(false);
                await IndexStore.StampPushedToProdAsync(keys, pushedUtc, Cts.Token).ConfigureAwait(false);
                await IndexStore.SetVisibilityAsync(keys, true, Cts.Token).ConfigureAwait(false);

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
            var rollbackResults = _publishRows
                .Select(row =>
                {
                    var (keyType, keyValue) = DeriveNaturalKey(row);
                    return new RowResult(row.Title, keyType, keyValue, false, "Rolled back — not written");
                })
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

    // ── IDisposable: cancel in-flight ops on circuit drop ───────────────────
    /// <summary>
    /// Cancels and disposes the active <see cref="CancellationTokenSource"/> so any in-flight
    /// prod read, SCP upload, or DB write is stopped when the operator closes the tab or the
    /// SignalR circuit drops (D-09).
    /// </summary>
}
