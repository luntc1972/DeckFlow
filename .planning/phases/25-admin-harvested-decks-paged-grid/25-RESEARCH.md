# Phase 25: Admin Harvested-Decks Paged Grid - Research

**Researched:** 2026-05-24
**Domain:** Brownfield ASP.NET 10 / Razor / SQLite+Postgres admin UI — server-side paging + SQL index additions + stat query parallelisation
**Confidence:** HIGH

---

## Summary

Phase 25 has two separable goals delivered together:

1. **Paged grid** — replace the top-ten `GROUP BY commander_name` list in the Stats section of `/Admin/Harvest` with a server-side paged table of ALL harvested decks from `deck_queue` (rows where `processed = 1`), showing deck_id, commander_name, inserted_utc, and last_checked_utc. Page size constant + total-count display; LIMIT/OFFSET; no full-table load into memory.

2. **Cold-cache perf fix** — the 7 sequential `await` calls in `HarvestStatsAggregator.BuildAsync` and the missing indexes on `deck_queue` make the cold path slow. Fix: add three composite indexes to `deck_queue`, parallelize the four independent stat queries with `Task.WhenAll`, and use a Postgres `reltuples` estimate (or a cheap maintained counter) for the `card_category_observations` full-table `COUNT(*)`.

Both goals land in the same phase because the index DDL that speeds up the stats queries (`deck_queue(processed)`, `deck_queue(processed, inserted_utc)`, `deck_queue(processed, commander_name)`) also speeds up the new paged-grid query, and both touch `CategoryKnowledgeStore` / `CategoryKnowledgeRepository`.

**Primary recommendation:** Mirror the `AdminFeedbackController` / `FeedbackStore` paging pattern exactly (LIMIT/OFFSET, `int page = 1` route param, `TotalPages` computed property on view model). Add a new `GetPagedProcessedDecksAsync(int page, int pageSize, CancellationToken)` method to `CategoryKnowledgeRepository` and a corresponding `GetPagedProcessedDecksAsync` + `GetTotalProcessedDeckCountAsync` wrapper on `ICategoryKnowledgeStore` / `CategoryKnowledgeStore`. Add the three indexes in `EnsureSchemaAsync` in `CategoryKnowledgeRepository` using `CREATE INDEX IF NOT EXISTS`, which is valid for both SQLite and Postgres. Parallelize the four independent `BuildAsync` queries with `Task.WhenAll`. Render the grid with `.admin-table-scroll` + `.admin-table` (overflow-x pattern, same as HarvestRuns table) and the pagination nav from `AdminFeedback/Index.cshtml`.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|---|---|---|---|
| Paged deck query (LIMIT/OFFSET + COUNT) | Database / Storage | — | Lives in `CategoryKnowledgeRepository`; all raw SQL is kept in the repository layer per project convention |
| New store method surface | API / Backend | — | `ICategoryKnowledgeStore` interface + `CategoryKnowledgeStore` wrapper; follows existing stat-method pattern |
| Controller action + view model | API / Backend | — | `AdminHarvestController.Index` gains `int page = 1` param; new `HarvestedDecksPage` sub-view-model |
| Razor grid + pagination nav | Frontend Server (SSR) | — | Razor partial or inline section in `AdminHarvest/Index.cshtml`; no client-side JS needed |
| Index DDL | Database / Storage | — | Added in `CategoryKnowledgeRepository.EnsureSchemaAsync`; dual-dialect via `CREATE INDEX IF NOT EXISTS` |
| Stat query parallelisation | API / Backend | — | `HarvestStatsAggregator.BuildAsync` — no interface change, pure implementation fix |

---

## Standard Stack

No new external packages. All work uses the existing project stack.

### Existing libraries already in use

| Library | Purpose | File |
|---|---|---|
| `Microsoft.Data.Sqlite` / `Npgsql` | Raw SQL via `DbConnection` / `DbCommand` | `CategoryKnowledgeRepository.cs`, `RelationalDatabaseConnection.cs` |
| `Microsoft.Extensions.Caching.Memory` | 60-second stats cache (`IMemoryCache`) | `HarvestStatsAggregator.cs` |
| ASP.NET Core MVC / Razor | Controller + view rendering | `AdminHarvestController.cs`, `AdminHarvest/Index.cshtml` |
| `admin-common.css` / `admin-mobile.css` | Phase 18 responsive admin shell | `wwwroot/css/` |

### Package Legitimacy Audit

> No new packages — section not applicable. All dependencies are already installed.

---

## Architecture Patterns

### System Architecture Diagram

```
Browser GET /Admin/Harvest?page=N
         |
AdminHarvestController.Index(page)
         |
         +---> HarvestStatsAggregator.GetAsync()   [60s IMemoryCache]
         |              |
         |              +--> BuildAsync() [Task.WhenAll four independent queries]
         |                       |
         |                       +--> GetTotalProcessedDeckCountAsync()   [COUNT deck_queue WHERE processed=1]
         |                       +--> GetTotalProcessedDeckCountSinceAsync() [COUNT+filter inserted_utc]
         |                       +--> GetTotalObservationCountAsync()     [reltuples estimate or COUNT]
         |                       +--> GetTopCommandersAsync(10)           [GROUP BY commander_name]
         |                       +--> GetRecentAsync(10)                  [sequential — depends on nothing above]
         |                       +--> GetPostgresDatabaseSizeBytesAsync() [sequential — Postgres only]
         |                       +--> GetLastSuccessUtcAsync()            [sequential — for NextScheduled calc]
         |
         +---> CategoryKnowledgeStore.GetPagedProcessedDecksAsync(page, pageSize)
         |              |
         |              +--> CategoryKnowledgeRepository.GetPagedProcessedDecksAsync()
         |                       SELECT deck_id, commander_name, inserted_utc, last_checked_utc
         |                       FROM deck_queue WHERE processed=1
         |                       ORDER BY inserted_utc DESC LIMIT @limit OFFSET @offset
         |
         +---> AdminHarvestViewModel { ..., PagedDecks, DeckPage, DeckPageSize, DeckTotalCount }
         |
Razor AdminHarvest/Index.cshtml
    -- existing Stats/Run/Schedule panels unchanged
    -- new "Harvested Decks" panel: admin-table-scroll + admin-table (overflow-x pattern)
    -- pagination nav (mirrors AdminFeedback/Index.cshtml lines 86-96)
```

### Recommended Project Structure

No new files/folders needed. All changes land in existing files:

```
DeckFlow.Core/Knowledge/
  CategoryKnowledgeRepository.cs      # new GetPagedProcessedDecksAsync() + 3 CREATE INDEX IF NOT EXISTS
DeckFlow.Web/Services/
  ICategoryKnowledgeStore.cs          # new GetPagedProcessedDecksAsync() method
  CategoryKnowledgeStore.cs           # new GetPagedProcessedDecksAsync() impl + EnsureSchema wrapper
  Harvest/
    HarvestStatsAggregator.cs         # BuildAsync: Task.WhenAll the 4 independent queries
    HarvestStatsModels.cs             # (no change needed — paged decks are not part of the stats payload)
DeckFlow.Web/Models/Admin/
  AdminHarvestViewModel.cs            # new HarvestedDeckRow record + paging fields
DeckFlow.Web/Controllers/Admin/
  AdminHarvestController.cs           # Index(page) param, PagedDecks fetch
DeckFlow.Web/Views/AdminHarvest/
  Index.cshtml                        # new "Harvested Decks" panel section
DeckFlow.Web.Tests/
  TestDoubles/FakeCategoryKnowledgeStore.cs   # new stub for GetPagedProcessedDecksAsync
  (optional) HarvestStatsAggregatorTests.cs   # verify Task.WhenAll shape
```

---

## Pattern 1: Existing Paging Pattern (AdminFeedback — the canonical analog)

**What:** `AdminFeedbackController.Index(int page = 1)` clamps page to 1, passes `page` + `const int pageSize = 50` into a `FeedbackListQuery`, calls `FeedbackStore.ListAsync(query)` (LIMIT/OFFSET) and `CountAsync()`. The view model carries `Page`, `PageSize`, `TotalCount`, and a computed `TotalPages`. The Razor view renders a `<nav class="admin-feedback-pagination">` with Prev/Next tag-helpers.

**When to use:** Every admin table that must not load all rows. Phase 25 MUST follow this pattern exactly.

**Controller pattern:**

```csharp
// Source: DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs:57-76 (VERIFIED in codebase)
[HttpGet("")]
public async Task<IActionResult> Index(FeedbackStatus? status = FeedbackStatus.New,
    FeedbackType? type = null, int page = 1)
{
    page = Math.Max(page, 1);
    const int pageSize = 50;
    var query = new FeedbackListQuery { Status = status, Type = type, Page = page, PageSize = pageSize };
    var items = await _store.ListAsync(query);
    var total = await _store.CountAsync(status, type);
    // ...
    var vm = new AdminFeedbackListViewModel
    {
        Items = items, Page = page, PageSize = pageSize, TotalCount = total,
    };
    return View(vm);
}
```

**Store SQL pattern (FeedbackStore.cs:96-104):**

```sql
-- Source: DeckFlow.Web/Services/FeedbackStore.cs:96-104 (VERIFIED in codebase)
SELECT ... FROM feedback {where}
ORDER BY {dialect.FeedbackOrderByClause}
LIMIT @limit OFFSET @offset
```

```csharp
RelationalDatabaseConnection.AddParameter(command, "@limit", pageSize);
RelationalDatabaseConnection.AddParameter(command, "@offset", (page - 1) * pageSize);
```

**Pagination nav Razor (AdminFeedback/Index.cshtml:86-96):**

```razor
@* Source: DeckFlow.Web/Views/AdminFeedback/Index.cshtml:86-96 (VERIFIED in codebase) *@
<nav class="admin-feedback-pagination">
    @if (Model.Page > 1)
    {
        <a asp-action="Index" asp-route-page="@(Model.Page - 1)">Prev</a>
    }
    <span>Page @Model.Page of @Model.TotalPages</span>
    @if (Model.Page < Model.TotalPages)
    {
        <a asp-action="Index" asp-route-page="@(Model.Page + 1)">Next</a>
    }
</nav>
```

**TotalPages computed property:**

```csharp
// Source: AdminFeedbackController.cs:33 (VERIFIED in codebase)
public int TotalPages => (int)Math.Ceiling((double)Math.Max(TotalCount, 1) / Math.Max(PageSize, 1));
```

---

## Pattern 2: Index DDL — CREATE INDEX IF NOT EXISTS (dual-dialect)

**What:** `HarvestRunStore` already uses `CREATE INDEX IF NOT EXISTS` in both its `PostgresCreateTableSql` and `SqliteCreateTableSql` constants (lines 450-451, 469-470). Both SQLite and Postgres support this syntax. It is safe to execute on an existing DB — idempotent.

**Pattern:**

```sql
-- Source: DeckFlow.Web/Services/Harvest/HarvestRunStore.cs:450-451 (VERIFIED in codebase)
CREATE INDEX IF NOT EXISTS ix_harvest_runs_state       ON harvest_runs(state);
CREATE INDEX IF NOT EXISTS ix_harvest_runs_started_utc ON harvest_runs(started_utc DESC);
```

**For Phase 25, three indexes on `deck_queue`:**

```sql
-- To be added in CategoryKnowledgeRepository.EnsureSchemaAsync (both dialects)
CREATE INDEX IF NOT EXISTS ix_deck_queue_processed
    ON deck_queue(processed);
CREATE INDEX IF NOT EXISTS ix_deck_queue_processed_inserted
    ON deck_queue(processed, inserted_utc);
CREATE INDEX IF NOT EXISTS ix_deck_queue_processed_commander
    ON deck_queue(processed, commander_name);
```

**Where to add:** `CategoryKnowledgeRepository.EnsureSchemaAsync()` already runs separate `CreateCardCategoryObservationsTableAsync`, `EnsureDeckQueueColumnsAsync`, etc. The indexes should be added as a new step at the end of `EnsureSchemaAsync`, issuing all three as a single multi-statement command (like HarvestRunStore does) or as separate sequential commands. Both work; the HarvestRunStore uses them as separate statements in one raw string literal.

**Dual-dialect:** `CREATE INDEX IF NOT EXISTS` is identical SQL for both SQLite and Postgres. No dialect branching needed.

**`INSERT INTO deck_queue ... ON CONFLICT` note:** The repository already uses Postgres-compatible `ON CONFLICT(deck_id) DO UPDATE` syntax (line 780), which is also supported by modern SQLite. The index DDL will similarly be identical between dialects.

---

## Pattern 3: HarvestStatsAggregator.BuildAsync — Parallelization

**What:** `BuildAsync` makes 7 sequential `await` calls. Four of them are independent (no data dependency on each other):

| Call | Independent? |
|---|---|
| `GetTotalProcessedDeckCountAsync` | YES — standalone COUNT |
| `GetTotalProcessedDeckCountSinceAsync` | YES — standalone COUNT |
| `GetTotalObservationCountAsync` | YES — standalone COUNT |
| `GetTopCommandersAsync(10)` | YES — standalone GROUP BY |
| `GetRecentAsync(10)` | YES — reads `harvest_runs`, different table/store |
| `GetPostgresDatabaseSizeBytesAsync` | YES — standalone pg_database_size |
| `GetLastSuccessUtcAsync` | Needed before `NextScheduledUtc` calc, but still independent of the 6 above |

All 7 are in fact independent of each other. They can all be parallelized with a single `Task.WhenAll`. The `NextScheduledUtc` derivation is a pure in-memory calculation that happens after all tasks complete.

**Parallelization pattern:**

```csharp
// Source: HarvestStatsAggregator.cs (pattern to apply — ASSUMED convention mirrors Task.WhenAll)
var totalDecksTask          = _categoryStore.GetTotalProcessedDeckCountAsync(cancellationToken);
var totalDecks30dTask       = _categoryStore.GetTotalProcessedDeckCountSinceAsync(DateTime.UtcNow.AddDays(-30), cancellationToken);
var totalObservationsTask   = _categoryStore.GetTotalObservationCountAsync(cancellationToken);
var topCommandersTask       = _categoryStore.GetTopCommandersAsync(10, cancellationToken);
var recentRunsTask          = _runStore.GetRecentAsync(10, cancellationToken);
var postgresStorageTask     = _categoryStore.GetPostgresDatabaseSizeBytesAsync(cancellationToken);
var lastSuccessTask         = _runStore.GetLastSuccessUtcAsync(cancellationToken);

await Task.WhenAll(totalDecksTask, totalDecks30dTask, totalObservationsTask,
    topCommandersTask, recentRunsTask, postgresStorageTask, lastSuccessTask);
```

**Connection pool concern:** Each `CategoryKnowledgeStore` stat method opens its own `DbConnection`. Postgres pool default is unconstrained but the project caps explicit pools at 10-15 (STATE.md cross-cutting invariant). 6 concurrent connections for a cold-cache stat fetch is safe given total pool budget and the fact that harvesting (the only other heavy consumer) does not overlap with admin page views in practice. The planner should note this as a monitoring point but it is not a blocker.

**SQLite note:** SQLite has a single-writer lock; for reads this is not an issue (multiple readers are allowed). `Task.WhenAll` on read-only queries against SQLite is safe.

---

## Pattern 4: Postgres reltuples Estimate for Observation Count

**What:** `SELECT COUNT(1) FROM card_category_observations` is a full sequential scan on the largest table. For Postgres, `pg_class.reltuples` provides a planner estimate (updated at ANALYZE / autovacuum) that is accurate to within a few percent and runs in microseconds.

**SQL (Postgres only):**

```sql
SELECT reltuples::bigint
FROM pg_class
WHERE relname = 'card_category_observations';
```

**Implementation path:** Branch inside `CategoryKnowledgeStore.GetTotalObservationCountAsync` on `_connectionInfo.IsPostgres` (already used in `GetPostgresDatabaseSizeBytesAsync` line 153). For SQLite, keep the `COUNT(1)` (SQLite is fast on small datasets and there is no reltuples equivalent). Return `(int)` from `reltuples` — acceptable precision since the UI only shows a display number.

**Risk:** `reltuples` is -1 if the table has never been analyzed. Guard: if result <= 0, fall through to `COUNT(1)`. [ASSUMED — standard Postgres pattern; no project precedent to VERIFY against]

---

## Pattern 5: Phase 18 Responsive Admin Table/Card Patterns

**What:** Phase 18 established two table strategies in `admin-common.css` + `admin-mobile.css`:

1. **`admin-table-scroll` + `admin-table`** — wrapper with `overflow-x: auto` + `tabindex="0"` for keyboard pan. Used for comparison-dense / data-heavy tables (Harvest runs, Analytics, ContentHarvest). Keeps all columns on wide viewports; horizontal scroll on narrow.

2. **`admin-table--card`** — card-stack pattern at `≤768px`. The `<thead>` is visually clipped (`.sr-only` clip, stays in a11y tree). Each `<td>` becomes a flex row with `::before { content: attr(data-label) }` providing the visual label. Used for scanning tables (Feedback list, Flags).

**For Phase 25 paged grid:** Use `admin-table-scroll` + `admin-table` (overflow-x pattern). A deck list has potentially many columns (deck_id, commander_name, date) and is comparison-dense rather than scanning. This mirrors the HarvestRuns table in the existing `AdminHarvest/Index.cshtml` (lines 124-149).

**CSS selectors already defined in `admin-common.css`:**

```css
/* Source: wwwroot/css/admin-common.css:319-323 (VERIFIED in codebase) */
.admin-shell .admin-table-scroll {
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}
```

No new CSS classes are needed. The paged grid reuses `.admin-table-scroll`, `.admin-table`, and `tabindex="0"` / `aria-label` (same pattern as lines 125-148 of the existing view).

**Pagination nav CSS:** The existing `.admin-feedback-pagination` class in `admin-common.css` (line 253) is scoped to `.admin-shell`. The planner may either reuse that class name (and accept a slight semantic mismatch) or add a generic `.admin-pagination` rule. Safest choice: add `.admin-shell .admin-pagination { margin-top: 1rem; display: flex; gap: 0.75rem; align-items: center; }` in `admin-common.css` and use it in both `AdminFeedback/Index.cshtml` and the new paged grid. Alternatively, duplicate the Feedback nav inline with a different class — acceptable for a single consumer. The planner decides.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead |
|---|---|---|
| Pagination math | Custom pagination object | Computed `TotalPages` property on the view model (mirror `AdminFeedbackListViewModel`) |
| LIMIT/OFFSET SQL | Cursor/keyset manually | Standard LIMIT/OFFSET via `RelationalDatabaseConnection.AddParameter` (proven in FeedbackStore) |
| Observation count fast path | Custom counter table | Postgres `reltuples` estimate + SQLite `COUNT(1)` fallback |
| Parallel stat queries | Sequential awaits | `Task.WhenAll` — all 7 BuildAsync calls are independent |
| Index creation | `ALTER TABLE` + `IF NOT EXISTS` guard in C# | SQL `CREATE INDEX IF NOT EXISTS` — both dialects support it natively |
| CSS | New admin stylesheet | Existing `admin-common.css` + `admin-mobile.css` classes |

---

## HarvestStatsAggregator.BuildAsync — Full Query Map

Verified from `HarvestStatsAggregator.cs` (lines 61-70) and `CategoryKnowledgeStore.cs`:

| Step | Method | SQL | Table | Index needed |
|---|---|---|---|---|
| 1 | `GetTotalProcessedDeckCountAsync` | `SELECT COUNT(1) FROM deck_queue WHERE processed = 1` | `deck_queue` | `ix_deck_queue_processed` |
| 2 | `GetTotalProcessedDeckCountSinceAsync` | `SELECT COUNT(1) FROM deck_queue WHERE processed = 1 AND inserted_utc >= @cutoff` | `deck_queue` | `ix_deck_queue_processed_inserted` |
| 3 | `GetTotalObservationCountAsync` | `SELECT COUNT(1) FROM card_category_observations` | `card_category_observations` | `reltuples` estimate (Postgres) |
| 4 | `GetTopCommandersAsync(10)` | `SELECT commander_name, COUNT(1) AS deck_count FROM deck_queue WHERE processed = 1 AND commander_name IS NOT NULL GROUP BY commander_name ORDER BY deck_count DESC LIMIT @n` | `deck_queue` | `ix_deck_queue_processed_commander` |
| 5 | `GetRecentAsync(10)` | SELECT recent harvest_runs (separate store) | `harvest_runs` | already indexed (`ix_harvest_runs_started_utc`) |
| 6 | `GetPostgresDatabaseSizeBytesAsync` | `SELECT pg_database_size(current_database())` | system | — |
| 7 | `GetLastSuccessUtcAsync` | reads `harvest_runs` | `harvest_runs` | already indexed |

Steps 1-7 are all independent. After `Task.WhenAll`, `NextScheduledUtc` is computed in-memory from `lastSuccessTask.Result` + `_scheduleCache.Snapshot()`.

---

## deck_queue Schema (current, verified)

```sql
-- Source: CategoryKnowledgeRepository.cs:60-66 + EnsureDeckQueueColumnsAsync (VERIFIED in codebase)
CREATE TABLE IF NOT EXISTS deck_queue (
    deck_id          TEXT PRIMARY KEY,
    inserted_utc     TEXT NOT NULL,
    processed        INTEGER NOT NULL DEFAULT 0,
    skipped          INTEGER NOT NULL DEFAULT 0,
    last_checked_utc TEXT,
    commander_name   TEXT NULL        -- added by EnsureDeckQueueColumnsAsync migration
);
-- NO indexes currently exist on deck_queue (confirmed by grep: 0 CREATE INDEX hits in CategoryKnowledgeRepository.cs)
```

**New indexes to add (both dialects — identical SQL):**

```sql
CREATE INDEX IF NOT EXISTS ix_deck_queue_processed
    ON deck_queue(processed);
CREATE INDEX IF NOT EXISTS ix_deck_queue_processed_inserted
    ON deck_queue(processed, inserted_utc);
CREATE INDEX IF NOT EXISTS ix_deck_queue_processed_commander
    ON deck_queue(processed, commander_name);
```

---

## ICategoryKnowledgeStore — New Method Surface

The interface currently has no paging method. Two new methods are needed:

```csharp
// New additions to ICategoryKnowledgeStore (and CategoryKnowledgeStore + FakeCategoryKnowledgeStore)
Task<IReadOnlyList<HarvestedDeckRow>> GetPagedProcessedDecksAsync(
    int page, int pageSize, CancellationToken cancellationToken = default);
```

`GetTotalProcessedDeckCountAsync` already exists and serves as the total count for pagination. No second count method is needed.

**New model record (add to `HarvestStatsModels.cs` or a new file):**

```csharp
// Immutable DTO — preserve { get; init; } per CLAUDE.md
public sealed record HarvestedDeckRow(
    string DeckId,
    string? CommanderName,
    string InsertedUtc,
    string? LastCheckedUtc);
```

**View model additions to `AdminHarvestViewModel`:**

```csharp
// New fields on AdminHarvestViewModel (preserve { get; init; })
public IReadOnlyList<HarvestedDeckRow> HarvestedDecks { get; init; } = Array.Empty<HarvestedDeckRow>();
public int DeckPage { get; init; } = 1;
public int DeckPageSize { get; init; } = 50;
public int DeckTotalCount { get; init; }
public int DeckTotalPages => (int)Math.Ceiling((double)Math.Max(DeckTotalCount, 1) / Math.Max(DeckPageSize, 1));
```

---

## AdminHarvestController.Index — Hook-in Point

Current signature: `public async Task<IActionResult> Index(CancellationToken cancellationToken)`

New signature: `public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)`

Add after stats fetch:

```csharp
page = Math.Max(page, 1);
const int deckPageSize = 50;
var pagedDecks = await _categoryStore.GetPagedProcessedDecksAsync(page, deckPageSize, cancellationToken);
var deckTotal  = await _categoryStore.GetTotalProcessedDeckCountAsync(cancellationToken);
```

Add to `AdminHarvestViewModel` initializer: `HarvestedDecks = pagedDecks, DeckPage = page, DeckPageSize = deckPageSize, DeckTotalCount = deckTotal`.

**Note:** `GetTotalProcessedDeckCountAsync` is already called by `HarvestStatsAggregator.BuildAsync` for the warm-cache Stats panel. The controller will call it a second time for pagination on a cold cache. This is acceptable (one extra fast indexed COUNT). On a warm cache, the stats payload already has `TotalDecks` — the planner could optionally use `stats?.TotalDecks ?? deckTotal` to avoid the second call, but this adds complexity. Recommended: keep the calls separate for clarity.

---

## AdminHarvest/Index.cshtml — Top-10 List Replacement

The existing "Top 10 Commanders" section (lines 188-203) is a `<ul>` rendered when `Model.Stats is not null`. This section should be **removed** and replaced with the new paged-decks section. The paged-decks panel should:

- Use `<section class="admin-harvest__panel">` (same pattern as the existing panels)
- Contain `<h2>Harvested Decks</h2>` + total count + `<div class="admin-table-scroll" role="region" aria-label="Harvested decks - scroll horizontally to see all columns" tabindex="0">`
- Inside: `<table class="admin-table">` with `<th scope="col">` for Deck ID, Commander, Harvested, Last Checked
- Below: pagination nav

---

## Common Pitfalls

### Pitfall 1: Adding index DDL outside EnsureSchemaAsync

**What goes wrong:** If index DDL is placed in a one-time migration that doesn't re-execute on deploy, new environments (fresh Render DB, dev SQLite) won't get the indexes.
**Why it happens:** Temptation to write a separate migration method.
**How to avoid:** Add `CREATE INDEX IF NOT EXISTS` at the end of the existing `EnsureSchemaAsync` method in `CategoryKnowledgeRepository`. It runs on every startup (idempotent). Mirrors exactly how `HarvestRunStore` does it.
**Warning signs:** Admin page is still slow after deploy — check `\d deck_queue` in psql to verify indexes were created.

### Pitfall 2: Loading all harvested decks before paging

**What goes wrong:** `SELECT * FROM deck_queue WHERE processed = 1` with no LIMIT returns all rows. At scale (tens of thousands) this exhausts the 512MB Render heap.
**Why it happens:** Forgetting to pass `@limit`/`@offset` parameters.
**How to avoid:** Always pass both parameters. Verify with a test that `GetPagedProcessedDecksAsync(page: 1, pageSize: 50)` issues a query containing `LIMIT 50 OFFSET 0`.

### Pitfall 3: { get; init; } → { get; } mutation on new records

**What goes wrong:** `System.Text.Json` silently skips get-only properties in .NET 9+. Already broke `EdhTop16Client` deserialization.
**Why it happens:** IDE formatter / ReSharper auto-conversion.
**How to avoid:** Per CLAUDE.md R-6: never auto-convert `{ get; init; }` on any property. Every new `sealed record` must use `{ get; init; }`.
**Warning signs:** `HarvestedDeckRow` properties serialize as null in any JSON context.

### Pitfall 4: Touching more lines than necessary in CategoryKnowledgeRepository.cs

**What goes wrong:** Auto-formatter rewrites the raw-string SQL literals, changing indentation and breaking byte-preservation.
**Why it happens:** "Format Document" on save.
**How to avoid:** Per CLAUDE.md R-6: touch only the lines that need touching. Add the index DDL as new statements; do not reformat surrounding code.

### Pitfall 5: Parallelizing queries that share a SQLite connection

**What goes wrong:** SQLite's write lock is per-connection. If parallel tasks share one `DbConnection` instance, concurrent reads on the same connection can serialize or fail.
**Why it happens:** Misreading `Task.WhenAll` as needing a shared connection.
**How to avoid:** Each `CategoryKnowledgeStore` stat method already opens its own `DbConnection` via `OpenConnectionAsync()` (confirmed in codebase). `Task.WhenAll` is safe because each task has its own connection. Do not share connections across tasks.

### Pitfall 6: page=0 or negative from query string

**What goes wrong:** OFFSET = (0-1) * 50 = -50 → SQL error on Postgres.
**Why it happens:** No clamping on the raw query string param.
**How to avoid:** `page = Math.Max(page, 1);` as the first line of the action — mirrors `AdminFeedbackController.cs:59`.

### Pitfall 7: reltuples returning -1

**What goes wrong:** `pg_class.reltuples` is -1 when the table has never been analyzed (fresh DB). The UI shows -1 observations.
**Why it happens:** `ANALYZE` hasn't run yet on a new deployment.
**How to avoid:** Guard: `if (reltuples <= 0) fall through to COUNT(1)`. Only use the estimate when `reltuples > 0`.

---

## Code Examples

### GetPagedProcessedDecksAsync — Repository Method

```csharp
// To add in CategoryKnowledgeRepository.cs — verified column names from EnsureSchemaAsync
public async Task<IReadOnlyList<HarvestedDeckRow>> GetPagedProcessedDecksAsync(
    int page, int pageSize, CancellationToken cancellationToken = default)
{
    await EnsureSchemaAsync(cancellationToken);
    await using var connection = CreateConnection();
    await connection.OpenAsync(cancellationToken);

    var command = connection.CreateCommand();
    command.CommandText = """
        SELECT deck_id, commander_name, inserted_utc, last_checked_utc
        FROM deck_queue
        WHERE processed = 1
        ORDER BY inserted_utc DESC
        LIMIT @limit OFFSET @offset;
        """;
    RelationalDatabaseConnection.AddParameter(command, "@limit", pageSize);
    RelationalDatabaseConnection.AddParameter(command, "@offset", (page - 1) * pageSize);

    var rows = new List<HarvestedDeckRow>(capacity: pageSize);
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        rows.Add(new HarvestedDeckRow(
            DeckId:         reader.GetString(0),
            CommanderName:  reader.IsDBNull(1) ? null : reader.GetString(1),
            InsertedUtc:    reader.GetString(2),
            LastCheckedUtc: reader.IsDBNull(3) ? null : reader.GetString(3)));
    }
    return rows;
}
```

### Task.WhenAll in BuildAsync

```csharp
// Replacement for the 7 sequential awaits in HarvestStatsAggregator.BuildAsync
var totalDecksTask        = _categoryStore.GetTotalProcessedDeckCountAsync(cancellationToken);
var totalDecks30dTask     = _categoryStore.GetTotalProcessedDeckCountSinceAsync(
                                DateTime.UtcNow.AddDays(-30), cancellationToken);
var totalObservationsTask = _categoryStore.GetTotalObservationCountAsync(cancellationToken);
var topCommandersTask     = _categoryStore.GetTopCommandersAsync(10, cancellationToken);
var recentRunsTask        = _runStore.GetRecentAsync(10, cancellationToken);
var postgresStorageTask   = _categoryStore.GetPostgresDatabaseSizeBytesAsync(cancellationToken);
var lastSuccessTask       = _runStore.GetLastSuccessUtcAsync(cancellationToken);

await Task.WhenAll(totalDecksTask, totalDecksTask30d, totalObservationsTask,
    topCommandersTask, recentRunsTask, postgresStorageTask, lastSuccessTask);

// Then use .Result on each completed task
```

### Paged Grid Razor Section

```razor
@* To add in AdminHarvest/Index.cshtml — mirrors existing HarvestRuns table pattern (lines 124-149) *@
<section class="admin-harvest__panel">
    <h2>Harvested Decks</h2>
    <p>@Model.DeckTotalCount total — Page @Model.DeckPage of @Model.DeckTotalPages</p>
    @if (Model.HarvestedDecks.Count == 0)
    {
        <p>No harvested decks yet.</p>
    }
    else
    {
        <div class="admin-table-scroll" role="region"
             aria-label="Harvested decks - scroll horizontally to see all columns" tabindex="0">
            <table class="admin-table">
                <caption class="sr-only">All harvested decks with deck ID, commander, and harvest dates</caption>
                <thead>
                    <tr>
                        <th scope="col">Deck ID</th>
                        <th scope="col">Commander</th>
                        <th scope="col">Harvested (UTC)</th>
                        <th scope="col">Last Checked (UTC)</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var deck in Model.HarvestedDecks)
                    {
                        <tr>
                            <td>@deck.DeckId</td>
                            <td>@(deck.CommanderName ?? "—")</td>
                            <td>@deck.InsertedUtc</td>
                            <td>@(deck.LastCheckedUtc ?? "—")</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
        <nav class="admin-feedback-pagination">
            @if (Model.DeckPage > 1)
            {
                <a asp-action="Index" asp-route-page="@(Model.DeckPage - 1)">Prev</a>
            }
            <span>Page @Model.DeckPage of @Model.DeckTotalPages</span>
            @if (Model.DeckPage < Model.DeckTotalPages)
            {
                <a asp-action="Index" asp-route-page="@(Model.DeckPage + 1)">Next</a>
            }
        </nav>
    }
</section>
```

---

## Testing

### Test Project

`DeckFlow.Web.Tests` — covers `CategoryKnowledgeStore`, `AdminHarvestController` area, and `HarvestStatsAggregator` indirectly via `FakeCategoryKnowledgeStore`.

`DeckFlow.Core.Tests` — covers `CategoryKnowledgeRepository` (SQLite in-memory). File: `CategoryKnowledgeRepositoryTests.cs`.

### Existing Test Patterns

**`CategoryKnowledgeStoreTests.cs`** — uses a `FakeWebHostEnvironment` pointing to a temp directory; no `[Fact]` that calls the DB (tests are argument-validation only at the `CategoryKnowledgeStore` wrapper level). This means the paged-query SQL lives in `CategoryKnowledgeRepository` and should be tested in `CategoryKnowledgeRepositoryTests.cs` (in-memory SQLite).

**`FakeCategoryKnowledgeStore.cs`** — implements every `ICategoryKnowledgeStore` member with stub returns. The new `GetPagedProcessedDecksAsync` method must be added here to keep compilation green. Return `Array.Empty<HarvestedDeckRow>()` by default; add a configurable `PagedDecksResult` property for controller tests.

**`CategoryKnowledgeRepositoryTests.cs`** — existing tests use in-memory SQLite (`:memory:` pattern per F-PROD-CONTRACT). New test: seed N rows with `processed=1`, call `GetPagedProcessedDecksAsync(page:1, pageSize:2)`, assert count=2 and correct rows returned. Also test page 2 offset.

**`HarvestStatsAggregator` tests** — no existing test file found for `HarvestStatsAggregator`. The planner may scope a new `HarvestStatsAggregatorTests.cs` that injects `FakeCategoryKnowledgeStore` and `FakeHarvestRunStore` to verify that `BuildAsync` (via `GetAsync` with an empty cache) calls all expected methods. However, since the class is sealed and has no `internal` test ctor, testing would require a real `IMemoryCache`. This is LOW priority — focus new tests on the repository layer.

### WSL VSTest

Per CLAUDE.md: VSTest is unreliable in WSL. Verification path: `dotnet build` clean + push to `v1.4` branch + observe CI. Targeted local verification: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -v minimal`.

---

## Validation Architecture

> `workflow.nyquist_validation` key not found in `.planning/config.json` — treat as enabled.

### Test Framework

| Property | Value |
|---|---|
| Framework | xUnit 2.9.3 |
| Config file | none (implicit) |
| Quick run | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -v minimal` |
| Full suite | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -v minimal` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|---|---|---|---|---|
| AHD-01 (paged grid) | `GetPagedProcessedDecksAsync(page,size)` returns correct LIMIT/OFFSET slice | unit | `dotnet test DeckFlow.Core.Tests -v minimal --filter GetPagedProcessed` | ❌ Wave 0 |
| AHD-01 (no full load) | Page 1 of 50 from 10k rows returns exactly 50 rows, not 10k | unit | same | ❌ Wave 0 |
| AHD-01 (total count) | `GetTotalProcessedDeckCountAsync` returns correct count for pagination math | unit | existing (already tested via `GetProcessedDeckCountAsync`) | ✅ (partial) |
| AHD-01 (perf fix) | `BuildAsync` calls all 7 store methods (via fake) | unit | `dotnet test DeckFlow.Web.Tests -v minimal --filter HarvestStats` | ❌ Wave 0 (optional) |
| AHD-01 (index DDL) | `EnsureSchemaAsync` creates 3 new indexes | unit | in-memory SQLite pragma query | ❌ Wave 0 |

### Wave 0 Gaps

- [ ] `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` — extend with `GetPagedProcessedDecksAsync` tests (page 1, page 2, empty result) and index-existence assertion
- [ ] `DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs` — add `GetPagedProcessedDecksAsync` stub

---

## Environment Availability

This phase is code/config/SQL changes only. No external tools beyond the existing project runtime.

| Dependency | Required By | Available | Version | Fallback |
|---|---|---|---|---|
| .NET 10 SDK | Build + test | ✓ | 10.x (confirmed by existing build) | — |
| SQLite (in-proc) | Local dev + tests | ✓ | via `Microsoft.Data.Sqlite` | — |
| Postgres | Render prod + integration tests | ✓ (Render) | 15+ | SQLite for local dev |

---

## Security Domain

> `security_enforcement` not explicitly set — treat as enabled.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---|---|---|
| V2 Authentication | No (admin gate already in place via `BasicAuthMiddleware`) | existing |
| V3 Session Management | No | — |
| V4 Access Control | No (all endpoints already behind `/Admin` BasicAuth branch) | existing |
| V5 Input Validation | Yes — `int page` query param | `Math.Max(page, 1)` clamping; no SQL injection risk (parameterized query) |
| V6 Cryptography | No | — |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---|---|---|
| SQL injection via page param | Tampering | Parameterized `@limit` / `@offset` via `RelationalDatabaseConnection.AddParameter` |
| Large OFFSET DoS (page=999999) | Denial of Service | OFFSET = (page-1)*50; at 999999 this is 49,999,950 — Postgres OFFSET scans are O(offset). Mitigation: clamp `page` to `Math.Min(page, DeckTotalPages)` in the controller. |

---

## State of the Art

| Old Approach | Current Approach | Notes |
|---|---|---|
| Top-10 commanders list (GROUP BY in stats) | Removed; replaced by paged deck grid | The `GetTopCommandersAsync(10)` call stays in BuildAsync for the Stats panel — only the Razor rendering of the top-10 list moves to the paged grid section |
| 7 sequential awaits in BuildAsync | Task.WhenAll all 7 | Pure implementation change; no interface change |
| No indexes on deck_queue | 3 composite indexes via CREATE INDEX IF NOT EXISTS | Applied in EnsureSchemaAsync; idempotent |

---

## Project Constraints (from CLAUDE.md)

- **{ get; init; } preserved** — every new record type must use `{ get; init; }`, not `{ get; }`. System.Text.Json silently skips get-only props.
- **Touch-only-what-you-touch** — no Format Document, no reformatting of raw-string SQL literals, no attribute inlining.
- **No new packages** — ask user first; this phase needs none.
- **Commits: plain default-author** — no Co-Authored-By trailers.
- **Codex implements, Claude reviews** — implementation dispatched to Codex; Claude plans and reviews.
- **No lockfile changes** — `package-lock.json` etc. are off-limits unless explicitly approved.
- **Layout CSS in admin-common.css** — any new pagination nav CSS goes in `admin-common.css` under `.admin-shell`, not in `admin.css` or `site.css`.
- **Admin CSS scoped to `.admin-shell`** — zero unscoped element selectors.
- **VSTest unreliable in WSL** — verify via `dotnet build` clean + CI push.
- **Render 512MB cap** — server-side paging is mandatory; never load all `deck_queue` rows.
- **dual-dialect SQLite + Postgres** — all SQL must work on both; `CREATE INDEX IF NOT EXISTS` is identical for both.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|---|---|---|
| A1 | `Task.WhenAll` on 7 concurrent `CategoryKnowledgeStore` calls is safe under the Postgres connection pool cap of 10-15 | Pattern 3 | Pool exhaustion during concurrent admin page views + harvest — unlikely but monitor |
| A2 | `reltuples <= 0` guard handles the fresh-DB case for Postgres observation count estimate | Pattern 4 | UI shows 0 observations on fresh Render deploy (not -1, which would be confusing) |
| A3 | Re-using `.admin-feedback-pagination` CSS class in the paged grid nav is acceptable (no new class needed) | Pattern 5 | Semantic mismatch if admin-feedback styles are ever split; low risk |
| A4 | `GetTotalProcessedDeckCountAsync` (already on the interface) is sufficient for the paged grid total; no new count method needed | ICategoryKnowledgeStore section | Correct — verified method exists |
| A5 | `HarvestStatsAggregator` has no existing xUnit tests | Testing section | If a test file exists that was missed, the planner will find it |

---

## Open Questions

1. **Pagination class name for the new nav**
   - What we know: `.admin-feedback-pagination` is defined in `admin-common.css` under `.admin-shell`; it works for any `<nav>` containing Prev/span/Next.
   - What's unclear: Should the planner reuse `.admin-feedback-pagination` verbatim, or add a generic `.admin-pagination` alias?
   - Recommendation: Add `.admin-shell .admin-pagination { margin-top: 1rem; display: flex; gap: 0.75rem; align-items: center; }` alongside the feedback rule; use `.admin-pagination` in the harvest view and optionally alias feedback to it. Minimal CSS delta.

2. **Whether to remove the "Top 10 Commanders" list entirely**
   - What we know: AHD-01 says replace the top-ten list with a paged grid. The paged grid rows include `commander_name`.
   - What's unclear: The Stats panel still shows `topCommanders` from `BuildAsync`. Does the user want the stats panel's top-commanders list completely gone, or just the standalone `<ul>` list?
   - Recommendation: Remove the standalone `<h3>Top 10 Commanders</h3>` + `<ul>` from the Stats panel (lines 188-203 of the current view). The paged grid replaces it. The planner should confirm with the ROADMAP spec — it says "replaces the current top-ten-decks list", confirming removal.

3. **Page size constant**
   - What we know: `AdminFeedback` uses `const int pageSize = 50`. With thousands of harvested decks, 50 rows/page is reasonable.
   - Recommendation: Use 50 as the default. Add to `AdminHarvestViewModel` as a static constant (mirrors `AllowedDurationSeconds` pattern) for testability.

---

## Sources

### Primary (HIGH confidence — verified in codebase)

- `DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs` — BuildAsync 7 sequential queries, 60s cache
- `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` — stat methods, EnsureSchemaAsync, OpenConnectionAsync pattern
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — EnsureSchemaAsync, deck_queue schema, no existing indexes
- `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` — Index action shape, view model assembly
- `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs` — canonical paging pattern (page param, pageSize, TotalPages)
- `DeckFlow.Web/Services/FeedbackStore.cs` — LIMIT/OFFSET SQL pattern, AddParameter usage
- `DeckFlow.Web/Views/AdminFeedback/Index.cshtml` — pagination nav Razor markup
- `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` — existing admin-table-scroll table pattern
- `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` — CREATE INDEX IF NOT EXISTS dual-dialect pattern
- `DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs` — HarvestStatsPayload, TopCommanderRow records
- `DeckFlow.Web/Services/ICategoryKnowledgeStore.cs` — full interface surface
- `DeckFlow.Web/wwwroot/css/admin-common.css` — admin-table-scroll, admin-table, admin-feedback-pagination CSS
- `DeckFlow.Web/wwwroot/css/admin-mobile.css` — admin-table--card card-stack pattern, 768px breakpoint
- `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml` — admin shell markup, sidebar nav
- `DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs` — view model shape
- `DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs` — stub pattern
- `DeckFlow.Web.Tests/CategoryKnowledgeStoreTests.cs` — test isolation pattern

### Secondary (MEDIUM confidence)

- ROADMAP.md Phase 25 Perf Investigation Note — named files + fix directions (project-authored, authoritative)
- REQUIREMENTS.md AHD-01 — requirement text

### Tertiary (LOW confidence / ASSUMED)

- A1-A5 in Assumptions Log above

---

## Metadata

**Confidence breakdown:**

- Standard stack: HIGH — no new packages; all patterns verified in codebase
- Architecture: HIGH — all hook-in points located and verified
- Pitfalls: HIGH — all derived from actual codebase evidence (CLAUDE.md formatting rules, existing patterns)
- Perf fix: HIGH — 7 queries verified in BuildAsync source; index absence confirmed by grep

**Research date:** 2026-05-24
**Valid until:** 2026-06-24 (stable brownfield; only invalidated by other phases touching CategoryKnowledgeRepository or AdminHarvestController)
