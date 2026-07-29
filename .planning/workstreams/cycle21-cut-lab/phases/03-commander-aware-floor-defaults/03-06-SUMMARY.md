# 03-06 Summary

## What Was Built

- Added `DeckFlow.Web/Services/CutLab/CutLabFloorFeasibility.cs` as a pure aggregate floor-feasibility calculator that returns `null` when the resolved floor set fits and a conservative advisory payload when it does not.
- Added `CutLabViewModel.FloorFeasibility`, wired `CutLabFloorFeasibility.Evaluate(result.ResolvedFloors)` into `CutLabViewModel.From`, and added `BuildFloorFeasibilityMessage(...)` so the advisory copy is unit-testable.
- Added a role-floors warning banner above the table in `DeckFlow.Web/Views/Deck/CutLab.cshtml`, reusing the existing `warning-banner` class with no CSS changes.
- Added `DeckFlow.Web.Tests/CutLabFloorFeasibilityTests.cs` and extended `DeckFlow.Web.Tests/CutLabViewModelTests.cs` to pin the overlap correction, strict `>` boundary, payoffs behavior, candidate ordering, and conservative copy.

## Exact Correction Set

Required nonland slots are computed as:

```csharp
int requiredNonlandSlots =
    ramp +
    Math.Max(draw, engines) +
    interactionTargeted +
    interactionMass +
    protection +
    payoffs;
```

Applied corrections, and only these corrections:

1. `Math.Max(draw, engines)` replaces `draw + engines` because engines is a proven strict subset of draw in the classifier.
2. `wincons` is omitted from the required-slot arithmetic and treated as free-riding because combo-piece win conditions can co-occur with any other role and the overlap magnitude is unmeasured.

No third correction exists in this implementation. `payoffs` still counts additively because "can co-occur" is not the proven subset relationship that justified collapsing engines into draw, and no overlap magnitude for payoffs was measured. Discounting payoffs would therefore be an unauthorized third correction and would suppress the advisory in the case D-06 exists to catch.

## Advisory Copy

The advisory copy is emitted by `CutLabViewModel.BuildFloorFeasibilityMessage(...)` with these exact sentences:

1. `"These floors need at least {0} nonland slots, but only {1} remain after {2} lands and the commander."`
2. `"Relax {0} first."` when relax candidates exist; omitted entirely when none exist.
3. `"This is a conservative estimate — roles overlap, every engine is also a draw spell and win conditions usually double as another role, so the real requirement is at least this large and may be larger."`

That third sentence is the required honesty statement: the estimate is conservative, not exact.

## Relax Ordering

Relax candidates come only from slot-consuming roles in the corrected sum: `ramp`, `interaction-targeted`, `interaction-mass`, `protection`, `payoffs`, and whichever of `draw` / `engines` has the larger floor. `wincons` is excluded because relaxing it would not reduce the computed deficit.

Ordering is:

1. commander-driven raise descending (`CommanderValue - BracketValue`, or 0 when the commander did not raise the role),
2. then effective `Floor` descending,
3. then `RoleKey` ordinal ascending,
4. capped at three entries.

## Verification

Per-task verification completed before each task commit:

- Task 1: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Release`
- Task 2: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Release`
- Task 3: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -c Release --filter "FullyQualifiedName~CutLabFloorFeasibilityTests|FullyQualifiedName~CutLabViewModelTests"`

Final gates completed sequentially:

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Release --no-incremental`
  - Result: 0 errors, 9 warnings.
  - Warning count matched the baseline exactly.
  - All 9 warnings were the pre-existing `CS8629` warnings in `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -c Release`
  - Result: 2155 passed / 16 skipped / 2171 total.
  - Baseline was 2138 passed / 16 skipped / 2154 total.
  - Net change: +17 passed tests, skip count unchanged, no newly failing pre-existing tests.

## Deviation

No plan deviation.
