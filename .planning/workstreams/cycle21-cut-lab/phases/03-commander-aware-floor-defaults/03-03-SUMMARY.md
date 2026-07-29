# 03-03 Summary

Verified on July 29, 2026.

## What Was Built

- Added `DeckFlow.Web/Services/CommanderBaselineKeys.cs` as the single shared commander-key candidate generator used by both baseline providers.
- Updated `DeckFlow.Web/Services/Manabase/CedhLandBaselineProvider.cs` to delegate to the shared helper without changing its matching behavior.
- Added `DeckFlow.Web/Services/CutLab/RoleFloorBaselineProvider.cs` as the fail-open runtime provider for `DeckFlow.Web/Data/role-floor-baseline/latest.json`.
- Registered `IRoleFloorBaselineProvider` in `DeckFlow.Web/Program.cs` beside the existing baseline registrations and added startup `EnsureLoaded()` warm-up beside the existing baseline warm-ups.
- Added `DeckFlow.Web.Tests/CommanderBaselineKeysTests.cs` and `DeckFlow.Web.Tests/RoleFloorBaselineProviderTests.cs` to pin the shared matching semantics, fail-open behavior, cached-miss behavior, DFC handling, and committed-snapshot resolution.

## Behavior Notes

- DFC commander names are never split on `" // "`. They are matched only as full single-card keys.
- The Phase 2 corpus contains zero partner-pair keys and 50 DFC keys in full `A // B` form. Partner and Background decks therefore resolve no commander-specific role-floor data here and correctly fall back to the bracket floor. This is intentional and avoids fabricating two-commander data from a solo commander's pattern.
- The role-floor provider fails open for missing, corrupt, truncated, or unreadable snapshot files by catching exactly `IOException`, `UnauthorizedAccessException`, and `JsonException`, logging once, returning no commander data, and caching the miss for the same 24-hour TTL.

## Final Gates

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Release --no-incremental`
  - Passed with `0` errors and exactly `9` warnings, all `CS8629` in `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -c Release`
  - Passed: `2118` passed / `16` skipped / `2134` total.
  - Baseline before this phase: `2104` passed / `16` skipped / `2120` total.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Release`
  - Passed: `2011` passed / `0` skipped / `2011` total.
  - Baseline before this phase: `2011` passed / `0` skipped / `2011` total.

## Deviations

- None.
