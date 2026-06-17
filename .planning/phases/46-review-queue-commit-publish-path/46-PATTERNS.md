# Phase 46: Review Queue + Commit-Publish Path - Pattern Map

**Mapped:** 2026-06-16
**Files analyzed:** 9 (4 new, 5 modified)
**Analogs found:** 9 / 9

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Studio/Pages/Review.razor` (NEW) | component (Blazor page) | CRUD + file-I/O | `DeckFlow.Studio/Pages/Harvest.razor` | exact |
| `DeckFlow.Studio/Pages/Publish.razor` (NEW) | component (Blazor page) | transform + event-driven (shell-out) | `DeckFlow.Studio/Pages/Harvest.razor` | role-match |
| `DeckFlow.Studio/Shared/NavMenu.razor` (MODIFY) | component (nav) | request-response | self (existing nav entries) | exact |
| `DeckFlow.Core/Content/IContentSiteIndexStore.cs` (MODIFY) | model (store interface) | CRUD | self (`GetApprovedRowsAsync`, `SetVisibilityAsync` decls) | exact |
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` (MODIFY) | service (store impl) | CRUD | self (`SetVisibilityAsync` @396, `SetEvergreenAsync` @464) | exact |
| `DeckFlow.Core/Integration/GitRepository*.cs` (NEW — git shell-out service) | service | event-driven (Process.Start) | `DeckFlow.Core/Integration/FfmpegAudioChunker.cs` | role-match |
| `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` (MODIFY — seed write + LF, D-13) | service (orchestrator) | file-I/O | `DeckFlow.CLI/ContentKbCommandRunners.cs:420-432` (`SerializeContentIndexExportRows`) | role-match (logic to relocate) |
| `DeckFlow.Studio/Services/ActionOrchestratorProgress.cs` (REUSE) | utility (progress bridge) | event-driven | self (no change) | exact |
| `DeckFlow.Studio/Program.cs` (MODIFY — DI registration of git service) | config (composition root) | n/a | self (`AddSingleton<IContentSiteIndexStore>` @50) | exact |

---

## Pattern Assignments

### `DeckFlow.Studio/Pages/Review.razor` (Blazor page, CRUD + file-I/O)

**Analog:** `DeckFlow.Studio/Pages/Harvest.razor` — the canonical Studio page. Copy its overall shape exactly: `@page` + `@implements IDisposable`, `@using` block, `@inject` services, status-driven table rows, optimistic state mutation, disposal-cancel CTS, `InvokeAsync(StateHasChanged)` marshalling.

**Page header + `@using`/`@page` pattern** (`Harvest.razor:1-14`):
```razor
@page "/harvest"
@implements IDisposable
@using DeckFlow.Core.Content
@using DeckFlow.Core.Integration
@using DeckFlow.Core.Orchestration
@using DeckFlow.Studio.Services

<PageTitle>Harvest + Distill</PageTitle>
<h1 class="h4 fw-semibold">Harvest + Distill</h1>
<p class="text-muted">Discover videos, harvest transcripts, and distill to knowledge base.</p>
<article class="content px-4">
```
→ For Review: `@page "/review"`, `<h1 class="h4 fw-semibold">Review Queue</h1>`, subtitle per UI-SPEC copy contract. Same `<article class="content px-4">` wrapper (UI-SPEC line 426 confirms the two-column sidebar + article layout).

**Service injection pattern** (`Harvest.razor:686-709`) — `[Inject]` properties, `default!`:
```csharp
[Inject]
private IContentSourceManager SourceManager { get; set; } = default!;
```
→ Review injects: `IContentSiteIndexStore IndexStore` (read queue + call new `SetApprovalStatusAsync`) and the `ContentKbOrchestratorOptions` (for `ArtifactRoot` to resolve `artifact_path` files, D-08). Resolve the absolute artifact file path by combining `ArtifactRoot` with the row's relative `ArtifactPath`.

**Checkbox table + select-all pattern** (`Harvest.razor:81-120`) — directly reuse for the queue table (UI-SPEC "Queue Table" + "Checkbox column"):
```razor
<table class="table table-sm table-hover align-middle">
    <thead class="table-light">
        <tr>
            <th scope="col" style="width:40px">
                <input type="checkbox"
                       @onclick="ToggleAllChannelSelections"
                       checked="@_allChannelSelected"
                       aria-label="Select all channel videos" />
            </th>
            ...
        </tr>
    </thead>
    <tbody>
        @foreach (var vm in _channelVideos)
        {
            <tr class="@(vm.Status == VideoStatus.Harvested ? "table-secondary" : "")">
                <td><input type="checkbox" checked="@vm.Selected"
                       @onchange="() => vm.Selected = !vm.Selected"
                       aria-label="Select @vm.Title" /></td>
                <td><a href="@vm.Url" target="_blank" rel="noopener noreferrer">@vm.Title</a></td>
                ...
            </tr>
        }
    </tbody>
</table>
```
→ Row tinting per UI-SPEC: `table-success` (approved), `table-danger` (rejected), none (pending) — swap the conditional class. External link with `target="_blank" rel="noopener noreferrer"` is already the house pattern.

**Status badge render-fragment pattern** (`Harvest.razor:1450-1458`) — copy the `switch`-to-`RenderFragment` style for the approval badge vocabulary (UI-SPEC "Approval Status Badge Vocabulary"):
```csharp
private static RenderFragment RenderBadge(VideoStatus status) => status switch
{
    VideoStatus.Distilled => @<span class="badge bg-success">Distilled</span>,
    VideoStatus.Blocked   => @<span class="badge bg-danger">Blocked</span>,
    _                     => @<span class="badge bg-secondary">Unknown</span>,
};
```
→ New approval badge: `pending` → `badge bg-secondary`, `approved` → `badge bg-success`, `rejected` → `badge bg-danger` (exactly the UI-SPEC table). Add an "Artifact missing" overlay badge `badge bg-warning text-dark` when the cached artifact read is null (D-10).

**Optimistic immediate-write action pattern (D-05)** — model on the lightweight `RaiseCapAsync` / `RemoveFromQueue` style (mutate state, no spinner for single fast ops; `Harvest.razor:1224-1239, 937-940`). Per-row approve/reject calls the NEW `IndexStore.SetApprovalStatusAsync(naturalKey, status)` then updates the row's in-memory status and re-evaluates disabled state. UI-SPEC state machine says per-row ops need no spinner; only batch ops set `_operationInFlight`.

**Batch operation + spinner-locked button pattern** (`Harvest.razor:484-510`) for "Approve Selected / Reject Selected":
```razor
<button class="btn btn-primary" @onclick="..." disabled="@(!enabled || _operationInFlight)">
    @if (_operationInFlight)
    {
        <span class="spinner-border spinner-border-sm" role="status" aria-label="Operation in progress">
            <span class="visually-hidden">Loading...</span></span>
        <span> ...</span>
    }
    else { <span>Approve Selected</span> }
</button>
```
→ Batch calls the NEW batch overload `SetApprovalStatusAsync(IReadOnlyList<key>, status)` (one round-trip, D-06). After completion: deselect all, refresh tab counts, re-render (mirror `RefreshBadgesAsync` @1421 + `finally { await InvokeAsync(StateHasChanged); }`).

**Artifact-read caching (D-08)** — no existing cache analog in `Harvest.razor`; use a plain `Dictionary<string,string?>` keyed by natural key (per UI-SPEC state machine `_expandCache`). Read the file inside `Task.Run` (off the Blazor sync context — Pitfall 1, `Harvest.razor:800`), store `null` on `FileNotFoundException`/`IOException` (graceful-degradation convention from CLAUDE.md error-handling; never crash). Null = show the D-10 warning and disable Approve.

**IDisposable cancel-on-circuit-drop (D-06)** (`Harvest.razor:1469-1479`):
```csharp
public void Dispose()
{
    _cts?.Cancel();
    _cts?.Dispose();
}
```

---

### `DeckFlow.Studio/Pages/Publish.razor` (Blazor page, transform + shell-out)

**Analog:** `DeckFlow.Studio/Pages/Harvest.razor` for the page/state shell; the git-diff/commit calls follow the **git shell-out service** assignment below. Two-stage gate (D-04) mirrors the Harvest two-stage spend gate (`Harvest.razor:514-621`) almost exactly — Stage A (Export+Diff) is the analog of the dry-run; Stage B (Commit) is the analog of "Run Distill" gated behind a reviewed-confirmation checkbox.

**Two-stage confirmation-checkbox gate** (`Harvest.razor:583-596`) — this is the precedent for D-04's reviewed-diff gate:
```razor
<div class="form-check">
    <input class="form-check-input" type="checkbox" id="distillConfirm"
           @bind="_distillSpendConfirmed" />
    <label class="form-check-label" for="distillConfirm">
        I have reviewed the estimated spend above and want to proceed with actual distillation.
    </label>
</div>
<button class="btn btn-primary" @onclick="..."
        disabled="@(!_distillSpendConfirmed || _operationInFlight)">Run Distill</button>
```
→ Publish: `@bind="_diffReviewed"`, label "I have reviewed the diff above and want to commit these changes.", Commit button `disabled="@(!_diffReviewed || _operationInFlight)"`. Reset `_diffReviewed = false` whenever a new export runs (UI-SPEC state machine).

**Result card pattern** (`Harvest.razor:558-578` dry-run card; `645-672` success card) → reuse for the Diff Preview card (`card border-primary`, UI-SPEC "Diff result card") and commit success/failure alerts. Summary counts use inline badges (`badge bg-success` Added, `badge bg-primary` Updated, `badge bg-danger` Removed).

**Scrollable monospace `<pre>` for raw diff** (`Harvest.razor:306-309`) — exact reuse for the raw `git diff` box (UI-SPEC: `max-height:300px`):
```razor
<pre class="bg-light border rounded p-2"
     style="height:200px; overflow-y:auto; font-size:0.8125rem; font-family:monospace"
     role="log" aria-live="polite">@string.Join("\n", _logLines)</pre>
```

**Task.Run + finally StateHasChanged orchestration call** (`Harvest.razor:1284-1307`) for the export+diff and commit operations:
```csharp
_operationInFlight = true; _cts = new CancellationTokenSource();
try
{
    _result = await Task.Run(() => Orchestrator.ExportIndexAndWriteAsync(...), _cts.Token);
    // then git diff via the git service
}
catch (OperationCanceledException) { ... }
finally { _operationInFlight = false; await InvokeAsync(StateHasChanged); }
```

**Branch display + push reminder** — no analog; these are new static UI per UI-SPEC copy contract. Branch comes from the git service `git rev-parse --abbrev-ref HEAD`. After successful commit, render the `alert alert-info` push reminder (D-01 — Studio never pushes).

---

### `DeckFlow.Core/Integration/GitRepository*.cs` (NEW git shell-out service)

**Analog:** `DeckFlow.Core/Integration/FfmpegAudioChunker.cs` — the cleanest `Process.Start` precedent (also `CliLlmDistillationService.cs:234-270`). Live in `DeckFlow.Core/Integration/` (Studio must NOT reference CLI; Core stays console-free — CLAUDE.md anti-pattern). Expose an interface `IGitRepository` and a `sealed` impl, registered in Studio DI.

**ProcessStartInfo + WaitForExit + ExitCode pattern** (`FfmpegAudioChunker.cs:17-33`):
```csharp
var startInfo = new ProcessStartInfo(FfmpegExecutable)
{
    Arguments = "-version",
    RedirectStandardError = true,
    RedirectStandardOutput = true,
    UseShellExecute = false,
    CreateNoWindow = true,
};
using var process = Process.Start(startInfo);
if (process is null) { return false; }
await process.WaitForExitAsync(ct).ConfigureAwait(false);
return process.ExitCode == 0;
```
→ For git, prefer **`ArgumentList`** over `Arguments` (avoids quoting bugs with paths) — `CliCommandSpec.cs` records args as `IReadOnlyList<string>` and `CliLlmDistillationService` uses `ArgumentList`. Set `WorkingDirectory = resolvedRepoRoot`. Capture stdout for diff/rev-parse: `var stdout = await process.StandardOutput.ReadToEndAsync(ct);` then `await process.WaitForExitAsync(ct)` (pattern at `CliLlmDistillationService.cs:262-270`).

**Methods this service needs (per D-02/D-03/D-11, Claude's-discretion path scoping):**
- `GetCurrentBranchAsync()` → `git rev-parse --abbrev-ref HEAD` (D-02 branch display).
- `ResolveRepoRootAsync()` → `git rev-parse --show-toplevel` (Claude's-discretion repo-root resolution; surface in UI).
- `DiffAsync(paths)` → `git diff -- {seed} {artifacts...}` (D-11 raw textual diff; D-12 raw box). Also diff vs HEAD's committed seed for the in-memory key comparison.
- `CatHeadSeedAsync()` → `git show HEAD:content-kb/seed/index-seed.json` for the in-memory Added/Updated/Removed natural-key comparison (D-11/D-12 friendly counts).
- `StageAndCommitAsync(paths, message)` → `git add -- {seed} {artifacts...}` then `git commit -m {message}` (D-03 stage both; Claude's-discretion path-scoping — never `git add -A`). Return the short SHA (parse `git rev-parse --short HEAD`).

**Error handling** — catch non-zero `ExitCode`, surface `StandardError` text as the failure reason (UI-SPEC: "Could not compute git diff — {reason}", "Commit failed — {reason}"). Follow CLAUDE.md: translate process failures to a typed result/exception with the captured stderr; never swallow silently for the commit path (operator must see it).

---

### `DeckFlow.Core/Content/IContentSiteIndexStore.cs` + `ContentSiteIndexStore.cs` (MODIFY — D-06 mutation method)

**Analog:** the existing `SetVisibilityAsync` / `SetEvergreenAsync` decls and impls in the same files. This is an exact same-shape extension.

**Interface decl pattern** (`IContentSiteIndexStore.cs:80-96`) — copy the `SetVisibilityAsync` / `SetHiddenAsync` doc + signature shape:
```csharp
/// <summary>
/// Sets visibility for a single site-index row.
/// </summary>
/// <param name="id">Site-index row identifier.</param>
/// <param name="visible">Whether the row should be visible.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The number of rows updated.</returns>
Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default);
```
→ Add (D-06): single + batch overloads keyed by **natural key** (CONTEXT specifies `naturalKey`, not surrogate id — keeps Studio decoupled from row ids):
```csharp
Task<int> SetApprovalStatusAsync(string naturalKeyType, string naturalKeyValue, string status, CancellationToken cancellationToken = default);
Task<int> SetApprovalStatusAsync(IReadOnlyList<(string Type, string Value)> keys, string status, CancellationToken cancellationToken = default);
```
Constrain `status` to `pending`/`approved`/`rejected` — validate at top with `ArgumentException` (CLAUDE.md: argument validation at top). The natural-key shape mirrors `GetByNaturalKeyAsync(naturalKeyType, naturalKeyValue, ...)` already on the interface (`IContentSiteIndexStore.cs:46-49`).

**Impl UPDATE pattern (Dapper, `ExecuteAsync` + `CommandDefinition`)** (`ContentSiteIndexStore.cs:396-410`):
```csharp
public async Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
{
    await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
    await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    return await connection.ExecuteAsync(new CommandDefinition(
        """
        UPDATE content_site_index
           SET is_visible = @visible,
               is_hidden = FALSE
         WHERE id = @id;
        """,
        new { visible, id },
        cancellationToken: cancellationToken)).ConfigureAwait(false);
}
```
→ Single: `UPDATE content_site_index SET approval_status = @status WHERE natural_key_type = @type AND natural_key_value = @value;`. Batch: Dapper supports `IN` expansion — `WHERE (natural_key_type, natural_key_value) IN ...` is awkward in SQLite; prefer parameterizing on `natural_key_value IN @values` filtered by a single `natural_key_type` per call, OR loop the single UPDATE inside one connection/transaction. The CONTEXT requirement is "one round-trip" — Dapper `ExecuteAsync` with a list parameter executes the command per item on one open connection, which satisfies it. The existing `WHERE approval_status='approved'` read (`GetApprovedRowsAsync` @325) confirms the column + literal-status vocabulary.

**Self-healing ALTER already present** (`ContentSiteIndexStore.cs:86-92`) — `approval_status TEXT NOT NULL DEFAULT 'pending'` column already exists; **no schema change needed** this phase. The mutation method only needs `EnsureSchemaAsync` at the top (which it calls anyway).

---

### `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` (MODIFY — D-13 LF seed write)

**Analog / relocation source:** `DeckFlow.CLI/ContentKbCommandRunners.cs:420-432` (`SerializeContentIndexExportRows`) and `:339-341` (the `File.WriteAllTextAsync`). Today `ExportIndexAsync` (`ContentKbOrchestrator.cs:716-740`) returns only in-memory rows; the JSON serialize + file write live in the CLI. Studio cannot reference the CLI, so the serialize+write must be available from Core.

**Existing in-memory export** (`ContentKbOrchestrator.cs:716-731`):
```csharp
public async Task<ContentIndexExportResult> ExportIndexAsync(IOrchestratorProgress? progress = null, CancellationToken cancellationToken = default)
{
    await _indexStore.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
    var rows = await _indexStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false);
    var exportRows = rows.Select(ContentIndexExportRow.From).ToList();
    return new ContentIndexExportResult { Success = true, Rows = exportRows, RowCount = exportRows.Count };
}
```

**LF-write logic to replicate (the D-13 byte-shape contract — already correct in CLI; preserve exactly)** (`ContentKbCommandRunners.cs:420-432`):
```csharp
var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
});
return json + "\n";
```
→ D-13 hardening: add a Core method (e.g. `ExportIndexToFileAsync(string seedPath, ...)`) that serializes with the **same** `JsonSerializerOptions` and writes with explicit `\n`. `WriteIndented = true` on Windows can emit `\r\n` between tokens in some runtimes — to guarantee SC5 (`file index-seed.json` → ASCII, not CRLF), normalize the serialized string (`json.Replace("\r\n", "\n")`) before appending the trailing `"\n"`, then `File.WriteAllTextAsync(seedPath, body)`. **Do NOT change the JSON shape** — the Phase 42 golden test pins the byte-shape (CONTEXT canonical-refs: "LF + membership only"). Seed path is `content-kb/seed/index-seed.json` resolved relative to the git repo root (Claude's-discretion path resolution).

**File write precedent** (Core already does `File.WriteAllText`): `ContentArtifactWriter.cs:111`, `DeltaExporter.cs:29` — Core writing files is an established convention, so adding the seed write to Core is in-pattern.

---

### `DeckFlow.Studio/Shared/NavMenu.razor` (MODIFY)

**Analog:** the existing Harvest nav entry in the same file (`NavMenu.razor:17-21`):
```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="harvest">
        <span class="oi oi-cloud-download" aria-hidden="true"></span> Harvest
    </NavLink>
</div>
```
→ Insert the two new entries below it (exact markup given in UI-SPEC lines 142-152): `href="review"` with `oi oi-task`, `href="publish"` with `oi oi-cloud-upload`. Nav order: Home / Harvest / Review / Publish.

---

### `DeckFlow.Studio/Program.cs` (MODIFY — DI for git service)

**Analog:** existing singleton registrations in the same file (`Program.cs:48-51, 83`):
```csharp
builder.Services.AddSingleton<IContentSiteIndexStore>(_ => new ContentSiteIndexStore(contentKbDatabasePath));
builder.Services.AddSingleton<IFfmpegAudioChunker, FfmpegAudioChunker>();
```
→ Register the new git service: `builder.Services.AddSingleton<IGitRepository, GitRepository>();` (or a factory if it needs the resolved repo root). `IContentSiteIndexStore` is already registered — Review/Publish pages just `@inject` it. `ContentKbOrchestratorOptions.ArtifactRoot` (@93-96) is the artifact-root the Review page needs for D-08 file reads; the git service needs the repo root (resolve via `git rev-parse --show-toplevel`, not `ArtifactRoot`).

---

### `DeckFlow.Studio/Services/ActionOrchestratorProgress.cs` (REUSE — no change)

The progress-sink bridge (`ActionOrchestratorProgress.cs:15-43`) is reused as-is for any orchestrator progress reporting on the Publish page (export progress). Wrap the sink in the disposal-safe `InvokeAsync` closure exactly as `Harvest.razor:1097-1107`:
```csharp
var progress = new ActionOrchestratorProgress(msg =>
    InvokeAsync(() =>
    {
        try { _logLines.Add(msg); StateHasChanged(); }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }));
```

---

## Shared Patterns

### Optimistic immediate-write (D-05)
**Source:** `Harvest.razor:1224-1239` (`RaiseCapAsync` — mutate + `StateHasChanged`, no spinner for fast ops).
**Apply to:** Review.razor per-row Approve/Reject. Call `SetApprovalStatusAsync` → update in-memory row status → re-render. No staged-save step.

### Disposal-safe async bridge to the Blazor circuit
**Source:** `Harvest.razor:1097-1107` (progress sink) + `1469-1479` (Dispose cancels CTS).
**Apply to:** all async ops in Review.razor and Publish.razor. Always `await InvokeAsync(StateHasChanged)` in `finally`; swallow `ObjectDisposedException`/`InvalidOperationException` from a dropped circuit.

### Off-sync-context IO (Pitfall 1)
**Source:** `Harvest.razor:800` (`await Task.Run(() => ...)`).
**Apply to:** artifact file reads (Review, D-08), `ExportIndexToFileAsync`, and every git `Process.Start` call (Publish). Never block the Blazor sync context on IO.

### Process.Start shell-out
**Source:** `FfmpegAudioChunker.cs:17-33`, `CliLlmDistillationService.cs:234-270`.
**Apply to:** the new `GitRepository` service. `UseShellExecute=false`, `CreateNoWindow=true`, `RedirectStandardOutput/Error=true`, `ArgumentList` (not `Arguments`), `WorkingDirectory=repoRoot`, `await WaitForExitAsync(ct)`, check `ExitCode`, surface stderr on failure.

### Store mutation method (Dapper UPDATE)
**Source:** `ContentSiteIndexStore.cs:396-410` (`SetVisibilityAsync`).
**Apply to:** the new `SetApprovalStatusAsync` (single + batch). `EnsureSchemaAsync` first, `OpenConnectionAsync`, `ExecuteAsync(new CommandDefinition(...))` with parameterized SQL. `approval_status` column already exists (@86-92) — no ALTER.

### LF-everywhere artifact write (D-13 / SC5)
**Source:** `ContentKbCommandRunners.cs:420-432` (`json + "\n"`).
**Apply to:** the new Core seed-write method. Same `JsonSerializerOptions` (camelCase, indented), normalize `\r\n`→`\n`, trailing `\n`. Belt-and-suspenders with `.gitattributes` LF rule. Shape is golden-test-pinned — membership + LF only.

### Studio→Core decoupling (architectural invariant)
**Source:** CLAUDE.md anti-patterns + `ContentKbOrchestratorSmokeService.cs` (Studio resolves Core orchestrator slices only).
**Apply to:** the git service and seed-write must live in `DeckFlow.Core` (or be invoked through Core); Studio must NOT reference `DeckFlow.CLI`, and Core must stay console-free (the new git service uses `Process`, not `Console`).

---

## No Analog Found

| File / Concern | Role | Data Flow | Reason |
|----------------|------|-----------|--------|
| Inline expand/collapse row with cached artifact read (D-08/D-09) | component | file-I/O | `Harvest.razor` has no expand-row or per-row file cache; the table + badge + Task.Run patterns transfer, but the expand markup + `Dictionary<string,string?>` cache and the `colspan` full-width `<tr>` are new (UI-SPEC dictates exact markup). |
| In-memory natural-key diff (Added/Updated/Removed counts, D-11/D-12) | utility | transform | No existing diff-of-two-seed-sets comparator in the codebase. New pure helper: parse HEAD's `index-seed.json` (via `git show HEAD:...`) vs the freshly exported approved rows, key by `(naturalKeyType, naturalKeyValue)`, classify Added/Updated/Removed. Use the pinned `ContentIndexExportRow` shape for parsing. |
| Filter-tab bar with live count badges (UI-SPEC) | component | request-response | No nav-tabs analog in Studio. Standard Bootstrap `nav nav-tabs`; counts derived from the loaded queue grouped by `approval_status`. Switching tabs clears selections. |

---

## Metadata

**Analog search scope:** `DeckFlow.Studio/Pages/`, `DeckFlow.Studio/Shared/`, `DeckFlow.Studio/Services/`, `DeckFlow.Core/Content/`, `DeckFlow.Core/Orchestration/`, `DeckFlow.Core/Integration/`, `DeckFlow.CLI/ContentKbCommandRunners.cs`.
**Files scanned:** ~12 source files read in full or targeted ranges.
**Pattern extraction date:** 2026-06-16
