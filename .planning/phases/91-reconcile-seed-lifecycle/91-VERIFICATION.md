---
phase: 91-reconcile-seed-lifecycle
verified: 2026-07-09T23:59:00Z
status: human_needed
score: 10/10 must-haves verified (code-level); 2 items require operator sign-off (91-09, not executed by design)
overrides_applied: 0
human_verification:
  - test: "91-09 Task 1 — dry-run detection against a real fixture checkout"
    expected: "All four discrepancy classes render, D-06 report file exists, prod state unchanged, run succeeded with sync.reconcile OFF"
    why_human: "Requires an operator git checkout, live Studio session, and visual/state inspection — explicitly a checkpoint:human-verify task in 91-09-PLAN.md, not run by this verifier per its own instructions"
  - test: "91-09 Task 2 — gated re-validated Apply + prod-owned safety against a real fixture"
    expected: "Stale Apply refused; flag-ON Apply soft-hides only seed-owned drift rows (retained); a prod-owned row stays visible; flag OFF/indeterminate refuses"
    why_human: "Requires mutating a live fixture, flipping the sync.reconcile web-DB flag, and confirming live prod-row-visibility outcomes — explicitly a checkpoint:human-verify task in 91-09-PLAN.md"
---

# Phase 91: Reconcile + Seed Lifecycle — Verification Report

**Phase Goal:** Ship the seed-lifecycle safety layer that lets the operator detect and correct
prod↔git↔seed drift, and — for the first time — let a git seed *remove* content from prod, without
any risk to prod-only rows. Requirements: SYNC-17 (seed-ownership marker), SYNC-11 (reconcile
detection: 4 discrepancy classes), SYNC-12 (`sync.reconcile` flag + gated Apply).

**Verified:** 2026-07-09
**Status:** human_needed (all 8 autonomous code plans verified against source; the phase's own
manual operator-gate plan, 91-09, has not been executed — its two `checkpoint:human-verify` tasks
remain outstanding by design)
**Scope:** Plans 91-01 through 91-08 (all `autonomous: true`). Plan 91-09 (`autonomous: false`)
was NOT executed, per the verification objective's explicit instruction — only the code it depends
on was checked.

## Method

This is an INITIAL verification (no prior `91-VERIFICATION.md` existed). Every truth below was
checked by reading the actual current source at HEAD (`55218cd7`), not by trusting SUMMARY.md
prose. Targeted test suites were re-run live in this session (not merely re-quoted from a prior
SUMMARY), and a full-solution build was re-run live. All commit hashes cited in the eight SUMMARYs
were confirmed present in `git log`.

```
dotnet.exe test DeckFlow.Core.Tests --filter "Reconcile|SeedManaged|SeedIndexFileReader"
  → Passed: 53, Failed: 0, Skipped: 0
dotnet.exe test DeckFlow.Studio.Tests --filter "Reconcile"
  → Passed: 34, Failed: 0, Skipped: 0
dotnet.exe test DeckFlow.Web.Tests --filter "Reconcile|FeatureFlag"
  → Passed: 63, Failed: 0, Skipped: 0
dotnet.exe build DeckFlow.sln --no-restore
  → Build succeeded. 0 Warning(s). 0 Error(s).
```

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `content_site_index` carries a `seed_managed` marker column, idempotent on BOTH SQLite and Postgres; null-only backfill setter never overwrites a classified row | ✓ VERIFIED | `ContentArtifactSpec.cs:174` `bool? SeedManaged`; `ContentSiteIndexStore.cs:152-161` dialect-guarded `ALTER TABLE ... ADD COLUMN seed_managed BOOLEAN NULL` (Postgres) / `INTEGER NULL` (SQLite), guarded by `if (!columns.Contains("seed_managed"))`; `SetSeedManagedIfNullAsync` (line 522) issues `UPDATE ... WHERE id=@id AND seed_managed IS NULL`. `SeedManagedSchemaTests`/`SeedManagedWritePathTests` green (part of the 53/53 Core run). |
| 2 | `SeedIndexFileReader` distinguishes seed-present-and-parsed (even valid EMPTY) from absent/unreadable/parse-failed via `SeedAvailable`; NO bare-set overload | ✓ VERIFIED | `SeedIndexFileReader.cs` — the ONLY public member is `static SeedIndexReadResult Read(...)`; missing file → `(false, EmptyKeySet)`; catch block on `IOException/UnauthorizedAccessException/JsonException` → `(false, EmptyKeySet)`; successful parse (incl. zero entries) → `(true, keys)`. No `ReadNaturalKeys` or bare-set method exists anywhere in the file (grep-confirmed). |
| 3 | Write paths (`ContentKbSeedLoader`, DirectPush `WriteContentAsync`) stamp `seed_managed=true`; `index-seed.json` carries `seedManaged` via shared factory; `ProdContentReader` round-trips `body_sha256` + `seed_managed` | ✓ VERIFIED | `ContentKbSeedLoader.cs:90` `SeedManaged = true` (hardcoded, not from JSON — Pitfall 4 honored); `DirectPushCoordinator.cs:252` `r with { SeedManaged = true }`; `ContentIndexExportRow.cs:52,81` field + hardcoded-true in `From()`; `ProdContentReader.cs:25,185-186,210-211` selects/maps both columns. |
| 4 | Startup backfill classifies legacy NULL rows ONLY when seed available; writes ZERO rows when unavailable; valid EMPTY seed classifies; idempotent; never crashes; runs on web AND Studio | ✓ VERIFIED | `SeedManagedBackfill.cs`: `RunAsync` checks `!membershipResult.SeedAvailable` and returns BEFORE `GetAllRowsAsync` (zero reads/writes) when unavailable; throwing membership source caught and folded into the same unavailable path; valid-empty seed still runs the classify loop (all → false); per-row `if (row.SeedManaged is not null) continue;` = idempotent. Wired in `DeckFlow.Web/Program.cs:283` (after seed load, line 269) and `DeckFlow.Studio/Program.cs:244`. |
| 5 | `ContentKbReconcileClassifier` is pure/IO-free, emits 4 classes, seed-drift GATED on `SeedAvailable`, file-orphan by artifact-path identity, deterministic IDs | ✓ VERIFIED | `ContentKbReconcileClassifier.cs`: static, zero I/O, takes only in-memory collections. Seed-drift branch (line 92) requires `seedIndex.SeedAvailable && row.SeedManaged == true` before any set-difference; unavailable → logged skip (line 59-68), zero seed-drift, other 3 classes still computed (published-orphan/file-orphan loops are unconditional). File-orphan (line 120-132) matches by `row.ArtifactPath` set membership only — `ContentNaturalKey.TryDerive` is never called in that direction. `ContentKbReconcileDiscrepancy.BuildId` (in `ContentKbReconcileDiscrepancy.cs:72-92`) is a pure string join, order-independent by construction. |
| 6 | `ContentKbReconcileStore`: idempotent upsert by deterministic ID; absent-on-rerun→resolved (not deleted); scope isolation; exposes `Kind` | ✓ VERIFIED | `ContentKbReconcileStore.cs`: `PersistRunAsync` runs upsert (`ON CONFLICT (discrepancy_id) DO UPDATE`) + resolution-by-absence (`SET resolved_utc=@now WHERE scope_tag=@scopeTag AND resolved_utc IS NULL AND discrepancy_id NOT IN @seenIds`) in one transaction; empty-seen case uses a dedicated `ResolveAllInScopeSql` (no reliance on empty-IN-list dialect quirks); no `DELETE` statement anywhere in the file; `GetOpenAsync` maps the persisted `kind` TEXT column back to `ContentKbReconcileKind` via `ParseKind`. |
| 7 | `ContentKbReconcileOrchestrator` dry-run: reads prod once, walks `repoRoot/content-kb` git tree, availability-aware seed read, `ArtifactPathSafety` on every path, emits D-06 report, returns `ReconcileDryRunResult(SeedAvailable, Discrepancies)`; zero seed-drift + notice when unavailable | ✓ VERIFIED | `ContentKbReconcileOrchestrator.cs`: `_prodReader.ReadAllAsync` called exactly once per `RunDryRunAsync`; `ReadGitContentTree` walks `Path.Combine(repoRoot,"content-kb")` (not any Studio-local artifact root), guards every candidate path with `ArtifactPathSafety.IsSafeArtifactPath` before adding to either collection; `seedIndex = SeedIndexFileReader.Read(seedFilePath, _logger)`; returns `new ReconcileDryRunResult(seedIndex.SeedAvailable, discrepancies)` — `SeedAvailable` sourced straight from the reader, not inferred from `discrepancies`; `BuildReportText` renders a "SEED UNAVAILABLE" advisory in place of the seed-drift section when `!result.SeedAvailable`. |
| 8 | `ReconcileCoordinator` + `/reconcile` page: dry-run read-only with `sync.reconcile` OFF, no destructive write, seed-unavailable notice | ✓ VERIFIED | `ReconcileCoordinator.RunDryRunAsync` delegates straight to the orchestrator, reads no flag; `Reconcile.razor` renders a `SEED UNAVAILABLE` warning card (`data-testid="seed-unavailable-banner"`) in place of the Seed Drift card when `!_result.SeedAvailable`; page copy explicitly states "READ-ONLY / detection-only … no visibility change, no prod DDL." |
| 9 | `sync.reconcile` registered + seeded OFF both dialects; `ApplyRemovalsAsync` re-runs seed-drift diff fresh, soft-hides ONLY still-present `seed_managed=true` rows; refuses on flag false AND null; refuses BEFORE any hide when seed unavailable; never hides `seed_managed=false` | ✓ VERIFIED | `FeatureFlagCatalog.cs:100` + `FeatureFlagStore.cs:234,275` (`('sync.reconcile', FALSE)` / `(0)`); `ReconcileCoordinator.ApplyRemovalsAsync` gate order confirmed by direct read: (1) `flag != true` → refuse (both `false` and `null` refuse, since only `== true` proceeds); (2) fresh `RunDryRunAsync` → `if (!fresh.SeedAvailable) return Refused(SeedUnavailable)` BEFORE the stale-check; (3) stale-check via `SetEquals` scoped to `Kind == SeedDrift` only; (4) a SECOND independent `seed_managed` re-check against a freshly-read prod row (`seedManagedByKey[...] == true`) before any key enters `keysToHide` — a prod-owned row cannot be hidden even under a hypothetical classifier regression; hide reuses `SetVisibilityAsync(keys, visible:false)` (natural-key batch, no timestamp column touched, confirmed by reading `ContentSiteIndexStore.cs:876-912`). |
| 10 | The reviewed set feeding Apply is scoped to seed-drift only (Codex-MED); a mixed-class dry-run does not false-reject | ✓ VERIFIED | `ReconcileCoordinator.ApplyRemovalsAsync`: `freshRemovals = fresh.Discrepancies.Where(d => d.Kind == ContentKbReconcileKind.SeedDrift)` — only this filtered set participates in the `SetEquals` stale-check on either side; `Reconcile.razor.cs` builds `reviewedRemovalDiscrepancyIds` from `Items(_result, ContentKbReconcileKind.SeedDrift)` only, never the full discrepancy list. |

**Score:** 10/10 code-level truths verified.

### Requirement Verdicts

| Requirement | Verdict | Basis |
|---|---|---|
| **SYNC-17** (seed-ownership marker) | **PASS** | Truths 1-4 fully verified against source + green tests (91-01/02/03). |
| **SYNC-11** (reconcile detection: 4 discrepancy classes) | **PASS** | Truths 5-8 fully verified against source + green tests (91-04/05/06/07). |
| **SYNC-12** (`sync.reconcile` flag + gated Apply) | **PASS** | Truths 9-10 fully verified against source + green tests (91-08). |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Core/Content/SeedIndexFileReader.cs` | 3-outcome sole-read-API seed reader | ✓ VERIFIED | Present, matches contract exactly, no bare-set overload |
| `DeckFlow.Core/Content/SeedManagedBackfill.cs` | Availability-gated D-02 backfill | ✓ VERIFIED | Present, gate confirmed at code level |
| `DeckFlow.Core/Content/ContentKbReconcileClassifier.cs` | Pure 4-class classifier | ✓ VERIFIED | Present, I/O-free, seed-drift gated |
| `DeckFlow.Core/Content/ContentKbReconcileDiscrepancy.cs` | Discrepancy records + deterministic ID | ✓ VERIFIED | Present, U+0000-keyed `BuildId` |
| `DeckFlow.Studio/Services/ContentKbReconcileStore.cs` | Local idempotent discrepancy store | ✓ VERIFIED | Present, transactional upsert + resolve-by-absence |
| `DeckFlow.Studio/Services/ContentKbReconcileOrchestrator.cs` | I/O orchestrator, D-06 report | ✓ VERIFIED | Present, single prod read, path-safety guarded |
| `DeckFlow.Studio/ViewModels/ReconcileCoordinator.cs` | Dry-run + gated Apply coordinator | ✓ VERIFIED | Present, full gate chain confirmed |
| `DeckFlow.Studio/Pages/Reconcile.razor(.cs)` | Operator dry-run + Apply page | ✓ VERIFIED | Present, routed `/reconcile`, seed-unavailable banner, removal-scoped Apply |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` + `FeatureFlagStore.cs` | `sync.reconcile` seeded OFF both dialects | ✓ VERIFIED | Present, both dialect seed blocks confirmed |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `ContentSiteIndexStore.BuildUpsertParameters` | `@seedManaged` | Bound Dapper parameter | ✓ WIRED | `ContentSiteIndexStore.cs:962` `parameters.Add("seedManaged", row.SeedManaged)`; no literal in any UPSERT VALUES clause |
| `SeedManagedBackfill.RunAsync` | `IContentSiteIndexStore.SetSeedManagedIfNullAsync` | per-row write, gated | ✓ WIRED | Confirmed in source; only called when `SeedManaged is null` and seed available |
| `SeedManagedBackfill.RunAsync` | `ISeedKeyMembershipSource.GetSeedMembership().SeedAvailable` | skip-run gate | ✓ WIRED | Confirmed short-circuit before `GetAllRowsAsync` |
| `ContentKbReconcileOrchestrator` | `ContentKbReconcileClassifier.Classify` | in-memory collections + `SeedIndexReadResult` | ✓ WIRED | Confirmed call site |
| `ContentKbReconcileOrchestrator` | `IContentKbReconcileStore.PersistRunAsync` | scope-tagged persistence | ✓ WIRED | Confirmed call site |
| `ReconcileCoordinator.RunDryRunAsync` | `IContentKbReconcileOrchestrator.RunDryRunAsync` | delegate | ✓ WIRED | Confirmed 1:1 delegation |
| `ReconcileCoordinator.ApplyRemovalsAsync` | `IProdContentReader.TryReadFlagAsync("sync.reconcile")` | tri-state gate | ✓ WIRED | Confirmed; only `== true` proceeds |
| `ReconcileCoordinator.ApplyRemovalsAsync` | `ReconcileDryRunResult.SeedAvailable` | pre-stale-check refuse | ✓ WIRED | Confirmed ordering: flag → SeedAvailable → stale-check → seed_managed re-check → hide |
| `ReconcileCoordinator.ApplyRemovalsAsync` | `IContentSiteIndexStore.SetVisibilityAsync(keys, false)` | seed_managed-only soft-hide | ✓ WIRED | Confirmed; natural-key batch overload, no timestamp column |

### Behavioral Spot-Checks (live re-run this session, not re-quoted from SUMMARY)

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Core reconcile/seed-managed/seed-index-reader unit tests | `dotnet.exe test DeckFlow.Core.Tests --filter "Reconcile\|SeedManaged\|SeedIndexFileReader"` | 53/53 passed | ✓ PASS |
| Studio reconcile unit tests | `dotnet.exe test DeckFlow.Studio.Tests --filter "Reconcile"` | 34/34 passed | ✓ PASS |
| Web reconcile + feature-flag unit tests | `dotnet.exe test DeckFlow.Web.Tests --filter "Reconcile\|FeatureFlag"` | 63/63 passed | ✓ PASS |
| Full solution build | `dotnet.exe build DeckFlow.sln --no-restore` | Build succeeded, 0 warnings, 0 errors | ✓ PASS |
| All 15 task commit hashes cited across 91-01..08 SUMMARYs | `git log --oneline` | All present at HEAD `55218cd7` | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| SYNC-17 | 91-01, 91-02, 91-03 | Row-level seed-ownership marker | ✓ SATISFIED | Truths 1-4 |
| SYNC-11 | 91-04, 91-05, 91-06, 91-07 | prod↔git↔seed reconciler + discrepancy store | ✓ SATISFIED | Truths 5-8 |
| SYNC-12 | 91-08 | Gated seed-driven removal | ✓ SATISFIED | Truths 9-10 |

No orphaned requirements found — REQUIREMENTS.md maps only SYNC-17/11/12 to Phase 91, and all three are claimed by the plans and satisfied.

### Anti-Patterns Found

Scanned all 57 files touched across commits `181364df..HEAD` for `TODO|FIXME|XXX|TBD|PLACEHOLDER|not yet implemented|coming soon`. One match: `ContentArtifactSpec.cs:17-18`, a pre-existing example URL (`https://www.youtube.com/watch?v=XXXXXXXXXXX`) inside a documentation string constant — not a debt marker, not phase-91-introduced logic. No genuine blockers or warnings found.

### 91-09 — Outstanding Manual Operator Gate

**Plan 91-09 (`autonomous: false`, wave 7) has NOT been executed.** No `91-09-SUMMARY.md` exists,
and per this verification's explicit scope instruction it was not run by this verifier. It consists
of two blocking `checkpoint:human-verify` tasks:

1. **Task 1** — operator runs a dry-run against a real fixture git checkout with `sync.reconcile`
   OFF and confirms all four discrepancy classes render, the D-06 report is written, and prod state
   is unchanged.
2. **Task 2** — operator mutates the fixture to prove a stale Apply is refused, then flips
   `sync.reconcile` ON and confirms Apply soft-hides only `seed_managed=true` seed-drift rows while
   a known prod-owned row stays visible, and confirms flag-OFF/indeterminate refuses.

**The CODE these checkpoints depend on was verified in this report** (Truths 7, 8, 9, 10 above,
plus the live-passing unit-test matrix that exercises the identical gate logic with fakes). What
remains unverified is the **live, real-fixture behavior** — unit tests with fakes cannot prove the
Studio page correctly wires a real git checkout, a real web-DB flag flip, and a real Postgres/SQLite
prod row. This is exactly the class of check 91-09 exists for, and it has not yet been performed.

## Gaps Summary

No code-level gaps found. All 8 autonomous plans (91-01 through 91-08) deliver exactly what their
`must_haves` frontmatter and the phase's `<verification_focus>` truths require, confirmed against
live-read source (not SUMMARY prose) and a live-rerun test matrix. The only outstanding item is the
91-09 human sign-off, which is a **known, by-design gate** (not a defect) — the phase cannot be
considered fully closed until an operator runs the two checkpoints against a real fixture and
confirms the live safety story holds. This routes the phase to `human_needed` per the verification
decision tree (human items present → not `passed`, and no code truth actually FAILED → not
`gaps_found`).

---

*Verified: 2026-07-09*
*Verifier: Claude (gsd-verifier)*
