# Phase 90: DirectPush Correctness + Seed Sync - Research

**Researched:** 2026-07-07
**Domain:** ASP.NET Core MVC (web serving) + Blazor Server (Studio orchestration) + Postgres/SQLite dual-dialect persistence — Content-KB prod↔git↔Studio sync
**Confidence:** HIGH on current-code-reality claims (all cited file:line, read directly); MEDIUM-LOW on the *mechanism* for the new SYNC-09 deploy-confirm step, which has no existing precedent in the codebase (flagged as an Open Question)

## Summary

This phase converges DirectPush onto the same end-state Publish already produces. The codebase
reality is more specific than the roadmap prose: DirectPush today is a **4-stage Blazor page**
(`DirectPush.razor.cs`) whose Stage 3 (`WriteRowsAsync` → `DirectPushCoordinator.WritePublishAsync`)
stamps `pushed_to_prod_utc` and flips `is_visible=true` on **prod first, then local** — entirely
inside one method, and entirely **before** Stage 4 (`CommitAndPushBodiesAsync`) has committed or
pushed anything to git. Stage 4 is optional/best-effort in the UI today (a git failure is
non-fatal because "content is already live"). This is the exact inversion SYNC-09/D-06 must fix:
expand (git commit → deploy → hash-verify) must happen **before** contract (stamp + visible), not
after. The current implementation is the textbook contract-before-expand anti-pattern.

`DirectPushCoordinator.CommitAndPushBodiesAsync` (`DirectPushCoordinator.cs:252-382`) also
deliberately and explicitly never stages the seed file (`content-kb/seed/index-seed.json`) — this
is SYNC-08's gap, and it is trivially close-able because `PublishCoordinator` already shows the
exact pattern to copy: it calls `_orchestrator.ExportIndexToFileAsync(seedAbsPath, ...)`
(`PublishCoordinator.cs:97`, `ContentKbOrchestrator.cs:748-793`) which both hosts already share
through `ContentIndexExportRow.From()` (Phase 89 already added `BodySha256` to this factory).
DirectPush just needs to call the same method and add the seed's repo-relative path to its staged
list before `StageAndCommitAsync`.

SYNC-07's serving flip is a smaller, more surgical change than the roadmap prose implies: the
resolver (`ContentKbArtifactPathResolver.TryResolveExistingArtifact`,
`ContentKbArtifactPathResolver.cs:92-131`) already tries **git first**, falling back to the
`/data` overlay (gated on `MTG_DATA_DIR`) only when the git-tree file is missing. "Dropping the
`/data`-SFTP-first overlay" is really "delete the fallback branch under the flag" — a ~15-line
change, not an architecture rewrite. The genuinely hard part of this phase is SYNC-09: it requires
Studio to *prove* the live deployed `/app` body matches, and **no code path today gives Studio any
HTTP visibility into the deployed web app** — Studio only ever talks to prod via raw Postgres
(`Studio:ProdConnectionString`), SSH/SCP, and git. This is a real gap the planner must resolve, not
paper over; see Open Questions.

**Primary recommendation:** Split the phase exactly as Codex/CONTEXT.md direct — Plan A (SYNC-07
serving flip, self-contained, web-only) can ship independently. Plan B (SYNC-08 seed re-export +
SYNC-09 ordering + SYNC-10 stamp timing) is one coherent re-plumb of `DirectPushCoordinator` +
`DirectPush.razor.cs`'s 4-stage flow into (at minimum) a 5th stage, and it has a hard, currently
un-designed dependency: an operator-triggered "verify deployed body" mechanism that does not exist
in the codebase today and must be designed as part of this phase's planning, not assumed.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Body serving resolution (`/app` vs `/data`) | API/Backend (`ContentKbController` + `ContentKbArtifactPathResolver`) | — | Pure web-serving concern; Studio never serves content |
| `sync.directpush-gitbody` flag storage | API/Backend (Postgres `feature_flags` table via `FeatureFlagStore`) | Studio (read-only mirror) | D-04 locked: web-DB is authoritative; Studio reads the same physical DB |
| DirectPush write orchestration (upsert/stamp/visibility) | Studio (Blazor Server, `DirectPushCoordinator`) | Database/Storage (`ContentSiteIndexStore` against prod Postgres) | Studio is the operator-driven orchestrator; the store is the write surface |
| Seed export (`index-seed.json`) | Database/Storage → Core (`ContentIndexExportRow.From`, `ContentKbOrchestrator.ExportIndexToFileAsync`) | Studio (caller) | Shared Core factory, already used by both hosts; DirectPush must call it, not fork it |
| Deploy-confirm / hash-verify at `/app` | **Undesigned — likely API/Backend (new endpoint) queried by Studio over HTTP** | Studio (poll/trigger) | No existing surface; Studio has no HTTP client into the deployed web app today (only Postgres/SSH/git) |
| Git durability (commit + push) | Studio (`DirectPushCoordinator.CommitAndPushBodiesAsync`, `IGitRepository`) | — | Unchanged mechanism; scope grows to include the seed file |

## Standard Stack

No new external packages are introduced by this phase — everything routes through existing
in-repo abstractions (`IContentSiteIndexStore`, `IGitRepository`, `IFeatureFlagStore`, Dapper,
Npgsql, RestSharp is NOT involved here). Per CLAUDE.md, do not add packages without asking first;
none are needed.

**Version verification:** N/A — no new dependency.

## Package Legitimacy Audit

Not applicable — this phase adds no new external packages (confirmed by full read of all six
canonical-reference files and the actual code under change; every new capability is composed from
existing project types: `IFeatureFlagStore`/`IFeatureFlagCache`, `ContentSiteIndexStore`,
`IGitRepository`, `ContentIndexExportRow`, `ContentSiteIndexContentSignature`). If planning
surfaces a need for an HTTP call from Studio to the web app (see Open Questions), that call should
use the existing `RestSharp` + Polly v8 convention already established for all outbound HTTP in
this codebase (per `CLAUDE.md` HTTP/Resilience Conventions) — no new HTTP package required.

## Architecture Patterns

### System Architecture Diagram

```text
Today (broken ordering — SYNC-09/M3/M6a):

Studio DirectPush.razor.cs
  Stage 1  ComputeDiffAsync        ─┐  reads local (approved) + prod (all) rows via
                                     │  IContentSiteIndexStore, classifies New/Updated/Unchanged
                                     │  (DirectPushCoordinator.ClassifyDiff, pure)
  Stage 2  UploadArtifactsAsync    ─┤  SCP bodies to prod /data overlay (ISshArtifactUploader)
  Stage 3  WriteRowsAsync          ─┤  DirectPushCoordinator.WritePublishAsync:
                                     │    prodStore.UpsertContentColumnsOnlyBatchAsync   (content, incl. body_sha256)
                                     │    prodStore.StampPushedToProdAsync   ◄── STAMPED HERE (too early)
                                     │    prodStore.SetVisibilityAsync(true) ◄── VISIBLE HERE (too early)
                                     │    localStore.Stamp + SetVisibility (mirror)
                                     │  *** row is now LIVE and "Published" — before git exists ***
  Stage 4  CommitAndPushAsync      ─┘  CommitAndPushBodiesAsync: copies ONLY pushed bodies into
                                        repo, commits with [skip render] (suppresses Render
                                        autodeploy!), pushes — SEED NEVER STAGED (explicit anti-
                                        pattern guard, DirectPushCoordinator.cs:240-251).
                                        Non-fatal on failure — "content is already live".

  Web serving (ContentKbController.Detail → ContentKbArtifactPathResolver):
    File.Exists(ContentBase/content-kb/<path>)   [git /app]      → serve if found
    else if DataOverlayBase set (MTG_DATA_DIR)   [/data overlay] → serve if found
    else 404 MissingFile

Target (this phase, under sync.directpush-gitbody = ON):

  Stage 1  ComputeDiffAsync        (unchanged)
  Stage 2  UploadArtifactsAsync    (unchanged, or removed later per SYNC-F1 — NOT this phase)
  Stage 3  WriteContentAsync       prodStore.UpsertContentColumnsOnlyBatchAsync ONLY
                                    (content incl. body_sha256; NO stamp, NO visibility)
  Stage 4  CommitAndPushAsync      copies bodies AND re-exports+stages the seed
                                    (ExportIndexToFileAsync, same factory as Publish);
                                    commit WITHOUT [skip render] so Render actually redeploys
                                    (see Open Question 1 — this contradicts the current constant's
                                    purpose and must be an explicit decision)
  Stage 5  VerifyAndPublishAsync   NEW — operator-triggered after Render shows deploy healthy;
                                    confirms deployed /app body hash == stored body_sha256 for
                                    every pushed row (via a mechanism TBD, Open Question 2), THEN:
                                      prodStore.StampPushedToProdAsync
                                      prodStore.SetVisibilityAsync(true)
                                      localStore mirror (same prod-first-then-local order, PUB-01)

  Web serving (flag ON):
    File.Exists(ContentBase/content-kb/<path>)   [git /app] → serve if found
    else → 404 MissingFile   (NO /data fallback — the flip)
```

### Recommended Project Structure

No new projects/folders. Changes land in:
```
DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs     # re-plumb WritePublishAsync; add seed export + confirm step
DeckFlow.Studio/Pages/DirectPush.razor(.cs)              # new stage(s), reworked stage gating
DeckFlow.Studio/Services/                                 # possible new IProdFlagReader / IDeployVerifier seam
DeckFlow.Web/Controllers/ContentKbController.cs           # flag-gated serving flip (inject IFeatureFlagCache)
DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs  # branch out /data fallback under flag
DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs  # register sync.directpush-gitbody description
DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs    # seed sync.directpush-gitbody = FALSE (PG + SQLite)
DeckFlow.Web/Controllers/Api/ or Admin/                    # possible NEW endpoint for deploy-confirm (Open Question 2)
```

### Pattern 1: Shared seed-export factory (SYNC-08)
**What:** `PublishCoordinator.ExportAndDiffAsync` writes the approved-only seed via
`_orchestrator.ExportIndexToFileAsync(seedAbsPath, progress, cancellationToken)`
(`PublishCoordinator.cs:94-101`), which internally calls `GetApprovedExportRowsAsync` →
`_indexStore.GetApprovedRowsAsync()` → `.Select(ContentIndexExportRow.From)`
(`ContentKbOrchestrator.cs:748-793, 909-915`). This is the SAME `IContentKbOrchestrator` instance
`DirectPushCoordinator` already holds (`_orchestrator` field, `DirectPushCoordinator.cs:30,77`) —
no new dependency needed.
**When to use:** DirectPush's git stage, immediately alongside the body copy, before staging.
**Example:**
```csharp
// Source: PublishCoordinator.cs:91-101 (pattern to replicate in DirectPushCoordinator)
var seedAbsPath = Path.GetFullPath(Path.Combine(repoRoot, SeedRelative));
var exportResult = await _orchestrator.ExportIndexToFileAsync(seedAbsPath, progress, cancellationToken)
    .ConfigureAwait(false);
// staged = [SeedRelative, ...copiedArtifactPaths] — see PublishCoordinator.cs:122-125
```
DirectPushCoordinator has no `SeedRelative` constant today — add one (mirror
`PublishCoordinator.cs:26`, same literal path `"content-kb/seed/index-seed.json"`) so both
coordinators write to the identical repo location. **Do not fork a second seed-writer** — the
whole point of Phase 89's `ContentIndexExportRow.From()` consolidation (D-08 there) is one seed
shape from one factory call site pattern.

### Pattern 2: Flag-gated serving branch (SYNC-07)
**What:** `FeatureFlagGateAttribute` (`FeatureFlagGateAttribute.cs`) is an **all-or-nothing 404
gate** applied at the action level — it cannot conditionally change *how* an action resolves a
body (git-only vs git-then-overlay). SYNC-07 needs `IFeatureFlagCache.IsEnabled("sync.directpush-gitbody")`
consulted **inside** the resolution path, not as a route gate.
**When to use:** Inject `IFeatureFlagCache` into `ContentKbArtifactPathResolver` (or check it in
`ContentKbController.Detail` before calling the resolver and short-circuit) and skip the
`DataOverlayBase` fallback block (`ContentKbArtifactPathResolver.cs:113-129`) when the flag is ON.
**Example:**
```csharp
// Source: ContentKbArtifactPathResolver.cs:92-131 (current fallback logic to gate)
if (File.Exists(gitPath)) { resolvedFullPath = gitPath; return Resolved; }
if (flagOn) return ContentKbArtifactResolution.MissingFile;   // NEW: no /data fallback under flag
if (DataOverlayBase is null) return ContentKbArtifactResolution.MissingFile;
// ...existing overlay fallback...
```
**Landmine:** `IFeatureFlagCache.IsEnabled` defaults **missing** keys to `true` (D-13 default-on,
`IFeatureFlagCache.cs:14`, `FeatureFlagCache.cs:46-56`). If `sync.directpush-gitbody` is not
explicitly seeded, the serving flip activates itself. It MUST be added to both
`FeatureFlagStore.PostgresSeedSql` and `SqliteSeedSql` (`FeatureFlagStore.cs:198-274`) as `FALSE`,
following the exact precedent of `tap-analyzer`/`wincon-map`/`mulligan-eval`/`tool.bracket.enabled`,
and to `FeatureFlagCatalog.Descriptions` (`FeatureFlagCatalog.cs:14-97`) — a `FeatureFlagCatalogTests`
guard already fails the build if a seeded key has no catalog description.

### Pattern 3: Studio→prod-DB read-only accessor (D-04)
**What:** `IProdContentReader.ReadAllAsync(connectionString, ct)` (`ProdContentReader.cs`) is the
existing house pattern for a **structurally read-only** query against prod: it builds an on-demand
Npgsql connection from `Studio:ProdConnectionString`, runs one plain `SELECT`, never calls
`EnsureSchemaAsync`. `DeckFlowDatabaseConnectionFactory.CreateFeatureFlagConnection` (`.cs:36-37`)
proves `feature_flags` shares the **same** Postgres connection string/env-vars as `content_site_index`
(`CreateContentSiteIndexConnection`, `.cs:70-71`) in production — both are `CreateConnection(...)`
calls against the single `DECKFLOW_DATABASE_CONNECTION_STRING`. This means the flag lives in the
**same physical database** Studio already connects to via `Studio:ProdConnectionString`
(`DirectPushCoordinator.cs:393`, `PullFromProdCoordinator.cs:84`).
**When to use:** Add a sibling read-only method — e.g. `IProdContentReader.ReadFlagAsync(connectionString, key, ct)`
or a new `IProdFeatureFlagReader` — that runs `SELECT enabled FROM feature_flags WHERE key = @key`
against the SAME connection string, mirroring `ProdContentReader`'s SSL-forcing, no-DDL pattern.
**Example:**
```csharp
// Source: ProdContentReader.cs:16-62 (pattern to replicate for the flag read)
var builder = new NpgsqlConnectionStringBuilder(normalized) { SslMode = SslMode.Require };
var conn = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, builder.ConnectionString);
await using var connection = await conn.OpenConnectionAsync(cancellationToken);
var enabled = await connection.QuerySingleOrDefaultAsync<bool?>(
    "SELECT enabled FROM feature_flags WHERE key = @key;", new { key });
```
Default to `false`/OFF on a missing row or connection failure (fail-safe: Studio should never
assume the flag is ON if it cannot confirm — this is the inverse default from the web-side
`IFeatureFlagCache` D-13 fail-open, and that asymmetry is intentional: the web-side default-on
protects *existing shipped features* from an empty table; the Studio-side accessor is gating a
*brand-new, riskier* behavior and should fail closed).

### Anti-Patterns to Avoid
- **Reordering only `WritePublishAsync`'s internal statement order without moving the stamp/visibility calls out of Stage 3 entirely** — the UI's stage gating (`_dbSuccess` gates Stage 4 in `DirectPush.razor.cs:346`) means Stage 4 already assumes Stage 3 completed and the row is "published"; simply reordering lines inside one method does not achieve expand-contract because Stage 4 (git) still runs strictly after in the same click-sequence, with no real deploy-verification in between.
- **Keeping `[skip render]` on the DirectPush durability commit while implementing SYNC-09** — `RenderSkipPhrase` (`DirectPushCoordinator.cs:33-37`) exists specifically to *prevent* a Render redeploy. SYNC-09's hash-gate requires a *real* redeploy to have happened. These are contradictory under the current constant; this must be an explicit decision, not an oversight (see Open Question 1).
- **Forking a second `index-seed.json` writer for DirectPush** — explicitly rejected by D-08; call `ExportIndexToFileAsync` via the shared `IContentKbOrchestrator`, exactly as Publish does.
- **Adding a `WHERE` clause on a timestamp column when querying "rows awaiting confirm"** — would reintroduce the F-51-PG-01 class (TEXT-vs-`timestamptz` Npgsql `42883`). Neither `StampPushedToProdAsync` (`ContentSiteIndexStore.cs:649-654`) nor `SetVisibilityAsync` (`.cs:767-773`) filter on any timestamp column today (both key purely on `natural_key_type`/`natural_key_value`) — keep it that way; if a new "pending confirm" query is added, dialect-guard any timestamp comparison.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Body-content hashing | A second SHA-256/normalization routine | `ContentSiteIndexContentSignature.ComputeBodySha256` (`ContentSiteIndexContentSignature.cs:131-145`) | Phase 89's D-01 contract: publish-side and render-side MUST call the identical helper so hashes are comparable; a second implementation risks a normalization mismatch (LF vs CRLF, UTF-8 decode) that would make every hash-gate check false-negative |
| Seed JSON serialization | A DirectPush-specific JSON writer | `IContentKbOrchestrator.ExportIndexToFileAsync` (`ContentKbOrchestrator.cs:748-793`) | Byte-shape (camelCase, 2-space indent, forced LF) is load-bearing for git diff noise and for `ContentKbSeedLoader` round-trip; a second writer risks drift |
| Prod read access from Studio | A new ad-hoc Npgsql connection helper | `IProdContentReader` pattern (`ProdContentReader.cs`) / `IProdStoreFactory` (`IProdStoreFactory.cs:18-34`) | Both already encode the house invariants: SSL-forced, schema-ensure OFF, ephemeral (never DI-singleton) connection — reinventing this risks accidentally issuing DDL against prod (the exact P88 violation this pattern was built to prevent) |
| Feature-flag gating | A Studio-local config flag mirroring the web flag | The read-only prod-DB accessor (Pattern 3 above) | D-04 locked: single source of truth is the web-DB flag; a duplicate Studio flag can drift from the authoritative value |

**Key insight:** every piece of this phase except the SYNC-09 deploy-confirm mechanism has a
direct, already-proven pattern to copy verbatim from Publish, Pull, or the P88/89 flag/hash work.
The deploy-confirm step is the one genuinely novel piece — treat it with proportionally more design
scrutiny in planning (see Open Questions).

## Common Pitfalls

### Pitfall 1: Flipping the serving flag exposes pre-existing unreconstructable rows
**What goes wrong:** STATE.md's live-audit blocker records **70 of 106 prod rows are NOT in the
approved git seed** and would not survive a reset today. Some fraction of these may also have
bodies that only ever reached prod via the `/data` SCP upload and were never durably committed to
git (Stage 4 has always been optional/best-effort and non-fatal on failure). The instant the
`sync.directpush-gitbody` flag flips ON, any such row's body resolution changes from
"`/data` overlay served it" to "404 MissingFile" — a live, previously-working KB detail page goes
dark.
**Why it happens:** SYNC-07 removes the exact fallback path (`ContentKbArtifactPathResolver.cs:113-129`)
that has been silently masking every historical Stage-4 git failure.
**How to avoid:** Before recommending the operator flip the flag, the planner/operator should be
able to answer "which visible rows have a body in `/app` right now" — this is naturally close to
what Phase 91's reconciler (file-orphan / published-orphan detection, out of scope here) will
formalize, but Phase 90 should NOT assume it can rely on that landing first. Consider a
plan-checker verification step in this phase's own scope: after implementing, run a check (script
or manual query) cross-referencing `is_visible=true` prod rows against `File.Exists` in the git
tree, and surface the count to the operator before recommending flag flip. Flag stays OFF (D-05)
regardless, so this is a rollout-time concern, not a code-correctness blocker for Phase 90 itself.
**Warning signs:** Any DirectPush row whose historical `is_visible=true` predates a successful
Stage-4 git push.

### Pitfall 2: `[skip render]` vs the hash-gate's dependency on a real deploy
**What goes wrong:** If the DirectPush git commit for content+seed keeps carrying
`RenderSkipPhrase` ("[skip render]" / "[render skip]" — Render's own deploy-skip convention,
`DirectPushCoordinator.cs:33-37`), Render never redeploys, the body never reaches the live `/app`
tree, and SYNC-09's hash-verify-at-`/app` step can never succeed — the row is permanently stuck
hidden (D-06: "until all three hold, the row stays hidden").
**Why it happens:** The constant's whole reason for existing (per its own comment) was "content is
already live via the web /data overlay, so the git push is durability only" — a premise SYNC-07
directly invalidates once the flag is ON.
**How to avoid:** Decide explicitly (this is Open Question 1, not a default): either (a) drop
`RenderSkipPhrase` from DirectPush's commit under the flag so pushes trigger a real Render deploy,
or (b) keep it and require the operator to manually trigger/observe a Render deploy via the
dashboard before clicking a "Verify & Publish" step. Either is workable; leaving it as-is (still
skip-render) silently breaks SYNC-09.
**Warning signs:** Rows stay hidden indefinitely after a "successful" Stage 4 commit+push.

### Pitfall 3: F-51-PG-01-class regression in any new "pending confirm" query
**What goes wrong:** Postgres raises `42883` (`operator does not exist: text = timestamp with time
zone`) when a parameterized `WHERE` compares a TEXT-typed column against a Npgsql
`DateTimeOffset`-bound parameter without an explicit cast.
**Why it happens:** Any new query the planner adds to find "rows content-upserted but not yet
stamped/visible" is tempted to filter `WHERE pushed_to_prod_utc IS NULL AND indexed_utc < @cutoff`
or similar — a fresh timestamp comparison, the exact shape that broke `AddDeckIdsAsync` in F-51-PG-01.
**How to avoid:** Prefer keying the "pending confirm" set off the **existing publish set** already
held in memory during the same operator session (the `_publishRows` list, `DirectPush.razor.cs:55`)
rather than re-querying prod by timestamp. If a re-query is unavoidable, dialect-guard exactly like
`AddDeckIdsAsync`'s `::timestamptz` cast precedent (`53-01-PLAN.md:114`, `.planning/debug/resolved/prod-harvest-42883-text-timestamptz.md`).
**Warning signs:** `Npgsql.PostgresException: 42883` in prod logs; SQLite tests pass while
`DECKFLOW_POSTGRES_TESTS=1` fails.

### Pitfall 4: Stuck-forever rows with no operator-visible "awaiting confirm" state
**What goes wrong:** Once content is upserted to prod (new Stage 3) but before the confirm step
runs (new Stage 5), a row is in a state indistinguishable from "just diffed, not yet touched" by
any existing column — `is_visible=false` and `pushed_to_prod_utc=null` describe BOTH "never
pushed" and "pushed, awaiting confirm". If the operator closes the Studio page mid-flow (Blazor
page-local `_publishRows` state is lost on navigation/reload), there is currently no persisted way
to resume or even discover which rows are mid-flight.
**Why it happens:** All in-flight state today (`_publishRows`, `_diffRows`, `_gitSha`, etc.) is
page-local Blazor component state (`DirectPush.razor.cs:34-90`), not persisted.
**How to avoid:** The planner should decide whether a resumable persisted state is in-scope for
Phase 90 or an accepted operational limitation (operator re-runs Stage 1 diff, which will
re-classify a content-upserted-but-invisible row as "Unchanged" per `ClassifyDiff`'s content-signature
comparison — since `body_sha256`/content columns already match, but visibility/stamp are separate
concerns the diff doesn't consider). **This is a real ambiguity to flag to the planner**: a
content-upserted-but-not-yet-visible row will be classified `Unchanged` on re-diff (its content
signature already matches prod) and therefore **excluded from `PublishRows`**
(`DirectPushCoordinator.cs:172-176`) — meaning the confirm/stamp/visibility step can never be
reached again through the normal Stage 1→N flow unless the coordinator tracks "confirmed" as a
distinct dimension from "content matches".
**Warning signs:** Rows that show `Unchanged` on every subsequent diff but never go visible.

### Pitfall 5: Prod-first-then-local write ordering must survive the split
**What goes wrong:** `WritePublishAsync`'s existing comment (`DirectPushCoordinator.cs:204-211`)
documents a deliberate ordering: prod is written FIRST so a prod failure leaves the local row
behind rather than over-reporting prod state (PUB-01/HIGH-3). If the stamp/visibility logic is
extracted into a new method, this invariant must be preserved in the new method too, not just in
the (now content-only) `WritePublishAsync`.
**Why it happens:** Splitting one method into two threads-through the ordering guarantee unless
explicitly re-documented and re-tested in the new method.
**How to avoid:** New confirm/stamp method should mirror the exact prod→local order and exception
semantics (`ContentSiteIndexBatchUpsertException` handling pattern) already proven in
`WritePublishAsync` and covered by `DirectPushCoordinatorTests.cs`.

## Code Examples

### Reusing the shared body-hash helper for a deploy-confirm check
```csharp
// Source: ContentSiteIndexContentSignature.cs:131-145 (existing, reuse verbatim — do not reimplement)
var computedHash = ContentSiteIndexContentSignature.ComputeBodySha256(rawArtifactText);
var matches = string.Equals(row.BodySha256, computedHash, StringComparison.Ordinal);
```
This is the identical call `ContentKbController.Detail` already makes at render time
(`ContentKbController.cs:126-134`) for the Phase 89 fail-open render guard. Whatever mechanism
Phase 90 designs for "verify the deployed `/app` body" should call this exact helper against
whatever raw text it obtains from the live server — reusing the comparison, not the log-only guard
itself (that guard only logs; it returns nothing to a caller).

### Existing DirectPush test-double wiring (extend, don't replace)
```csharp
// Source: DirectPushCoordinatorTests.cs:58-70 — the seam to extend for seed-export + reorder tests
private static DirectPushCoordinator Build(
    FakeContentSiteIndexStore local,
    FakeContentSiteIndexStore prod,
    FakeSshArtifactUploader? uploader = null,
    string artifactRoot = "/data/content-kb",
    FakeGitRepository? git = null,
    FakeContentKbOrchestrator? orchestrator = null)
    => new(local, uploader ?? new(), new FakeProdStoreFactory(prod),
           new ConfigurationBuilder().Build(),
           new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot }, ...);
```
`FakeContentKbOrchestrator` already exists (used by `PublishCoordinatorTests.cs` too) and is the
natural seam to assert DirectPush now calls `ExportIndexToFileAsync` (SYNC-08) with the correct
seed path, and to assert the new ordering (no `StampPushedToProdAsync`/`SetVisibilityAsync` call
until after a simulated confirm).

## State of the Art

| Old Approach | Current Approach (this phase) | When Changed | Impact |
|--------------|------------------|---------------|--------|
| DirectPush stamps+flips-visible inside the same call as the content upsert (Stage 3), before git even runs | Stamp+visibility move to a new, later step gated on a confirmed deploy | This phase (SYNC-09/D-06) | Rows can no longer go visible before their body is durably in git and deployed |
| DirectPush never re-exports the seed | DirectPush calls the same `ExportIndexToFileAsync` factory Publish uses | This phase (SYNC-08/D-08) | A redeploy/reseed can fully reconstruct DirectPush'd rows; kills the every-deploy-revert bug (C3) |
| Web serves `/app` then silently falls back to `/data` overlay | Web serves `/app` only, under a flag | This phase (SYNC-07), flag OFF by default | Removes the "unreachable body" and "stale `/app` shadows fresh `/data`" failure classes, but requires every visible row's body to actually be in git first |

**Deprecated/outdated:** The `/data` SFTP-overlay-as-body-source model is being phased out for
DirectPush'd content (per the design doc's `0dd49f19` precedent already applied to Pull). The
`RenderSkipPhrase`/`[skip render]` convention on DirectPush commits is now in tension with the new
ordering requirement and needs an explicit decision (see Open Questions), not silent carry-forward.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `Studio:ProdConnectionString` and the web app's `DECKFLOW_DATABASE_CONNECTION_STRING` point to the exact same physical Postgres database/instance in production (not just "both Postgres, possibly different instances") | Pattern 3 (D-04 accessor) | If false, Studio cannot read `feature_flags` via the prod content connection string at all, and D-04's "minimal read-only accessor" would need an entirely separate connection string plumbed into Studio config — a bigger change than CONTEXT.md implies. This is inferred from code structure (`DeckFlowDatabaseConnectionFactory` using identical env-var-driven connection logic for both) but was not verified by actually querying prod in this research session. |
| A2 | Render's autodeploy lag (commit→push→build→live) is long enough (likely 1-5+ minutes) that a synchronous in-request wait is infeasible, making an operator-triggered "Verify & Publish" button (rather than a background poller) the right shape per D-02 (no CDC/queues) | Open Question 1/2, System Architecture Diagram | If Render deploys are actually fast/observable synchronously, a simpler polling-within-one-click design might be viable, changing the planner's UI design significantly |
| A3 | No existing admin/API endpoint already exposes a live `/app`-body-hash lookup by artifact path (confirmed by directory listing of `Controllers/Admin/` and `Controllers/Api/` — none inspect `ContentKbArtifactPathResolver` output) | Open Question 2 | If some other endpoint already does this indirectly, the planner could reuse it instead of building new surface area |

**If this table is empty:** N/A — see above.

## Open Questions

1. **Does the DirectPush git commit keep `[skip render]` under the new flag, or drop it?**
   - What we know: The constant exists specifically to suppress a Render redeploy
     (`DirectPushCoordinator.cs:33-37`), because historically the `/data` overlay already served
     the content live. SYNC-09 requires a genuine `/app` redeploy to have happened before the
     hash-verify can succeed.
   - What's unclear: CONTEXT.md's D-06/D-07 describe the ordering outcome (expand→verify→contract)
     but do not address this specific mechanical contradiction with the existing skip-render
     constant.
   - Recommendation: Planner must decide and document one of: (a) drop `[skip render]` for
     DirectPush commits when the flag is ON so pushes trigger a real deploy automatically, or
     (b) keep `[skip render]` and require the operator to manually trigger a Render deploy (e.g.
     via the Render dashboard or a "Manual Deploy" button) between the git-push stage and the
     verify stage. Option (a) is simpler operationally but changes deploy cadence/cost
     characteristics (every DirectPush now triggers a full Render build); option (b) adds an
     explicit operator step. Either choice should be captured as a locked decision before planning
     proceeds, since it changes the shape of the new Studio stage(s).

2. **What is the actual mechanism for "confirm the deployed `/app` body matches"?**
   - What we know: D-06 says reuse `ComputeBodySha256` + "the render-guard comparison." The
     render-guard (`ContentKbController.cs:121-134`) computes this hash today as a side effect of
     a normal page GET, but only **logs** on mismatch — it returns nothing machine-readable to a
     caller, and Studio never makes HTTP calls into the deployed web app at all (its only prod
     touchpoints are raw Postgres via `Studio:ProdConnectionString`, SSH/SCP via
     `ISshArtifactUploader`, and git via `IGitRepository`).
   - What's unclear: whether Phase 90 is expected to add a NEW web endpoint (e.g.
     `GET /api/content-kb/verify-body?artifactPath=...` returning the computed hash, following the
     `ArchidektCacheJobsController` pattern for a small internal-status API) that Studio calls over
     RestSharp+Polly (per house HTTP convention), or whether some lighter-weight mechanism (e.g.
     Studio fetches the public `/content-kb/{id}` HTML page and can't get the hash that way since
     it's not exposed in the rendered output either) is intended.
   - Recommendation: Design a small, admin/internal-only API endpoint (mirroring the existing
     `Controllers/Api/` pattern, gated by the same-origin validator or a shared secret rather than
     `BasicAuthMiddleware` since Studio is a separate process without a browser session) that reads
     the artifact via `ContentKbArtifactPathResolver` + `ComputeBodySha256` and returns the hash (or
     a match boolean against a query-supplied expected hash) for one or more artifact paths. This
     endpoint is new production surface and should get its own security review in planning (it is
     a read of arbitrary-but-validated repo-relative paths, similar in shape to the existing
     `TryResolveExistingArtifact` containment guards).

3. **How does DirectPush distinguish "content upserted, awaiting confirm" from "not pushed" across a Studio session boundary?**
   - What we know: All Stage 1-4 state today is page-local Blazor state, not persisted
     (`DirectPush.razor.cs:34-90`). `ClassifyDiff` would reclassify a content-matching-but-invisible
     row as `Unchanged` and exclude it from `PublishRows` on any re-diff (Pitfall 4).
   - What's unclear: whether Phase 90 needs a persisted "confirm-pending" marker (e.g. reusing
     `pushed_to_prod_utc IS NULL AND is_visible = false AND <content already matches prod>` as an
     implicit signal computed at diff time) or whether the confirm step is designed to run
     synchronously in the same operator session as Stage 3/4 (never left pending across a page
     reload), making persistence unnecessary.
   - Recommendation: Simplest correct design is likely to keep the whole expand→verify→contract
     sequence within ONE Stage-4/5 operator session (no page navigation in between) so no new
     persisted state is needed — the operator waits for/confirms the deploy, then the confirm step
     runs against the SAME in-memory `_publishRows`/`exportedKeys` list already held from Stage 3.
     This avoids inventing a new state column but constrains the UX (operator can't safely close
     the tab mid-flow without losing the resumability of "these specific rows are pending"). Flag
     this UX tradeoff explicitly for CONTEXT.md-level confirmation if not already implicitly
     decided by "Claude's Discretion."

## Environment Availability

Not applicable — no new external tool/service/runtime dependency. All work is against existing
in-repo abstractions and the existing prod Postgres connection Studio already uses.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (both `DeckFlow.Core.Tests` and `DeckFlow.Studio.Tests`/`DeckFlow.Web.Tests`) |
| Config file | none — standard `dotnet test` per project |
| Quick run command | `dotnet test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --filter DirectPush` |
| Full suite command | `dotnet build && dotnet test` (whole solution) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SYNC-07 | Flag OFF: serving unchanged (git-then-overlay); Flag ON: git-only, no overlay fallback | unit (Web) | `dotnet test DeckFlow.Web.Tests --filter ContentKbArtifactPathResolver` | ❌ Wave 0 — extend existing resolver tests with `FakeFeatureFlagStore`/cache seam |
| SYNC-08 | DirectPush commit stages `content-kb/seed/index-seed.json` alongside bodies, via the shared factory | unit (Studio) | `dotnet test DeckFlow.Studio.Tests --filter DirectPushCoordinator` | ❌ Wave 0 — extend `DirectPushCoordinatorTests.cs` with `FakeContentKbOrchestrator` assertion |
| SYNC-09 | No `StampPushedToProdAsync`/`SetVisibilityAsync` call happens before a simulated "confirmed" signal | unit (Studio) | same file/filter as above | ❌ Wave 0 — needs the new confirm-seam fake designed per Open Question 2 |
| SYNC-10 | `pushed_to_prod_utc` timestamp is only set in the post-confirm path, never in the content-only upsert path | unit (Studio) | same file/filter as above | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet build` clean (per CLAUDE.md — VSTest unreliable in WSL, rely on
  build-clean + targeted `dotnet test` for the specific changed project)
- **Per wave merge:** `dotnet test` across `DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`,
  `DeckFlow.Studio.Tests`
- **Phase gate:** Full suite green before `/gsd-verify-work`; per CLAUDE.md, UI/visual smoke for
  the (Blazor) DirectPush page changes should use `scripts/run-web-test.sh`-style non-browser
  verification — no browser on the Windows host.

### Wave 0 Gaps
- [ ] Extend `DeckFlow.Web.Tests` for `ContentKbArtifactPathResolver` flag-gated resolution
      (needs `FakeFeatureFlagStore.cs`, already exists in `DeckFlow.Web.Tests/TestDoubles/`)
- [ ] Extend `DirectPushCoordinatorTests.cs` for seed-export-on-commit assertion (uses existing
      `FakeContentKbOrchestrator`)
- [ ] New test double/seam for the SYNC-09 deploy-confirm mechanism — cannot be fully designed
      until Open Question 2 is resolved; at minimum, the confirm step's *ordering* (called after
      git success, before stamp/visibility) is testable today with a stub that always returns
      "confirmed", independent of the real HTTP mechanism's design.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | Only if Open Question 2's new endpoint is built | Reuse `BasicAuthMiddleware` (`/Admin/*` convention) or `SameOriginRequestValidator` (existing API pattern) — do not invent a third auth mechanism |
| V3 Session Management | No | Studio↔prod calls are stateless, ephemeral connections per existing pattern |
| V4 Access Control | Yes, if new endpoint added | The new deploy-confirm endpoint reads arbitrary-but-validated artifact paths; must reuse `ContentKbArtifactPathResolver`'s existing containment guards (`IsSafeArtifactPath`, `IsContainedUnderRoot`) — never accept an unvalidated path parameter |
| V5 Input Validation | Yes | Artifact path / natural key inputs to any new endpoint must go through the same validation already proven in `ContentKbArtifactPathResolver.IsSafeArtifactPath` (`.cs:175-193`) |
| V6 Cryptography | No new crypto | SHA-256 hashing is already implemented and reused (`ContentSiteIndexContentSignature.ComputeBodySha256`) — never hand-roll a second hash routine |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal via a Studio-supplied artifact path to a new "verify body hash" endpoint | Tampering | Reuse `ContentKbArtifactPathResolver`'s existing `IsSafeArtifactPath`/`IsContainedUnderRoot` guards verbatim; never build a second path-validation routine (this exact class of bug was already found and fixed once, per the `Jul 5, 2026` "Code Review Identifies Two Medium-Severity Issues in Baked Prompt and Path Containment Logic" note) |
| Unauthenticated internal endpoint exposing repo-tree read access | Information Disclosure / Elevation of Privilege | Any new endpoint built for SYNC-09 must sit behind an auth boundary at least as strong as `/Admin/*`'s `BasicAuthMiddleware`, or be restricted to same-origin/internal callers only — it must NOT be a public unauthenticated route, since it is effectively a targeted file-read primitive |
| `feature_flags` table read from Studio bypassing the write-protection invariant | Tampering | The new Studio accessor (Pattern 3) MUST be read-only — model it strictly on `IProdContentReader`'s single-`SELECT`, no-DDL, no-write contract; do not accidentally reuse `IProdStoreFactory`-style store construction that could expose a write path |

## Sources

### Primary (HIGH confidence — direct code reads this session)
- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` — full read; current 4-stage write/stamp/visibility/git ordering
- `DeckFlow.Studio/ViewModels/PublishCoordinator.cs` — full read; the reference seed-export pattern
- `DeckFlow.Studio/Pages/DirectPush.razor.cs` — full read; exact UI stage sequencing and gating
- `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs` — full read; confirms Studio's only prod touchpoints (Postgres/git), no HTTP to web app
- `DeckFlow.Web/Controllers/ContentKbController.cs` — full read; Phase 89 render guard, body-serving call site
- `DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs` — full read; exact git-then-overlay resolution order
- `DeckFlow.Web/Services/FeatureFlags/{FeatureFlagCatalog,FeatureFlagStore,IFeatureFlagStore,FeatureFlagCache,IFeatureFlagCache}.cs` — full read; flag registration/seed/default-on mechanics
- `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs` — full read; confirms attribute is action-level 404-only, not usable for SYNC-07's conditional branch
- `DeckFlow.Web/Services/Persistence/DeckFlowDatabaseConnectionFactory.cs` — full read; confirms `feature_flags` and `content_site_index` share connection-string plumbing in prod
- `DeckFlow.Studio/Services/{ProdContentReader,IProdContentReader,IProdStoreFactory}.cs` — full read; the read-only accessor pattern to replicate
- `DeckFlow.Core/Orchestration/{ContentIndexExportRow,ContentKbOrchestrator,IContentIndexExporter}.cs` — full read (relevant sections); shared export factory internals
- `DeckFlow.Core/Content/{ContentSiteIndexContentSignature,ContentSiteIndexStore}.cs` — full read (relevant sections); hash helper, upsert/stamp/visibility SQL, F-51-PG-01 non-applicability confirmed for existing stamp SQL
- `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` — full read; confirms `UpsertRowPreservingVisibilityAsync` reload path (the C3 revert mechanism SYNC-08 neutralizes)
- `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs` — partial read; confirms existing fake seams (`FakeContentSiteIndexStore`, `FakeProdStoreFactory`, `FakeGitRepository`, `FakeContentKbOrchestrator`, `FakeSshArtifactUploader`)
- `.planning/phases/90-directpush-correctness-seed-sync/90-CONTEXT.md`, `.planning/phases/89-content-hash-foundation/89-CONTEXT.md`, `.planning/REQUIREMENTS.md`, `.planning/STATE.md`, `docs/research/kb-prod-sync-fix-design.md`, `docs/research/kb-prod-sync-roadmap.md` — full read

### Secondary (MEDIUM confidence)
- `.planning/debug/resolved/prod-harvest-42883-text-timestamptz.md`, `.planning/milestones/cycle8-*` — F-51-PG-01 historical record, cross-referenced via grep across multiple planning docs for consistency

### Tertiary (LOW confidence)
- None — no WebSearch was needed for this phase; it is entirely in-repo code archaeology.

## Metadata

**Confidence breakdown:**
- Current-code-reality claims (file:line citations): HIGH — every claim was verified by reading the actual file, not inferred from documentation or memory
- SYNC-07/08/10 target-state design: HIGH — direct, mechanical pattern-copy from `PublishCoordinator`/`ContentKbArtifactPathResolver`/`FeatureFlagStore` precedent
- SYNC-09 deploy-confirm mechanism: LOW — no existing precedent in the codebase; genuinely novel design surface flagged as Open Questions 1-3, needs a locked decision before planning can size this work accurately

**Research date:** 2026-07-07
**Valid until:** ~14 days (fast-moving milestone; Phase 91/92 land on top of this phase's output and could shift assumptions about seed lifecycle/marker state referenced here)
