---
phase: 25-admin-harvested-decks-paged-grid
plan: 01
subsystem: database
tags: [aspnet, sqlite, postgres, paging, harvest-stats]

requires: []
provides:
  - Core paged processed-deck tuple query with stable ordering
  - Web HarvestedDeckRow DTO and store paging wrapper
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
  - "Keep Core/Web boundary intact: Core returns named tuples, Web maps to HarvestedDeckRow."
  - "Keep GetTopCommandersAsync public API in place, but remove it from the stats cold path."
  - "Retain an empty TopCommanders compatibility property until 25-02 removes the existing Razor consumer."

patterns-established:
  - "Paged deck_queue reads use ORDER BY inserted_utc DESC, deck_id DESC with LIMIT/OFFSET parameters."
  - "Stats payload cold reads start together and synchronize with Task.WhenAll."

requirements-completed: [AHD-01]

duration: 21 min
completed: 2026-05-24
---

# Phase 25 Plan 01: Admin Harvested Decks Data Layer Summary

**Server-side harvested-deck paging and harvest stats cold-path reductions across Core and Web stores**

## Performance

- **Duration:** 21 min
- **Started:** 2026-05-24T22:29:00Z
- **Completed:** 2026-05-24T22:49:46Z
- **Tasks:** 3
- **Files modified:** 10

## Accomplishments

- Added `GetPagedProcessedDeckRowsAsync` in Core with defensive page/pageSize clamps, stable two-key ordering, and parameterized LIMIT/OFFSET.
- Added the three planned `deck_queue` indexes, including `(processed, inserted_utc, deck_id)` for the paged sort.
- Added `HarvestedDeckRow`, Web store paging wrapper, and all four `ICategoryKnowledgeStore` implementor updates.
- Reworked harvest stats cold path to use Postgres `reltuples` for observation count, run independent queries via `Task.WhenAll`, and stop querying top commanders.

## Task Commits

1. **Task 1: Repository paged rows + indexes** - `d8318b2` (`feat(25-01): add paged processed deck repository query`)
2. **Task 2: DTO + store interface/impl + fakes** - `01bcc39` (`feat(25-01): add harvested deck store paging`)
3. **Task 3: reltuples + stats parallelization** - `c1a4994` (`perf(25-01): parallelize harvest stats queries`)

## Files Created/Modified

- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` - paged processed-deck tuple query and deck_queue index DDL.
- `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` - repository paging, tie stability, null, clamp, and index tests.
- `DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs` - `HarvestedDeckRow` DTO and stats payload constructor update.
- `DeckFlow.Web/Services/ICategoryKnowledgeStore.cs` - paged harvested-decks store method.
- `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` - Web DTO mapping, reltuples count fast path, COUNT fallback.
- `DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs` - `Task.WhenAll` fan-out and top-commanders query removal.
- `DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs` - paged result configuration plus page/pageSize recording.
- `DeckFlow.Web.Tests/CommanderCategoryServiceTests.cs` - inline fake interface stub.
- `DeckFlow.Web.Tests/CategorySuggestionServiceTests.cs` - inline fake interface stub.
- `DeckFlow.Web.Tests/CategoryKnowledgeStoreTests.cs` - store wrapper and shared fake tests.
- `DeckFlow.Web.Tests/HarvestStatsAggregatorTests.cs` - stats fan-out and top-commanders regression tests.

## Decisions Made

- The Core repository remains Web-agnostic; `HarvestedDeckRow` exists only in Web.
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
**Impact on plan:** Cold-path performance goals are intact; the compatibility property carries no queried data and should be removed by 25-02 with the Razor block.

## Issues Encountered

- Full `DeckFlow.Web.Tests` currently fails 13 unrelated `AdminCssPhase1Tests` marker assertions (`Failed: 13, Passed: 454, Skipped: 3, Total: 470`). These tests target Phase 18 CSS markers and were not changed or repaired in this plan.

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Release` - **Passed**, 0 warnings / 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -v minimal --filter "FullyQualifiedName~CategoryKnowledgeRepositoryTests"` - **Passed**, Failed: 0, Passed: 12.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -v minimal --filter "FullyQualifiedName~CategoryKnowledgeStoreTests|FullyQualifiedName~HarvestStatsAggregatorTests"` - **Passed**, Failed: 0, Passed: 21.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -v minimal` - **Failed**, Failed: 13, Passed: 454, Skipped: 3, Total: 470; failures are the unrelated `AdminCssPhase1Tests` marker assertions.
- Grep gates passed: deck_queue index count 3, `deck_id DESC` count 1, `Task.WhenAll` count 1, aggregator `GetTopCommandersAsync` count 0, `to_regclass` count 2.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

25-02 can consume `ICategoryKnowledgeStore.GetPagedProcessedDecksAsync` and `HarvestedDeckRow` directly. It should remove the old top-commanders Razor block, which will also allow removing the temporary empty `HarvestStatsPayload.TopCommanders` compatibility property.

## Self-Check: FAILED

The implementation and Release build gates passed, but the full `DeckFlow.Web.Tests` suite is not Failed: 0 because of pre-existing/unrelated `AdminCssPhase1Tests` assertion failures.

---
*Phase: 25-admin-harvested-decks-paged-grid*
*Completed: 2026-05-24*
