---
phase: 82-refactor-review-sweep-ui-baseline-audit
plan: 03
subsystem: refactor
tags: [typescript, dapper, blazor, dependency-injection, srp-extraction, vitest, playwright]

# Dependency graph
requires:
  - phase: 82-refactor-review-sweep-ui-baseline-audit (plan 82-01)
    provides: REFACTOR-TRIAGE.md (the authoritative in-scope/backlog decision table)
provides:
  - "3 in-scope refactors executed under the byte-identical/behavior-neutral gate: deck-sync.ts split (2 of 6 concerns), Harvest.razor.cs 4-coordinator split, ContentSiteIndexStore.cs upsert-parameter dedup"
  - "REFACTOR-BACKLOG.md — written deferral record for the 6 remaining candidates (1b, 3, 5, 6, 7, 8)"
affects: [phase-83-packet-service-split, phase-85-chatgpt-naming-cleanup]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "window.* cross-file bridge for wwwroot/ts scripts under tsconfig module:none (mirrors existing window.DeckFlow pattern) — needed because Vitest's per-file ESM import graph doesn't share bare top-level identifiers across dynamically-imported .ts files the way tsc's unified program + the browser's shared <script> scope do"
    - "Studio ViewModels coordinator extraction (mirrors DirectPushCoordinator precedent) — coordinator owns I/O per concern, page keeps markup-bound state + busy/error/log wiring"
    - "Dapper DynamicParameters shared parameter-binding helper behind near-parallel SQL upsert variants"

key-files:
  created:
    - DeckFlow.Web/wwwroot/ts/busy-indicator.ts
    - DeckFlow.Web/wwwroot/ts/moxfield-extension-bridge.ts
    - DeckFlow.Web/ts-tests/busy-indicator-progress.test.ts
    - DeckFlow.Web/ts-tests/moxfield-extension-bridge-mobile.test.ts
    - DeckFlow.Studio/ViewModels/HarvestQueueCoordinator.cs
    - DeckFlow.Studio/ViewModels/AutoApproveSettingsCoordinator.cs
    - DeckFlow.Studio/ViewModels/CreatorManagementCoordinator.cs
    - DeckFlow.Studio/ViewModels/SpendCapCoordinator.cs
    - .planning/phases/82-refactor-review-sweep-ui-baseline-audit/REFACTOR-BACKLOG.md
  modified:
    - DeckFlow.Web/wwwroot/ts/deck-sync.ts
    - DeckFlow.Web/ts-tests/busy-overlay-pageshow.test.ts
    - DeckFlow.Web/ts-tests/cedh-commander-typeahead.test.ts
    - DeckFlow.Web/ts-tests/primer-copy.test.ts
    - 13 Razor views loading deck-sync.js (added busy-indicator.js + moxfield-extension-bridge.js script tags)
    - DeckFlow.Studio/Pages/Harvest.razor.cs
    - DeckFlow.Studio/Program.cs
    - DeckFlow.Studio.Tests/HarvestPageTests.cs
    - DeckFlow.Core/Content/ContentSiteIndexStore.cs

key-decisions:
  - "Confirmed empirically (isolated tsc probe) that tsconfig's module:none unifies all wwwroot/ts/*.ts files into one type-checking program and one browser <script> global scope — cross-file bare-identifier calls compile clean and work in production, but Vitest's per-file dynamic-import ESM graph does NOT share them, requiring a window.* bridge (mirroring the codebase's existing window.DeckFlow pattern) for every new cross-file edge introduced by the split."
  - "Harvest.razor.cs coordinators keep all Blazor UI state (fields bound in markup, ElementReference, StateHasChanged) in the page — coordinators own only the injected-service I/O per concern, matching what DirectPushCoordinator actually does (not literally stateless-everything, but page-state-free) and respecting HarvestPlanner.cs's own prior documented note that a full DirectPushCoordinator-style split was rejected for Harvest due to state interleaving; only the I/O slices that ARE cleanly separable were extracted."
  - "ContentSiteIndexStore dedup used Dapper's DynamicParameters (already an established pattern in FeatureFlagStore.cs/FeedbackStore.cs) instead of a shared anonymous-object type, since the 3 upsert variants each add different extra columns on top of a common base set."

requirements-completed: [REVIEW-02]

# Metrics
duration: 40min
completed: 2026-07-04
---

# Phase 82 Plan 03: Refactor Execution + Backlog Recording Summary

**Executed all 3 in-scope REFACTOR-TRIAGE.md targets (deck-sync.ts 2-concern split, Harvest.razor.cs 4-coordinator split, ContentSiteIndexStore.cs upsert dedup) under the byte-identical gate, and recorded all 6 remaining candidates in REFACTOR-BACKLOG.md with written deferral reasons.**

## Performance

- **Duration:** ~40 min
- **Tasks:** 2 (Task 1: execute in-scope refactors; Task 2: record backlog)
- **Files modified/created:** 32 (21 in the TS extraction, 7 in the Harvest split, 1 in the ContentSiteIndexStore dedup, 1 backlog doc, plus this summary)

## Accomplishments

- **`deck-sync.ts` (2877 LOC) narrowed split:** extracted `busy-indicator.ts` (concern #2, fully
  chatgpt-*-free) and `moxfield-extension-bridge.ts` (concern #1, moving its 3 chatgpt-* cache-key
  string literals verbatim — no rename, Phase-85-safe) into their own files. Cross-file calls route
  through `window.*` rather than bare identifiers, discovered necessary via a failing Vitest run
  after the initial extraction (see Deviations).
- **`Harvest.razor.cs` (1225 LOC) coordinator split:** extracted `HarvestQueueCoordinator`,
  `AutoApproveSettingsCoordinator`, `CreatorManagementCoordinator`, `SpendCapCoordinator` (mirroring
  the `DirectPushCoordinator` precedent), and broke `HarvestAndAutoDistillAsync` into
  `DetermineHarvestReadyIdsAsync` + `RunOneClickDistillAndApproveAsync`.
- **`ContentSiteIndexStore.cs` (1096 LOC) upsert dedup:** extracted `ValidateRowForUpsert` +
  `BuildUpsertParameters` (a shared Dapper `DynamicParameters` bag) behind all 3 `Upsert*Async`
  variants and the batch upsert's per-row loop.
- **REFACTOR-BACKLOG.md:** every one of the 6 remaining REFACTOR-TRIAGE.md candidates (1b, 3, 5, 6,
  7, 8) recorded with a written reason and an unblock note.

## Task Commits

1. **Task 1a: Harvest.razor.cs coordinator split** - `83ff929b` (refactor)
2. **Task 1b: ContentSiteIndexStore.cs upsert dedup** - `07c9ad98` (refactor)
3. **Task 1c: deck-sync.ts busy-indicator/extension-bridge split** - `80e427df` (refactor)
4. **Task 2: REFACTOR-BACKLOG.md** - `c09381b0` (docs)

## Files Created/Modified

- `DeckFlow.Web/wwwroot/ts/busy-indicator.ts` - Extracted progress-overlay concern
- `DeckFlow.Web/wwwroot/ts/moxfield-extension-bridge.ts` - Extracted Moxfield extension-bridge concern
- `DeckFlow.Web/wwwroot/ts/deck-sync.ts` - 2 concerns removed; remaining code calls the extracted modules via `window.*`
- `DeckFlow.Web/ts-tests/busy-indicator-progress.test.ts` - New: progress-step cycling coverage
- `DeckFlow.Web/ts-tests/moxfield-extension-bridge-mobile.test.ts` - New: mobile-browser abort-path coverage (not reachable by the existing desktop-only e2e)
- `DeckFlow.Web/ts-tests/busy-overlay-pageshow.test.ts`, `cedh-commander-typeahead.test.ts`, `primer-copy.test.ts` - Import preamble updated to load the 2 extracted modules before deck-sync.ts
- 13 Razor views (`CommanderCategories`, `Bracket`, `CardLookup`, `CedhMetaGap`, `DeckAnalysis`, `DeckComparison`, `DeckConvert`, `DeckPrimer`, `DeckSync`, `JudgeQuestions`, `Manabase`, `MechanicLookup`, `SuggestCategories`) - Added the 2 new `<script>` tags before `deck-sync.js`
- `DeckFlow.Studio/ViewModels/HarvestQueueCoordinator.cs`, `AutoApproveSettingsCoordinator.cs`, `CreatorManagementCoordinator.cs`, `SpendCapCoordinator.cs` - New collaborators
- `DeckFlow.Studio/Pages/Harvest.razor.cs` - Injects the 4 coordinators instead of the raw services they wrap; `HarvestAndAutoDistillAsync` broken into 2 named steps
- `DeckFlow.Studio/Program.cs` - DI registration for the 4 new coordinators
- `DeckFlow.Studio.Tests/HarvestPageTests.cs` - `RenderHarvest` helper registers the 4 new coordinators for bUnit's DI container
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` - `ValidateRowForUpsert` + `BuildUpsertParameters` extracted
- `.planning/phases/82-refactor-review-sweep-ui-baseline-audit/REFACTOR-BACKLOG.md` - New: deferral record for rows 1b, 3, 5, 6, 7, 8

## Decisions Made

See `key-decisions` in frontmatter above (empirical tsc/Vitest scope-sharing finding; Harvest
coordinator boundary choice respecting HarvestPlanner.cs's prior documented rejection of a full
DirectPushCoordinator-style split; DynamicParameters for the upsert dedup).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Cross-file `wwwroot/ts` calls needed a `window.*` bridge, not bare identifiers**
- **Found during:** Task 1 (deck-sync.ts extraction) — first Vitest run after extracting
  busy-indicator.ts/moxfield-extension-bridge.ts and removing their code from deck-sync.ts failed
  all 5 test files that import deck-sync.ts with `ReferenceError: hideBusyIndicator is not defined`.
- **Issue:** The plan's typescript_module_constraint assumed tsconfig's `module: "none"` shared
  global scope (confirmed true for tsc + the browser) would also apply under Vitest. It does not —
  Vite/esbuild treats each dynamically-imported `.ts` file as its own isolated ES module and does
  not merge bare top-level identifiers across files, even with zero import/export statements.
- **Fix:** Both extracted files now expose their cross-file surface via `window.*` properties
  (`window.hideBusyIndicator`, `window.registerBusyIndicator`, `window.abortBridgeBusy`,
  `window.attachMoxfieldExtensionImport`, `window.DeckInputSource`) and `interface Window {...}`
  augmentations, mirroring the codebase's pre-existing `window.DeckFlow` bridge pattern used for
  the identical reason elsewhere in this same file. No ES import/export was introduced anywhere —
  the fix stays within the `module: "none"` constraint's letter and spirit.
- **Files modified:** `busy-indicator.ts`, `moxfield-extension-bridge.ts`, `deck-sync.ts`
- **Verification:** `tsc --noEmit` 0 errors; Vitest 30/30 (was 5 files failing, now all pass);
  Playwright e2e (`deck-sync-bridge-busy.spec.ts`, `scripts.spec.ts`, `busy-overlay-guard.spec.ts`,
  `manabase.spec.ts`, `deck-primer-bridge.spec.ts`, `cross-tool-deck-persistence.spec.ts`,
  `smoke.spec.ts` — 62 tests) all pass.
- **Committed in:** `80e427df` (same commit as the extraction — the fix was applied before
  committing, so no separate fix-up commit was needed)

**2. [Rule 1 - Bug] Paste-queue duplicate guard widened to also check the in-batch added list**
- **Found during:** Task 1 (Harvest.razor.cs extraction) — reading the original
  `AddToQueueAsync` loop closely revealed `_queueVideos.Any(...)` was checked against the list
  *while it grew inside the same loop* (each iteration's `_queueVideos.Add(...)` was visible to the
  next iteration's dedupe check). A first-draft extraction that checked only the pre-batch snapshot
  would have silently changed behavior for a batch containing internal duplicates (e.g. a playlist
  expansion yielding the same video twice).
- **Fix:** `HarvestQueueCoordinator.FetchQueueAdditionsAsync`'s dedupe check is
  `existingQueue.Any(...) || added.Any(...)`, exactly reproducing the original growing-list check.
- **Files modified:** `DeckFlow.Studio/ViewModels/HarvestQueueCoordinator.cs`
- **Verification:** All 45 Harvest bUnit tests + 258 Studio.Tests pass.
- **Committed in:** `83ff929b`

---

**Total deviations:** 2 auto-fixed (both Rule 1 — bugs caught and fixed before committing, not
scope creep; both are corrections to keep the refactor byte-behavior-identical to the original).
**Impact on plan:** Both fixes were necessary to satisfy the plan's own byte-identical/behavior-
neutral gate — without them the refactor would NOT have been behavior-neutral.

## Issues Encountered

None beyond the two deviations above, both caught by the gate itself (a failing Vitest run and a
close read of the original loop) before any commit — no red gate was ever shipped.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- REVIEW-02 satisfied: every in-scope triage target refactored under the byte-identical gate;
  every over-budget target recorded in REFACTOR-BACKLOG.md with a written reason.
- Phase 82 (Refactor-Review Sweep) is now fully complete (82-01, 82-02, 82-03 all done).
- Row 1b (deck-sync.ts persistence/card-picker/chatgpt-packets) and row 6 (PacketArtifactStore)
  are explicitly flagged for Phase 83/85 coordination — those phases' planners should read
  REFACTOR-BACKLOG.md before finalizing their own scope.
- No blockers for Phase 83 (Packet-Service SRP Split).

---
*Phase: 82-refactor-review-sweep-ui-baseline-audit*
*Completed: 2026-07-04*

## Self-Check: PASSED

All 8 created files verified present on disk; all 4 commit hashes (`83ff929b`, `07c9ad98`,
`80e427df`, `c09381b0`) verified present in `git log --oneline --all`. No missing items.
