---
phase: 106-partial-quantity-tuning
plan: 02
subsystem: api
tags: [cut-lab, working-list, quantity-adjustments, export, what-if, analysis]
requires:
  - phase: 106-partial-quantity-tuning
    provides: quantity-adjustment persistence, synthetic basic metadata, and the three-argument working-list derive overload
provides:
  - adjustment-derived working lists across page, API, export, what-if, and overshoot consumers
  - synthetic resolved-card augmentation for added basics without Scryfall lookups
  - BuildState preservation of quantity adjustments during full-page reconstruction
  - regression coverage for count, role-floor, export, and overshoot consistency
affects: [cut-lab, export, analysis, simulation, what-if]
tech-stack:
  added: []
  patterns: [single-source working-list derivation, synthetic basic resolved-card augmentation, reconstruction carry-forward regression coverage]
key-files:
  created: []
  modified:
    - DeckFlow.Web/Models/CutLabViewModel.cs
    - DeckFlow.Web/Services/CutLab/CutLabPageService.cs
    - DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs
    - DeckFlow.Web/Services/CutLab/CutLabExportService.cs
    - DeckFlow.Web/Services/CutLab/CutLabWhatifPreviewService.cs
    - DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs
    - DeckFlow.Web/Controllers/Api/CutLabApiController.cs
    - DeckFlow.Web/Controllers/CutLabController.cs
    - DeckFlow.Web.Tests/CutLabWorkingListTests.cs
    - DeckFlow.Web.Tests/CutLabDecisionApplierTests.cs
    - DeckFlow.Web.Tests/CutLabPageServiceTests.cs
    - DeckFlow.Web.Tests/CutLabAnalysisContextBuilderTests.cs
    - DeckFlow.Web.Tests/CutLabWhatifTests.cs
key-decisions:
  - "Derived the working list once in CutLabViewModel.From and reused it for current-count and floor display totals so UI counts match the same folded state every other consumer reads."
  - "Centralized synthetic-basic augmentation in CutLabAnalysisContextBuilder and reused it from what-if subset seeding so analysis, export, and what-if inherit the same no-network behavior."
  - "Kept the BuildState fix as an explicit QuantityAdjustments initializer to preserve the existing object-initializer style and minimize byte churn."
patterns-established:
  - "Any consumer that derives the current Cut Lab pool should pass state.QuantityAdjustments into CutLabWorkingList.Derive."
  - "Resolved-card subsets for derived pools should be augmented with CutLabBasicLands synthetic payloads before missing-card/network logic."
requirements-completed: [EDIT-01, EDIT-03]
duration: 9 min
completed: 2026-07-22
---

# Phase 106 Plan 02: Consumer Fold Summary

**Cut Lab now treats the adjustment-derived working list as the shared truth for count, floors, analysis, simulation, what-if, export, and full-page reconstruction, with added basics resolved locally instead of through Scryfall.**

## Performance

- **Duration:** 9 min
- **Started:** 2026-07-22T06:52:11-06:00
- **Completed:** 2026-07-22T07:00:59-06:00
- **Tasks:** 4
- **Files modified:** 13

## Accomplishments

- Folded `QuantityAdjustments` through every in-scope `CutLabWorkingList.Derive` consumer so page, API, export, what-if, and overshoot logic all read the same tuned list.
- Added synthetic basic `ScryfallCardData` augmentation before missing-card resolution and derived-pool subset seeding, eliminating network dependency for added basics.
- Preserved `QuantityAdjustments` in `CutLabPageService.BuildState` and locked the new behavior with regression tests for counts, floors, export, and budget handling.

## Task Commits

Each task was committed atomically:

1. **Task 1: Fold adjustments into every Derive consumer + role-floor display counts** - `9c9a122e` (fix)
2. **Task 2: Inject synthetic added-basic ScryfallCardData into analysis + what-if resolved sets** - `03680b05` (fix)
3. **Task 3: Preserve QuantityAdjustments in CutLabPageService.BuildState** - `f4fdd303` (fix)
4. **Task 4: Regression tests — counts, overshoot budget, and export reflect adjustments** - `31c433e8` (test)

## Files Created/Modified

- `DeckFlow.Web/Models/CutLabViewModel.cs` - Reused the adjustment-derived working list for `CurrentCount`, land-floor display counts, and what-if card-out options.
- `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` - Derived the working list with adjustments and preserved `QuantityAdjustments` during BuildState reconstruction.
- `DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs` - Added centralized synthetic-basic augmentation for resolved-card caches and derived-pool seeding.
- `DeckFlow.Web/Services/CutLab/CutLabExportService.cs` - Export now reconstructs from the adjustment-derived working list.
- `DeckFlow.Web/Services/CutLab/CutLabWhatifPreviewService.cs` - What-if preview derives with adjustments and pre-seeds synthetic basic resolved cards.
- `DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs` - Overshoot budget now measures against the adjustment-derived list.
- `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` - JSON decide/what-if validation and rebuild paths now derive with adjustments.
- `DeckFlow.Web/Controllers/CutLabController.cs` - No-JS what-if validation now derives with adjustments.
- `DeckFlow.Web.Tests/CutLabWorkingListTests.cs` - Added count-delta regressions for positive and negative quantity adjustments.
- `DeckFlow.Web.Tests/CutLabDecisionApplierTests.cs` - Added overshoot-budget regression proving added-basic deltas affect accept validation.
- `DeckFlow.Web.Tests/CutLabPageServiceTests.cs` - Added regressions for adjusted floor/count display, BuildState carry-forward, CardsRemaining deltas, and export reconstruction.
- `DeckFlow.Web.Tests/CutLabAnalysisContextBuilderTests.cs` - Added no-network synthetic-basic analysis coverage.
- `DeckFlow.Web.Tests/CutLabWhatifTests.cs` - Added what-if coverage proving an added basic can be selected as `cardOut`.

## Decisions Made

- Kept the synthetic-basic helper in `CutLabAnalysisContextBuilder` instead of creating a new shared utility file so the change stayed inside the plan fence.
- Covered export regression behavior from `CutLabPageServiceTests` because the dedicated export test file was outside this plan’s `files_modified` fence.
- Left `RoleGroups` and other non-count display surfaces unchanged because the plan only required current-count, role-floor display counts, and working-list consumers to fold adjustments.

## Deviations from Plan

None - plan executed exactly as written within the file fence.

## Issues Encountered

- `dotnet` was not on the WSL shell `PATH`; all verification used `"/mnt/c/Program Files/dotnet/dotnet.exe"`.
- A parallel final verification attempt caused a transient `CS2012` file-lock failure in `DeckFlow.Core`; rerunning `dotnet build DeckFlow.sln` sequentially succeeded cleanly, so this was an execution artifact rather than a code issue.

## User Setup Required

None - no external service configuration required.

## Test Results

- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabPageServiceTests.From_QuantityAdjustmentsDriveCurrentCountAndLandFloorDisplay"`: Passed (1 test).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj`: Passed with 0 warnings and 0 errors after Task 1.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabAnalysisContextBuilderTests|FullyQualifiedName~CutLabWhatifTests"`: Passed (21 tests).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabPageServiceTests"`: Passed (51 tests).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabDecisionApplierTests|FullyQualifiedName~CutLabPageServiceTests|FullyQualifiedName~CutLabWorkingListTests"`: Passed (77 tests).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabApiControllerTests|FullyQualifiedName~CutLabWhatifTests|FullyQualifiedName~CutLabAnalysisContextBuilderTests|FullyQualifiedName~CutLabPageServiceTests|FullyQualifiedName~CutLabDecisionApplierTests|FullyQualifiedName~CutLabWorkingListTests"`: Passed (114 tests).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "Category=CarveOutGuard"`: Passed (4 tests).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln"`: Passed with 0 warnings and 0 errors on the final sequential rerun.

## Next Phase Readiness

- Read-path folding is complete: count, analysis, roles, simulation, export, and what-if now agree on the same adjustment-derived working list.
- Full-page reconstruction no longer drops quantity adjustments, so the write-path endpoint work in later plans can build on stable persisted state behavior.

## Self-Check: PASSED

---
*Phase: 106-partial-quantity-tuning*
*Completed: 2026-07-22*
