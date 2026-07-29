# 03-01 Summary

## What Built

- Added `DeckFlow.Core/Research/RoleFloorBaseline.cs` with the role-floor snapshot DTOs, the hardcoded six-role allowlist, and the pure adoption filter.
- Added `DeckFlow.Core/Research/RoleFloorBaselineDriftCheck.cs` with the fail-closed thresholds contract, findings/verdict DTOs, and all six drift rules.
- Added `DeckFlow.Core.Tests/RoleFloorBaselineTests.cs` covering all eight adoption-filter cases.
- Added `DeckFlow.Core.Tests/RoleFloorBaselineDriftCheckTests.cs` covering the drift rules plus missing-threshold failure.

## Task Commits

- Task 1: `08627b15` `feat(cutlab): add role-floor snapshot contract and adoption filter`
- Task 2: `241e4f73` `feat(cutlab): add fail-closed role-floor drift check`
- Task 3: `c90e44ea` `test(cutlab): cover role-floor adoption filter and drift rules`

## Mutation Check

Temporarily inverting the ordering so the `> 0` gate ran against raw `role.P25` caused `Build_P25BelowOne_IsDroppedAsNoSignal` to fail, proving the test pins truncate-then-test ordering rather than merely coexisting with it.

Observed failure output:

```text
[xUnit.net 00:00:00.29]     DeckFlow.Core.Tests.RoleFloorBaselineTests.Build_P25BelowOne_IsDroppedAsNoSignal [FAIL]
  Failed DeckFlow.Core.Tests.RoleFloorBaselineTests.Build_P25BelowOne_IsDroppedAsNoSignal [9 ms]
  Error Message:
   Assert.DoesNotContain() Failure: Item found in collection
             ↓ (pos 0)
Collection: ["ramp", "draw"]
Found:      "ramp"
```

After restoring the correct implementation, `Build_P25BelowOne_IsDroppedAsNoSignal` passed again and the full filtered `RoleFloorBaseline` test gate passed.

## Final Gates

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Release`
  - `0 Warning(s)`
  - `0 Error(s)`
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -c Release --filter "FullyQualifiedName~RoleFloorBaseline"`
  - `Failed: 0`
  - `Passed: 19`
  - `Skipped: 0`
  - `Total: 19`

## Deviations

- None.
