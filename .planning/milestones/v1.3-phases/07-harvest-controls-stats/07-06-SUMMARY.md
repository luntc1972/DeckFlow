---
plan: 07-06
phase: 07
title: Stats aggregator + 8-metric panel + D-13 explicit cache invalidation
wave: 5
status: complete
shipped: 2026-05-03
requirements: [HARV-06]
---

# Plan 07-06 — Stats Aggregator + Panel

## What shipped

Closes HARV-06: the eight-metric stats panel on `/Admin/Harvest`. Also closes B1 (D-13 explicit cache invalidation): `HarvestStatsAggregator.Invalidate()` is wired through to `HarvestRunStore` writes via the nullable ctor parameter shipped by Plan 01, so an operator who clicks Run Now sees fresh numbers without waiting for the 60s TTL.

## Files modified / created

| File | Change |
|------|--------|
| `DeckFlow.Web/Services/Harvest/IHarvestStatsAggregator.cs` | NEW — relocated from stub in HarvestRunStore.cs; gains `GetAsync(ct)` alongside `Invalidate()` |
| `DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs` | NEW — sealed impl, IMemoryCache 60s TTL on key `admin.harvest.stats.v1`, `Invalidate()` calls `_memoryCache.Remove(...)` |
| `DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs` | NEW — `HarvestStatsPayload` and `TopCommanderRow` records |
| `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` | Stub `IHarvestStatsAggregator` interface removed (moved to its own file); `_stats?.Invalidate()` calls intact (B1 / D-13 wiring preserved) |
| `DeckFlow.Web/Services/ICategoryKnowledgeStore.cs` | +5 method declarations: GetTotalProcessedDeckCountAsync, GetTotalProcessedDeckCountSinceAsync, GetTotalObservationCountAsync, GetTopCommandersAsync, GetPostgresDatabaseSizeBytesAsync |
| `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` | +5 impl methods (W8 — store layer only, none on the repository) |
| `DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs` | Registration swapped from NullHarvestStatsAggregator scaffold to HarvestStatsAggregator; scaffold class deleted; B1 ordering preserved (AGG=29 < STORE=30) |
| `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` | +1 ctor dep `IHarvestStatsAggregator`; `Index` GET calls `_statsAggregator.GetAsync(ct)` to populate `vm.Stats` |
| `DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs` | `Stats` property changes from placeholder to `HarvestStatsPayload?` |
| `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` | Stats panel placeholder replaced with full 8-metric render: total decks, 30d, observations, top-10 commanders list, recent runs table, PG storage size (N/A on SQLite), last success, next scheduled |
| `DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs` | +5 no-op stubs to satisfy broadened interface |
| `DeckFlow.Web.Tests/CategorySuggestionServiceTests.cs` | +5 no-op stubs on private `FakeKnowledgeStore` |

## Decisions honored

| ID | Decision | Where |
|----|----------|-------|
| D-13 | IMemoryCache 60s TTL + explicit invalidation on harvest_runs writes | `HarvestStatsAggregator.GetAsync` cache config + `_stats?.Invalidate()` calls in HarvestRunStore (count = 2) |
| D-14 | `pg_database_size()` PG-only via IRelationalDialect.IsPostgres | `CategoryKnowledgeStore.GetPostgresDatabaseSizeBytesAsync` early-return null on SQLite |
| D-15 | Top-10 commanders GROUP BY deck_queue.commander_name | `GetTopCommandersAsync` SQL |
| D-16 | All 8 metrics rendered | `HarvestStatsPayload` + Razor view |
| D-17 | Top-N reads commander_name from deck_queue (Plan 01 column add + Plan 02 commander capture + Plan 04 URL-path UPSERT all required) | End-to-end |
| RESEARCH Q2 | Single source of truth for last_success_utc — `IHarvestRunStore.GetLastSuccessUtcAsync` shared by both stats aggregator and HarvestScheduleService | `HarvestStatsAggregator.GetAsync` calls the same method as Plan 03's tick service |

## W8 (store-only consolidation)

Verified: `grep -qE "GetTopCommandersAsync\|GetTotalProcessedDeckCountAsync\|GetTotalProcessedDeckCountSinceAsync\|GetTotalObservationCountAsync\|GetPostgresDatabaseSizeBytesAsync" DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` returns nothing — none of the new stats methods leaked into the repository.

## Acceptance gates (all pass)

```
✓ IHarvestStatsAggregator.cs has Invalidate + GetAsync
✓ HarvestStatsAggregator key admin.harvest.stats.v1 + 60s TTL + _memoryCache.Remove
✓ Five stats methods on CategoryKnowledgeStore (W8: zero on repository)
✓ pg_database_size present in store
✓ DI registration: AddSingleton<IHarvestStatsAggregator, HarvestStatsAggregator>
✓ NullHarvestStatsAggregator scaffold removed
✓ B1 ordering: AGG=29 < STORE=30 ✓
✓ D-13 invalidate count in HarvestRunStore: 2
✓ Controller calls _statsAggregator.GetAsync
✓ View placeholder "Statistics will appear here" replaced
✓ View renders top commanders / recent runs / storage / timestamps
```

`dotnet build DeckFlow.sln --nologo` → 0 Warning(s), 0 Error(s).

## Commits

- `(prev)` feat(07-06): add IHarvestStatsAggregator + 5 store query methods (D-13, D-14, D-15, D-16)
- `270018c` feat(07-06): wire stats panel into /Admin/Harvest + replace null aggregator (HARV-06)

## Routing

Authored via Codex MCP at `gpt-5.4` (full) per the global model-selection rule. Multi-file architecture work: 12 files modified/created across the controller, view, store, aggregator, DI extension, models, and test fakes.
