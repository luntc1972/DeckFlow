---
phase: 32-expert-context-selection
plan: 02
subsystem: content-kb
tags: [packet-zip, cache-key, selection-persistence, json-round-trip]
requires:
  - "ExpertSelection / GetMergedClipsAsync / ClipOrigin (32-01)"
provides:
  - "DeckAnalysisRequest.PinnedVideoIds / FollowedCreators / ExpertSelectionJson"
  - "33-expert-selection.json zip artifact (allowlist + writer + reader)"
  - "ExpertSelectionState (top-level record) + ExpertSelectionJsonOptions (camelCase)"
  - "selection folded into DeckAnalysis packet cache key (pins ordinal, follows ignore-case)"
  - "ContentKbRelevanceService.ResolvePinTitlesAsync + DeckAnalysisPacketResult.ResolvedPinTitles"
affects:
  - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
  - DeckFlow.Web/Controllers/DeckController.cs
tech-stack:
  added: []
  patterns:
    - "dedicated camelCase JsonSerializerOptions for zip selection round-trip"
    - "selection folded into value-record cache key with per-field case semantics"
    - "replay-first guard restores selection + short-circuits re-merge"
key-files:
  created:
    - DeckFlow.Web.Tests/DeckAnalysisRequestTests.cs
  modified:
    - DeckFlow.Web/Models/DeckAnalysisRequest.cs
    - DeckFlow.Web/Services/PacketArtifactStore.cs
    - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
    - DeckFlow.Web/Services/PacketSessionCache.cs
    - DeckFlow.Web/Services/ContentKbRelevanceService.cs
    - DeckFlow.Web/Controllers/DeckController.cs
    - DeckFlow.Web.Tests/PacketArtifactStoreTests.cs
    - DeckFlow.Web.Tests/ContentKbRelevanceServiceTests.cs
    - DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs
    - DeckFlow.Web.Tests/AdminContentKbControllerTests.cs
    - DeckFlow.Web.Tests/DeckAnalysisPacketServiceExpertContextTests.cs
key-decisions:
  - "ResolvePinTitlesAsync added to two more IContentKbRelevanceService test fakes the plan's implementer list omitted (compile-blocker, same class of fix as 32-01)"
  - "HIGH-B: selection folded INTO the cache key (option a) so a changed selection forces re-merge"
  - "HIGH-D: pins normalized case-sensitively (ordinal, no lowercase), follows case-insensitively (lowercased), matching the merge/title-resolution path"
requirements-completed: [SEL-04, SEL-02]
duration: ~30 min
completed: 2026-06-07
---

# Phase 32 Plan 02: Selection Persistence + Cache Key Summary

Round-tripped the expert selection through the request DTO, the packet zip (`33-expert-selection.json` with a dedicated camelCase `JsonSerializerOptions`), and the packet service. Swapped the non-replay clip source to `GetMergedClipsAsync`, folded the normalized selection into the DeckAnalysis packet cache key (pins case-sensitive/ordinal, follows case-insensitive — HIGH-B + HIGH-D), threaded `selectionJson` through both `DeckAnalysisDownload` BuildZip call sites, and added `ResolvePinTitlesAsync` + `DeckAnalysisPacketResult.ResolvedPinTitles` for Plan 03's replay chips. Replay-first guard preserved.

- **Tasks:** 3
- **Files:** 11 (10 planned + 1 authorized; 2 of the 10 were extra test fakes also authorized)
- **Commits:** `7d2c895`, `0998334`, `4a6f027`
- **Executor:** Codex (gpt-5.4, medium) — Claude review

## Build / Test Results

- `dotnet build DeckFlow.Web` — succeeded, 0 errors
- `dotnet build DeckFlow.Web.Tests` — succeeded, 0 errors
- `dotnet test --filter "PacketArtifactStoreTests|DeckAnalysisRequestTests|ResolvePinTitlesAsync|CacheKey_SameDeckDifferentPins"` — 13 passed / 0 failed
- `dotnet test --filter "ContentKbRelevanceServiceTests|DeckAnalysisPacketServiceTests"` (regression) — 53 passed / 0 failed

## Deviations from Plan

**[Rule 2 - Missing critical, authorized] Two more interface fakes** — Found during: Task 2. Adding `ResolvePinTitlesAsync` to `IContentKbRelevanceService` broke `AdminContentKbControllerTests.cs` and `DeckAnalysisPacketServiceExpertContextTests.cs` (CS0535). Fix: minimal stub members. Commit `0998334`.

**[Rule 1 - Transient, environment] Static-web-assets file lock** — Found during: Task 2 verify. Parallel Web + Web.Tests builds collided on `staticwebassets.build.endpoints.json`; reran Web build sequentially, clean. No code impact.

**Total deviations:** 2 (1 authorized scope expansion + 1 transient build-env). **Impact:** none on behavior.

## Reviewer Notes (Claude)

- 33-expert-selection.json present 5× (allowlist + BuildZip section + LoadFromZip + writer); same-commit rule honored.
- `ExpertSelectionState` is a top-level record (line 745), referenced by bare name; `ExpertSelectionJsonOptions` (camelCase + case-insensitive) used at every serialize/deserialize site (LoadFromZip, BuildAsync serialize, replay-guard restore) — no default-options serde leak.
- Both `DeckAnalysisDownload` BuildZip call sites thread `selectionJson:` (cache-hit + cache-miss).
- HIGH-D verified in BuildDeckAnalysisCacheInputs: pins `.Trim()` + `Distinct/OrderBy(Ordinal)` (NO lowercase); follows `.Trim().ToLowerInvariant()` + ordinal. CacheKey_SameDeckDifferentPins test covers all branches (different pins fork, case-only pins fork, reordered/dup pins same, case-only follows same).
- 2× `catch (JsonException)` graceful-degrade (LoadFromZip + replay guard).

## Issues Encountered

None unresolved.

## Next Phase Readiness

Ready for 32-03 (browse + analysis selection UI). Server contracts complete: request fields bind repeated hidden inputs, `ResolvedPinTitles` available for replay chips, selection persists + restores via zip.

## Self-Check: PASSED
