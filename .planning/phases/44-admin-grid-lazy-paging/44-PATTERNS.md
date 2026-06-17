# Phase 44: Admin Grid Lazy Paging - Pattern Map

**Mapped:** 2026-06-13
**Files analyzed:** 7 new/modified files
**Analogs found:** 7 / 7

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` | controller | request-response | Self (existing `Status` action lines 122-150) | exact — same file, same guard pattern |
| `DeckFlow.Web/Views/AdminHarvest/_CommandersGrid.cshtml` | component (Razor partial) | request-response | `Views/AdminHarvest/Index.cshtml` lines 218-269 | exact — extract-in-place |
| `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` | component (Razor view) | request-response | Self (existing grid section lines 218-269) | exact — replace interior with placeholder |
| `DeckFlow.Web/Models/Admin/CommandersGridViewModel.cs` | model | — | `DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs` | exact — extract paging fields only |
| `DeckFlow.Web/wwwroot/ts/admin-harvest.ts` | utility (client script) | request-response | Self (existing `fetchStatus` function lines 92-111) | exact — extend IIFE with second fetch path |
| `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` | service | CRUD | Self (existing `indexCommand` block lines 106-136) | exact — same DDL pattern, modify in place |
| `DeckFlow.Web.Tests/AdminHarvestControllerTests.cs` | test | — | Self (existing tests lines 1-191) + `AdminContentKbControllerTests.cs` lines 282-303 | exact — same stub builder + cross-origin header pattern |

---

## Pattern Assignments

### `AdminHarvestController.cs` — new `Commanders` action (controller, request-response)

**Analog:** Same file, `Status` action (lines 122-150) for the SameOrigin guard; `Index` action (lines 73-116) for the paging math.

**Imports pattern** (lines 1-11 — no new using directives needed):
```csharp
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
```

**SameOrigin 403 guard pattern** (lines 125-129 — copy verbatim from `Status`):
```csharp
if (!SameOriginRequestValidator.IsValid(Request))
{
    return StatusCode(
        StatusCodes.Status403Forbidden,
        new { Message = "This endpoint only accepts same-origin browser requests." });
}
```

**Paging math pattern** (lines 93-100 of `Index` — replicate in `Commanders`):
```csharp
page = Math.Max(page, 1);
const int pageSize = AdminHarvestViewModel.DefaultDeckPageSize;
var deckTotal = await _categoryStore.GetDistinctProcessedCommanderCountAsync(cancellationToken).ConfigureAwait(false);
var deckTotalPages = (int)Math.Ceiling((double)Math.Max(deckTotal, 1) / Math.Max(pageSize, 1));
page = Math.Min(page, deckTotalPages);
var pagedCommanders = await _categoryStore.GetPagedProcessedCommandersAsync(page, pageSize, cancellationToken).ConfigureAwait(false);
```

**PartialView return pattern** (first use of `PartialView()` in DeckFlow.Web; contrast with existing `View(viewModel)` at line 115):
```csharp
// Existing full-view return (do NOT use for the partial endpoint):
return View(viewModel);

// New pattern for the commanders partial endpoint:
return PartialView("_CommandersGrid", model);
```

**`Index` action stripping** — remove lines 97-100 (the two query calls and their paging math) and the `HarvestedCommanders`/`DeckPage`/`DeckPageSize`/`DeckTotalCount` assignments from the `AdminHarvestViewModel` initializer at lines 106-109. Also remove the `page` parameter from `Index`'s method signature (line 74) since paging moves entirely to the `Commanders` action.

**HttpGet attribute pattern** (line 73 / 122 — same `[Route]` on the class, `[HttpGet("...")]` on each action):
```csharp
[Route("Admin/Harvest")]          // on the class (line 17)
// ...
[HttpGet("")]                     // Index (line 73)
[HttpGet("status")]               // Status (line 122)
[HttpGet("commanders")]           // NEW Commanders action — same convention
```

**CancellationToken pattern** (all async actions take it as last optional param):
```csharp
public async Task<IActionResult> Commanders(int page = 1, CancellationToken cancellationToken = default)
```

---

### `Views/AdminHarvest/_CommandersGrid.cshtml` — new Razor partial (component, request-response)

**Analog:** `Views/AdminHarvest/Index.cshtml` lines 218-269 — the grid section being extracted verbatim, then extended with numbered pagination.

**Partial model declaration** (new pattern for this codebase — no existing partials with `@model`):
```razor
@model DeckFlow.Web.Models.Admin.CommandersGridViewModel
```

**Grid markup to extract verbatim** (lines 218-269 of `Index.cshtml`):
- Page count line: `<p>@Model.DeckTotalCount commanders - Page @Model.DeckPage of @Model.DeckTotalPages</p>` — upgrade to `admin-harvest__grid-meta` class and em-dash separator per UI-SPEC
- Empty state: `<p>No harvested commanders yet.</p>` — add `admin-empty` class (already in admin-common.css:335)
- Table: `<div class="admin-table-scroll" role="region" aria-label="..." tabindex="0">` + `<table class="admin-table">` — copy verbatim
- `<caption class="sr-only">Harvested commanders with deck count and last processed timestamp</caption>` — copy verbatim
- Rank offset: `var rankOffset = (Model.DeckPage - 1) * Model.DeckPageSize;` — copy verbatim
- Row loop: `@for (var i = 0; i < Model.HarvestedCommanders.Count; i++)` — copy verbatim

**FormatLastProcessedUtc local function** — re-declare at the top of the partial (it is currently a local function in `Index.cshtml`; Razor local functions do not cross file boundaries). Read the existing declaration in `Index.cshtml` before line 218 to copy its exact signature.

**Numbered pagination** (replaces the prev/next-only `<nav>` at lines 257-268 of `Index.cshtml`):
```razor
@* Existing prev/next nav (Index.cshtml:257-268) — replace with numbered window in partial: *@
<nav class="admin-feedback-pagination admin-harvest__grid-pager"
     aria-label="Commander grid pagination">
    @* Razor computes window: current ±2, ellipsis for gaps, ≤7 pages show all *@
    @* data-page attrs on <a> and <strong aria-current="page"> for TS delegation *@
</nav>
```
Note: `admin-feedback-pagination` (admin-common.css:357) already has `display: flex; gap: 0.75rem; align-items: center` and the 44px touch-target rule (lines 402-417). No new CSS for the `<nav>` itself.

**Pagination data-page attribute pattern** (TypeScript delegation target):
```html
<a href="#" data-page="2">2</a>          <!-- clickable page link -->
<strong aria-current="page">3</strong>   <!-- current page, not an <a> -->
```

---

### `Views/AdminHarvest/Index.cshtml` — grid section replacement (component, request-response)

**Analog:** The existing section (lines 218-269) — the interior is replaced; only the outer shell changes.

**Placeholder pattern** (replaces lines 219-269 interior — keep `<section id="harvested-commanders">` wrapper at line 218):
```html
<section id="harvested-commanders" class="admin-harvest__panel">
  <h2>Harvested Commanders</h2>
  <div id="commanders-grid-container"
       aria-live="polite"
       aria-busy="true"
       aria-label="Harvested commanders grid">
    <p class="admin-harvest__grid-loading">Loading commanders…</p>
  </div>
</section>
```

**Scripts section** (line 272-274 — no change, `admin-harvest.js` is already referenced):
```razor
@section Scripts
{
    <script src="~/js/admin-harvest.js" asp-append-version="true"></script>
}
```

---

### `Models/Admin/CommandersGridViewModel.cs` — new slim view model (model)

**Analog:** `DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs` — extract only the four paging fields + the computed `DeckTotalPages` property.

**Class structure pattern** (lines 9-54 of `AdminHarvestViewModel.cs` — extract and slim):
```csharp
// AdminHarvestViewModel.cs:9 — sealed record with init properties
public sealed record AdminHarvestViewModel
{
    public const int DefaultDeckPageSize = 25;                         // line 12

    public IReadOnlyList<HarvestedCommanderRow> HarvestedCommanders   // line 29
        { get; init; } = Array.Empty<HarvestedCommanderRow>();

    public int DeckPage { get; init; } = 1;                           // line 32
    public int DeckPageSize { get; init; } = DefaultDeckPageSize;     // line 35
    public int DeckTotalCount { get; init; }                          // line 38

    // CRITICAL: computed { get; } only — NOT { get; init; }
    // Why: System.Text.Json silently skips get-only properties in .NET 9+;
    // this is Razor-only so serialization is irrelevant. Formula from line 41:
    public int DeckTotalPages =>
        (int)Math.Ceiling((double)Math.Max(DeckTotalCount, 1) / Math.Max(DeckPageSize, 1));
}
```

**New file `CommandersGridViewModel.cs` — extracts only these fields** (do not copy `ActiveRun`, `RecentRuns`, `Schedule`, `Stats`, `LastBanner`, `IntervalOptions`, `DurationOptions`).

**Namespace:** `DeckFlow.Web.Models.Admin` (file-scoped, matches `AdminHarvestViewModel.cs:3`).

**Using directives** (copy from `AdminHarvestViewModel.cs:1`):
```csharp
using DeckFlow.Web.Services.Harvest;   // for HarvestedCommanderRow
```
Also needs reference to `AdminHarvestViewModel.DefaultDeckPageSize` — either reference across the namespace (they share `DeckFlow.Web.Models.Admin`) or inline the constant value `25`.

---

### `wwwroot/ts/admin-harvest.ts` — extend IIFE with commander-grid fetch (utility, request-response)

**Analog:** `fetchStatus` function (lines 92-111) — same fetch pattern, same IIFE scope, different Accept header and response handler.

**IIFE constraint** (lines 1 / 183): all new code goes INSIDE the existing `((): void => { ... })()` block. The `module: "none"` tsconfig means inner functions are not accessible outside.

**fetchStatus pattern** (lines 92-111 — adapt for HTML response):
```typescript
// EXISTING (lines 92-111) — JSON response:
const fetchStatus = async (): Promise<HarvestStatusPayload | null> => {
  const abortController = new AbortController();
  const timeoutId = window.setTimeout(() => abortController.abort(), FETCH_TIMEOUT_MS);
  try {
    const response = await fetch('/Admin/Harvest/status', {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
      signal: abortController.signal
    });
    if (!response.ok) { return null; }
    return await response.json() as HarvestStatusPayload;
  } finally {
    window.clearTimeout(timeoutId);
  }
};

// NEW pattern (adapt for text/html response — add after line 111, inside IIFE):
const COMMANDERS_FETCH_TIMEOUT_MS = 10000;

const fetchCommandersGrid = async (page: number): Promise<string | null> => {
  const abortController = new AbortController();
  const timeoutId = window.setTimeout(() => abortController.abort(), COMMANDERS_FETCH_TIMEOUT_MS);
  try {
    const response = await fetch(`/Admin/Harvest/commanders?page=${page}`, {
      credentials: 'same-origin',
      headers: { Accept: 'text/html' },
      signal: abortController.signal
    });
    if (!response.ok) { return null; }
    return await response.text();   // innerHTML swap target
  } finally {
    window.clearTimeout(timeoutId);
  }
};
```

**DOMContentLoaded handler extension pattern** (lines 113-182 — add second branch in the same handler):
```typescript
// EXISTING handler starts at line 113:
document.addEventListener('DOMContentLoaded', () => {
  // existing: finds #harvest-status-live and starts polling loop (lines 114-181)

  // NEW: second branch for commander grid (add after existing root-guard):
  const gridContainer = document.querySelector<HTMLElement>('#commanders-grid-container');
  if (gridContainer) {
    // Event delegation — attach once on stable parent (survives innerHTML swaps):
    gridContainer.addEventListener('click', (e) => {
      const target = (e.target as HTMLElement).closest<HTMLElement>('[data-page]');
      if (!target) { return; }
      e.preventDefault();
      const page = parseInt(target.dataset.page ?? '1', 10);
      void loadCommandersGrid(gridContainer, page);
    });
    // Auto-fire on load (SC2):
    void loadCommandersGrid(gridContainer, 1);
  }
});
```

**Loading state + innerHTML swap pattern** (new, consistent with existing `render()` + `renderFallback()` style):
```typescript
const COMMANDERS_LOADING_HTML =
  '<p class="admin-harvest__grid-loading">Loading commanders…</p>';

const COMMANDERS_ERROR_HTML =
  '<p class="admin-harvest__grid-error">Could not load commanders. ' +
  '<a href="#" id="commanders-retry">Retry</a></p>';

const loadCommandersGrid = async (container: HTMLElement, page: number): Promise<void> => {
  container.setAttribute('aria-busy', 'true');
  container.innerHTML = COMMANDERS_LOADING_HTML;
  const html = await fetchCommandersGrid(page);
  if (html === null) {
    container.innerHTML = COMMANDERS_ERROR_HTML;
    container.setAttribute('aria-busy', 'false');
    const retry = container.querySelector<HTMLElement>('#commanders-retry');
    retry?.addEventListener('click', (e) => {
      e.preventDefault();
      void loadCommandersGrid(container, page);
    });
    return;
  }
  container.innerHTML = html;
  container.setAttribute('aria-busy', 'false');
  // Scroll into view with prefers-reduced-motion guard (UI-SPEC §Interaction Contract step 7):
  const section = document.getElementById('harvested-commanders');
  if (section) {
    const prefersReduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    section.scrollIntoView({ behavior: prefersReduced ? 'instant' : 'smooth', block: 'start' });
  }
};
```

**No import/export** — `module: "none"` means all symbols are inner-IIFE. Never add `import` or `export` to this file.

---

### `CategoryKnowledgeRepository.cs` — EnsureSchemaAsync index consolidation (service, CRUD)

**Analog:** Same file, `indexCommand` block (lines 106-136) — same multi-statement raw-string-literal DDL pattern, same try/catch swallow.

**Existing index block structure** (lines 106-136 — the block to modify):
```csharp
var indexCommand = connection.CreateCommand();
indexCommand.CommandText = """
    CREATE UNIQUE INDEX IF NOT EXISTS ux_cards_normalized ON cards(normalized_card_name);
    ...
    CREATE INDEX IF NOT EXISTS ix_deck_queue_processed ON deck_queue(processed);
    CREATE INDEX IF NOT EXISTS ix_deck_queue_processed_inserted_deck ON deck_queue(processed, inserted_utc, deck_id);
    CREATE INDEX IF NOT EXISTS ix_deck_queue_processed_commander ON deck_queue(processed, commander_name);           // line 114 — DROP this
    CREATE INDEX IF NOT EXISTS ix_deck_queue_processed_commander_lower ON deck_queue(processed, LOWER(commander_name)); // line 115 — DROP this
    CREATE UNIQUE INDEX IF NOT EXISTS ux_obs_grain ON card_category_observations(...);
    ...
    """;
indexCommand.CommandTimeout = 15;
// Why: indexes are startup optimizations ... (lines 125-126 comment — preserve)
try
{
    await indexCommand.ExecuteNonQueryAsync(cancellationToken);
}
catch (Exception exception) when (exception is DbException or OperationCanceledException or TimeoutException)
{
    _logger?.LogWarning(...);     // lines 132-135 — preserve verbatim
}
```

**Index consolidation DDL** (replace lines 114-115 in the raw string literal; both SQLite and Postgres support identical syntax — no dialect branch needed):
```sql
-- DROP the two redundant composite indexes (add before remaining CREATEs):
DROP INDEX IF EXISTS ix_deck_queue_processed_commander;
DROP INDEX IF EXISTS ix_deck_queue_processed_commander_lower;
-- ADD the single partial expression index that SC3 specifies:
CREATE INDEX IF NOT EXISTS ix_deck_queue_commander_lower_processed
    ON deck_queue(LOWER(commander_name)) WHERE processed = 1;
```

**Raw-string-literal preservation** — the index block uses a C# raw string literal (`""" ... """`). Do not change indentation of the literal content (the CLAUDE.md constraint: "never re-indent C# raw-string literals — changes the literal value").

**Dual-dialect note** — `DROP INDEX IF EXISTS <name>` syntax is identical in SQLite and Postgres (no table qualifier needed for either). No `IsSqlite` branch required for the DROP statements, matching the existing pattern where all 14 `CREATE INDEX` statements in the block run against both dialects without branching.

**EXPLAIN verification** (D-09 — Wave 0 task before plan locks DDL): run against local SQLite KB DB:
```sql
EXPLAIN QUERY PLAN
SELECT COUNT(DISTINCT LOWER(commander_name)) FROM deck_queue WHERE processed = 1 AND commander_name IS NOT NULL;

EXPLAIN QUERY PLAN
SELECT LOWER(commander_name), COUNT(*) as deck_count, MAX(last_checked_utc) as last_processed_utc
FROM deck_queue WHERE processed = 1 AND commander_name IS NOT NULL
GROUP BY LOWER(commander_name) ORDER BY deck_count DESC LIMIT 25 OFFSET 0;
```
Expected: `SEARCH deck_queue USING INDEX ix_deck_queue_commander_lower_processed` (not `SCAN TABLE`).

---

### `DeckFlow.Web.Tests/AdminHarvestControllerTests.cs` — new test cases (test)

**Analog:** Same file (lines 1-191) for builder/stub pattern; `AdminContentKbControllerTests.cs` (lines 282-303) for cross-origin header injection.

**Test builder pattern** (lines 75-92 — copy, then add `Origin` header for cross-origin tests):
```csharp
// EXISTING Build() (lines 75-92) — no Origin header set:
private static AdminHarvestController Build(ICategoryKnowledgeStore store)
{
    var httpContext = new DefaultHttpContext();
    return new AdminHarvestController(
        new StubArchidektCacheJobService(),
        new StubHarvestRunStore(),
        new StubHarvestScheduleStore(),
        new StubHarvestScheduleCache(),
        new StubHarvestStatsAggregator(),
        new StubArchidektDeckImporter(),
        store,
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<AdminHarvestController>.Instance)
    {
        ControllerContext = new ControllerContext { HttpContext = httpContext },
        TempData = new TempDataDictionary(httpContext, new StubTempDataProvider()),
    };
}
```

**Cross-origin header pattern** (from `AdminContentKbControllerTests.cs:297-299`):
```csharp
// AdminContentKbControllerTests.cs:297-299 — the template for SC4 test:
httpContext.Request.Scheme = "https";
httpContext.Request.Host = new HostString("deckflow.test");
httpContext.Request.Headers.Origin = crossOrigin ? "https://evil.test" : "https://deckflow.test";
```

**New overload of Build()** for the `Commanders` action tests:
```csharp
private static AdminHarvestController Build(ICategoryKnowledgeStore store, bool crossOrigin)
{
    var httpContext = new DefaultHttpContext();
    httpContext.Request.Scheme = "https";
    httpContext.Request.Host = new HostString("deckflow.test");
    httpContext.Request.Headers.Origin = crossOrigin ? "https://evil.test" : "https://deckflow.test";
    // ... same controller construction as existing Build() ...
}
```

**FakeCategoryKnowledgeStore call-count properties** (lines 32-38, 100-108 — used in SC1 test):
```csharp
// FakeCategoryKnowledgeStore properties to assert call-count zero in SC1 test:
store.DistinctProcessedCommanderCount  // set via property initializer
store.PagedCommandersResult            // set via property initializer
store.LastPagedCommanderPage           // captures the page arg passed in
store.LastPagedCommanderPageSize       // captures the pageSize arg passed in
// NOTE: the fake does NOT expose a call-count for GetDistinctProcessedCommanderCountAsync.
// SC1 test must verify via LastPagedCommanderPage == 0 (default) after calling Index().
```

**PartialViewResult assertion pattern** (new — no existing example in the file):
```csharp
// Pattern for asserting PartialView return (SC2 test):
var partial = Assert.IsType<PartialViewResult>(result);
Assert.Equal("_CommandersGrid", partial.ViewName);
var model = Assert.IsType<CommandersGridViewModel>(partial.Model);
Assert.Equal(1, model.DeckPage);
```

**ObjectResult 403 assertion pattern** (from `AdminContentKbControllerTests.cs:267-271` — already used in project):
```csharp
// AssertForbidden helper pattern (AdminContentKbControllerTests.cs:267-271):
private static void AssertForbidden(IActionResult result)
{
    var obj = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
}
```

---

### `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` — update index test (test)

**Analog:** Same file, `EnsureSchemaAsync_CreatesDeckQueueIndexes` test (lines 276-287).

**Existing test to update** (lines 275-287 — the only test that must change in Wave 0):
```csharp
[Fact]
public async Task EnsureSchemaAsync_CreatesDeckQueueIndexes()
{
    var repository = CreateRepository();
    await repository.EnsureSchemaAsync();
    var indexNames = await GetDeckQueueIndexNamesAsync();
    Assert.Contains("ix_deck_queue_processed", indexNames);
    Assert.Contains("ix_deck_queue_processed_inserted_deck", indexNames);
    Assert.Contains("ix_deck_queue_processed_commander", indexNames);        // REMOVE (index dropped)
    Assert.Contains("ix_deck_queue_processed_commander_lower", indexNames);  // REMOVE (index dropped)
}
```

**Updated assertions** (same structure, swap the last two lines):
```csharp
Assert.Contains("ix_deck_queue_processed", indexNames);
Assert.Contains("ix_deck_queue_processed_inserted_deck", indexNames);
Assert.DoesNotContain("ix_deck_queue_processed_commander", indexNames);      // dropped by consolidation
Assert.DoesNotContain("ix_deck_queue_processed_commander_lower", indexNames); // dropped by consolidation
Assert.Contains("ix_deck_queue_commander_lower_processed", indexNames);       // new partial expression index
```

---

## Shared Patterns

### SameOrigin 403 guard
**Source:** `AdminHarvestController.cs:125-129` (copy verbatim)
**Apply to:** New `Commanders` action
```csharp
if (!SameOriginRequestValidator.IsValid(Request))
{
    return StatusCode(
        StatusCodes.Status403Forbidden,
        new { Message = "This endpoint only accepts same-origin browser requests." });
}
```
**Note:** Uses inline `new { Message = "..." }` form, not `GetForbiddenMessage()`. Both are equivalent; D-04 says "copy Status action's guard pattern verbatim".

### async action signature with CancellationToken
**Source:** All existing async actions in `AdminHarvestController.cs`
**Apply to:** New `Commanders` action
```csharp
public async Task<IActionResult> Commanders(int page = 1, CancellationToken cancellationToken = default)
```

### ArgumentNullException guard in constructor
**Source:** `AdminHarvestController.cs:47-55`
**Apply to:** Not applicable for `CommandersGridViewModel` (record, no constructor). Not applicable for the new action (uses existing controller's already-guarded fields).

### `{ get; init; }` vs computed `{ get; }` on ViewModels
**Source:** `AdminHarvestViewModel.cs:41` — `DeckTotalPages` is a computed `{ get; }` property
**Apply to:** `CommandersGridViewModel.DeckTotalPages` — MUST be computed `{ get; }`, not `{ get; init; }`
**Why:** Project CLAUDE.md constraint: "never auto-convert `{ get; init; }` to `{ get; }`". Conversely: never use `{ get; init; }` for a property that is derived from other properties — it would require the caller to compute and supply the value, creating a consistency bug.

### IReadOnlyList on public surface
**Source:** `AdminHarvestViewModel.cs:29` — `IReadOnlyList<HarvestedCommanderRow> HarvestedCommanders { get; init; } = Array.Empty<HarvestedCommanderRow>();`
**Apply to:** `CommandersGridViewModel.HarvestedCommanders` — use same type and default

### CSS additions — admin-common.css only, `.admin-shell` scope
**Source:** `admin-common.css:376-388` (`.admin-harvest__panel` rules) + `admin-common.css:335-338` (`.admin-empty`)
**Apply to:** All Phase 44 new CSS classes (`admin-harvest__grid-loading`, `admin-harvest__grid-error`, `admin-harvest__grid-pager strong[aria-current="page"]`)
**Rule:** New rules go in `admin-common.css` only, scoped under `.admin-shell`. Never in `site.css` or `site-common.css`.

### EnsureSchemaAsync try/catch swallow
**Source:** `CategoryKnowledgeRepository.cs:127-136`
**Apply to:** The DROP+CREATE block must be inside the same existing try/catch — do not add a separate try/catch. The swallow is intentional ("indexes are startup optimizations").

---

## No Analog Found

None. All files have strong analogs in the codebase.

---

## Critical Anti-Patterns (from RESEARCH.md — repeat here for planner visibility)

| Anti-Pattern | Source to Follow Instead |
|---|---|
| Add event listeners inside innerHTML-swap callback | Attach delegation once on `#commanders-grid-container` (stable parent) before first swap |
| Pass full `AdminHarvestViewModel` to partial | Use slim `CommandersGridViewModel` — the full model has null harvest-run fields in this context |
| Use `{ get; init; }` for `DeckTotalPages` | Use computed `{ get; }` — identical to `AdminHarvestViewModel.cs:41` |
| Put new CSS in `site.css` or `site-common.css` | `admin-common.css` only, `.admin-shell` scope |
| Add `import` / `export` to `admin-harvest.ts` | All new code goes inside the existing IIFE — `module: "none"` means no module system |
| Commit `wwwroot/js/admin-harvest.js` | Only `wwwroot/ts/admin-harvest.ts` is committed; `.js` is gitignored |
| Run `DROP INDEX` in a separate command outside the try/catch | Add DROP statements at the top of the existing `indexCommand.CommandText` raw string literal |
| Use `CREATE INDEX CONCURRENTLY` | Use `CREATE INDEX IF NOT EXISTS` — the existing try/catch makes startup index work safe without CONCURRENTLY |
| Test "no headers → 403" for SC4 | Test cross-origin Referer/Origin → 403; no-header requests pass the validator by design |

---

## Metadata

**Analog search scope:** `DeckFlow.Web/Controllers/Admin/`, `DeckFlow.Web/Views/AdminHarvest/`, `DeckFlow.Web/Models/Admin/`, `DeckFlow.Web/wwwroot/ts/`, `DeckFlow.Web/wwwroot/css/`, `DeckFlow.Core/Knowledge/`, `DeckFlow.Web.Tests/`, `DeckFlow.Core.Tests/`
**Files scanned:** 9 source files read directly (controller, view, view model, TypeScript, CSS, repository, fake store, two test files)
**Pattern extraction date:** 2026-06-13
