---
phase: 11
plan: 10
subsystem: web-accessibility
tags: [a11y, aria-live, admin-harvest, razor]
requirements: [WDG-10]
requires: [11-03, 11-06, 11-07]
provides:
  - "ARIA polite live-region semantics on the AdminHarvest status block so screen readers hear each AJAX-poll status update written by admin-harvest.ts render()."
affects:
  - DeckFlow.Web/Views/AdminHarvest/Index.cshtml
tech-stack:
  added: []
  patterns:
    - "WAI-ARIA polite live region (role=\"status\" + aria-live=\"polite\") on a dynamically-updated status block."
key-files:
  created: []
  modified:
    - DeckFlow.Web/Views/AdminHarvest/Index.cshtml
decisions:
  - "HTML-only fix — no JavaScript changes to admin-harvest.ts. The browser fires SR announcements automatically once the live-region attributes are present on the target element, so the AJAX-poll write side (admin-harvest.ts:151 render()) does not need updating."
  - "Attribute order: role=\"status\" then aria-live=\"polite\", placed immediately after the id attribute and before the existing data-harvest-status / data-state attributes."
metrics:
  duration: "≈3 minutes"
  completed: "2026-05-13"
---

# Phase 11 Plan 10: Add ARIA polite live-region to AdminHarvest status block (Sweep 10) Summary

Sweep 10 of the WDG audit lands the smallest fix in Phase 11: two HTML attributes on the `#harvest-status-live` element so screen readers hear each AJAX-poll status update. Delivers the Phase 7 SC #1/#3 SR-announce intent without any JavaScript changes.

## What changed

- `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` — added `role="status"` and `aria-live="polite"` to the `<div id="harvest-status-live">` element at line 54.

Diff (one-line):

```diff
-    <div id="harvest-status-live" data-harvest-status data-state="@activeState">
+    <div id="harvest-status-live" role="status" aria-live="polite" data-harvest-status data-state="@activeState">
```

## Why it matters

The `#harvest-status-live` element is rewritten on every poll cycle by `DeckFlow.Web/wwwroot/ts/admin-harvest.ts:151 render()` (Status / Decks / Started / Elapsed). Before this fix, those DOM mutations were silent for assistive tech. Adding the polite live-region semantics lets screen readers announce each update without interrupting the user — exactly what Phase 7 SC #1/#3 intended when the live region id was named `harvest-status-live` in the first place. WDG audit FINDINGS.md §"P1 finding N" (lines 127-132) called this out as a misnomer-without-implementation; this fix closes that gap.

## Verification

- `dotnet build DeckFlow.sln --configuration Release` — 0 warnings, 0 errors, build succeeded.
- `grep` assertions (all pass):
  - `id="harvest-status-live"` still present on the element.
  - `role="status"` and `aria-live="polite"` are both on the same element as the id (regex check confirms attributes are on the same `<div ...>` opening tag).
- Prior-sweep regression checks on the same file (all pass — the AdminHarvest/Index.cshtml stack is now 4 sweeps deep: 11-03, 11-06, 11-07, 11-10):
  - 11-03: `selected="@(...)"` Razor expressions intact on the duration + interval selects.
  - 11-06: 2 `<caption>` elements intact on the Recent Runs / Run Log tables.
  - 11-07: `autocomplete="url"` AND `inputmode="url"` intact on the Single URL input.
- `admin-harvest.ts` untouched: `git diff --name-only DeckFlow.Web/wwwroot/ts/admin-harvest.ts` is empty.
- `git diff --stat` shows the single targeted addition only (1 insertion, 1 deletion — same line rewritten with two new attributes).

## Deviations from Plan

None — plan executed exactly as written. Single HTML-only attribute addition, no auto-fixes triggered, no Rule 1-4 deviations.

## Commits

- `54e069b` — `fix(11-10): add aria-live polite role status to harvest live region`

## Self-Check: PASSED

- File modified exists and contains both new attributes on the same element:
  - `FOUND: DeckFlow.Web/Views/AdminHarvest/Index.cshtml` (with `role="status" aria-live="polite"` on the `#harvest-status-live` div).
- Commit exists in worktree branch:
  - `FOUND: 54e069b` (`fix(11-10): add aria-live polite role status to harvest live region`).
- Build clean: 0 warnings, 0 errors against `DeckFlow.sln` Release configuration.
- All four prior-sweep regression checks (11-03, 11-06, 11-07) pass on the same file.
- `admin-harvest.ts` confirmed untouched.
