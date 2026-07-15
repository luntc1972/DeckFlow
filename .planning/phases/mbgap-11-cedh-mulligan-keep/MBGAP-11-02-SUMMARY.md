---
phase: mbgap-11-cedh-mulligan-keep
plan: 02
subsystem: core
tags: [cedh, mulligan, keep-shapes, simulator, analyzer, tests]
requires:
  - phase: MBGAP-11-01
    provides: CedhMulliganCalibration constants and additive DTO fields for plan-keepable and shape outputs
provides:
  - cEDH three-shape keep gate inside SimulatePlanPresence
  - mode and keepShapes threading through Analyze and mulligan aggregation
  - commander exclusion from drawable plan library and IsInteractionSpell carry-through
  - plan-keepable surfacing on cEDH mulligan evaluation with keep-shape tests
affects: [MBGAP-11-03, mulligan-evaluation, plan-presence]
tech-stack:
  added: []
  patterns: [single-pass keep-shape gating inside SimulatePlanPresence, defaulted signature expansion for low-churn caller migration]
key-files:
  created:
    - .planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-02-SUMMARY.md
    - DeckFlow.Core.Tests/Manabase/CastabilitySimulatorKeepShapeTests.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerMulliganTests.cs
  modified:
    - DeckFlow.Core/Manabase/CastabilitySimulator.cs
    - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs
key-decisions:
  - "Kept the cEDH keep gate inside SimulatePlanPresence's existing trial loop so off-path behavior adds zero extra simulation work."
  - "Included interaction-only spells in the drawable plan library only for cEDH keep-shapes mode; casual and flag-off plan-presence behavior stays unchanged."
  - "Surfaced PlanKeepablePercent and PlanKeepableBand only when keepShapes && mode == Cedh; defaults remain 0/empty elsewhere."
patterns-established:
  - "Private helper expansion with defaulted params preserves existing callers while enabling new cEDH-only behavior."
  - "Representative opener shape labels are computed during the plan-presence pass and handed forward rather than recomputed later."
requirements-completed: [MBGAP-11-AC1, MBGAP-11-AC2, MBGAP-11-AC5, MBGAP-11-F01]
duration: 16min
completed: 2026-07-14
---

# Phase MBGAP-11-02 Summary

**cEDH keep-shape gating now runs inside plan-presence, credits acceleration and commander-premium starts, and surfaces plan-keepable on the mulligan evaluation without changing casual or flag-off reads**

## Performance

- **Duration:** 16 min
- **Started:** 2026-07-14T17:43:00-06:00
- **Completed:** 2026-07-14T17:59:32-06:00
- **Tasks:** 4
- **Files modified:** 5

## Accomplishments

- Threaded `mode` and defaulted `keepShapes` through `Analyze -> ComputeMulliganEvaluation -> SimulatePlanPresence`.
- Implemented Shape A / B / C keep gating in `SimulatePlanPresence`, plus `PlanKeepablePercent`, `PlanKeepableBand`, and per-shape percents in cEDH keep-shapes mode.
- Added `LibraryCard.IsInteractionSpell`, excluded commander spells from the drawable plan library, and surfaced plan-keepable on `ManabaseMulliganEvaluation`.
- Added deterministic keep-shape regression tests and analyzer surfacing tests.

## Files Created/Modified

- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` - cEDH keep-shape gate, commander-premium path, interaction bridge counting, library membership fix, opener shape labels.
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` - threaded `keepShapes`, updated mulligan aggregation signature, surfaced cEDH plan-keepable fields.
- `DeckFlow.Core.Tests/Manabase/CastabilitySimulatorKeepShapeTests.cs` - deterministic shape A/B/C, commander library exclusion, monotonicity, and flag-off tests.
- `DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerMulliganTests.cs` - cEDH surfacing and casual/flag-off default tests.
- `.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-02-SUMMARY.md` - execution summary.

## Caller Updates

- Updated production callers in `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` for both `SimulatePlanPresence(...)` and `ComputeMulliganEvaluation(...)`.
- Existing callers of `ComputeMulliganEvaluationForTest(...)` in `DeckFlow.Core.Tests/Manabase/ManabaseMulliganEvaluationTests.cs` required no edits because the new `mode` and `keepShapes` parameters are defaulted.
- Existing caller of `SimulatePlanPresence(...)` in `DeckFlow.Core.Tests/Manabase/CastabilitySimulatorPlanPresenceTests.cs` required no edits because the new `mode` and `keepShapes` parameters are defaulted.
- Added new direct cEDH keep-shapes callers in `DeckFlow.Core.Tests/Manabase/CastabilitySimulatorKeepShapeTests.cs` and `DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerMulliganTests.cs`.

## DTO Fields Added

- None in this plan. Plan 01 had already added `PlanKeepablePercent`, `PlanKeepableBand`, `ShapeExplosivePercent`, `ShapeEnginePercent`, `ShapeBridgePercent`, and `OpeningHandSample.ShapeLabel`.

## Build Results

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj -c Debug` -> `Build succeeded.`
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Debug` -> `Build succeeded.`
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug` -> `Build succeeded.` with `1 Warning(s)` and `0 Error(s)`; the warning is pre-existing in `DeckFlow.Web.Tests/MetaGapServiceTests.cs(302,109)` (`CS8602`).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Debug --filter \"FullyQualifiedName~CastabilitySimulatorKeepShapeTests|FullyQualifiedName~ManabaseAnalyzerMulliganTests\"` -> `Passed!` (`12` passed, `0` failed).

## EOL Check Result

- `git diff --stat` and `git diff --ignore-all-space --stat` matched for the touched tracked core files, confirming no whitespace-only churn.
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` CR count matched `HEAD` (`0` vs `0`).
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` CR count matched `HEAD` (`0` vs `0`).
- New test files were created LF-only (`CR=0`).

## Decisions Made

- Included interaction-only cards in `BuildLibrary` only when `keepShapes && mode == Cedh`, so Shape C can count non-permanent counterspells without changing legacy plan-presence scans.
- Preserved the existing plan-presence percent and role tally logic, layering the keep gate alongside it instead of replacing it.
- Used keep-shape precedence `Explosive > Engine > Bridge` when a hand satisfies multiple shapes, matching the plan.

## Deviations from Plan

### Auto-fixed Issues

**1. Environment verification blocker: missing local TypeScript install**
- **Found during:** Task 1 verification
- **Issue:** `DeckFlow.Web.csproj` failed the required solution build because `DeckFlow.Web/node_modules/typescript/bin/tsc` was missing from the worktree.
- **Fix:** Ran `npm ci` in `DeckFlow.Web` to restore the already-pinned devDependencies from `package-lock.json`.
- **Files modified:** None tracked
- **Verification:** Re-ran `dotnet build DeckFlow.sln -c Debug` and it succeeded

---

**Total deviations:** 1 auto-fixed (environment only)
**Impact on plan:** No code scope change. Necessary to satisfy the mandated solution-build verification.

## Issues Encountered

- The first Shape A test fixture assumed a two-land rock line would make four mana by turn 3 with `useManaQuantity=false`; the simulator models ordinary rocks as one mana in that mode, so the deterministic fixture was corrected to `3 lands + 1 rock -> 4 mana on turn 3`.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `OpeningHandSample.ShapeLabel` is now populated for cEDH keep-shapes samples, so plan 03 can stamp representative opener copy without recomputing the gate.
- `PlanKeepablePercent` and per-shape percents are available on the core DTOs and surfaced through the mulligan evaluation for cEDH consumers.

---
*Phase: mbgap-11-cedh-mulligan-keep*
*Completed: 2026-07-14*
