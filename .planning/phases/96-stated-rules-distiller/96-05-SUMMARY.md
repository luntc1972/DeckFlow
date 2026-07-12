---
phase: 96-stated-rules-distiller
plan: 05
subsystem: database
tags: [dotnet, sqlite, postgres, dapper, content-kb, stated-rules]
requires:
  - phase: 96-stated-rules-distiller
    provides: StatedRuleCandidate contract and phase-96 stated-rule vocabulary context
provides:
  - content_stated_rules persistence on SQLite and Postgres with clean re-distill clearing
  - additive content_type and stated_rules artifact frontmatter contract
  - round-trip and byte-stability regression coverage for stated-rule persistence and rendering
affects: [phase-96, phase-97, content-kb, stated-rules]
tech-stack:
  added: []
  patterns: [default interface extension methods for optional store capabilities, dual-dialect child-table parity, explicit JsonPropertyName projection for locked artifact contracts]
key-files:
  created: []
  modified:
    [
      DeckFlow.Core/Content/IContentVideoStore.cs,
      DeckFlow.Core/Content/ContentVideoStore.cs,
      DeckFlow.Core/Knowledge/ContentArtifactSpec.cs,
      DeckFlow.Core/Knowledge/ContentArtifactWriter.cs,
      DeckFlow.Core.Tests/ContentVideoStoreDistillTests.cs,
      DeckFlow.Core.Tests/ContentArtifactWriterTests.cs,
      DeckFlow.Core.Tests/ContentArtifactSpecTests.cs,
      .planning/phases/96-stated-rules-distiller/96-05-SUMMARY.md
    ]
key-decisions:
  - "Kept InsertStatedRuleAsync as a default interface member with the locked NotSupportedException body so every existing fake inherited it unchanged and DeckFlow.sln stayed clean."
  - "Used a [SetsRequiredMembers] compatibility constructor on ContentArtifactMetadata so the new required ContentType property remained additive within the scope fence while preserving existing callers."
  - "Serialized stated_rules through an internal projection record with explicit JsonPropertyName attributes so clip_ts and video_date match the locked contract exactly."
patterns-established:
  - "Pattern 1: New per-video distill dimensions mirror content_clips/content_tags in both Postgres and SQLite DDL, insert SQL, and ClearDistillOutputAsync cleanup in the same edit."
  - "Pattern 2: Artifact frontmatter contract changes stay additive and are guarded by tests that normalize line endings before asserting byte stability of pre-existing lines."
requirements-completed: [CS-11a, CS-11b, CS-11c]
duration: 16min
completed: 2026-07-12
---

# Phase 96: Stated-Rules Distiller Summary

**Per-video stated-rule persistence plus additive content_type/stated_rules artifact frontmatter with locked snake_case serialization and clean re-distill semantics**

## Performance

- **Duration:** 16 min
- **Started:** 2026-07-12T16:50:00Z
- **Completed:** 2026-07-12T17:06:50Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments
- Added `content_stated_rules` to both `ContentVideoStore` dialect schemas, plus `InsertStatedRuleAsync`, `InsertStatedRuleSql`, `ix_content_stated_rules_video_id`, and the 4th `ClearDistillOutputSql` delete so re-distill replaces rows instead of accumulating them.
- Extended `ContentArtifactMetadata`, `ContentArtifactSpec`, and `ContentArtifactWriter.ToText` with additive `content_type:` and `stated_rules:` frontmatter lines while keeping the pre-existing frontmatter/body output byte-stable.
- Added SQLite round-trip and clear-on-redistill coverage, an env-gated Postgres round-trip test path, and writer/spec assertions for exact snake_case keys and frontmatter ordering.

## Task Commits

No git commits were created. The plan hard rule forbade git operations, and none were performed.

## Files Created/Modified
- `DeckFlow.Core/Content/IContentVideoStore.cs` - Added the locked default `InsertStatedRuleAsync` interface method so untouched fake stores keep compiling.
- `DeckFlow.Core/Content/ContentVideoStore.cs` - Added `content_stated_rules` DDL, insert SQL/implementation, and clear-on-redistill deletion for both dialects.
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` - Added `SerializeStatedRules`, `ContentType`, `StatedRules`, and updated the artifact format fixture.
- `DeckFlow.Core/Knowledge/ContentArtifactWriter.cs` - Added the two frontmatter lines in the locked position between `generated_utc` and the closing fence.
- `DeckFlow.Core.Tests/ContentVideoStoreDistillTests.cs` - Added SQLite/Postgres stated-rule round-trip coverage and clear/reinsert assertions.
- `DeckFlow.Core.Tests/ContentArtifactWriterTests.cs` - Added ordering, byte-stability, and snake_case artifact assertions.
- `DeckFlow.Core.Tests/ContentArtifactSpecTests.cs` - Added spec fixture and stated-rule serialization contract assertions.
- `.planning/phases/96-stated-rules-distiller/96-05-SUMMARY.md` - Execution summary for this plan.

## Decisions Made

None beyond the locked plan requirements; implementation followed the specified interface, schema, and serialization contracts directly.

## Deviations from Plan

None - plan executed as written.

## Issues Encountered

- Writer assertions initially failed on Windows newline expectations rather than content drift. The fix was test-only normalization to LF before comparing the preserved pre-existing bytes, which keeps the byte-stability gate focused on the serialized content contract.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 97 now has a queryable per-video stated-rule substrate in both DB dialects and a locked artifact `stated_rules:` contract to parse.
- The untouched `IContentVideoStore` fakes remained unchanged, and `dotnet.exe build DeckFlow.sln` stayed clean, so downstream work can extend orchestration without interface fallout.

## Verification

- `dotnet.exe build DeckFlow.Core/DeckFlow.Core.csproj`  
  PASS - Build succeeded with 0 warnings and 0 errors
- `dotnet.exe test DeckFlow.Core.Tests --filter FullyQualifiedName~"ContentVideoStoreDistillTests|ContentArtifactWriterTests|ContentArtifactSpecTests"`  
  PASS - Failed: 0, Passed: 24, Skipped: 1, Total: 25  
  Note: the skipped test was `ContentVideoStoreDistillTests.InsertStatedRuleAsync_RoundTrips_AllFields_OnPostgres`, skipped by the existing `DECKFLOW_POSTGRES_TESTS` env gate as intended.
- `dotnet.exe build DeckFlow.sln`  
  PASS - Build succeeded with 0 warnings and 0 errors

## Additional Notes

- Byte-stable gate held: the writer tests prove the pre-existing `source/title/url/video_id/tags/generated_utc` frontmatter lines and `## Summary/## Key Clips/## Tags` body remain unchanged after removing only the two newly inserted lines.
- All existing `IContentVideoStore` fakes were left untouched; the clean solution build validated that the new method remained source-compatible through the default interface implementation.
- No new package was added, and `stated_rules:` uses `JsonSerializer` output only.
- No git operations were performed.

---
*Phase: 96-stated-rules-distiller*
*Completed: 2026-07-12*
