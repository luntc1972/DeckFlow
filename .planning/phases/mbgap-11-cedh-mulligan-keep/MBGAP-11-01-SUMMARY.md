# MBGAP-11-01 Summary

## Files Changed

- `DeckFlow.Core/Manabase/CedhMulliganCalibration.cs`
- `DeckFlow.Core/Manabase/ManabaseModels.cs`
- `DeckFlow.Core.Tests/Manabase/CedhMulliganCalibrationTests.cs`
- `.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-01-SUMMARY.md`

## Constant Values

- `TurnCapExplosive = 3`
- `TurnCapEngine = 2`
- `RepresentativeLineTurnCap = 4`
- `BridgeInteractionMin = 2`
- `BridgeDevelopmentMin = 2`
- Added `GetRepresentativeLineTurnCap(ManabaseMode mode)` accessor for later cEDH-only representative-line consumers.

## DTO Fields Added

### `OpeningHandSample`

- `string ShapeLabel { get; init; } = string.Empty;`

### `ManabasePlanPresence`

- `int PlanKeepablePercent { get; init; }`
- `string PlanKeepableBand { get; init; } = string.Empty;`
- `int ShapeExplosivePercent { get; init; }`
- `int ShapeEnginePercent { get; init; }`
- `int ShapeBridgePercent { get; init; }`

### `ManabaseMulliganEvaluation`

- `int PlanKeepablePercent { get; init; }`
- `string PlanKeepableBand { get; init; } = string.Empty;`
- `double CurveCoverageTurns { get; init; }`

## Build Result

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj -c Debug` -> succeeded with `0 Warning(s)` and `0 Error(s)`
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Debug` -> succeeded with `0 Warning(s)` and `0 Error(s)`
- `git diff --stat` and `git diff --ignore-all-space --stat` matched for the modified tracked file, confirming no whitespace/EOL churn in `ManabaseModels.cs`

## Deviation

- None.
