# Phase 47: Direct Prod-DB + SCP Publish Path — Pattern Map

**Mapped:** 2026-06-16
**Files analyzed:** 9 new/modified files
**Analogs found:** 9 / 9

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Studio/Pages/DirectPush.razor` | component (page) | request-response + streaming | `DeckFlow.Studio/Pages/Publish.razor` | exact |
| `DeckFlow.Studio/Shared/NavMenu.razor` | component (nav) | — | `DeckFlow.Studio/Shared/NavMenu.razor` (self — add entry) | exact |
| `DeckFlow.Studio/StudioConfig.cs` | config | — | `DeckFlow.Studio/StudioConfig.cs` (self — extend) | exact |
| `DeckFlow.Studio/Program.cs` | config / DI wiring | — | `DeckFlow.Studio/Program.cs` (self — extend) | exact |
| `DeckFlow.Studio/Services/ISshArtifactUploader.cs` | service (interface + result record) | file-I/O | `DeckFlow.Core/Integration/IGitRepository.cs` | role-match |
| `DeckFlow.Studio/Services/SftpArtifactUploader.cs` | service (implementation) | file-I/O | `DeckFlow.Core/Integration/GitRepository.cs` | role-match |
| `DeckFlow.Studio/Services/IProdStoreFactory.cs` | service (interface + implementation) | CRUD | `DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs` | role-match |
| `DeckFlow.Studio.Tests/DirectPushPageTests.cs` | test | request-response | `DeckFlow.Studio.Tests/PublishPageTests.cs` | exact |
| `DeckFlow.Studio.Tests/TestDoubles/FakeSshArtifactUploader.cs` | test double | file-I/O | `DeckFlow.Studio.Tests/TestDoubles/FakeGitRepository.cs` | role-match |
| `DeckFlow.Studio.Tests/TestDoubles/FakeProdStoreFactory.cs` | test double | CRUD | `DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs` | role-match |

---

## Pattern Assignments

### `DeckFlow.Studio/Pages/DirectPush.razor` (component page, request-response + streaming)

**Analog:** `DeckFlow.Studio/Pages/Publish.razor`

This is a 3-stage variant of Publish.razor's 2-stage pattern, with `btn-danger` on write
steps and per-item reconcile lists instead of a raw diff `<pre>`. Mirror every structural
detail below exactly.

**Directive and using block** (lines 1–7):
```razor
@page "/direct-push"
@implements IDisposable
@using DeckFlow.Core.Content
@using DeckFlow.Core.Storage
@using DeckFlow.Studio.Services
@using Microsoft.Extensions.Configuration
@using System.Text.Json
```

**Page skeleton** (lines 9–14):
```razor
<PageTitle>Publish to Production (Direct)</PageTitle>

<h1 class="h4 fw-semibold">Publish to Production (Direct)</h1>
<p class="text-muted">Push approved entries straight to production — upload artifacts to
Render /data, then upsert the rows into the prod database. Artifacts first; the DB step
unlocks only after every file uploads.</p>

<article class="content px-4">
    @* ... *@
</article>
```

**Init loading / error guard** (lines 16–30 of Publish.razor):
```razor
@if (_initError is not null)
{
    <div class="alert alert-danger py-2 mt-3">@_initError</div>
}
else if (_initInFlight)
{
    <div class="d-flex align-items-center gap-2 mt-3">
        <span class="spinner-border spinner-border-sm text-primary"
              role="status"
              aria-label="Operation in progress">
            <span class="visually-hidden">Loading...</span>
        </span>
        <span class="text-muted">Resolving configuration...</span>
    </div>
}
```

**Not-configured gate** (new for Phase 47 — render after init guard, before any action UI):
```razor
@* SC5 / D-07: never show connection-string values; only presence *@
@if (!Config.IsProdConfigured || !Config.IsScpConfigured)
{
    @if (!Config.IsProdConfigured)
    {
        <div class="alert alert-warning py-2 mt-3">
            Prod connection: not configured. Set the prod connection string in user-secrets
            to enable direct publish.
        </div>
    }
    @if (!Config.IsScpConfigured)
    {
        <div class="alert alert-warning py-2 mt-3">
            SCP: not configured. Set the Render SSH target in user-secrets to enable
            artifact upload.
        </div>
    }
}
```

**TARGET: PRODUCTION danger banner** (always shown when configured — above diff card):
```razor
<div class="alert alert-danger py-2 mt-3">
    <span class="oi oi-warning" aria-hidden="true"></span>
    <strong> TARGET: PRODUCTION</strong> — this writes directly to the live site database
    and the Render /data disk. There is no undo.
</div>
```

**Stage-1 card — Compute Prod Diff** (mirror Publish.razor lines 51–118, swap inner logic):
```razor
<div class="card mt-3">
    <div class="card-header">
        <h2 class="h5 fw-semibold mb-0">Stage 1 — Compute Prod Diff</h2>
    </div>
    <div class="card-body">
        @if (!string.IsNullOrEmpty(_diffError))
        {
            <div class="alert alert-danger py-2">@_diffError</div>
        }

        <button class="btn btn-outline-primary"
                @onclick="ComputeDiffAsync"
                disabled="@(_operationInFlight || _approvedCount == 0
                            || !Config.IsProdConfigured || !Config.IsScpConfigured)">
            @if (_diffComputeInFlight)
            {
                <span class="spinner-border spinner-border-sm text-primary"
                      role="status" aria-label="Operation in progress">
                    <span class="visually-hidden">Loading...</span>
                </span>
                <span> Comparing against production...</span>
            }
            else { <span>Compute Prod Diff</span> }
        </button>

        @if (_diffReady)
        {
            <div class="card border-primary mt-3">
                <div class="card-body">
                    <h3 class="h6 fw-semibold text-primary mb-2">Diff Preview</h3>
                    <div class="d-flex gap-2 align-items-center flex-wrap mb-2">
                        <span class="badge bg-success">New: @_newCount</span>
                        <span class="badge bg-primary">Updated: @_updatedCount</span>
                    </div>
                    @* Per-row table (New + Updated) — mirror Review.razor table style *@
                    <table class="table table-sm table-hover align-middle mt-2"
                           aria-live="polite">
                        @* Title / NaturalKey / New-vs-Updated / ArtifactFile *@
                    </table>
                </div>
            </div>
        }
    </div>
</div>
```

**Confirmation checkbox** (Publish.razor lines 143–153 — copy exactly, change id + label):
```razor
<div class="mb-3">
    <div class="form-check">
        <input class="form-check-input"
               type="checkbox"
               id="prodReviewed"
               @bind="_prodReviewed" />
        <label class="form-check-label" for="prodReviewed">
            I have reviewed what will be written to PRODUCTION above.
        </label>
    </div>
</div>
```

**Stage-2 card — SCP Upload** (gated on `_diffReady && _prodReviewed`; button is `btn-danger`):
```razor
<div class="card mt-3">
    <div class="card-header">
        <h2 class="h5 fw-semibold mb-0">Stage 2 — Upload Artifacts to Prod /data (SCP)</h2>
    </div>
    <div class="card-body">
        @if (!string.IsNullOrEmpty(_scpError))
        {
            <div class="alert alert-danger py-2">@_scpError</div>
        }

        <button class="btn btn-danger"
                @onclick="UploadArtifactsAsync"
                disabled="@(!_prodReviewed || _operationInFlight || !_diffReady)"
                aria-label="@(_prodReviewed ? null : "Check the confirmation box above to enable upload")">
            @if (_scpInFlight)
            {
                <span class="spinner-border spinner-border-sm"
                      role="status" aria-label="Operation in progress">
                    <span class="visually-hidden">Loading...</span>
                </span>
                <span> Uploading artifacts...</span>
            }
            else { <span>Upload Artifacts to Prod /data (SCP)</span> }
        </button>

        @* Per-file reconcile list — aria-live so it streams (SC4) *@
        @if (_fileResults.Count > 0)
        {
            <table class="table table-sm table-hover align-middle mt-3"
                   aria-live="polite">
                @* File / Status badge / Reason *@
            </table>
        }

        @if (_scpSuccess)
        {
            <div class="alert alert-success py-2 mt-3">
                All @_fileResults.Count artifact(s) uploaded to production /data.
            </div>
        }
    </div>
</div>
```

**Stage-3 card — DB Upsert** (gated on `_scpSuccess`; button is `btn-danger`):
```razor
<div class="card mt-3">
    <div class="card-header">
        <h2 class="h5 fw-semibold mb-0">Stage 3 — Write Approved Rows to Prod DB</h2>
    </div>
    <div class="card-body">
        @if (!string.IsNullOrEmpty(_dbError))
        {
            <div class="alert alert-danger py-2">@_dbError</div>
        }

        @if (!_scpSuccess)
        {
            <p class="text-muted small mb-2">
                Locked until every artifact has uploaded successfully.
            </p>
        }

        <button class="btn btn-danger"
                @onclick="WriteRowsAsync"
                disabled="@(!_scpSuccess || _operationInFlight)"
                aria-label="@(_scpSuccess ? null : "Locked until every artifact has uploaded successfully")">
            @if (_dbInFlight)
            {
                <span class="spinner-border spinner-border-sm"
                      role="status" aria-label="Operation in progress">
                    <span class="visually-hidden">Loading...</span>
                </span>
                <span> Writing rows to production...</span>
            }
            else { <span>Write Approved Rows to Prod DB</span> }
        </button>

        @* Per-row reconcile list *@
        @if (_rowResults.Count > 0)
        {
            <table class="table table-sm table-hover align-middle mt-3"
                   aria-live="polite">
                @* Title / NaturalKey / Status badge / Reason *@
            </table>
        }

        @if (_dbSuccess)
        {
            <div class="alert alert-success py-2 mt-3">
                All @_rowResults.Count approved row(s) written to production.
                is_visible and is_evergreen on existing rows were preserved.
            </div>
        }
    </div>
</div>
```

**@code block — injected services**:
```csharp
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
```

**@code block — state variables** (mirror Publish.razor lines 213–261, extend for 3 stages):
```csharp
// ── Init state ──────────────────────────────────────────────────────────
private bool _initInFlight = true;
private string? _initError;
private int _approvedCount;

// ── Shared in-flight guard ──────────────────────────────────────────────
private bool _operationInFlight;

// ── CTS for disposal-safe cancellation ─────────────────────────────────
private CancellationTokenSource _cts = new();

// ── Stage 1 — compute diff ──────────────────────────────────────────────
private bool _diffComputeInFlight;
private string _diffError = string.Empty;
private bool _diffReady;
private int _newCount;
private int _updatedCount;
// Approved local rows cached for SCP + upsert steps
private IReadOnlyList<ContentSiteIndexRow> _approvedRows = Array.Empty<ContentSiteIndexRow>();

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
private List<(string Title, string KeyType, string KeyValue, bool Success, string? Reason)> _rowResults = new();
```

**OnInitializedAsync** (mirror Publish.razor lines 264–300 exactly):
```csharp
protected override async Task OnInitializedAsync()
{
    try
    {
        // Why: Task.Run moves store calls off the Blazor sync context (Pitfall 7).
        var approvedCount = await Task.Run(async () =>
        {
            var rows = await IndexStore.GetApprovedRowsAsync(_cts.Token).ConfigureAwait(false);
            return rows.Count;
        }, _cts.Token);

        _approvedCount = approvedCount;
    }
    catch (OperationCanceledException)
    {
        // Component disposed mid-load — swallow.
    }
    catch (Exception ex)
    {
        _initError = $"Initialization failed — {ex.Message}";
    }
    finally
    {
        _initInFlight = false;
        await InvokeAsync(StateHasChanged);
    }
}
```

**ComputeDiffAsync — Stage 1 handler** (mirror ExportAndDiffAsync from Publish.razor lines 303–480):
```csharp
private async Task ComputeDiffAsync()
{
    if (_operationInFlight || _approvedCount == 0) return;

    _operationInFlight = true;
    _diffComputeInFlight = true;
    _diffError = string.Empty;
    _diffReady = false;
    _prodReviewed = false;
    _scpSuccess = false;
    _fileResults = new();
    _dbSuccess = false;
    _rowResults = new();

    try
    {
        await Task.Run(async () =>
        {
            // Read local approved rows
            var localRows = await IndexStore.GetApprovedRowsAsync(_cts.Token).ConfigureAwait(false);

            // Build on-demand prod store (D-03) — never at DI startup
            var rawConnStr = Configuration["Studio:ProdConnectionString"] ?? string.Empty;
            var prodStore = ProdStoreFactory.Create(rawConnStr);
            await prodStore.EnsureSchemaAsync(_cts.Token).ConfigureAwait(false);

            // Read prod rows (GetAllRowsAsync — all rows regardless of visibility)
            var prodRows = await prodStore.GetAllRowsAsync(_cts.Token).ConfigureAwait(false);

            // In-memory natural-key diff (D-04)
            var prodByKey = prodRows.ToDictionary(
                r => (r.YoutubeVideoId ?? r.RssGuid ?? string.Empty));
            int newCount = 0, updatedCount = 0;
            foreach (var row in localRows)
            {
                var key = row.YoutubeVideoId ?? row.RssGuid ?? string.Empty;
                if (!prodByKey.ContainsKey(key)) newCount++;
                else updatedCount++;
            }

            await InvokeAsync(() =>
            {
                _approvedRows = localRows;
                _newCount = newCount;
                _updatedCount = updatedCount;
                _diffReady = true;
                _diffComputeInFlight = false;
                _operationInFlight = false;
                try { StateHasChanged(); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            });
        }, _cts.Token);
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
        // Why: never include conn-string or SSH values in user-facing error (D-07)
        _diffError = $"Could not read production — {ex.Message}. Check the prod connection and retry. Nothing was written.";
        _diffComputeInFlight = false;
        _operationInFlight = false;
        await InvokeAsync(StateHasChanged);
    }
}
```

**UploadArtifactsAsync — Stage 2 handler** (same Task.Run + InvokeAsync(StateHasChanged) shape):
```csharp
private async Task UploadArtifactsAsync()
{
    if (!_prodReviewed || _operationInFlight || !_diffReady) return;

    _operationInFlight = true;
    _scpInFlight = true;
    _scpError = string.Empty;
    _scpSuccess = false;
    _fileResults = new();

    try
    {
        await Task.Run(async () =>
        {
            // Resolve local absolute paths from ArtifactPath on each approved row
            // (ArtifactPath is relative, e.g. content-kb/slug/vid.md)
            var dataRoot = /* resolve from config or ContentKbOrchestratorOptions */ string.Empty;
            var localPaths = _approvedRows
                .Select(r => Path.GetFullPath(Path.Combine(dataRoot, r.ArtifactPath)))
                .ToList();

            // Progress streams per-file results into _fileResults via InvokeAsync
            var progress = new Progress<SshUploadResult>(result =>
            {
                _ = InvokeAsync(() =>
                {
                    _fileResults.Add(result);
                    try { StateHasChanged(); }
                    catch (ObjectDisposedException) { }
                    catch (InvalidOperationException) { }
                });
            });

            var results = await SshUploader.UploadArtifactsAsync(localPaths, progress, _cts.Token)
                .ConfigureAwait(false);

            var allOk = results.All(r => r.Success);

            await InvokeAsync(() =>
            {
                _fileResults = results.ToList();
                _scpSuccess = allOk;
                if (!allOk)
                    _scpError = "Artifact upload finished with failures — see the per-file list below. " +
                                "The database step stays locked. Fix the failed files and re-run upload.";
                _scpInFlight = false;
                _operationInFlight = false;
                try { StateHasChanged(); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            });
        }, _cts.Token);
    }
    catch (OperationCanceledException)
    {
        _scpError = "Upload was cancelled.";
        _scpInFlight = false;
        _operationInFlight = false;
        await InvokeAsync(StateHasChanged);
    }
    catch (Exception)
    {
        // Why: SshException.Message may contain hostname (D-07) — use sanitized copy only
        _scpError = "SSH connection failed — check SCP configuration and Render SSH access.";
        _scpInFlight = false;
        _operationInFlight = false;
        await InvokeAsync(StateHasChanged);
    }
}
```

**WriteRowsAsync — Stage 3 handler** (gated on `_scpSuccess`):
```csharp
private async Task WriteRowsAsync()
{
    if (!_scpSuccess || _operationInFlight) return;

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

            var localResults = new List<(string Title, string KeyType, string KeyValue, bool Success, string? Reason)>();
            foreach (var row in _approvedRows)
            {
                try
                {
                    // SC3 / D-08: ONLY UpsertContentColumnsOnlyAsync — never UpsertRowAsync
                    await prodStore.UpsertContentColumnsOnlyAsync(row, _cts.Token).ConfigureAwait(false);
                    localResults.Add((row.Title, row.YoutubeVideoId is not null ? "youtube" : "podcast",
                        row.YoutubeVideoId ?? row.RssGuid ?? string.Empty, true, null));
                }
                catch (Exception ex)
                {
                    localResults.Add((row.Title, row.YoutubeVideoId is not null ? "youtube" : "podcast",
                        row.YoutubeVideoId ?? row.RssGuid ?? string.Empty, false, ex.Message));
                }

                await InvokeAsync(() =>
                {
                    _rowResults = localResults.ToList();
                    try { StateHasChanged(); }
                    catch (ObjectDisposedException) { }
                    catch (InvalidOperationException) { }
                });
            }

            var allOk = localResults.All(r => r.Success);
            await InvokeAsync(() =>
            {
                _rowResults = localResults;
                _dbSuccess = allOk;
                if (!allOk)
                    _dbError = "Database upsert finished with failures — see the per-row list below. " +
                               "Artifacts already uploaded successfully; reconcile only the failed rows.";
                _dbInFlight = false;
                _operationInFlight = false;
                try { StateHasChanged(); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            });
        }, _cts.Token);
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
        _dbError = $"Database write failed — {ex.Message}";
        _dbInFlight = false;
        _operationInFlight = false;
        await InvokeAsync(StateHasChanged);
    }
}
```

**IDisposable** (copy exactly from Publish.razor lines 540–551):
```csharp
public void Dispose()
{
    // Why: CTS disposed on component disposal so circuit drop cancels in-flight ops.
    _cts.Cancel();
    _cts.Dispose();
}
```

---

### `DeckFlow.Studio/Shared/NavMenu.razor` (component nav — add one entry)

**Analog:** `DeckFlow.Studio/Shared/NavMenu.razor` (self)

**Existing Publish entry to insert below** (lines 27–31):
```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="publish">
        <span class="oi oi-cloud-upload" aria-hidden="true"></span> Publish
    </NavLink>
</div>
```

**New entry to add immediately after** (mirror exact `div.nav-item.px-3` shape):
```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="direct-push">
        <span class="oi oi-data-transfer-upload" aria-hidden="true"></span> Direct Push
    </NavLink>
</div>
```

---

### `DeckFlow.Studio/StudioConfig.cs` (config record — extend)

**Analog:** `DeckFlow.Studio/StudioConfig.cs` (self — current line 6)

**Current (line 6):**
```csharp
public sealed record StudioConfig(bool IsProdConfigured);
```

**Extended record (add second positional parameter):**
```csharp
/// <summary>
/// Indicates whether the production Studio connection and SCP transport have been configured.
/// </summary>
public sealed record StudioConfig(bool IsProdConfigured, bool IsScpConfigured);
```

The existing constructor call `new StudioConfig(isProdConfigured)` in `Program.cs` (line 47)
must also be updated to `new StudioConfig(isProdConfigured, isScpConfigured)`.

---

### `DeckFlow.Studio/Program.cs` (DI wiring — extend)

**Analog:** `DeckFlow.Studio/Program.cs` (self)

**Existing prod-conn pattern** (lines 38–39, 47, 110 — the SC5 template to extend):
```csharp
var prodConnStr = builder.Configuration["Studio:ProdConnectionString"];
var isProdConfigured = !string.IsNullOrEmpty(prodConnStr);
// ...
builder.Services.AddSingleton(new StudioConfig(isProdConfigured));
// ...
Log.Information("Studio prod connection: {Status}", isProdConfigured ? "configured" : "not configured");
```

**New SCP presence-only detection to add after line 39** (D-02, D-07):
```csharp
// Why: presence-only check — never log values (D-07 / SC5).
var isScpConfigured = !string.IsNullOrEmpty(builder.Configuration["Studio:Scp:Host"])
    && !string.IsNullOrEmpty(builder.Configuration["Studio:Scp:Username"])
    && !string.IsNullOrEmpty(builder.Configuration["Studio:Scp:KeyFile"])
    && !string.IsNullOrEmpty(builder.Configuration["Studio:Scp:RemoteArtifactRoot"]);
```

**Updated StudioConfig singleton line 47:**
```csharp
builder.Services.AddSingleton(new StudioConfig(isProdConfigured, isScpConfigured));
```

**New service registrations to add after the existing singletons** (before `AddRazorPages`):
```csharp
builder.Services.AddSingleton<ISshArtifactUploader, SftpArtifactUploader>();
builder.Services.AddSingleton<IProdStoreFactory, ProdStoreFactory>();
```

**New startup log to add after line 110** (mirror existing pattern exactly):
```csharp
Log.Information("Studio SCP: {Status}", isScpConfigured ? "configured" : "not configured");
```

---

### `DeckFlow.Studio/Services/ISshArtifactUploader.cs` (service interface + result record)

**Analog:** `DeckFlow.Core/Integration/IGitRepository.cs` (interface with async methods returning
typed results; no implementation coupling)

**Pattern — interface declaration style** (from `IGitRepository.cs`):
```csharp
// One interface per file; xml doc on every public member; async returns Task<T>
// or Task<IReadOnlyList<T>>; optional CancellationToken as last param
```

**Concrete definition (from RESEARCH.md — use verbatim):**
```csharp
namespace DeckFlow.Studio.Services;

/// <summary>
/// Uploads local artifact files to the configured remote path via SFTP.
/// </summary>
public interface ISshArtifactUploader
{
    /// <summary>
    /// Uploads a set of local artifact files to the configured remote path.
    /// Returns per-file results; does not throw on individual file failure.
    /// </summary>
    /// <param name="localPaths">Absolute paths to local artifact files.</param>
    /// <param name="progress">Optional per-file progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per-file upload results.</returns>
    Task<IReadOnlyList<SshUploadResult>> UploadArtifactsAsync(
        IReadOnlyList<string> localPaths,
        IProgress<SshUploadResult>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Per-file result of an SFTP upload attempt.</summary>
/// <param name="LocalPath">Absolute local path of the file.</param>
/// <param name="Success">Whether the upload succeeded.</param>
/// <param name="FailureReason">Sanitized failure reason; null on success. Never contains host/key/path secrets.</param>
public sealed record SshUploadResult(string LocalPath, bool Success, string? FailureReason);
```

---

### `DeckFlow.Studio/Services/SftpArtifactUploader.cs` (SFTP implementation)

**Analog:** `DeckFlow.Core/Integration/GitRepository.cs` (process-launching service that wraps
an external system, error-translates exceptions, one external call per method call)

**SSH.NET SftpClient construction pattern** (from RESEARCH.md — per-call client, not per-instance):
```csharp
// Why: SftpClient is not thread-safe across concurrent calls; open one client per
// UploadArtifactsAsync invocation, upload sequentially, then disconnect. (Pitfall 5)
using var privateKey = new PrivateKeyFile(keyFilePath);           // passphrase overload if needed
using var client = new SftpClient(host, port, username, privateKey);
client.Connect();
// Per-file upload loop:
foreach (var localPath in localPaths)
{
    var remotePath = BuildRemotePath(remoteArtifactRoot, localPath, dataRoot);
    var remoteDir = Path.GetDirectoryName(remotePath)?.Replace('\\', '/') ?? remoteArtifactRoot;
    // Why: SFTP does not auto-create parent dirs (Pitfall 6)
    if (!client.Exists(remoteDir))
        client.CreateDirectory(remoteDir);
    using var fs = File.OpenRead(localPath);
    client.UploadFile(fs, remotePath);
}
client.Disconnect();
```

**Error sanitization** (D-07 / Pitfall 3):
```csharp
catch (SshException ex)
{
    // Why: SshException.Message may contain hostname — never surface raw message
    // in user-facing output (D-07 / Pitfall 3). Use sanitized copy only.
    _ = ex; // log internally if needed, but never expose to UI
    results.Add(new SshUploadResult(localPath, false,
        "SSH upload failed — check SCP configuration and Render SSH access."));
}
```

**Config reading** (from `IConfiguration` injected in ctor; never log values):
```csharp
// Constructor reads from IConfiguration:
_host = config["Studio:Scp:Host"] ?? string.Empty;
_port = int.TryParse(config["Studio:Scp:Port"], out var p) ? p : 22;
_username = config["Studio:Scp:Username"] ?? string.Empty;
_keyFile = config["Studio:Scp:KeyFile"] ?? string.Empty;
_keyPassphrase = config["Studio:Scp:KeyPassphrase"];   // optional; null = no passphrase
_remoteArtifactRoot = config["Studio:Scp:RemoteArtifactRoot"] ?? string.Empty;
```

---

### `DeckFlow.Studio/Services/IProdStoreFactory.cs` (factory interface + implementation)

**Analog:** `DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs` (constructs a
`RelationalDatabaseConnection` from a raw connection string with normalization)

**DeckFlow.Web precedent** (verified lines 88–99):
```csharp
return new RelationalDatabaseConnection(
    RelationalDatabaseProvider.Postgres,
    NormalizePostgresConnectionString(configuredConnectionString));
```

**Interface + implementation (from RESEARCH.md — use verbatim):**
```csharp
namespace DeckFlow.Studio.Services;

/// <summary>Creates an on-demand prod <see cref="IContentSiteIndexStore"/> from a connection string.</summary>
public interface IProdStoreFactory
{
    /// <summary>Builds a Postgres-backed store from <paramref name="connectionString"/>.</summary>
    IContentSiteIndexStore Create(string connectionString);
}

/// <summary>Production implementation that wires the Postgres dialect.</summary>
public sealed class ProdStoreFactory : IProdStoreFactory
{
    /// <inheritdoc />
    public IContentSiteIndexStore Create(string connectionString)
    {
        // Why: normalize handles postgresql:// URL form from Render DATABASE_URL (D-03).
        var normalized = PostgresConnectionStringNormalizer.Normalize(connectionString);
        var conn = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, normalized);
        // Why: ContentSiteIndexStore ctor accepts any RelationalDatabaseConnection (verified
        // ContentSiteIndexStore.cs:30); no new overload needed.
        return new ContentSiteIndexStore(conn);
    }
}
```

---

### `DeckFlow.Studio.Tests/DirectPushPageTests.cs` (bUnit test class)

**Analog:** `DeckFlow.Studio.Tests/PublishPageTests.cs` (exact same bUnit test framework,
`BunitContext` subclass, `RenderXxx` helper pattern, `WaitForAssertion` / `WaitForState`,
`InvokeAsync` for DOM interactions)

**Test class skeleton** (mirror PublishPageTests.cs lines 16–59):
```csharp
public sealed class DirectPushPageTests : BunitContext
{
    private static ContentSiteIndexRow MakeApprovedRow(long id, string videoId)
        => new ContentSiteIndexRow
        {
            Id = id,
            Source = "test-channel",
            Title = $"Video {id}",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            IndexedUtc = DateTimeOffset.UtcNow,
            ApprovalStatus = "approved",
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
        };

    private (IRenderedComponent<DirectPush> Cut,
             FakeContentSiteIndexStore LocalStore,
             FakeContentSiteIndexStore ProdStore,
             FakeSshArtifactUploader Uploader,
             FakeProdStoreFactory ProdFactory)
        RenderDirectPush(
            IEnumerable<ContentSiteIndexRow>? localApproved = null,
            IEnumerable<ContentSiteIndexRow>? prodRows = null,
            bool isProdConfigured = true,
            bool isScpConfigured = true)
    {
        var localStore = new FakeContentSiteIndexStore();
        var prodStore = new FakeContentSiteIndexStore();
        var uploader = new FakeSshArtifactUploader();
        var prodFactory = new FakeProdStoreFactory(prodStore);

        foreach (var r in localApproved ?? Enumerable.Empty<ContentSiteIndexRow>())
            localStore.Rows.Add(r);
        foreach (var r in prodRows ?? Enumerable.Empty<ContentSiteIndexRow>())
            prodStore.Rows.Add(r);

        Services.AddSingleton<IContentSiteIndexStore>(localStore);
        Services.AddSingleton<ISshArtifactUploader>(uploader);
        Services.AddSingleton<IProdStoreFactory>(prodFactory);
        Services.AddSingleton(new StudioConfig(isProdConfigured, isScpConfigured));

        var cut = Render<DirectPush>();
        return (cut, localStore, prodStore, uploader, prodFactory);
    }
    // ... test methods follow
}
```

**WaitForAssertion / WaitForState + InvokeAsync dispatcher pattern** (mirror PublishPageTests.cs
lines 93–96, 201–208 — required for every DOM click after Task.Run renders):
```csharp
// Always dispatch click via InvokeAsync so the event handler ID is current after re-renders
cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
cut.WaitForAssertion(() => Assert.Contains("expected text", cut.Markup));
// For state-sensitive waits:
cut.WaitForState(() => cut.Markup.Contains("Stage 2"));
```

**Disabled-attribute assertion pattern** (PublishPageTests.cs lines 187–189):
```csharp
Assert.True(btn.HasAttribute("disabled"), "Stage-3 button must be disabled until SCP succeeds");
Assert.False(btn.HasAttribute("disabled"), "Stage-3 button must be enabled after full SCP success");
```

---

### `DeckFlow.Studio.Tests/TestDoubles/FakeSshArtifactUploader.cs` (test double)

**Analog:** `DeckFlow.Studio.Tests/TestDoubles/FakeGitRepository.cs` (canned returns +
fault injection + call recording, all in one internal sealed class)

**Pattern from FakeGitRepository.cs lines 11–54:**
- `internal sealed class` — never `public`
- Canned return properties settable per test
- `FilesToFail` for fault injection (mirrors `ThrowOnCommit`)
- `UploadedFiles` for call recording (mirrors `CommitCalls`)

**Concrete implementation (from RESEARCH.md — use verbatim):**
```csharp
internal sealed class FakeSshArtifactUploader : ISshArtifactUploader
{
    /// <summary>Paths that should be reported as failed.</summary>
    public HashSet<string> FilesToFail { get; } = new();

    /// <summary>Records paths of successfully uploaded files.</summary>
    public List<string> UploadedFiles { get; } = new();

    public Task<IReadOnlyList<SshUploadResult>> UploadArtifactsAsync(
        IReadOnlyList<string> localPaths,
        IProgress<SshUploadResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SshUploadResult>();
        foreach (var path in localPaths)
        {
            var failed = FilesToFail.Contains(path);
            if (!failed) UploadedFiles.Add(path);
            var result = new SshUploadResult(path, !failed, failed ? "Simulated failure" : null);
            results.Add(result);
            progress?.Report(result);
        }
        return Task.FromResult<IReadOnlyList<SshUploadResult>>(results);
    }
}
```

---

### `DeckFlow.Studio.Tests/TestDoubles/FakeProdStoreFactory.cs` (test double)

**Analog:** `DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs` (wraps an
existing fake; delegates all calls to it; keeps one `internal sealed class`)

**Pattern from FakeContentSiteIndexStore.cs lines 11–13:**
```csharp
internal sealed class FakeContentSiteIndexStore : IContentSiteIndexStore
{
    public List<ContentSiteIndexRow> Rows { get; } = new();
```

**Concrete implementation (from RESEARCH.md — use verbatim):**
```csharp
internal sealed class FakeProdStoreFactory : IProdStoreFactory
{
    private readonly IContentSiteIndexStore _prodStore;

    public FakeProdStoreFactory(IContentSiteIndexStore prodStore)
    {
        ArgumentNullException.ThrowIfNull(prodStore);
        _prodStore = prodStore;
    }

    /// <summary>Returns the pre-configured fake prod store; ignores the connection string.</summary>
    public IContentSiteIndexStore Create(string _) => _prodStore;
}
```

---

## Shared Patterns

### Disposal-safe InvokeAsync(StateHasChanged)
**Source:** `DeckFlow.Studio/Pages/Publish.razor` lines 322–328, 447–462
**Apply to:** all three stage handlers in `DirectPush.razor`, and the `OnInitializedAsync`
```csharp
await InvokeAsync(() =>
{
    try { StateHasChanged(); }
    catch (ObjectDisposedException) { }
    catch (InvalidOperationException) { }
});
```

### Task.Run for blocking work off Blazor sync context
**Source:** `DeckFlow.Studio/Pages/Publish.razor` lines 269–276 (OnInitializedAsync),
lines 330–464 (ExportAndDiffAsync)
**Apply to:** `ComputeDiffAsync`, `UploadArtifactsAsync`, `WriteRowsAsync` in DirectPush.razor.
All store reads, SftpClient calls, and per-row DB writes go inside `Task.Run(async () => { ... }, _cts.Token)`.

### Single in-flight guard
**Source:** `DeckFlow.Studio/Pages/Publish.razor` lines 248, 305–312
**Apply to:** all three stage button `disabled` attributes in `DirectPush.razor`
```csharp
// Set at top of every handler:
_operationInFlight = true;
// Clear in finally or after final InvokeAsync update:
_operationInFlight = false;
```

### CancellationTokenSource lifecycle
**Source:** `DeckFlow.Studio/Pages/Publish.razor` lines 251, 540–551
**Apply to:** `DirectPush.razor`
```csharp
private CancellationTokenSource _cts = new();

public void Dispose()
{
    _cts.Cancel();
    _cts.Dispose();
}
```

### Presence-only secret logging
**Source:** `DeckFlow.Studio/Program.cs` lines 38–39, 110
**Apply to:** `Program.cs` SCP detection block and startup log; never log raw config values
```csharp
Log.Information("Studio SCP: {Status}", isScpConfigured ? "configured" : "not configured");
```

### ArgumentNullException.ThrowIfNull in constructors
**Source:** `DeckFlow.Studio/Services/ActionOrchestratorProgress.cs` line 28;
`DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs` indirectly
**Apply to:** `SftpArtifactUploader` ctor, `FakeProdStoreFactory` ctor
```csharp
ArgumentNullException.ThrowIfNull(sink);
```

### bUnit `BunitContext` subclass + `Services.AddSingleton` wiring
**Source:** `DeckFlow.Studio.Tests/PublishPageTests.cs` lines 16, 36–58
**Apply to:** `DirectPushPageTests.cs` — every test class inherits `BunitContext`; fakes
registered via `Services.AddSingleton<IFoo>(fakeInstance)` before `Render<DirectPush>()`.

---

## No Analog Found

All files have close analogs. No entries in this section.

---

## Metadata

**Analog search scope:** `DeckFlow.Studio/`, `DeckFlow.Studio.Tests/`, `DeckFlow.Core/Content/`,
`DeckFlow.Core/Storage/`, `DeckFlow.Web/Services/`
**Files scanned:** 10
**Pattern extraction date:** 2026-06-16
