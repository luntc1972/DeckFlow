# Phase 65: Prod Content Artifact Reconcile — Research

**Researched:** 2026-06-22
**Domain:** Prod content consistency — content_site_index vs /data artifact files
**Confidence:** HIGH

---

## Summary

Phase 65 is not a code-delivery phase. It is an investigation + operator-executed reconcile that
closes a data-integrity hole discovered during Phase 60 Pull-from-Prod: prod `content_site_index`
has ~109 rows, but Render `/data/content-kb` holds artifacts for only 3 creators (~23 rows). The
remaining ~86 rows reference `.md` files that are absent from the prod disk.

The critical code question — **does the live site serve content-KB body from `/data` `.md` files
or from a DB content column?** — is fully answerable from the source: the serving path reads
`System.IO.File.ReadAllTextAsync` against the resolved artifact path. There is no body column in
`content_site_index`. The DB stores only metadata, tags, and the relative `artifact_path`. Missing
artifact → browser sees metadata-only "artifact unavailable" detail page.

**Severity is gated on visibility state.** A row with `is_visible = FALSE` is never shown on the
public browse page (`/content-kb`) and the detail route returns 404. Only rows with `is_visible =
TRUE` produce a user-visible broken experience. The reconcile plan must therefore count
**published orphans** (visible rows whose artifact is absent) before choosing a remediation path.

**Primary recommendation:** Run the prod probe (SQL + SFTP listing) first to count published
orphans. If published orphans > 0, re-upload their artifacts via Studio DirectPush (already-built
mechanism). If published orphans = 0, reconcile by unpublishing the orphan rows or formally
downgrading to cosmetic with a recorded note. In either case, build a small read-only CLI
`content-kb-check` command to make SC3 repeatable.

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DATA-01 | Determine and document whether the live site serves content-KB body from `/data` `.md` files or from the DB content column. | CONFIRMED: serving path uses `File.ReadAllTextAsync`. See `## DATA-01 Answer` below. |
| DATA-02 | Reconcile prod `content_site_index` + backing artifacts — every row either has its `.md` artifact on `/data`, or is reconciled down / formally downgraded to cosmetic with the decision recorded. | Three reconcile paths documented with decision criteria and operational steps. |
</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Content KB body serving | Frontend Server (Kestrel/ASP.NET MVC) reads from local filesystem (`/data`) | — | `ContentKbController.Detail` calls `File.ReadAllTextAsync(resolved)` at request time. No CDN layer; artifacts are served direct from Render disk mount. |
| Artifact storage | Database (`content_site_index`) holds metadata + relative path only | Render `/data` disk holds `.md` artifact files | Two-part: DB is the index; filesystem is the body store. |
| Prod read-only probing | Render MCP tool `mcp__render__query_render_postgres` (AI) + SFTP via `ISshArtifactDownloader` (operator) | — | AI reads DB; operator uses Studio or SFTP CLI to inspect filesystem. |
| Reconcile write paths | Operator-run only (Studio DirectPush for artifact upload; admin UI for visibility) | — | AI never writes prod. |
| Post-reconcile check tool | `DeckFlow.CLI` (new `content-kb-check` command) | — | Repeatable consistency report joins index rows to local artifact files. |

---

## DATA-01 Answer: Confirmed — Served from `/data` `.md` Files

**Confidence:** HIGH [VERIFIED: codebase grep + file read]

The live site serves content-KB body by reading the `.md` artifact file from disk at request
time. There is **no `content` or `body` column** in `content_site_index`. The schema stores only:
`artifact_path` (TEXT, relative), metadata columns, tag arrays, and visibility/admin flags.

**Evidence chain:**

1. `ContentKbController.Detail` (`DeckFlow.Web/Controllers/ContentKbController.cs:115-124`):
   ```csharp
   if (!System.IO.File.Exists(resolved))
   {
       _logger.LogWarning(...);
       return View("Detail", BuildDetailModel(row, ..., artifactUnavailable: true));
   }
   var raw = await System.IO.File.ReadAllTextAsync(resolved, cancellationToken);
   var (_, body) = ContentArtifactParser.SplitHeader(raw);
   ```
   — The controller checks `File.Exists`, reads `File.ReadAllTextAsync`, parses body. No DB
   content column is consulted. If the file is absent the action returns `artifactUnavailable: true`.

2. `ContentKbArtifactPathResolver.ResolveArtifactFullPath` (`DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs:51-56`):
   ```csharp
   return Path.GetFullPath(Path.Combine(ContentBase, artifactPath));
   ```
   `ContentBase` resolves through `ContentKb:ContentBase` config key, falling back to
   `ContentRootPath` and one parent up. On Render, `MTG_DATA_DIR=/data` and the app's content
   root is the app directory, so the resolver walks: (a) configured `ContentKb:ContentBase`, (b)
   ContentRootPath, (c) parent of ContentRootPath, (d) CWD — and takes the first that has a
   `content-kb/` subdirectory. On Render `/data` does not match any of those candidates unless
   `ContentKb:ContentBase=/data` is explicitly set in env — **the resolver silently falls back to
   ContentRootPath if `/data/content-kb/` doesn't exist**, but the upload puts files at
   `/data/content-kb/*` and `RemoteArtifactRoot` in the Studio SCP config is `/data` (matches
   `MTG_DATA_DIR`). The `render.yaml` confirms `MTG_DATA_DIR=/data`.

   **ASSUMPTION NOTE:** The exact environment variable `ContentKb:ContentBase` is not documented
   in render.yaml or appsettings.json. The resolver falls back to checking `ContentRootPath`,
   `ContentRootPath/..`, and CWD for a `content-kb/` directory. The app's `/data` disk holds
   the `content-kb/` tree. To confirm the resolver finds `/data`, the operator can verify
   `Content KB content base resolved to` in the Serilog startup log on Render.

3. `ContentSiteIndexStore` DDL (both Postgres and SQLite variants, `ContentSiteIndexStore.cs:931-975`):
   neither `PostgresCreateTableSql` nor `SqliteCreateTableSql` define a `content`, `body`, or
   `full_text` column. The only text columns are `source`, `title`, `video_url`, `artifact_path`,
   tag arrays, and status fields. **A DB content column does not exist and cannot be a serving
   source.** [VERIFIED: codebase read]

**DATA-01 conclusion:** The live site reads from `/data/content-kb/{source-slug}/{video-id}.md`.
A missing file produces a metadata-only detail page with `ArtifactUnavailable: true` rendered.
The public browse page (`/content-kb`, `GetPublishedRowsAsync`) returns rows regardless of
artifact presence — it lists all `is_visible = TRUE` rows; artifact absence only surfaces on
the individual detail page.

---

## Standard Stack

This phase uses no new packages. The relevant existing stack:

| Component | Purpose | Relevance |
|-----------|---------|-----------|
| `DeckFlow.Core.Content.ContentSiteIndexStore` | Postgres + SQLite read/write for the index | Prod-probe SQL must match its column set |
| `DeckFlow.Studio.Services.SftpArtifactUploader` | SFTP artifact upload to Render `/data` | Re-upload path (Option A reconcile) |
| `DeckFlow.Studio.Services.SftpArtifactDownloader` | SFTP artifact download from Render `/data` | Listing / inspection of prod disk contents |
| `DeckFlow.Studio.Pages.DirectPush.razor` | Operator UI: diff + SCP upload + DB upsert | Existing tool for artifact re-upload |
| Render MCP `mcp__render__query_render_postgres` | Read-only prod Postgres query (AI side) | Used for the prod probe SQL |
| `DeckFlow.CLI` | System.CommandLine host | Home for the new `content-kb-check` command |

**Installation:** none — no new packages. `DeckFlow.CLI` already exists.

---

## Package Legitimacy Audit

No new packages in this phase. Existing packages (`Dapper`, `Npgsql`, `SSH.NET`) were vetted in
prior phases. No audit required.

---

## How Artifacts Reach Prod `/data` (Upload Mechanism)

**Confidence:** HIGH [VERIFIED: codebase read — DirectPush.razor + SftpArtifactUploader.cs]

The Studio DirectPush 3-stage flow is the only path that puts artifact files on Render `/data`:

**Stage 1 — Compute Prod Diff** (`DirectPush.razor:486-580`):
  - Reads local `GetApprovedRowsAsync()` from Studio's SQLite `content-kb.db`.
  - Reads prod `GetAllRowsAsync()` via `ProdStoreFactory` (Npgsql, on-demand).
  - Diffs by natural key (YouTube video id or RSS GUID) to find new vs updated rows.

**Stage 2 — SCP Upload** (`DirectPush.razor:582-660`):
  - Builds an `SshUploadRequest` per approved row: `LocalPath = Path.Combine(_dataRoot, row.ArtifactPath)`, `RemoteRelativePath = row.ArtifactPath`.
  - `_dataRoot` = `Path.GetDirectoryName(Options.ArtifactRoot)` where `ArtifactRoot = {studioDataDirectory}/content-kb` → so `_dataRoot = {studioDataDirectory}`.
  - `row.ArtifactPath` = `content-kb/{source-slug}/{video-id}.md` (relative, no leading slash).
  - `SftpArtifactUploader.UploadArtifactsAsync` joins `RemoteArtifactRoot + "/" + ArtifactPath` → uploads to `/data/content-kb/{source-slug}/{video-id}.md`.
  - **The DB step (Stage 3) is gated on every SCP upload succeeding** — `_scpSuccess = results.All(r => r.Success)`.

**Stage 3 — DB Upsert + Visibility** (`DirectPush.razor:662-750`):
  - Calls `prodStore.UpsertContentColumnsOnlyAsync(row)` for each approved row. This updates only content/nav columns on existing rows; never touches `is_visible`, `is_hidden`, `is_evergreen`, `approval_status` on existing rows.
  - After all upserts succeed: `prodStore.StampPushedToProdAsync(keys, pushedUtc)` then `prodStore.SetVisibilityAsync(keys, true)` → marks rows `is_visible = TRUE` in prod.

**Key insight on the orphan gap:** `UpsertContentColumnsOnlyAsync` can create DB rows without a
prior Stage 2 SCP upload. If someone ran only Stage 3 (or used a different path to upsert rows)
without a corresponding SCP upload, DB rows exist with no artifact on disk. This matches the
backlog note's likely cause (c): rows upserted via DB operations (seed load, prior Stage 3
failures mid-batch, or a manual seed-based deploy) without a corresponding SFTP upload of the
artifact files.

---

## Prod Probe Specification (READ-ONLY)

The operator (or AI via Render MCP) runs these in sequence to count and identify orphans.

### Step 1 — Count and enumerate prod rows by visibility

**Tool:** `mcp__render__query_render_postgres` with `postgresId: "dpg-d7oj8iugvqtc73fso0g0-a"`

**Query A — Summary by source and visibility:**
```sql
SELECT source,
       COUNT(*)                                        AS total_rows,
       SUM(CASE WHEN is_visible THEN 1 ELSE 0 END)   AS visible_rows,
       SUM(CASE WHEN NOT is_visible THEN 1 ELSE 0 END) AS hidden_rows
  FROM content_site_index
 GROUP BY source
 ORDER BY source;
```
Output tells us which creators have rows and whether they are published.

**Query B — Counts by visibility tier:**
```sql
SELECT is_visible, is_hidden, approval_status, COUNT(*) AS n
  FROM content_site_index
 GROUP BY is_visible, is_hidden, approval_status
 ORDER BY is_visible DESC, is_hidden, approval_status;
```
Output tells us how many rows are published (`is_visible=TRUE`), hidden (`is_hidden=TRUE`),
or unpublished (both FALSE).

**Query C — Full row list for artifact path review:**
```sql
SELECT id, source, title, artifact_path,
       is_visible, is_hidden, approval_status,
       pushed_to_prod_utc
  FROM content_site_index
 ORDER BY source, title;
```
Output = complete list; operator matches each `artifact_path` to what SFTP listing shows.

### Step 2 — List artifact files on Render `/data/content-kb`

**Tool:** Studio Pull-from-Prod page (existing, Phase 60) **OR** the operator SSHes to Render
and runs `find /data/content-kb -name "*.md" | sort` (or equivalent SFTP listing).

Expected layout: `/data/content-kb/{source-slug}/{video-id}.md`

The operator builds a set of artifact paths from the SFTP listing. Any row from Query C whose
`artifact_path` is NOT in that set is an orphan.

### Step 3 — Identify published orphans

Cross-join Step 1 (DB rows, `is_visible=TRUE`) with Step 2 (SFTP file listing). A row is a
**published orphan** if `is_visible = TRUE AND artifact_path NOT IN SFTP listing`.

The count of published orphans is the severity gate that drives the reconcile path.

---

## Three Reconcile Paths

### Option A — Re-upload missing artifacts (restore completeness)

**What:** Run Studio DirectPush Stage 2 for the 86 orphan rows: upload their `.md` files from
the operator's local `content-kb.db` Studio directory to Render `/data`.

**Requires:**
1. Operator has the artifact `.md` files locally (they were distilled and approved in Studio).
2. The local Studio `approval_status = 'approved'` and the rows are present in local `content-kb.db`.
3. Operator runs DirectPush; Stage 2 uploads artifacts; Stage 3 is a no-op for already-known
   rows (`UpsertContentColumnsOnlyAsync` is idempotent).
4. For rows that are currently `is_visible = FALSE` in prod, the operator runs the admin console
   to set `is_visible = TRUE` after verifying the content.

**Tradeoff:** Best outcome (no information loss). Requires operator to have matching local
artifacts. If local Studio was also reset (lost `content-kb.db` or local artifact files), this
path is not available without re-distilling from source.

**Decision criterion:** **Choose Option A if** published orphan count > 0 AND operator confirms
local `.md` artifact files exist for those rows.

### Option B — Reconcile rows down (unpublish or delete orphans)

**What:** For each orphan row, either:
  - Unpublish: set `is_visible = FALSE` via admin console `/Admin/ContentKb` (row stays in DB, no
    longer shown on public browse, detail page returns 404).
  - Delete: admin console deletes the row entirely (cleans up DB, no artifact upload needed).

**Requires:**
1. Operator decides row-by-row which orphans to keep (for future re-upload) vs delete.
2. No artifact upload needed — just admin actions on existing rows.
3. If the source creator's content is meant to be on the public site, this is a temporary measure
   pending a future re-upload (Option A deferred).

**Tradeoff:** No information loss for unpublished rows; immediate fix for "no published orphans"
criterion; can be done entirely from the admin console. Rows are effectively staging orphans
until artifacts are uploaded.

**Decision criterion:** **Choose Option B (unpublish) if** local artifacts are unavailable for
re-upload AND the operator wants to preserve the DB rows for future re-distill. **Choose Option B
(delete) if** the orphan rows represent content that was never successfully distilled or is
not wanted on the public site.

### Option C — Formally downgrade to cosmetic (accept the gap)

**What:** Document that the 86 rows are not currently visible (`is_visible = FALSE` already for
most orphans per Phase 60 findings — the operator confirmed the Pull-from-Prod page reported
"not downloaded" for those rows, implying they were not published). Record the state in a
decision note. No action on the DB rows; no artifact upload.

**Requires:**
1. Operator probes Step 1 Query B and confirms the orphan rows are NOT `is_visible = TRUE`.
2. If published orphans = 0, the live user experience is not broken — no one sees the missing
   files.
3. A formal note records that the 86 rows are "index-only, no artifact yet" and that they will
   be resolved when the operator re-distills and re-publishes content for those creators.

**Tradeoff:** Zero operational effort; not a fix. Acceptable ONLY if published orphan count = 0.
Sets up DATA-02 as "formally downgraded to cosmetic" with the record.

**Decision criterion:** **Choose Option C if** published orphan count = 0 AND operator confirms
they are comfortable with the current state being cosmetic-only (index rows that are not visible
to end users).

### Decision tree

```
Run prod probe (SQL + SFTP listing)
        │
        ├─ published orphan count > 0?
        │       ├─ YES: local .md artifacts available? → YES → Option A (re-upload)
        │       │                                      → NO  → Option B (unpublish/delete published orphans THEN reclassify rest)
        │       └─ NO (all orphans are hidden/unpublished):
        │               ├─ want to restore content eventually? → YES → Option B (unpublish, note for future re-upload)
        │               └─ content not needed? → Option B (delete) or Option C (formally accept cosmetic)
        │
        └─ SC3: post-reconcile check via `content-kb-check` CLI or manual SQL confirms 0 unexplained orphans
```

---

## Consistency-Check Tool Recommendation

**Build it.** The recommended approach is a new CLI command `content-kb-check` added to
`DeckFlow.CLI`. This makes SC3 ("post-reconcile check confirms no remaining unexplained orphans")
repeatable and documents the expected state going forward. This is read-only code that Claude
will author.

**Why a CLI command (not Studio page or one-off script):**
- `DeckFlow.CLI` already exists and has all the relevant types (`ContentSiteIndexStore`,
  `ContentKbArtifactPathResolver`, `ContentKbOrchestratorOptions`).
- A CLI command is faster to write than a Studio page and fits the "operator runs locally"
  model.
- The check runs against the LOCAL `content-kb.db` and LOCAL artifact files, not prod. The
  "prod" dimension is handled by running Studio Pull-from-Prod first (pulls prod DB + artifacts
  locally), then running `content-kb-check` against that pulled local state.
- Alternatively: the check runs against the prod DB via `ProdContentReader` (Npgsql read-only)
  and compares against a local SFTP listing — but this requires the operator's SCP config. The
  simpler path is: pull locally, check locally.

**Proposed command signature:**
```bash
dotnet run --project DeckFlow.CLI -- content-kb-check \
  --db /path/to/content-kb.db \
  --artifact-root /path/to/content-kb
```

**Output:**
```
content-kb-check: Scanning 109 rows against /path/to/content-kb
  OK    salubrioussnail/abc123.md (visible)
  MISSING  commander-baumi/xyz789.md (NOT visible, approval=pending)
  ...

Summary:
  Total rows: 109
  Rows with artifact: 23
  Missing artifacts: 86
    - Published (is_visible=TRUE, missing artifact): 0    <- severity gate
    - Unpublished (is_visible=FALSE, missing artifact): 86

EXIT CODE: 0 if published-missing == 0; 1 otherwise.
```

**Where it lives:** `DeckFlow.CLI/ContentKbCommandRunners.cs` (new handler) and
`DeckFlow.CLI/Program.cs` (new `content-kb-check` command registration). This follows the exact
pattern of every existing CLI command.

---

## Architecture Patterns

### Pattern 1: CLI command registration (existing pattern to follow)

```csharp
// Program.cs — register new command (Source: DeckFlow.CLI/Program.cs)
var contentKbCheckCommand = new Command("content-kb-check",
    "Check content_site_index rows against local artifact files and report orphans.");
var checkDbOption = new Option<FileInfo>("--db", "Path to the local content-kb.db") { IsRequired = true };
var checkArtifactRootOption = new Option<DirectoryInfo>("--artifact-root",
    "Root directory containing content-kb/{slug}/*.md artifacts") { IsRequired = true };
contentKbCheckCommand.AddOption(checkDbOption);
contentKbCheckCommand.AddOption(checkArtifactRootOption);
contentKbCheckCommand.SetHandler(async (db, root) =>
    await runners.ContentKbCheckAsync(db, root), checkDbOption, checkArtifactRootOption);
rootCommand.Add(contentKbCheckCommand);
```

### Pattern 2: Read-only store access (existing pattern)

```csharp
// ContentKbCommandRunners.cs — new ContentKbCheckAsync method
public async Task ContentKbCheckAsync(FileInfo db, DirectoryInfo artifactRoot)
{
    var store = new ContentSiteIndexStore(db.FullName);
    var rows = await store.GetAllRowsAsync().ConfigureAwait(false);
    var resolver = new ContentKbArtifactPathResolver(...); // or just: Path.Combine(artifactRoot.FullName, row.ArtifactPath)

    var missing = rows
        .Where(row => !File.Exists(Path.GetFullPath(Path.Combine(artifactRoot.FullName, row.ArtifactPath))))
        .ToList();

    var publishedMissing = missing.Where(r => r.IsVisible).ToList();
    var hiddenMissing = missing.Where(r => !r.IsVisible).ToList();

    foreach (var row in rows)
    {
        var path = Path.GetFullPath(Path.Combine(artifactRoot.FullName, row.ArtifactPath));
        var exists = File.Exists(path);
        Console.WriteLine($"  {(exists ? "OK     " : "MISSING")}  {row.ArtifactPath} ({(row.IsVisible ? "visible" : "not visible")}, approval={row.ApprovalStatus})");
    }

    Console.WriteLine($"\nTotal rows: {rows.Count}");
    Console.WriteLine($"Rows with artifact: {rows.Count - missing.Count}");
    Console.WriteLine($"Missing artifacts: {missing.Count}");
    Console.WriteLine($"  Published (missing): {publishedMissing.Count}");
    Console.WriteLine($"  Unpublished (missing): {hiddenMissing.Count}");

    Environment.ExitCode = publishedMissing.Count > 0 ? 1 : 0;
}
```

### Anti-Patterns to Avoid

- **Do not call `EnsureSchemaAsync` against prod.** `ProdContentReader.ReadAllAsync` is the
  correct prod-read pattern — it runs a plain SELECT with no DDL. The `ContentSiteIndexStore`
  constructor used directly against prod would run `CREATE TABLE IF NOT EXISTS` on the first
  store operation.
- **Do not surface connection strings or exception messages in output.** All prod-accessing
  code already has `D-07` guards in place.
- **Do not use `is_visible` alone to determine "published" state.** The full semantic is
  `is_visible = TRUE AND is_hidden = FALSE`. The existing `GetPublishedRowsAsync` query
  already uses `WHERE is_visible = @visible` (where `visible = true`); `is_hidden` is implied
  cleared when `is_visible` is set (see `SetVisibilityAsync` SQL).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead |
|---------|-------------|-------------|
| Artifact SFTP upload | Custom SCP client | `ISshArtifactUploader` / `SftpArtifactUploader` (already built, Phase 47) |
| Prod DB read | Raw Npgsql | `ProdContentReader.ReadAllAsync` (Phase 60, read-only, no EnsureSchema) |
| Orphan re-upload UI | Custom admin page | Studio DirectPush (existing, operator-run) |
| Admin visibility toggle | Direct SQL | Admin console `/Admin/ContentKb` visibility controls (existing) |

---

## Common Pitfalls

### Pitfall 1: Confusing `content.kb.enabled` flag with artifact availability

**What goes wrong:** `content.kb.enabled` controls whether the `/content-kb` route is reachable
at all (feature flag gate on the controller). It is `TRUE` in the prod DB seed by default
(`FeatureFlagStore.cs:156`). A missing artifact does NOT make the flag false. The gate is
independent of per-row artifact presence.

**Why it happens:** The flag hides the whole KB; artifact absence hides only individual detail
pages. Two separate mechanisms.

**How to avoid:** Check flag state separately from artifact state. For this phase, assume the
flag may be enabled and the route is live.

### Pitfall 2: Treating all 86 orphans as published

**What goes wrong:** Assuming all 86 orphan rows are `is_visible = TRUE` and therefore breaking
user-facing pages.

**Why it happens:** Phase 60 found the SFTP listing was missing files, but did not distinguish
visibility state.

**How to avoid:** Run Query B from the prod probe to count `is_visible = TRUE` orphans before
choosing a remediation path. If the count is 0, the live site is not serving broken pages.

### Pitfall 3: Re-uploading from prod → local then treating it as authoritative

**What goes wrong:** Studio Pull-from-Prod downloads artifacts from prod `/data`. If an artifact
is missing from prod, Pull-from-Prod marks it "not downloaded" and leaves the local row with no
artifact. Then `content-kb-check` against the local DB + pulled files would correctly report
missing. But if the operator then runs DirectPush to "re-upload", they push nothing (the local
`.md` is also absent).

**How to avoid:** The re-upload source is the operator's original distill output (a prior Studio
session's `content-kb.db` + `content-kb/` artifact tree). Pull-from-Prod is for mirroring prod
state; it cannot reconstitute missing artifacts.

### Pitfall 4: ContentKbArtifactPathResolver candidate walk

**What goes wrong:** The path resolver (`ContentKbArtifactPathResolver.cs:58-85`) walks
candidates in order: configured `ContentKb:ContentBase`, `ContentRootPath`, one level up, CWD.
On Render, if `ContentKb:ContentBase` is not set and `/data/content-kb/` exists, the resolver
hits (b) or (c) only if the content root's parent is `/data` (it is typically `/app`). If
none of the candidates contain `content-kb/`, it falls back to `ContentRootPath` and logs a
warning.

**Operational check:** On Render, search startup logs for `Content KB content base resolved to`
to confirm `/data` is the selected base.

**How to avoid:** If the resolver falls back and resolves to `/app` (not `/data`), then even
after re-uploading to `/data/content-kb/` the Detail action would try to read from the wrong
path. The fix is to set `ContentKb__ContentBase=/data` in Render env vars if not already set.

---

## Runtime State Inventory

> Include because this phase touches data layout on prod disk and prod DB — not a code rename
> but a data reconcile.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | `content_site_index` in prod Postgres: ~109 rows; `is_visible` column present (added via ALTER in prior cycles); `approval_status` present | Read-only probe SQL in this phase; writes (visibility toggle) are operator-run via admin console |
| Live service config | Render `/data` disk (`content-kb/` subtree): 3 creator directories with ~23 `.md` files; 86 artifact paths referenced by DB rows are absent | Operator: re-upload via Studio DirectPush OR unpublish/delete orphan rows |
| OS-registered state | None — Render manages the service; no Task Scheduler or cron relevant here | None |
| Secrets/env vars | `Studio:Scp:*` (SCP secrets, user-secrets on operator's machine): required for DirectPush artifact upload. `ContentKb:ContentBase` may need to be `/data` if not already set. | Operator: verify resolver log; set `ContentKb__ContentBase=/data` in Render env if needed |
| Build artifacts | None relevant to this reconcile | None |

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Render MCP `mcp__render__query_render_postgres` | Prod probe SQL (AI-side) | [ASSUMED: yes, per MEMORY.md] | — | Operator runs SQL in Render Postgres console |
| Studio with SCP secrets configured | DirectPush artifact upload (Option A) | Operator-dependent | — | Operator re-distills from source if local artifacts also missing |
| Local `content-kb.db` with approved rows | Option A re-upload source | Operator-dependent | — | Must re-harvest+distill if lost |
| `DeckFlow.CLI` build | `content-kb-check` command | ✓ (in-repo, builds as part of solution) | net10.0 | — |

---

## Validation Architecture

Nyquist validation is enabled (`workflow.nyquist_validation: true` in `.planning/config.json`).

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` and `DeckFlow.Web.Tests/` |
| Quick run command | `dotnet test --filter "FullyQualifiedName~ContentKbCheck" --no-build` |
| Full suite command | `dotnet test` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DATA-01 | Serving path uses File.ReadAllTextAsync, not DB content column | manual-only — confirmed by code inspection; no behavior change to test | n/a | n/a |
| DATA-02 | Post-reconcile: no published orphans remain | `content-kb-check` CLI exit-code 0 means no published orphans | `dotnet run --project DeckFlow.CLI -- content-kb-check --db <path> --artifact-root <path>` | ❌ Wave 0 |

**Note:** DATA-01 is investigation/documentation only — no code change, no test needed. DATA-02
post-reconcile verification is operator-run (`content-kb-check` CLI). Unit tests for the new
command's logic (orphan detection) can be added to `DeckFlow.Core.Tests` if the scanner is
extracted as a pure function, but the phase gate is the operator successfully running the command
against the post-reconcile prod-pulled local state.

### Sampling Rate

- **Per task commit:** `dotnet build --no-incremental 2>&1 | tail -3` (format gate; no logic change until `content-kb-check` is added)
- **Per wave merge:** `dotnet test --no-build`
- **Phase gate:** `content-kb-check` exits 0 (no published orphans) before `/gsd:verify-work 65`

### Wave 0 Gaps

- [ ] `DeckFlow.CLI/ContentKbCommandRunners.cs` — add `ContentKbCheckAsync` handler
- [ ] `DeckFlow.CLI/Program.cs` — register `content-kb-check` command
- [ ] Optional: `DeckFlow.Core.Tests` — unit tests for orphan-detection logic if extracted to a shared helper

---

## Security Domain

`security_enforcement` is not explicitly `false` in config — treat as enabled.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — |
| V3 Session Management | no | — |
| V4 Access Control | no — prod read-only; no new write paths added | — |
| V5 Input Validation | yes — `content-kb-check` reads `artifact_path` from DB before combining with a root; must path-validate | `ValidateArtifactPath` already in `ContentSiteIndexStore`; apply same guard in CLI |
| V6 Cryptography | no | — |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal via `artifact_path` DB column | Tampering | Validate no `..` segments and no rooted paths before `Path.Combine`; same logic as `ContentSiteIndexStore.ValidateArtifactPath` |
| Logging sensitive values (connection string, SCP host) | Info Disclosure | Follow D-07 pattern: never log `ex.Message` from Npgsql/SSH exceptions; use sanitized literals only |

---

## Open Questions

1. **Is `ContentKb:ContentBase` set on Render?**
   - What we know: `render.yaml` sets `MTG_DATA_DIR=/data` but does not set `ContentKb:ContentBase`.
   - What's unclear: Whether the resolver's fallback successfully finds `/data/content-kb/`
     via the parent-of-ContentRootPath walk, or whether it silently falls back to the app
     directory.
   - Recommendation: Operator checks startup logs for `Content KB content base resolved to`.
     If not `/data`, add `ContentKb__ContentBase=/data` to Render env vars.

2. **How many of the 86 orphan rows are `is_visible = TRUE`?**
   - What we know: Phase 60 found the SFTP listing was missing the files; no visibility
     breakdown was recorded.
   - What's unclear: Whether any of the 86 are published (would cause broken user-facing pages).
   - Recommendation: Run prod probe Query B immediately; this decides which reconcile path is
     appropriate.

3. **Does the operator's local Studio instance have the `.md` files for the 86 orphan creators?**
   - What we know: The 3 publishers (salubrioussnail, the-command-zone, based-deck-department)
     have artifacts. The other creators may have been seeded from the JSON seed file
     (`index-seed.json`) without ever generating local artifacts.
   - What's unclear: Whether those 86 rows came from a seed load (content-only upsert, no
     artifact) or from a DirectPush that failed to upload files.
   - Recommendation: Operator checks local Studio `content-kb/` directory for creator subdirs
     beyond the 3 known. If none: those rows came from a seed load; Option B (unpublish/delete)
     is the practical path.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `ContentKb:ContentBase` is not set in Render env vars; resolver uses fallback walk | DATA-01 Answer, Pitfall 4 | If it IS set but wrong, artifacts would be served from wrong path even after re-upload |
| A2 | Render MCP `mcp__render__query_render_postgres` is available (per MEMORY.md) | Environment Availability | Operator must run SQL via Render Postgres console instead |
| A3 | The 86 orphan rows are predominantly `is_visible = FALSE` (based on Phase 60 behavior: "Pull-from-Prod reported not downloaded for all 86") | Open Questions | If some are visible, SC3 gates on Option A re-upload before the phase can close |

---

## Sources

### Primary (HIGH confidence)
- `DeckFlow.Web/Controllers/ContentKbController.cs` — verified serving path (lines 95-124)
- `DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs` — resolver candidate walk
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — DDL (no body column), upsert SQL variants
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` / `ContentSiteIndexRow` — artifact path format
- `DeckFlow.Core/Knowledge/ContentArtifactWriter.cs` — `content-kb/{slug}/{id}.md` path convention
- `DeckFlow.Studio/Pages/DirectPush.razor` — 3-stage upload mechanism (lines 486-750)
- `DeckFlow.Studio/Services/SftpArtifactUploader.cs` — SCP upload details
- `DeckFlow.Studio/Services/SftpArtifactDownloader.cs` — SCP download (for Pull-from-Prod)
- `DeckFlow.Studio/Services/ProdContentReader.cs` — read-only prod Postgres pattern
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — `content.kb.enabled` default = TRUE
- `render.yaml` — `MTG_DATA_DIR=/data`, Render disk config
- `.planning/REQUIREMENTS.md` — DATA-01/DATA-02 requirement text
- `.planning/ROADMAP.md` — Phase 65 success criteria, backlog note on artifact gap

### Secondary (MEDIUM confidence)
- `.planning/STATE.md` — current cycle state, Phase 60 outcome (Pull-from-Prod found 86 missing)

---

## Metadata

**Confidence breakdown:**
- DATA-01 answer: HIGH — code-verified, no DB body column, File.ReadAllTextAsync confirmed
- Reconcile paths: HIGH — based on existing DirectPush mechanism and admin console operations
- Published orphan count: [ASSUMED] — must be confirmed by prod probe; we know 86 are missing, not how many are visible
- Post-reconcile check tool design: HIGH — follows existing CLI command pattern exactly

**Research date:** 2026-06-22
**Valid until:** 2026-07-22 (stable — no planned changes to the content KB serving path)

---

## RESEARCH COMPLETE

**Phase:** 65 — Prod Content Artifact Reconcile
**Confidence:** HIGH (code-verified domains) / ASSUMED (prod visibility count)

### Key Findings

1. **DATA-01 is definitively answered:** Content KB body is served from `/data` `.md` files via
   `System.IO.File.ReadAllTextAsync`. There is no DB body column. A missing file renders
   `ArtifactUnavailable: true` (metadata-only detail page) or 404 if `is_visible = FALSE`.

2. **Schema confirmed:** `content_site_index` has 17 columns (id, source, title, video_url,
   artifact_path, published_utc, pushed_to_prod_utc, indexed_utc, three tag arrays, natural_key
   pair, is_visible, is_hidden, is_evergreen, approval_status). No `content` or `body` column
   exists in either the Postgres or SQLite DDL.

3. **Upload mechanism verified:** Artifacts reach prod only through Studio DirectPush Stage 2
   (`SftpArtifactUploader`). DB upsert (`UpsertContentColumnsOnlyAsync`) can run without a
   prior SCP upload — this is the likely cause of the 86 orphan rows.

4. **Severity is gated on prod probe:** The number of `is_visible = TRUE` orphan rows determines
   which reconcile path applies. Run prod probe Query B first.

5. **`content-kb-check` CLI command is the right tool for SC3:** 15-20 lines of new code in
   `DeckFlow.CLI` makes the post-reconcile check repeatable. Claude authors this code.

### File Created

`/mnt/c/users/chrislunt/source/personal/deckflow-cycle11/.planning/phases/65-prod-content-artifact-reconcile/65-RESEARCH.md`

### Confidence Assessment

| Area | Level | Reason |
|------|-------|--------|
| DATA-01 (serving path) | HIGH | Code-verified: no DB column, File.ReadAllTextAsync confirmed |
| Schema structure | HIGH | DDL read directly from ContentSiteIndexStore.cs |
| Upload mechanism | HIGH | DirectPush.razor + SftpArtifactUploader.cs read directly |
| Published orphan count | [ASSUMED] | Requires prod probe; not yet run |
| Reconcile option viability | HIGH | Based on existing DirectPush + admin console capabilities |
| CLI check tool design | HIGH | Follows existing CLI command pattern exactly |

### Open Questions

- How many of the 86 orphan rows are `is_visible = TRUE`? (run prod probe Query B)
- Is `ContentKb:ContentBase` set in Render env? (check startup log for resolver path)
- Does operator have local `.md` files for the 86 orphan creators? (check local Studio `content-kb/` dir)

### Ready for Planning

Research complete. Planner can now create PLAN.md covering:
- Plan 65-01: Prod probe (operator-run SQL + SFTP listing) + DATA-01 decision document
- Plan 65-02: Reconcile execution (operator-run, path chosen based on probe results)
- Plan 65-03: `content-kb-check` CLI command (Claude-coded) + post-reconcile SC3 verification
