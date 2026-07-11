---
phase: 89-content-hash-foundation
plan: 04
subsystem: content-kb
tags: [content-hash, seed-json, sha256, sync, dapper]

requires:
  - phase: 89-content-hash-foundation
    provides: "89-01: ContentSiteIndexRow.BodySha256 property + ComputeBodySha256 helper"
  - phase: 89-content-hash-foundation
    provides: "89-02: body_sha256 column round-trips through all store SELECTs/upserts"
provides:
  - "bodySha256 round-trips through index-seed.json via the single shared export factory ContentIndexExportRow.From(row) — both the CLI export path and DirectPush inherit it with zero code changes"
  - "ContentKbSeedLoader.BuildRow maps a loaded seed entry's bodySha256 into ContentSiteIndexRow.BodySha256, defaulting to null for legacy seed entries that predate this phase"
  - "Golden fixture (index-seed.golden.json) and writer tests (ContentIndexSeedWriteTests) updated and green with bodySha256 present"
affects: [90-directpush-correctness-seed-sync, 91-reconcile-seed-lifecycle]

tech-stack:
  added: []
  patterns:
    - "bodySha256 appended after the last existing field (CardCategoryTags) in ContentIndexExportRow to preserve byte-stable JSON key order for pre-existing keys — new nullable-optional fields join at the end, not interleaved"
    - "Single shared seed-row factory (ContentIndexExportRow.From) extended once so both CLI export and DirectPush inherit a new field with no edits to either consumer — same 'one signature, one home' payoff as AreContentEqual"

key-files:
  created: []
  modified:
    - "DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs"
    - "DeckFlow.Core/Orchestration/ContentIndexExportRow.cs"
    - "DeckFlow.Core.Tests/Orchestration/ContentIndexExportJsonGoldenTests.cs"
    - "DeckFlow.Core.Tests/Orchestration/Fixtures/index-seed.golden.json"
    - "DeckFlow.Web.Tests/ContentKbSeedLoaderTests.cs"

key-decisions:
  - "D-09 honored: only the shared export factory (From()) and the seed load mapper (BuildRow) were touched — ContentKbCommandRunners.cs and PublishCoordinator.cs were confirmed NOT in the diff since they route through From() and inherit the field automatically"
  - "BodySha256 declared non-required/nullable on both ContentIndexExportRow and ContentKbSeedEntry, matching the PublishedUtc nullable-optional convention, so legacy seed entries/rows without a hash still round-trip cleanly as null"

patterns-established:
  - "New optional seed-JSON fields append after the last existing property (not interleaved) to keep byte-stable key order for pre-existing keys in golden-fixture-pinned exports"

requirements-completed: [SYNC-01]

duration: ~20min
completed: 2026-07-07
---

# Phase 89 Plan 04: Content-Hash Foundation Summary

`body_sha256` now round-trips through `index-seed.json` (camelCase `bodySha256`) via the single shared export factory `ContentIndexExportRow.From()` and the seed-load mapper `ContentKbSeedLoader.BuildRow` — a prod reseed/redeploy reconstructs the hash instead of silently dropping it, and both the CLI export path and DirectPush inherit the field with zero code changes to either consumer.

## Performance

- **Duration:** ~20 min
- **Tasks:** 2 completed
- **Files modified:** 5 (2 source, 3 test/fixture)

## Accomplishments

- `ContentKbSeedEntry` (seed load side) gained a nullable `BodySha256` mapped into `ContentSiteIndexRow.BodySha256` by `BuildRow`; camelCase JSON policy already in place handles `bodySha256` with no `[JsonPropertyName]` needed.
- `ContentIndexExportRow` (the single shared seed-row factory) gained a nullable `BodySha256` appended after `CardCategoryTags` (preserving byte-stable key order for all pre-existing fields), mapped in `From(ContentSiteIndexRow row)`.
- Golden fixture `index-seed.golden.json` and `CreateRows()` in `ContentIndexExportJsonGoldenTests.cs` updated to include `bodySha256` (two entries with a value, one with `null`), confirming the serializer's existing null-rendering convention applies unchanged to the new field.
- `ContentKbCommandRunners.cs` and `PublishCoordinator.cs` were confirmed absent from the diff — both build `ContentIndexExportRow` instances exclusively via `From()`, so they inherit `bodySha256` automatically (D-09 / SYNC-02 "one signature, one home" invariant).
- Added round-trip test coverage in `ContentKbSeedLoaderTests.cs` for both the present-hash and legacy-absent-hash cases (TDD `<behavior>` block for Task 1).

## Task Commits

Each task was committed atomically:

1. **Task 1: Add bodySha256 to seed load record + mapper** - `09449541` (feat)
2. **Task 2: Add bodySha256 to the shared ContentIndexExportRow factory and update golden** - `8c9444bf` (feat)
3. **Test coverage for Task 1's behavior (present + legacy-absent bodySha256)** - `be642b16` (test)

## Files Created/Modified

- `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` - `ContentKbSeedEntry.BodySha256` (nullable) + `BuildRow` mapping
- `DeckFlow.Core/Orchestration/ContentIndexExportRow.cs` - `BodySha256` record property (appended after `CardCategoryTags`) + `From()` mapping
- `DeckFlow.Core.Tests/Orchestration/ContentIndexExportJsonGoldenTests.cs` - `CreateRows()` sets `BodySha256` (value + null cases)
- `DeckFlow.Core.Tests/Orchestration/Fixtures/index-seed.golden.json` - `bodySha256` added to all 3 entries (value/null/value)
- `DeckFlow.Web.Tests/ContentKbSeedLoaderTests.cs` - 2 new tests: bodySha256 hydrates when present; stays null on legacy entries that omit it

## Decisions Made

- Field placement: appended `BodySha256` after the last existing field (`CardCategoryTags`) on both the record and the golden JSON entries, per the plan's explicit instruction to preserve pre-existing key order for byte-stable diffs.
- Added targeted test coverage beyond the plan's minimum build-only verification for Task 1, since `tdd="true"` and the plan's `<behavior>` block described two explicit scenarios (present hash / legacy-absent hash) that weren't otherwise asserted anywhere in the suite.

## Deviations from Plan

None - plan executed exactly as written. The one addition (test coverage for Task 1) is within the plan's own `tdd="true"`/`<behavior>` scope, not a deviation from it.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `body_sha256` is now fully wired end to end: DDL (89-02) → publish-time compute (89-03) → seed export/load round-trip (this plan). Ready for 89-05 (render guard) and 89-06 (D-08 backfill).
- No blockers. `DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.Studio` all build clean (0 warnings). `DeckFlow.Core.Tests` 1131/1131, `DeckFlow.Web.Tests` 1223/1235 (12 PG-skip, 0 failed), `DeckFlow.Studio.Tests` 293/293. Changed-lines format gate clean on all 5 touched files.

## Self-Check: PASSED

- FOUND: `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs`
- FOUND: `DeckFlow.Core/Orchestration/ContentIndexExportRow.cs`
- FOUND: `DeckFlow.Core.Tests/Orchestration/ContentIndexExportJsonGoldenTests.cs`
- FOUND: `DeckFlow.Core.Tests/Orchestration/Fixtures/index-seed.golden.json`
- FOUND: `DeckFlow.Web.Tests/ContentKbSeedLoaderTests.cs`
- FOUND: commit `09449541`
- FOUND: commit `8c9444bf`
- FOUND: commit `be642b16`

---
*Phase: 89-content-hash-foundation*
*Completed: 2026-07-07*
