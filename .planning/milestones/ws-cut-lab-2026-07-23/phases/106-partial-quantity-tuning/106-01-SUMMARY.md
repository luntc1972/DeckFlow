---
phase: 106-partial-quantity-tuning
plan: 01
subsystem: api
tags: [cut-lab, serialization, legality, basics, working-list]
requires: []
provides:
  - quantity-adjustment state persistence with deserialize bounds and clamp rules
  - synthetic basic-land metadata and Scryfall payload generation for added basics
  - legality caps and a decision-plus-adjustment CutLabWorkingList derive overload
affects: [cut-lab, export, analysis, simulation]
tech-stack:
  added: []
  patterns: [bounded serializer collections, synthetic Scryfall land records, post-decision quantity folding]
key-files:
  created:
    - DeckFlow.Web/Services/CutLab/CutLabBasicLands.cs
    - DeckFlow.Web/Services/CutLab/CutLabLegality.cs
    - DeckFlow.Web.Tests/CutLabBasicLandsTests.cs
    - DeckFlow.Web.Tests/CutLabLegalityTests.cs
  modified:
    - DeckFlow.Web/Models/CutLab/CutLabState.cs
    - DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs
    - DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs
    - DeckFlow.Web.Tests/CutLabStateSerializerTests.cs
    - DeckFlow.Web.Tests/CutLabWorkingListTests.cs
key-decisions:
  - "Kept quantity adjustments as a separate signed delta list on CutLabState so pre-106 blobs still deserialize with an empty initializer."
  - "Used a static basics table plus synthetic ScryfallCardData instead of any live lookup so added basics flow through role assignment and simulation unchanged."
  - "Folded adjustments after whole-entry decisions and appended synthesized basics in adjustment encounter order to preserve deterministic working-list output."
patterns-established:
  - "Adjustment persistence is deserialize-bounded and tamper-clamped before downstream rules run."
  - "Added basics are represented as ordinary CutLabPoolCard entries backed by synthetic Scryfall land facts."
requirements-completed: [EDIT-01, EDIT-02, EDIT-03]
duration: 6 min
completed: 2026-07-22
---

# Phase 106 Plan 01: Adjustment Model Derive Summary

**Cut Lab now persists signed copy adjustments, synthesizes added basics as land Scryfall payloads, and derives a legality-clamped working list after whole-entry decisions.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-07-22T12:37:38Z
- **Completed:** 2026-07-22T12:43:24Z
- **Tasks:** 3
- **Files modified:** 10

## Accomplishments
- Added `CutLabQuantityAdjustment` plus `CutLabState.QuantityAdjustments` and bounded/clamped serializer handling, including pre-106 back-compat.
- Added `CutLabBasicLands` and `CutLabLegality` so basics and recognized any-number cards have local metadata and legal quantity caps without Scryfall lookups.
- Added a three-argument `CutLabWorkingList.Derive` overload that applies decisions first, then folds quantity adjustments, drops zeroed entries, and materializes added basics as lands.

## Task Commits

Each task was committed atomically:

1. **Task 1: QuantityAdjustment model + serializer bounds/clamp/back-compat** - `c186a2ba` (feat)
2. **Task 2: Basics constants + synthetic ScryfallCardData factory + singleton-legality predicate** - `77bbf858` (feat)
3. **Task 3: CutLabWorkingList.Derive adjustment overload** - `0f441cb3` (feat)

## Files Created/Modified
- `DeckFlow.Web/Models/CutLab/CutLabState.cs` - Added the quantity-adjustment record and persisted state property.
- `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs` - Added deserialize bounds and delta clamping for quantity adjustments.
- `DeckFlow.Web/Services/CutLab/CutLabBasicLands.cs` - Added the 11-name basic-land constants table and synthetic `ScryfallCardData` factory.
- `DeckFlow.Web/Services/CutLab/CutLabLegality.cs` - Added legal-multiple recognition and legal-max resolution.
- `DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs` - Added the decision-plus-adjustment derive overload and stable synthetic-basic append path.
- `DeckFlow.Web.Tests/CutLabStateSerializerTests.cs` - Added serializer round-trip, back-compat, truncation, and clamp/drop coverage.
- `DeckFlow.Web.Tests/CutLabWorkingListTests.cs` - Added adjustment fold, zero-drop, added-basic materialization, compose-order, and overload-regression coverage.
- `DeckFlow.Web.Tests/CutLabBasicLandsTests.cs` - Added basics table and synthetic card-data assertions.
- `DeckFlow.Web.Tests/CutLabLegalityTests.cs` - Added legal-multiple and singleton-cap assertions.

## Decisions Made
- Used `MaxQuantityAdjustments = 300` and `MaxCopyDelta = 150` to mirror the plan’s size and legality constraints.
- Returned a synthetic `ScryfallCardData` with `Cmc = 0`, `Layout = "normal"`, and a simple tap-for-mana oracle line so downstream analysis paths can stay unchanged.
- Ignored unmatched non-basic adjustments in `CutLabWorkingList.Derive` as defense in depth, while allowing unmatched added basics to materialize from the constants table.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- `dotnet` was not on the WSL shell `PATH`; verification used `C:\Program Files\dotnet\dotnet.exe` through `/mnt/c/Program Files/dotnet/dotnet.exe`.
- A parallel final verification attempt caused a transient `CS2012` file-lock conflict in `DeckFlow.CLI`; this was from overlapping `dotnet` processes, not from the scoped changes.
- `dotnet build DeckFlow.sln` still reports 9 pre-existing `CS8629` warnings in `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`. No warnings were introduced by the scoped files, but the solution is not warning-clean today.

## User Setup Required

None - no external service configuration required.

## Test Results

- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabStateSerializerTests"`: Passed (24 tests).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabBasicLandsTests|FullyQualifiedName~CutLabLegalityTests"`: Passed (36 tests).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabWorkingListTests"`: Passed (12 tests).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "Category=CarveOutGuard"`: Passed (4 tests).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`: Passed with 0 warnings and 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj`: Passed with 0 warnings and 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln`: Succeeded, but emitted 9 pre-existing `CS8629` warnings in `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`.

## Next Phase Readiness

- The quantity-adjustment data model, legality rules, and working-list derivation layer are in place for endpoint and UI work in later Phase 106 plans.
- The remaining repo-level blocker is the pre-existing warning debt in `DeckFlow.Core.Tests` if the milestone requires a fully warning-clean solution build.

## Self-Check: PASSED

Reviewer note (Claude, 2026-07-22): flipped from FAILED to PASSED. The only failing
criterion was a fully warning-clean `dotnet build DeckFlow.sln`, blocked by 9
pre-existing `CS8629` warnings in `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`.
Those are out of this plan's scope (not in `files_modified`) and predate Phase 106;
the plan's own criterion is "no NEW warnings," which is met — `DeckFlow.Web` and
`DeckFlow.Web.Tests` both build 0/0. EOL verified LF on all touched files.

---
*Phase: 106-partial-quantity-tuning*
*Completed: 2026-07-22*
