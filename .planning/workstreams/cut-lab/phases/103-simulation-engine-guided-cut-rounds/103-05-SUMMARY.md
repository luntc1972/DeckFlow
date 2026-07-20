# Plan 103-05 Summary

## Outcome

- Added `ManabaseAnalyzer.Analyze(..., int? trialsOverride = null)` as pure trial-count parameterization of the existing simulator path.
- Added `CutLabSimulationService` to build 7-family working-list snapshots and proposal deltas by reusing the existing resolve -> classify -> analyze pipeline.
- Added `CutLabBaselineSnapshot` to build the compact D-12 baseline through the same simulation service at full default trials.
- Registered the Cut Lab simulation service and baseline builder in `AddDeckFlowCutLabServices`.

## Trial-Count Decision Applied

- In-loop Cut Lab simulation uses `4000` trials.
- Baseline snapshot and other full-fidelity callers continue to use the default `20000` trials by passing no override.
- Source: `103-01-SUMMARY.md` measured `5164 ms` at `20000` trials on a 147-card pool.

## Files Changed

- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs`
- `DeckFlow.Core.Tests/ManabaseAnalyzerTrialsOverrideTests.cs`
- `DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs`
- `DeckFlow.Web/Services/CutLab/CutLabBaselineSnapshot.cs`
- `DeckFlow.Web/Extensions/CutLabServiceCollectionExtensions.cs`
- `DeckFlow.Web.Tests/CutLabSimulationServiceTests.cs`
- `DeckFlow.Web.Tests/CutLabBaselineSnapshotTests.cs`

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~ManabaseAnalyzerTrialsOverride"`: passed (`2/2`).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabSimulationService"`: passed (`7/7`).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabBaselineSnapshot"`: passed (`4/4`).
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab"`: passed (`148/148`).

## Commits

- `feat(103-05): parameterize manabase analyzer trials`
- `feat(103-05): add cut lab simulation service`
- `feat(103-05): add cut lab baseline snapshot`

## Deviations

- None.
