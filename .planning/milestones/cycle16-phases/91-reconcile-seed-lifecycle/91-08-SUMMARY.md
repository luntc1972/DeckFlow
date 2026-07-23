---
phase: 91-reconcile-seed-lifecycle
plan: 08
subsystem: content-kb
tags: [content-kb, reconcile, studio, feature-flag, destructive-apply, seed-lifecycle]

# Dependency graph
requires:
  - phase: 91-reconcile-seed-lifecycle (91-06)
    provides: "IContentKbReconcileOrchestrator.RunDryRunAsync + ReconcileDryRunResult(SeedAvailable, Discrepancies)"
  - phase: 91-reconcile-seed-lifecycle (91-07)
    provides: "ReconcileCoordinator + Reconcile.razor(.cs) dry-run page, ReconcileCoordinator.GetOpenDiscrepanciesAsync"
  - phase: 90-directpush-correctness-seed-sync (90-04)
    provides: "IProdContentReader.TryReadFlagAsync tri-state pattern; web-DB feature-flag-seeded-OFF convention"
provides:
  - "sync.reconcile web-DB feature flag — registered in FeatureFlagCatalog, seeded OFF on both dialects"
  - "ReconcileCoordinator.ApplyRemovalsAsync — flag-gated, seed-availability-gated, re-validated, seed_managed-only soft-hide scoped to seed-drift removals"
  - "Reconcile.razor Apply removals stage — operator-confirmed, renders applied/refused/stale outcomes"
affects: [92-pull-hardening, 93-round-trip-integration-test]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ApplyRemovalsAsync layers TWO independent fail-safe-to-refuse gates before any write: the tri-state sync.reconcile flag read (false AND null both refuse) and the raw ReconcileDryRunResult.SeedAvailable flag (refuses BEFORE the stale-check, independent of the discrepancy list) — mirrors DirectPushCoordinator's TryReadFlagAsync tri-state precedent but goes further: DirectPush's tri-state only changes WHICH safe path runs (verify vs immediate-publish), while Reconcile's tri-state is a hard refuse with zero writes"
    - "Defense-in-depth seed_managed re-check: even though ContentKbReconcileClassifier.Classify already gates SeedDrift emission on row.SeedManaged == true, ApplyRemovalsAsync independently re-reads fresh prod rows via CreateProdStore().GetAllRowsAsync() and cross-checks each matched removal's CURRENT seed_managed value before hiding — a prod-owned row cannot be hidden through this method even under a hypothetical future classifier regression"
    - "Reviewed-set scoping to one discrepancy Kind (SeedDrift) prevents a mixed-class dry-run from ever false-rejecting an Apply as stale — only removal-class IDs enter the set-equality stale-check on either side"

key-files:
  created:
    - DeckFlow.Web.Tests/Services/FeatureFlags/ReconcileFeatureFlagTests.cs
    - DeckFlow.Studio.Tests/TestDoubles/FakeReconcileFlagReader.cs
  modified:
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
    - DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs
    - DeckFlow.Studio/ViewModels/ReconcileCoordinator.cs
    - DeckFlow.Studio.Tests/ViewModels/ReconcileCoordinatorTests.cs
    - DeckFlow.Studio/Pages/Reconcile.razor
    - DeckFlow.Studio/Pages/Reconcile.razor.cs
    - README.md

key-decisions:
  - "ReconcileCoordinator gained three new constructor dependencies (IProdStoreFactory, IProdContentReader, IConfiguration) beyond the 91-07 (orchestrator, store, logger) shape — required for Apply's flag read and its own prod store write, both structurally absent from the dry-run-only 91-07 coordinator. Program.cs needed no change: plain AddSingleton<ReconcileCoordinator>() auto-resolves the new params since IProdStoreFactory/IProdContentReader/IConfiguration are already registered singletons/framework services (same DI shape DirectPushCoordinator already proved in 90-01)."
  - "Apply's seed_managed check re-reads prod fresh via CreateProdStore().GetAllRowsAsync() rather than trusting the discrepancy's own Kind==SeedDrift. The classifier already scopes SeedDrift emission to row.SeedManaged==true, so this is deliberate defense-in-depth (T-91-20): a discrepancy record carries NaturalKeyType/NaturalKeyValue but not SeedManaged, so proving the invariant at the Apply layer itself (independent of the classifier's correctness) requires the extra read. This also makes the seed_managed=false-never-hidden test a genuine regression guard on Apply's own code, not just a pass-through of the fake orchestrator's seeded result."
  - "The stale-check and the reviewed set are both scoped to ContentKbReconcileKind.SeedDrift only (never the other three classes) per the plan's Codex-MED fix, so a mixed-class dry-run (all four classes present) never false-rejects the seed-drift Apply as stale — verified by a dedicated mixed-class test."

requirements-completed: [SYNC-12]

# Metrics
duration: ~35min
completed: 2026-07-09
---

# Phase 91 Plan 08: Gated Removal Apply + sync.reconcile Flag Summary

**`ReconcileCoordinator.ApplyRemovalsAsync` lets a git seed finally remove content from prod — soft-hide only, reversible — gated behind a fail-safe-to-refuse feature flag, an independent seed-availability refuse that runs before any other check, fresh re-validation against a stale reviewed set, and a seed_managed re-check that structurally cannot hide a prod-owned row.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-07-09T23:15:00Z (approx, following 91-07)
- **Completed:** 2026-07-09T23:50:00Z
- **Tasks:** 3
- **Files modified:** 10 (2 created, 8 modified)

## Accomplishments
- `sync.reconcile` registered in `FeatureFlagCatalog.Descriptions` (describing that it gates ONLY the destructive Apply while detection/dry-run stay always-available, D-09) and seeded `FALSE`/`0` in both `PostgresSeedSql` and `SqliteSeedSql`, preserving the `ON CONFLICT (key) DO NOTHING` operator-value contract. 4 new `ReconcileFeatureFlagTests` assert catalog registration, the SQLite runtime-seeded-OFF default, and the literal Postgres/SQLite seed rows via reflection.
- `ReconcileCoordinator.ApplyRemovalsAsync(reviewedRemovalDiscrepancyIds, scopeTag, ct)` implements the full gate chain in order, each refusing with zero writes before the next runs:
  1. **Flag gate:** reads `sync.reconcile` via the tri-state `IProdContentReader.TryReadFlagAsync`; only a confirmed `true` proceeds — both `false` and indeterminate `null` refuse (fail-safe-to-REFUSE).
  2. **Seed-availability gate (Codex BLOCK / T-91-27):** re-runs the reconcile diff FRESH via the orchestrator and refuses on the raw `ReconcileDryRunResult.SeedAvailable` flag — independent of the discrepancy list, before any stale-check or hide.
  3. **Stale-check:** filters the fresh result to `ContentKbReconcileKind.SeedDrift` only, compares that ID set against `reviewedRemovalDiscrepancyIds` by set equality; any mismatch stale-rejects with zero writes. Non-removal classes never enter this comparison on either side.
  4. **Seed-managed re-check (T-91-20 defense-in-depth):** re-reads prod rows fresh via the on-demand prod store and hides a matched removal's natural key ONLY when the CURRENT prod row for that key has `SeedManaged == true` — independent of the classifier's own gate.
  The hide reuses `IContentSiteIndexStore.SetVisibilityAsync` (natural-key batch) exclusively; no timestamp column is ever touched.
- 13 new `ApplyRemovalsAsync` tests plus 5 constructor null-guard tests: flag true applies / false refuses / indeterminate refuses (with zero orchestrator calls on refusal), seed-unavailable refuses BEFORE the stale-check even with a stale non-empty reviewed set supplied, a `seed_managed=false` row is NEVER hidden even when the discrepancy Kind claims seed-drift (the core SYNC-17 regression guard), a stale reviewed set is rejected with no write, a mixed-class fresh diff (all four classes) does not false-reject and still applies the seed-drift removals, and no `StampPushedToProdAsync`/timestamp call ever occurs on the soft-hide path.
- `Reconcile.razor`/`.razor.cs` gained an "Apply removals" card: builds the reviewed-removal set from ONLY the currently-displayed seed-drift discrepancies (`Items(_result, ContentKbReconcileKind.SeedDrift)`), requires an explicit review checkbox before the button enables, and renders four distinct outcomes (applied with count, refused-flag-off-or-indeterminate, refused-seed-unavailable, stale-rejected) via a `DescribeRefusal` helper. A fresh dry-run clears any prior Apply result/checkbox state so a stale outcome can never be mistaken for the current run's. The card explicitly states the destructive-but-reversible (soft-hide) nature, the flag gate, and the seed-drift-only scope.
- README documents the full dry-run → gated Apply flow: the flag (seeded OFF, fail-safe tri-state), the independent seed-availability refuse ordering, the fresh re-validation against staleness, and the seed_managed=true-only hide invariant.

## Task Commits

Each task was committed atomically:

1. **Task 1: Register + seed the sync.reconcile web-DB flag (OFF, both dialects)** - `74fde782` (feat)
2. **Task 2: ApplyRemovalsAsync — gated, re-validated, seed_managed-only soft-hide** - `ac3c1ba8` (feat)
3. **Task 3: Reconcile page Apply stage + README** - `6d19f247` (feat)

**Plan metadata:** commit follows this SUMMARY.

## Files Created/Modified
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` - Adds `sync.reconcile` description
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` - Adds `('sync.reconcile', FALSE/0)` to both seed SQL blocks
- `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` - Adds `sync.reconcile` to the seeded-flag theory list
- `DeckFlow.Web.Tests/Services/FeatureFlags/ReconcileFeatureFlagTests.cs` - 4 tests: catalog registration, SQLite seeded-OFF, Postgres/SQLite literal seed-row reflection checks
- `DeckFlow.Studio/ViewModels/ReconcileCoordinator.cs` - Adds `ApplyRemovalsAsync`, `ReconcileFlagKey` const, `TryReadReconcileFlagAsync`/`CreateProdStore` helpers, `ReconcileApplyResult`/`ReconcileApplyRefusalReason` types; constructor gains `IProdStoreFactory`, `IProdContentReader`, `IConfiguration`
- `DeckFlow.Studio.Tests/ViewModels/ReconcileCoordinatorTests.cs` - Adds a `Build` helper (5-arg constructor) + 13 `ApplyRemovalsAsync` tests + 5 constructor null-guard tests; existing dry-run tests updated to the new `Build` helper
- `DeckFlow.Studio.Tests/TestDoubles/FakeReconcileFlagReader.cs` - New tri-state `IProdContentReader` fake dedicated to `ReconcileCoordinator`'s flag dependency (mirrors `FakeDirectPushFlagReader`'s one-fake-per-coordinator convention)
- `DeckFlow.Studio/Pages/Reconcile.razor` - Adds the Apply removals card (review checkbox, destructive-but-reversible alert, applied/refused/stale outcome rendering); updates the intro paragraph
- `DeckFlow.Studio/Pages/Reconcile.razor.cs` - Adds `RunApplyRemovalsAsync`, `DescribeRefusal`, Apply-related fields, and a test seam (`InvokeRunApplyRemovalsForTest`)
- `README.md` - Documents the gated Apply flow (flag + seed-availability + stale-reject + seed_managed invariants)

## Decisions Made
See `key-decisions` in frontmatter. In summary: (1) `ReconcileCoordinator`'s constructor grew to 5 required params (plus optional logger) because Apply needs its own flag read and prod write surface, both absent from the 91-07 dry-run-only shape — DI needed no registration change since all three new deps are already-registered singletons; (2) Apply's seed_managed check deliberately re-reads prod fresh rather than trusting the discrepancy Kind, making the SYNC-17 invariant a property of Apply's own code, not an inherited assumption about the classifier's correctness; (3) the stale-check and reviewed set are scoped to `SeedDrift` only, so a dry-run surfacing all four classes never false-rejects the removal Apply.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Stray NUL bytes introduced while typing the `\u0000` composite-key delimiter**
- **Found during:** Task 2, immediately after writing `ReconcileCoordinator.cs` and `ReconcileCoordinatorTests.cs`
- **Issue:** While constructing composite natural-key strings (`$"{type}\u0000{value}"`) to match the codebase's established `ContentNaturalKey`/`ContentSyncDiffClassifier`/`DirectPushCoordinator` delimiter convention, the tool-call text intended to contain the six-character escape sequence `\u0000` instead wrote literal raw NUL (0x00) control bytes into both files — the exact "Subagent .md NUL bytes" failure mode noted in project memory (`reference_subagent_markdown_nul_bytes.md`), here hitting `.cs` source rather than `.md`. `grep` silently failed to match lines containing the raw NUL byte, which is how the discrepancy was caught (an expected `grep` match came back empty).
- **Fix:** Byte-scanned both files with a Python script, confirmed the NUL byte offsets and their surrounding context, and replaced each raw `\x00` with the literal ASCII text `\u0000` (the correct C# escape-sequence source form the rest of the codebase uses) so the compiled string constant is identical while the source file itself carries no raw NUL bytes.
- **Files modified:** `DeckFlow.Studio/ViewModels/ReconcileCoordinator.cs`, `DeckFlow.Studio.Tests/ViewModels/ReconcileCoordinatorTests.cs`
- **Verification:** Post-fix Python byte-scan confirmed `NUL: 0` in both files (and every other touched file in this plan); `dotnet build` and the full `ReconcileCoordinatorTests` suite passed identically before and after the byte-level fix (the compiled behavior was never affected — only the on-disk source encoding).
- **Committed in:** `ac3c1ba8` (Task 2 commit; caught and fixed before the commit was made)

---

**Total deviations:** 1 auto-fixed (Rule 1 — a source-encoding bug introduced by this plan's own edits, caught and fixed before commit, with no behavioral impact).
**Impact on plan:** No scope creep. Every touched file in this plan (`FeatureFlagCatalog.cs`, `FeatureFlagStore.cs`, `FeatureFlagCatalogTests.cs`, `ReconcileFeatureFlagTests.cs`, `ReconcileCoordinator.cs`, `ReconcileCoordinatorTests.cs`, `FakeReconcileFlagReader.cs`, `Reconcile.razor`, `Reconcile.razor.cs`, `README.md`) was byte-scanned for NUL bytes and CR characters before every commit; all are clean (LF-only, zero NUL).

## Issues Encountered
- The plan's own `<verify>` command for Task 3 (`grep -icE 'error|warning'` on the build output, expecting `0`) always returns `2` because `dotnet build`'s own summary lines (`0 Warning(s)`, `0 Error(s)`) match the case-insensitive `error|warning` pattern — a false positive in the verify command's own regex, not a build defect. Manually confirmed via `dotnet build DeckFlow.sln --no-restore`: `Build succeeded. 0 Warning(s). 0 Error(s).` — genuinely clean.
- No `TypeScript.Tasks.dll` lock encountered; no Studio/Web dev server was running during this plan, per the project constraint.

## User Setup Required
None — no new external service configuration. `sync.reconcile` ships OFF (matching every prior cycle's flag convention); no operator action is required for this plan to land safely. An operator will need to flip the flag ON in prod when ready to exercise the destructive Apply for real (a later-phase pre-flip gate, per the `sync.directpush-gitbody`/P93 precedent noted in STATE.md).

## Next Phase Readiness
- SYNC-12 (seed-driven removal, gated) is now fully shipped: detection (91-06), operator dry-run visibility (91-07), and the gated destructive Apply (91-08) complete the phase's three locked requirements (SYNC-17 91-01/91-02/91-03, SYNC-11 91-04/91-05/91-06/91-07, SYNC-12 91-08).
- Phase 91 has no further plans; the next roadmap phase is 92 (Pull Hardening, SYNC-13/14/15), which reuses this phase's composite-key diffing and discrepancy vocabulary per STATE.md's phase-ordering rationale.
- `sync.reconcile` and `sync.directpush-gitbody` both ship OFF; flipping either ON in prod is explicitly deferred to Phase 93's pre-flip checklist (per 91-CONTEXT.md's deferred-ideas list and the `90-FOLLOWUPS.md` precedent).
- No blockers. Full solution builds clean: `DeckFlow.Core.Tests` 1201/1201, `DeckFlow.Web.Tests` 1255/1267 (12 Postgres-gated skips), `DeckFlow.Studio.Tests` 385/389 (4 Postgres-gated skips) — all 0 errors / 0 new warnings.

---
*Phase: 91-reconcile-seed-lifecycle*
*Completed: 2026-07-09*

## Self-Check: PASSED

All 10 created/modified source files verified present on disk; all three task commit hashes (`74fde782`, `ac3c1ba8`, `6d19f247`) verified present in git log.
