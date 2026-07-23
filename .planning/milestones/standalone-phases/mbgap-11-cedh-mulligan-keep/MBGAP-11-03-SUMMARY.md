---
phase: mbgap-11-cedh-mulligan-keep
plan: 03
subsystem: manabase
tags: [cedh, mulligan, opener-selection, commander-centrality, curve-coverage, tests]
requires:
  - phase: MBGAP-11-02
    provides: keep-shape gating, representative opener samples, ShapeLabel/CurveCoverageTurns DTO fields
provides:
  - D-02 commander-centrality heuristic in ManabaseAnalyzer
  - cEDH representative opener turn-cap and commander eligibility rewrite
  - D-03 SimulateCurveCoverage wired into mulligan evaluation
affects: [MBGAP-11, mulligan evaluation, castability simulator, cEDH opener surfacing]
tech-stack:
  added: []
  patterns: [internal analyzer test seam, dedicated deck-level simulator pass, cEDH-only opener selection branch]
key-files:
  created:
    - DeckFlow.Core.Tests/Manabase/ManabaseCommanderCentralityTests.cs
    - DeckFlow.Core.Tests/Manabase/CastabilitySimulatorCurveCoverageTests.cs
  modified:
    - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs
    - DeckFlow.Core/Manabase/CastabilitySimulator.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerMulliganTests.cs
key-decisions:
  - "Used a D-02 helper fed only by already-computed castability + deck roles; no extra simulation."
  - "Kept commander inclusion and representative-line turn cap strictly inside the cEDH keep-shapes branch."
  - "Implemented curve coverage as a separate pass over castable candidates per turn so it stays role-independent."
patterns-established:
  - "Commander centrality requires cEDH mode, non-Low importance, CedhSupportThreshold castability, and a win-directed commander role unless the entire deck is untagged."
  - "Curve coverage counts each turn 1-5 at most once and short-circuits on the first castable candidate."
requirements-completed: [MBGAP-11-AC1, MBGAP-11-AC3, MBGAP-11-AC4, MBGAP-11-D02, MBGAP-11-D03]
duration: unknown
completed: 2026-07-14
---

# Phase MBGAP-11 Plan 03 Summary

**Commander-central cEDH openers now respect a turn-4 representative-line cap, surface keep-shape labels, and casual keep-shapes now compute role-independent curve coverage.**

## Files Changed

- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs`
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs`
- `DeckFlow.Core.Tests/Manabase/ManabaseCommanderCentralityTests.cs`
- `DeckFlow.Core.Tests/Manabase/CastabilitySimulatorCurveCoverageTests.cs`
- `DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerMulliganTests.cs`

## DTO / Field Additions

- No new DTO fields were added in this plan.
- Consumed existing `OpeningHandSample.ShapeLabel` and `ManabaseMulliganEvaluation.CurveCoverageTurns`.
- Added internal test seams only:
  - `ManabaseAnalyzer.IsCommanderCentralForTest(...)`
  - `ManabaseAnalyzer.ComputeMulliganEvaluationForTest(...)` optional `importance` / `planPresence` parameters

## Outcome

- Added `IsCommanderCentral(...)` in `ManabaseAnalyzer` with the D-02 heuristic:
  - `mode == Cedh`
  - `CommanderImportance != Low`
  - strongest commander castability row meets `CedhSupportThreshold` (88)
  - commander has a win-directed `PlanRole` (`Payoff|Engine|TutorCombo`)
  - narrow fallback to `(importance + castability)` only when the whole deck is untagged
- Rewrote representative opener selection for `keepShapes && mode == Cedh`:
  - no `OnCurveTurn >= 5` row can surface as a workable representative line
  - commander rows enter the opener pool only when `IsCommanderCentral(...)` is true
  - commander rows are preferred when they deploy ahead of printed curve
  - casual / flag-off opener selection remains unchanged
- Updated `BuildPlanOpenerSample(...)` / shape-label text so cEDH keep-shape samples emit:
  - `explosive keep`
  - `engine keep`
  - `bridge keep`
  - `no plan by turn 4 - mulligan`
- Added `CastabilitySimulator.SimulateCurveCoverage(...)`:
  - separate deck-level pass
  - iterates eligible castable candidates each turn 1..5
  - counts each turn at most once
  - short-circuits on first covered candidate
  - wired into `ManabaseMulliganEvaluation.CurveCoverageTurns` only when `keepShapes` is on

## Verification

- Task 1 verify:
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Debug`
  - Result: `Build succeeded.`
- Task 2 verify:
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug`
  - Result: `Build succeeded.`
- Task 3 verify:
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Debug`
  - Result: `Build succeeded.`
- Final clean/build:
  - Requested command `dotnet build DeckFlow.sln -c Debug clean` is invalid MSBuild syntax (`MSB1008`)
  - Equivalent command run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug -t:Clean`
  - Result: `Build succeeded.`
  - Follow-up full build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug`
  - Result: `Build succeeded.` with one pre-existing unrelated warning in `DeckFlow.Web.Tests/MetaGapServiceTests.cs(302,109)`
- Final filtered tests:
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~ManabaseCommanderCentralityTests|FullyQualifiedName~CastabilitySimulatorCurveCoverageTests|FullyQualifiedName~ManabaseAnalyzerMulliganTests"`
  - Result: `Passed! Failed: 0, Passed: 14, Skipped: 0, Total: 14`

## EOL Check

- `git diff --stat` == `git diff --ignore-all-space --stat` for:
  - `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs`
  - `DeckFlow.Core/Manabase/CastabilitySimulator.cs`
- `\r` count vs `HEAD`:
  - `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs`: working tree `0`, `HEAD` `0`
  - `DeckFlow.Core/Manabase/CastabilitySimulator.cs`: working tree `0`, `HEAD` `0`
  - `DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerMulliganTests.cs`: working tree `0`, `HEAD` `0`
  - `DeckFlow.Core.Tests/Manabase/ManabaseCommanderCentralityTests.cs`: new file, working tree `0`
  - `DeckFlow.Core.Tests/Manabase/CastabilitySimulatorCurveCoverageTests.cs`: new file, working tree `0`

## Caller / Signature Notes

- No production caller broke on a new public signature.
- Updated the internal analyzer call site to pass sim parameters into `ComputeMulliganEvaluation(...)`.
- Updated tests to use the expanded internal seam where needed.

## Deviations

- One execution-only deviation:
  - The plan’s final clean command used invalid CLI syntax (`dotnet build ... clean`).
  - Used the equivalent valid clean target invocation: `dotnet build DeckFlow.sln -c Debug -t:Clean`.
  - No code-scope deviation from the plan.
