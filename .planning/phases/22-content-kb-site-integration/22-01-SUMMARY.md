---
phase: 22-content-kb-site-integration
plan: 01
subsystem: database
tags: [content-kb, sqlite, postgres, visibility, feature-flags]

requires: []
provides:
  - content_site_index is_visible column with fresh-DDL and additive migration coverage
  - visibility-preserving site-index upsert and visibility read/update APIs
  - content.kb.enabled default-OFF feature flag seed
  - SQLite visibility integration tests plus cross-dialect DDL assertion
affects: [22-content-kb-site-integration, content-kb-seed-loader, content-kb-public-browse, content-kb-admin-curation]

tech-stack:
  added: []
  patterns: [guarded additive migration, visibility-preserving upsert, per-fact SQLite store tests]

key-files:
  created:
    - DeckFlow.Core.Tests/Content/ContentSiteIndexStoreVisibilityTests.cs
    - .planning/phases/22-content-kb-site-integration/22-01-SUMMARY.md
  modified:
    - DeckFlow.Core/Knowledge/ContentArtifactSpec.cs
    - DeckFlow.Core/Content/IContentSiteIndexStore.cs
    - DeckFlow.Core/Content/ContentSiteIndexStore.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
    - DeckFlow.Core.Tests/RunDistillAsyncTests.cs

key-decisions:
  - "Kept UpsertRowAsync unchanged; added UpsertRowPreservingVisibilityAsync for deploy seed paths."
  - "MED-Postgres: no Postgres fixture exists in Core.Tests, so coverage is SQLite integration plus cross-dialect DDL unit assertion."

patterns-established:
  - "Guarded ALTER migration: GetTableColumnsAsync checks for is_visible before adding the column."
  - "Preserving upsert: is_visible is inserted as hidden for new rows and omitted from DO UPDATE SET."

requirements-completed: [KB-08, KB-09]

duration: 20min
completed: 2026-06-02
---

# Phase 22: Content KB Site Integration Plan 01 Summary

**Content KB site-index visibility contract with hidden-by-default rows, curation-preserving seed upserts, and default-off feature flag seeding**

## Performance

- **Duration:** 20 min
- **Started:** 2026-06-02T09:26:00-06:00
- **Completed:** 2026-06-02T09:46:00-06:00
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments

- Added `ContentSiteIndexRow.IsVisible` and `content_site_index.is_visible` fresh-schema plus guarded migration DDL for SQLite and Postgres.
- Added the expanded `IContentSiteIndexStore` contract, production implementation, and working fake implementation for published/all/by-id reads and visibility updates.
- Seeded `content.kb.enabled` OFF for both dialects and added focused visibility tests covering preserving upsert, filtering, setters, migration, and DDL.

## Task Commits

1. **Task 1: Add is_visible to row record + schema** - `679b0df` (feat)
2. **Task 2: Visibility-preserving upsert + queries + fake update** - `b336b34` (feat)
3. **Task 3: Flag seed + visibility fixture + summary** - included in the commit containing this summary (feat)

## Files Created/Modified

- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` - Added `IsVisible` to the site-index row record.
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` - Added six visibility and read APIs.
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` - Added additive migration, preserving upsert, visibility reads, and setters.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` - Seeded `content.kb.enabled` default OFF in both dialect seed SQL blocks.
- `DeckFlow.Core.Tests/RunDistillAsyncTests.cs` - Updated `FakeContentSiteIndexStore` to implement the expanded interface.
- `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreVisibilityTests.cs` - Added SQLite visibility integration tests and cross-dialect DDL assertion.

## Decisions Made

Followed the plan as specified. The only coverage disposition is MED-Postgres: DeckFlow.Core.Tests has no Postgres fixture, so this plan ships SQLite integration coverage plus a unit assertion that the Postgres DDL contains `is_visible BOOLEAN NOT NULL DEFAULT FALSE`.

## Deviations from Plan

None - plan executed within the hard file fence.

## Issues Encountered

The plan's absolute WSL project paths were rejected by Windows `dotnet.exe` as MSBuild switches. Verification used the required Windows dotnet executable with equivalent Windows project paths.

VSTest did not flake for the targeted fixture; no manual-test fallback was needed.

## Verification

- Core build after Task 1: `Build succeeded`
- Whole-solution build after Task 2: `Build succeeded`, no `CS0535`
- Task 2 preserving-upsert grep: `0` `is_visible` hits in `DO UPDATE SET`
- Task 3 targeted tests: `Passed: 6, Failed: 0, Skipped: 0`
- Final whole-solution build after Task 3: `Build succeeded`
- `content.kb.enabled` grep count: `2`

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plans 02-04 can rely on a hidden-by-default site index, preserving seed upserts, published/all/by-id reads, per-entry/per-source visibility setters, and the default-off `content.kb.enabled` feature flag.

---
*Phase: 22-content-kb-site-integration*
*Completed: 2026-06-02*
