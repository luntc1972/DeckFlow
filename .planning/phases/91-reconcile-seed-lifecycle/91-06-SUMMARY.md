---
phase: 91-reconcile-seed-lifecycle
plan: 06
subsystem: content-kb
tags: [content-kb, reconcile, studio, orchestrator, report, seed-availability]

# Dependency graph
requires:
  - phase: 91-reconcile-seed-lifecycle (91-02)
    provides: "ProdContentReader.ReadAllAsync now selects+maps body_sha256/seed_managed"
  - phase: 91-reconcile-seed-lifecycle (91-04)
    provides: "ContentKbReconcileClassifier.Classify (pure, I/O-free 4-class classifier)"
  - phase: 91-reconcile-seed-lifecycle (91-05)
    provides: "IContentKbReconcileStore.PersistRunAsync (idempotent upsert + resolution-by-absence)"
provides:
  - "IContentKbReconcileOrchestrator + ContentKbReconcileOrchestrator.RunDryRunAsync: the runnable dry-run — reads prod once, walks the git content-kb tree, reads the seed availability-aware, classifies, persists, reports"
  - "ReconcileDryRunResult(SeedAvailable, Discrepancies) — SeedAvailable sourced straight from SeedIndexFileReader.Read, never inferred from the discrepancy list"
  - "D-06 git-tracked human-readable report at content-kb/reconcile-report.md, with a seed-unavailable advisory in place of the seed-drift section"
  - "Registered as a singleton in DeckFlow.Studio/Program.cs"
affects: [91-07-dry-run-page, 91-08-apply-gated-removal]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "I/O orchestrator mirrors GitBodyCoverageAudit's shape exactly: depends only on the structurally read-only IProdContentReader (never IProdStoreFactory), reads prod exactly once per run, resolves repoRoot via IGitRepository.ResolveRepoRootAsync(StudioRepoLocator.ResolveStartDirectory())"
    - "One filesystem scan builds two collections (existing-paths set + body-text map) reused for published-orphan/file-orphan AND body-hash-mismatch — avoids a second directory walk (91-RESEARCH Open Question 2)"
    - "Self-referential-output exclusion: the orchestrator's own report artifact path is excluded from its own file-orphan enumeration input by name, preventing an infinite self-flagging loop on re-run"

key-files:
  created:
    - DeckFlow.Studio/Services/IContentKbReconcileOrchestrator.cs
    - DeckFlow.Studio/Services/ContentKbReconcileOrchestrator.cs
    - DeckFlow.Studio.Tests/Services/ContentKbReconcileOrchestratorTests.cs
  modified:
    - DeckFlow.Studio/Program.cs

key-decisions:
  - "Constructor takes IConfiguration in addition to the plan's stated (IProdContentReader, IContentKbReconcileStore, IGitRepository, ILogger) — RunDryRunAsync's contracted signature is (scopeTag, ct) with no connection-string parameter (unlike GitBodyCoverageAudit.RunAsync), so the orchestrator must read Studio:ProdConnectionString itself; mirrors PullFromProdCoordinator's exact pattern (ephemeral read, never materialized into DI state)"
  - "D-06 report path chosen as content-kb/reconcile-report.md (the plan's own example path) with an explicit by-name exclusion from the file-orphan *.md enumeration — without this exclusion the report would self-classify as a file-orphan on every run after the first, since no prod row will ever claim that artifact path"
  - "Tasks 1 and 2 committed separately per the plan's task split (unlike 91-04/91-05's tightly-coupled precedent) — RunDryRunAsync's core (read/classify/persist) has independent meaning and passing tests without the report writer, so the split commits are meaningful checkpoints, not artificial"
  - "Report AppendSection helper mines only the StringBuilder/section-with-count SHAPE of ReconciliationReporter (deck-comparison domain) — no ReconciliationReporter/DeckDiff type is imported or reused, per 91-RESEARCH Anti-Patterns"

requirements-completed: [SYNC-11]

# Metrics
duration: ~50min
completed: 2026-07-09
---

# Phase 91 Plan 06: Reconcile Dry-Run Orchestrator + D-06 Report Summary

**`ContentKbReconcileOrchestrator.RunDryRunAsync` turns the pure classifier into a runnable dry-run: one prod read, one git `content-kb/**/*.md` walk, an availability-aware seed read, persisted scope-tagged results, and a git-tracked report that renders a "seed unavailable" advisory instead of a misleading empty seed-drift section.**

## Performance

- **Duration:** ~50 min
- **Started:** 2026-07-09T22:47:00Z
- **Completed:** 2026-07-09T23:37:00Z
- **Tasks:** 2
- **Files modified:** 4 (3 created, 1 modified)

## Accomplishments
- `IContentKbReconcileOrchestrator.RunDryRunAsync(scopeTag, ct)` + the `ReconcileDryRunResult(SeedAvailable, Discrepancies)` contract — `SeedAvailable` comes straight from `SeedIndexFileReader.Read`'s `SeedIndexReadResult.SeedAvailable`, never inferred from whether any seed-drift discrepancies happened to be found, so 91-08's Apply can independently refuse an unavailable seed (Codex BLOCK closure).
- `ContentKbReconcileOrchestrator` mirrors `GitBodyCoverageAudit`'s shape: resolves `repoRoot` via `IGitRepository.ResolveRepoRootAsync(StudioRepoLocator.ResolveStartDirectory())`, reads prod exactly once via `IProdContentReader.ReadAllAsync`, enumerates `repoRoot/content-kb/**/*.md` (the git tree, not any Studio local artifact root — 91-RESEARCH Pitfall 6), reads `index-seed.json` via `SeedIndexFileReader.Read`, drives `ContentKbReconcileClassifier.Classify`, and persists via `IContentKbReconcileStore.PersistRunAsync`.
- Every file-orphan candidate path (converted from an absolute enumerated path to a content-kb-relative, forward-slash-normalized path) is validated through `ArtifactPathSafety.IsSafeArtifactPath` before being added to either the existing-paths set or the body-text map — one filesystem scan builds both collections, reused for published-orphan/file-orphan detection AND body-hash-mismatch (91-RESEARCH Open Question 2).
- D-06 report writer: `BuildReportText` renders a sectioned Markdown report (`## <Class> (<count>)` + a bulleted item list, `  none` when empty) to `content-kb/reconcile-report.md` under the repo checkout. When `SeedAvailable == false`, the seed-drift section is replaced with a `SEED UNAVAILABLE - seed-drift/removal skipped` advisory instead of an empty `(0)` section, so the operator can never misread "seed unreadable" as "no drift found."
- Self-pollution guard: the report's own artifact path (`content-kb/reconcile-report.md`) is excluded by name from the `*.md` enumeration feeding the classifier, so the orchestrator's own output file can never classify itself as a file-orphan on the very next run.
- Registered as a `Singleton` in `DeckFlow.Studio/Program.cs` (all constructor dependencies — `IProdContentReader`, `IContentKbReconcileStore`, `IGitRepository`, `IConfiguration`, `ILogger<T>` — are themselves singletons/framework services, so no captive-dependency risk).
- 7 new `ContentKbReconcileOrchestratorTests`: all four discrepancy classes detected + persisted to a real (temp-file) `ContentKbReconcileStore` with `SeedAvailable == true`; an unavailable seed (no `index-seed.json` written) yields `SeedAvailable == false` and zero seed-drift while published-orphan/file-orphan/body-hash-mismatch are still computed; prod is read exactly once per run (spy count); `index-seed.json` is naturally excluded from file-orphan enumeration (it is JSON, not `.md`); the report file is written with a section+count per class; the seed-unavailable advisory replaces the empty seed-drift section in the report text; and the report itself never self-classifies as a file-orphan across two consecutive runs.

## Task Commits

1. **Task 1: ContentKbReconcileOrchestrator — prod read + git enum + seed read -> classifier -> store, returns ReconcileDryRunResult** - `07dfc13f` (feat)
2. **Task 2: D-06 human-readable report writer (with seed-unavailable notice)** - `e320923a` (feat)

_Plan metadata commit and STATE/ROADMAP updates follow this SUMMARY._

## Files Created/Modified
- `DeckFlow.Studio/Services/IContentKbReconcileOrchestrator.cs` - `IContentKbReconcileOrchestrator` contract + `ReconcileDryRunResult(SeedAvailable, Discrepancies)` record
- `DeckFlow.Studio/Services/ContentKbReconcileOrchestrator.cs` - `RunDryRunAsync` (read/enumerate/classify/persist) + `ReadGitContentTree` (one-scan path+body builder) + `WriteReport`/`BuildReportText`/`AppendSection` (D-06 report)
- `DeckFlow.Studio/Program.cs` - Registers `IContentKbReconcileOrchestrator` as a singleton
- `DeckFlow.Studio.Tests/Services/ContentKbReconcileOrchestratorTests.cs` - 7 tests covering all four classes, seed-availability gating, single-prod-read, seed-json exclusion, report sections/counts, seed-unavailable notice, and report self-exclusion

## Decisions Made
See `key-decisions` in frontmatter. In summary: (1) `IConfiguration` was added to the constructor beyond the plan's literal dependency list because `RunDryRunAsync`'s own contracted signature carries no connection-string parameter — this mirrors `PullFromProdCoordinator`'s established ephemeral-read pattern, not a deviation from intent; (2) the D-06 report path is excluded by name from its own input enumeration to prevent a self-referential file-orphan loop; (3) Tasks 1 and 2 were committed as two separate atomic commits (unlike the 91-04/91-05 tightly-coupled precedent) because the orchestrator's dry-run core is independently meaningful and fully tested without the report writer.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added `IConfiguration` constructor dependency for the prod connection string**
- **Found during:** Task 1 implementation
- **Issue:** The plan's `<action>` text names the constructor dependencies as `IProdContentReader, IContentKbReconcileStore, IGitRepository, ILogger`, but the plan's own `<interfaces>` contract defines `RunDryRunAsync(scopeTag, ct)` with no connection-string parameter (unlike `GitBodyCoverageAudit.RunAsync(prodConnectionString, repoRoot, ct)`, which takes it from the caller). Without a connection string source, `_prodReader.ReadAllAsync` cannot be called at all — a blocking gap between the stated constructor and the stated method signature.
- **Fix:** Added `IConfiguration configuration` as a fifth constructor parameter, reading `_configuration["Studio:ProdConnectionString"] ?? string.Empty` inside `RunDryRunAsync` exactly once per run — the identical pattern already established by `PullFromProdCoordinator.PullAndClassifyAsync` (ephemeral read, never materialized into DI state, D-03/D-07 precedent).
- **Files modified:** `DeckFlow.Studio/Services/ContentKbReconcileOrchestrator.cs`, `DeckFlow.Studio/Services/IContentKbReconcileOrchestrator.cs` (no interface change needed — only the concrete constructor), `DeckFlow.Studio/Program.cs` (DI container resolves `IConfiguration` automatically, no explicit registration needed)
- **Verification:** `dotnet build DeckFlow.sln` clean (0 warnings); orchestrator tests construct it via `new ConfigurationBuilder().Build()` (empty config, matching `FakeProdContentReader`'s ignore-connection-string behavior) and all pass.
- **Committed in:** `07dfc13f` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 3 — blocking gap between stated constructor and stated method signature)
**Impact on plan:** Necessary to make `RunDryRunAsync` callable at all per its own contracted `(scopeTag, ct)` signature. No scope creep — the fix is pure DI plumbing following an established in-codebase pattern.

## Issues Encountered
None beyond the deviation above — no build lock, no missing dependency, no architectural surprise. `TypeScript.Tasks.dll` was not locked (no Studio dev server was running during this plan).

## User Setup Required
None - no external service configuration required. The orchestrator's prod read continues to use the existing `Studio:ProdConnectionString` configuration key; no new environment variable or dashboard step is introduced.

## Next Phase Readiness
- `IContentKbReconcileOrchestrator.RunDryRunAsync` is ready for 91-07 (the dry-run Studio page) to call directly: it needs only a `scopeTag` string (e.g. `"full"`) and returns a fully-populated `ReconcileDryRunResult` plus a persisted store state and a git-tracked report file — no further plumbing required.
- `ReconcileDryRunResult.SeedAvailable` is ready for 91-08 (removal-scoped Apply) to gate its own independent refusal on an unavailable seed, per the Codex BLOCK closure this plan implements.
- No blockers. Full solution builds clean (`DeckFlow.Core`, `DeckFlow.Core.Tests`, `DeckFlow.Studio`, `DeckFlow.Studio.Tests` 365/365 (4 Postgres-gated skips), `DeckFlow.Web`, `DeckFlow.Web.Tests`, `DeckFlow.CLI` all verified 0 errors / 0 warnings).

---
*Phase: 91-reconcile-seed-lifecycle*
*Completed: 2026-07-09*

## Self-Check: PASSED

All 4 created/modified source files verified present on disk; both task commit hashes (`07dfc13f`, `e320923a`) verified present in git log.
