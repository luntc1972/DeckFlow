---
phase: 55-publish-state-foundation
plan: 01
subsystem: database
tags: [sqlite, postgres, dapper, blazor, publish-tracking]
requires: []
provides:
  - pushed_to_prod_utc schema + migration on content_site_index
  - dedicated StampPushedToProdAsync batch writer
  - publish-boundary stamping in git Publish and DirectPush
  - test coverage for stamp persistence and publish-boundary contracts
affects: [phase-55-02, publish-state, studio-publish, admin-surface]
tech-stack:
  added: []
  patterns: [idempotent dual-dialect column migration, dedicated stamp writer separate from upserts]
key-files:
  created:
    - .planning/phases/55-publish-state-foundation/55-01-SUMMARY.md
    - DeckFlow.Core.Tests/Content/ContentSiteIndexStorePushedToProdTests.cs
    - DeckFlow.Core.Tests/Orchestration/ContentPublishStampTests.cs
  modified:
    - DeckFlow.Core/Content/ContentSiteIndexStore.cs
    - DeckFlow.Core/Content/IContentSiteIndexStore.cs
    - DeckFlow.Core/Knowledge/ContentArtifactSpec.cs
    - DeckFlow.Studio/Pages/Publish.razor
    - DeckFlow.Studio/Pages/DirectPush.razor
    - DeckFlow.Studio.Tests/PublishPageTests.cs
    - DeckFlow.Studio.Tests/DirectPushPageTests.cs
key-decisions:
  - "Use a distinct pushed_to_prod_utc column/property, not published_utc, because published_utc remains the video's YouTube publish date and the seed JSON contract stays byte-stable."
  - "Treat pushed_to_prod_utc as a LOCAL fact written only by StampPushedToProdAsync; all shared upserts deliberately omit the column so re-distill cannot clear it."
  - "Stamp the git publish path at commit-success time as operator publish intent; accepted Render deploy lag is not modeled by this field."
  - "Postgres verification remains a manual PG-gated step even though the schema/migration path is dual-dialect in code."
patterns-established:
  - "Publish tracking uses a dedicated batch UPDATE method rather than embedding state changes into shared upserts."
  - "Studio publish boundaries capture one UTC instant per batch and reuse it across every stamped key."
requirements-completed: [PUB-01]
duration: 12 min
completed: 2026-06-18
---

# Phase 55 Plan 01 Summary

**A dual-dialect pushed-to-prod timestamp now round-trips through content_site_index and is stamped only when git Publish commits or DirectPush finishes its prod batch.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-06-18T17:22:40Z
- **Completed:** 2026-06-18T17:34:58Z
- **Tasks:** 2
- **Files modified:** 19

## Accomplishments

- Added `pushed_to_prod_utc` to the local/prod `content_site_index` schema with fresh-create coverage, idempotent SQLite/Postgres migration logic, SELECT round-trip support, and a dedicated `StampPushedToProdAsync` writer.
- Preserved the existing `published_utc` and seed-export contract by keeping the new field out of every upsert path, including `UpsertContentColumnsOnlyAsync`, so re-distill cannot reset a prior publish stamp.
- Wired both publish boundaries: git Publish now stamps the local index after commit success, and DirectPush now stamps local plus prod with the same `DateTimeOffset.UtcNow` instant after the prod upsert batch succeeds.
- Added focused Core and Studio test coverage plus no-op/recording fake implementations needed for the expanded `IContentSiteIndexStore` contract.

## Task Commits

1. **Task 1: Add pushed_to_prod_utc column + dedicated writer + round-trip persistence** - `727b977` (`feat(55-01): add pushed-to-prod site index stamp`)
2. **Task 2: Stamp pushed_to_prod_utc at both publish boundaries and update fakes/tests** - `d4a138d` (`feat(55-01): stamp publish boundaries`)

## Files Created/Modified

- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` - added `ContentSiteIndexRow.PushedToProdUtc` with `init` semantics preserved.
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` - added `StampPushedToProdAsync` as the sole publish-stamp writer.
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` - added schema/create/select support for `pushed_to_prod_utc` plus the dedicated batch update writer.
- `DeckFlow.Studio/Pages/Publish.razor` - captured Stage-1 approved keys and stamped the local index after commit success.
- `DeckFlow.Studio/Pages/DirectPush.razor` - stamped local and prod stores with one shared UTC instant after successful prod upserts.
- `DeckFlow.Core.Tests/Content/ContentSiteIndexStorePushedToProdTests.cs` - covered migration idempotency, targets-only stamping, round-trip persistence, null semantics, and re-distill preservation.
- `DeckFlow.Core.Tests/Orchestration/ContentPublishStampTests.cs` - covered interface-level stamp/null behavior.
- `DeckFlow.Studio.Tests/PublishPageTests.cs` - asserted commit-success stamping on the git publish path.
- `DeckFlow.Studio.Tests/DirectPushPageTests.cs` - asserted local+prod stamping with one shared instant on the direct-push path.

## Decisions Made

- `pushed_to_prod_utc` is the new column name; `published_utc` remains the video publication timestamp and the seed JSON shape remains unchanged.
- `StampPushedToProdAsync` is the only writer of the new column; all three upsert SQL constants intentionally omit it.
- The git publish path records commit-time intent, not deploy completion; Render redeploy lag is accepted.
- Manual Postgres verification is still required for the PG-gated path even though the code now carries the migration branch.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- The plan’s Task 1 build command passed two projects to one `dotnet build` invocation, which MSBuild rejects. Verification used separate Windows `dotnet` builds for `DeckFlow.Core` and `DeckFlow.Core.Tests`, then the full-solution gate.
- A parallel Core/Core.Tests build briefly locked `DeckFlow.Core.dll`; rerunning the test-project build serially resolved the tooling issue with no code change.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 55-02 can now derive publish state from the local `PushedToProdUtc` fact without touching the seed export contract.
- Manual PG verification remains the only follow-up gate called out by this plan.

---
*Phase: 55-publish-state-foundation*
*Completed: 2026-06-18*
