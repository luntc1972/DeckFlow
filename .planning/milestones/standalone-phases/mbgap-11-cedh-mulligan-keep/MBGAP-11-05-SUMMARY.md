---
phase: mbgap-11-cedh-mulligan-keep
plan: 05
subsystem: [ui, testing]
tags: [manabase, mulligan, cedh, razor, xunit]
requires:
  - phase: MBGAP-11-03
    provides: result DTO reads for plan-keepable, shape labels, and curve coverage
  - phase: MBGAP-11-04
    provides: result.ShowKeepShapes feature flag wiring from analysis
provides:
  - ShowKeepShapes wired from controller result into the view model and download artifact builder
  - cEDH opening-hand panel shows a second plan-keepable headline and shape-labeled representative openers
  - casual opening-hand panel shows the curve-coverage line when keep-shapes are enabled
  - ManabaseReportTextBuilder emits keep-shapes-gated prompt lines with OFF-state byte-identity tests
affects: [MBGAP-11-06, manabase-opening-hand, downloadable-artifact]
tech-stack:
  added: []
  patterns: [flag-gated additive render/artifact bytes, byte-identity excision tests]
key-files:
  created:
    - .planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-05-SUMMARY.md
  modified:
    - DeckFlow.Web/Models/ManabaseViewModel.cs
    - DeckFlow.Web/Controllers/ManabaseController.cs
    - DeckFlow.Web/Views/Deck/Manabase.cshtml
    - DeckFlow.Web/Models/ManabaseDisplay.cs
    - DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderMulliganTests.cs
    - DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs
key-decisions:
  - "Kept all new page/artifact bytes behind ShowKeepShapes/includeCedhKeepShapes so OFF stays byte-identical."
  - "Touched ManabaseDisplay.cs for a shared CurveCoverageText helper rather than duplicating view formatting logic."
  - "Proved the in-card OFF path with a dedicated keep-shapes excision render test in addition to existing mulligan-card excision coverage."
patterns-established:
  - "Opening-hand additions remain additive only: gate new spans/lines instead of mutating shared OFF-path text."
  - "Artifact/view wording stays aligned for rounded curve-coverage output via matching helper logic."
requirements-completed: [MBGAP-11-AC2, MBGAP-11-AC4, MBGAP-11-AC7, MBGAP-11-D01]
duration: 21min
completed: 2026-07-15
---

# Phase MBGAP-11-05 Summary

**ShowKeepShapes now drives the cEDH second headline, shape-labeled openers, casual curve coverage, and the downloadable mulligan artifact with OFF-state byte identity preserved.**

## Performance

- **Duration:** 21 min
- **Started:** 2026-07-15T00:23:00Z
- **Completed:** 2026-07-15T00:44:29Z
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments

- Wired `result.ShowKeepShapes` into [DeckFlow.Web/Models/ManabaseViewModel.cs](/mnt/c/users/chrislunt/source/personal/deckflow-mbgap11/DeckFlow.Web/Models/ManabaseViewModel.cs) and [DeckFlow.Web/Controllers/ManabaseController.cs](/mnt/c/users/chrislunt/source/personal/deckflow-mbgap11/DeckFlow.Web/Controllers/ManabaseController.cs), including the download builder call.
- Updated [DeckFlow.Web/Views/Deck/Manabase.cshtml](/mnt/c/users/chrislunt/source/personal/deckflow-mbgap11/DeckFlow.Web/Views/Deck/Manabase.cshtml) to show cEDH plan-keepable headlines plus shape labels, and the casual curve-coverage line, all behind `Model.ShowKeepShapes`.
- Extended [DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs](/mnt/c/users/chrislunt/source/personal/deckflow-mbgap11/DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs) and the Core/Web pin suites to prove ON behavior and OFF-state byte identity, including the Acceptance #7 excision case.

## Files Created/Modified

- [DeckFlow.Web/Models/ManabaseViewModel.cs](/mnt/c/users/chrislunt/source/personal/deckflow-mbgap11/DeckFlow.Web/Models/ManabaseViewModel.cs) - added `ShowKeepShapes`.
- [DeckFlow.Web/Controllers/ManabaseController.cs](/mnt/c/users/chrislunt/source/personal/deckflow-mbgap11/DeckFlow.Web/Controllers/ManabaseController.cs) - wired `ShowKeepShapes` into the view model and `includeCedhKeepShapes` into download text generation.
- [DeckFlow.Web/Views/Deck/Manabase.cshtml](/mnt/c/users/chrislunt/source/personal/deckflow-mbgap11/DeckFlow.Web/Views/Deck/Manabase.cshtml) - added gated second headline, shape label rendering, and casual curve-coverage row.
- [DeckFlow.Web/Models/ManabaseDisplay.cs](/mnt/c/users/chrislunt/source/personal/deckflow-mbgap11/DeckFlow.Web/Models/ManabaseDisplay.cs) - added `CurveCoverageText` helper.
- [DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs](/mnt/c/users/chrislunt/source/personal/deckflow-mbgap11/DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs) - added `includeCedhKeepShapes` and the gated artifact lines.
- [DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderMulliganTests.cs](/mnt/c/users/chrislunt/source/personal/deckflow-mbgap11/DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderMulliganTests.cs) - added cEDH/casual pins plus `KeepShapesOff_ByteIdenticalToBaseline`.
- [DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs](/mnt/c/users/chrislunt/source/personal/deckflow-mbgap11/DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs) - added keep-shapes render assertions and OFF-state excision proof.

## Build and Test Results

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -c Debug` after Task 1: passed.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -c Debug` after Task 2: passed.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug` after Task 3: passed.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" clean DeckFlow.sln -c Debug`: passed.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug`: passed with 0 warnings / 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~ManabaseReportTextBuilderMulliganTests"`: passed, 8/8.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -c Debug --filter "FullyQualifiedName~ManabaseViewRenderTests"`: passed, 30/30.

## EOL Check

- All touched files remained LF-only with unchanged carriage-return counts versus `HEAD`.
- Per-file check:
  - `DeckFlow.Web/Models/ManabaseViewModel.cs` `HEAD_CR=0 WORK_CR=0`
  - `DeckFlow.Web/Controllers/ManabaseController.cs` `HEAD_CR=0 WORK_CR=0`
  - `DeckFlow.Web/Views/Deck/Manabase.cshtml` `HEAD_CR=0 WORK_CR=0`
  - `DeckFlow.Web/Models/ManabaseDisplay.cs` `HEAD_CR=0 WORK_CR=0`
  - `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs` `HEAD_CR=0 WORK_CR=0`
  - `DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderMulliganTests.cs` `HEAD_CR=0 WORK_CR=0`
  - `DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs` `HEAD_CR=0 WORK_CR=0`
- `git diff --stat` matched `git diff --ignore-all-space --stat` across the touched `.cs` and `.cshtml` files after cleanup.

## Decisions Made

- Used the existing `manabase-mulliganlens-split` and `manabase-lens-big--soft` structures for the second headline, with no layout CSS changes.
- Kept the existing `workable line` / `no clear line` text path intact whenever `ShapeLabel` is empty so casual/off output stays unchanged.
- Rounded curve coverage to an invariant 0-5 whole-turn count in both view and artifact wording.

## Deviations from Plan

### Auto-fixed Issues

**1. SDK command-line compatibility**
- **Found during:** Final verification
- **Issue:** The exact command `dotnet build DeckFlow.sln -c Debug clean` is invalid under the installed .NET SDK (`MSB1008: Only one project can be specified`).
- **Fix:** Ran the equivalent clean/build sequence: `dotnet clean DeckFlow.sln -c Debug` then `dotnet build DeckFlow.sln -c Debug`.
- **Files modified:** None
- **Verification:** Clean succeeded; follow-up solution build succeeded with 0 warnings / 0 errors.

---

**Total deviations:** 1 auto-fixed
**Impact on plan:** No scope change. Verification used the SDK-equivalent clean/build sequence and all required tests still passed.

## Issues Encountered

- Task 1 initially failed to compile because the controller passed `includeCedhKeepShapes` before the builder signature accepted it. Resolved by adding the defaulted signature parameter before re-running the Task 1 build.
- The keep-shapes excision render test needed a tighter regex to remove only the gated cEDH block, and the Razor diff needed one indentation-only cleanup so `--stat` matched `--ignore-all-space --stat`.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- MBGAP-11-06 can build on the new view and artifact outputs for broader UI/theme/mobile coverage.
- No blockers remain in this slice; Acceptance #7 OFF-state byte identity is covered in both artifact and render tests.

---
*Phase: mbgap-11-cedh-mulligan-keep*
*Completed: 2026-07-15*
