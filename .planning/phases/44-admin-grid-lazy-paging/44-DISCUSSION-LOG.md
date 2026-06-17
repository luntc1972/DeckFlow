# Phase 44: Admin Grid Lazy Paging - Discussion Log

**Date:** 2026-06-13
**Mode:** discuss (default)

Human-reference record of the discussion. Canonical decisions live in `44-CONTEXT.md`.

## Areas discussed (all 4 selected)

### 1. Grid render mechanism
- Options: PartialView (Razor) | JSON + client-side render
- **Chosen:** PartialView (Razor) → D-01/D-02. Razor stays single source of row markup; first PartialView() in DeckFlow.Web.

### 2. Index strategy (GRID-02)
- Finding surfaced: composite `(processed, LOWER(commander_name))` index already exists @115 — likely already serves the query; SC3's "missing index" premise largely false.
- Options: Verify-first then consolidate | Add partial as specified | Confirm existing suffices, no new index
- **Chosen:** Verify-first, then consolidate → D-08/D-09/D-10. EXPLAIN both queries; replace the 2 overlapping indexes with 1 partial expression index `LOWER(commander_name) WHERE processed=1`; dual-dialect; amend SC3 wording to honor intent.

### 3. Auto-load + endpoint shape
- Options: Auto-fire page=1 reusing Status guard | Explicit Load button
- **Chosen:** Auto-fire page=1 on DOMContentLoaded; new `GET /Admin/Harvest/commanders?page=n` reusing Status's SameOrigin→403 pattern (SC4); "Loading…" + error/retry behavior → D-03/D-04/D-05/D-06. Visual deferred to ui-phase.

### 4. Pagination model + page size
- Options: Numbered + prev/next (keep 25) | Prev/next only (keep 25)
- **Chosen:** Numbered + prev/next, keep DefaultDeckPageSize=25, total count + "Page X of Y" → D-07.

## Deferred ideas
- Server-side count caching; generalize AJAX-partial paging to other admin pages; page-size selector.

## Claude's discretion
- Partial file name/location + view model; pagination re-bind via delegation vs re-attach; index swap idempotency form; test placement (Web.Tests controller 403 + partial; Core.Tests index/EXPLAIN).
