---
phase: 25-admin-harvested-decks-paged-grid
plan: 01
subsystem: database
tags: [aspnet, sqlite, postgres, paging, harvest-stats]

requires: []
provides:
  - Core paged processed-commander aggregate query with stable ordering
  - Web HarvestedCommanderRow DTO and store paging wrapper
  - deck_queue indexes for processed, paged sort, and commander paths
  - Harvest stats cold-path parallelization and reltuples observation-count fast path
affects: [25-02-admin-harvested-decks-paged-grid-ui, admin-harvest, category-knowledge]

tech-stack:
  added: []
  patterns:
    - Core repository returns named tuples; Web store maps to DTOs
    - Schema-qualified Postgres reltuples estimate with COUNT fallback
    - Independent stats reads fan out via Task.WhenAll

key-files:
  created:
    - DeckFlow.Web.Tests/HarvestStatsAggregatorTests.cs
  modified:
    - DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs
    - DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs
    - DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs
    - DeckFlow.Web/Services/ICategoryKnowledgeStore.cs
    - DeckFlow.Web/Services/CategoryKnowledgeStore.cs
    - DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs
    - DeckFlow.Web.Tests/CommanderCategoryServiceTests.cs
    - DeckFlow.Web.Tests/CategorySuggestionServiceTests.cs
    - DeckFlow.Web.Tests/CategoryKnowledgeStoreTests.cs

key-decisions:
  - "Keep Core/Web boundary intact: Core returns named tuples, Web maps to HarvestedCommanderRow."
  - "Keep GetTopCommandersAsync public API in place, but remove it from the stats cold path."
  - "Retain an empty TopCommanders compatibility property until 25-02 removes the existing Razor consumer."

patterns-established:
  - "Paged deck_queue commander aggregates order by deck_count DESC, last_processed_utc DESC, commander_name ASC."
  - "Stats payload cold reads start together and synchronize with Task.WhenAll."

requirements-completed: [AHD-01]

duration: 21 min
completed: 2026-05-24
---

# Phase 25 Plan 01: Admin Harvested Decks Data Layer Summary

**Server-side harvested-commander aggregate paging and harvest stats cold-path reductions across Core and Web stores**

## Rework: Commander Aggregate

- Replaced the per-deck page shape with commander aggregates: `CommanderName`, `DeckCount`, and `LastProcessedUtc = MAX(last_checked_utc)`.
- Added `GetDistinctProcessedCommanderCountAsync`; paging totals now use distinct processed commanders instead of total processed decks.
- Removed the dead per-deck repository/store/DTO surface after the UI switched to commander rows.
- Rework commits: `6ff020d` (`feat(25-01): add commander aggregate queries`) and `f8568e0` (`feat(25-02): show harvested commander aggregates`).

## Performance

- **Duration:** 21 min
- **Started:** 2026-05-24T22:29:00Z
- **Completed:** 2026-05-24T22:49:46Z
- **Tasks:** 3
- **Files modified:** 10

## Accomplishments

- Added `GetPagedProcessedCommanderRowsAsync` in Core with defensive page/pageSize clamps, aggregate ordering, and parameterized LIMIT/OFFSET.
- Added `GetDistinctProcessedCommanderCountAsync` for paging totals based on distinct processed commanders.
- Added the three planned `deck_queue` indexes, including `(processed, inserted_utc, deck_id)` for the paged sort.
- Added `HarvestedCommanderRow`, Web store paging wrapper, and all `ICategoryKnowledgeStore` implementor updates.
- Reworked harvest stats cold path to use Postgres `reltuples` for observation count, run independent queries via `Task.WhenAll`, and stop querying top commanders.

## Task Commits

1. **Task 1: Repository paged rows + indexes** - `d8318b2` (`feat(25-01): add paged processed deck repository query`)
2. **Task 2: DTO + store interface/impl + fakes** - `01bcc39` (`feat(25-01): add harvested deck store paging`)
3. **Task 3: reltuples + stats parallelization** - `c1a4994` (`perf(25-01): parallelize harvest stats queries`)
4. **Rework: commander aggregate repository query/count** - `6ff020d` (`feat(25-01): add commander aggregate queries`)

## Files Created/Modified

- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` - paged processed-commander aggregate query, distinct commander count, and deck_queue index DDL.
- `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` - repository aggregate ordering, page slicing, null/unprocessed exclusion, clamp, distinct count, and index tests.
- `DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs` - `HarvestedCommanderRow` DTO and stats payload constructor update.
- `DeckFlow.Web/Services/ICategoryKnowledgeStore.cs` - paged harvested-commanders store method and distinct commander count.
- `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` - Web DTO mapping, distinct commander count delegation, reltuples count fast path, COUNT fallback.
- `DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs` - `Task.WhenAll` fan-out and top-commanders query removal.
- `DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs` - paged result configuration plus page/pageSize recording.
- `DeckFlow.Web.Tests/CommanderCategoryServiceTests.cs` - inline fake interface stub.
- `DeckFlow.Web.Tests/CategorySuggestionServiceTests.cs` - inline fake interface stub.
- `DeckFlow.Web.Tests/CategoryKnowledgeStoreTests.cs` - store wrapper and shared fake tests.
- `DeckFlow.Web.Tests/HarvestStatsAggregatorTests.cs` - stats fan-out and top-commanders regression tests.

## Decisions Made

- The Core repository remains Web-agnostic; `HarvestedCommanderRow` exists only in Web.
- `GetTopCommandersAsync` remains on `ICategoryKnowledgeStore` and `CategoryKnowledgeStore`; only the stats cold-path call was removed.
- The existing Razor top-commanders block is left for 25-02, so `HarvestStatsPayload.TopCommanders` remains as an empty compatibility property, not a constructor payload parameter.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added empty TopCommanders compatibility property**
- **Found during:** Task 3
- **Issue:** Removing the `HarvestStatsPayload` positional `TopCommanders` parameter made `AdminHarvest/Index.cshtml` fail Razor compilation before 25-02 removes that block.
- **Fix:** Kept the cold-path query removed and added an empty init-only `TopCommanders` compatibility property so the existing Razor block compiles until 25-02 replaces it.
- **Files modified:** `DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs`
- **Verification:** `dotnet build DeckFlow.sln -c Release` exits 0 with 0 warnings / 0 errors; `grep -c 'GetTopCommandersAsync' DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs` returns 0.
- **Committed in:** `c1a4994`

---

**Total deviations:** 1 auto-fixed (blocking compile bridge).
**Impact on plan:** Cold-path performance goals are intact; the compatibility property carried no queried data and was removed by 25-02 with the Razor block.

## Issues Encountered

- Full `DeckFlow.Web.Tests` currently fails 13 unrelated `AdminCssPhase1Tests` marker assertions (`Failed: 13, Passed: 454, Skipped: 3, Total: 470`). These tests target Phase 18 CSS markers and were not changed or repaired in this plan.

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Release` - **Passed**, 0 warnings / 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -v minimal --filter "FullyQualifiedName~CategoryKnowledgeRepositoryTests"` - **Passed**, Failed: 0, Passed: 13.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -v minimal --filter "FullyQualifiedName~CategoryKnowledgeStoreTests|FullyQualifiedName~HarvestStatsAggregatorTests"` - **Passed**, Failed: 0, Passed: 21.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -v minimal` - **Failed**, Failed: 13, Passed: 454, Skipped: 3, Total: 470; failures are the unrelated `AdminCssPhase1Tests` marker assertions.
- Grep gates passed: deck_queue index count 3, `deck_id DESC` count 1, `Task.WhenAll` count 1, aggregator `GetTopCommandersAsync` count 0, `to_regclass` count 2.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

25-02 now consumes `ICategoryKnowledgeStore.GetPagedProcessedCommandersAsync`, `GetDistinctProcessedCommanderCountAsync`, and `HarvestedCommanderRow`. The old top-commanders Razor block and temporary empty `HarvestStatsPayload.TopCommanders` compatibility property have been removed.

## Self-Check: PASSED

The reworked data layer passes the Release build and targeted repository tests. Known pre-existing `AdminCssPhase1Tests` failures remain unrelated and were ignored per dispatch instructions.

---
*Phase: 25-admin-harvested-decks-paged-grid*
*Completed: 2026-05-24*
