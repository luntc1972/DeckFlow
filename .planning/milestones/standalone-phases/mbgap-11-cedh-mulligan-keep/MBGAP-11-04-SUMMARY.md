---
phase: mbgap-11-cedh-mulligan-keep
plan: 04
subsystem: web
tags: [feature-flags, manabase, cache, tests, postgres, sqlite]
requires:
  - phase: MBGAP-11-02
    provides: keepShapes analyzer parameter in Core
  - phase: MBGAP-11-03
    provides: cEDH mulligan/keep-shape output plumbing
provides:
  - keep-shapes web-layer feature flag wiring with MED-2 gating
  - catalog + Postgres/SQLite seed rows for analysis.manabase.keep-shapes default OFF
  - precautionary PromptMutatingAnalysisFlags registration with no-cache rationale
affects: [MBGAP-11-05, manabase, feature-flags, packet-cache]
tech-stack:
  added: []
  patterns: [flag resolution via IsFlagOn fail-safe OFF, prompt-cache insurance registry, mixed-EOL preservation]
key-files:
  created: [.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-04-SUMMARY.md]
  modified:
    - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
    - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
    - DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs
    - DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs
key-decisions:
  - "Resolved keepShapes as keepShapesFlag && showMulliganEval so no hidden role classification or shape simulation runs when the opening-hand block is suppressed."
  - "Added keep-shapes to PromptMutatingAnalysisFlags as inert-today cache insurance because current manabase text is rebuilt per request, but future cache-routing should not risk stale flag-ON replay."
patterns-established:
  - "Manabase web flags that render only inside the mulligan block must be gated on showMulliganEval before any downstream I/O or sim work."
  - "New prompt-mutating or potentially cache-routed flags should join PromptMutatingAnalysisFlags even when today's path is rebuilt per request."
requirements-completed: [MBGAP-11-AC7]
duration: 9min
completed: 2026-07-14
---

# Phase MBGAP-11-04 Summary

**Web-layer keep-shapes flag wiring now resolves OFF by default, gates hidden work behind mulligan visibility, and is registered for future packet-cache safety without changing current OFF-path output**

## Performance

- **Duration:** 9 min
- **Started:** 2026-07-14T18:23:00-06:00
- **Completed:** 2026-07-14T18:32:01-06:00
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments

- Added `analysis.manabase.keep-shapes` web wiring in `ManabaseAnalysisService` with a new flag constant, MED-2 gating (`keepShapesFlag && showMulliganEval`), widened cEDH role-classification gating, analyzer threading, and `ShowKeepShapes` surfaced on both result construction paths.
- Catalogued the new flag and seeded it OFF in both SQL dialect blocks exactly once each: Postgres `FALSE`, SQLite `0`.
- Registered keep-shapes in `PromptMutatingAnalysisFlags` with a code comment documenting that it is inert today because manabase text is rebuilt per request, but retained as protection against future cache-routing regressions.

## Task Commits

No commits were created in this session.

## Files Created/Modified

- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` - added keep-shapes flag resolution, MED-2 gate, analyzer arg, cEDH role-classification widening, and `ShowKeepShapes`.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` - added the operator-facing keep-shapes description.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` - added one OFF seed row in each dialect block.
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` - added keep-shapes to `PromptMutatingAnalysisFlags` with the precautionary cache comment and imported the manabase namespace for the constant reference.
- `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` - added the explicit seeded-key guard entry for keep-shapes.
- `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` - added the SQLite/Postgres seed assertions for keep-shapes.
- `.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-04-SUMMARY.md` - recorded execution evidence for this plan.

## Decisions Made

- Kept the raw flag separate from the downstream gate so the service can read the operator snapshot fail-safe OFF, then collapse to a single `keepShapes` value that honors hidden-block suppression everywhere else.
- Used `ManabaseAnalysisService.KeepShapesFlagKey` in the packet-cache registry so the key stays centralized rather than duplicating the literal.
- Updated only the two guard tests that enumerate seeded keys explicitly; no prompt-cache membership guard test required changes because no exact registry-set assertion exists today.

## Verified No-Cache Finding

- `grep -nEi 'manabase|mulligan' DeckFlow.Web/Services/DeckAnalysisPacketService.cs` returned only unrelated prose/comments at lines 170 and 246; there is no manabase prompt-cache path there.
- `DeckFlow.Web/Controllers/ManabaseController.cs:137` `Download(...)` rebuilds the paste artifact inline via `ManabaseReportTextBuilder.Build(...)` from the fresh `RunAnalysisAsync(...)` result, including live `ShowMulliganEval`, `ShowPlanPresence`, and interaction/tap inputs.
- Conclusion: adding keep-shapes to `PromptMutatingAnalysisFlags` is precautionary insurance today, not load-bearing for current manabase downloads.

## Build + Test Results

- Task 1 verify: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -c Debug` -> `Build succeeded.`
- Task 2 verify: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -c Debug` -> `Build succeeded.`
- Task 3 verify: `grep -n "keep-shapes\\|Precautionary" DeckFlow.Web/Services/DeckAnalysisPacketService.cs | head; "/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -c Debug` -> registry entry found, `Build succeeded.`
- Final clean/build: `dotnet clean DeckFlow.sln -c Debug` -> succeeded; `dotnet build DeckFlow.sln -c Debug` -> succeeded with 1 pre-existing warning in `DeckFlow.Web.Tests/MetaGapServiceTests.cs(302,109)` and 0 errors.
- Guard tests: `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -c Debug --filter "FullyQualifiedName~FeatureFlagCatalogTests|FullyQualifiedName~FeatureFlagStoreSeedTests"` -> `Passed! Failed: 0, Passed: 61, Skipped: 0, Total: 61`.

## EOL Check

- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs`: worktree `\r` count 0, `HEAD` `\r` count 0.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs`: worktree `\r` count 0, `HEAD` `\r` count 0.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs`: worktree `\r` count 0, `HEAD` `\r` count 0.
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs`: worktree `\r` count 0, `HEAD` `\r` count 0.
- `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs`: worktree `\r` count 0, `HEAD` `\r` count 0.
- `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs`: worktree `\r` count 0, `HEAD` `\r` count 0.
- Seed-block diff proof: `FeatureFlagStore.cs` shows exactly one added row in the Postgres block and one added row in the SQLite block:
  - `('analysis.manabase.keep-shapes', FALSE),`
  - `('analysis.manabase.keep-shapes', 0),`

## Deviations from Plan

- The plan referenced `DeckFlow.Web.Tests/FeatureFlags/FeatureFlagCatalogTests.cs` and `DeckFlow.Web.Tests/FeatureFlags/FeatureFlagStoreSeedTests.cs`, but the real files live at `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` and `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs`. Execution followed the intended tests at their actual locations.
- Task 3’s preferred constant reference required adding `using DeckFlow.Web.Services.Manabase;` in `DeckAnalysisPacketService.cs` because the type lives in `DeckFlow.Web.Services.Manabase`, not the local namespace. No behavior change beyond enabling the constant-backed registry entry.

## Issues Encountered

- First Task 3 build failed with `CS0103` because `ManabaseAnalysisService` was not in scope inside `DeckAnalysisPacketService.cs`. Resolved by importing the manabase namespace, then reran the plan’s verify command successfully.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `analysis.manabase.keep-shapes` now exists end-to-end in the web layer, seeded OFF, with MED-2 gating in place and result-level `ShowKeepShapes` available for plan 05 prompt/view work.
- Packet-cache insurance is already registered if a future change routes manabase or merged artifacts through `PacketSessionCache`.

---
*Phase: mbgap-11-cedh-mulligan-keep*
*Completed: 2026-07-14*
