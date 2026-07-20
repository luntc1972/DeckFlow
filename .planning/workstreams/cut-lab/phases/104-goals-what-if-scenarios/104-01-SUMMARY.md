# 104-01 Summary

## What changed

- Added persisted Cut Lab goal settings with seeded defaults, server-side turn clamping, and pre-104 JSON back-compat.
- Threaded goal turns through Cut Lab snapshot and delta computation, including dynamic by-turn labels and the late-turn `CastPercent` fix for commander/plan metrics.
- Folded goal turns into snapshot and proposal-delta cache keys so the same pool no longer reuses stale 3/2/4 metrics after a goal edit.
- Passed `state.Goals` through the page-service baseline, current snapshot, and proposal-delta paths.
- Expanded serializer and simulation tests to cover defaults, clamping, goal-driven metric changes, determinism, and cache separation.

## Key files created or modified

- `DeckFlow.Web/Models/CutLab/CutLabGoals.cs`
- `DeckFlow.Web/Services/CutLab/CutLabGoalRules.cs`
- `DeckFlow.Web/Models/CutLab/CutLabState.cs`
- `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs`
- `DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs`
- `DeckFlow.Web/Services/CutLab/CutLabPageService.cs`
- `DeckFlow.Web/Services/CutLab/CutLabDeltaCache.cs`
- `DeckFlow.Web.Tests/CutLabStateSerializerTests.cs`
- `DeckFlow.Web.Tests/CutLabSimulationServiceTests.cs`

## Deviations

- The plan called for changing the existing `ICutLabSimulationService` method signatures in place. Doing that directly broke fake interface implementations in test files outside the allowed write set. To stay within the boundary and keep the build green, I kept the legacy interface methods and added goal-aware overloads plus default interface forwarding. The behavior, page-service wiring, cache-key changes, and test coverage match the plan intent.
- Verification had to use external `OutDir` locations under `C:/tmp/...` because a running local `DeckFlow.Web` process locked the normal project output path, and temporary in-repo build outputs caused recursive content pickup. Final build and test results below reflect the clean external-output runs.

## Self-check

- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj -p:OutDir=C:/tmp/df-final-webbuild/out/` succeeded with `0 Warning(s)` and `0 Error(s)`.
- `dotnet build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -p:OutDir=C:/tmp/df-final-testbuild/out/` succeeded with `0 Warning(s)` and `0 Error(s)`.
- `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabStateSerializerTests" -p:OutDir=C:/tmp/df-final-state/out/` passed: `20` tests.
- `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabSimulationServiceTests" -p:OutDir=C:/tmp/df-final-sim/out/` passed: `17` tests.
- Grep gates passed for `CutLabGoalRules.ClampGoals`, `goals.CommanderByTurn`, and `state.Goals`.
