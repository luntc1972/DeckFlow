# Phase 44: Admin Grid Lazy Paging - Research

**Researched:** 2026-06-13
**Domain:** ASP.NET Core MVC partial views, vanilla TypeScript AJAX, dual-dialect SQLite/Postgres index DDL
**Confidence:** HIGH — all findings verified against actual source files

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-01:** The new endpoint returns a Razor PartialView (grid table rows + numbered pagination as HTML). Client fetches and swaps innerHTML of `#commanders-grid-container`. This is the first `PartialView()` in DeckFlow.Web — establish the pattern cleanly.

**D-02:** Extract current grid markup (`Index.cshtml:218-269`) into the partial. Initial `Index.cshtml` keeps only the section shell, empty placeholder, and a JS hook.

**D-03:** New action `GET /Admin/Harvest/commanders?page={n}` on `AdminHarvestController`, returning `PartialView`. It runs `GetDistinctProcessedCommanderCountAsync` + `GetPagedProcessedCommandersAsync` (the calls REMOVED from `Index`).

**D-04:** The endpoint uses verbatim copy of the `Status` action's guard pattern: `SameOriginRequestValidator.IsValid(Request)` → on failure `StatusCode(403, new { Message = "..." })`. Direct browser navigation to `/Admin/Harvest/commanders` (no same-origin Origin/Referer) returns 403 (SC4).

**D-05:** Client TS auto-fires `fetch('/Admin/Harvest/commanders?page=1', { credentials: 'same-origin' })` on `DOMContentLoaded`. Extends existing `admin-harvest.ts`. No "Load" button.

**D-06:** Loading state placeholder while fetch is in flight; error state with retry on fetch failure. Visual treatment deferred to 44-UI-SPEC.md (already produced); behavior locked here.

**D-07:** Numbered pages PLUS prev/next (windowed, current ±2). Show total count + "Page X of Y". Event delegation on `#commanders-grid-container`. Keep `DefaultDeckPageSize = 25`.

**D-08/D-09/D-10:** Verify-and-consolidate index strategy. Existing indexes at `:114-115` already exist. Plan must run EXPLAIN on both queries, then replace the two overlapping indexes with a single partial expression index `ON deck_queue(LOWER(commander_name)) WHERE processed = 1`. SC3 amended by D-10: "an optimal partial expression index serves the count + paged queries; EXPLAIN shows index use." Dual-dialect (SQLite + Postgres) parity required.

### Claude's Discretion

- Exact partial-view file name/location (`Views/AdminHarvest/_CommandersGrid.cshtml` or similar) and the view model passed to it.
- Whether pagination re-binds via event delegation on a stable parent vs re-attaching listeners after each innerHTML swap (delegation preferred).
- The `EnsureSchemaAsync` idempotency form for the index swap (drop-if-exists old + create-if-not-exists new), consistent with existing index blocks.
- Test placement: controller-level test for the new action's SameOrigin 403 + partial render (DeckFlow.Web.Tests); repository/index assertions (DeckFlow.Core.Tests) including an EXPLAIN-plan assertion if feasible in SQLite CI.

### Deferred Ideas (OUT OF SCOPE)

- Server-side caching of the commander count.
- Replacing prev/next full-page links elsewhere in admin with AJAX partial pattern.
- Page-size selector (10/25/50).

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| GRID-01 | `/Admin/Harvest` commander-deck grid loads pages on demand (AJAX partial endpoint, same-origin guarded, numbered pages) instead of computing the full grid on initial page load | Controller refactor (remove two query calls from Index), new commanders action, PartialView pattern, TypeScript AJAX extension, SameOriginRequestValidator guard |
| GRID-02 | The underlying slow query is fixed at the source — add/consolidate the index on `LOWER(commander_name)` so the distinct-count + paged read no longer full-scan on every load | `EnsureSchemaAsync` index consolidation, drop redundant pair, add partial expression index, dual-dialect DDL, EXPLAIN verification |

</phase_requirements>

---

## Summary

Phase 44 has three separable units of work: (1) remove the two slow query calls from `AdminHarvestController.Index` and replace the grid section with an empty placeholder, (2) add a new `GET /Admin/Harvest/commanders` action that returns a `PartialView` with the grid + numbered pagination, guarded by `SameOriginRequestValidator`, and (3) consolidate overlapping indexes in `CategoryKnowledgeRepository.EnsureSchemaAsync` to a single partial expression index serving the count and paged queries.

All three implementation areas are well-grounded in existing codebase patterns. The `Status` action (`AdminHarvestController.cs:122-150`) is the direct template for the new `commanders` action — same guard, same 403 return shape. The existing `admin-harvest.ts` (183 lines, IIFE pattern, `fetch` with `credentials:'same-origin'`) needs to be extended with an innerHTML-swap loader and event delegation; the TypeScript patterns are idiomatic and straightforward to extend without structural change. The index work is the highest-precision task: two indexes at lines 114-115 of `EnsureSchemaAsync` must be replaced with a single partial expression index, requiring dialect-branched DDL because the `DROP INDEX` approach differs between SQLite and Postgres.

The UI-SPEC (`44-UI-SPEC.md`) is already produced and fully specifies the loading/error states, the pagination structure, all copy, and CSS additions to `admin-common.css`. The planner must read it; this RESEARCH.md does not re-derive what the UI-SPEC already locks.

**Primary recommendation:** Three-wave plan — Wave 0 (index consolidation + EXPLAIN verification in `EnsureSchemaAsync`), Wave 1 (controller: strip Index, add commanders PartialView action, model), Wave 2 (TypeScript extension + partial Razor template + CSS). Tests travel with their wave.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Commander grid data (count + paged rows) | API / Backend | Database / Storage | Query runs server-side; client gets pre-rendered HTML, not JSON data |
| Grid HTML rendering | Frontend Server (SSR) | — | PartialView renders Razor on the server; client receives HTML fragment |
| Grid population trigger + pagination click | Browser / Client | — | TypeScript fires the fetch and swaps innerHTML; no SSR involvement after initial page load |
| Index optimization | Database / Storage | — | `EnsureSchemaAsync` DDL owns index lifecycle; pure data-tier concern |
| Same-origin guard | API / Backend | — | `SameOriginRequestValidator.IsValid(Request)` runs in the controller action before any DB work |
| Loading / error state | Browser / Client | — | TypeScript owns the placeholder markup swap; no Razor involved for these transient states |

---

## Standard Stack

No new packages. All capabilities are met by what is already in the solution.

### Core (existing — confirmed against source)

| Library | Version | Purpose | Confirmed |
|---------|---------|---------|-----------|
| ASP.NET Core MVC | 10.0 | `PartialView()` return from controller action | [VERIFIED: DeckFlow.Web.csproj] |
| TypeScript | 6.0.2 | Extend `admin-harvest.ts` with fetch + innerHTML swap | [VERIFIED: DeckFlow.Web/package.json] |
| Microsoft.Data.Sqlite | 10.0.0 | SQLite DDL for partial expression index | [VERIFIED: DeckFlow.Core.csproj] |
| Npgsql | 10.0.0 | Postgres DDL for partial expression index | [VERIFIED: DeckFlow.Core.csproj] |
| xUnit | 2.9.3 | Test framework for both test projects | [VERIFIED: DeckFlow.Web.Tests.csproj] |

**Installation:** None required. No new packages.

---

## Package Legitimacy Audit

> Not applicable — Phase 44 installs zero new packages (npm, NuGet, or otherwise). All capabilities use existing in-solution dependencies.

---

## Architecture Patterns

### System Architecture Diagram

```
Browser GET /Admin/Harvest
        │
        ▼
AdminHarvestController.Index (sync path — no DB calls for commander grid)
        │  renders skeleton:
        │    stats, recent runs, schedule sections  (unchanged)
        │    <div id="commanders-grid-container" aria-busy="true">
        │      <p class="admin-harvest__grid-loading">Loading commanders…</p>
        │    </div>
        │
        ▼
Browser DOMContentLoaded
        │
        ▼
admin-harvest.ts: fetchCommandersGrid(page=1)
        │  fetch('/Admin/Harvest/commanders?page=1', {credentials:'same-origin'})
        │
        ▼
AdminHarvestController.commanders (new action)
        │  SameOriginRequestValidator.IsValid(Request)
        │    ├── FAIL (no Origin, cross-origin) → 403 StatusCode(403, {Message})
        │    └── PASS (same-origin fetch sends Origin automatically)
        │
        │  GetDistinctProcessedCommanderCountAsync()  ───┐
        │  GetPagedProcessedCommandersAsync(page, 25) ───┤  CategoryKnowledgeStore
        │                                                └──► CategoryKnowledgeRepository
        │                                                        │
        │                                                        ▼
        │                                               deck_queue table
        │                                               partial expression index:
        │                                               LOWER(commander_name) WHERE processed=1
        │
        │  return PartialView("_CommandersGrid", model)
        │
        ▼
Browser: innerHTML swap of #commanders-grid-container
        │  set aria-busy="false"
        │  attach event delegation on #commanders-grid-container for [data-page] clicks
        │
        ▼
Pagination click → fetchCommandersGrid(page=N) → same flow above
```

### Recommended Project Structure (additions only)

```
DeckFlow.Web/
├── Controllers/Admin/
│   └── AdminHarvestController.cs         (modified: strip Index calls; add commanders action)
├── Views/AdminHarvest/
│   ├── Index.cshtml                      (modified: replace grid section interior with placeholder)
│   └── _CommandersGrid.cshtml            (NEW: partial — grid table + numbered pagination)
├── Models/Admin/
│   └── CommandersGridViewModel.cs        (NEW: slim model for the partial)
└── wwwroot/
    ├── ts/
    │   └── admin-harvest.ts              (modified: add fetchCommandersGrid + event delegation)
    └── css/
        └── admin-common.css              (modified: add Phase 44 loading/error/pagination CSS)
DeckFlow.Core/
└── Knowledge/
    └── CategoryKnowledgeRepository.cs   (modified: EnsureSchemaAsync index consolidation)
```

### Pattern 1: PartialView return from controller action

**What:** `AdminHarvestController.commanders` returns `PartialView("_CommandersGrid", model)` after populating `CommandersGridViewModel`.

**When to use:** When the client fetches a fragment of HTML to inject into the page without a full navigation. This is the first `PartialView()` call in `DeckFlow.Web` — all existing actions return `View(...)`, `Json(...)`, `StatusCode(...)`, or `RedirectToAction(...)`.

**Razor partial naming convention:** Underscore prefix (`_CommandersGrid.cshtml`) in the same `Views/AdminHarvest/` folder as `Index.cshtml`. ASP.NET Core MVC resolves `PartialView("_CommandersGrid", model)` to `Views/AdminHarvest/_CommandersGrid.cshtml` when called from `AdminHarvestController`. [VERIFIED: ASP.NET Core MVC view resolution rules — same-controller subfolder lookup]

```csharp
// Source: pattern derived from existing View("", model) calls in AdminHarvestController + DeckController
[HttpGet("commanders")]
public async Task<IActionResult> Commanders(int page = 1, CancellationToken cancellationToken = default)
{
    if (!SameOriginRequestValidator.IsValid(Request))
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new { Message = "This endpoint only accepts same-origin browser requests." });
    }

    page = Math.Max(page, 1);
    const int pageSize = AdminHarvestViewModel.DefaultDeckPageSize;
    var total = await _categoryStore.GetDistinctProcessedCommanderCountAsync(cancellationToken).ConfigureAwait(false);
    var totalPages = (int)Math.Ceiling((double)Math.Max(total, 1) / pageSize);
    page = Math.Min(page, totalPages);
    var rows = await _categoryStore.GetPagedProcessedCommandersAsync(page, pageSize, cancellationToken).ConfigureAwait(false);

    var model = new CommandersGridViewModel
    {
        HarvestedCommanders = rows,
        DeckPage = page,
        DeckPageSize = pageSize,
        DeckTotalCount = total,
    };

    return PartialView("_CommandersGrid", model);
}
```

### Pattern 2: SameOriginRequestValidator 403 guard (copy verbatim from Status action)

**What:** `SameOriginRequestValidator.IsValid(Request)` at the top of any AJAX-only action; on false, return `StatusCode(403, new { Message = "..." })`.

**Critical behavioral detail for SC4:** The validator's fallback at line 31 of `SameOriginRequestValidator.cs` is:
```csharp
// Allow non-browser callers and same-origin requests where the browser omitted both headers.
return true;
```
This means: if a request arrives with **no Origin and no Referer** header, `IsValid` returns `true` (ALLOW). A direct browser GET from the address bar typically sends no `Origin` but MAY send a `Referer` from the previous page. The validator at `AdminHarvestController` line 125 uses the **inline message** form (not `GetForbiddenMessage()`); `AdminContentKbController` uses `GetForbiddenMessage()`. Either form is acceptable — the CONTEXT says "copy Status action's guard pattern verbatim", which means the inline `new { Message = "..." }` form.

**SC4 nuance — what "direct navigation → 403" actually means in practice:** When a user types `/Admin/Harvest/commanders` into the address bar, Chrome/Firefox typically send no `Origin` and no `Referer` (clean navigation). Under the current validator, that would PASS (return 200 with partial HTML), not 403. However, since the endpoint is under `/Admin/Harvest` which is already behind `BasicAuthMiddleware`, an unauthenticated direct browser GET would first hit the Basic Auth gate and get a 401. Authenticated direct-browser access returns partial HTML (no page shell) — ugly but not a security hole. **D-04 locks SameOriginRequestValidator as the guard mechanism.** The planner should note that SC4's "403 on direct navigation" is satisfied for cross-origin requests (different-origin Referer/Origin → 403), and that bare direct-navigation (no headers) technically passes the validator — this is consistent with how `Status` and all other `AdminContentKbController` endpoints behave. The test in `AdminContentKbControllerTests` simulates cross-origin by setting `Origin: https://evil.test` and verifies 403; same pattern applies here.

```csharp
// Source: AdminHarvestController.cs:125-129 (Status action)
if (!SameOriginRequestValidator.IsValid(Request))
{
    return StatusCode(
        StatusCodes.Status403Forbidden,
        new { Message = "This endpoint only accepts same-origin browser requests." });
}
```

### Pattern 3: TypeScript AJAX fetch with innerHTML swap (extending IIFE pattern)

**What:** The existing `admin-harvest.ts` is a single self-invoking IIFE (`((): void => { ... })()`). TypeScript `module: "none"` means no `import`/`export` — everything is function-scoped within the IIFE. The new commander-grid loader must be added inside the same IIFE block.

**Key facts about the existing TS:**
- Lines 92-111: `fetchStatus()` — async function, AbortController timeout, `credentials:'same-origin'`, `Accept: application/json`, returns null on non-2xx
- Lines 113-182: `DOMContentLoaded` handler — finds `#harvest-status-live`, starts polling loop
- The file has no `import`/`export`; all helpers are inner-IIFE functions or constants

**Extension pattern:**

```typescript
// Source: admin-harvest.ts:92-111 (fetchStatus pattern adapted for HTML response)
// Inside the existing IIFE, after existing constants:

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

    if (!response.ok) {
      return null;  // triggers error state
    }

    return await response.text();
  } finally {
    window.clearTimeout(timeoutId);
  }
};
```

**Loading state, innerHTML swap, event delegation:** The DOMContentLoaded handler gains a second branch (alongside the `#harvest-status-live` branch) that finds `#commanders-grid-container` and fires the initial load + sets up delegation.

**Event delegation survives innerHTML swaps** because it is attached to `#commanders-grid-container` (the stable parent), not to the `[data-page]` links inside it. After each innerHTML swap the parent element persists; the inner links are replaced but the delegation listener on the parent continues to intercept `[data-page]` clicks.

### Pattern 4: Index consolidation in EnsureSchemaAsync — dual-dialect DDL

**What:** Replace the two overlapping indexes at `CategoryKnowledgeRepository.cs:114-115`:
```sql
-- Current (to be replaced):
CREATE INDEX IF NOT EXISTS ix_deck_queue_processed_commander ON deck_queue(processed, commander_name);
CREATE INDEX IF NOT EXISTS ix_deck_queue_processed_commander_lower ON deck_queue(processed, LOWER(commander_name));

-- Target: single partial expression index
-- SQLite syntax:
CREATE INDEX IF NOT EXISTS ix_deck_queue_commander_lower_processed ON deck_queue(LOWER(commander_name)) WHERE processed = 1;
-- Postgres syntax: identical — both support partial indexes with WHERE and expression columns
```

**Idempotency:** Both SQLite and Postgres support `DROP INDEX IF EXISTS <name>` to remove the old indexes (run AFTER the new index is created — see ORDER MANDATE below). The Postgres form does not need the table name (`DROP INDEX IF EXISTS name` is valid in Postgres 8.2+; Npgsql 10.0.0 targets Postgres 12+).

**Dialect branching requirement:** The current `indexCommand` block (lines 106-123) does NOT branch by dialect — all 14 indexes run against both SQLite and Postgres with the same SQL text. This works because the `LOWER()` function and `CREATE INDEX IF NOT EXISTS` syntax are cross-dialect. **However, the `DROP INDEX` step IS dialect-sensitive:** SQLite requires `DROP INDEX IF EXISTS name` (no table qualifier); Postgres uses the same form (`DROP INDEX IF EXISTS name`). Both dialects match here — no branching needed for DROP either.

**Full replacement block in EnsureSchemaAsync (within existing `try` block):**

> **ORDER MANDATE (D-09 / 44-01-PLAN.md Task 2):** CREATE the new index FIRST, then DROP the old pair. The batch aborts on a failed CREATE before the DROPs run, so the old indexes survive — no silent regression to no-index when `EnsureSchemaAsync` swallows the exception. Do NOT reorder to drop-first.

```sql
-- Add the single partial expression index that SC3 specifies (CREATE before DROP)
CREATE INDEX IF NOT EXISTS ix_deck_queue_commander_lower_processed ON deck_queue(LOWER(commander_name)) WHERE processed = 1;
-- Drop the two redundant composite indexes that the partial expression replaces
DROP INDEX IF EXISTS ix_deck_queue_processed_commander;
DROP INDEX IF EXISTS ix_deck_queue_processed_commander_lower;
```

**EXPLAIN verification (D-09):** Both queries must be verified with EXPLAIN before the plan locks the DDL. The `GetPagedProcessedCommanderRowsAsync` query groups by `LOWER(commander_name) WHERE processed = 1 AND commander_name IS NOT NULL`. The `GetDistinctProcessedCommanderCountAsync` query is `COUNT(DISTINCT LOWER(commander_name)) FROM deck_queue WHERE processed = 1 AND commander_name IS NOT NULL`. Both filter on `processed = 1` and operate on `LOWER(commander_name)` — the partial expression index matches their access pattern exactly.

**SQLite EXPLAIN QUERY PLAN expected output (after index consolidation):**
- Should show `SEARCH deck_queue USING INDEX ix_deck_queue_commander_lower_processed` rather than `SCAN TABLE deck_queue`

**EXPLAIN is a Wave 0 task, not a planning assumption.** If the EXPLAIN unexpectedly shows the existing indexes already cover the queries optimally, the drop+consolidate still proceeds because D-09 explicitly calls for consolidation to the intent-revealing partial index.

### Anti-Patterns to Avoid

- **Building the full-page shell in the partial:** The partial (`_CommandersGrid.cshtml`) must render ONLY the grid interior: meta count line, table, pagination nav. It must NOT include `<section>`, `<h2>`, or any element that is already in `Index.cshtml`'s placeholder.
- **Re-attaching event listeners after each swap:** Attach the `data-page` click delegation once to `#commanders-grid-container` (the stable parent). Do not call `addEventListener` inside the innerHTML-swap callback — the delegation persists across swaps.
- **Modifying `DeckTotalPages` property on `AdminHarvestViewModel`:** The `DeckTotalPages` computed property (line 41 of `AdminHarvestViewModel.cs`) is `(int)Math.Ceiling(...)` — it uses `{ get; }` only (not `init`). Since the project constraint forbids converting `{ get; init; }` to `{ get; }`, note that `DeckTotalPages` is ALREADY a computed `{ get; }` property. The new `CommandersGridViewModel` must replicate this as a computed property too.
- **Using the same `AdminHarvestViewModel` as the partial's model:** Do not pass the full `AdminHarvestViewModel` to the partial. It contains fields (`ActiveRun`, `RecentRuns`, `Schedule`, `Stats`) that are null in the partial context, creating confusion. A slim `CommandersGridViewModel` (same paging fields, no harvest-run fields) is cleaner and safer.
- **Putting CSS in `site.css` or `site-common.css`:** All Phase 44 CSS additions go in `admin-common.css` only, under `.admin-shell` scope. Confirmed by UI-SPEC and `./CLAUDE.md`.
- **Staging or committing compiled `.js` files:** The compiled JS is gitignored (`DeckFlow.Web/wwwroot/js/*.js`). Only `wwwroot/ts/admin-harvest.ts` is committed.
- **Using `CREATE INDEX CONCURRENTLY` in EnsureSchemaAsync:** This is a Postgres-only DDL form that cannot run inside a transaction. `EnsureSchemaAsync` does not use a transaction for the index block (the existing try/catch swallows errors). `CREATE INDEX IF NOT EXISTS` (non-concurrent) is safe here and matches the existing pattern.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Same-origin CSRF guard | Custom header check | `SameOriginRequestValidator.IsValid(Request)` | Already implements scheme+host+port matching with X-Forwarded-Proto; validated in prod |
| HTTP fetch with timeout | `setTimeout` + `fetch` from scratch | Replicate the `fetchStatus` pattern in `admin-harvest.ts:92-111` | Already handles AbortController, timeout cleanup, non-2xx → null |
| Pagination windowing logic | Compute page windows in TypeScript | Compute in Razor (`_CommandersGrid.cshtml`) using C# | Server-side keeps all pagination math in one place; client just fires the URL |
| Index existence check before CREATE | `SELECT FROM sqlite_master WHERE type='index'` | `CREATE INDEX IF NOT EXISTS` | Built into SQL DDL; idempotent by design |

---

## Runtime State Inventory

> Phase 44 is not a rename/refactor/migration phase. No runtime state inventory required.

---

## Common Pitfalls

### Pitfall 1: SameOriginRequestValidator returns true for no-header requests
**What goes wrong:** A test simulating "direct browser navigation" by sending a request with no `Origin` and no `Referer` will PASS the validator (return 200), not 403. SC4 says "direct browser navigation → 403" — but the existing validator explicitly allows no-header requests to pass (line 31: "Allow non-browser callers and same-origin requests where the browser omitted both headers").
**Why it happens:** The validator is designed to allow API clients (curl, Postman) that don't send browser headers. Direct navigation from the address bar sends no `Origin`; it may or may not send `Referer` depending on browser security settings.
**How to avoid:** Test the correct threat model: cross-origin Referer (e.g. `Referer: https://evil.test`) → 403. Same-origin fetch (Origin header matching the request host) → 200 with partial HTML. This matches exactly how `AdminContentKbControllerTests` tests 403 (sets `Origin: https://evil.test`).
**Warning signs:** A test that sends no headers at all and expects 403 will fail because the validator returns true.

### Pitfall 2: `DROP INDEX` executing in the same multi-statement block as `CREATE INDEX` in SQLite
**What goes wrong:** SQLite's `DbCommand` can execute multiple statements in one `CommandText` block separated by `;`. However, the existing `indexCommand` at lines 106-123 uses a raw string literal with multiple `CREATE INDEX` statements. Adding `DROP INDEX` statements to the same block is safe in SQLite (tested API). But if the table/index is in a WAL-mode database and another connection holds a read lock, the DROP may fail.
**Why it happens:** The existing `try/catch` at line 127 swallows `DbException` so failures are non-fatal — this is correct behavior (startup index work is best-effort).
**How to avoid:** Add the `DROP INDEX IF EXISTS` lines at the top of the index command block, before the `CREATE INDEX IF NOT EXISTS` for the new partial index. The existing try/catch handles any failures gracefully.

### Pitfall 3: TypeScript IIFE module scope — no top-level declarations accessible
**What goes wrong:** If the new commander-grid code is added OUTSIDE the existing `((): void => { ... })()` IIFE block, it will fail at compile time or runtime because `tsconfig.json` uses `module: "none"` — there is no module system. All code in the compiled file is global-scope, meaning helpers defined outside the IIFE are visible globally (no isolation). More critically: the existing helper functions (`setText`, `formatUtc`, `fetchStatus`, etc.) are defined INSIDE the IIFE and are not accessible outside it.
**Why it happens:** `module: "none"` means TypeScript compiles to raw JS; the IIFE is the only encapsulation.
**How to avoid:** All new functions (`fetchCommandersGrid`, `loadCommandersGrid`, grid-container event setup) must be defined inside the existing IIFE, after the existing helper functions.

### Pitfall 4: DeckTotalPages computed property on the new ViewModel
**What goes wrong:** The project constraint says "never auto-convert `{ get; init; }` to `{ get; }`". If the new `CommandersGridViewModel` declares `DeckTotalPages` as `{ get; init; }` (a stored property), the value must be set explicitly on construction. If it is a computed `{ get; }` property, it cannot have `init` and must compute from `DeckTotalCount` and `DeckPageSize`.
**Why it happens:** `System.Text.Json` silently skips `{ get; }` only properties in .NET 9+ (the existing `AdminHarvestViewModel.DeckTotalPages` is computed `{ get; }` — it is never serialized). For a Razor-only view model, computed `{ get; }` is correct.
**How to avoid:** `CommandersGridViewModel.DeckTotalPages` should be a computed property: `public int DeckTotalPages => (int)Math.Ceiling((double)Math.Max(DeckTotalCount, 1) / Math.Max(DeckPageSize, 1));` — same formula as `AdminHarvestViewModel.cs:41`.

### Pitfall 5: Index consolidation test update required in CategoryKnowledgeRepositoryTests
**What goes wrong:** `EnsureSchemaAsync_CreatesDeckQueueIndexes` at `CategoryKnowledgeRepositoryTests.cs:276-287` asserts `Assert.Contains("ix_deck_queue_processed_commander", indexNames)` and `Assert.Contains("ix_deck_queue_processed_commander_lower", indexNames)`. After the consolidation drops those two indexes and creates `ix_deck_queue_commander_lower_processed`, this test FAILS.
**Why it happens:** The test asserts the exact index names that existed before the refactor.
**How to avoid:** Update the test assertions in the same wave as the `EnsureSchemaAsync` change: remove the two old name assertions, add `Assert.Contains("ix_deck_queue_commander_lower_processed", indexNames)`. This is the only test that must change.

---

## Code Examples

### Commander grid placeholder in Index.cshtml

```html
<!-- Source: derived from 44-UI-SPEC.md + D-02 (replace Index.cshtml:218-269) -->
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

### _CommandersGrid.cshtml structure (partial model: CommandersGridViewModel)

```html
<!-- Source: derived from Index.cshtml:218-269 (D-02 extract) + 44-UI-SPEC.md §3-4 -->
@model DeckFlow.Web.Models.Admin.CommandersGridViewModel

<p class="admin-harvest__grid-meta">@Model.DeckTotalCount commanders — Page @Model.DeckPage of @Model.DeckTotalPages</p>

@if (Model.HarvestedCommanders.Count == 0)
{
    <p class="admin-empty">No harvested commanders yet.</p>
}
else
{
    <div class="admin-table-scroll" role="region"
         aria-label="Harvested commanders - scroll horizontally to see all columns" tabindex="0">
        <table class="admin-table">
            <caption class="sr-only">Harvested commanders with deck count and last processed timestamp</caption>
            <thead>
                <tr>
                    <th scope="col">#</th>
                    <th scope="col">Commander</th>
                    <th scope="col">Decks Categorized</th>
                    <th scope="col">Last Processed (UTC)</th>
                </tr>
            </thead>
            <tbody>
                @{
                    var rankOffset = (Model.DeckPage - 1) * Model.DeckPageSize;
                }
                @for (var i = 0; i < Model.HarvestedCommanders.Count; i++)
                {
                    var c = Model.HarvestedCommanders[i];
                    <tr>
                        <td>@(rankOffset + i + 1)</td>
                        <td>@c.CommanderName</td>
                        <td>@c.DeckCount</td>
                        <td>@FormatLastProcessedUtc(c.LastProcessedUtc)</td>
                    </tr>
                }
            </tbody>
        </table>
    </div>

    @* Numbered pagination — see 44-UI-SPEC.md §4 for full windowing spec *@
    @if (Model.DeckTotalPages > 1)
    {
        <nav class="admin-feedback-pagination admin-harvest__grid-pager"
             aria-label="Commander grid pagination">
            @* Prev / window / Next — computed by Razor, data-page attrs picked up by TS delegation *@
        </nav>
    }
}
```

Note: `FormatLastProcessedUtc` is a local function defined in `Index.cshtml`. Since the partial is a separate file, it must either re-declare the helper or receive pre-formatted strings. **Recommended:** re-declare the same `static string FormatLastProcessedUtc(string? v)` local function at the top of `_CommandersGrid.cshtml`, since it is a pure string utility. [ASSUMED — no established partial/shared-function pattern exists in this codebase]

### CommandersGridViewModel

```csharp
// Source: derived from AdminHarvestViewModel.cs — extract the paging fields only
using DeckFlow.Web.Services.Harvest;

namespace DeckFlow.Web.Models.Admin;

/// <summary>
/// View model for the _CommandersGrid partial — paging state only, no harvest-run fields.
/// </summary>
public sealed record CommandersGridViewModel
{
    /// <summary>Processed harvested commanders for the current admin grid page.</summary>
    public IReadOnlyList<HarvestedCommanderRow> HarvestedCommanders { get; init; } = Array.Empty<HarvestedCommanderRow>();

    /// <summary>One-based page number currently rendered.</summary>
    public int DeckPage { get; init; } = 1;

    /// <summary>Number of rows per page.</summary>
    public int DeckPageSize { get; init; } = AdminHarvestViewModel.DefaultDeckPageSize;

    /// <summary>Total distinct processed commanders.</summary>
    public int DeckTotalCount { get; init; }

    /// <summary>Total page count, never less than 1.</summary>
    public int DeckTotalPages => (int)Math.Ceiling((double)Math.Max(DeckTotalCount, 1) / Math.Max(DeckPageSize, 1));
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Full page reload for pagination | AJAX partial fetch + innerHTML swap | Phase 44 | Eliminates full-page navigation for pagination; only grid section updates |
| Count+paged queries on every initial page load | Count+paged queries deferred to async partial request | Phase 44 | Unblocks the synchronous render path; page skeleton appears immediately |
| Two composite indexes (`processed, LOWER(commander_name)` and `processed, commander_name`) | One partial expression index (`LOWER(commander_name) WHERE processed=1`) | Phase 44 | Smaller index footprint; intent-revealing; matches both query access patterns |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `PartialView("_CommandersGrid", model)` resolves to `Views/AdminHarvest/_CommandersGrid.cshtml` when called from `AdminHarvestController` (same-controller subfolder lookup) | Architecture Patterns §1 | If resolution fails, controller returns 500; fix by providing full path `"~/Views/AdminHarvest/_CommandersGrid.cshtml"` |
| A2 | `FormatLastProcessedUtc` should be redeclared as a local function in `_CommandersGrid.cshtml` since Razor local functions don't cross files | Code Examples | If wrong, compile error in partial; alternative: move format to the ViewModel's `HarvestedCommanderRow` or to a Razor `@functions` block in a `_ViewImports`-registered helper |
| A3 | `DROP INDEX IF EXISTS ix_deck_queue_processed_commander` + `DROP INDEX IF EXISTS ix_deck_queue_processed_commander_lower` will execute without error inside the existing try/catch index block for both SQLite and Postgres | Architecture Patterns §4 | If DROP fails (e.g. index doesn't exist on a fresh DB before CREATE), the `IF EXISTS` clause prevents any error — this is a non-issue by DDL design |
| A4 | Render's Postgres instance is version 12+ (supporting `DROP INDEX IF EXISTS` without table qualifier and partial expression indexes) | Architecture Patterns §4 | If Postgres is older than 9.0 (extremely unlikely for a 2026 Render deployment), partial indexes are unavailable; use the existing composite index instead |

---

## Open Questions (RESOLVED)

1. **Should the `commanders` action be named `Commanders` or keep the lowercase route name from D-03?**
   - What we know: D-03 specifies `GET /Admin/Harvest/commanders?page={n}`. The `[Route("Admin/Harvest")]` on the controller + `[HttpGet("commanders")]` on the action produces this URL. The action method name (C# method name) is independent of the route.
   - What's unclear: nothing — `[HttpGet("commanders")]` with method name `Commanders` is idiomatic.
   - Recommendation: Use method name `Commanders` with `[HttpGet("commanders")]` attribute.

2. **Does the `scrollIntoView` call after pagination belong in the TypeScript or should the partial's response trigger it?**
   - What we know: UI-SPEC §Interaction Contract step 7 says TypeScript calls `document.getElementById('harvested-commanders')?.scrollIntoView(...)` after a successful swap. The stable `#harvested-commanders` section element exists in `Index.cshtml` and survives all innerHTML swaps of `#commanders-grid-container`.
   - What's unclear: nothing — TypeScript calls scroll after `await fetchCommandersGrid(page)` succeeds. The `#harvested-commanders` target is always present.
   - Recommendation: TypeScript owns the scroll; it fires after innerHTML assignment completes.

---

## Environment Availability

> Phase 44 changes are purely code and schema modifications against the existing development environment. No external tools or new runtimes are required.

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build + test | ✓ | 10.0 | — |
| Node.js (for tsc) | TypeScript compile in MSBuild | ✓ (Docker build-time) | 20 | — |
| SQLite (via Microsoft.Data.Sqlite) | Repository tests | ✓ | 10.0.0 | — |
| `dotnet test` | xUnit test runner | ✓ (WSL2) | 10.0 | Manual harness |

No missing dependencies.

---

## Validation Architecture

> `workflow.nyquist_validation: true` — this section is required.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none (default discovery) |
| Quick run (web tests) | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -x` |
| Quick run (core tests) | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -x` |
| Full suite | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| GRID-01 SC1 | `Index` action does NOT call `GetDistinctProcessedCommanderCountAsync` or `GetPagedProcessedCommandersAsync` | unit | Web.Tests quick run | ❌ Wave 0 — new test in `AdminHarvestControllerTests.cs` |
| GRID-01 SC2 | `commanders` action returns `PartialViewResult` with `CommandersGridViewModel` populated | unit | Web.Tests quick run | ❌ Wave 1 — new test in `AdminHarvestControllerTests.cs` |
| GRID-01 SC4 | Cross-origin request to `commanders` action returns 403 | unit | Web.Tests quick run | ❌ Wave 1 — new test in `AdminHarvestControllerTests.cs` |
| GRID-01 SC2/SC4 (same-origin pass) | Same-origin request to `commanders` action returns 200 PartialView | unit | Web.Tests quick run | ❌ Wave 1 — new test |
| GRID-02 SC3 | `EnsureSchemaAsync` creates `ix_deck_queue_commander_lower_processed` and drops the two old indexes | unit (SQLite in-process) | Core.Tests quick run | ❌ Wave 0 — update `CategoryKnowledgeRepositoryTests.cs:276-287` |
| GRID-02 SC3 | EXPLAIN QUERY PLAN on count query shows index use, not scan | manual | Run EXPLAIN against local SQLite KB DB | — manual only |
| GRID-01 SC2 | Browser: grid populates after page load without full-page reload | smoke (manual) | Navigate to `/Admin/Harvest` in browser | — |
| GRID-01 | Pagination click loads next page via AJAX without full reload | smoke (manual) | Click a page number in the commander grid | — |

### Sampling Rate

- **Per task commit:** `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -x` (or Core.Tests for Wave 0 changes)
- **Per wave merge:** Full `DeckFlow.sln` test run
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` — update `EnsureSchemaAsync_CreatesDeckQueueIndexes` to assert `ix_deck_queue_commander_lower_processed` exists and the two old names do not; covers GRID-02 SC3
- [ ] `DeckFlow.Web.Tests/AdminHarvestControllerTests.cs` — add `Index_DoesNotCallCommanderCountOrPagedQuery` test; covers GRID-01 SC1 (verify the FakeCategoryKnowledgeStore call-count properties for GetDistinctProcessedCommanderCount and GetPagedProcessedCommanders are both 0 after `Index()`)

---

## Security Domain

> `security_enforcement` is not explicitly set to false — treated as enabled.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | Yes (admin route) | `BasicAuthMiddleware` already gates all `/Admin/*` routes — no change needed |
| V3 Session Management | No | No session state introduced |
| V4 Access Control | Yes | `SameOriginRequestValidator.IsValid(Request)` gates the partial endpoint against cross-origin calls |
| V5 Input Validation | Yes | `page` parameter: `Math.Max(page, 1)` + `Math.Min(page, totalPages)` clamp (same pattern as existing `Index` action) |
| V6 Cryptography | No | No crypto introduced |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Cross-origin AJAX call to partial endpoint | Spoofing / Information Disclosure | `SameOriginRequestValidator` rejects cross-origin Origin/Referer |
| Integer overflow on `page` parameter | Tampering | `Math.Max(1)` + `Math.Min(totalPages)` clamp — already applied in `Index`; replicate in `Commanders` action |
| Unauthenticated access to partial endpoint | Elevation of Privilege | `BasicAuthMiddleware` gates all `/Admin/*` routes in `Program.cs:225-227` before route dispatch — partial endpoint inherits this gate automatically |
| Partial HTML injection via innerHTML swap | XSS | Response is server-rendered Razor HTML returned as `text/html`; no user-controlled content injected into innerHTML; Razor HTML-encodes all `HarvestedCommanderRow` fields by default |

---

## Sources

### Primary (HIGH confidence)

- `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` — full source read; `Index` action (lines 73-116), `Status` action (lines 122-150), 403 return pattern (lines 125-129)
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — full source read; `EnsureSchemaAsync` (lines 59-137), index block (lines 106-123), `GetPagedProcessedCommanderRowsAsync` (lines 354-390), `GetDistinctProcessedCommanderCountAsync` (lines 396-410)
- `DeckFlow.Web/Security/SameOriginRequestValidator.cs` — full source read; `IsValid` logic (lines 17-33) — critical for SC4 behavior analysis
- `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` — grid section read (lines 218-269) confirming exact markup to extract
- `DeckFlow.Web/wwwroot/ts/admin-harvest.ts` — full source read (183 lines); `fetchStatus` pattern (lines 92-111), IIFE structure, `DOMContentLoaded` handler (lines 113-182)
- `DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs` — full source read; `DefaultDeckPageSize` constant, computed `DeckTotalPages` property
- `DeckFlow.Web.Tests/AdminHarvestControllerTests.cs` — full source read; existing test structure, stub setup pattern for this controller
- `DeckFlow.Web.Tests/AdminContentKbControllerTests.cs` — cross-origin test pattern (lines 270-304); `crossOrigin: true/false` via `httpContext.Request.Headers.Origin`
- `DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs` — full source read; `DistinctProcessedCommanderCount`, `PagedCommandersResult`, `LastPagedCommanderPage` properties
- `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` — `EnsureSchemaAsync_CreatesDeckQueueIndexes` test (lines 276-287) — the test that must be updated
- `DeckFlow.Web/wwwroot/css/admin-common.css` — confirmed: `admin-feedback-pagination` at line 357, `admin-empty` at line 335, `admin-harvest__panel` at line 376, `admin-table-scroll` at line 427
- `.planning/phases/44-admin-grid-lazy-paging/44-UI-SPEC.md` — full read; authoritative visual/interaction contract
- `.planning/phases/44-admin-grid-lazy-paging/44-CONTEXT.md` — full read; locked decisions D-01 through D-10

### Secondary (MEDIUM confidence)

- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — confirmed IsSqlite dialect branch pattern for ALTER TABLE ADD COLUMN (lines 33, 61-80, 84-99) — establishes the dual-dialect DDL approach used in Phase 43
- `DeckFlow.Web/Services/ICategoryKnowledgeStore.cs` — lines 83-93: confirmed `GetPagedProcessedCommandersAsync` and `GetDistinctProcessedCommanderCountAsync` interface signatures

---

## Metadata

**Confidence breakdown:**
- Controller refactor pattern: HIGH — Status action is a direct template; verified line by line
- PartialView mechanism: HIGH — standard ASP.NET Core MVC; first use in this project but no project-specific constraints against it
- TypeScript extension: HIGH — IIFE structure fully read; `fetchStatus` is the direct template for `fetchCommandersGrid`
- Index consolidation DDL: HIGH — existing index block read; SQLite/Postgres partial index syntax confirmed
- SC4 / SameOriginRequestValidator behavior: HIGH — validator source fully read; the "no-header → pass" behavior is explicit in the source comment at line 31
- Test patterns: HIGH — existing test files read; `AdminHarvestControllerTests` and `CategoryKnowledgeRepositoryTests` are direct templates

**Research date:** 2026-06-13
**Valid until:** 2026-07-13 (stable framework; 30-day horizon)
