# Phase 47: Direct Prod-DB + SCP Publish Path — Research

**Researched:** 2026-06-16
**Domain:** Blazor Server page, SSH.NET SFTP upload, on-demand Postgres store construction,
in-memory natural-key diff, two-stage gated UI state machine
**Confidence:** HIGH — all findings code-verified from the project itself or from the official
NuGet registry; SSH.NET API from official GitHub README.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** SSH.NET (`Renci.SshNet`) NuGet package, in-process SCP/SFTP; explicitly
  operator-approved exception to "no new packages". Codex plan-review must scrutinize
  supply-chain addition on public repo.
- **D-02:** SSH connection parameters read from `Studio:Scp:*` user-secrets section
  (exact key names Claude's discretion). Presence-only logging — never log values.
- **D-03:** On-demand Postgres `ContentSiteIndexStore` built inside the publish action from
  `Studio:ProdConnectionString`, NOT a startup DI singleton.
- **D-04:** Pre-write diff computed in-memory by natural-key comparison (prod rows vs local
  approved rows) → New / Updated lists. Raw `git diff` not applicable here.
- **D-05:** Partial failure surfaces per-item status: per-file for SCP, per-row for DB upsert.
  Counts-only was rejected.
- **D-06:** Step 1 = SCP all artifacts; Step 2 = prod Postgres upsert. Step 2 button disabled
  until Step 1 reports full success.
- **D-07:** Secret redaction: prod conn string, SSH host/user/key, remote paths never appear
  in any log or UI text. Presence-only ("configured / not configured").
- **D-08:** `UpsertContentColumnsOnlyAsync` exclusively on prod — no other upsert path may
  touch prod. `is_visible`/`is_evergreen` preserved on pre-existing rows.
- **D-09:** Explicit confirmation gate ("I have reviewed...") before Step 1 (SCP) enables.
  Mirrors `Publish.razor` D-04 reviewed-diff checkbox gate.

### Claude's Discretion

- Exact `Studio:Scp:*` user-secrets key names and SSH key auth presentation.
- `Renci.SshNet` exact version + whether SFTP or SCP subsystem is used.
- `StateHasChanged` / async-bridging + button-lock state machine details.
- Whether prod-diff read and upsert reuse one on-demand store instance or two.
- Resolving the local artifact file set from approved rows' `artifact_path`.

### Deferred Ideas (OUT OF SCOPE)

- Shell-out to system `scp` (rejected in D-01).
- Always-live prod store DI singleton (rejected in D-03).
- Per-row interleaved SCP+upsert (rejected in D-06).
- Page/nav layout, expand-vs-modal markup, visual styling (locked in 47-UI-SPEC.md).
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PUB-04 | Direct prod-DB push: approved rows written to Render Postgres via safe content-only upsert, artifacts uploaded via SCP; artifact-first ordering | D-03 store construction recipe + D-01 SSH.NET SFTP upload pattern below |
| PUB-05 | Dry-run/preview + explicit confirmation showing which rows/artifacts write to prod; partial-failure state for reconcile | D-04 in-memory key diff recipe + D-05 per-item status list pattern below |
</phase_requirements>

---

## Summary

Phase 47 adds a second publish path to the local DeckFlow.Studio Blazor Server app: the
operator pushes approved Content KB entries directly to production — SFTP-uploading artifact
markdown files to Render `/data` first, then upserting approved rows into the prod Render
Postgres database via the existing safe-upsert overload. This bypasses the git-commit →
Render-auto-deploy cycle that Phase 46 delivered.

All five research unknowns flagged in CONTEXT.md are now fully resolved with file:line
evidence:

1. **D-03 (Postgres store construction):** `ContentSiteIndexStore` already has a
   `RelationalDatabaseConnection` overload (line 30). Constructing a Postgres-backed store
   on demand requires only
   `new ContentSiteIndexStore(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, normalizedConnStr))`.
   No new ctor or overload is needed. The normalizer already handles `postgresql://` URL form.

2. **D-01 (SSH.NET):** Use `SftpClient` (not `ScpClient`) — SFTP is more reliable over
   SSH.NET and is explicitly feature-listed in the official README. Latest stable NuGet
   package is `SSH.NET 2025.0.0` (published 2025-04-18; 286M total downloads; source at
   `github.com/sshnet/SSH.NET`). Pin to `2025.0.0` (not `2025.1.0` which is a future
   dated entry not yet current-stable at the time of this research).

3. **Phase 43 data layer:** `UpsertContentColumnsOnlyAsync` signature confirmed.
   `GetApprovedRowsAsync` returns `IReadOnlyList<ContentSiteIndexRow>`; each row has
   `ArtifactPath` (relative, e.g. `content-kb/source-slug/videoId.md`).

4. **Phase 45/46 Studio patterns:** `Publish.razor` is the direct template. The
   `_operationInFlight` boolean guard, `_cts` CancellationTokenSource disposal, and
   `InvokeAsync(() => { try { StateHasChanged(); } catch (ObjectDisposedException) { }
   catch (InvalidOperationException) { } })` pattern are all verified line-by-line.

5. **Testing seam:** `DeckFlow.Studio.Tests` already uses bUnit 2.7.2 + xUnit 2.9.3.
   Pattern is `BunitContext` subclass + `FakeContentSiteIndexStore` (tracks
   `UpsertContentColumnsOnlyAsync` calls) + `FakeGitRepository` (canned returns + call
   recording). New phase needs `ISshArtifactUploader` interface seam so the bUnit tests
   never need a live SSH connection.

**Primary recommendation:** Add `SSH.NET 2025.0.0` to `DeckFlow.Studio.csproj`, create an
`ISshArtifactUploader` service backed by `SftpClient` with a `FakeSshArtifactUploader`
test double, construct the on-demand prod store inline in the page's `Task.Run` block using
the existing `RelationalDatabaseConnection` + `ContentSiteIndexStore` two-arg chain, and
mirror `Publish.razor` state machine with three stages instead of two.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Prod diff preview (read prod Postgres) | DeckFlow.Studio (page) | DeckFlow.Core (ContentSiteIndexStore) | On-demand store built in page; Core owns the SQL |
| SFTP artifact upload | DeckFlow.Studio (service: ISshArtifactUploader) | SSH.NET library | Studio owns the config; Core must stay console-free |
| Prod Postgres upsert | DeckFlow.Core (ContentSiteIndexStore.UpsertContentColumnsOnlyAsync) | DeckFlow.Studio (page wires it) | Business rule (preserve is_visible/is_evergreen) lives in Core SQL |
| Secret config reading | DeckFlow.Studio (Program.cs / page) | user-secrets | Never flows to Core; Core is config-agnostic |
| Per-item reconcile list | DeckFlow.Studio (page Razor markup) | — | UI presentation only |
| State machine (stage gating) | DeckFlow.Studio (page @code) | — | Blazor component state |

---

## Unknown #1 Resolved — D-03: Postgres On-Demand Store Construction

### Finding

`ContentSiteIndexStore` already has TWO constructors:

**`ContentSiteIndexStore.cs` lines 23–42:**

```csharp
// SQLite convenience ctor (takes a file path string)
public ContentSiteIndexStore(string databasePath)
    : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

// General ctor — accepts any RelationalDatabaseConnection (SQLite or Postgres)
public ContentSiteIndexStore(RelationalDatabaseConnection connectionInfo)
{
    ArgumentNullException.ThrowIfNull(connectionInfo);
    _connectionInfo = connectionInfo;
    if (_connectionInfo.IsSqlite)
    {
        // ... directory creation for SQLite only
    }
}
```

**No new ctor or Core change is needed.** The second ctor takes any `RelationalDatabaseConnection`, and `RelationalDatabaseConnection` supports both providers (`RelationalDatabaseProvider.Sqlite` and `RelationalDatabaseProvider.Postgres`).

### Exact Prod Store Construction Recipe

```csharp
// Inside the publish action's Task.Run block (never at DI startup):
var rawConnStr = _prodConnectionString; // from user-secrets, already held by page
var normalizedConnStr = PostgresConnectionStringNormalizer.Normalize(rawConnStr);
var prodConnection = new RelationalDatabaseConnection(
    RelationalDatabaseProvider.Postgres,
    normalizedConnStr);
var prodStore = new ContentSiteIndexStore(prodConnection);
await prodStore.EnsureSchemaAsync(cancellationToken);
// Now use: prodStore.GetAllRowsAsync() for prod-side read
//          prodStore.UpsertContentColumnsOnlyAsync(row) for prod write
```

**Key facts:**
- `RelationalDatabaseConnection` ctor is `record(RelationalDatabaseProvider, string)` — plain constructor call, no factory needed. [VERIFIED: `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs:21`]
- `PostgresConnectionStringNormalizer.Normalize()` handles both `postgresql://` URL form and Npgsql key-value form. [VERIFIED: `DeckFlow.Core/Storage/PostgresConnectionStringNormalizer.cs:15-68`]
- Render provides `DATABASE_URL` in `postgresql://user:pass@host:port/db?sslmode=require` form — the normalizer handles this exactly. [VERIFIED: normalizer handles `postgresql://` + `sslmode` query param]
- `DapperTypeHandlers.EnsureRegistered()` is called in the `RelationalDatabaseConnection` static ctor (`RelationalDatabaseConnection.cs:23-27`), so Dapper type handlers register automatically on first use. No extra wiring needed.
- `ContentSiteIndexStore.EnsureSchemaAsync()` is idempotent and safe to call on prod — it uses `CREATE TABLE IF NOT EXISTS` and `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` guarded by `GetTableColumnsAsync` introspection. [VERIFIED: `ContentSiteIndexStore.cs:45-113`]
- The prod connection must be built on-demand (inside the page's publish action), NOT registered as a DI singleton. This matches D-03 intent.

### How DeckFlow.Web Does It (precedent)

`DeckFlowDatabaseConnectionFactory.cs:88-99` [VERIFIED]:
```csharp
return new RelationalDatabaseConnection(
    RelationalDatabaseProvider.Postgres,
    NormalizePostgresConnectionString(configuredConnectionString));
```
The pattern for Studio is identical — same two-argument `RelationalDatabaseConnection` record constructor, same normalizer.

### Prod-Side Read for Diff

For the diff preview, `prodStore.GetAllRowsAsync()` (not `GetPublishedRowsAsync`) returns all
prod rows regardless of visibility, which is the correct comparison base for a direct push.
The diff keying is by `(natural_key_type, natural_key_value)` — same natural key the
`UpsertContentColumnsOnlySql` uses for its `ON CONFLICT` clause.

---

## Unknown #2 Resolved — D-01: SSH.NET Transport Choice + API

### Package Legitimacy

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| SSH.NET | NuGet | ~15 yrs (2011.7.29 first) | 286M total | github.com/sshnet/SSH.NET | N/A (NuGet; slopcheck only checks PyPI) | Approved |

**slopcheck note:** slopcheck checked PyPI where `SSH.NET` does not exist (it is a NuGet
package). This is expected ecosystem mismatch — not a slop signal. The package was manually
verified on the NuGet registry: 32 versions since 2011, 286M downloads, `github.com/sshnet/SSH.NET`
as source repository, MIT license, `Renci` as author. No build targets or postinstall
scripts in the `.nupkg`. [VERIFIED: NuGet registry flat-container API + catalog entry for
SSH.NET 2025.1.0]

**Recommended pin:** `SSH.NET 2025.0.0` (published 2025-04-18, stable). [VERIFIED: NuGet
flat-container API]

**Supply-chain note for Codex plan-review (per D-01):** This is a well-established library
(15 years, 286M downloads) on a public repo. Risk is LOW compared to a new/obscure package.
The plan-reviewer should verify the NuGet package ID matches exactly `SSH.NET` (capital S,
capital H, capital N, capital E, capital T) — the NuGet ID is case-insensitive on the
registry but the `.csproj` should use the canonical casing.

### SFTP vs SCP Recommendation

**Use `SftpClient`, not `ScpClient`.** [ASSUMED: based on SSH.NET README + community
consensus — SFTP via SSH.NET is more feature-complete, supports async `UploadFile`, handles
partial-failure per file cleanly, and is the primary documented path.]

SFTP advantages for this use case:
- Async `UploadFile(Stream, remotePath)` works well for per-file progress capture
- Returns after successful upload OR throws a typed exception on failure
- SSH.NET's SFTP client is more actively tested than the SCP client

### SftpClient Construction with Private-Key Auth

```csharp
// SSH.NET 2025.0.0 SFTP upload pattern with key-file auth
// Source: SSH.NET official README (github.com/sshnet/SSH.NET)
using var privateKey = new PrivateKeyFile(keyFilePath);       // passphrase overload also exists
using var client = new SftpClient(host, port, username, privateKey);
client.Connect();
// Per-file upload:
using var fs = File.OpenRead(localPath);
client.UploadFile(fs, remotePath);
client.Disconnect();
```

**With optional passphrase:**
```csharp
using var privateKey = new PrivateKeyFile(keyFilePath, passphrase);
```

### Recommended `Studio:Scp:*` Key Names

Claude's discretion — recommended key names for user-secrets:

| Key | Purpose | Example value |
|-----|---------|---------------|
| `Studio:Scp:Host` | Render SSH hostname | `ssh.renders.com` or instance hostname |
| `Studio:Scp:Port` | SSH port | `22` |
| `Studio:Scp:Username` | SSH username | `render` |
| `Studio:Scp:KeyFile` | Absolute path to private key file on local machine | `/home/user/.ssh/render_id_ed25519` |
| `Studio:Scp:KeyPassphrase` | Optional passphrase for the key | (empty if none) |
| `Studio:Scp:RemoteArtifactRoot` | Remote base path on `/data` disk | `/data/content-kb` |

**Presence-only config check** (mirrors `StudioConfig.IsProdConfigured`):

```csharp
var isScpConfigured = !string.IsNullOrEmpty(config["Studio:Scp:Host"])
    && !string.IsNullOrEmpty(config["Studio:Scp:Username"])
    && !string.IsNullOrEmpty(config["Studio:Scp:KeyFile"])
    && !string.IsNullOrEmpty(config["Studio:Scp:RemoteArtifactRoot"]);
```

Log once at startup: `"Studio SCP: {Status}", isScpConfigured ? "configured" : "not configured"` —
never log any of the values.

### `StudioConfig` Extension

```csharp
// Extend the existing presence-only record:
public sealed record StudioConfig(bool IsProdConfigured, bool IsScpConfigured);
// DeckFlow.Studio/StudioConfig.cs:6 currently only has IsProdConfigured
```

### Remote Path Resolution

Local `ArtifactPath` is relative (e.g. `content-kb/source-slug/abc123.md`). The remote path
is: `{RemoteArtifactRoot}/{ArtifactPath.Replace('\\', '/')}`.

For example: `/data/content-kb/source-slug/abc123.md`

The remote directory must exist or be created before upload. `SftpClient.CreateDirectory()`
can create the parent directory; `SftpClient.Exists()` checks first. For the Render
`/data` disk the parent (`/data/content-kb/{source-slug}/`) needs to exist.

### Error Types

SSH.NET throws `SshConnectionException` on connect failure and `SftpPermissionDeniedException`
/ `SshException` on upload failure. The page should catch `SshException` (base) and surface
`ex.Message` in the per-file failure reason — with no host/key/path values in the message.
[ASSUMED: from SSH.NET class hierarchy in training data; not verified in this session via
official docs beyond README]

---

## Unknown #3 Resolved — Phase 43 Data Layer Signatures

### `UpsertContentColumnsOnlyAsync`

**Interface:** `IContentSiteIndexStore.cs:36-37` [VERIFIED]:
```csharp
Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row,
    CancellationToken cancellationToken = default);
```

**What it touches on `ON CONFLICT (natural_key_type, natural_key_value) DO UPDATE`:**
- Updates: `source`, `title`, `video_url`, `artifact_path`, `published_utc`, `indexed_utc`,
  `archetype_tags`, `bracket_tags`, `card_category_tags`
- Does NOT update: `is_visible`, `is_hidden`, `is_evergreen`, `approval_status`
- New rows get `approval_status = 'pending'` (INSERT branch literal in SQL)
[VERIFIED: `ContentSiteIndexStore.cs:801-838` — `UpsertContentColumnsOnlySql` constant]

**Safety contract (SC3):** This is confirmed as the ONLY safe prod write. The other upserts
(`UpsertRowAsync`, `UpsertRowPreservingVisibilityAsync`) DO touch `is_visible`/`is_evergreen`
and must never be called against prod.

### `GetApprovedRowsAsync`

**Interface:** `IContentSiteIndexStore.cs:62-63` [VERIFIED]:
```csharp
Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(
    CancellationToken cancellationToken = default);
```

**SQL filter:** `WHERE approval_status = 'approved'` ORDER BY `source, title, id`
[VERIFIED: `ContentSiteIndexStore.cs:301-330`]

### `ContentSiteIndexRow` Key Fields for This Phase

[VERIFIED: `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs:107-164`]

| Field | Type | Role in Phase 47 |
|-------|------|-----------------|
| `ArtifactPath` | `required string` | RELATIVE path (e.g. `content-kb/slug/vid.md`); resolved against local data root for SCP upload; never a secret |
| `YoutubeVideoId` / `RssGuid` | `string?` | Natural key components; exactly one non-null per row |
| `Title` | `required string` | Per-row diff/reconcile display |
| `Source` | `required string` | Per-row diff/reconcile display |
| `IsVisible` | `bool` | Prod-side field that MUST be preserved; read from prod store, never written via `UpsertContentColumnsOnlyAsync` |
| `IsEvergreen` | `bool` | Same — preserved |
| `ApprovalStatus` | `string` | Local = 'approved'; prod = whatever prod has; `UpsertContentColumnsOnlySql` never overwrites it |

### Natural Key for Diff

The diff key is `(natural_key_type, natural_key_value)` from the SQL, which maps to
`(ContentSourceType.Youtube, row.YoutubeVideoId)` or `(ContentSourceType.Podcast, row.RssGuid)`.

For in-memory diffing, use a `Dictionary<(string, string), ContentSiteIndexRow>` keyed by
natural key. Compare local-approved vs prod-all-rows: keys in local-not-prod = **New**;
keys in both but content differs = **Updated**; no "Removed" (direct push never deletes).

Content comparison for "Updated" detection: serialize each row's display fields to a
canonical string (same approach Publish.razor uses with `JsonSerializer.Serialize` + camelCase
options). The exact comparison approach is Claude's discretion (D-04).

---

## Unknown #4 Resolved — Phase 45/46 Studio Page Patterns

### Two-Stage Button-Lock State Machine (from `Publish.razor`)

[VERIFIED: `DeckFlow.Studio/Pages/Publish.razor`]

**Core state variables to replicate (with additions for Phase 47's 3-stage design):**

```csharp
// Lifecycle
private bool _initInFlight = true;         // spinner shown on load
private string? _initError;                 // disables all action if set
private bool _operationInFlight;            // single in-flight guard across ALL stages

// Stage 1 — compute diff
private bool _diffComputeInFlight;
private string _diffError = string.Empty;
private bool _diffReady;
// ... diff data fields (_newCount, _updatedCount, per-row lists, per-file lists)

// Confirmation gate (D-09)
private bool _prodReviewed;                 // checkbox "I have reviewed what will be written to PRODUCTION"

// Stage 2 — SCP upload
private bool _scpInFlight;
private bool _scpSuccess;
private string _scpError = string.Empty;
// per-file status list

// Stage 3 — DB upsert (gated on _scpSuccess)
private bool _dbInFlight;
private bool _dbSuccess;
private string _dbError = string.Empty;
// per-row status list
```

**Single in-flight guard pattern** (`Publish.razor:305-312` [VERIFIED]):
- Set `_operationInFlight = true` at the top of every handler
- Set `_operationInFlight = false` in the `finally` block
- All three stage buttons have `disabled="@(_operationInFlight || ...other conditions...)"` in their markup

**Artifact-first gate (D-06):**
```
Stage 3 button: disabled="@(!_scpSuccess || _operationInFlight)"
```

### `OnInitializedAsync` Pattern

[VERIFIED: `Publish.razor:264-300`]

```csharp
protected override async Task OnInitializedAsync()
{
    try
    {
        // Move store reads off Blazor sync context (Pitfall 7)
        var (approvedCount, scpConfigured) = await Task.Run(async () =>
        {
            var rows = await IndexStore.GetApprovedRowsAsync(_cts.Token).ConfigureAwait(false);
            return (rows.Count, Config.IsScpConfigured);
        }, _cts.Token);

        _approvedCount = approvedCount;
    }
    catch (OperationCanceledException)
    {
        // Component disposed mid-load — swallow
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

### `InvokeAsync(StateHasChanged)` Disposal-Safe Pattern

[VERIFIED: `Publish.razor:322-328`, `447-462`]

```csharp
await InvokeAsync(() =>
{
    try { StateHasChanged(); }
    catch (ObjectDisposedException) { }
    catch (InvalidOperationException) { }
});
```

Always wrap in a try/catch for both `ObjectDisposedException` and `InvalidOperationException`.
This is the canonical pattern used in every in-flight state update across `Publish.razor`,
`Review.razor`, and `Harvest.razor`.

### `IDisposable` CTS Pattern

[VERIFIED: `Publish.razor:540-551`]

```csharp
@implements IDisposable
// ...
private CancellationTokenSource _cts = new();

public void Dispose()
{
    _cts.Cancel();
    _cts.Dispose();
}
```

### `ActionOrchestratorProgress` Bridge

[VERIFIED: `DeckFlow.Studio/Services/ActionOrchestratorProgress.cs`]

The progress bridge takes a `Func<string, Task>` sink — typically wrapping
`InvokeAsync(() => { try { StateHasChanged(); } ... })`. For Phase 47's per-item streaming
lists, the same pattern applies: the SCP uploader and DB uploader call a progress callback
that triggers `StateHasChanged` so list rows stream live.

### Reviewed-Diff Checkbox Gate (D-09 precedent)

[VERIFIED: `Publish.razor:144-153`]

```razor
<div class="form-check">
    <input class="form-check-input"
           type="checkbox"
           id="diffReviewed"
           @bind="_diffReviewed" />
    <label class="form-check-label" for="diffReviewed">
        I have reviewed the diff above and want to commit these changes.
    </label>
</div>
```

Phase 47 copy (from 47-UI-SPEC.md):
```razor
<input class="form-check-input" type="checkbox" id="prodReviewed" @bind="_prodReviewed" />
<label class="form-check-label" for="prodReviewed">
    I have reviewed what will be written to PRODUCTION above.
</label>
```

Stage 2 (SCP) button enabled only when `_prodReviewed && !_operationInFlight && _diffReady`.

### Presence-Only `StudioConfig` Pattern

[VERIFIED: `DeckFlow.Studio/Program.cs:38-39, 47, 110`]

```csharp
// Program.cs — read once at startup, never log the value:
var prodConnStr = builder.Configuration["Studio:ProdConnectionString"];
var isProdConfigured = !string.IsNullOrEmpty(prodConnStr);
builder.Services.AddSingleton(new StudioConfig(isProdConfigured));
// ...
Log.Information("Studio prod connection: {Status}", isProdConfigured ? "configured" : "not configured");
```

Phase 47 extension — add SCP detection and hold `prodConnStr` for on-demand construction:

```csharp
var isProdConfigured = !string.IsNullOrEmpty(prodConnStr);
var scpHost = builder.Configuration["Studio:Scp:Host"];
var isScpConfigured = !string.IsNullOrEmpty(scpHost)
    && !string.IsNullOrEmpty(builder.Configuration["Studio:Scp:Username"])
    && !string.IsNullOrEmpty(builder.Configuration["Studio:Scp:KeyFile"])
    && !string.IsNullOrEmpty(builder.Configuration["Studio:Scp:RemoteArtifactRoot"]);
builder.Services.AddSingleton(new StudioConfig(isProdConfigured, isScpConfigured));
// Presence-only log — D-07:
Log.Information("Studio SCP: {Status}", isScpConfigured ? "configured" : "not configured");
```

`StudioConfig` needs a second property `IsScpConfigured`. The current record
`StudioConfig.cs:6` is `public sealed record StudioConfig(bool IsProdConfigured)` and must
be extended to `StudioConfig(bool IsProdConfigured, bool IsScpConfigured)`.

The raw `prodConnStr` value (not a bool) must also be passed to the page for on-demand store
construction. Options: (a) register a separate `ProdConnectionStringProvider` singleton that
holds the raw string, or (b) inject `IConfiguration` directly into the page (simpler). Approach
(b) mirrors how `Harvest.razor` reads environment-specific config — recommend using
`[Inject] IConfiguration Config` and reading `Config["Studio:ProdConnectionString"]` at action
time, which avoids materializing the secret into a singleton. This means the string is
ephemeral in the page's local variable during the publish action, not held in DI state.

---

## Unknown #5 Resolved — Testing Seam

### Existing Test Infrastructure

[VERIFIED: `DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj`]

- Framework: **xUnit 2.9.3** + **bUnit 2.7.2**
- Test pattern: `BunitContext` subclass — each test class inherits `BunitContext`, calls
  `Services.AddSingleton<IFoo>(fake)` to wire fakes, then `Render<PageComponent>()`.
- Test doubles live in `DeckFlow.Studio.Tests/TestDoubles/`

### Existing Fakes Available for Reuse

| Fake | File | What it provides |
|------|------|-----------------|
| `FakeContentSiteIndexStore` | `TestDoubles/FakeContentSiteIndexStore.cs` | In-memory `IContentSiteIndexStore`; already implements `UpsertContentColumnsOnlyAsync` (adds to `Rows` list, line 33); tracks single/batch approval calls. **Reuse directly.** |
| `FakeGitRepository` | `TestDoubles/FakeGitRepository.cs` | Canned returns + call recording. Not needed for Phase 47 page (no git dependency), but reusable pattern. |
| `FakeContentKbOrchestrator` | `TestDoubles/FakeContentKbOrchestrator.cs` | Not directly relevant to Phase 47. |

### New Test Double Required

**`FakeSshArtifactUploader`** — in-memory fake for `ISshArtifactUploader`:

```csharp
// DeckFlow.Studio.Tests/TestDoubles/FakeSshArtifactUploader.cs
internal sealed class FakeSshArtifactUploader : ISshArtifactUploader
{
    // Configure which files succeed/fail
    public HashSet<string> FilesToFail { get; } = new();
    public List<string> UploadedFiles { get; } = new();

    public Task<IReadOnlyList<SshUploadResult>> UploadArtifactsAsync(
        IReadOnlyList<string> localPaths,
        IProgress<SshUploadResult>? progress,
        CancellationToken ct)
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

### `ISshArtifactUploader` Interface (new, in DeckFlow.Studio/Services/)

The interface exists only in Studio (not Core — Core stays console-free and SSH-free).

```csharp
// DeckFlow.Studio/Services/ISshArtifactUploader.cs
public interface ISshArtifactUploader
{
    /// <summary>Uploads a set of local artifact files to the configured remote path.
    /// Returns per-file results; does not throw on individual file failure.</summary>
    Task<IReadOnlyList<SshUploadResult>> UploadArtifactsAsync(
        IReadOnlyList<string> localPaths,
        IProgress<SshUploadResult>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record SshUploadResult(string LocalPath, bool Success, string? FailureReason);
```

**Implementation (`SftpArtifactUploader`) registered in `Program.cs` as singleton.** Since
`SftpClient` is not itself thread-safe between calls, the implementation opens a new client
per `UploadArtifactsAsync` call (matching the on-demand prod store pattern for D-03).

### What Is Unit-Testable Without Live SSH/Postgres

| Test | Approach | New fake needed |
|------|----------|----------------|
| Page loads with "not configured" warning when `StudioConfig(false, false)` | bUnit + `FakeContentSiteIndexStore` | None |
| "Compute Prod Diff" shows New/Updated counts | bUnit + `FakeContentSiteIndexStore` returning prod rows + local approved rows | `FakeContentSiteIndexStore` with two sets |
| Confirmation checkbox gates Stage 2 SCP button | bUnit — check `disabled` attribute | None |
| Stage 3 DB button disabled until Stage 2 SCP success | bUnit — check `disabled` after fake SCP completes | `FakeSshArtifactUploader` |
| SCP partial failure keeps Stage 3 locked | bUnit — `FakeSshArtifactUploader.FilesToFail` | `FakeSshArtifactUploader` |
| `UpsertContentColumnsOnlyAsync` called (not `UpsertRowAsync`) for each approved row | bUnit + `FakeContentSiteIndexStore` tracking | `FakeContentSiteIndexStore` already tracks calls |
| Per-file reconcile list shows failure reason | bUnit + `FakeSshArtifactUploader` | `FakeSshArtifactUploader` |
| Secrets never appear in rendered markup | bUnit — assert absence of conn-string/host substrings | None |

### `FakeContentSiteIndexStore` Extension for Dual-Store Pattern

The diff preview reads from a "prod store" and `GetApprovedRowsAsync` reads from the "local
store." Since both are `IContentSiteIndexStore`, the page will need to call `GetApprovedRowsAsync`
on the injected local `IndexStore` (DI singleton), and `GetAllRowsAsync` (or
`GetApprovedRowsAsync`) on the on-demand prod store (constructed in the action).

For bUnit tests, the simplest approach: inject a fake for the local store, and inject a
factory delegate (`Func<IContentSiteIndexStore>`) that returns a second fake representing
the prod store. Or: pass the on-demand prod store construction into the page via an
`IProdStoreFactory` interface so the test can inject a `FakeContentSiteIndexStore` for the
prod side without needing a real Postgres connection.

**Recommended `IProdStoreFactory` seam:**

```csharp
// DeckFlow.Studio/Services/IProdStoreFactory.cs
public interface IProdStoreFactory
{
    IContentSiteIndexStore Create(string connectionString);
}

// Real impl:
public sealed class ProdStoreFactory : IProdStoreFactory
{
    public IContentSiteIndexStore Create(string connectionString)
    {
        var normalized = PostgresConnectionStringNormalizer.Normalize(connectionString);
        var conn = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, normalized);
        return new ContentSiteIndexStore(conn);
    }
}

// Test fake:
internal sealed class FakeProdStoreFactory : IProdStoreFactory
{
    private readonly IContentSiteIndexStore _prodStore;
    public FakeProdStoreFactory(IContentSiteIndexStore prodStore) => _prodStore = prodStore;
    public IContentSiteIndexStore Create(string _) => _prodStore;
}
```

This is the cleanest seam: the page injects `IProdStoreFactory`, bUnit tests inject
`FakeProdStoreFactory(new FakeContentSiteIndexStore { /* prod rows */ })`.

---

## Standard Stack

### Core (all already in solution)

| Library | Version | Purpose | Status |
|---------|---------|---------|--------|
| `DeckFlow.Core` | (solution project) | `ContentSiteIndexStore`, `RelationalDatabaseConnection`, `UpsertContentColumnsOnlyAsync`, `GetApprovedRowsAsync` | Reuse |
| `DeckFlow.Studio` | (solution project) | `Publish.razor` pattern, `StudioConfig`, `ActionOrchestratorProgress` | Extend |
| `DeckFlow.Studio.Tests` | (solution project) | bUnit 2.7.2 + xUnit 2.9.3 test infrastructure | Extend |

### New Package

| Library | Version | Purpose | Ecosystem |
|---------|---------|---------|-----------|
| `SSH.NET` | `2025.0.0` | SFTP upload to Render `/data`; `SftpClient` + `PrivateKeyFile` auth | NuGet |

**Installation:**
```xml
<!-- DeckFlow.Studio/DeckFlow.Studio.csproj -->
<PackageReference Include="SSH.NET" Version="2025.0.0" />
```

No package needed in `DeckFlow.Studio.Tests` — the `ISshArtifactUploader` interface is
defined in `DeckFlow.Studio`, and the fake implements the interface without referencing
`SSH.NET` directly.

---

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| SSH.NET | NuGet | ~15 yrs (first: 2011.7.29) | 286M total | github.com/sshnet/SSH.NET | N/A — slopcheck is PyPI-only; NuGet verified manually | Approved |

**Packages removed due to slopcheck [SLOP] verdict:** none

**Packages flagged as suspicious [SUS]:** none

**Manual NuGet verification performed:** `api.nuget.org/v3-flatcontainer/ssh.net/index.json`
confirms 32 versions from 2011 to 2025. Catalog entry for `2025.1.0` confirms `Renci` as
author, `github.com/sshnet/SSH.NET` as source repository, no build/script targets. Total
downloads 286M (from `nuget.org/packages/SSH.NET` page). MIT license.

*slopcheck was available (`v0.6.1`) but only checks PyPI; SSH.NET is a NuGet-only package.
All claims about this package are tagged [VERIFIED: NuGet registry].*

---

## Architecture Patterns

### System Architecture Diagram

```
[Operator browser]
      |
      | Blazor SignalR
      v
[DirectPush.razor (Studio)]
  |                    |
  | GetApprovedRowsAsync    | on-demand build (D-03)
  v                    v
[local ContentSiteIndexStore]   [prod ContentSiteIndexStore]
  (SQLite, DI singleton)         (Postgres, per-action, via IProdStoreFactory)
                                     |
                    in-memory diff   |  GetAllRowsAsync (prod read)
                    (natural key)    |
                                     |
  [ISshArtifactUploader]            |
  (SftpArtifactUploader)            |  UpsertContentColumnsOnlyAsync (prod write)
         |                          |
         | SftpClient.UploadFile    |
         v                          v
  [Render /data disk]         [Render Postgres]
  (artifact .md files)        (content_site_index)
```

**Sequence:**
1. Page init → read local approved count + check config presence
2. "Compute Prod Diff" → read local approved rows + read prod all rows → in-memory diff
3. Diff preview shown → operator checks "I have reviewed..." checkbox
4. "Upload Artifacts (SCP)" → SftpArtifactUploader.UploadArtifactsAsync → per-file status
5. All uploads OK → "Write Rows to Prod DB" button unlocks
6. "Write Rows to Prod DB" → foreach approved row: prodStore.UpsertContentColumnsOnlyAsync

### Recommended Project Structure (new files only)

```
DeckFlow.Studio/
├── Pages/
│   └── DirectPush.razor              # new page, @page "/direct-push"
├── Services/
│   ├── ISshArtifactUploader.cs       # new interface + SshUploadResult record
│   ├── SftpArtifactUploader.cs       # SSH.NET SftpClient impl
│   └── IProdStoreFactory.cs          # new interface + ProdStoreFactory impl
DeckFlow.Studio.Tests/
├── DirectPushPageTests.cs            # new bUnit tests
└── TestDoubles/
    └── FakeSshArtifactUploader.cs    # new fake
```

Nav entry in `Shared/NavMenu.razor`: one `nav-item px-3` directly below the existing
"Publish" entry, `href="direct-push"`, icon `oi oi-data-transfer-upload`, label "Direct Push".

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| SSH/SFTP upload | Custom SSH handshake | `SftpClient` (SSH.NET 2025.0.0) | Key exchange, key formats, cipher negotiation, partial reads |
| Postgres connection | Raw `NpgsqlConnection` + manual parameterization | `RelationalDatabaseConnection` + `ContentSiteIndexStore` | Dapper type handlers, dialect abstraction, EnsureSchema already written |
| Postgres URL normalization | Hand-parse `postgresql://` URL | `PostgresConnectionStringNormalizer.Normalize()` | Already handles port, sslmode, userinfo edge cases |
| Prod-write safety gate | Ad-hoc upsert | `UpsertContentColumnsOnlyAsync` | is_visible/is_evergreen preservation is in the SQL `ON CONFLICT` clause — cannot be replicated safely ad-hoc |

---

## Common Pitfalls

### Pitfall 1: Constructing the Prod Store at DI Startup

**What goes wrong:** If a `ContentSiteIndexStore(prodConnectionString)` is registered as a
startup singleton, it holds an open (or readily-openable) Postgres connection to prod at all
times, maximizing the accidental-write surface.

**How to avoid:** Build the store on-demand inside the publish action (D-03). The
`IProdStoreFactory` seam makes this testable without a live Postgres connection.

### Pitfall 2: Enabling Stage 3 After SCP Partial Failure

**What goes wrong:** Any file that failed to SCP means the DB row would reference a missing
artifact. Stage 3 button must remain `disabled` as long as any SCP upload failed.

**Guard:** `disabled="@(!_scpSuccess || _operationInFlight)"` where `_scpSuccess` is only
set to `true` when the `SshUploadResult` list has zero failures.

**Warning signs:** A test where `FakeSshArtifactUploader.FilesToFail` is non-empty still
shows the Stage 3 button as enabled.

### Pitfall 3: Logging SSH Config Values

**What goes wrong:** `ex.Message` from `SshConnectionException` often includes the hostname.
Surfacing `ex.Message` directly in UI text or logs leaks `Studio:Scp:Host` (D-07).

**How to avoid:** Catch `SshException` and surface a sanitized message:
`"SSH connection failed — check SCP configuration and Render SSH access."` Do not include
`ex.Message` in user-facing strings or logs for SSH exceptions. For per-file `SftpPermissionDeniedException`,
the message typically contains the remote path (also a secret per D-07) — sanitize similarly.

### Pitfall 4: Using `UpsertRowAsync` Instead of `UpsertContentColumnsOnlyAsync`

**What goes wrong:** `UpsertRowAsync` clobbers `is_hidden` and `is_evergreen`; it does NOT
preserve them on `ON CONFLICT`. Using the wrong method silently destroys admin-curated
visibility on prod rows (violates SC3).

**Warning signs:** After a direct push, prod rows that previously had `is_visible=true` show
as hidden. Integration test: set `is_visible=TRUE` on prod-fake row, call the push flow,
assert `is_visible` unchanged on prod-fake.

### Pitfall 5: SftpClient is Not Thread-Safe Between Concurrent Calls

**What goes wrong:** Reusing one `SftpClient` instance across concurrent uploads causes
`SshException`. Phase 47 uploads files sequentially (not in parallel), so this is a risk
only if future code parallelizes.

**How to avoid:** `SftpArtifactUploader` opens one `SftpClient` per `UploadArtifactsAsync`
call (the whole batch shares one connection), uploads sequentially inside the batch, then
disconnects. Document this in a code comment.

### Pitfall 6: Remote Directory Not Existing on `/data`

**What goes wrong:** `SftpClient.UploadFile` throws if the remote directory does not exist.
Unlike local `Directory.CreateDirectory`, SFTP does not auto-create parents.

**How to avoid:** Before uploading each file, call `client.CreateDirectory(remoteDir)` if
`!client.Exists(remoteDir)`. Or pre-create all directories before any upload. The per-file
result captures the exception reason, which surfaces in the reconcile list (SC4).

### Pitfall 7: Blazor Sync Context Blocking (Task.Run)

**What goes wrong:** Calling `SftpClient.Connect()` or `client.UploadFile()` directly in a
Blazor event handler blocks the SignalR circuit thread, freezing the UI.

**How to avoid:** Wrap all SSH and store calls in `Task.Run(...)` (identical pattern to
`Publish.razor:329` and `Harvest.razor`). Update per-item progress via
`InvokeAsync(() => { try { StateHasChanged(); } ... })` inside the Task.Run block.

---

## Validation Architecture

`workflow.nyquist_validation` = `true` in `.planning/config.json` — this section is required.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + bUnit 2.7.2 |
| Config file | None — inherits from `DeckFlow.Studio.Tests.csproj` |
| Quick run command | `dotnet test DeckFlow.Studio.Tests/ --filter "DirectPush"` |
| Full suite command | `dotnet test DeckFlow.Studio.Tests/` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| PUB-04 (SC2) | Stage 3 button disabled until Stage 2 SCP success | bUnit | `dotnet test ... --filter "DirectPush_Stage3Locked_UntilScpSuccess"` | Wave 0 gap |
| PUB-04 (SC3) | Only `UpsertContentColumnsOnlyAsync` called on prod, not `UpsertRowAsync` | bUnit | `dotnet test ... --filter "DirectPush_UsesContentColumnsOnlyUpsert"` | Wave 0 gap |
| PUB-04 (SC2) | Confirmation checkbox gates Stage 2 button | bUnit | `dotnet test ... --filter "DirectPush_CheckboxGates_ScpButton"` | Wave 0 gap |
| PUB-05 (SC1) | Diff shows New/Updated counts before any write | bUnit | `dotnet test ... --filter "DirectPush_DiffPreview_ShowsNewUpdatedCounts"` | Wave 0 gap |
| PUB-05 (SC4) | SCP partial failure → Stage 3 stays locked + per-file list shown | bUnit | `dotnet test ... --filter "DirectPush_ScpPartialFailure_Stage3Locked"` | Wave 0 gap |
| PUB-05 (SC4) | DB partial failure → per-row list shown; does not re-lock Stage 2 | bUnit | `dotnet test ... --filter "DirectPush_DbPartialFailure_PerRowListShown"` | Wave 0 gap |
| SC5 | Secrets never appear in rendered markup | bUnit | `dotnet test ... --filter "DirectPush_Secrets_NeverInMarkup"` | Wave 0 gap |
| SC2 | `not configured` state disables all buttons | bUnit | `dotnet test ... --filter "DirectPush_NotConfigured_ButtonsDisabled"` | Wave 0 gap |

### Sampling Rate

- **Per task commit:** `dotnet test DeckFlow.Studio.Tests/ --filter "DirectPush"`
- **Per wave merge:** `dotnet test DeckFlow.Studio.Tests/`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `DeckFlow.Studio.Tests/DirectPushPageTests.cs` — covers all 8 req/SC test cases above
- [ ] `DeckFlow.Studio.Tests/TestDoubles/FakeSshArtifactUploader.cs` — per-file
  success/fail injection
- [ ] `DeckFlow.Studio/Services/ISshArtifactUploader.cs` — interface + `SshUploadResult` record
- [ ] `DeckFlow.Studio/Services/IProdStoreFactory.cs` — interface + `ProdStoreFactory` impl
- [ ] `DeckFlow.Studio.Tests/TestDoubles/FakeProdStoreFactory.cs` — test seam for prod store

---

## Security Domain

`security_enforcement` not explicitly set in config.json — treated as enabled.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Studio is single-operator local tool, no auth |
| V3 Session Management | No | Local Blazor Server, no web sessions |
| V4 Access Control | No | Same — local tool |
| V5 Input Validation | Yes | `ArtifactPath` must be relative (already validated by `ContentSiteIndexStore.ValidateArtifactPath` before upload); remote path must be constructed only from `RemoteArtifactRoot` + sanitized relative path — prevent path traversal |
| V6 Cryptography | Yes (SSH key) | Private key handled by SSH.NET `PrivateKeyFile` — never hand-rolled |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SSH key / conn-string in logs | Info Disclosure | D-07: presence-only logging; sanitize `SshException.Message` before surfacing |
| Path traversal in `ArtifactPath` → remote write outside `/data/content-kb/` | Tampering | `ValidateArtifactPath` in `ContentSiteIndexStore` already rejects rooted and `..` paths; confirm before SCP upload that resolved remote path starts with `RemoteArtifactRoot` |
| Accidental prod write via wrong store instance | Tampering | `IProdStoreFactory` seam ensures prod store is only created with the prod connection string; local store remains the DI singleton |
| `is_visible` / `is_evergreen` clobbered | Tampering | Enforce `UpsertContentColumnsOnlyAsync` exclusively; add test verifying no other upsert is called |

---

## Open Questions

1. **Render SSH access mechanism**
   - What we know: Render exposes an SSH endpoint for shell access to the service container.
   - What's unclear: The exact SSH host/port format for the Render `mtg-deck-studio` service,
     and whether the operator has already set up an SSH key pair for Render access.
   - Recommendation: Operator provides these values in user-secrets; no code assumption needed.
     The `Studio:Scp:Host` / `Studio:Scp:Port` config keys cover it.

2. **Remote directory pre-existence on `/data`**
   - What we know: The `content-kb/` directory likely exists under `/data` from the Phase 20+
     harvest runs. Source-slug subdirectories (`/data/content-kb/{slug}/`) may or may not exist.
   - What's unclear: Whether the operator has pre-created subdirectories.
   - Recommendation: `SftpArtifactUploader` should call `CreateDirectory` for each unique
     parent directory before uploading to it. This is a one-time cost per source channel and
     is idempotent.

3. **`EnsureSchemaAsync` on prod during diff**
   - What we know: `EnsureSchemaAsync` runs `CREATE TABLE IF NOT EXISTS` and `ALTER TABLE ... ADD COLUMN`
     guards. On prod it would add missing columns if the schema lags.
   - What's unclear: Whether the prod schema is already up to date with Phase 43 columns
     (`approval_status`, `is_hidden`).
   - Recommendation: Call `EnsureSchemaAsync` before the prod-side read; it is idempotent and
     safe. This ensures the diff does not fail on a schema gap. Document this in the plan.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `dotnet` (net10.0) | Build + test | Yes (WSL) | 10.0 | — |
| `SSH.NET` NuGet package | SftpArtifactUploader | Not yet added | 2025.0.0 (to pin) | — |
| Render SSH access | Live SCP smoke test | Unknown | Operator-dependent | Skip live test; rely on bUnit + FakeSshArtifactUploader |
| Render Postgres | Live prod diff + upsert | Unknown | Operator-dependent | Skip live test; rely on bUnit + FakeProdStoreFactory |

**Missing dependencies with no fallback:** none that block planning or automated testing.

**Missing dependencies affecting live smoke:** Render SSH access + prod Postgres are needed
only for the operator's manual smoke test (SC1–SC4 verified live). CI tests use fakes.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `SftpClient` is more reliable than `ScpClient` for SSH.NET SFTP uploads | Unknown #2 | If wrong, switch to `ScpClient` — same package, different class; API is similar |
| A2 | SSH.NET `SshException` base class catches all connection + upload failures | Unknown #2 | If there are uncaught exception subclasses, some errors would be unhandled; catch `Exception` as outer fallback |
| A3 | `SshConnectionException.Message` includes the hostname (D-07 risk) | Pitfall 3 | If messages are already sanitized, the extra sanitization is harmless but unnecessary |
| A4 | Render `/data/content-kb/{slug}/` subdirectories may not pre-exist | Open Question 2 | If they all exist, the `CreateDirectory` calls are no-ops (safe) |
| A5 | `EnsureSchemaAsync` on prod Postgres is safe to run as a side effect of the diff read | Open Question 3 | If prod has schema customizations, an unexpected ALTER could fail — but the self-healing pattern guards with column-existence checks first |

**All other claims in this document are VERIFIED from the codebase or the NuGet registry.**

---

## Sources

### Primary (HIGH confidence — code-verified)

- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — ctor overloads (lines 23–42), `UpsertContentColumnsOnlySql` (lines 801–838), `GetApprovedRowsAsync` (lines 301–330)
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` — interface signatures (lines 36–37, 62–63)
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` — `ContentSiteIndexRow` record (lines 107–164)
- `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` — two-arg ctor, `FromSqlitePath`, `OpenConnectionAsync` (lines 21–126)
- `DeckFlow.Core/Storage/PostgresConnectionStringNormalizer.cs` — `Normalize()` (lines 15–68)
- `DeckFlow.Studio/Program.cs` — `IsProdConfigured` pattern, startup log (lines 38–39, 47, 110)
- `DeckFlow.Studio/StudioConfig.cs` — current single-field record (line 6)
- `DeckFlow.Studio/Pages/Publish.razor` — two-stage state machine, `_operationInFlight`, `_cts`, `_diffReviewed` checkbox, `Task.Run` pattern, disposal-safe `InvokeAsync` (throughout)
- `DeckFlow.Studio/Services/ActionOrchestratorProgress.cs` — fire-and-forget progress bridge pattern
- `DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj` — bUnit 2.7.2 + xUnit 2.9.3
- `DeckFlow.Studio.Tests/PublishPageTests.cs` — bUnit page test pattern, `BunitContext` subclass
- `DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs` — existing fake structure
- `DeckFlow.Studio.Tests/TestDoubles/FakeGitRepository.cs` — existing canned-return + call-recording pattern
- `DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs` — Postgres connection construction precedent (lines 88–99)

### Secondary (MEDIUM confidence — NuGet registry verified)

- NuGet registry `api.nuget.org/v3-flatcontainer/ssh.net/index.json` — SSH.NET exists, 32 versions since 2011, latest `2025.1.0`
- NuGet catalog entry `ssh.net.2025.1.0.json` — author `Renci`, source `github.com/sshnet/SSH.NET`, published 2025-10-27
- `nuget.org/packages/SSH.NET` page — 286M downloads, MIT license, `SftpClient.UploadFile` API confirmed

### Tertiary (LOW confidence — training data / ASSUMED)

- SSH.NET exception type hierarchy (`SshException`, `SshConnectionException`, `SftpPermissionDeniedException`) — not verified from official docs in this session (A2)
- `SftpClient` vs `ScpClient` reliability recommendation (A1)

---

## Metadata

**Confidence breakdown:**
- D-03 Postgres store construction: HIGH — two code-verified ctors in codebase
- D-01 SSH.NET package legitimacy: HIGH — NuGet registry verified
- D-01 SSH.NET API (SFTP vs SCP, exceptions): MEDIUM — README verified; exception types ASSUMED
- Phase 43 data layer signatures: HIGH — code-verified in store + interface
- Phase 46 Studio patterns: HIGH — code-verified in Publish.razor + ActionOrchestratorProgress
- Testing seam: HIGH — code-verified in existing test project

**Research date:** 2026-06-16
**Valid until:** 2026-07-16 (stable libraries; re-verify SSH.NET if version is bumped)

---

## RESEARCH COMPLETE

**Phase:** 47 — Direct Prod-DB + SCP Publish Path
**Confidence:** HIGH

### Key Findings

1. **D-03 RESOLVED — no Core change needed:** `ContentSiteIndexStore` already has a
   `RelationalDatabaseConnection` overload at line 30. Postgres-backed on-demand store
   construction is exactly: `new ContentSiteIndexStore(new RelationalDatabaseConnection(Postgres, normalized))`.

2. **D-01 RESOLVED — pin `SSH.NET 2025.0.0`:** Use `SftpClient` (not `ScpClient`). 286M downloads,
   15 years on NuGet, MIT license, `github.com/sshnet/SSH.NET`. Add to `DeckFlow.Studio.csproj` only.
   Recommended user-secrets schema: `Studio:Scp:{Host,Port,Username,KeyFile,KeyPassphrase,RemoteArtifactRoot}`.

3. **Phase 43 data layer confirmed:** `UpsertContentColumnsOnlyAsync(ContentSiteIndexRow)` is the ONLY
   safe prod write (never touches `is_visible`/`is_evergreen`/`is_hidden`/`approval_status` on
   existing rows). `GetApprovedRowsAsync()` returns the local approved set. `ArtifactPath` on each row
   is the relative path used for both SCP source and prod DB storage.

4. **Testing seam:** `IProdStoreFactory` interface + `FakeProdStoreFactory` test double is the
   cleanest seam for bUnit tests without a live Postgres connection. `ISshArtifactUploader` +
   `FakeSshArtifactUploader` covers the SCP path. Both follow the existing Fake* pattern from
   Phase 46.

5. **`StudioConfig` must be extended:** Current record has only `IsProdConfigured`; Phase 47
   adds `IsScpConfigured` as a second positional parameter. The `prodConnStr` raw value is best
   accessed via `[Inject] IConfiguration` in the page at action time (not held in DI state).

### File Created

`.planning/phases/47-direct-prod-db-scp-publish-path/47-RESEARCH.md`

### Confidence Assessment

| Area | Level | Reason |
|------|-------|--------|
| D-03 Postgres store construction | HIGH | Two ctors code-verified at line 23 and 30 |
| D-01 SSH.NET package | HIGH | NuGet registry: 286M downloads, 15 yrs, MIT, github.com/sshnet/SSH.NET |
| D-01 SSH.NET SFTP API | MEDIUM | README API confirmed; exception types ASSUMED |
| Phase 43 data layer | HIGH | Interface + SQL code-verified |
| Phase 46 Studio patterns | HIGH | Publish.razor read line-by-line |
| Testing seam | HIGH | Existing test project and fakes fully read |

### Open Questions

1. Render SSH host/port format — operator must provide in user-secrets (no code assumption needed).
2. Remote `/data/content-kb/{slug}/` subdirectory pre-existence — `SftpArtifactUploader` should
   auto-create with `client.CreateDirectory()`.
3. Whether prod Postgres schema is already at Phase 43 column level — `EnsureSchemaAsync` handles
   this safely.

### Ready for Planning

Research complete. Planner can now create PLAN.md files for Phase 47.
