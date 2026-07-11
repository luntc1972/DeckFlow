# Phase 90: DirectPush Correctness + Seed Sync - Pattern Map

**Mapped:** 2026-07-07
**Files analyzed:** 9 (2 self-contained Plan A / Web, 4 Studio orchestration, 2 shared/read, 1 test)
**Analogs found:** 9 / 9

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs` (modify) | service | request-response (file resolution) | itself, prior version (`TryResolveExistingArtifact`, lines 92-131) | exact — same file, add flag branch |
| `DeckFlow.Web/Controllers/ContentKbController.cs` (modify, inject flag cache into resolver call site or resolver ctor) | controller | request-response | itself, prior version (`Detail`, lines 91-143) + `ScryfallTaggerLookupService.cs:96-100` (inline flag check pattern) | exact / role-match |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (modify — add one entry) | config | CRUD (static dictionary) | itself, prior entries e.g. `analysis.mulligan-eval` (lines 92-96) | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (modify — add seed row x2 dialects) | config/store | CRUD (seed INSERT) | itself, `analysis.mulligan-eval` seed rows (Postgres line 232, SQLite line 271) | exact |
| `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` (modify — re-plumb `WritePublishAsync` into content-only write + new confirm/stamp stage; add seed export) | service (orchestrator) | CRUD + event-driven (multi-stage workflow) | `PublishCoordinator.cs` (`ExportAndDiffAsync` lines 85-193, `CommitAsync` lines 203-231) — the target end-state to converge onto | exact — sibling coordinator, same shape |
| `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` — NEW seed-export call in `CommitAndPushBodiesAsync` | service | file-I/O (seed write + stage) | `PublishCoordinator.cs:91-125` (`SeedRelative` const + `ExportIndexToFileAsync` + staged-paths list) | exact |
| `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` — NEW confirm step (SYNC-09/D-09) | service | request-response (outbound HTTPS GET) | `DeckFlow.Studio/Program.cs:121-124` (singleton `HttpClient` + `ResilientHttpHandler`) + `ContentKbController.cs:118-134` (hash comparison to replicate) | role-match (new capability, existing HttpClient plumbing to extend) |
| `DeckFlow.Studio/Services/IProdContentReader.cs` + `ProdContentReader.cs` (new sibling method, e.g. `ReadFlagAsync`) | service (read-only prod accessor) | request-response (single SELECT) | `ProdContentReader.cs` (whole file, esp. lines 16-62) | exact |
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` (modify — add nullable "awaiting-confirm" marker column, D-10) | model / migration | CRUD (dialect-guarded ALTER + column) | itself, `body_sha256`/`approval_status` column precedent (schema DDL ~lines 1120-1163; idempotent ALTER ~lines 115-120, 132-137) | exact |
| `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs` (modify — extend) | test | unit (fakes) | itself, existing `Build(...)` seam (lines 58-72) + `FakeContentKbOrchestrator` | exact |
| Pre-flip git-coverage audit (new — Studio startup check or admin/report, D-11) | utility (reporting/scan) | batch (read-only cross-reference) | No direct analog found in codebase (see "No Analog Found") | none — design fresh, small |

## Pattern Assignments

### `DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs` (service, request-response) — SYNC-07 flag-gated serving flip

**Analog:** itself (current file) — the resolver already does git-first, overlay-fallback; the change is dropping the fallback branch under a flag, not a new pattern.

**Current fallback to gate** (`ContentKbArtifactPathResolver.cs:92-131`):
```csharp
public ContentKbArtifactResolution TryResolveExistingArtifact(string artifactPath, out string resolvedFullPath)
{
    resolvedFullPath = string.Empty;
    if (!IsSafeArtifactPath(artifactPath))
    {
        return ContentKbArtifactResolution.InvalidPath;
    }

    var gitRoot = Path.Combine(ContentBase, "content-kb");
    var gitPath = Path.GetFullPath(Path.Combine(ContentBase, artifactPath));
    if (!IsContainedUnderRoot(gitPath, gitRoot))
    {
        return ContentKbArtifactResolution.InvalidPath;
    }

    if (File.Exists(gitPath))
    {
        resolvedFullPath = gitPath;
        return ContentKbArtifactResolution.Resolved;
    }

    if (DataOverlayBase is null)
    {
        return ContentKbArtifactResolution.MissingFile;
    }

    var overlayPath = Path.GetFullPath(Path.Combine(DataOverlayBase, artifactPath["content-kb/".Length..]));
    if (!IsContainedUnderRoot(overlayPath, DataOverlayBase))
    {
        return ContentKbArtifactResolution.InvalidPath;
    }

    if (File.Exists(overlayPath))
    {
        resolvedFullPath = overlayPath;
        return ContentKbArtifactResolution.Resolved;
    }

    return ContentKbArtifactResolution.MissingFile;
}
```
**Target shape:** inject `IFeatureFlagCache` (constructor param, mirrors `ScryfallTaggerLookupService`'s ctor below) and short-circuit to `MissingFile` immediately after the `File.Exists(gitPath)` check fails, when the flag is ON — skip the `DataOverlayBase` block entirely rather than deleting it (flag OFF must preserve exact current behavior).

**Path-safety guard to reuse verbatim** (`ContentKbArtifactPathResolver.cs:175-193`, `IsSafeArtifactPath`) — do not write a second path-validation routine; any new confirm/audit code that touches artifact paths must call this same static helper or the resolver's public methods.

---

### `DeckFlow.Web/Controllers/ContentKbController.cs` (controller, request-response) — inline flag read pattern

**Analog:** `DeckFlow.Web/Services/Scryfall/ScryfallTaggerLookupService.cs:51,73,96-100` — the house pattern for an inline (non-attribute) flag check, because `FeatureFlagGateAttribute` is all-or-nothing 404 (confirmed in RESEARCH — not usable here since SYNC-07 changes *how* a resolved action serves the body, not *whether* the route exists):
```csharp
private readonly IFeatureFlagCache _flagCache;
// ctor: IFeatureFlagCache flagCache, ArgumentNullException.ThrowIfNull(flagCache); _flagCache = flagCache;

// FLAG-04-style kill-switch gate — read inline, not via [FeatureFlagGate]:
if (!_flagCache.IsEnabled("service.scryfall-tagger.enabled"))
{
    return Array.Empty<string>();
}
```
Apply the same shape: read `_flagCache.IsEnabled("sync.directpush-gitbody")` either in the resolver (preferred, keeps the branch co-located with the git/overlay logic) or in `ContentKbController.Detail` immediately before/inside the `TryResolveExistingArtifact` call (`ContentKbController.cs:106`).

**Existing render-guard to reuse verbatim, unchanged this phase** (`ContentKbController.cs:118-134`):
```csharp
var raw = await System.IO.File.ReadAllTextAsync(resolved, cancellationToken).ConfigureAwait(false);
var (_, body) = ContentArtifactParser.SplitHeader(raw);

var computedHash = ContentSiteIndexContentSignature.ComputeBodySha256(raw);
if (row.BodySha256 is null || !string.Equals(row.BodySha256, computedHash, StringComparison.Ordinal))
{
    _logger.LogWarning(
        "Content KB body hash mismatch for row {ContentKbRowId}: stored={StoredHash} computed={ComputedHash}",
        row.Id, row.BodySha256 ?? "(none)", computedHash);
}
```
This is the exact comparison D-06/D-09 says to reuse for the `/app` hash-verify — Studio's confirm step should call `ComputeBodySha256` against whatever body text it fetches, using this same helper (`ContentSiteIndexContentSignature.ComputeBodySha256`, `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs:131-145`). Do not hand-roll a second hash routine.

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (config) — register `sync.directpush-gitbody`

**Analog:** existing dictionary entries, e.g. (`FeatureFlagCatalog.cs:92-96`):
```csharp
["analysis.mulligan-eval"] =
    "Show the opening-hand / mulligan evaluator block on the mana base page and its paste " +
    "artifact - a keepable-hand band, London mulligan keep-depth process, and representative " +
    "openers with a per-play on-curve and has-a-plan read, all a heuristic consistency signal " +
    "derived from the existing simulation. Off = byte-identical output.",
```
Add `["sync.directpush-gitbody"] = "..."` following the same one-line-description convention. `FeatureFlagCatalogTests` fails the build if a seeded key has no description — this entry is mandatory, not optional.

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (config/store) — seed OFF (D-05)

**Analog:** the most recent precedent, `analysis.mulligan-eval` seeded FALSE in both dialects (`FeatureFlagStore.cs:232` Postgres, `FeatureFlagStore.cs:271` SQLite):
```csharp
// PostgresSeedSql
('analysis.wincon-map', FALSE),
('analysis.mulligan-eval', FALSE),
('tool.primer.stale-flag', FALSE)
ON CONFLICT (key) DO NOTHING;

// SqliteSeedSql
('analysis.wincon-map', 0),
('analysis.mulligan-eval', 0),
('tool.primer.stale-flag', 0)
ON CONFLICT (key) DO NOTHING;
```
Add `('sync.directpush-gitbody', FALSE)` / `(..., 0)` to both blocks, in the same relative position (append before the closing row). **Landmine (RESEARCH-confirmed):** `IFeatureFlagCache.IsEnabled` defaults missing keys to `true` (`FeatureFlagCache.cs:46-56`, D-13) — omitting this seed row would silently activate the flip.

---

### `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` (service/orchestrator) — SYNC-08 seed re-export

**Analog:** `PublishCoordinator.cs:91-101, 122-125` (verbatim pattern to replicate, cited directly by D-08):
```csharp
// PublishCoordinator.cs — the pattern to copy
private const string SeedRelative = "content-kb/seed/index-seed.json";
...
var seedAbsPath = Path.GetFullPath(Path.Combine(repoRoot, SeedRelative));
var exportResult = await _orchestrator.ExportIndexToFileAsync(seedAbsPath, progress, cancellationToken).ConfigureAwait(false);
if (!exportResult.Success)
{
    return PublishExportResult.SeedExportFailure(exportResult.Message ?? string.Empty);
}
...
var staged = new List<string> { SeedRelative };
staged.AddRange(copiedArtifactPaths);
```
`DirectPushCoordinator` already holds `_orchestrator` (`IContentKbOrchestrator`, field at `DirectPushCoordinator.cs:30`) — no new dependency. Add the same `SeedRelative` constant (identical literal, `"content-kb/seed/index-seed.json"`) and call `_orchestrator.ExportIndexToFileAsync(...)` inside `CommitAndPushBodiesAsync` (`DirectPushCoordinator.cs:252-382`), adding `SeedRelative` to the staged-paths list alongside `copied` (currently built at `DirectPushCoordinator.cs:328-330`).

**The guard being replaced** (D-08, `DirectPushCoordinator.cs:240-251` doc comment + the fact that `CommitAndPushBodiesAsync` never references `SeedRelative`/`ExportIndexToFileAsync` today):
```csharp
/// Resolves the anti-pattern: this commits body files ONLY — it never invokes the approved-only
/// seed export, so the committed index-seed.json (the full published set in git) is left
/// untouched. A partial Studio store can therefore never overwrite the seed here.
```
This comment/behavior is the exact thing SYNC-08 deletes; replace with the export call above.

---

### `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` — SYNC-09/SYNC-10 ordering re-plumb

**Analog:** the existing method to split, `WritePublishAsync` (`DirectPushCoordinator.cs:213-233`):
```csharp
public async Task WritePublishAsync(
    IReadOnlyList<ContentSiteIndexRow> publishRows,
    CancellationToken cancellationToken)
{
    var prodStore = CreateProdStore();
    await prodStore.UpsertContentColumnsOnlyBatchAsync(publishRows, cancellationToken).ConfigureAwait(false);

    var keys = publishRows
        .Select(row => ContentIndexExportRow.From(row))
        .Select(row => (Type: row.NaturalKeyType, Value: row.NaturalKeyValue))
        .ToList();
    var pushedUtc = DateTimeOffset.UtcNow;

    await prodStore.StampPushedToProdAsync(keys, pushedUtc, cancellationToken).ConfigureAwait(false);
    await prodStore.SetVisibilityAsync(keys, true, cancellationToken).ConfigureAwait(false);
    await _localStore.StampPushedToProdAsync(keys, pushedUtc, cancellationToken).ConfigureAwait(false);
    await _localStore.SetVisibilityAsync(keys, true, cancellationToken).ConfigureAwait(false);
}
```
**Target:** split into (a) a content-only `WriteContentAsync` that keeps only the `UpsertContentColumnsOnlyBatchAsync` call, and (b) a new `ConfirmAndPublishAsync`/`StampAndPublishAsync`-style method that runs the four `StampPushedToProdAsync`/`SetVisibilityAsync` calls **after** the confirm step succeeds — preserving the exact prod-first-then-local ordering and the `ContentIndexExportRow.From()` key-derivation shown above (Pitfall 5 in RESEARCH explicitly calls out this ordering invariant must survive the split, and is covered by `DirectPushCoordinatorTests`).

**Reuse `ContentIndexExportRow.From()` for key derivation** — do not hand-roll natural-key extraction a second time; this call site is already the correct pattern (`DirectPushCoordinator.cs:224-226`).

**`[skip render]` interaction (Pitfall 2 / Open Question 1 — planner must decide, not default silently):**
```csharp
// DirectPushCoordinator.cs:33-37
private const string RenderSkipPhrase = "[skip render]";
```
Referenced at `DirectPushCoordinator.cs:342` inside `CommitAndPushBodiesAsync`. D-09/CONTEXT.md directs: DirectPush must NOT `[skip render]` for the git-body flow — the redeploy must actually happen. The planner should gate this on the same `sync.directpush-gitbody` flag (drop the phrase when flag ON) rather than removing the constant outright, since flag-OFF DirectPush behavior must stay byte-identical to today.

---

### `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` — SYNC-09 deploy-confirm (D-09, novel — but Studio HttpClient plumbing already exists)

> ⚠ **SUPERSEDED DETAIL — read this first.** D-09 was REVISED after Codex plan-review. The confirm
> is NO LONGER a public-detail-page HTTPS GET treating "HTTP 200 = reachable." It is now a poll
> against a dedicated AUTHENTICATED endpoint `GET /Admin/api/contentkb/deployed-body-hash` (git
> `/app` only, not `is_visible`-gated, 404 on missing, returns `{bodySha256}`), confirming ONLY on
> `200 && bodySha256 == expected`, keyed by natural key. See 90-CONTEXT D-09 REVISED + 90-07-PLAN +
> 90-05-PLAN. The **HttpClient / ResilientHttpHandler transport wiring** described below IS still
> the right pattern to reuse; DISREGARD the "200 suffices / public detail URL / no hash needed"
> guidance in the paragraph below — the confirmer DOES compare the returned hash.

**Analog for the HttpClient wiring:** `DeckFlow.Studio/Program.cs:121-124`:
```csharp
// Why (M1): wrap the shared HttpClient in ResilientHttpHandler so long YouTube-list /
// transcript fetches survive a transient blip instead of dying outright.
builder.Services.AddSingleton(_ => new HttpClient(new ResilientHttpHandler()) { Timeout = TimeSpan.FromMinutes(15) });
```
`ResilientHttpHandler` (`DeckFlow.Studio/Services/ResilientHttpHandler.cs:18-120`) already gives Studio Polly-v8 retry-with-backoff on idempotent GET/HEAD (matching the house RestSharp+Polly convention CLAUDE.md requires, even though this call is plain `HttpClient` not RestSharp — Studio's existing convention for outbound HTTP is `HttpClient` + `ResilientHttpHandler`, not RestSharp; **follow the Studio-local convention here, not the Web-project RestSharp convention**, since this is the established pattern for this project's only existing outbound-HTTP consumers). Inject the same singleton `HttpClient` into `DirectPushCoordinator`'s constructor (mirrors how `YouTubeChannelVideoLister`/transcript providers consume `sp.GetRequiredService<HttpClient>()` at `Program.cs:131-140`) and issue `GetAsync(detailUrl, cancellationToken)`; treat `HttpStatusCode.OK` as "reachable" per D-09 (200 = body reachable at `/app`; corruption detection is delegated to the Phase 89 render guard already logging server-side — Studio does NOT need to fetch/parse the HTML to extract a hash, D-09 only requires the 200).

**No existing precedent for Studio calling the deployed web app itself** — this specific target URL (the public Content-KB detail page) is new; only the *transport pattern* (singleton `HttpClient` + `ResilientHttpHandler`) is reused.

---

### `DeckFlow.Studio/Services/IProdContentReader.cs` + `ProdContentReader.cs` (service) — D-04 read-only prod-flag accessor

**Analog:** the whole file, this is the exact pattern to replicate for a flag read (`ProdContentReader.cs:16-62`):
```csharp
public sealed class ProdContentReader : IProdContentReader
{
    private const string SelectAllSql = """
        SELECT id, source, title, ... FROM content_site_index;
        """;

    public async Task<IReadOnlyList<ContentSiteIndexRow>> ReadAllAsync(
        string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var normalized = PostgresConnectionStringNormalizer.Normalize(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(normalized) { SslMode = SslMode.Require };
        var conn = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, builder.ConnectionString);
        await using var connection = await conn.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ContentSiteIndexRowData>(
            new CommandDefinition(SelectAllSql, cancellationToken: cancellationToken));
        return rows.Select(ToContentSiteIndexRow).ToList();
    }
}
```
**Interface contract to mirror** (`IProdContentReader.cs:1-20`):
```csharp
/// This contract is deliberately read-only: it exposes a single SELECT method and no upsert,
/// delete, set, or schema-ensure operation, so the production side is structurally incapable of
/// being written through it (R1/R2).
public interface IProdContentReader
{
    Task<IReadOnlyList<ContentSiteIndexRow>> ReadAllAsync(string connectionString, CancellationToken cancellationToken = default);
}
```
**Target:** add a sibling method (e.g. `ReadFlagAsync(string connectionString, string key, CancellationToken ct)`) on the SAME interface/class — NOT a new class — running:
```sql
SELECT enabled FROM feature_flags WHERE key = @key;
```
against the identical `NpgsqlConnectionStringBuilder`/`SslMode.Require`/no-DDL construction shown above. Confirmed by `DeckFlowDatabaseConnectionFactory.cs:36-37,70-71`: `CreateFeatureFlagConnection` and `CreateContentSiteIndexConnection` both route through the same `CreateConnection(environment, sqliteFileName)` helper keyed off the same `DECKFLOW_DATABASE_PROVIDER`/`DECKFLOW_DATABASE_CONNECTION_STRING` env vars — in production both tables live in the one physical Postgres database Studio already reaches via `Studio:ProdConnectionString` (confirms RESEARCH Assumption A1). Default to `false`/OFF on missing row or connection failure — fail-closed, the inverse of the web-side `IFeatureFlagCache` D-13 default-on (per D-04/Pattern 3 in RESEARCH).

---

### `DeckFlow.Core/Content/ContentSiteIndexStore.cs` (model/migration) — D-10 durable "awaiting-confirm" marker

**Analog:** the `body_sha256` / `approval_status` column-addition precedent — dialect-guarded idempotent ALTER inside `EnsureSchemaAsync`, plus the CREATE TABLE definitions for both dialects:

**Idempotent ALTER pattern** (`ContentSiteIndexStore.cs:115-120, 132-137`):
```csharp
if (!columns.Contains("approval_status"))
{
    await using var addApprovalStatus = connection.CreateCommand();
    addApprovalStatus.CommandText =
        "ALTER TABLE content_site_index ADD COLUMN approval_status TEXT NOT NULL DEFAULT 'pending';";
    await addApprovalStatus.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
...
if (!columns.Contains("body_sha256"))
{
    await using var addBodySha256 = connection.CreateCommand();
    addBodySha256.CommandText = "ALTER TABLE content_site_index ADD COLUMN body_sha256 TEXT NULL;";
    await addBodySha256.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```
**CREATE TABLE column precedent** (both Postgres and SQLite blocks, ~lines 1120-1163):
```sql
-- Postgres
body_sha256        TEXT NULL,
-- SQLite
body_sha256        TEXT NULL,
```
**Target:** add a new nullable column (e.g. `awaiting_confirm_utc TIMESTAMPTZ NULL` / SQLite `TEXT NULL`, or a simpler boolean `TEXT NULL` status flag) following this exact two-part pattern (CREATE-TABLE column + idempotent ALTER guard), and add it to `ContentSiteIndexRowData`/`ContentSiteIndexRow` the same way `BodySha256` was threaded through (`ContentSiteIndexStore.cs:971, 1185`). **Do not filter any new "pending confirm" query on a timestamp column** (Pitfall 3 — F-51-PG-01 class); if a query is needed, key it on `natural_key_type`/`natural_key_value` like `StampPushedToProdAsync`/`SetVisibilityAsync` already do (`ContentSiteIndexStore.cs:649-654, 767-773` — no `WHERE` on any timestamp column in either), or dialect-guard any timestamp comparison exactly like the F-51-PG-01 `::timestamptz` fix precedent.

**Per D-09/Open-Question-3 recommendation:** the simplest correct design keeps the whole expand→verify→contract sequence in one Studio operator session against the in-memory `_publishRows`/exported-keys list already held from Stage 3 (no page navigation in between) — this may make the new column unnecessary if the planner adopts that simplification; CONTEXT.md D-10 still requires *some* durable marker, so if the planner keeps cross-session resumability in scope, this is the column pattern to use.

---

### `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs` (test) — extend existing seam

**Analog:** itself, the existing `Build(...)` helper and fakes (`DirectPushCoordinatorTests.cs:58-72`):
```csharp
private static DirectPushCoordinator Build(
    FakeContentSiteIndexStore local,
    FakeContentSiteIndexStore prod,
    FakeSshArtifactUploader? uploader = null,
    string artifactRoot = "/data/content-kb",
    FakeGitRepository? git = null,
    FakeContentKbOrchestrator? orchestrator = null)
    => new(
        local,
        uploader ?? new FakeSshArtifactUploader(),
        new FakeProdStoreFactory(prod),
        new ConfigurationBuilder().Build(),
        new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot },
        git ?? new FakeGitRepository(),
        orchestrator ?? new FakeContentKbOrchestrator());
```
`FakeContentKbOrchestrator` already exists and is shared with `PublishCoordinatorTests.cs` — reuse it to assert the new `CommitAndPushBodiesAsync` calls `ExportIndexToFileAsync` with the expected seed path (SYNC-08), and to assert the new ordering: no `StampPushedToProdAsync`/`SetVisibilityAsync` call happens until a simulated "confirmed" signal (SYNC-09/SYNC-10). The confirm-step fake/seam is new (no existing precedent) — per RESEARCH, its *ordering* is testable today with a stub that always returns "confirmed", independent of the real HTTP mechanism.

---

## Shared Patterns

### Body-hash comparison (D-06)
**Source:** `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs:131-145` (`ComputeBodySha256`)
**Apply to:** `ContentKbController.cs` (already applied, Phase 89); the NEW `deployed-body-hash` endpoint (Plan 90-07) recomputes with this same `ComputeBodySha256` helper server-side and returns `{bodySha256}`; the Studio confirmer (Plan 90-05) compares that returned hash against the expected stored hash (D-09 REVISED — the confirm is a hash match, NOT "200 alone"). Reuse the single existing helper on both sides; do not build a second hash path.
```csharp
public static string ComputeBodySha256(string rawArtifactText)
{
    var (_, body) = ContentArtifactParser.SplitHeader(rawArtifactText);
    var normalizedBody = body.Replace("\r\n", "\n").Replace("\r", "\n");
    var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedBody));
    return Convert.ToHexStringLower(hashBytes);
}
```

### Inline feature-flag check (not the attribute)
**Source:** `DeckFlow.Web/Services/Scryfall/ScryfallTaggerLookupService.cs:51,73,96-100`
**Apply to:** `ContentKbArtifactPathResolver` / `ContentKbController` (SYNC-07 serving flip)
```csharp
private readonly IFeatureFlagCache _flagCache;
// ctor: ArgumentNullException.ThrowIfNull(flagCache); _flagCache = flagCache;
if (!_flagCache.IsEnabled("service.scryfall-tagger.enabled"))
{
    return Array.Empty<string>();
}
```

### Shared seed-export factory (D-08 — do not fork a second writer)
**Source:** `DeckFlow.Studio/ViewModels/PublishCoordinator.cs:26,94-101,122-125` calling `IContentKbOrchestrator.ExportIndexToFileAsync` → `ContentIndexExportRow.From()` (`DeckFlow.Core/Orchestration/ContentIndexExportRow.cs`, Phase 89)
**Apply to:** `DirectPushCoordinator.CommitAndPushBodiesAsync`

### Prod-first-then-local write ordering (PUB-01/HIGH-3, must survive the Stage-3/Stage-5 split)
**Source:** `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs:204-211` (doc comment) and `213-233` (`WritePublishAsync` body)
**Apply to:** both the new content-only write method and the new confirm/stamp method

### Read-only prod accessor (structurally incapable of writing)
**Source:** `DeckFlow.Studio/Services/ProdContentReader.cs` (whole file) + `IProdContentReader.cs`
**Apply to:** the new D-04 flag-read sibling method; also the model for any read-only pre-flip git-coverage audit (D-11) that needs to read prod rows without risk of a write path.

### Dialect-guarded idempotent DDL (nullable-column addition)
**Source:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs:115-120` (approval_status) and the `body_sha256` equivalent (~132-137), plus both CREATE TABLE blocks (~1120-1163)
**Apply to:** the D-10 awaiting-confirm marker column, if the planner keeps a persisted column in scope.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| Pre-flip git-coverage audit (D-11 — lists approved+visible rows whose body is missing from the git `/app` tree) | utility (reporting/scan) | batch (read-only cross-reference of prod DB rows vs. filesystem) | No existing "cross-reference DB rows against filesystem existence" utility exists in either `DeckFlow.Web` or `DeckFlow.Studio`. Closest *partial* structural analogs: `ContentKbArtifactPathResolver.TryResolveExistingArtifact` (the `File.Exists` check to reuse per-row) and `ProdContentReader.ReadAllAsync` (the prod row source) — the planner should compose these two rather than invent new file-existence logic, but there is no single existing "audit/report" class to point to as the shape. Use `RESEARCH.md`'s System Architecture Diagram guidance instead (Pitfall 1: "run a check cross-referencing `is_visible=true` prod rows against `File.Exists` in the git tree"). |
| Studio confirm-poll / resumable "verify deploy" mechanism itself (the actual HTTP round-trip logic, as opposed to the HttpClient transport wiring) | service (novel, event-driven) | request-response (poll-until-200 or single-shot GET) | RESEARCH explicitly flags this as genuinely novel (Open Questions 1-3, confidence LOW) — no codebase precedent for Studio calling the deployed web app. The *transport* (`HttpClient` + `ResilientHttpHandler`) is reused (see Pattern Assignments above); the *poll/resume logic* itself has no analog and must be designed fresh in planning. |

## Metadata

**Analog search scope:** `DeckFlow.Studio/ViewModels/`, `DeckFlow.Studio/Services/`, `DeckFlow.Studio/Program.cs`, `DeckFlow.Studio.Tests/ViewModels/`, `DeckFlow.Web/Controllers/`, `DeckFlow.Web/Services/Content/`, `DeckFlow.Web/Services/FeatureFlags/`, `DeckFlow.Web/Services/Persistence/`, `DeckFlow.Core/Content/`, `DeckFlow.Core/Orchestration/`
**Files scanned (read in full or targeted-range):** `PublishCoordinator.cs`, `DirectPushCoordinator.cs`, `DirectPushCoordinatorTests.cs` (partial), `ContentKbController.cs` (partial), `ContentKbArtifactPathResolver.cs`, `FeatureFlagCatalog.cs`, `FeatureFlagStore.cs` (partial), `ProdContentReader.cs`, `IProdContentReader.cs`, `ContentSiteIndexContentSignature.cs` (partial), `ContentSiteIndexStore.cs` (partial), `DeckFlowDatabaseConnectionFactory.cs` (partial), `ResilientHttpHandler.cs`, `Program.cs` (Studio, partial), `ScryfallTaggerLookupService.cs` (partial)
**Pattern extraction date:** 2026-07-07
