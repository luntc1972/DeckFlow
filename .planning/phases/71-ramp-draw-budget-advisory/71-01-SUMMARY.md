---
phase: 71-ramp-draw-budget-advisory
plan: 01
subsystem: core
tags: [manabase, classifier, advisory, ramp, draw, testing]
requires: []
provides:
  - advisory ramp/draw bucket counts on ManabaseDeck
  - pure ramp/draw slot-budget calculator with commander and curve-proxy thresholds
affects: [phase-71, manabase, plain-language-verdict]
tech-stack:
  added: []
  patterns:
    - additive advisory-only classifier fields that never feed the land-target path
    - pure threshold/interpolation calculator with deterministic xUnit anchors
key-files:
  created:
    - DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseRampDrawBucketTests.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseRampDrawBudgetTests.cs
  modified:
    - DeckFlow.Core/Manabase/ManabaseClassifier.cs
    - DeckFlow.Core/Manabase/ManabaseModels.cs
key-decisions:
  - "Kept IsRampOrDraw byte-identical and added separate budget-only predicates for advisory bucket counts."
  - "Implemented draw-budget widening with one compiled regex plus a literal draw-a-card check so wheels and repeatable draw are counted."
  - "Used a zero-based ceil 75th-percentile index for the curve proxy so the locked {1,1,2,2,3,3,4,6} fixture resolves to 4."
patterns-established:
  - "Advisory-only classifier outputs can extend ManabaseDeck without affecting report verdict inputs."
  - "Ramp/draw slot targeting lives in a pure calculator, not in classifier or analyzer land math."
requirements-completed: [PLV-BUDGET]
completed: 2026-06-26T22:56:57-06:00
---

# Phase 71-01 Summary

**Advisory ramp/draw bucket counts and a pure 24-slot ramp/draw budget calculator now exist in Core without changing the historical land-target path.**

## What Was Built

- Added `RampPieceCount`, `DrawPieceCount`, and `RampDrawBothCount` to `ManabaseDeck` as advisory-only fields with 0.5/0.5 overlap handling.
- Extended `ManabaseClassifier` with separate budget-only ramp/draw predicates, including wheel and repeatable-draw coverage, while leaving `IsRampOrDraw` untouched.
- Created `ManabaseRampDrawBudget`, `ManabaseRampDrawThresholdSource`, and `ManabaseRampDrawBudgetCalculator` for commander-threshold / curve-proxy targeting, interpolation, and nudge flags.
- Added focused xUnit coverage for overlap cards, wheels, Mystic-Remora-style draw, land-target regression safety, threshold selection, interpolation anchors, and balanced/light/heavy outcomes.

## Task Commits

1. Task 1: ramp/draw budget bucket counts on `ManabaseDeck` - `6fd02f7`
2. Task 2: ramp/draw slot-budget calculator - `d4314c6`

## Files Created/Modified

- `DeckFlow.Core/Manabase/ManabaseClassifier.cs` - advisory bucket accumulation and budget-only predicates
- `DeckFlow.Core/Manabase/ManabaseModels.cs` - new advisory bucket fields on `ManabaseDeck`
- `DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs` - threshold source enum, budget record, pure calculator
- `DeckFlow.Core.Tests/Manabase/ManabaseRampDrawBucketTests.cs` - classifier bucket and regression coverage
- `DeckFlow.Core.Tests/Manabase/ManabaseRampDrawBudgetTests.cs` - threshold/interpolation/nudge coverage

## Decisions / Deviations

- Decisions:
  - Followed the plan exactly on keeping the historic land-target predicate isolated.
  - Exposed an internal interpolation helper so the `T=3.5 -> 11` midpoint anchor is testable.
- Deviations:
  - None from scope or behavior. One test assertion was corrected during RED to use existing land-target breakdown fields instead of a non-existent `RawTarget` property.

## Test Results

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj -clp:ErrorsOnly` -> passed, 0 warnings, 0 errors
- `cmd.exe /c "dotnet test DeckFlow.Core.Tests\DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~ManabaseRampDrawBucketTests"` -> 4/4 passed
- `cmd.exe /c "dotnet test DeckFlow.Core.Tests\DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~ManabaseRampDraw"` -> 15/15 passed
- `cmd.exe /c "dotnet test DeckFlow.Core.Tests\DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~Manabase"` -> 172/172 passed

## Self-Check

PASSED
