---
phase: 80-win-condition-combo-map
plan: 02
subsystem: analysis
tags: [web, deck-analysis, feature-flag, prompt-variants, cache-bypass, win-condition, combos]

# Dependency graph
requires: ["80-01-win-condition-combo-map"]
provides:
  - "analysis.wincon-map feature flag, seeded OFF in both SQLite and Postgres, with a catalog description"
  - "DeckAnalysisPacketService.WinConMapFlag gate + widened single-combo-fetch gate + BuildWinConMapText"
  - "ShouldBypassPacketCache() shared predicate (command-zone-awareness OR wincon-map) replacing the single-flag Phase-73 bypass"
  - "winConMapText threaded through IAnalysisPromptVariant.Build / AnalysisPromptVariantRegistry / all three concrete variants"
affects: ["80-03-win-condition-combo-map"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Flag-gated block-builder pattern (mirrors Phase 77/79): explicit-snapshot flag read -> Core aggregator Compute() -> internal static Build*Text() -> threaded as a new trailing optional string param through BuildAnalysisPrompt/registry/each variant"
    - "Single-fetch gate widening: existing (scoreEnabled || requiresComboLookup) combo-lookup gate widened to (scoreEnabled || winConMapEnabled || requiresComboLookup) so a new flag reuses an already-fetched upstream result instead of adding a second call"
    - "Generalized cache-bypass predicate: single-flag IsCommandZoneAwarenessEnabled() bypass promoted to ShouldBypassPacketCache() (OR of both flags), used identically at the read-side (TryComputeCacheKeyAsync) and write-side (BuildAsync) guards"
    - "ADR-0001 decoupled prompt variants: each of ChatGPT/Claude/Gemini gets its own independent hand-edited guard inserting the pre-built block text; no shared helper extracted"

key-files:
  created:
    - DeckFlow.Web.Tests/WinConMapPromptParityTests.cs
    - DeckFlow.Web.Tests/WinConMapCacheBypassTests.cs
  modified:
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
    - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs
    - DeckFlow.Web.Tests/Tools/ToolFlagSeedConsistencyTests.cs
    - DeckFlow.Web.Tests/AiPlatformExtensionTests.cs

key-decisions:
  - "Generalized ShouldBypassPacketCache() calls IsCommandZoneAwarenessEnabled() and the WinConMapFlag snapshot fresh at BOTH the read-side (TryComputeCacheKeyAsync) and write-side (BuildAsync) guards, per the plan's explicit code snippet -- the write-side no longer routes through the once-per-request commandZoneAwareness latch local, which is a narrower re-read window than the Codex-73-HIGH-1 fix originally closed, but matches the plan's literal, reviewed-and-approved directive"
  - "comboDataAvailable for the win-con map is comboResult is not null (matches the existing comboDetectionAvailable pattern the score block already uses one line above), distinguishing Commander Spellbook API failure from an empty combo result"
  - "BuildWinConMapText lives beside BuildScoreBlockText/BuildInteractionAuditText as another internal static method on DeckAnalysisPacketService rather than a new class, mirroring the established Phase 77/79 pattern exactly"
  - "WinConMapCacheBypassTests is declared as a sibling partial of DeckAnalysisPacketServiceTests (not a new standalone class) so it can reuse the existing private fakes (FakeMoxfieldDeckImporter, CreateCompanionFixtureEntries, CreateCollectionResponse, etc.) and inject a single shared PacketSessionCache instance across the ON/OFF calls -- test method names carry the WinConMapCacheBypass prefix so the plan's `--filter FullyQualifiedName~WinConMapCacheBypass` verification command still matches"

requirements-completed: [WINCON-01, WINCON-02, WINCON-03, WINCON-04]

# Metrics
duration: ~55min
completed: 2026-07-02
---

# Phase 80 Plan 02: Win-Condition Map Web Integration Summary

**Wires the Phase 80-01 WinConMapAggregator into the /deck-analysis paste artifact behind a new `analysis.wincon-map` flag (seeded OFF), reusing the single already-fetched Commander Spellbook result (gate widened, never re-fetched) and generalizing the Phase-73 command-zone cache bypass into a shared predicate so a wincon-ON packet can never be replayed after the flag flips OFF.**

## Performance

- **Duration:** ~55 min
- **Completed:** 2026-07-02T18:42:43Z
- **Tasks:** 4
- **Files modified:** 10 (2 created, 8 modified)

## Accomplishments

- `analysis.wincon-map` seeded OFF (`FALSE`/`0`) immediately after `analysis.interaction-audit` in both the Postgres and SQLite seed blocks, with a catalog description explaining the block and the off-by-default byte-identity contract. A new `AnalysisWinConMapFlag_SeededOff_InBothDialects` test (sibling to the Phase-79 interaction-audit one, reusing `GetSeedKeysWithPrefix`) proves it.
- `DeckAnalysisPacketService.WinConMapFlag` gates on the explicit `_flagCache.Snapshot()` value (never `IsEnabled()`), and the single existing combo-lookup gate widened from `(scoreEnabled || requiresComboLookup)` to `(scoreEnabled || winConMapEnabled || requiresComboLookup)` -- `FindCombosAsync` is still invoked exactly once; the reused `comboResult` is mapped onto `WinConComboInput`/`WinConNearComboInput`/`WinConClosingCardInput` and passed to `WinConMapAggregator.Compute`, with `comboDataAvailable = comboResult is not null` distinguishing Commander Spellbook failure from an empty result.
- `BuildWinConMapText` renders a hedged, ASCII-only block: a header framing every line as a candidate the AI must confirm (never "the deck wins"), a "Combo data unavailable" disclosure distinct from "no combos detected", ranked combos with optional per-combo band phrasing, near-combos always labeled "one card away (not currently a win line)", an overall band line (omitted when Unknown), and closing cards that render even when combo data is unavailable so a combo-less/lookup-failed deck still gets a read.
- `ShouldBypassPacketCache()` generalizes the Phase-73 `IsCommandZoneAwarenessEnabled()`-only bypass into `IsCommandZoneAwarenessEnabled() || (winConMapEnabled snapshot)`, used at both the `TryComputeCacheKeyAsync` read-side guard and the `BuildAsync` write-side `_packetCache.Set` guard, so a wincon-ON packet is never cached.
- `winConMapText` threaded as a new trailing optional parameter through `BuildAnalysisPrompt` -> `AnalysisPromptVariantRegistry.Build` -> each of `ChatGptAnalysisPromptVariant`/`ClaudeAnalysisPromptVariant`/`GeminiAnalysisPromptVariant` (Gemini included), each with its own independent `if (!string.IsNullOrWhiteSpace(winConMapText))` guard -- no shared helper (ADR-0001).
- `WinConMapPromptParityTests`: a 3-platform `[Theory]` proving the block (and the "one card away (not currently a win line)" label) renders in all three variants, a null-path byte-identity `[Theory]` proving the flag-OFF path is unchanged, and an absence `[Theory]` proving the sentinel never leaks into the null-path output.
- `WinConMapCacheBypassTests`: proves a wincon-ON `BuildAsync` call renders the block and its cache-key computation returns null (so the controller-level replay guard never even attempts a lookup), that flipping the flag OFF does not surface a stale cached ON packet under the now-computed key, and that command-zone-awareness still bypasses the cache after the predicate generalization.

## Task Commits

Each task was committed atomically:

1. **Task 1: Seed analysis.wincon-map OFF in both dialects + catalog description + seed-consistency assertion** - `dc75a4b9` (feat)
2. **Task 2 + Task 3 wiring: flag gate, widened combo gate, BuildWinConMapText, cache-bypass generalization, thread through all three prompt variants** - `1a67a977` (feat)
3. **Task 3 test: 3-platform parity + null-path byte identity** - `37bae944` (test)
4. **Task 4: cache-bypass regression** - `3652a782` (test)

**Plan metadata:** (this commit) - SUMMARY.md + STATE.md

_Note on commit granularity: Tasks 2 and 3's production code (the `winConMapText` signature threading through `IAnalysisPromptVariant`/`AnalysisPromptVariantRegistry`/all three variants) had to land in a single commit because `BuildAnalysisPrompt`'s call to `_analysisPromptRegistry.Build(...)` would not compile against the widened argument list without the registry and variant signatures changing in the same build. Task 3's dedicated test file (`WinConMapPromptParityTests.cs`) was committed separately, matching the plan's task boundary for the test deliverable. See Deviations._

## Files Created/Modified

- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` - Added `('analysis.wincon-map', FALSE)` / `('analysis.wincon-map', 0)` seed rows in both dialects, immediately after `analysis.interaction-audit`.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` - Added the `analysis.wincon-map` description.
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` - `WinConMapFlag` const, `ShouldBypassPacketCache()` predicate, widened combo gate, `winConMap`/`winConMapText` computation region (mirrors the score/interaction-audit regions), `BuildWinConMapText` + its private formatting helpers, `winConMapText` threaded through `BuildAnalysisPrompt` and the `BuildAsync` call site.
- `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` - Added the trailing `string? winConMapText = null` parameter + XML doc.
- `DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs` - Forwards `winConMapText` to the dispatched variant.
- `DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs` / `ClaudeAnalysisPromptVariant.cs` / `GeminiAnalysisPromptVariant.cs` - Each independently accepts and guards on `winConMapText`, inserted immediately after that variant's own `interactionAuditText` guard.
- `DeckFlow.Web.Tests/Tools/ToolFlagSeedConsistencyTests.cs` - Added `AnalysisWinConMapFlag_SeededOff_InBothDialects`.
- `DeckFlow.Web.Tests/AiPlatformExtensionTests.cs` - Updated the SC5 4th-platform `StubTestAnalysisVariant` to match the widened `IAnalysisPromptVariant.Build` signature (Rule 3 blocking-issue fix; the interface change would not otherwise compile).
- `DeckFlow.Web.Tests/WinConMapPromptParityTests.cs` (new) - 3-platform parity + null-path byte-identity + absence tests.
- `DeckFlow.Web.Tests/WinConMapCacheBypassTests.cs` (new) - Cache-bypass regression, declared as a `DeckAnalysisPacketServiceTests` partial to reuse existing fakes.

## Decisions Made

- `ShouldBypassPacketCache()` is called fresh at both the read-side and write-side guards (not routed through the once-per-request `commandZoneAwareness` local), exactly as the plan's explicit code snippet specified. This is narrower than the original Codex-73-HIGH-1 latch (which existed specifically to prevent the enrichment read and the cache-write bypass from observing different flag values within one request), but implemented literally per the reviewed, Codex-approved plan text; flagged here for visibility rather than silently deviating.
- `comboDataAvailable = comboResult is not null` mirrors the existing `comboDetectionAvailable` pattern the score block already uses one line above in the same method, keeping the "API failure vs. empty result" distinction consistent across both flag-gated blocks.
- Declared `WinConMapCacheBypassTests` as a `DeckAnalysisPacketServiceTests` partial (rather than a fully standalone class) so it could reuse `CreateCompanionFixtureEntries`, `FakeMoxfieldDeckImporter`, and the Scryfall response builders without duplicating ~80 lines of fixture code or widening any existing method's visibility; renamed its two `[Fact]` methods to carry the `WinConMapCacheBypass` prefix so the plan's `--filter "FullyQualifiedName~WinConMapCacheBypass"` verification command still resolves both tests (xUnit's `FullyQualifiedName` includes the method name, not just the file/class name).

## Deviations from Plan

**1. [Rule 3 - blocking issue] SC5 4th-platform stub variant did not implement the widened interface**
- **Found during:** Task 2/3 build verification
- **Issue:** `AiPlatformExtensionTests.StubTestAnalysisVariant` implements `IAnalysisPromptVariant` with the pre-Phase-80 12-argument `Build` signature; widening the interface to 13 arguments (trailing `winConMapText`) broke the build with `CS0535` (interface member not implemented).
- **Fix:** Added the matching `string? winConMapText = null` parameter to the stub's `Build` method.
- **Files modified:** `DeckFlow.Web.Tests/AiPlatformExtensionTests.cs`
- **Commit:** `1a67a977`

**2. [Task-commit granularity, non-substantive] Tasks 2 and 3's production code landed in one commit**
- **Found during:** Task 2
- **Issue:** `BuildAnalysisPrompt`'s call into `_analysisPromptRegistry.Build(...)` requires the registry and all three concrete variants to accept the new trailing parameter in the same build -- there is no way to land Task 2's `DeckAnalysisPacketService.cs` changes alone with a compiling `dotnet build DeckFlow.Web`.
- **Fix:** Committed the interface, registry, and all three variant signature/insertion changes together with the `DeckAnalysisPacketService.cs` changes in commit `1a67a977`; Task 3's dedicated test file (`WinConMapPromptParityTests.cs`) was still committed separately in `37bae944`, preserving the plan's test-deliverable boundary.
- **Files modified:** `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs`, `AnalysisPromptVariantRegistry.cs`, `ChatGptAnalysisPromptVariant.cs`, `ClaudeAnalysisPromptVariant.cs`, `GeminiAnalysisPromptVariant.cs`
- **Committed in:** `1a67a977`

---

**Total deviations:** 2 (1 Rule-3 blocking-issue fix, 1 non-substantive commit-sequencing note)
**Impact on plan:** None on correctness or scope. All must_haves truths verified.

## Issues Encountered
None beyond the deviations above.

## User Setup Required
None - no external service configuration required, no new dependencies. The flag defaults OFF; an operator must flip `analysis.wincon-map` on via `/Admin` (or a seed-row update) to activate the feature in any environment.

## Self-Check: PASSED

- FOUND: DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
- FOUND: DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
- FOUND: DeckFlow.Web/Services/DeckAnalysisPacketService.cs
- FOUND: DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs
- FOUND: DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs
- FOUND: DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs
- FOUND: DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs
- FOUND: DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs
- FOUND: DeckFlow.Web.Tests/WinConMapPromptParityTests.cs
- FOUND: DeckFlow.Web.Tests/WinConMapCacheBypassTests.cs
- FOUND: DeckFlow.Web.Tests/Tools/ToolFlagSeedConsistencyTests.cs
- FOUND commit dc75a4b9
- FOUND commit 1a67a977
- FOUND commit 37bae944
- FOUND commit 3652a782
- `dotnet.exe build DeckFlow.Web` -- 0 Warning(s), 0 Error(s)
- `dotnet.exe build DeckFlow.Web.Tests` -- 0 Warning(s), 0 Error(s)
- `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~ToolFlagSeedConsistency"` -- Passed: 4, Failed: 0
- `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~WinConMapPromptParity"` -- Passed: 9, Failed: 0
- `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~WinConMapCacheBypass"` -- Passed: 2, Failed: 0
- `dotnet.exe test DeckFlow.Web.Tests` (full suite) -- Passed: 1110, Skipped: 12 (pre-existing Postgres integration tests), Failed: 0
- `scripts/format-check-changed.sh ci` -- clean, no violations reported
- grep confirms exactly one `FindCombosAsync` call site in `DeckAnalysisPacketService.cs` (no second combo fetch added)
- grep confirms `BuildWinConMapText` uses the `_flagCache.Snapshot()` pattern (not `IsEnabled`), contains `one card away (not currently a win line)` and `verify`, and does not contain the phrase `the deck wins`
- grep confirms `ShouldBypassPacketCache` appears at both the `TryComputeCacheKeyAsync` read-side guard and the `BuildAsync` write-side guard

## Next Phase Readiness
- Plan 80-03 can promote the already-computed `winConMap` local (currently discarded after `BuildWinConMapText`) into `DeckAnalysisPacketResult` and the Step-3 UI readout, following the exact `computedScore`/`interactionAudit` precedent.
- No blockers. The flag, gate-widening, cache-bypass generalization, and 3-variant threading are all in place and fully tested; flipping `analysis.wincon-map` ON in any environment activates the paste-artifact block with no further wiring required.

---
*Phase: 80-win-condition-combo-map*
*Completed: 2026-07-02*
