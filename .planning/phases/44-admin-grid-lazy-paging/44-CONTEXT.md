# Phase 44: Admin Grid Lazy Paging - Context

**Gathered:** 2026-06-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Make `/Admin/Harvest` fast on initial load by deferring the commander-deck grid off the synchronous render path and fixing the source query.

1. Initial `GET /Admin/Harvest` renders the page skeleton (stats, recent runs, schedule) WITHOUT running the commander count/paged queries; the grid section is an empty placeholder (GRID-01, SC1).
2. The grid populates via AJAX from a new same-origin-guarded partial endpoint; pagination swaps only the grid section, no full-page reload (GRID-01, SC2/SC4).
3. The slow query is fixed at the source via the correct index (GRID-02, SC3).

**In scope:** removing the two query calls from the `Index` action, adding the empty grid placeholder + auto-load JS, a new `GET /Admin/Harvest/commanders` action returning a Razor PartialView (grid rows + numbered pagination), SameOrigin guard + 403 on that endpoint, the index verify-and-consolidate work in `CategoryKnowledgeRepository.EnsureSchemaAsync`, and tests.

**Out of scope:** Studio track (Phases 45–47), any change to the harvest/distill pipeline itself, the commander grid's columns/data shape, non-admin pages. Pure visual styling of the loading state + pagination control is owned by `44-UI-SPEC.md` (run `/gsd-ui-phase 44` next).

</domain>

<decisions>
## Implementation Decisions

### Grid render mechanism (GRID-01)
- **D-01:** The new endpoint returns a **Razor PartialView** (grid table rows + numbered pagination control as HTML). The client fetches it and swaps the `innerHTML` of the grid section. Rationale: Razor stays the single source of row/pagination markup; with the initial load now an empty placeholder, the row markup lives ONLY in the partial (no duplication). This is the first `PartialView()` in `DeckFlow.Web` — establish the pattern cleanly. Reject JSON+client-render (would duplicate row markup in TypeScript and drift from Razor).
- **D-02:** Extract the current grid markup (`AdminHarvest/Index.cshtml:218-269`) into the partial so initial render and AJAX render share one template. Initial `Index.cshtml` keeps only the section shell + empty placeholder + a JS hook (e.g. `data-` attribute / known element id) for the auto-loader.

### Auto-load + endpoint contract (GRID-01, SC2/SC4)
- **D-03:** New action `GET /Admin/Harvest/commanders?page={n}` on `AdminHarvestController`, returning `PartialView`. It runs `GetDistinctProcessedCommanderCountAsync` + `GetPagedProcessedCommandersAsync` (the calls REMOVED from `Index`).
- **D-04:** The endpoint reuses the existing `Status` action's guard pattern verbatim: `SameOriginRequestValidator.IsValid(Request)` → on failure `StatusCode(403, ...)` with `GetForbiddenMessage()` (`AdminHarvestController.cs:125-129`). Direct browser navigation to `/Admin/Harvest/commanders` (no same-origin Origin/Referer) returns 403 (SC4).
- **D-05:** Client TS auto-fires `fetch('/Admin/Harvest/commanders?page=1', { credentials: 'same-origin' })` on `DOMContentLoaded` (SC2 "populates automatically"), then sets the grid section's innerHTML to the response text. Reject an explicit "Load" button (contradicts SC2). Extend the existing `admin-harvest.ts` (it already fetches `/Admin/Harvest/status`); this adds the first innerHTML-swap pattern alongside its current `textContent` updates.
- **D-06:** While the first fetch is in flight the placeholder shows a "Loading…" affordance; on fetch failure it shows an inline error with a retry. Exact visual treatment (spinner vs skeleton, copy) is deferred to `44-UI-SPEC.md`; the BEHAVIOR (auto-fire, loading state, error+retry) is locked here.

### Pagination model (GRID-01)
- **D-07:** Numbered pages PLUS prev/next (windowed), rendered inside the partial. REQUIREMENTS GRID-01 calls for numbered pages; the current view has prev/next only (`Index.cshtml:257-268`). Show total count + "Page X of Y". Each page link triggers the same fetch-and-swap against `?page={n}` (event delegation on the swapped-in control, or re-bind after each swap). Keep `AdminHarvestViewModel.DefaultDeckPageSize = 25` (`AdminHarvestViewModel.cs:12`) — no page-size change this phase.

### Index strategy (GRID-02, SC3) — divergence from literal SC3
- **D-08:** **FINDING:** GRID-02 / SC3 assume a *missing* index causing a full scan, but `CategoryKnowledgeRepository.EnsureSchemaAsync` ALREADY creates `ix_deck_queue_processed_commander_lower ON deck_queue(processed, LOWER(commander_name))` (`:115`) plus `ix_deck_queue_processed_commander(processed, commander_name)` (`:114`). The leading `processed` column means `:115` very likely already serves `WHERE processed=1 ... GROUP BY LOWER(commander_name)` (count + paged). The "missing index" premise is largely false.
- **D-09:** **Verify-first, then consolidate.** Plan must (a) run `EXPLAIN QUERY PLAN` (SQLite) / `EXPLAIN` (Postgres) on both `GetDistinctProcessedCommanderCountAsync` and `GetPagedProcessedCommanderRowsAsync` queries to confirm which index is used and that no full table scan occurs; (b) if `:115` already serves them optimally, replace the two overlapping indexes with a SINGLE **partial expression index** `ON deck_queue(LOWER(commander_name)) WHERE processed = 1` (smaller, intent-revealing, matches SC3's wording) — dropping the now-redundant non-partial pair; (c) keep dual-dialect parity (SQLite partial+expression index syntax and the Postgres equivalent), mirroring the phase-43 self-healing-schema dual-dialect discipline.
- **D-10:** This **amends SC3's literal wording** ("the missing index … no longer full-scans") to "an optimal index (partial expression `LOWER(commander_name) WHERE processed=1`) serves the count + paged queries; EXPLAIN shows index use, not a full scan." Honors SC3/GRID-02 *intent* (source query is fast, index-backed) without pretending a non-existent gap. If EXPLAIN unexpectedly shows the existing indexes do NOT serve the query, fall back to simply adding the partial index (no drop) and document why.

### Claude's Discretion
- Exact partial-view file name/location (`Views/AdminHarvest/_CommandersGrid.cshtml` or similar) and the view model passed to it.
- Whether pagination re-binds via event delegation on a stable parent vs re-attaching listeners after each innerHTML swap (planner/UI-spec choose; delegation preferred to survive swaps).
- The `EnsureSchemaAsync` idempotency form for the index swap (drop-if-exists old + create-if-not-exists new), consistent with the existing index blocks.
- Test placement: controller-level test for the new action's SameOrigin 403 + partial render (DeckFlow.Web.Tests); repository/index assertions (DeckFlow.Core.Tests) including an EXPLAIN-plan assertion if feasible in SQLite CI.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope
- `.planning/ROADMAP.md` §"Phase 44: Admin Grid Lazy Paging" — goal + 4 success criteria (note SC3 amended by D-10)
- `.planning/REQUIREMENTS.md` — GRID-01, GRID-02

### Code being changed
- `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` — `Index` action @73-116 (remove count@97 + paged@100 calls; add empty placeholder); `Status` action @122-150 = the SameOrigin+403+cache pattern to copy for the new `commanders` action (`IsValid`@125, 403@127-129)
- `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` — grid section @218-269 (extract to partial; leave placeholder + JS hook); page display @220, prev/next nav @257-268
- `DeckFlow.Web/wwwroot/ts/admin-harvest.ts` — existing fetch+JSON pattern @92-111 (extend for the innerHTML-swap auto-loader)
- `DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs` — `DefaultDeckPageSize = 25` @12
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — `EnsureSchemaAsync` schema+indexes @59-137 (index defs @114-115); paged query @354-390; count query @396-410
- `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` — wrappers @172-189; `DeckFlow.Web/Services/ICategoryKnowledgeStore.cs` @83-93
- `DeckFlow.Web/Security/SameOriginRequestValidator.cs` — `IsValid` @17-33, `GetForbiddenMessage` @39-40

### Pattern precedents
- `AdminContentKbController` 403 responses (SameOrigin guard reuse across admin endpoints)
- Phase 43 dual-dialect self-healing schema discipline ([[project_phase43_planned]]) — same SQLite/Postgres branch approach for the index DDL

### Design contract (next step)
- `44-UI-SPEC.md` — to be produced by `/gsd-ui-phase 44`; owns loading-state + pagination VISUAL treatment. Planner reads it once it exists.

</canonical_refs>

<code_context>
## Reusable Assets & Patterns

- **SameOrigin-guarded endpoint:** `Status` (`AdminHarvestController.cs:122-150`) is the direct template for the new `commanders` action — same `IsValid`→403 guard; the new one returns `PartialView` instead of `Json`.
- **Existing AJAX TS:** `admin-harvest.ts:92-111` already does `fetch` with `credentials:'same-origin'` and a `render()` step; extend it with an innerHTML-swap loader + page-link delegation (no new TS file required).
- **Index DDL pattern:** `EnsureSchemaAsync` @106-123 already emits multiple `CREATE INDEX` statements with a dialect branch — add the consolidated partial index (and drop the redundant pair) in the same idempotent style.
- **Grid markup to extract:** `Index.cshtml:218-269` is the table + pagination to move into the partial verbatim, then drive numbered paging.
- **No PartialView precedent:** this phase introduces the first `PartialView()` return in `DeckFlow.Web` — keep it conventional (action returns `PartialView("_CommandersGrid", vm)`).

</code_context>

<deferred>
## Deferred Ideas
- Server-side caching of the commander count (it changes only when harvest processes new decks) — perf nicety, not required; revisit if the count query is still hot after the index fix.
- Replacing prev/next full-page links elsewhere in admin with the same AJAX partial pattern — generalize later if this lands well.
- Page-size selector (10/25/50) — UX nicety, out of scope (keep 25).
</deferred>

<scope_fence>
## Scope Fence
- Only `/Admin/Harvest` (controller, its view, its TS, the backing repository index). No other pages.
- No change to grid columns/data or the harvest/distill pipeline.
- No new NuGet packages; no new TS framework.
- Visual styling (spinner/skeleton/pagination look) belongs to `44-UI-SPEC.md`, not the planner's invention.
- SC3 is amended by D-10 (verify-and-consolidate, not "add a missing index") — do not blindly add an index that duplicates the existing `:115` composite.
</scope_fence>
