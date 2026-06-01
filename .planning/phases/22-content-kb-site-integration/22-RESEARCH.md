# Phase 22: Content KB Site Integration - Research

**Researched:** 2026-06-01
**Domain:** ASP.NET 10 + Razor MVC — slim-index materialization on Render Postgres, public browse/filter UI, per-entry admin curation, artifact serving, commit-then-deploy seed load
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01a (public browse):** Public site page (route `/content-kb`) using the responsive site shell (`site-common.css` + WDG-08 primitives — NOT `.admin-shell`). Gated by `content_kb_enabled` via `FeatureFlagGateAttribute` (default OFF). Primary deliverable.
- **D-01b (admin manage — per-entry curation):** Surface under `.admin-shell` (BasicAuth) with a per-entry grid (title/source/tags/visible-state + publish/unpublish toggle per entry), per-source bulk publish/hide, the global `content_kb_enabled` flag toggle (reuse AdminFlagsController pattern), index status (row count, distinct sources, last-loaded timestamp), and "reload index from committed seed" action.
- **D-02a (artifacts in repo):** Distilled artifacts are committed to the repo and served as content. Current `content-kb/` and `artifacts/` are gitignored. Planner decides published location and static-vs-controller serving.
- **D-02b (index seed):** Slim-index rows ship as a committed seed/import file (local CLI exports `content_site_index` rows to a tracked JSON/SQL file; may need a new `content-index-export` CLI verb). On Render startup: `EnsureSchemaAsync` then idempotent load (upsert by natural key). CRITICAL: the upsert MUST NOT clobber `is_visible` on rows the admin already curated on Render — default hidden for NEW rows only; UPDATE refreshes content fields (title/tags/artifact_path) but LEAVES `is_visible` untouched.
- **D-02c:** No upload endpoint. Admin "reload index from seed" action carries `[ValidateAntiForgeryToken]` + `SameOriginRequestValidator`.
- **D-03:** Per-entry detail page (shareable URL) rendering the artifact summary + timestamped clips + tags via Markdig, with a "copy for ChatGPT" button reusing the existing `attachDynamicCopyButton` TS/UX. Must render at 375px.
- **D-04:** Public browse renders ONLY published rows (server query filters `WHERE is_visible = true`). Client-side faceted filter by source / archetype / bracket / card_category + text search. Empty-state CTA for zero-content first run AND "flag on but nothing published yet".
- **D-05:** Add `is_visible` column (admin publish state) to `content_site_index` — default hidden (`0`/`false`). Additive migration: `EnsureSchemaAsync` adds guarded `ALTER TABLE ... ADD COLUMN is_visible ...` for BOTH dialects (Postgres `BOOLEAN`/SQLite `INTEGER`). Store gains: published-only query, all-rows query, per-entry and per-source `SetVisibility` upsert.

### Claude's Discretion

- Exact public route name (`/content-kb` vs `/knowledge` vs `/decks/insights`)
- Seed file format (JSON vs SQL) + whether a new CLI export verb or reuse of an existing one
- Published artifact location + static-vs-controller serving
- Whether the admin manage view is a new tab or folded into existing flags/maintenance admin page

### Deferred Ideas (OUT OF SCOPE)

- Admin upload-to-/data artifact path (rejected — commit-then-deploy only)
- Server-query filter + pagination (when index outgrows client-side filtering)
- Full admin CRUD over the index/sources
- Deck-analysis integration of Content KB tags (v1.5)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| KB-08 | Slim index on Render Postgres + browse/filter display | EnsureSchemaAsync + is_visible-preserving seed upsert patterns documented below; public browse controller + client-side filter spec in UI-SPEC |
| KB-09 | `content_kb_enabled` flag (default OFF) + per-entry/per-source admin curation + CSRF on every mutating POST | Flag seeding in FeatureFlagStore, FeatureFlagGateAttribute gate, SameOriginRequestValidator + ValidateAntiForgeryToken patterns all verified in codebase |
</phase_requirements>

---

## Summary

Phase 22 wires the locally-produced Content KB artifacts and slim index rows into the live site. All the seams exist — `ContentSiteIndexStore`, `FeatureFlagGateAttribute`, `HelpContentService` (Markdig), `AdminFlagsController`, `SameOriginRequestValidator` — and need extension, not replacement.

The five critical research questions all have verified answers grounded in the actual codebase:

1. **Startup seed-load hook:** Program.cs already calls `EnsureSchemaAsync` + startup work in a sequential block at lines 423-452. The content site-index store goes in the same block, gated by provider. The is_visible-preserving upsert SQL shape is documented below.
2. **Additive `is_visible` migration:** The codebase already has a full two-dialect additive-column pattern in `CategoryKnowledgeRepository.cs` (lines 78-84 + `GetTableColumnsAsync` at lines 1223-1255). Use the same `PRAGMA table_info` (SQLite) / `information_schema.columns` (Postgres) helper pattern verbatim.
3. **Artifact serving:** Artifacts currently live under `artifacts/content-kb/{source-slug}/{video_id}.md` (gitignored). The correct answer is un-gitignore a tracked `content-kb/` publish directory at repo root and serve via a controller action that reads the file, strips frontmatter, and returns the clean text. Controller serving is required because the copy-for-ChatGPT button needs server-side frontmatter stripping.
4. **Seed format + export verb:** JSON is recommended (matches existing CLI patterns, allows schema evolution). New `content-index-export` CLI verb writes to a tracked path (e.g., `content-kb/seed/index-seed.json`). The seed file contains only index columns — never transcript/audio/spend.
5. **Flag-gated routing:** `FeatureFlagGateAttribute` currently returns HTTP 503 with a maintenance view when flag is OFF. For the public route the effect is correct (page is unavailable). The attribute resolves `IFeatureFlagCache` per-request from `HttpContext.RequestServices`. The `content_kb_enabled` flag seed row must be added to `FeatureFlagStore` with `DEFAULT FALSE` / `0`.

**Primary recommendation:** Implement in 4 plans: (1) store extension — `is_visible` migration + new query/SetVisibility methods + `content_kb_enabled` flag seed, (2) CLI seed export verb + artifact publish location, (3) public browse + detail controller + TypeScript filter, (4) admin curation controller + view.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| is_visible column + additive migration | Database/Storage (Core) | — | Schema change in ContentSiteIndexStore; both dialects |
| Seed-load upsert at Render startup | API/Backend (Program.cs) | Database/Storage | EnsureSchemaAsync + seed import; runs before RunAsync |
| content-index-export CLI verb | CLI (DeckFlow.CLI) | Core store | Reads local SQLite content_site_index; writes tracked JSON seed |
| Artifact file location + git tracking | Repo structure | — | Must be in tracked path; `content-kb/` at repo root |
| Artifact serving + frontmatter strip | API/Backend (controller) | — | Controller reads file from repo path; strips --- frontmatter block before returning |
| Public browse page + client filter | Frontend Server (Razor) + Browser | — | Server renders published rows; JS filters client-side |
| Artifact detail page + Markdig render | Frontend Server (Razor) | — | Controller reads file, calls Markdig, passes HTML to view |
| Copy-for-ChatGPT button | Browser / Client | Frontend Server | Hidden textarea server-rendered; TS clipboard write |
| content_kb_enabled flag gate | API/Backend (FeatureFlagGateAttribute) | — | Per-action filter; resolves IFeatureFlagCache per request |
| Admin curation grid (per-entry + bulk) | Frontend Server (Razor) + API/Backend | — | Server-rendered table; form POSTs to admin controller |
| CSRF: ValidateAntiForgeryToken + SameOriginRequestValidator | API/Backend (controller) | — | Both required on every mutating POST (SC4/P11) |
| Nav link conditional render | Frontend Server (_Layout.cshtml) | — | Show "Knowledge Base" link only when flag ON |

---

## Standard Stack

### Core (all already in the project — no new packages)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.Data.Sqlite` | 10.0.0 | SQLite local content site-index | Already referenced in DeckFlow.Core.csproj |
| `Npgsql` | 10.0.0 | Postgres content site-index on Render | Already referenced in DeckFlow.Core.csproj |
| `Markdig` | 0.38.0 | Render artifact markdown → HTML | Already referenced in DeckFlow.Web; used by HelpContentService |
| `ContentSiteIndexStore` | (project) | Slim index persistence (Phase 19, built) | Already exists; needs extension only |
| `FeatureFlagGateAttribute` | (project) | Per-action flag gate | Already exists (Phase 6) |
| `SameOriginRequestValidator` | (project) | CSRF guard on admin POST | Already exists (Phase 7.1) |
| `[ValidateAntiForgeryToken]` | (ASP.NET Core built-in) | Anti-forgery token on admin POST | Already wired via `AddControllersWithViews()` |
| `IHelpContentService` / `HelpContentService` | (project) | Markdig pipeline + frontmatter strip pattern | Already exists; borrow its `SplitHeader` + `Markdown.ToHtml` |

[VERIFIED: codebase] No new NuGet packages are required for Phase 22. All dependencies are already referenced.

**Installation:** None required.

---

## Package Legitimacy Audit

Not applicable — Phase 22 installs no new external packages. All libraries are already present in the solution.

---

## Architecture Patterns

### System Architecture Diagram

```
[Operator runs CLI locally]
  |
  | dotnet run --project DeckFlow.CLI -- content-index-export
  |   Reads: artifacts/content-kb.db (local SQLite, content_site_index)
  |   Writes: content-kb/seed/index-seed.json  (tracked in git)
  |
[git commit + push → Render deploy]
  |
  |--- content-kb/{source-slug}/{video_id}.md  (artifact files, tracked)
  |--- content-kb/seed/index-seed.json          (seed file, tracked)
  |
[Render startup — Program.cs]
  |
  | EnsureSchemaAsync (content_site_index on Postgres)
  |   → CREATE TABLE IF NOT EXISTS + ALTER TABLE ADD COLUMN is_visible (guarded)
  |
  | SeedLoadAsync (reads index-seed.json from ContentRootPath)
  |   → is_visible-preserving upsert per row
  |
[HTTP request: GET /content-kb]
  |
  | FeatureFlagGateAttribute("content.kb.enabled")
  |   → OFF: 503 maintenance view
  |   → ON: continue
  |
  | ContentKbController.Index()
  |   → store.GetPublishedRowsAsync()   [WHERE is_visible = 1/true]
  |   → View(rows) — server renders hub-card grid
  |
  | Browser — content-kb.ts
  |   → text search + df-select filter → show/hide .hub-card elements
  |
[HTTP request: GET /content-kb/{key}]
  |
  | ContentKbController.Detail(key)
  |   → store.GetByNaturalKeyAsync(keyType, keyValue)
  |   → 404 if null or is_visible = false
  |   → Read artifact file from repo path (safe path resolution)
  |   → SplitHeader() → strip frontmatter
  |   → Markdown.ToHtml(body, Pipeline) → HTML
  |   → Server-render clean text into hidden <textarea> for copy button
  |
[HTTP request: GET /Admin/ContentKb]
  |
  | BasicAuthMiddleware
  | AdminContentKbController.Index()
  |   → store.GetAllRowsAsync()          [ALL rows, any is_visible]
  |   → View — admin curation grid
  |
[HTTP request: POST /Admin/ContentKb/SetVisibility]
  |
  | [ValidateAntiForgeryToken] + SameOriginRequestValidator.IsValid(Request)
  | AdminContentKbController.SetVisibility(id, visible)
  |   → store.SetVisibilityAsync(id, visible)
  |   → RedirectToAction("Index")
  |
[HTTP request: POST /Admin/ContentKb/BulkSetVisibility]
  |
  | [ValidateAntiForgeryToken] + SameOriginRequestValidator.IsValid(Request)
  | AdminContentKbController.BulkSetVisibility(source, visible)
  |   → store.SetVisibilityBySourceAsync(source, visible)
  |   → RedirectToAction("Index")
  |
[HTTP request: POST /Admin/ContentKb/ReloadSeed]
  |
  | [ValidateAntiForgeryToken] + SameOriginRequestValidator.IsValid(Request)
  | AdminContentKbController.ReloadSeed()
  |   → SeedLoadAsync (same logic as startup, idempotent)
  |   → RedirectToAction("Index") with success banner
```

### Recommended Project Structure

```
content-kb/                          ← NEW: tracked, un-gitignored publish dir
  seed/
    index-seed.json                  ← exported by CLI content-index-export verb
  edhrecast/
    zkAmYkIOx98.md                   ← copied/moved from artifacts/content-kb/
  the-command-zone/
    f8782tCIwmk.md
  (... 10 artifacts from UAT run)

DeckFlow.Core/Content/
  ContentSiteIndexStore.cs           ← EXTEND: is_visible column, new queries, SetVisibility
  IContentSiteIndexStore.cs          ← EXTEND: new method signatures

DeckFlow.Web/Controllers/
  ContentKbController.cs             ← NEW: public browse + detail actions
  Admin/
    AdminContentKbController.cs      ← NEW: admin curation actions

DeckFlow.Web/Views/
  ContentKb/
    Index.cshtml                     ← NEW: public browse page (.hub-grid)
    Detail.cshtml                    ← NEW: artifact detail page (.kb-artifact-prose)
    _ViewStart.cshtml                ← NEW
  AdminContentKb/
    Index.cshtml                     ← NEW: admin curation grid
    _ViewStart.cshtml                ← NEW

DeckFlow.Web/wwwroot/ts/
  content-kb.ts                      ← NEW: client-side multi-facet filter + clear

DeckFlow.Web/wwwroot/css/
  site-common.css                    ← EXTEND: .kb-filter-bar, .kb-tag, .kb-empty, .kb-artifact-prose
  admin-common.css                   ← EXTEND: .admin-shell .kb-status, .admin-shell .kb-tag

DeckFlow.Web/Services/
  ContentKbSeedLoader.cs             ← NEW: reads index-seed.json, calls store upsert (preserves is_visible)
  IContentKbSeedLoader.cs            ← NEW

DeckFlow.Web/Program.cs              ← EXTEND: register IContentSiteIndexStore + call EnsureSchemaAsync + seed load
DeckFlow.Web/Views/Shared/_Layout.cshtml  ← EXTEND: conditional "Knowledge Base" nav link
DeckFlow.Web/Views/Shared/_AdminLayout.cshtml ← EXTEND: "Content KB" sidebar nav entry

DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs ← EXTEND: add content_kb_enabled seed row (FALSE/0)

DeckFlow.CLI/Program.cs             ← EXTEND: register content-index-export verb
DeckFlow.CLI/CommandRunners.cs      ← EXTEND: RunContentIndexExportAsync method
```

---

## Critical Research Findings (5 Questions)

### Q1: Render Postgres Index Materialization + is_visible-Preserving Seed Upsert

**Startup hook location:** Program.cs lines 423-431 already call `EnsureSchemaAsync` for harvest and analytics stores in sequential `await` calls BEFORE `app.RunAsync()` (line 452). The content site-index store goes in the same block. [VERIFIED: codebase — Program.cs:423-431]

**Pattern:**
```csharp
// After existing harvest/analytics EnsureSchemaAsync calls:
app.Logger.LogInformation("Ensuring Content KB site-index schema during startup.");
var contentIndexStore = app.Services.GetRequiredService<IContentSiteIndexStore>();
await contentIndexStore.EnsureSchemaAsync();
app.Logger.LogInformation("Content KB site-index schema ensured.");

// Seed load — only when seed file exists (works locally via SQLite too)
var seedLoader = app.Services.GetRequiredService<IContentKbSeedLoader>();
await seedLoader.LoadIfPresentAsync(app.Environment);
app.Logger.LogInformation("Content KB seed load complete.");
```

`IContentSiteIndexStore` is registered as a singleton (matches the pattern for `IFeedbackStore`, `ICategoryKnowledgeStore` etc.). `IContentKbSeedLoader` is a new singleton service in `DeckFlow.Web/Services/` that reads `content-kb/seed/index-seed.json` from `IWebHostEnvironment.ContentRootPath` and calls `UpsertRowPreservingVisibilityAsync` per entry.

**Postgres connection pool discipline:** `ContentSiteIndexStore` already opens-and-disposes (`await using var connection`) per operation (verified lines 51, 80, 107 — no connection held across awaits). This is correct for Render's pool-cap environment. No change needed.

**is_visible-preserving upsert SQL shapes:**

Postgres — the key is to include `is_visible` in INSERT (default hidden = `FALSE`) but exclude it from the DO UPDATE SET list entirely:
```sql
INSERT INTO content_site_index (
  source, title, video_url, artifact_path,
  published_utc, indexed_utc,
  archetype_tags, bracket_tags, card_category_tags,
  natural_key_type, natural_key_value,
  is_visible)
VALUES (
  @source, @title, @videoUrl, @artifactPath,
  @publishedUtc, @indexedUtc,
  @archetypeTags, @bracketTags, @cardCategoryTags,
  @naturalKeyType, @naturalKeyValue,
  FALSE)
ON CONFLICT (natural_key_type, natural_key_value) DO UPDATE SET
  source             = EXCLUDED.source,
  title              = EXCLUDED.title,
  video_url          = EXCLUDED.video_url,
  artifact_path      = EXCLUDED.artifact_path,
  published_utc      = EXCLUDED.published_utc,
  indexed_utc        = EXCLUDED.indexed_utc,
  archetype_tags     = EXCLUDED.archetype_tags,
  bracket_tags       = EXCLUDED.bracket_tags,
  card_category_tags = EXCLUDED.card_category_tags;
  -- is_visible intentionally OMITTED from DO UPDATE — preserves admin curation
```

SQLite — identical structure (SQLite 3.35+ supports `EXCLUDED`; the existing UpsertSql already uses it — verified ContentSiteIndexStore.cs line 261):
```sql
INSERT INTO content_site_index (
  source, title, video_url, artifact_path,
  published_utc, indexed_utc,
  archetype_tags, bracket_tags, card_category_tags,
  natural_key_type, natural_key_value,
  is_visible)
VALUES (
  @source, @title, @videoUrl, @artifactPath,
  @publishedUtc, @indexedUtc,
  @archetypeTags, @bracketTags, @cardCategoryTags,
  @naturalKeyType, @naturalKeyValue,
  0)
ON CONFLICT (natural_key_type, natural_key_value) DO UPDATE SET
  source             = EXCLUDED.source,
  title              = EXCLUDED.title,
  video_url          = EXCLUDED.video_url,
  artifact_path      = EXCLUDED.artifact_path,
  published_utc      = EXCLUDED.published_utc,
  indexed_utc        = EXCLUDED.indexed_utc,
  archetype_tags     = EXCLUDED.archetype_tags,
  bracket_tags       = EXCLUDED.bracket_tags,
  card_category_tags = EXCLUDED.card_category_tags;
  -- is_visible intentionally OMITTED from DO UPDATE — preserves admin curation
```

The existing `UpsertRowAsync` method in `ContentSiteIndexStore` does NOT preserve `is_visible` (it uses the current UpsertSql which clobbers all columns). Phase 22 adds a NEW method `UpsertRowPreservingVisibilityAsync` (used by seed-load + CLI distill path going forward) distinct from `UpsertRowAsync` (used by CLI during local distill only — local visibility does not matter).

[VERIFIED: codebase — ContentSiteIndexStore.cs UpsertSql lines 236-271]

---

### Q2: Additive `is_visible` Migration in EnsureSchemaAsync

The codebase has a complete, verified two-dialect additive-column pattern in `CategoryKnowledgeRepository.cs` (Phase 27). [VERIFIED: codebase — CategoryKnowledgeRepository.cs:78-84, GetTableColumnsAsync:1223-1255]

**The exact pattern to replicate in `ContentSiteIndexStore.EnsureSchemaAsync`:**

After the `CREATE TABLE IF NOT EXISTS` command:
```csharp
// Step 1: Run CREATE TABLE IF NOT EXISTS (existing — no change)
await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

// Step 2: Additive is_visible column guard (D-05)
var columns = await GetTableColumnsAsync(connection, "content_site_index", cancellationToken)
    .ConfigureAwait(false);
if (!columns.Contains("is_visible"))
{
    await using var addCol = connection.CreateCommand();
    // Postgres: BOOLEAN NOT NULL DEFAULT FALSE
    // SQLite:   INTEGER NOT NULL DEFAULT 0
    addCol.CommandText = _connectionInfo.IsPostgres
        ? "ALTER TABLE content_site_index ADD COLUMN is_visible BOOLEAN NOT NULL DEFAULT FALSE;"
        : "ALTER TABLE content_site_index ADD COLUMN is_visible INTEGER NOT NULL DEFAULT 0;";
    await addCol.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```

**`GetTableColumnsAsync` helper** (copy from CategoryKnowledgeRepository, make it a private static method in `ContentSiteIndexStore`):
- SQLite path: `PRAGMA table_info(table_name)` — column name is at ordinal 1
- Postgres path: `SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = @tableName`

**Why no `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` (Postgres 9.6+):** The codebase deliberately uses the `GetTableColumnsAsync` + conditional ALTER pattern rather than `IF NOT EXISTS`, because this pattern also works for SQLite (which has no `IF NOT EXISTS` on ALTER). Keep the same pattern for consistency.

[VERIFIED: codebase — CategoryKnowledgeRepository.cs:78-84 for SQLite; :1223-1255 for both dialects]

The `CREATE TABLE IF NOT EXISTS` DDL for `content_site_index` must also be updated to include `is_visible` for fresh databases (new Render deploys start with the column already present):
```sql
-- Postgres addition to PostgresCreateTableSql:
is_visible BOOLEAN NOT NULL DEFAULT FALSE,

-- SQLite addition to SqliteCreateTableSql:
is_visible INTEGER NOT NULL DEFAULT 0,
```

The `_schemaReady` volatile bool + `_schemaGate` SemaphoreSlim in `ContentSiteIndexStore` remain — this ensures `EnsureSchemaAsync` is idempotent (called by multiple operations). The SemaphoreSlim guarantees the ALTER is only attempted once per process lifetime.

---

### Q3: Serving Committed Artifacts

**Current state:** 10 artifacts exist at `artifacts/content-kb/{source-slug}/{video_id}.md` on disk but `artifacts/` is gitignored. [VERIFIED: codebase — .gitignore line 4; confirmed 10 files in artifacts/content-kb/]

**Recommended approach: un-gitignore a tracked `content-kb/` publish directory at repo root, controller-served.**

Rationale:
- The copy-for-ChatGPT button needs the clean body text (frontmatter stripped). A static file would serve raw frontmatter to the browser; a controller action strips it server-side before rendering the hidden `<textarea>`.
- `prompt-templates/` is the existing precedent for repo-committed content assets served via a controller (HelpContentService reads `Help/*.md` from `ContentRootPath`).
- Path: `content-kb/{source-slug}/{video_id}.md` at repo root (sibling of `prompt-templates/`).
- `.gitignore` currently has `content-kb/` on line 5 — remove this line.
- The CLI `ContentArtifactWriter` writes to `artifacts/content-kb/` (based on `MTG_DATA_DIR`/`artifacts/`). The operator copies/moves the `artifacts/content-kb/` tree to the tracked `content-kb/` dir before committing. (The CLI `content-index-export` verb can document this two-step: export → copy artifacts → commit.)

**Controller artifact resolution (path-traversal guard):**

`ContentSiteIndexStore` already validates `ArtifactPath` against rooted paths and `..` segments in `ValidateArtifactPath()` (lines 169-186 — VERIFIED). The controller must:
1. Call `store.GetByNaturalKeyAsync(keyType, keyValue)` to get the row (returns null → 404).
2. Check `row.IsVisible == false` → 404 (public controller; admin controller skips this check).
3. Resolve artifact path:
```csharp
// Why: ArtifactPath is relative (validated by store); resolve against tracked content-kb dir
//      at ContentRootPath/../content-kb/ (repo root, not wwwroot).
var repoRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, ".."));
var safeArtifactPath = Path.GetFullPath(Path.Combine(repoRoot, "content-kb", row.ArtifactPath));

// Verify the resolved path stays inside content-kb/ (defense-in-depth against malformed DB data)
var contentKbRoot = Path.GetFullPath(Path.Combine(repoRoot, "content-kb"));
if (!safeArtifactPath.StartsWith(contentKbRoot, StringComparison.OrdinalIgnoreCase))
{
    return NotFound(); // or Problem() — never expose the path
}

if (!File.Exists(safeArtifactPath))
{
    // Render "artifact temporarily unavailable" in the view (UI-SPEC copy)
    return View("Detail", new ContentKbDetailViewModel { ArtifactUnavailable = true, ... });
}
```
4. Read the file, call `SplitHeader(rawText)` (borrow `HelpContentService`'s private static method — extract to `ContentArtifactParser` static helper), pass `body` to Markdig, pass clean `body` text into `<textarea>`.

**Note on ContentRootPath:** On Render, `ContentRootPath` = `/app/DeckFlow.Web/` (the project directory inside the container). The repo root in the container is `/app/`. `content-kb/` at repo root resolves to `/app/content-kb/`. The `Dockerfile` copies the full repo into `/app/` in the build stage and publishes to the runtime image — the content-kb directory is included if tracked in git. [ASSUMED — Dockerfile multi-stage build confirmed present but not inspected for exact COPY paths]

**Alternative:** Register `content-kb/` as a `StaticFiles` root. This is simpler for raw file serving but does not support frontmatter stripping or is_visible gating (would expose hidden entries via direct URL). Controller approach is correct.

---

### Q4: Committed Seed Format + Export Verb

**Recommendation: JSON seed file, new `content-index-export` CLI verb.**

**Format:** JSON array of objects, one per `content_site_index` row. Only index columns — NO `is_visible` (that is admin-only state that must not be overwritten by seed), NO transcript/audio/spend. [VERIFIED: D-02b + SC1 requirements]

```json
[
  {
    "naturalKeyType": "youtube_channel",
    "naturalKeyValue": "zkAmYkIOx98",
    "source": "EDHRECast",
    "title": "Bizarre Mana Curves That ACTUALLY Work | EDHRECast 407",
    "videoUrl": "https://www.youtube.com/watch?v=zkAmYkIOx98&list=...",
    "artifactPath": "edhrecast/zkAmYkIOx98.md",
    "publishedUtc": null,
    "indexedUtc": "2026-06-01T19:45:56Z",
    "archetypeTags": ["ramp","spellslinger","voltron"],
    "bracketTags": ["cEDH"],
    "cardCategoryTags": ["ramp","draw","removal"]
  }
]
```

**Tracked seed location:** `content-kb/seed/index-seed.json` (inside the tracked `content-kb/` directory). `IContentKbSeedLoader` resolves it from `Path.Combine(environment.ContentRootPath, "..", "content-kb", "seed", "index-seed.json")`.

**CLI verb registration pattern** (mirrors existing `distillCommand` at lines 72-118 in CLI/Program.cs):

```csharp
// In DeckFlow.CLI/Program.cs:
var contentIndexExportCommand = new Command("content-index-export",
    "Exports the local content_site_index to a tracked JSON seed file for commit-then-deploy.");
var exportDbOption = new Option<FileInfo?>("--db")
    { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var exportOutputOption = new Option<FileInfo?>("--output")
    { Description = "Output JSON path. Defaults to content-kb/seed/index-seed.json." };
contentIndexExportCommand.AddOption(exportDbOption);
contentIndexExportCommand.AddOption(exportOutputOption);
rootCommand.AddCommand(contentIndexExportCommand);
contentIndexExportCommand.SetHandler((FileInfo? db, FileInfo? output) =>
    CommandRunners.RunContentIndexExportAsync(db, output), exportDbOption, exportOutputOption);
```

`CommandRunners.RunContentIndexExportAsync`:
1. Opens local SQLite via `CreateLocalContentKbConnection` (always-SQLite, D-14).
2. Calls a new `IContentSiteIndexStore.GetAllRowsAsync()` (no visibility filter — exports ALL rows so the admin can curate from the full set on Render).
3. Serializes to JSON (System.Text.Json, already implicit-using in CLI).
4. Writes to the output path (default: finds repo root from Assembly location, writes `content-kb/seed/index-seed.json`).
5. Prints count + output path to stdout.

[ASSUMED — CLI `RunContentIndexExportAsync` pattern mirrors existing `RunDistillAsync`; no alternative approach documented in official sources]

---

### Q5: Flag-Gated Routing + Admin CSRF Pattern

**`content_kb_enabled` flag registration:**

Add to `FeatureFlagStore.PostgresSeedSql` and `SqliteSeedSql` (lines 174-189 — VERIFIED):
```sql
-- Postgres:
('content.kb.enabled', FALSE),   -- default OFF per D-01a + SC5

-- SQLite:
('content.kb.enabled', 0),
```

Use key `content.kb.enabled` (dotted-namespace convention, verified from existing keys: `scryfall.tagger.enabled`, `page.help.enabled`, etc.).

The `AdminFlagsController.Toggle` validates the key exists in the snapshot before writing (line 83 — VERIFIED: T-06-E2 mitigation). Adding the key to the seed ensures it appears in the snapshot immediately on first boot.

**Public controller gating:**

```csharp
[Route("content-kb")]
public sealed class ContentKbController : Controller
{
    [HttpGet("")]
    [FeatureFlagGate("content.kb.enabled",
        Title = "Knowledge Base unavailable",
        Message = "The Knowledge Base is not currently available.")]
    public async Task<IActionResult> Index() { ... }

    [HttpGet("{key}")]
    [FeatureFlagGate("content.kb.enabled",
        Title = "Knowledge Base unavailable",
        Message = "The Knowledge Base is not currently available.")]
    public async Task<IActionResult> Detail(string key) { ... }
}
```

`FeatureFlagGateAttribute` returns HTTP 503 (not 404) when flag is OFF. The UI-SPEC says "standard 404 — no custom copy needed (flag-off renders 404 at the framework level)" — this is a minor spec inaccuracy. The actual behaviour is 503 + `_MaintenancePage` view (verified FeatureFlagGateAttribute.cs lines 66-84). The 503 is acceptable (hides the page from users) and consistent with all other flag-gated pages. [VERIFIED: FeatureFlagGateAttribute.cs]

**Nav link conditional render in `_Layout.cshtml`:**

```cshtml
@inject DeckFlow.Web.Services.FeatureFlags.IFeatureFlagCache FeatureFlagCache
@if (FeatureFlagCache.IsEnabled("content.kb.enabled"))
{
    <li><a asp-controller="ContentKb" asp-action="Index">Knowledge Base</a></li>
}
```

`IFeatureFlagCache.IsEnabled` is lock-free (confirmed FeatureFlagCache design — comment in IFeatureFlagCache.cs). Safe to call on every request.

**Admin curation controller CSRF pattern** (both mechanisms required per STATE.md invariant #7):

```csharp
[Route("Admin/ContentKb")]
public sealed class AdminContentKbController : Controller
{
    [HttpPost("SetVisibility")]
    [ValidateAntiForgeryToken]   // anti-forgery token
    public async Task<IActionResult> SetVisibility(long entryId, bool visible, CancellationToken ct)
    {
        if (!SameOriginRequestValidator.IsValid(Request))  // same-origin check
            return StatusCode(StatusCodes.Status403Forbidden,
                SameOriginRequestValidator.GetForbiddenMessage());
        ...
    }

    [HttpPost("BulkSetVisibility")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkSetVisibility(string source, bool visible, CancellationToken ct)
    {
        if (!SameOriginRequestValidator.IsValid(Request)) return Forbid();
        ...
    }

    [HttpPost("ReloadSeed")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReloadSeed(CancellationToken ct)
    {
        if (!SameOriginRequestValidator.IsValid(Request)) return Forbid();
        ...
    }
}
```

Razor views use `@Html.AntiForgeryToken()` inside each `<form method="post">` (existing pattern verified in AdminHarvest/Index.cshtml line 52). The `AdminFlagsController.Toggle` at `/Admin/Flags/{key}/toggle` already handles `content.kb.enabled` flag toggle via the existing flags surface — the admin KB page just links to it or embeds the same form pattern.

[VERIFIED: SameOriginRequestValidator.cs; AdminFlagsController.cs; AdminHarvest/Index.cshtml]

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Markdown rendering | Custom markdown parser | `Markdig` (existing `HelpContentService.Pipeline`) | Edge cases: nested lists, code blocks, links, HTML injection. HelpContentService.cs line 13-14 already configures `UseAdvancedExtensions().DisableHtml()` |
| Frontmatter stripping | Regex or manual | `HelpContentService.SplitHeader` (extract to static helper) | Already handles `---` delimiters, CRLF/LF, multi-line header, returns (Header dict, Body string) |
| CSRF protection | Session tokens, nonce, custom validation | `[ValidateAntiForgeryToken]` + `SameOriginRequestValidator.IsValid()` | Two-layer defense already tested and deployed; missing either breaks SC4 |
| Feature flag gating | Custom middleware or response filter | `FeatureFlagGateAttribute` | Per-action; handles 503 + Retry-After + maintenance view; resolves cache per-request |
| Clipboard copy | `document.execCommand('copy')` | Existing `attachDynamicCopyButton` TS (data-copy-target pattern) | `navigator.clipboard.writeText`, idle/copied/failed states already tested across 3 workflows |
| Path traversal guard | String.Contains("..") | `ValidateArtifactPath()` (already in ContentSiteIndexStore) + `Path.GetFullPath` prefix check | Windows vs Linux path separator, rooted path detection already handles edge cases |
| Additive DB migration | Drop+recreate table | `GetTableColumnsAsync` + conditional `ALTER TABLE ADD COLUMN` | Preserves existing data; exact pattern already in CategoryKnowledgeRepository.cs |

**Key insight:** Every hard problem in this phase is already solved in the codebase. The planner must wire existing seams, not build new infrastructure.

---

## Common Pitfalls

### Pitfall 1: Seed Upsert Clobbers is_visible
**What goes wrong:** Using the existing `UpsertRowAsync` SQL (or a naive INSERT ON CONFLICT DO UPDATE SET ... is_visible = FALSE) re-hides every entry the admin published. Every deploy resets curation to zero.
**Why it happens:** The is_visible-preserving constraint is an uncommon SQL pattern; developers default to updating all columns.
**How to avoid:** Use the `UpsertRowPreservingVisibilityAsync` method with the exact SQL shown in Q1 above. `is_visible` appears in the INSERT column list (for new rows — gets the default hidden value) but NOT in the DO UPDATE SET list (for existing rows — value untouched).
**Warning signs:** After a seed reload or redeploy, all entries show as "Hidden" in the admin grid.

### Pitfall 2: Additive ALTER in EnsureSchemaAsync Runs Every Startup
**What goes wrong:** `ALTER TABLE ADD COLUMN` without the column-existence check throws on Postgres (`column "is_visible" of relation "content_site_index" already exists`) and silently no-ops or throws on SQLite.
**Why it happens:** Forgetting the `GetTableColumnsAsync` + `if (!columns.Contains("is_visible"))` guard.
**How to avoid:** Copy the exact guard pattern from `CategoryKnowledgeRepository.cs:78-84`.
**Warning signs:** Render startup logs show an exception from `EnsureSchemaAsync` on second deploy.

### Pitfall 3: Artifact Path Resolution Escapes content-kb/ Root
**What goes wrong:** An `ArtifactPath` value like `../../DeckFlow.Web/appsettings.json` resolves to a file outside the content-kb directory. Even with the store-level validation, defensive path checking in the controller is required.
**Why it happens:** `ValidateArtifactPath` in the store rejects `..` segments at write time, but data could be in the DB from an older version or corrupted seed.
**How to avoid:** After `Path.GetFullPath(Path.Combine(contentKbRoot, row.ArtifactPath))`, verify the result starts with `contentKbRoot` (with trailing separator check). Return 404 silently on failure — never log the resolved path in user-visible output.
**Warning signs:** Unit test: pass `ArtifactPath = "../../secret.txt"` and verify controller returns 404, not the file content.

### Pitfall 4: content-kb/ Directory Not Copied into Docker Image
**What goes wrong:** `content-kb/` is tracked in git and present in the build context, but the `Dockerfile` COPY step only copies specific directories (e.g., `DeckFlow.Web/` only), leaving `content-kb/` out of the runtime image. Artifact reads return 404 in prod but work locally.
**Why it happens:** Dockerfiles often copy only the project directory, not the repo root.
**How to avoid:** Verify `Dockerfile` COPY includes the repo root or explicitly add `COPY content-kb/ ./content-kb/`. [ASSUMED — Dockerfile not fully inspected]
**Warning signs:** `File.Exists(safeArtifactPath)` returns false in prod for all entries; `ArtifactUnavailable = true` banner shows on detail pages.

### Pitfall 5: FeatureFlagGateAttribute Returns 503 Not 404
**What goes wrong:** The UI-SPEC says "standard 404" when flag is OFF, but the attribute actually returns HTTP 503 + Retry-After: 300 + _MaintenancePage view. This is correct behavior but the verifier must not test for 404.
**Why it happens:** UI-SPEC was written with assumed behavior; attribute implementation differs.
**How to avoid:** When verifying SC5, confirm the flag-off response is 503 (not 404). The maintenance page is the correct UX. [VERIFIED: FeatureFlagGateAttribute.cs:66-69]

### Pitfall 6: Flag Seed Row Missing → content.kb.enabled Not Recognized by AdminFlagsController
**What goes wrong:** `AdminFlagsController.Toggle` validates the key against `_cache.Snapshot()` (line 83 — T-06-E2 mitigation). If `content.kb.enabled` is not seeded into `feature_flags`, it never appears in the snapshot, so the toggle endpoint returns 400 Bad Request.
**Why it happens:** Forgetting to add `('content.kb.enabled', FALSE)` to both `PostgresSeedSql` and `SqliteSeedSql` in `FeatureFlagStore.cs`.
**How to avoid:** Add the seed row in Phase 22 plan-01 alongside the store extension. Test: after restart, confirm `GET /Admin/Flags` shows `content.kb.enabled` = disabled.
**Warning signs:** Admin Flags page shows the key is missing; clicking Enable returns 400.

### Pitfall 7: content-kb/ gitignore Conflict
**What goes wrong:** `.gitignore` line 5 currently ignores `content-kb/`. If the operator adds files to `content-kb/` without removing this line, git silently ignores them and they never ship to Render.
**Why it happens:** The gitignore was added during Phase 21 to exclude the local work-in-progress directory.
**How to avoid:** Remove `content-kb/` from `.gitignore` AND add `artifacts/content-kb/` as the gitignored path (local-only distill output). [VERIFIED: .gitignore lines 4-5 — `artifacts/` is line 4, `content-kb/` is line 5]

---

## Code Examples

### Additive Column Migration (verified pattern from codebase)

```csharp
// Source: DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs:78-84
var deckQueueColumns = await GetTableColumnsAsync(connection, "deck_queue", cancellationToken);
if (!deckQueueColumns.Contains("content_hash"))
{
    var addContentHashCommand = connection.CreateCommand();
    addContentHashCommand.CommandText = "ALTER TABLE deck_queue ADD COLUMN content_hash TEXT NULL;";
    await addContentHashCommand.ExecuteNonQueryAsync(cancellationToken);
}
```

```csharp
// Source: DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs:1223-1255
private async Task<IReadOnlySet<string>> GetTableColumnsAsync(
    DbConnection connection, string tableName, CancellationToken cancellationToken)
{
    var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (_connectionInfo.IsSqlite)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            if (!reader.IsDBNull(1)) columns.Add(reader.GetString(1));
        return columns;
    }
    var pgCommand = connection.CreateCommand();
    pgCommand.CommandText = """
        SELECT column_name FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = @tableName
        ORDER BY ordinal_position;
        """;
    RelationalDatabaseConnection.AddParameter(pgCommand, "@tableName", tableName);
    await using var pgReader = await pgCommand.ExecuteReaderAsync(cancellationToken);
    while (await pgReader.ReadAsync(cancellationToken))
        columns.Add(pgReader.GetString(0));
    return columns;
}
```

### Feature Flag Seed (existing pattern to extend)

```csharp
// Source: DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs:174-190
private const string PostgresSeedSql = """
    INSERT INTO feature_flags (key, enabled) VALUES
      ('scryfall.tagger.enabled', TRUE),
      ('page.help.enabled', TRUE),
      ('harvest.cron.enabled', TRUE),
      ('feature.categories.enabled', TRUE),
      ('content.kb.enabled', FALSE)        -- Phase 22: default OFF per D-01a
    ON CONFLICT (key) DO NOTHING;
    """;

private const string SqliteSeedSql = """
    INSERT INTO feature_flags (key, enabled) VALUES
      ('scryfall.tagger.enabled', 1),
      ('page.help.enabled', 1),
      ('harvest.cron.enabled', 1),
      ('feature.categories.enabled', 1),
      ('content.kb.enabled', 0)            -- Phase 22: default OFF per D-01a
    ON CONFLICT (key) DO NOTHING;
    """;
```

### Frontmatter-Stripping Pattern (existing pattern to extract)

```csharp
// Source: DeckFlow.Web/Services/HelpContentService.cs:66-88
// Extract to: DeckFlow.Web/Services/ContentArtifactParser.cs (static helper)
private static (Dictionary<string, string> Header, string Body) SplitHeader(string raw)
{
    var header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var lines = raw.Replace("\r\n", "\n").Split('\n');
    if (lines.Length == 0 || lines[0].Trim() != "---")
        return (header, raw);
    var end = Array.FindIndex(lines, 1, l => l.Trim() == "---");
    if (end < 0) return (header, raw);
    for (var i = 1; i < end; i++)
    {
        var line = lines[i];
        var colon = line.IndexOf(':');
        if (colon <= 0) continue;
        header[line[..colon].Trim()] = line[(colon + 1)..].Trim();
    }
    return (header, string.Join('\n', lines.Skip(end + 1)));
}
```

### Startup EnsureSchemaAsync Pattern (existing pattern to follow)

```csharp
// Source: DeckFlow.Web/Program.cs:423-431
await ValidateDatabaseConnectionsAsync(app.Services, app.Environment, app.Logger);
app.Logger.LogInformation("Ensuring harvest store schemas during startup.");
await app.Services.GetRequiredService<IHarvestRunStore>().EnsureSchemaAsync();
await app.Services.GetRequiredService<IHarvestScheduleStore>().EnsureSchemaAsync();
app.Logger.LogInformation("Harvest store schemas ensured during startup.");
```

### Admin Action Form + CSRF (existing pattern)

```cshtml
<!-- Source: DeckFlow.Web/Views/AdminHarvest/Index.cshtml:50-53 -->
<form method="post" asp-action="RunNow" class="admin-action-form">
    @Html.AntiForgeryToken()
    <button type="submit">Run Now</button>
</form>
```

### Copy Button Pattern (existing pattern — reuse for artifact detail)

```html
<!-- Source: UI-SPEC Surface 2 — mirrors existing card-lookup.ts attachDynamicCopyButton -->
<button type="button" class="copy-button run-button"
        data-copy-target="kb-artifact-text"
        aria-label="Copy this artifact for ChatGPT">
  Copy for ChatGPT
</button>
<textarea id="kb-artifact-text" readonly hidden>@Model.CleanBodyText</textarea>
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Static file serving for markdown | Controller-served with frontmatter strip | Phase 22 (new) | Enables server-side clean text for copy button; enables is_visible gating |
| All rows visible by default | New rows hidden by default (`is_visible=0`), admin opt-in | Phase 22 D-05 | Admin curates before users see anything |
| `content-kb/` gitignored (local work dir) | tracked `content-kb/` (publish dir) + `artifacts/content-kb/` gitignored | Phase 22 D-02a | Artifacts ship via git, not upload |

**Deprecated/outdated:**
- `UpsertRowAsync` for seed-load path: still valid for local CLI distill use but must NOT be used for the Render seed-load (it clobbers is_visible). Use `UpsertRowPreservingVisibilityAsync` for seed-load.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Dockerfile COPY includes repo root (so `content-kb/` ships to runtime image) | Q3 Artifact Serving | All artifact reads return "unavailable" in prod; mitigated by verifying Dockerfile before plan |
| A2 | CLI `RunContentIndexExportAsync` can locate repo root via `Assembly.GetExecutingAssembly().Location` + path navigation | Q4 Seed Format | Export writes to wrong path; low risk — operator sees the written path in stdout |
| A3 | `ContentRootPath` in the Render container is `/app/DeckFlow.Web/` (making repo root `/app/`) | Q3 Artifact Serving | Artifact path resolution fails; verify with `app.Logger.LogInformation` of `ContentRootPath` at startup |
| A4 | Bulk `SetVisibilityBySourceAsync` upsert on Render Postgres with 10 rows poses no pool-cap concern | Q1 Startup | Negligible at current scale; pool cap (10-15) is per-connection limit, not per-row |

---

## Open Questions

1. **Dockerfile artifact copy path**
   - What we know: `content-kb/` will be tracked in git; Dockerfile exists but was not fully inspected
   - What's unclear: Whether the existing `COPY` instructions include the repo root or only the project dirs
   - Recommendation: Planner adds a task in Wave 0 to inspect `Dockerfile` and add `COPY content-kb/ ./content-kb/` if missing

2. **Route for detail page: natural key URL shape**
   - What we know: `ContentSiteIndexRow` has `natural_key_type` (`youtube_channel` | `podcast_rss`) and `natural_key_value` (video_id or rss_guid)
   - What's unclear: Whether the URL should be `/content-kb/{video_id}` (simple) or `/content-kb/{source-slug}/{video_id}` (scoped)
   - Recommendation: Use `/content-kb/{key}` where `key = {source_slug}--{video_id}` (slugified, URL-safe, human-readable). The controller splits on `--` or uses the natural_key_value directly since video IDs are already URL-safe. Planner decides.

3. **`ArtifactPath` stored in seed vs resolved at runtime**
   - What we know: `ArtifactPath` is currently relative (e.g., `edhrecast/zkAmYkIOx98.md`) — validated by store
   - What's unclear: Whether the path in the seed JSON should be relative to `content-kb/` root or include `content-kb/` prefix
   - Recommendation: Store without the `content-kb/` prefix (controller prepends the root). Consistent with how `HelpContentService` stores slug without the `Help/` prefix.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Npgsql (Postgres) | Render content_site_index | Already in solution | 10.0.0 | SQLite for local dev |
| Microsoft.Data.Sqlite | Local dev content_site_index | Already in solution | 10.0.0 | — |
| Markdig | Artifact detail render | Already in solution | 0.38.0 | — |
| `dotnet` (.NET 10 SDK) | Build + test | Available in WSL | 10.x | — |
| `content-kb/` tracked files | Artifact serving | 10 artifacts confirmed at `artifacts/content-kb/` — need copy to tracked dir | — | Controller returns "unavailable" banner gracefully |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:**
- Tracked `content-kb/` directory (not yet created) — controller shows "artifact temporarily unavailable" banner when file missing; non-blocking for browse page.

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes (admin surface) | Existing `BasicAuthMiddleware` on `/Admin/*` — unchanged |
| V3 Session Management | no | No new sessions |
| V4 Access Control | yes | `FeatureFlagGateAttribute` on public route; `BasicAuthMiddleware` on admin route; `is_visible` check on detail page |
| V5 Input Validation | yes | `ValidateArtifactPath` (store-level); `Path.GetFullPath` prefix check (controller-level); natural key type whitelist (`CHECK` constraint on DB) |
| V6 Cryptography | no | No new crypto |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal via `ArtifactPath` | Tampering | `ValidateArtifactPath()` at store write time + `Path.GetFullPath` prefix check in controller — defense in depth |
| CSRF on admin visibility toggle | Spoofing | `[ValidateAntiForgeryToken]` + `SameOriginRequestValidator.IsValid()` on all POST actions — both required per STATE.md invariant #7 |
| Flag enumeration / unknown key injection | Elevation of Privilege | `AdminFlagsController.Toggle` rejects unknown keys (T-06-E2); seed ensures `content.kb.enabled` is known |
| Unauthenticated access to admin curation | Spoofing | `BasicAuthMiddleware` on `/Admin/*` branch (Program.cs:384-386) — unchanged, covers new admin controller |
| Artifact content leaking hidden entries | Info Disclosure | `is_visible = false` check in `ContentKbController.Detail()` returns 404 before reading file |
| Secrets in committed artifact files | Info Disclosure | Artifacts contain only LLM-distilled public content (summary/clips/tags of public YouTube/podcast content). No API keys or personal data. SC1 enforced by CLI export (no transcript/audio/spend in seed). Public repo risk: low. |

---

## Project Constraints (from CLAUDE.md)

| Directive | Impact on Phase 22 |
|-----------|-------------------|
| Layout CSS in `site-common.css` / `admin-common.css`, NOT `site.css` | New `.kb-filter-bar`, `.kb-tag`, `.kb-empty`, `.kb-artifact-prose` in `site-common.css`; admin KB status classes in `admin-common.css` under `.admin-shell` |
| Admin CSS scoped to `.admin-shell` parent | All admin KB CSS uses `.admin-shell .kb-status` etc. — zero unscoped selectors |
| No new NuGet packages without asking | Confirmed: zero new packages required |
| `{ get; init; }` preserved on all record types | `ContentSiteIndexRow` already uses `init`; new view models must too |
| No Format Document / code cleanup on existing files | Touch only lines that change; FeatureFlagStore.cs and ContentSiteIndexStore.cs edits are surgical |
| Plain commits, no Co-Authored-By trailer | Applies to all Phase 22 commits |
| Public repo — no secrets in commits | `content-kb/` artifacts contain only distilled public content; no API keys |
| VSTest unreliable in WSL — use `dotnet build` + manual UAT | Verification = build clean + targeted unit tests + manual UAT: browse page, artifact detail, copy button, admin curation, reload seed, flag toggle |
| Postgres connection pool cap 10-15 — never hold connection across awaits | `ContentSiteIndexStore` already opens+disposes per operation — no change needed |
| R-6 formatting paranoia — touch only lines that need touching | Especially critical in FeatureFlagStore.cs (raw-string SQL literals) and ContentSiteIndexStore.cs |

---

## Sources

### Primary (HIGH confidence)
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — schema DDL, UpsertSql, ValidateArtifactPath, EnsureSchemaAsync pattern
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs:78-84, 1223-1255` — additive ALTER TABLE pattern (both dialects)
- `DeckFlow.Web/Program.cs:423-452` — EnsureSchemaAsync startup hook location
- `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs` — returns 503 + MaintenancePage when flag OFF
- `DeckFlow.Web/Controllers/Admin/AdminFlagsController.cs` — flag toggle pattern, T-06-E2 key validation
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs:154-208` — seed SQL pattern (ON CONFLICT DO NOTHING)
- `DeckFlow.Web/Security/SameOriginRequestValidator.cs` — CSRF validator pattern
- `DeckFlow.Web/Services/HelpContentService.cs` — Markdig pipeline + SplitHeader frontmatter pattern
- `DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs` — CreateContentSiteIndexConnection (provider-aware)
- `DeckFlow.CLI/Program.cs:59-234` — CLI verb registration pattern (distillCommand, content-source-add)
- `.gitignore lines 4-5` — `artifacts/` and `content-kb/` both gitignored currently
- `artifacts/content-kb/edhrecast/zkAmYkIOx98.md` — confirmed artifact format matches ContentArtifactSpec

### Secondary (MEDIUM confidence)
- `22-CONTEXT.md` — locked decisions D-01..D-05; canonical references
- `22-UI-SPEC.md` (force-approved) — component inventory, CSS class names, copywriting contract
- `REQUIREMENTS.md KB-08, KB-09` — requirement definitions
- `ROADMAP.md Phase 22` — success criteria

### Tertiary (LOW confidence)
- [ASSUMED] Dockerfile COPY includes repo root — not verified by inspection
- [ASSUMED] `ContentRootPath` = `/app/DeckFlow.Web/` on Render — inferred from standard ASP.NET Core Docker layout

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries verified present in solution; zero new packages
- Architecture: HIGH — all seams verified in actual source files with line numbers
- Pitfalls: HIGH — pitfalls 1-3 and 5-6 derived from verified code; pitfall 4 and 7 are LOW (Dockerfile and gitignore assumptions)
- SQL patterns: HIGH — is_visible-preserving upsert verified against existing UpsertSql; ALTER TABLE guard verified from CategoryKnowledgeRepository

**Research date:** 2026-06-01
**Valid until:** 2026-07-01 (stable stack; main risk is Dockerfile inspection)
