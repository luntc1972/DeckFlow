---
phase: 25-admin-harvested-decks-paged-grid
reviewed: 2026-05-24T23:55:00Z
depth: standard
files_reviewed: 15
files_reviewed_list:
  - DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs
  - DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs
  - DeckFlow.Web/Services/ICategoryKnowledgeStore.cs
  - DeckFlow.Web/Services/CategoryKnowledgeStore.cs
  - DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs
  - DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs
  - DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs
  - DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs
  - DeckFlow.Web/Views/AdminHarvest/Index.cshtml
  - DeckFlow.Web.Tests/AdminHarvestControllerTests.cs
  - DeckFlow.Web.Tests/CategoryKnowledgeStoreTests.cs
  - DeckFlow.Web.Tests/HarvestStatsAggregatorTests.cs
  - DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs
  - DeckFlow.Web.Tests/CommanderCategoryServiceTests.cs
  - DeckFlow.Web.Tests/CategorySuggestionServiceTests.cs
findings:
  critical: 0
  warning: 4
  info: 4
  total: 8
status: issues_found
---

# Phase 25: Code Review Report

**Reviewed:** 2026-05-24T23:55:00Z
**Depth:** standard
**Files Reviewed:** 15
**Status:** issues_found

## Summary

Reviewed the server-side paged COMMANDER-aggregate grid added to `/Admin/Harvest`, the
supporting data-layer methods (`GetPagedProcessedCommanderRowsAsync`,
`GetDistinctProcessedCommanderCountAsync`), the three new `deck_queue` indexes, the
`Task.WhenAll` parallelization of `HarvestStatsAggregator.BuildAsync`, and the
schema-qualified Postgres reltuples observation count.

**Security:** No SQL injection. `@limit`/`@offset` are bound via
`RelationalDatabaseConnection.AddParameter` (parameterized, never interpolated). The
admin grid is behind the existing `/Admin/*` BasicAuth gate. No injection on the
page→OFFSET path. `to_regclass('public.card_category_observations')` is a hardcoded
schema-qualified literal — no untrusted input reaches it.

**Correctness:** Page clamping is correct (`Math.Max(page,1)` then
`Math.Min(page, deckTotalPages)`), the repository defends independently with
`Math.Max(page,1)`/`Math.Max(pageSize,1)`, OFFSET is computed as `long` so no overflow
on a huge page, and the `reltuples <= 0` fallback to `COUNT(1)` is correct (reltuples
returns -1 on never-analyzed tables → falls through). `Task.WhenAll` aggregates all
exceptions before the per-task awaits, so no unobserved-task leak. Core does not
reference any Web type — the repository returns a value tuple and the
`HarvestedCommanderRow` projection lives in the Web layer (clean layering).

The remaining concerns are quality and edge-case robustness issues, not blockers: a
case-sensitivity mismatch between the count/grouping query and the commander
detail/lookup queries, unformatted timestamp rendering, a checked-cast overflow risk on
the reltuples path, and a count/paged read consistency gap on this two-query admin page.

## Warnings

### WR-01: Distinct-commander count is case-sensitive but commander lookups are case-insensitive

**File:** `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs:338-394`
**Issue:** `GetPagedProcessedCommanderRowsAsync` groups by raw `commander_name`
(`GROUP BY commander_name`) and `GetDistinctProcessedCommanderCountAsync` uses
`COUNT(DISTINCT commander_name)` — both case-sensitive. But the click-through query
`GetCategoryRowsForCommanderAsync` (line 285-292) and `GetCommanderDeckCountAsync`
(line 321-325) match with `LOWER(q.commander_name) = LOWER(@commanderName)`. If the same
commander is stored under two casings (e.g. `"Atraxa"` and `"atraxa"`, possible because
`commander_name` comes from imported deck text with no normalization at write time), the
grid renders two distinct rows with split deck counts, but selecting either aggregates
both as one. The displayed `DeckCount` then disagrees with the detail page, and
`DeckTotalCount`/`DeckTotalPages` overstate the real commander population.
**Fix:** Make the grid grouping and distinct count agree with the lookup path. Group and
count on a normalized key:
```sql
-- count
SELECT COUNT(DISTINCT LOWER(commander_name))
FROM deck_queue
WHERE processed = 1 AND commander_name IS NOT NULL;

-- paged rows
SELECT MIN(commander_name) AS commander_name, COUNT(1) AS deck_count, MAX(last_checked_utc) AS last_processed_utc
FROM deck_queue
WHERE processed = 1 AND commander_name IS NOT NULL
GROUP BY LOWER(commander_name)
ORDER BY deck_count DESC, last_processed_utc DESC, LOWER(commander_name) ASC
LIMIT @limit OFFSET @offset;
```
(Or normalize `commander_name` at write time in `MarkDeckProcessedAsync` /
`MarkUrlDeckProcessedAsync`.) At minimum add a test that seeds mixed-case commanders and
asserts a single grid row.

### WR-02: `LastProcessedUtc` rendered as raw ISO-8601 string in the grid

**File:** `DeckFlow.Web/Views/AdminHarvest/Index.cshtml:221`
**Issue:** The harvested-commanders grid prints the raw stored value
`@(c.LastProcessedUtc ?? "—")`, which is `DateTimeOffset.ToString("O")`, e.g.
`2026-01-04T00:00:00.0000000+00:00`. Every other timestamp on the page is formatted —
the Recent Runs / Run Log tables use `.ToString("u")` (lines 140, 174) and the Stats
block uses `"yyyy-MM-dd HH:mm UTC"` (lines 118-119). The column header even says
"Last Processed (UTC)" but shows a 7-fractional-digit `+00:00`-offset string. Poor
readability and inconsistent with the rest of the admin UI.
**Fix:** Parse and format in the view (the value is a plain string off the value tuple),
e.g.:
```cshtml
<td>@(DateTimeOffset.TryParse(c.LastProcessedUtc, out var lp) ? lp.UtcDateTime.ToString("u") : "—")</td>
```
or expose `LastProcessedUtc` as a `DateTimeOffset?` on `HarvestedCommanderRow` and format
with `.ToString("u")` for parity with the run tables.

### WR-03: `checked((int)value)` on reltuples can throw OverflowException for large tables

**File:** `DeckFlow.Web/Services/CategoryKnowledgeStore.cs:114-136, 321-332`
**Issue:** `GetTotalObservationCountAsync` runs `SELECT reltuples::bigint ...` and routes
the result through `ExecuteCountAsync`, which does `long value => checked((int)value)`.
`card_category_observations` is the high-cardinality observation table (one row per
card/category/board/source); on a long-running harvest it can plausibly exceed
`int.MaxValue` (~2.1B). When it does, the `checked` cast throws `OverflowException`,
which is caught only as a generic failure in the controller (the whole stats panel goes
"unavailable"), rather than degrading gracefully. The same cast is on the
`COUNT(1)` fallback path.
**Fix:** Saturate instead of throwing for the count display, e.g. clamp:
```csharp
long value => value > int.MaxValue ? int.MaxValue : (value < int.MinValue ? int.MinValue : (int)value),
```
or change `GetTotalObservationCountAsync`/the payload field to `long`. Since this is a
display-only counter, saturating to `int.MaxValue` is acceptable and avoids tearing down
the entire stats panel.

### WR-04: Grid total count and grid rows are read in two unsynchronized queries

**File:** `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs:87-90`
**Issue:** `deckTotal` (line 87) and `pagedCommanders` (line 90) are two separate round
trips on two separate connections, with the harvest background sweep concurrently
writing `deck_queue`. If a sweep commits new commanders between the two reads, `page` can
be clamped against a stale `deckTotalPages` and the grid can show "Page N of M" where M
no longer matches the data, or the last page can come back empty after a clamp computed
against the smaller count. On a low-traffic admin page this is cosmetic, but it is a
genuine read-consistency gap introduced by splitting count and slice.
**Fix:** Either accept it explicitly (document that the admin grid is eventually
consistent), or read count + page inside one connection/transaction. Given the page's
low stakes, a one-line `// Why:` comment acknowledging the eventual-consistency tradeoff
is the minimum; a single-connection read is the robust fix.

## Info

### IN-01: `GetTopCommandersAsync` is now orphaned production code

**File:** `DeckFlow.Web/Services/ICategoryKnowledgeStore.cs:74`, `CategoryKnowledgeStore.cs:139-162`
**Issue:** After this phase replaced the top-10 panel with the paged grid,
`GetTopCommandersAsync` has no production caller (only test doubles and the interface
implement it). It remains on the public interface and in the implementation as dead
surface. (Consistent with prior observation 1549 flagging it orphaned.)
**Fix:** Remove `GetTopCommandersAsync` from `ICategoryKnowledgeStore` and
`CategoryKnowledgeStore`, plus the corresponding members in the test doubles, unless a
near-term consumer is planned. Keeping it grows the interface that every fake must
implement (Interface Segregation).

### IN-02: Skipped-deck exclusion relies on caller passing null commander, not an explicit predicate

**File:** `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs:355, 390`
**Issue:** The grid/count queries filter `processed = 1 AND commander_name IS NOT NULL`
but never `skipped = 0`. Skipped decks have `processed = 1, skipped = 1`. They are
excluded only because the sole skip caller (`ArchidektDeckCacheSession.cs:124`) passes
`commanderName: null`. The exclusion is therefore implicit and would silently break if a
future caller marks a deck skipped while retaining a commander name.
**Fix:** Make the intent explicit by adding `AND skipped = 0` to both queries so
correctness does not depend on caller discipline. Low priority since the current caller
behaves correctly.

### IN-03: Redundant `recentRuns` fetch in `Index`

**File:** `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs:69, 74`
**Issue:** `Index` calls `_runStore.GetRecentAsync(10, ...)` directly (line 69) and the
stats aggregator independently fetches `GetRecentAsync(10, ...)` inside `BuildAsync`
(`HarvestStatsAggregator.cs:67`). Two round trips for the same data on each cold page
load. The view uses `Model.Stats.RecentRuns` when present and `Model.RecentRuns`
otherwise, so both are needed today, but the duplication is wasteful.
**Fix:** Have the view rely on a single source (prefer `Stats.RecentRuns`, fall back to
the directly-fetched list only when stats are null), and skip the direct fetch when stats
succeed. Pre-existing pattern, optional.

### IN-04: `GetOrCreateAsync(...)!` null-forgiving masks a real nullable contract

**File:** `DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs:44-49`
**Issue:** `GetAsync` returns `_memoryCache.GetOrCreateAsync(...)!`. The `!` suppresses
the `Task<HarvestStatsPayload?>` → `Task<HarvestStatsPayload>` mismatch. The factory
never returns null today, so it is safe, but the suppression hides the contract and would
silently propagate a null if the factory ever changed (e.g. early-return on cancellation).
**Fix:** Either keep `GetAsync` returning `Task<HarvestStatsPayload?>` and let the
controller's existing null check handle it, or assert non-null with a clear message
instead of a bare `!`. Style/robustness only.

---

_Reviewed: 2026-05-24T23:55:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
