---
phase: 260726-pxw
plan: 01
subsystem: ui
tags: [typescript, form-persistence, sessionStorage, cut-lab, regression-fix]

# Dependency graph
requires:
  - phase: quick-260726-mug
    provides: "Identical fix pattern applied to cEDH Meta Gap's WorkflowStep/FetchedEntriesJson/MetaGapPromptText fields"
provides:
  - "CutLabStateJson excluded from generic sessionStorage form-persistence cache (capture + restore)"
  - "Regression test proving stale cache no longer clobbers server-rendered Cut Lab state"
affects: [cut-lab, deck-sync-form-persistence]

# Tech tracking
tech-stack:
  added: []
  patterns: ["nonPersistedFieldNames exclusion Set for server-authoritative hidden fields (same pattern as HistoryJson, WorkflowStep, FetchedEntriesJson, MetaGapPromptText)"]

key-files:
  created: [DeckFlow.Web/ts-tests/cut-lab-hidden-field-persistence.test.ts]
  modified: [DeckFlow.Web/wwwroot/ts/deck-sync.ts]

key-decisions:
  - "Mirrored the cedh-meta-gap-hidden-field-persistence.test.ts structure exactly for consistency with the established fix pattern for this bug class"
  - "Used DeckUrl (not DeckText/PrimaryPlan) for the 'normal field still hydrates' assertion — simplest plain-text input on the cut-lab form"

patterns-established: []

requirements-completed: [QUICK-PXW-01]

# Metrics
duration: 8min
completed: 2026-07-26
---

# Quick Task 260726-pxw: Fix Hidden-Field Cache Clobber on Cut Lab Summary

**Excluded `CutLabStateJson` from the generic `data-cache-key` sessionStorage form-persistence cache in `deck-sync.ts`, fixing the third confirmed instance of the hidden-field cache-clobber bug class (after `HistoryJson` and `WorkflowStep`/`FetchedEntriesJson`/`MetaGapPromptText`).**

## Performance

- **Duration:** 8 min
- **Started:** 2026-07-26T18:45:06Z (first test run)
- **Completed:** 2026-07-26T18:47:17-06:00 (fix commit)
- **Tasks:** 2 completed
- **Files modified:** 2

## Accomplishments
- Added a failing regression test (`cut-lab-hidden-field-persistence.test.ts`) proving the bug: a stale `sessionStorage['decksync-form-state-cut-lab']` cache was overwriting the server-rendered `CutLabStateJson` hidden field on page load, while normal fields like `DeckUrl` correctly restored from cache.
- Added `'CutLabStateJson'` to the `nonPersistedFieldNames` Set in `deck-sync.ts` (line ~526), which governs both the capture side (`serializePersistedFormFields`) and restore side (`restoreFormFields`) — the fix went green with a one-line change.
- Full vitest suite (123 tests across 32 files) passes, including the new test and the two prior analogous fixes (`deck-history-hidden-field-persistence`, `cedh-meta-gap-hidden-field-persistence`).
- `dotnet build DeckFlow.sln` clean: 0 warnings, 0 errors.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add failing regression test for cut-lab hidden-field clobber** - `8ec3eca6` (test)
2. **Task 2: Exclude CutLabStateJson from generic form persistence** - `ca48a41b` (fix)

_TDD flow: RED (failing test confirmed: `CutLabStateJson` observed as `{"stale":true}` instead of `{"fresh":true}`, `DeckUrl` assertion passed proving hydration ran) → GREEN (one-line Set addition, full suite green)._

## Files Created/Modified
- `DeckFlow.Web/ts-tests/cut-lab-hidden-field-persistence.test.ts` - New regression test: seeds a stale `decksync-form-state-cut-lab` sessionStorage cache, renders a form with fresh server values, imports `deck-sync.ts` to trigger boot-time hydration, asserts `CutLabStateJson` keeps the fresh server value while `DeckUrl` still restores from cache.
- `DeckFlow.Web/wwwroot/ts/deck-sync.ts` - Added `'CutLabStateJson'` to the `nonPersistedFieldNames` Set (one line), extending the existing exclusion mechanism used for `HistoryJson`, `WorkflowStep`, `FetchedEntriesJson`, `MetaGapPromptText`.

## Decisions Made
None - plan executed exactly as written. Verified facts in the plan (Set location, storage key format `decksync-form-state-cut-lab`, single `data-cache-key="cut-lab"` form, no other file needing a `CutLabStateJson` exclusion) all held true during execution; no re-derivation was needed.

## Deviations from Plan

None in code. One environment-only workaround, not a plan deviation:

### Environment note (not a deviation, no code change)

The worktree had no `DeckFlow.Web/node_modules` (fresh worktree checkout). Per the established project pattern (`feedback_worktree_node_modules_junction_stage`), created a Windows directory junction (`cmd /c mklink /J node_modules <main-repo-node_modules>`) so both `npx vitest` and the Windows `dotnet.exe` MSBuild TypeScript target (`tsc`) could resolve `node_modules`. Verified `git check-ignore -v DeckFlow.Web/node_modules` confirms `.gitignore`'s `node_modules` (no trailing slash) rule ignores the junction itself, so it was never staged in either commit (`git status --short` was clean after both commits). No files were added, removed, or committed as part of this workaround.

## Self-Check: PASSED

- FOUND: DeckFlow.Web/ts-tests/cut-lab-hidden-field-persistence.test.ts
- FOUND: DeckFlow.Web/wwwroot/ts/deck-sync.ts (CutLabStateJson entry present)
- FOUND commit 8ec3eca6 (test: add failing test for cut lab hidden field cache clobber)
- FOUND commit ca48a41b (fix: stop form cache clobbering cut lab state)
- Full vitest suite: 32 files, 123 tests, all passed
- `dotnet build DeckFlow.sln`: Build succeeded, 0 Warning(s), 0 Error(s)
- `git diff --stat` vs `git diff --ignore-all-space --stat` for `deck-sync.ts`: both report "1 file changed, 1 insertion(+)" — no EOL churn
- `file` reports both touched files as plain text with no CRLF marker (LF-only preserved)
- Exactly two files changed across both commits: the new test and `deck-sync.ts` — no view, README, or other source file touched
