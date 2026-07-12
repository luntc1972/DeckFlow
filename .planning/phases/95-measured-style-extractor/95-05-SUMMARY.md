# Plan 95-05 Summary

## What Was Built

- Added `CategoryCounter` as a pure static helper for measured-style extraction, with per-deck category counts, deduped deck-level presence, and mean per-deck aggregate counts.
- The category counter applies `CategoryFilter.IncludedOrFallback` but still drops generic type labels from counting, so a multi-bucket card contributes to every included bucket while a creature-only label contributes to none.
- Added `LiftCalculator` as a pure static helper that computes D-07 lift as creator `Pr(A∩B)` over global `Pr(A)·Pr(B)`, using canonical sorted `catA|catB` keys and omitting pairs whose baseline denominator is missing or zero.
- Added focused xUnit coverage for the locked CS-06 and CS-07 behaviors: multi-bucket counting, creature-only exclusion, deduped deck presence, sorted pair-key handling, zero-baseline omission, and the staple-demoting lift case.

## Key Files

- Added `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/CategoryCounter.cs`
- Added `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/LiftCalculator.cs`
- Added `DeckFlow.Core.Tests/MeasuredStyleExtraction/CategoryCounterTests.cs`
- Added `DeckFlow.Core.Tests/MeasuredStyleExtraction/LiftCalculatorTests.cs`

## Decisions And Deviations

- Category lookup checks `DeckEntry.Name` first, then `DeckEntry.NormalizedName`, so the helper remains tolerant of whichever key shape the host resolved into `MeasuredStyleInputs.CardCategories`.
- `CountPerDeck` increments by `DeckEntry.Quantity`, while `DeckCategoryPresence` remains boolean-per-category for lift numerators.
- `LiftCalculator` reads the canonical sorted pair key from `GlobalCategoryBaseline.DecksWithCategoryPair` and omits pairs when the global marginal denominator cannot be formed; this avoids propagating `NaN` or `Infinity` downstream.
- No scope deviations. No edits outside the fenced files, and no changes to earlier 95-01..04 artifacts.

## Verification Status

- `dotnet.exe build DeckFlow.Core/DeckFlow.Core.csproj`: PASSED
- `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~CategoryCounterTests"`: PASSED
- `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~LiftCalculatorTests"`: PASSED
- `grep -c "\.First()\|\.Take(1)" DeckFlow.Core/Knowledge/MeasuredStyleExtraction/CategoryCounter.cs`: `0`
- `demotes staples` test: PASSED
- `zero-baseline omission` test: PASSED
- LF-only check on all five plan-95-05 files: PASSED

## Self-Check

- PASSED
