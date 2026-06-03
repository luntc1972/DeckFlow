---
phase: 14-broader-codebase-name-vs-behavior-audit
plan: 02
subsystem: codebase-naming
tags: [rename, test-doubles, naming-audit, D-05, D-02, D-06, D-08]
dependency_graph:
  requires: [14-01]
  provides: [renamed-types-with-summaries]
  affects: [DeckFlow.Web/Services, DeckFlow.Web.Tests, DeckFlow.Core.Tests]
tech_stack:
  added: []
  patterns: [rename-and-propagate, git-mv-blame-preservation, D-05-Fake-Stub-Throwing-taxonomy]
key_files:
  created:
    - DeckFlow.Web/Services/ScryfallTaggerLookupService.cs
    - DeckFlow.Web.Tests/Services/ScryfallTaggerLookupServiceTests.cs
  modified:
    - DeckFlow.Web/Program.cs
    - DeckFlow.Web/Services/CategorySuggestionService.cs
    - DeckFlow.Web/Services/ScryfallTaggerParsers.cs
    - DeckFlow.Web.Tests/CategorySuggestionServiceTests.cs
    - DeckFlow.Web.Tests/Integration/ScryfallTaggerCookieReplayTests.cs
    - DeckFlow.Web.Tests/AdminFeedbackControllerTests.cs
    - DeckFlow.Web.Tests/DeckControllerTests.cs
    - DeckFlow.Web.Tests/CommanderControllerTests.cs
    - DeckFlow.Core.Tests/ArchidektDeckCacheSessionTests.cs
    - .planning/codebase/ARCHITECTURE.md
    - .planning/codebase/STRUCTURE.md
    - .planning/codebase/INTEGRATIONS.md
    - .planning/codebase/CONCERNS.md
    - .planning/codebase/TESTING.md
decisions:
  - "Row (d) collision detected: FakeDeckAnalysisPacketService already existed at L775 as a throw-guard placeholder (NotImplementedException). Added preliminary rename L775 FakeDeckAnalysisPacketService → StubDeckAnalysisPacketService before renaming CapturingDeckAnalysisPacketService → FakeDeckAnalysisPacketService. Mirrors the FakeMetaGapService/ConfigurableMetaGapService collision-resolution pattern from Plan 14-01 row (a)."
  - "11 rename commits total (vs 10 planned): 1 production + 9 test-double + 1 unplanned preliminary collision-resolution for FakeDeckAnalysisPacketService. The 9 original test-double targets are fully renamed; all collisions resolved cleanly."
  - "ScryfallTaggerLookupService class-level summary updated to enumerate all three responsibilities (Scryfall REST card resolution, Tagger GraphQL lookup, CSRF session/flag management) per D-02 loose rename trigger rationale."
metrics:
  duration: ~30 minutes
  completed: "2026-05-17"
  tasks_completed: 2
  files_changed: 14
---

# Phase 14 Plan 02: Renames Summary

**One-liner:** All production and test-double class renames from 14-AUDIT-REPORT.md executed; ScryfallTaggerService becomes ScryfallTaggerLookupService; 8 non-canonical test-double prefixes canonicalized to Fake/Stub/Throwing taxonomy.

## Tasks Completed

| Task | Description | Commits |
|------|-------------|---------|
| Task 1 | ScryfallTaggerService → ScryfallTaggerLookupService (production rename) | e9cee68 |
| Task 2 | 9 test-double renames to Fake/Stub/Throwing taxonomy (+ 1 unplanned preliminary) | f02ddfc, 52b2368, edd4ae9, 0f836e6, dac4f24, 709f0f1, 1760d05, 4121839, a296e39, db86397 |

## Rename Commits (all with `refactor(14-02):` prefix)

| Hash | Description |
|------|-------------|
| e9cee68 | rename ScryfallTaggerService → ScryfallTaggerLookupService with three-responsibility summary |
| f02ddfc | rename FakeMetaGapService → StubMetaGapService (D-05 Stub canned-response — collision-resolution preliminary) |
| 52b2368 | rename NullTempDataProvider → StubTempDataProvider (D-05 Stub canonicalization) |
| edd4ae9 | rename ConfigurableMetaGapService → FakeMetaGapService (D-05 Fake configurable stateful) |
| 0f836e6 | rename FakeDeckAnalysisPacketService → StubDeckAnalysisPacketService (unplanned collision-resolution preliminary) |
| dac4f24 | rename CapturingDeckAnalysisPacketService → FakeDeckAnalysisPacketService (D-05 Fake state-capture) |
| 709f0f1 | rename SuccessfulCardLookupService → StubSuccessfulCardLookupService (D-05 Stub) |
| 1760d05 | rename SuccessfulSingleCardLookupService → StubSuccessfulSingleCardLookupService (D-05 Stub) |
| 4121839 | rename SuccessfulMechanicLookupService → StubSuccessfulMechanicLookupService (D-05 Stub) |
| a296e39 | rename DummyCommanderSearchService → StubCommanderSearchService (D-05 Stub) |
| db86397 | rename FailingRecentDecksImporter → ThrowingRecentDecksImporter (D-05 Throwing) |

## D-08 Mid-Plan Green Invariant Confirmation

Build was verified green after EVERY commit. Final Release build result:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:04.05
```

## D-10 Preservation List Confirmation

Representative D-10 grep results showing live values unchanged:

- `"ChatGPT"` AI key: present in `AiPlatform`, `DeckAnalysisRequest`, `MetaGapRequest`, `DeckComparisonRequest`
- `TargetAiPlatform` property: present in `DeckController.cs`, request models
- `"chatgpt"` zip filename fallback: present in `PacketArtifactStore.cs` (3 occurrences)
- All 22 guild theme CSS files: untouched (no CSS edits in Plan 14-02)
- No `Co-Authored-By` in any commit (grep across all 11 commits returns 0)

## Allowlist Verification

- `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs`: preserved (legitimate factory, not a test double)
- `DeckFlow.Web.Tests/DeckControllerTests.cs:914` `FakeCardLookupService`: preserved (pre-existing Stub behavior, semantically distinct from renamed Stub*CardLookupService neighbors)

## Final Stale-Old-Name Grep (0 hits expected)

```bash
grep -rEn '(NullTempDataProvider|ConfigurableMetaGapService|CapturingDeckAnalysisPacketService|SuccessfulCardLookupService|SuccessfulSingleCardLookupService|SuccessfulMechanicLookupService|DummyCommanderSearchService|FailingRecentDecksImporter|class ScryfallTaggerService|IScryfallTaggerService)' --include='*.cs' DeckFlow.Web/ DeckFlow.Core/ DeckFlow.CLI/ DeckFlow.Core.Tests/ DeckFlow.Web.Tests/
```

**Result: 0 hits** (all old names removed)

## { get; init; } Preservation Gate

Post-plan check across all 11 rename commits:

```bash
git log 9018af3c27ca9aa2793c9b27a9bf9a60f51bf789..HEAD -p -- '*.cs' | grep -E "^\\-.*\\{ get; init; \\}" | grep -v "^---"
```

**Result: 0 hits** (no init accessors stripped)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Missed Collision] FakeDeckAnalysisPacketService at L775 collided with planned row (d) rename**

- **Found during:** Task 2, row (d) collision pre-check
- **Issue:** `DeckFlow.Web.Tests/DeckControllerTests.cs` already had a `private sealed class FakeDeckAnalysisPacketService` at line 775 (a throw-guard placeholder that throws `NotImplementedException` — added in the Phase 13 sweep commit `d7510da`). The 14-AUDIT-REPORT.md row (d) planned to rename `CapturingDeckAnalysisPacketService` → `FakeDeckAnalysisPacketService`, which would create a duplicate class name in the same outer class.
- **Fix:** Applied the same collision-resolution pattern as the planned row (a) FakeMetaGapService/ConfigurableMetaGapService collision:
  - Preliminary rename: L775 `FakeDeckAnalysisPacketService` → `StubDeckAnalysisPacketService` (correct taxonomy: throws NotImplementedException as placeholder = Stub guard semantics; name frees `FakeDeckAnalysisPacketService` for the capturing class)
  - Row (d): `CapturingDeckAnalysisPacketService` → `FakeDeckAnalysisPacketService` (correct taxonomy: stateful capture = Fake)
- **Files modified:** `DeckFlow.Web.Tests/DeckControllerTests.cs`
- **Commits:** 0f836e6, dac4f24
- **Result:** 11 total rename commits (vs 10 planned). All original audit targets fully addressed.

## Known Stubs

None — no stub data introduced; this plan is renames-only.

## Threat Flags

None — renames do not introduce new trust boundaries or network endpoints. D-10 preservation confirmed.

## Next Step

Plan 14-03: Doc-comment backfill for all public types across 5 projects that are missing `<summary>`.
The test-double inner classes renamed in Plan 14-02 already carry `<summary>` comments added in lockstep.
Plan 14-03 is responsible for the outer test class file-level summaries (e.g., `DeckControllerTests`, etc.).

## Self-Check: PASSED

- ScryfallTaggerLookupService.cs exists: YES
- ScryfallTaggerService.cs deleted: YES (renamed via git mv)
- DI registration updated in Program.cs: YES (IScryfallTaggerLookupService, ScryfallTaggerLookupService)
- All 11 commits exist in git log: YES
- 0 Warning(s) 0 Error(s) in Release build: YES
- FakeCardLookupService at L914 preserved: YES
- TestServiceFactory.cs preserved: YES
- 0 Co-Authored-By trailers: YES
