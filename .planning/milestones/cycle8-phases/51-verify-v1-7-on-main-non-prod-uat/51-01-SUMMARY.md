# 51-01 Summary — Web /Admin/Harvest lazy-grid smoke (P44 / HARD-01)

**Status:** PASS · **Date:** 2026-06-17

Ran the deferred Phase 44 browser smoke on the v1.7-on-main build (Windows `dotnet run`, :5173),
driven headless via gstack + curl. All checks passed: no initial scroll-jump (`scrollY=0` while
grid at `offsetTop 1082`), grid lazy-loads via `GET /Admin/Harvest/commanders?page=1 → 200`,
pagination click swaps only `#commanders-grid-container` (`?page=2 → 200`, "Page 2 of 51", current
page `<strong aria-current="page">`, scrolls to grid), and the cross-origin negative check passed
(same-origin 200, foreign Origin 403).

No DeckFlow.Web files modified. No defects. Full evidence: `51-UAT-RESULTS.md`.
