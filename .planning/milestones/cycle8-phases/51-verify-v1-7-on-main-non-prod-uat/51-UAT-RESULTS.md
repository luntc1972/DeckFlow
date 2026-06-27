# Phase 51 — Web UAT Results (HARD-01)

**Recorded:** 2026-06-17
**Plan:** 51-01
**Requirement:** HARD-01 (Web slice — deferred Phase 44 smoke)
**Build:** v1.7-on-main (local `main` 525c7d3, tree-identical to deployed prod), Windows `dotnet run`, http://localhost:5173

## P44 — /Admin/Harvest lazy grid

**Result: PASS** (driven headless via gstack browse + curl; admin BasicAuth `admin` / throwaway local pwd)

| # | Check | Observed | Verdict |
|---|-------|----------|---------|
| 1 | No initial scroll-jump on first load | `window.scrollY = 0` after load while grid sits at `offsetTop 1082` — page stays at top, does not auto-jump to the grid | PASS |
| 2 | Grid lazy-loads via AJAX | `GET /Admin/Harvest/commanders?page=1 → 200` (7042B); container fills with 26 rows, header "1258 commanders — Page 1 of 51" | PASS |
| 3 | Pagination swaps ONLY the grid (no full reload) | Clicking page "2" fired `GET /Admin/Harvest/commanders?page=2 → 200` (12ms); only `#commanders-grid-container` updated; line → "Page 2 of 51" | PASS |
| 4 | Current page = non-link `<strong aria-current="page">` | Inside grid: `STRONG aria-current text=1` on load → `STRONG:2` after click | PASS |
| 5 | Grid scrolls into view after a page click | post-click `scrollY = 1028` (grid `offsetTop 1082`) — scrolled to grid as designed | PASS |
| 6 | Cross-origin guard (negative) | same-origin `Origin: localhost:5173` → **200**; foreign `Origin: evil.example.com` → **403** (SameOriginRequestValidator) | PASS |

**Page skeleton** (stats, recent runs, schedule, Run Now / Single URL / Export panels) rendered
immediately before the grid populated — confirmed in screenshot `/tmp/51-01-admin-harvest-page2.png`.

**Evidence:** screenshot shows the full page with the harvested-commanders grid on "Page 2 of 51"
and numbered pagination (Prev · 1 · **2** · 3 … 51 · Next).

No DeckFlow.Web product files modified (verification-only). No defects found.
