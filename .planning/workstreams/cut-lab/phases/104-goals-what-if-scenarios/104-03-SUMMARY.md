# 104-03 Summary

## What changed

- Added `CutLabWhatifApiRequest` / `CutLabWhatifApiResponse` plus a shared `ICutLabWhatifPreviewService` / `CutLabWhatifPreviewService` for side-effect-free swap previews.
- Added `PostWhatifAsync` and `PostWhatifCommitAsync` to `CutLabApiController`, with the same-origin, request-size, and bad-request guard pattern mirrored from `PostDecideAsync`.
- Registered the `whatif-swap` round key in `CutLabCutRoundEngine` and threaded `goals: state.Goals` into the existing decide-path `ComputeProposalDeltas` call.
- Registered `ICutLabWhatifPreviewService` in `Program.cs`.
- Added tests for cut-pile swap candidates, zero-`ResolveSingleAsync` preview seeding, preview guards, atomic commit behavior, and goal-aware decide deltas.

## Verification

- `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabWhatifTests|FullyQualifiedName~CutLabWorkingListTests" -p:OutDir=C:/tmp/df-10403-task1-green3/out/`
  Passed: `16` tests, `0` failed.
- `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabApiControllerTests" -p:OutDir=C:/tmp/df-10403-task2-green2/out/`
  Passed: `15` tests, `0` failed.
- `dotnet build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -p:OutDir=C:/tmp/df-10403-webtests-build/out/`
  Succeeded with `0 Warning(s)` and `0 Error(s)`.
- `grep -n "ICutLabWhatifPreviewService" DeckFlow.Web/Program.cs`
  Matched the DI registration.
- `grep -n "PostWhatifAsync\\|PostWhatifCommitAsync\\|goals: state.Goals" DeckFlow.Web/Controllers/Api/CutLabApiController.cs`
  Matched the preview endpoint, commit endpoint, and goal-aware decide call.
- `rg -n "WhatifSwapKey|WhatifSwapLabel|A hypothetical swap you kept" DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs`
  Matched the round key constant plus all three registration points.

## Notes

- `dotnet build DeckFlow.sln -p:OutDir=C:/tmp/df-10403-final-build/out/` succeeded but still reported 9 existing `CS8629` warnings in `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`, which is outside this plan's allowed write set.
- A direct `DeckFlow.Web.csproj` build to its own `OutDir` hit a local file lock on `DeckFlow.Web/obj/Debug/net10.0/DeckFlow.Web.dll` from a running `DeckFlow.Web` process, so the clean no-warning verification used `DeckFlow.Web.Tests.csproj`, which rebuilt `DeckFlow.Web` successfully as part of the test project build.
