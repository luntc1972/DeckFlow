---
phase: 106-partial-quantity-tuning
plan: 04
subsystem: ui
tags: [cut-lab, razor, typescript, vitest, css]
requires:
  - phase: 106-03
    provides: adjust endpoint, quantity adjustment state, working-list derivation
provides:
  - inline quantity tuner rows derived from the adjustment-aware working list
  - exact-100 export gating in Razor and JS
  - under-100 proposal-panel branch and add-basic UI
affects: [cut-lab, export, adjust, ui-testing]
tech-stack:
  added: []
  patterns: [adjustment-derived view-model rows, exact-count client export gating]
key-files:
  created: [DeckFlow.Web/wwwroot/ts/__tests__/cut-lab.adjust.test.ts]
  modified:
    [DeckFlow.Web/Models/CutLabViewModel.cs, DeckFlow.Web/Views/Deck/CutLab.cshtml, DeckFlow.Web/wwwroot/css/site-common.css, DeckFlow.Web/wwwroot/ts/cut-lab.ts, DeckFlow.Web.Tests/CutLabViewModelWordingTests.cs]
key-decisions:
  - "Derived `WorkingListRows` and `CurrentCount` from one 3-arg working-list fold so tuner rows, counts, and gates stay aligned."
  - "Client export enablement now prefers exact count derived from serialized state and falls back to `cardsRemaining` only when older stub payloads cannot be parsed."
  - "Kept add-basic JS on the full-post path because the repo's current vitest/config fence does not safely support the broader DOM insertion work without touching out-of-fence files."
patterns-established:
  - "Cut Lab tuner rows render from view-model projections, not the immutable pool table."
  - "Exact-100 export gating must update both the workflow tab and the build-export submit button."
requirements-completed: [EDIT-01, EDIT-02, EDIT-03]
duration: 57min
completed: 2026-07-22
---

# Phase 106 Plan 04 Summary

**Inline Cut Lab tuning now renders from the adjustment-derived working list, adds exact-100 export gating, and introduces the under-100 UI branch with progressive-enhancement adjust wiring.**

## Performance

- **Duration:** 57 min
- **Started:** 2026-07-22T06:31:00-06:00
- **Completed:** 2026-07-22T07:27:54-06:00
- **Tasks:** 4
- **Files modified:** 7

## Accomplishments

- Added `WorkingListRows` and `AddableBasics` to `CutLabViewModel`, both sourced from the same adjustment-aware derived working list as `CurrentCount`.
- Reworked the Decide UI to show inline steppers and add-basic controls from `Model.WorkingListRows`, added the under-100 proposal branch, and moved both Export gates to `currentCount == 100`.
- Extended `cut-lab.ts` so stepper adjusts post to `/api/cut-lab/adjust`, patch hidden state, update sticky count, and re-evaluate both export gates from the serialized state.

## Files Created/Modified

- `DeckFlow.Web/Models/CutLabViewModel.cs` - Added tunable-row projection, addable basics, and supporting view model record.
- `DeckFlow.Web/Views/Deck/CutLab.cshtml` - Added adjust forms, tuner table, add-basic toolbar, exact-100 export gates, sticky live-region attributes, and the under-100 proposal branch.
- `DeckFlow.Web/wwwroot/css/site-common.css` - Added tuner layout, stepper, and add-basic styles using existing theme tokens.
- `DeckFlow.Web/wwwroot/ts/cut-lab.ts` - Added adjust endpoint wiring, exact-count export gating, tuner-row patching, and fallback error-banner handling.
- `DeckFlow.Web/wwwroot/ts/__tests__/cut-lab.adjust.test.ts` - Added an in-fence adjust spec covering success and error paths.
- `DeckFlow.Web.Tests/CutLabViewModelWordingTests.cs` - Added coverage for derived tuner rows, addable basics filtering, and adjustment-aware current count.

## Decisions Made

- Used the derived working list as the single source for both rendered tuner rows and `CurrentCount` to prevent count/gate drift.
- Preserved the existing Cut Lab JSON state contract when `quantityAdjustments` is empty so the preexisting vitest suite remains green.
- Left add-basic on the non-intercepted full-post path in JS because auto-discovering and executing the new in-fence vitest file would require out-of-fence test-config changes.

## Deviations from Plan

### Fence mismatch: vitest discovery path

- **Issue:** The plan's `files_modified` includes `DeckFlow.Web/wwwroot/ts/__tests__/cut-lab.adjust.test.ts`, but the repo's current vitest discovery only includes `ts-tests/**/*.test.ts`.
- **Impact:** The new in-fence adjust spec compiles cleanly, but it is not discovered by `npm test` without touching out-of-fence vitest config or moving the test into the out-of-fence `ts-tests` directory.
- **Resolution:** Added the test at the fenced path, verified `tsc` stays clean, and kept the default vitest suite green. No out-of-fence file was changed.

## Issues Encountered

- A parallel build/test run caused one transient `MSB3026` file-lock retry warning; rerunning `dotnet build DeckFlow.sln` in isolation produced a clean 0-warning build.
- The default vitest config rejects the new in-fence test path, so direct execution of `cut-lab.adjust.test.ts` would require an out-of-fence config change.

## Test Results

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` : PASS, 0 warnings, 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter CutLabViewModelWordingTests --no-restore` : PASS, 10/10 tests passed.
- `cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit` : PASS.
- `cd DeckFlow.Web && npm test` : PASS, 17 files / 69 tests passed.
- `cd DeckFlow.Web && npx --no-install vitest run wwwroot/ts/__tests__/cut-lab.adjust.test.ts` : NOT RUN via default config; repo include pattern excludes the fenced path.

## Next Phase Readiness

- The UI work is in place for Phase 106 follow-up QA and screenshots.
- If Phase 106 or later needs the new adjust vitest to run under the default suite, `DeckFlow.Web/vitest.config.ts` or the test location will need an out-of-fence update.

## Self-Check: PASSED

Reviewer note (Claude, 2026-07-22): flipped FAILED→PASSED after resolving the sole
failing criterion. The adjust vitest was authored at `wwwroot/ts/__tests__/cut-lab.adjust.test.ts`,
a path the repo's `vitest.config.ts` (`include: ['ts-tests/**/*.test.ts']`) does not
discover — the plan's declared path was wrong. Follow-up commit `0220d965` relocated it to
`ts-tests/cut-lab-adjust.test.ts` (sibling convention, ES imports), no config edit. Full
suite now 18 files / 71 tests, all green (was 17/69) — the 2 new adjust tests run and pass.
tsc clean, dotnet build 0/0, CutLabViewModelWordingTests 10/10. Both export gates exact-100,
under-100 branch copy exact, steppers iterate WorkingListRows, no compiled JS committed,
site.css untouched, EOL LF.
