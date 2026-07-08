# Phase 91: Reconcile + Seed Lifecycle - Research

**Researched:** 2026-07-08
**Domain:** Prod↔git↔seed drift detection + gated destructive seed-driven removal (DeckFlow Content-KB, Studio operator tooling)
**Confidence:** HIGH (all findings grounded in read code + CONTEXT.md locked decisions; no external library research needed — this phase is 100% in-house code extension)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Design stance (inherited P89/P90 — unchanged, MUST honor)**
- git = single source of truth for BODIES (P90 D-01). The prod `content_site_index` row is subordinate and reconstructable from the git seed. Reconcile treats git/seed as authoritative.
- No CDC / queue / SFTP-body-push (P90 D-02). Reconcile is idempotent keyed diff + hash comparison, run on operator demand. It must NOT introduce any out-of-band body transport.
- Reuse, do not fork: P89 `ContentSiteIndexContentSignature.ComputeBodySha256` + unified signature for body-hash-mismatch detection; P90 shared `ContentIndexExportRow.From()` seed factory for any seed (re)write. No second hash path, no second seed-writer.

**Seed-ownership marker (SYNC-17)**
- D-01: New DB column `seed_managed` (bool) + matching seed JSON field. Dialect-guarded additive DDL (SQLite + Postgres), idempotent `ensure`-style, exactly like SYNC-01's `body_sha256` rollout. Rejected: derive-from-seed-set-at-load. Set `seed_managed = true` whenever a row is written from the seed-managed set (Publish / DirectPush seed export / seed reload).
- D-02: Backfill legacy rows by CURRENT seed membership (index-seed.json). Present → `seed_managed = true`; absent → `seed_managed = false` (prod-owned).
- Invariant: seed-driven removal (SYNC-12) applies to `seed_managed = true` rows ONLY. Prod-owned (`seed_managed = false`) rows are NEVER hidden/deleted by reconcile.

**Removal action (SYNC-12)**
- D-03: Soft-hide only. `is_visible = false`, RETAINS the row (marker, `body_sha256`, timestamps, history), logs the discrepancy. No hard-delete this phase.

**Reconciler surface + discrepancy store (SYNC-11)**
- D-04: Reconciler runs as a Studio operator action (operator-triggered only; scheduled runs are SYNC-F2, out of cycle).
- D-05: Persistent discrepancy store lives LOCAL to Studio (local content-kb DB / SQLite). Idempotent upsert keyed by a deterministic discrepancy ID; re-run resolves-by-absence; partial/scoped runs are scope-tagged. Rejected: prod DB table (violates minimal-additive-prod-DDL cap) and a git-tracked flat file as the store.
- D-06: Git-tracked report file is the human-readable dry-run OUTPUT, layered on top of D-05 (does not replace it).
- D-07: Reconciler is a NEW build (per SYNC-11), NOT a `ContentKbOrphanScanner` extension. Mine it (and `ContentSyncDiffClassifier`, `ReconciliationReporter`) for reusable classification/reporting shapes.

**Dry-run → apply gating + flag (SYNC-11 / SYNC-12)**
- D-08: Two-step re-validated apply. Dry-run produces the discrepancy report (D-06) + persists discrepancies (D-05). A SEPARATE "Apply removals" action RE-RUNS the diff at apply time and soft-hides only discrepancies still present then.
- D-09: `sync.reconcile` gates ONLY the destructive (soft-hide) apply. Detection, dry-run, the discrepancy store, and the report are ALWAYS available (read-only, safe).
- D-10: `sync.reconcile` is a WEB-DB feature flag; Studio reads it via the P90 accessor; seeded OFF. Register in `FeatureFlagCatalog`, persisted in `FeatureFlagStore`.

### Claude's Discretion
- Exact column name/type nuance for `seed_managed` (bool vs nullable smallint) and the deterministic discrepancy-ID scheme — planner/researcher pick the least-invasive form per the code, honoring the dialect-guarded additive-DDL house pattern and idempotent-upsert requirement.
- Where the local discrepancy store schema lives (new table in the existing local content-kb DB vs a sibling store) — least-invasive per D-05.

### Deferred Ideas (OUT OF SCOPE)
- Hard-delete of seed-absent rows / two-stage hide-then-delete — deferred; soft-hide only this phase (D-03).
- Scheduled / automatic reconcile runs — SYNC-F2, explicitly out of this cycle; operator-triggered only.
- Pull-from-Prod hardening (per-field master, git-pull-first staleness guard) — Phase 92 (SYNC-13/14/15). Do not modify Pull semantics here.
- End-to-end containerized round-trip integration test — Phase 93 (SYNC-16).
- Flipping `sync.reconcile` (or `sync.directpush-gitbody`) ON in prod — gated by Phase 93 pre-flip checklist; this phase ships the flag OFF.
- Any prod-side discrepancy table or new prod DDL beyond the `seed_managed` marker — rejected by the minimal-additive-prod-DDL cap.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SYNC-17 | Row-level seed-management marker distinguishing seed-owned rows from prod-only rows (hard prereq before any seed-driven delete) | §"seed_managed column" below: exact DDL location, the 4 upsert-SQL variants and which ones must stamp `true`, why the column must be threaded through `ContentSiteIndexRow` as a per-call parameter (NOT hardcoded in shared SQL), backfill via nullable-column IS-NULL idempotent pattern (mirrors `SetBodySha256IfNullAsync`) |
| SYNC-11 | NEW prod↔git↔seed reconciler + persistent discrepancy store; 4 discrepancy classes; deterministic IDs; idempotent re-run; resolution-by-absence; scope-tagged partial runs; dry-run ships first | §"Reconciler architecture" below: pure-classifier-in-Core / I/O-orchestrator-in-Studio split, per-class data sources, `ProdContentReader` extension required, local discrepancy-store schema + resolution-by-absence query shape, git-tree file enumeration (net-new capability) |
| SYNC-12 | Seed reload handles removals — seed_managed rows absent from the seed-managed set are hidden + logged, replacing additive-only upsert; destructive apply gated behind dry-run validation | §"Removal / soft-hide apply" below: which component performs the write (Studio reconciler Apply action, NOT an automatic `ContentKbSeedLoader` boot-time deletion — flagged as an Open Question given the ambiguity between the requirement text and the locked D-04/D-08/D-09 decisions), reuse of `SetVisibilityBySourceAsync`-shaped writes, F-51-PG-01 preservation |
</phase_requirements>

## Summary

Phase 91 is a pure in-house extension of code shipped in Phases 88-90 — there is no new external
library, framework, or package to evaluate. The three requirements decompose cleanly onto the
existing `ContentSiteIndexStore` (dialect-guarded DDL host), `ProdContentReader` (read-only prod
access), `IGitBodyCoverageAudit` (nearest existing prod↔git join), and the Studio local
`content-kb.db` (already hosts 9 sibling local stores using the identical schema-ensure pattern).

The single most important finding is a **non-obvious correctness trap**: the `seed_managed`
marker cannot be a hardcoded literal in the shared `UpsertContentColumnsOnlySql`/
`UpsertPreservingVisibilitySql` statements, because those exact SQL strings are called from BOTH
a local-distill write path (`ContentKbOrchestrator`, which must NOT set `seed_managed=true`) and
the prod-publish write paths (`DirectPushCoordinator.WriteContentAsync`,
`ContentKbSeedLoader.LoadIfPresentAsync`, which MUST set it true). `seed_managed` must become a
first-class `ContentSiteIndexRow` property bound as a Dapper parameter — mirroring exactly how
`ApprovalStatus`/`BodySha256` are already mirrored per-row — with each caller setting the value
explicitly. Getting this wrong (hardcoding `TRUE` in the shared SQL) would silently mark every
local Studio distill row as seed-managed and break the SYNC-17 invariant this phase exists to
establish.

The second key finding is a **missing read surface**: `ProdContentReader.ReadAllAsync` — the ONLY
prod-read seam available to Studio — does not currently select `body_sha256` or
`awaiting_confirm_utc` (its `SelectAllSql` and private `ContentSiteIndexRowData`/mapping omit
both columns, confirmed by direct read). The reconciler's body-hash-mismatch class is
structurally impossible without extending this reader first — this is a required, in-scope task,
not an edge case.

**Primary recommendation:** Build a pure, I/O-free classifier in `DeckFlow.Core/Content/`
(mirroring `ContentSyncDiffClassifier`'s shape: static class, no DI, takes already-loaded
collections, returns discrepancy records) that joins three inputs — extended `ProdContentReader`
rows, a git-tree file enumeration under `content-kb/`, and parsed `index-seed.json` entries — into
the four discrepancy classes. Wrap it in a new Studio service (parallel to
`GitBodyCoverageAudit`) that does the actual I/O and persists results to a new local SQLite table
in `content-kb.db`, using the exact schema-ensure/CREATE-TABLE pattern already used by
`ContentHarvestRunStore` and 8 other local stores. Gate only the soft-hide Apply action behind
`sync.reconcile`, read via the P90 `IProdContentReader.TryReadFlagAsync` accessor already built.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `seed_managed` column + DDL | Database / Storage (`ContentSiteIndexStore`, shared class) | — | Same dialect-guarded `EnsureSchemaAsync` host used for `body_sha256`/`awaiting_confirm_utc`; exists structurally on both local SQLite and prod Postgres schemas because it is one shared store class |
| `seed_managed` write-path stamping | API / Backend (Web: `ContentKbSeedLoader`) + Studio operator surface (`DirectPushCoordinator`) | Database / Storage | The marker's TRUTH is set at the moment a row enters the seed-managed set — two write call sites, not the schema layer |
| `seed_managed` backfill (D-02) | Database / Storage (Core, host-agnostic backfill class) | API/Backend (web startup) + Studio operator surface (local store) | Mirrors `ContentBodyHashBackfill`'s host-agnostic design exactly; must run on the PROD-pointed store (via web startup, schema-ensure ON) since that is where the ~70 prod-only rows live |
| Discrepancy classification (4 classes) | Database / Storage — pure logic, Core | — | No I/O; takes 3 already-loaded collections; unit-testable without a DB or git process, mirrors `ContentSyncDiffClassifier` |
| Prod read (extended for `body_sha256`/`seed_managed`) | Database / Storage (`ProdContentReader`, Studio) | — | Structurally read-only seam Studio already owns; must be extended, not forked |
| Git-tree file enumeration (file-orphan class) | Studio operator surface (new component) | — | Net-new capability; no existing component walks the full `content-kb/**/*.md` tree |
| Seed-file parsing (seed-drift class) | Studio operator surface (new component, or reuse `ContentKbSeedLoader`'s JSON shape) | — | Reads `index-seed.json` from the operator's git checkout, same file `ContentKbSeedLoader`/`PublishCoordinator` read/write |
| Discrepancy persistence + resolution-by-absence | Database / Storage — new local SQLite table in `content-kb.db` | Studio operator surface (orchestration) | D-05 mandates local-only; identical schema-ensure host pattern as `ContentHarvestRunStore` |
| Dry-run human-readable report | Studio operator surface (file write) | — | D-06: layered output on top of the D-05 store, git-tracked |
| Soft-hide Apply (destructive) | Database / Storage (`ContentSiteIndexStore.SetVisibilityBySourceAsync`-shaped write against PROD) | Studio operator surface (gated action, flag read) | Reuses existing visibility-write surface; Studio triggers it behind `sync.reconcile` |
| `sync.reconcile` flag | API / Backend (Web: `FeatureFlagCatalog`/`FeatureFlagStore`) | Studio operator surface (read-only accessor) | Single source of truth is the web-DB flag store, per D-10/P90 D-04 precedent |

## Standard Stack

No new external libraries, NuGet packages, or frameworks are required. This phase is a pure
extension of existing in-house components:

| Component | Already In Solution | Role This Phase |
|-----------|---------------------|------------------|
| Dapper | Yes (`DeckFlow.Core`) | Parameter binding for the new `seed_managed` column, discrepancy-store upsert |
| Microsoft.Data.Sqlite | Yes (`DeckFlow.Core`) | Local discrepancy store (same `content-kb.db`) |
| Npgsql | Yes (`DeckFlow.Core`) | Prod Postgres `seed_managed` ALTER (dialect-guarded) |
| System.Text.Json | Yes (BCL) | Seed JSON parsing (reuse `ContentKbSeedLoader`'s `JsonSerializerOptions` shape) |
| System.Security.Cryptography | Yes (BCL, via `ContentSiteIndexContentSignature`) | Body-hash-mismatch class (reuse `ComputeBodySha256`, no new hashing) |

**Installation:** None required.

**Version verification:** Not applicable — no new package versions to pin.

## Package Legitimacy Audit

Not applicable — this phase installs no external packages. All work extends existing,
already-audited in-solution code (`ContentSiteIndexStore`, `ProdContentReader`,
`GitBodyCoverageAudit`, `ContentKbSeedLoader`, `FeatureFlagCatalog`).

## Architecture Patterns

### System Architecture Diagram

```
                         ┌─────────────────────────────┐
                         │   Studio operator (Blazor)   │
                         │  new "Reconcile" page/action  │
                         └───────────────┬───────────────┘
                                         │ triggers dry-run
                                         ▼
              ┌──────────────────────────────────────────────────┐
              │  NEW: ContentKbReconcileOrchestrator (Studio)      │
              │  (I/O orchestrator — parallel to GitBodyCoverageAudit) │
              └───┬─────────────┬──────────────────┬───────────────┘
                  │             │                  │
     reads prod   │  reads git  │       reads seed │
     rows         ▼  tree       ▼       JSON       ▼
        ┌───────────────┐ ┌───────────────┐ ┌───────────────────┐
        │ ProdContentReader│ │ Directory.Enum-│ │ index-seed.json   │
        │ EXTENDED:       │ │ erateFiles     │ │ parse (reuse       │
        │ + body_sha256   │ │ content-kb/**/*│ │ ContentKbSeedLoader│
        │ + seed_managed  │ │ .md under      │ │ JSON shape)        │
        └────────┬────────┘ │ repoRoot       │ └─────────┬──────────┘
                  │          └───────┬───────┘           │
                  └──────────┬───────┴───────────┬────────┘
                             ▼                    ▼
              ┌────────────────────────────────────────────┐
              │  NEW: pure classifier (DeckFlow.Core)        │
              │  no I/O — 3 in-memory collections IN,         │
              │  discrepancy records OUT                      │
              │  4 classes: published-orphan, file-orphan,    │
              │  seed-drift, body-hash-mismatch                │
              └───────────────────┬────────────────────────┘
                                  ▼
              ┌────────────────────────────────────────────┐
              │  NEW local discrepancy store (content-kb.db) │
              │  idempotent upsert by deterministic ID        │
              │  resolution-by-absence, scope tags            │
              └───────────────┬─────────────┬────────────────┘
                              │             │
                    D-06 report file        │
                    (git-tracked,           │
                     human-readable)        │
                                            ▼
                         ┌─────────────────────────────┐
                         │ Operator reviews report,       │
                         │ flips sync.reconcile (elsewhere)│
                         │ then clicks "Apply removals"   │
                         └───────────────┬───────────────┘
                                         ▼
                      ┌───────────────────────────────────┐
                      │ Apply action (D-08):                │
                      │ 1. read sync.reconcile via P90       │
                      │    IProdContentReader.TryReadFlagAsync│
                      │ 2. RE-RUN seed-drift diff fresh       │
                      │ 3. soft-hide (is_visible=false) only  │
                      │    rows STILL absent, seed_managed=true│
                      │    only — reuses SetVisibility-shaped │
                      │    write against PROD                 │
                      └───────────────────────────────────┘
```

### Recommended Project Structure

```
DeckFlow.Core/Content/
├── ContentSiteIndexStore.cs          # MODIFIED: seed_managed DDL + column threading
├── IContentSiteIndexStore.cs         # MODIFIED: seed_managed on ContentSiteIndexRow (in Knowledge/ContentArtifactSpec.cs)
├── ContentKbReconcileClassifier.cs   # NEW: pure 4-class discrepancy classifier (mirrors ContentSyncDiffClassifier)
├── ContentKbReconcileDiscrepancy.cs  # NEW: discrepancy record types + deterministic ID builder
└── SeedManagedBackfill.cs            # NEW: host-agnostic backfill (mirrors ContentBodyHashBackfill)

DeckFlow.Studio/Services/
├── ProdContentReader.cs              # MODIFIED: select + map body_sha256, seed_managed, awaiting_confirm_utc
├── IContentKbReconcileStore.cs       # NEW: local discrepancy-store contract
├── ContentKbReconcileStore.cs        # NEW: SQLite-backed, content-kb.db, schema-ensure pattern (mirrors ContentHarvestRunStore)
└── ContentKbReconcileOrchestrator.cs # NEW: I/O orchestrator — git enumeration + seed parse + prod read → classifier → store + report

DeckFlow.Studio/ViewModels/
└── ReconcileCoordinator.cs           # NEW: dry-run + Apply actions, flag read, mirrors DirectPushCoordinator/PublishCoordinator shape

DeckFlow.Studio/Pages/
├── Reconcile.razor                   # NEW: operator page (mirrors DirectPush.razor / PullFromProd.razor)
└── Reconcile.razor.cs

DeckFlow.Web/Services/Content/
└── ContentKbSeedLoader.cs            # MODIFIED: stamp seed_managed=true on every upserted row

DeckFlow.Web/Services/FeatureFlags/
├── FeatureFlagCatalog.cs             # MODIFIED: register sync.reconcile description
└── FeatureFlagStore.cs               # MODIFIED: seed sync.reconcile OFF (Postgres + SQLite seed SQL)
```

### Pattern 1: `seed_managed` as a per-row parameter, not a SQL literal

**What:** Add `SeedManaged` (recommend nullable `bool?`, see Pitfall 1) to `ContentSiteIndexRow`
(`DeckFlow.Core/Knowledge/ContentArtifactSpec.cs:107`) and bind it in
`ContentSiteIndexStore.BuildUpsertParameters` exactly like `ApprovalStatus`/`BodySha256` are
already bound (`ContentSiteIndexStore.cs:917-923`). Add `seed_managed` to the column list of
`UpsertContentColumnsOnlySql` and `UpsertPreservingVisibilitySql`, with
`seed_managed = EXCLUDED.seed_managed` on conflict (mirrors the `body_sha256` line at
`ContentSiteIndexStore.cs:1207`).

**When to use:** Every prod-write call site decides the value explicitly:
- `ContentKbOrchestrator.cs:1358` (local Studio distill write, `UpsertContentColumnsOnlyAsync`) → leave `SeedManaged` unset/false — a freshly-distilled local row is NOT yet seed-managed.
- `DirectPushCoordinator.WriteContentAsync` (`DirectPushCoordinator.cs:250`, `UpsertContentColumnsOnlyBatchAsync` against PROD) → MUST set `SeedManaged = true` on each row before the batch call — a DirectPush'd row enters the seed-managed set immediately (it also re-exports the seed, SYNC-08).
- `ContentKbSeedLoader.BuildRow` (`ContentKbSeedLoader.cs:68-89`, feeds `UpsertRowPreservingVisibilityAsync` against PROD at web boot) → MUST set `SeedManaged = true` unconditionally — this IS the seed-load path D-01 explicitly names.
- `PullFromProdCoordinator.cs:167` (`UpsertContentColumnsOnlyAsync` against LOCAL, pulling FROM prod) → out of this phase's scope (Phase 92), but note it calls the SAME shared SQL — whatever prod's `SeedManaged` value is should mirror into local, not be forced. Flag this for the Phase 92 planner.

**Example (parameter binding, mirrors the existing `approvalStatus`/`bodySha256` binding):**
```csharp
// Source: DeckFlow.Core/Content/ContentSiteIndexStore.cs:903-925 (existing pattern to extend)
private static DynamicParameters BuildUpsertParameters(ContentSiteIndexRow row, (string Type, string Value) naturalKey)
{
    var parameters = new DynamicParameters();
    // ... existing bindings ...
    parameters.Add("approvalStatus", row.ApprovalStatus);
    parameters.Add("bodySha256", row.BodySha256);
    // NEW: seed_managed, bound the same way — variants whose SQL doesn't reference
    // @seedManaged ignore this parameter harmlessly, exactly like the two above.
    parameters.Add("seedManaged", row.SeedManaged);
    return parameters;
}
```

### Pattern 2: Dialect-guarded additive DDL for `seed_managed`

**What:** Follow the EXACT `body_sha256` template at `ContentSiteIndexStore.cs:132-138` inside
`EnsureSchemaAsync`.

**Example:**
```csharp
// Source: DeckFlow.Core/Content/ContentSiteIndexStore.cs:132-138 — template to replicate
if (!columns.Contains("seed_managed"))
{
    // Why: nullable so backfill (D-02) can target "WHERE seed_managed IS NULL" —
    // mirrors SetBodySha256IfNullAsync's IS-NULL idempotent backfill pattern exactly.
    // A plain non-null bool with a DEFAULT would make "not yet classified" indistinguishable
    // from "classified as prod-owned (false)", breaking the one-time-backfill idempotency.
    await using var addSeedManaged = connection.CreateCommand();
    addSeedManaged.CommandText = _connectionInfo.IsPostgres
        ? "ALTER TABLE content_site_index ADD COLUMN seed_managed BOOLEAN NULL;"
        : "ALTER TABLE content_site_index ADD COLUMN seed_managed INTEGER NULL;";
    await addSeedManaged.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```
Add `seed_managed` to all 5 `SELECT` column lists in `ContentSiteIndexStore.cs` (`GetByNaturalKeyAsync`,
`GetPublishedRowsAsync`, `GetApprovedRowsAsync`, `GetAllRowsAsync`, `GetByIdAsync`), the
`PostgresCreateTableSql`/`SqliteCreateTableSql` fresh-create statements, and the private
`ContentSiteIndexRowData`/`ToContentSiteIndexRow` mapping — exactly parallel to how `body_sha256`
and `awaiting_confirm_utc` were threaded through in P89/P90.

### Pattern 3: Local schema-ensure store, mirrored from `ContentHarvestRunStore`

**What:** The new discrepancy store follows the identical shape as `ContentHarvestRunStore`
(`DeckFlow.Core/Content/ContentHarvestRunStore.cs`) — a `SemaphoreSlim`-gated
`EnsureSchemaAsync`, dialect-guarded `CREATE TABLE IF NOT EXISTS`, constructed in
`DeckFlow.Studio/Program.cs` with the SAME `contentKbDatabasePath` (`content-kb.db`) every other
local store uses (9 existing siblings: `ContentSourceStore`, `ContentVideoStore`,
`ContentSiteIndexStore`, `BlockedVideoStore`, `CreatorSourceStore`, `SkippedVideoStore`,
`ContentHarvestRunStore`, `LlmSpendLedger`, `WhisperSpendLedger`). This directly satisfies D-05
("new table in the existing local content-kb DB") with zero new infrastructure.

**Example (registration, mirrors `Program.cs:103`):**
```csharp
// Source: DeckFlow.Studio/Program.cs:103 — pattern to replicate for the new store
builder.Services.AddSingleton<IContentHarvestRunStore>(_ => new ContentHarvestRunStore(contentKbDatabasePath));
// NEW, same shape:
builder.Services.AddSingleton<IContentKbReconcileStore>(_ => new ContentKbReconcileStore(contentKbDatabasePath));
```

**Recommended schema:**
```sql
CREATE TABLE IF NOT EXISTS content_kb_reconcile_discrepancy (
  discrepancy_id   TEXT PRIMARY KEY,   -- deterministic, see Pattern 4
  kind             TEXT NOT NULL,      -- 'published_orphan' | 'file_orphan' | 'seed_drift' | 'body_hash_mismatch'
  natural_key_type TEXT NULL,
  natural_key_value TEXT NULL,
  artifact_path    TEXT NULL,
  title            TEXT NULL,
  scope_tag        TEXT NOT NULL,      -- e.g. 'full' or a source-scoped tag for partial runs
  first_seen_utc   TEXT NOT NULL,
  last_seen_utc    TEXT NOT NULL,
  resolved_utc     TEXT NULL
);
```

### Pattern 4: Deterministic discrepancy ID + resolution-by-absence

**What:** Build the ID from the SAME U+0000-separator convention `ContentNaturalKey` already
established for anti-collision composite keys (`ContentNaturalKey.cs`, used identically in
`ContentSyncDiffClassifier.IndexByNaturalKey` and `DirectPushCoordinator.ClassifyDiff`):

```csharp
// Row-keyed classes (published-orphan, seed-drift, body-hash-mismatch):
var discrepancyId = $"{kind} {naturalKeyType} {naturalKeyValue}";
// File-orphan (no row/natural key — keyed by artifact path instead):
var discrepancyId = $"file_orphan path {artifactPath}";
```

**Resolution-by-absence query shape** (run at the end of every reconcile pass, scoped):
```sql
UPDATE content_kb_reconcile_discrepancy
   SET resolved_utc = @now
 WHERE scope_tag = @scopeTag
   AND resolved_utc IS NULL
   AND discrepancy_id NOT IN @currentlySeenIds;  -- Dapper IN-clause expansion
```
The `scope_tag` filter is what makes scoped/partial runs safe — a run scoped to only YouTube rows
must never resolve podcast discrepancies it didn't examine (D-05's "scope-tagged so they never
false-resolve discrepancies outside their scope").

### Pattern 5: `sync.reconcile` flag registration (mirrors `sync.directpush-gitbody`)

**What:** Follow the exact P90 precedent, verified in code:
- `FeatureFlagCatalog.Descriptions` (`FeatureFlagCatalog.cs:97-99`) — add a `["sync.reconcile"] = "..."` entry.
- `FeatureFlagStore.cs:198-274` — add `('sync.reconcile', FALSE)` to both `PostgresSeedSql` and `('sync.reconcile', 0)` to `SqliteSeedSql`, alongside the existing `sync.directpush-gitbody` line.
- Studio reads it via `IProdContentReader.TryReadFlagAsync` (already built in P90, `IProdContentReader.cs:63-64`, real-implemented in `ProdContentReader.cs:110-149`) — same tri-state (`true`/`false`/`null`-indeterminate) contract `DirectPushCoordinator.TryReadDirectPushGitBodyFlagAsync` already consumes. The Apply action should treat `null` (indeterminate) as "refuse to apply" (fail-safe), matching D-09's "gates ONLY the destructive apply" intent — an uncertain read must never be treated as ON.

### Anti-Patterns to Avoid
- **Hardcoding `seed_managed = TRUE` in the shared upsert SQL:** breaks the local-distill path (Pitfall 1 above). Must be a bound parameter set per-caller.
- **Extending `ContentKbOrphanScanner`:** explicitly rejected by D-07 — it is local-only, has no prod/git/seed awareness, and its `ContentKbRowCheck`/`ContentKbOrphanScanResult` shapes only cover ONE of the four discrepancy classes (published-orphan-equivalent). Mine its "row → file existence" loop shape only.
- **Treating `DeckFlow.Core/Reporting/ReconciliationReporter.cs` as directly reusable:** this class is entirely deck-comparison (Moxfield/Archidekt) domain — its `DeckDiff`/`PrintingConflict` types have nothing to do with Content-KB. D-07's "mine ... `ReconciliationReporter` ... for reusable classification/reporting shapes" should be read as "reuse the TEXT-REPORT-BUILDING shape" (a `StringBuilder`-based sectioned report with `AppendSection` helpers) — not any actual type or method from this file.
- **Automatic deletion at web boot:** the roadmap's original C2 framing ("seed reload deletes automatically") is explicitly softened by the Codex HIGH finding and the phase's D-04/D-08/D-09 decisions into an operator-triggered, dry-run-then-re-validated-apply flow. Do NOT wire `ContentKbSeedLoader.LoadIfPresentAsync` to soft-hide anything automatically — see Open Question 1.
- **Reading `sync.reconcile` with the fail-CLOSED `ReadFlagAsync` for the Apply gate:** use the TRI-STATE `TryReadFlagAsync` instead, per the P90 re-review precedent (`ProdContentReader.cs:110-149` comments) — an indeterminate read must not silently proceed as if the flag were OFF (which would block Apply, arguably safe) NOR as if ON (destructive) without confirmation. Follow whichever fail-safe direction the flag's OWN semantics demand: since this flag gates a DESTRUCTIVE write, "indeterminate → refuse to apply" is the correct fail-safe direction (inverse of `VerifyAndPublishAsync`'s reasoning, which fails toward the SAFER of two non-destructive options).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Body content hashing | A second SHA-256 helper | `ContentSiteIndexContentSignature.ComputeBodySha256` | P89 D-01/D-02 established this as the ONE hash surface; a second implementation risks a different normalization (LF vs CRLF) and false mismatches |
| Natural-key derivation | Inline `if (YoutubeVideoId...)` logic | `ContentNaturalKey.TryDerive` | Already the shared seam `ContentSyncDiffClassifier` and `DirectPushCoordinator.ClassifyDiff` both use (SYNC-05 anti-collision) |
| Artifact-path validation before `File.Exists` | A third path-safety routine | `DeckFlow.Studio.Services.ArtifactPathSafety.TryBuildContainedPath` | Already deduplicated once (P90 D-11/T-90-05) from `PullFromProdCoordinator` + `GitBodyCoverageAudit`; a third copy reintroduces the traversal-risk surface it closed |
| Seed JSON parsing shape | A new DTO/serializer | `ContentKbSeedLoader`'s private `ContentKbSeedEntry` record + `JsonSerializerOptions` (camelCase, case-insensitive) | Byte-shape must match what `PublishCoordinator`/`DirectPushCoordinator` actually write via `ContentIndexExportRow.From()`; a second parser risks silently drifting from the real seed shape |
| Feature-flag read from prod | A new Studio-to-prod query | `IProdContentReader.TryReadFlagAsync` | Built in P90 specifically as the single reusable seam; a second query duplicates the SSL/connection-string normalization logic in `ProdContentReader` |
| Prod-writable store construction | A raw `RelationalDatabaseConnection` + Npgsql call | `IProdStoreFactory.Create(connectionString)` (schema-ensure OFF) | `DirectPushCoordinator.CreateProdStore()` already establishes this is the only sanctioned way Studio writes to prod — bypassing it risks accidentally running DDL against prod |

**Key insight:** every I/O seam this phase needs (prod read, prod write, git-tree root
resolution, seed-file shape, path safety, flag read, body hash) already exists in the codebase
from Phases 88-90. The only genuinely new capability is the FULL git-tree file enumeration for
the file-orphan class (nothing today walks `content-kb/**/*.md` and diffs it against all prod
rows — `ContentKbOrphanScanner` only goes row→file, never file→row) and the discrepancy
persistence/resolution-by-absence logic itself.

## Common Pitfalls

### Pitfall 1: Plain `bool` for `seed_managed` breaks D-02's one-time backfill idempotency
**What goes wrong:** If `seed_managed` is a non-nullable `bool` with a SQL `DEFAULT FALSE` (like
`is_visible`/`is_hidden`), a freshly-ALTERed legacy row and a row correctly classified
"prod-owned" are indistinguishable — both read `false`. The backfill (D-02) then cannot tell
"not yet classified" from "classified as prod-owned," so it cannot be re-run safely and cannot
target a `WHERE seed_managed IS NULL` no-op-on-rerun pass.
**Why it happens:** Every other boolean column on this table (`is_visible`, `is_hidden`,
`is_evergreen`) uses a non-nullable default, so it is the path of least surprise to copy that
shape — but this is the ONE column that genuinely needs a third "unclassified" state.
**How to avoid:** Ship `seed_managed` as `BOOLEAN NULL` (Postgres) / `INTEGER NULL` (SQLite),
exactly mirroring `body_sha256 TEXT NULL`'s role as "not yet computed" vs a real value. Backfill
targets `WHERE seed_managed IS NULL`, directly reusing the `SetBodySha256IfNullAsync` idiom
(`ContentSiteIndexStore.cs:486-501`) as the template for a new `SetSeedManagedIfNullAsync` (or
equivalent batch variant).
**Warning signs:** A backfill unit test that re-runs the backfill twice and asserts zero writes
on the second pass will fail immediately if the column is non-nullable with a default.

### Pitfall 2: `ProdContentReader` cannot see `body_sha256` or `seed_managed` today
**What goes wrong:** The reconciler's body-hash-mismatch class needs prod's stored
`body_sha256`; the seed-drift and removal-apply logic need prod's `seed_managed`. Neither is in
`ProdContentReader.SelectAllSql` or its private `ContentSiteIndexRowData`/`ToContentSiteIndexRow`
mapping today (confirmed by direct read of `ProdContentReader.cs`) — a caller reading
`row.BodySha256` off a `ProdContentReader.ReadAllAsync()` result gets `null` unconditionally,
even when the prod column is populated.
**Why it happens:** `ProdContentReader` was built in P90 for the git-coverage audit + flag reads,
which never needed the hash column; nobody has needed it from Studio until this phase.
**How to avoid:** Extend `SelectAllSql`, `ContentSiteIndexRowData`, and `ToContentSiteIndexRow` in
`ProdContentReader.cs` to include `body_sha256` and `seed_managed` (and, while touching this file,
consider `awaiting_confirm_utc` for completeness, though this phase doesn't strictly need it).
This is a REQUIRED task, not a nice-to-have — the reconciler is unbuildable without it.
**Warning signs:** A `ProdContentReaderTests` test asserting `row.BodySha256` round-trips will
catch this immediately; the existing test file (`DeckFlow.Studio.Tests/Services/ProdContentReaderTests.cs`) is the natural home for the regression test.

### Pitfall 3: Reusing `ContentSyncDiffClassifier`'s equal-timestamp/UTC-direction logic for seed-drift is the WRONG model
**What goes wrong:** `ContentSyncDiffClassifier.Classify` (used by Pull) compares prod vs a LOCAL
DB's rows by `indexed_utc` direction (ProdNewer / Diverged / LocalOnly / MissingLocally) — it is
built for a DB-vs-DB reconciliation with a clear timestamp ordering. Seed-drift is a DIFFERENT
shape: prod rows (a DB) vs the git seed FILE's entries (not a DB, no meaningful
`indexed_utc`-direction comparison against a JSON snapshot). Force-fitting the seed-drift check
through `ContentSyncDiffClassifier` risks importing timestamp-direction semantics that don't apply.
**Why it happens:** `ContentSyncDiffClassifier` is the most prominent existing "diff two row sets
by natural key" component, so it's the obvious first thing to reach for.
**How to avoid:** For seed-drift, do a simple SET DIFFERENCE by natural key
(`seed_managed=true` prod rows whose natural key is absent from the seed file's entries) — reuse
only the natural-key indexing helper (`ContentNaturalKey.TryDerive` + the ` `-joined
dictionary-key pattern both `ContentSyncDiffClassifier` and `DirectPushCoordinator.ClassifyDiff`
already use), not the timestamp-direction branch logic.
**Warning signs:** If the seed-drift classifier ends up importing `SyncDiffKind` or comparing
`IndexedUtc` against a seed entry, that's a sign the wrong existing component was force-fit.

### Pitfall 4: `UpsertPreservingVisibilitySql`'s `body_sha256 = EXCLUDED.body_sha256` precedent does NOT automatically apply to `seed_managed`
**What goes wrong:** The existing comment at `ContentSiteIndexStore.cs:1159-1161` deliberately has
`body_sha256` OVERWRITTEN (not preserved) on `UpsertRowPreservingVisibilityAsync`, reasoning "a
corrected seed hash must propagate on reseed." If `seed_managed` copies that same
always-overwrite behavior blindly, it's actually CORRECT here too (every row passing through this
SQL IS by definition entering/re-entering the seed-managed set at seed-load time) — but this
should be a deliberate decision with its own comment, not an accidental copy-paste, since a future
reader needs to know WHY `seed_managed` is unconditionally `TRUE` on this specific SQL variant
(unlike `is_visible`/`is_hidden`/`is_evergreen`, which ARE preserved via
`content_site_index.is_visible` self-reference on this same statement).
**Why it happens:** `UpsertPreservingVisibilitySql` has three different override strategies
already coexisting in one statement (preserved-from-existing-row, overwritten-from-EXCLUDED,
and now this phase adds a fourth: always-`TRUE`-regardless-of-either-side) — easy to misplace a
column into the wrong bucket.
**How to avoid:** Bind `seedManaged` in the C# caller as a HARDCODED `true` local value at the
`ContentKbSeedLoader.BuildRow` call site (not derived from `row.SeedManaged` off the JSON entry —
the seed JSON should also carry `seedManaged` per D-01 wording "matching seed JSON field," but the
loaded-row's actual stamped DB value should be `true` unconditionally on this path regardless of
what the JSON says, since "this row is in the file we just loaded" already proves seed-managed).
Document this explicitly in the SQL comment, following the existing house convention (see the
`body_sha256` comment at line 1159 as the template for HOW to write this kind of comment).

### Pitfall 5: F-51-PG-01 timestamptz-vs-text class risk on the soft-hide Apply write
**What goes wrong:** The soft-hide Apply write only sets `is_visible = FALSE` — it should NOT
touch any timestamp column. If a future implementer adds a "hidden_utc" audit timestamp to
satisfy "retain... timestamps" (D-03), that new column must follow the existing dialect-guarded
TIMESTAMPTZ-vs-TEXT pattern (`pushed_to_prod_utc`, `awaiting_confirm_utc`) and must NEVER be
filtered on in a WHERE clause (per the `awaiting_confirm_utc` comment at
`ContentSiteIndexStore.cs:140-144`, which explicitly calls out this exact class of bug).
**Why it happens:** F-51-PG-01 has recurred multiple times across this project (see STATE.md
decision log) whenever a new timestamp column is added without following the established
guard.
**How to avoid:** D-03 says "retain... timestamps" meaning the EXISTING timestamps
(`published_utc`, `pushed_to_prod_utc`, `body_sha256`'s implicit provenance) are left untouched —
it does NOT require a NEW timestamp column this phase. If the planner does add one (e.g. for
audit trail beyond what the local discrepancy store's `first_seen_utc`/`last_seen_utc` already
covers), it must follow the dialect-guarded pattern and never appear in a WHERE clause, keyed only
by natural key like `StampPushedToProdAsync`/`SetAwaitingConfirmAsync`.
**Warning signs:** Any new SQL with `WHERE <new_timestamp_column> ...` against a Postgres-pointed
store is an immediate F-51-PG-01 red flag.

### Pitfall 6: The file-orphan class needs a REAL directory walk, which the codebase has never done for content-kb
**What goes wrong:** Every existing row↔file check (`ContentKbOrphanScanner`,
`GitBodyCoverageAudit`) starts from ROWS and checks whether each row's expected file exists — none
of them start from the FILESYSTEM and check whether each file has a corresponding row. A naive
`Directory.EnumerateFiles(contentKbRoot, "*.md", SearchOption.AllDirectories)` needs to exclude the
`content-kb/seed/index-seed.json` file itself (not a `.md`, safe) but must be scoped correctly
under the git checkout's `content-kb/` folder specifically (not the Studio local artifact root,
which is a DIFFERENT directory tree per `contentKbArtifactRoot` in `Program.cs:76` vs the
git-checkout `repoRoot/content-kb` `GitBodyCoverageAudit` reads from).
**Why it happens:** Studio has TWO separate content-kb directory trees in play: the local
harvest/distill artifact root (`studioDataDirectory/content-kb`) and the git checkout's
`repoRoot/content-kb` (what becomes `/app` after deploy). The reconciler's file-orphan class is
about the GIT tree (matching `GitBodyCoverageAudit`'s `repoRoot` parameter), not the local
artifact root.
**How to avoid:** Resolve `repoRoot` via `IGitRepository.ResolveRepoRootAsync` +
`StudioRepoLocator.ResolveStartDirectory()` exactly as `DirectPushCoordinator.CommitAndPushBodiesAsync`
and `GitBodyCoverageAudit`'s callers already do, then enumerate under
`Path.Combine(repoRoot, "content-kb")`, filtering to files matching the `ArtifactPathSafety`
`content-kb/`-prefix shape once made relative.
**Warning signs:** A file-orphan count of ~328 (matching the 2026-07-05 live audit number in
STATE.md) is the expected order of magnitude; a count near 0 or in the tens of thousands signals
the wrong root was walked.

## Code Examples

### `SetBodySha256IfNullAsync` — the exact template for a `SetSeedManagedIfNullAsync` backfill write
```csharp
// Source: DeckFlow.Core/Content/ContentSiteIndexStore.cs:486-501
public async Task<int> SetBodySha256IfNullAsync(long id, string bodySha256, CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(bodySha256);
    await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

    await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    return await connection.ExecuteAsync(new CommandDefinition(
        """
        UPDATE content_site_index
           SET body_sha256 = @bodySha256
         WHERE id = @id
           AND body_sha256 IS NULL;
        """,
        new { bodySha256, id },
        cancellationToken: cancellationToken)).ConfigureAwait(false);
}
```
A `SetSeedManagedIfNullAsync(long id, bool seedManaged, ...)` following this exact shape
(`WHERE id = @id AND seed_managed IS NULL`) is the natural per-row backfill write; the D-02
backfill component (host-agnostic, mirroring `ContentBodyHashBackfill`) would call
`GetAllRowsAsync`, compute membership against the parsed seed file, then call this per un-classified
row.

### `ContentSyncDiffClassifier`'s natural-key indexing — the reusable piece for seed-drift
```csharp
// Source: DeckFlow.Core/Content/ContentSyncDiffClassifier.cs:85-110 — reuse the KEY-BUILDING shape only
private static Dictionary<string, ContentSiteIndexRow> IndexByNaturalKey(
    IReadOnlyList<ContentSiteIndexRow> rows, ILogger? logger)
{
    var map = new Dictionary<string, ContentSiteIndexRow>(StringComparer.Ordinal);
    foreach (var row in rows)
    {
        if (!ContentNaturalKey.TryDerive(row, out var nk))
        {
            logger?.LogWarning("Skipping content row with no natural key...", row.Title, row.Source);
            continue;
        }
        map.TryAdd($"{nk.Type} {nk.Value}", row);
    }
    return map;
}
```
For seed-drift, build the SAME shape of dictionary from the parsed `index-seed.json` entries
(keyed by `NaturalKeyType`/`NaturalKeyValue`, both already present on `ContentIndexExportRow`) and
set-diff it against `seed_managed=true` prod rows indexed the same way.

### `GitBodyCoverageAudit` — nearest existing analog for the published-orphan class
```csharp
// Source: DeckFlow.Studio/Services/GitBodyCoverageAudit.cs:25-65 (full file read; reuse this loop shape)
var prodRows = await _prodReader.ReadAllAsync(prodConnectionString ?? string.Empty, cancellationToken).ConfigureAwait(false);
foreach (var row in prodRows)
{
    if (!string.Equals(row.ApprovalStatus, "approved", StringComparison.Ordinal) || !row.IsVisible) continue;
    var isPresent = ArtifactPathSafety.TryBuildContainedPath(repoRoot, row.ArtifactPath, out var fullPath)
        && File.Exists(fullPath);
    if (!isPresent) { /* published-orphan */ }
}
```
This is the published-orphan class almost verbatim — generalize rather than duplicate; either the
reconciler composes `IGitBodyCoverageAudit` as a dependency, or its loop body is lifted into the
new pure classifier (preferred, since the classifier should be I/O-free and this loop's only I/O
is the injected `prodRows` + a path check that the ORCHESTRATOR, not the classifier, should
perform up front and pass in as a `HashSet<string>` of existing file paths).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | SYNC-12's "seed reload handles removals" is satisfied by the NEW Studio reconciler's gated Apply action, NOT by making `ContentKbSeedLoader.LoadIfPresentAsync` (web boot) delete/hide anything automatically | Open Question 1 below | If the planner instead wires automatic boot-time removal into `ContentKbSeedLoader`, it reintroduces exactly the "bad seed at boot could delete live rows" risk Codex flagged as HIGH in the roadmap doc, which D-04/D-08/D-09's entire operator-gated design exists to prevent. This is a locked-decision-adjacent reading, not a verified fact — flagged for discuss-phase/planner confirmation. |
| A2 | The pure discrepancy classifier belongs in `DeckFlow.Core/Content/` (new file), not inside Studio | Architecture Patterns, Recommended Project Structure | Low risk — if placed in Studio instead, it still works, just loses the "zero-DB-mock unit test" benefit `ContentSyncDiffClassifier` demonstrates is valuable in this codebase |
| A3 | `seed_managed` should be `bool?` (nullable), not a non-nullable bool with a status-string alternative | Pitfall 1 | Low risk — this is explicitly flagged as Claude's Discretion in CONTEXT.md; the `SetBodySha256IfNullAsync` precedent is a strong, directly-analogous justification, but the planner could reasonably choose a status-string (`'unclassified'/'seed'/'prod'`) instead if it wants richer future states |
| A4 | File-orphan enumeration should walk `repoRoot/content-kb`, not the Studio local artifact root | Pitfall 6 | Medium risk if wrong — walking the wrong tree produces a meaningless orphan count; verified against `GitBodyCoverageAudit`'s established `repoRoot` convention and the git=SoT design stance, so confidence is HIGH this is correct, not truly assumed |

**A1 is the load-bearing assumption in this research** — everything else is a direct code-reading
finding. Recommend the planner explicitly re-confirm A1 with the user during `/gsd:plan-phase`
convergence if there is any doubt, since it determines whether `ContentKbSeedLoader.cs` gets a
removal-write or stays additive-plus-marker-stamp-only.

## Open Questions

1. **Does SYNC-12's "seed reload handles removals" literally mean `ContentKbSeedLoader` performs
   the soft-hide, or does the Studio reconciler's Apply action?**
   - What we know: REQUIREMENTS.md's SYNC-12 text says "Seed reload handles removals... replacing
     additive-only upsert." The ORIGINAL design doc (`kb-prod-sync-fix-design.md` #4/C2) also frames
     this as `ContentKbSeedLoader` gaining delete behavior. But 91-CONTEXT.md's locked decisions
     (D-04 Studio-operator-action, D-08 two-step re-validated apply, D-09 flag-gates-only-the-apply)
     describe an entirely operator-triggered, dry-run-then-Apply flow that reads far more like a
     NEW Studio action than an automatic change to the web boot-time seed loader. The roadmap doc's
     own Codex adjustment explicitly says: "add a row-level seed-management marker FIRST; ship
     delete as dry-run/reconcile-only before destructive apply" — i.e., the delete moves INTO the
     reconciler, out of the automatic seed-reload path.
   - What's unclear: Whether `ContentKbSeedLoader.LoadIfPresentAsync` needs ANY behavior change
     beyond stamping `seed_managed = true` on the rows it upserts (a SYNC-17 concern, not a
     SYNC-12 one), or whether it also needs to be the trigger point for SYNC-12's removal logic.
   - Recommendation: Plan SYNC-12 as delivered by the Studio reconciler's Apply action (per A1
     above) — `ContentKbSeedLoader` gets ONLY the `seed_managed = true` stamp (a SYNC-17 change).
     This is the safer, locked-decision-consistent reading. If the planner or user prefers the
     literal "seed reload deletes" reading, that would need `ContentKbSeedLoader` to run the SAME
     re-validated diff-then-soft-hide logic behind the SAME `sync.reconcile` flag at web boot,
     which is a materially different (and higher-risk, since it's not operator-observed)
     implementation — surface this choice explicitly in the plan or a fast-path decision question.

2. **Should the pure classifier consume `IGitBodyCoverageAudit` as a dependency (composition) or
   have its published-orphan loop re-derived (duplication) inside the new classifier?**
   - What we know: `IGitBodyCoverageAudit.RunAsync` already does prod-read + approved/visible
     filter + path-safety-checked file-existence, returning `GitBodyCoverageReport`. This is
     Studio-side (has I/O), while the recommended new classifier is Core-side (I/O-free).
   - What's unclear: Whether the orchestrator should call `IGitBodyCoverageAudit` directly for the
     published-orphan class (reuse via composition, but then that ONE class's data source is
     structurally different from the other three, which all flow through the new pure classifier)
     or whether `GitBodyCoverageAudit`'s loop body should be duplicated (as a `HashSet<string>` of
     existing paths) inside the pure classifier for a uniform four-class implementation.
   - Recommendation: Have the Studio orchestrator build ONE `HashSet<string>` of existing git-tree
     file paths (reused for BOTH published-orphan and file-orphan checks, since both need the same
     file-existence data, just checked in opposite directions) and pass it into the pure
     classifier alongside prod rows and seed entries — this keeps the classifier I/O-free and
     avoids computing the file-existence set twice. `IGitBodyCoverageAudit` itself can stay as-is
     (its P90 purpose — the pre-flip audit — still stands independently) or be superseded by the
     new reconciler's published-orphan class; the planner should decide whether to retire/redirect
     it or leave it as a lighter-weight standalone check.

3. **Does the local discrepancy store need Postgres-dialect support, or is SQLite-only acceptable?**
   - What we know: D-05 mandates the store is local-to-Studio; Studio's `content-kb.db` is
     ALWAYS constructed via the SQLite-path constructor in `Program.cs` (never pointed at
     Postgres) — no existing local store in Studio has ever needed dialect-guarding.
   - What's unclear: Whether following `ContentHarvestRunStore`'s fully-dialect-capable pattern
     (via `RelationalDatabaseConnection`, even though only ever constructed as SQLite) is worth
     the consistency, or whether a SQLite-only implementation is acceptable/simpler.
   - Recommendation: Mirror `ContentHarvestRunStore`'s dialect-capable-but-SQLite-constructed
     pattern for consistency with the other 9 local stores and to keep the door open for a future
     test seam using `RelationalDatabaseConnection.FromSqlitePath` — this costs nothing extra
     given `RelationalDatabaseConnection` already handles both dialects uniformly.

## Environment Availability

Not applicable — no new external dependency, tool, runtime, or service is introduced this phase.
All work is `dotnet build`-verifiable in-repo. The existing `Studio:ProdConnectionString` /
`Studio:Scp:*` / `Studio:AdminUser`/`Studio:AdminPassword` config keys (already required for
DirectPush) are reused unchanged for the reconciler's prod read + Apply write; no new config keys
are needed beyond what `ProdContentReader`/`IProdStoreFactory` already consume.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (all three test projects: `DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`, `DeckFlow.Studio.Tests`) |
| Config file | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`, `DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj` |
| Quick run command | `dotnet build` (VSTest unreliable in WSL per `.planning/codebase/TESTING.md`; build-clean is the primary gate) |
| Full suite command | `dotnet test` (cross-platform; run via Windows `dotnet.exe` over WSL per CLAUDE.md) |

### Phase Requirement → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SYNC-17 | `seed_managed` dialect-guarded ALTER (SQLite + Postgres), idempotent re-run | unit | `dotnet test --filter ContentSiteIndexStoreTests` | ✅ `DeckFlow.Core.Tests/ContentSiteIndexStoreTests.cs` (extend) |
| SYNC-17 | `seed_managed=true` stamped on DirectPush prod write | unit | `dotnet test --filter DirectPushCoordinatorTests` | ✅ existing coordinator test file (verify exact name via `find DeckFlow.Studio.Tests -iname "*DirectPush*"`) |
| SYNC-17 | `seed_managed=true` stamped on `ContentKbSeedLoader.LoadIfPresentAsync` | unit | `dotnet test --filter ContentKbSeedLoaderTests` | ✅ `DeckFlow.Web.Tests/ContentKbSeedLoaderTests.cs` (extend) |
| SYNC-17 | Backfill classifies by current seed membership, idempotent (`IS NULL` re-run = zero writes) | unit | new test class | ❌ Wave 0 — new `SeedManagedBackfillTests` (Core.Tests), mirrors `ContentBodyHashBackfillTests` if one exists (verify) |
| SYNC-11 | 4 discrepancy classes classify correctly from fixed in-memory inputs | unit | new test class | ❌ Wave 0 — new `ContentKbReconcileClassifierTests` (Core.Tests), zero I/O, mirrors `ContentSyncDiffClassifierTests` |
| SYNC-11 | Idempotent re-run — running reconcile twice on unchanged inputs produces zero duplicate discrepancy rows | unit/integration | new test class | ❌ Wave 0 — new `ContentKbReconcileStoreTests` (Studio.Tests) |
| SYNC-11 | Resolution-by-absence — a discrepancy no longer detected is marked resolved, not deleted/re-created | unit | new test class | ❌ Wave 0 — same file as above |
| SYNC-11 | Scope-tagged partial run never false-resolves discrepancies outside its scope | unit | new test class | ❌ Wave 0 — same file as above |
| SYNC-11 | `ProdContentReader` round-trips `body_sha256`/`seed_managed` | unit | `dotnet test --filter ProdContentReaderTests` | ✅ `DeckFlow.Studio.Tests/Services/ProdContentReaderTests.cs` (extend — this is the regression test for Pitfall 2) |
| SYNC-12 | Apply soft-hides ONLY `seed_managed=true` rows still absent from a re-validated diff | unit | new test class | ❌ Wave 0 — new `ReconcileCoordinatorTests` (Studio.Tests), mirrors `DirectPushCoordinator`/`PublishCoordinator` test shape |
| SYNC-12 | Apply is a no-op (does not soft-hide) when `sync.reconcile` reads OFF or indeterminate (null) | unit | same file as above | ❌ Wave 0 |
| SYNC-12 | Soft-hide never touches `seed_managed=false` (prod-owned) rows, even if absent from seed | unit | same file as above — the core SYNC-17 safety invariant | ❌ Wave 0 |
| SYNC-12 | F-51-PG-01 regression — if any new timestamp column is added, direction-comparison test on Postgres dialect | integration | `dotnet test --filter <NewColumnName>` against `PostgresContainerFixture` if a new timestamp is introduced | Only needed if Pitfall 5's optional new column is added |

### Sampling Rate
- **Per task commit:** `dotnet build` (clean, 0 warnings) — the house convention per CLAUDE.md/TESTING.md.
- **Per wave merge:** `dotnet test` full suite across all three test projects.
- **Phase gate:** Full suite green (Core / Web / Studio, matching the P88/P89/P90 precedent of
  reporting exact green counts in STATE.md) before `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `DeckFlow.Core.Tests/ContentKbReconcileClassifierTests.cs` — covers SYNC-11's 4 discrepancy classes, pure/no-I/O
- [ ] `DeckFlow.Core.Tests/SeedManagedBackfillTests.cs` — covers SYNC-17's D-02 backfill idempotency
- [ ] `DeckFlow.Studio.Tests/Services/ContentKbReconcileStoreTests.cs` — covers SYNC-11's idempotent upsert, resolution-by-absence, scope tags
- [ ] `DeckFlow.Studio.Tests/ViewModels/ReconcileCoordinatorTests.cs` — covers SYNC-12's dry-run/Apply gating, re-validated apply, flag-indeterminate-refuses-apply
- [ ] Extend `DeckFlow.Studio.Tests/Services/ProdContentReaderTests.cs` — regression-guards Pitfall 2 (`body_sha256`/`seed_managed` round-trip)
- [ ] Extend `DeckFlow.Core.Tests/ContentSiteIndexStoreTests.cs` — dialect-guarded `seed_managed` ALTER, both dialects (reuse the existing `body_sha256` test cases as the template)
- [ ] Extend `DeckFlow.Web.Tests/ContentKbSeedLoaderTests.cs` — `seed_managed=true` stamped on every loaded row
- [ ] No new test framework or fixture needed — `PostgresContainerFixture.cs` (`DeckFlow.Web.Tests/Integration/`) already exists for any dialect-parity test that needs a real Postgres instance.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | No new external-facing endpoint; the reconciler is a Studio (local operator Blazor Server app) action only, same trust boundary as existing DirectPush/Publish/Pull actions |
| V3 Session Management | No | Studio is a single-operator local tool; no new session surface |
| V4 Access Control | Yes | Apply (destructive soft-hide) MUST be gated behind `sync.reconcile` read via `IProdContentReader.TryReadFlagAsync` (indeterminate → refuse), per D-09; dry-run/detection stays always-available (read-only, low-risk) per the same decision |
| V5 Input Validation | Yes | Every artifact-path derived from a prod row OR a filesystem enumeration MUST pass through `DeckFlow.Studio.Services.ArtifactPathSafety` before any `File.Exists`/read — both directions (row→file for published-orphan, file→row for file-orphan) are traversal-risk surfaces if a stored/enumerated path is trusted blindly |
| V6 Cryptography | Yes | Body-hash-mismatch reuses `ContentSiteIndexContentSignature.ComputeBodySha256` (SHA-256) — no new cryptographic primitive is introduced or hand-rolled |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal via a maliciously/accidentally crafted `ArtifactPath` in a prod row | Tampering / Elevation of Privilege | `ArtifactPathSafety.TryBuildContainedPath` — already the sole sanctioned guard, rejects rooted/`..`-traversal/non-`content-kb/`-prefixed paths, containment-verified against the resolved root |
| Accidental mass-deletion of live content via a bad/regressed git seed | Tampering / Denial of Service | SYNC-17 marker (only `seed_managed=true` rows are ever eligible) + D-08 two-step re-validated apply (Apply re-runs the diff fresh, never trusts a stale dry-run snapshot) + D-09 flag gate on the write only |
| Prod connection-string / credential leakage via logs or exceptions | Information Disclosure | Existing D-07 convention (`ProdContentReader`'s `catch` blocks never surface connection string or exception detail) MUST be preserved in any new reconciler logging — do not introduce a new logging path that bypasses this |
| Reconciler running unbounded/expensive queries against prod on every dry-run | Denial of Service | `ProdContentReader.ReadAllAsync` is already a single plain SELECT with no WHERE-on-timestamp (no F-51-PG-01 exposure); the reconciler should call it ONCE per run (not per-row), matching `GitBodyCoverageAudit`'s existing usage pattern |
| An indeterminate (`null`) flag read being misinterpreted as license to proceed with the destructive Apply | Tampering | Explicit fail-safe-to-refuse handling for `TryReadFlagAsync() == null` in the Apply path, mirroring (but inverted from) `VerifyAndPublishAsync`'s existing indeterminate-handling precedent |

## Sources

### Primary (HIGH confidence — direct code reads this session)
- `.planning/phases/91-reconcile-seed-lifecycle/91-CONTEXT.md` — all locked decisions (D-01..D-10), canonical refs, scope
- `.planning/REQUIREMENTS.md` — SYNC-17/SYNC-11/SYNC-12 exact text, design stance, minimal-additive-prod-DDL cap
- `.planning/STATE.md` — live prod drift numbers (106 rows/36 in seed/70 not reconstructable, 328 file-orphans, 32 mojibake) used to sanity-check expected discrepancy counts
- `.planning/phases/90-directpush-correctness-seed-sync/90-CONTEXT.md` — D-01/D-02/D-04/D-05/D-10/D-11 (git=SoT, web-DB flag + Studio accessor precedent, local durable state precedent)
- `.planning/phases/89-content-hash-foundation/89-CONTEXT.md` — body_sha256 + unified signature + `ComputeBodySha256` provenance
- `docs/research/kb-prod-sync-fix-design.md` — §4 reconciliation design, C2 additive-only-upsert weakness, Codex HIGH seed-delete-unsafe finding
- `docs/research/kb-prod-sync-roadmap.md` — original P4 (now Phase 91) scope + the Codex sequencing adjustment that moved delete OUT of automatic seed-reload and INTO a gated reconciler
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — full file read: DDL pattern, all 3 upsert SQL variants, `BuildUpsertParameters`, `SetVisibilityBySourceAsync`/`SetVisibilityAsync`, `SetBodySha256IfNullAsync`
- `DeckFlow.Core/Content/ContentKbOrphanScanner.cs`, `ContentSyncDiffClassifier.cs` — full file reads, mined for classification shapes per D-07
- `DeckFlow.Core/Reporting/ReconciliationReporter.cs` — full file read; confirmed this is deck-comparison domain, not Content-KB (Anti-Pattern noted)
- `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs`, `ContentIndexExportRow.cs`, `ContentBodyHashBackfill.cs`, `ContentHarvestRunStore.cs` — full file reads
- `DeckFlow.Studio/Services/IProdContentReader.cs`, `ProdContentReader.cs`, `IGitBodyCoverageAudit.cs`, `GitBodyCoverageAudit.cs`, `ArtifactPathSafety.cs` — full file reads; confirmed the `body_sha256`/`seed_managed` gap in `ProdContentReader` (Pitfall 2) directly
- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs`, `PublishCoordinator.cs` — full file reads; confirmed the shared-SQL/local-vs-prod caller ambiguity (Pattern 1) via direct grep of every call site
- `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` — full file read
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` — full file read; `sync.directpush-gitbody` grep against `FeatureFlagStore.cs` for the exact seed-SQL locations to mirror
- `DeckFlow.Studio/Program.cs`, `AutoApproveSettingsStore.cs` — confirmed the 9-sibling local-store `content-kb.db` pattern
- `.planning/codebase/TESTING.md` — test framework/commands
- `.planning/config.json` — `nyquist_validation: true`, no `security_enforcement: false` (both sections included)

### Secondary / Tertiary
None — no WebSearch/Context7 lookups were needed; this phase is entirely in-house code
extension with no external library, framework, or documentation dependency.

## Metadata

**Confidence breakdown:**
- Standard stack: N/A (no external stack) - no new packages
- Architecture: HIGH - every pattern cited is a direct read of existing, working code in this repo
- Pitfalls: HIGH - Pitfalls 1, 2, 4, 6 are confirmed defects/gaps found by direct code inspection this session (not speculative); Pitfalls 3, 5 are precedent-grounded design guidance
- Open Question 1 (A1): MEDIUM - this is a genuine ambiguity between the requirement text and the locked decisions, not a code-verifiable fact; flagged prominently for planner/user confirmation

**Research date:** 2026-07-08
**Valid until:** No expiry concern — all findings are grounded in the current state of this
repository's own code, which only changes via this project's own commits. Re-verify only if
Phase 90 code changes land between this research and plan execution.

## RESEARCH COMPLETE

**Phase:** 91 - Reconcile + Seed Lifecycle
**Confidence:** HIGH

### Key Findings
- `seed_managed` must be threaded as a per-row bound parameter (new `ContentSiteIndexRow.SeedManaged` property), NOT hardcoded into the shared upsert SQL — that SQL is called from both a local-distill path (must stay false) and prod-publish paths (must be true), confirmed by direct grep of every call site.
- `ProdContentReader` (Studio's only prod-read seam) does not currently select `body_sha256` or `seed_managed` — this is a required extension, not an edge case; the reconciler's body-hash-mismatch class is unbuildable without it.
- Recommend `seed_managed` as nullable (`bool?`/`BOOLEAN NULL`), mirroring `body_sha256`'s null-means-unclassified role, so the D-02 backfill can reuse the exact `SetBodySha256IfNullAsync` idempotent-write idiom already established.
- The reconciler splits cleanly into a pure, I/O-free classifier in `DeckFlow.Core` (mirrors `ContentSyncDiffClassifier`'s shape) plus a Studio I/O orchestrator (mirrors `GitBodyCoverageAudit`) plus a new local SQLite table in `content-kb.db` (mirrors `ContentHarvestRunStore`'s schema-ensure pattern) — every seam needed already has a direct precedent in the codebase.
- Genuine open question (flagged, not resolved): whether SYNC-12's removal write belongs in `ContentKbSeedLoader` (automatic, web-boot) or the new Studio reconciler's Apply action (operator-gated) — the locked D-04/D-08/D-09 decisions strongly favor the latter, and this research recommends that reading, but it is a requirement-text-vs-decision ambiguity worth an explicit planner/user confirmation rather than a silently-assumed fact.

### File Created
`.planning/phases/91-reconcile-seed-lifecycle/91-RESEARCH.md`

### Confidence Assessment
| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | N/A | No new external dependency this phase |
| Architecture | HIGH | Every recommended pattern is a direct extension of code read in full this session |
| Pitfalls | HIGH | 4 of 6 pitfalls are confirmed live defects/gaps (not speculative); grounded in direct file reads |

### Open Questions
1. Whether `ContentKbSeedLoader` (web boot) or the Studio reconciler's Apply action performs the SYNC-12 soft-hide write (see Open Questions §1 / Assumption A1) — recommend the latter, flagged for explicit confirmation.
2. Whether the published-orphan class should compose `IGitBodyCoverageAudit` directly or share a file-existence set built once by the orchestrator (see Open Questions §2) — recommend the shared-set approach.
3. Whether the local discrepancy store should be dialect-capable-but-SQLite-only (mirroring `ContentHarvestRunStore`) or SQLite-only-typed (see Open Questions §3) — recommend mirroring the existing pattern for consistency.

### Ready for Planning
Research complete. Planner can now create PLAN.md files for Phase 91.
