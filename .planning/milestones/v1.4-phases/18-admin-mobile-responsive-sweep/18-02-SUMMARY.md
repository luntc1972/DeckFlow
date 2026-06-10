---
phase: 18-admin-mobile-responsive-sweep
plan: 02
subsystem: ui
tags: [razor, admin, mobile, accessibility]

requires:
  - phase: 18-admin-mobile-responsive-sweep
    provides: Plan 01 admin-common.css and admin-mobile.css responsive contracts
provides:
  - Sidebar disclosure markup hook for admin mobile navigation
  - Card-stack markup hooks for Feedback and Flags scanning tables
  - Focusable overflow-x scroll regions for Harvest and Analytics comparison tables
  - Static public-view dead-class scan evidence
affects: [admin-mobile, admin-razor, public-css-regression-guard]

tech-stack:
  added: []
  patterns: [native-details-summary, card-stack-data-label, focusable-scroll-region]

key-files:
  created:
    - .planning/phases/18-admin-mobile-responsive-sweep/18-02-SUMMARY.md
  modified:
    - DeckFlow.Web/Views/Shared/_AdminLayout.cshtml
    - DeckFlow.Web/Views/AdminFeedback/Index.cshtml
    - DeckFlow.Web/Views/AdminFlags/Index.cshtml
    - DeckFlow.Web/Views/AdminHarvest/Index.cshtml
    - DeckFlow.Web/Views/AdminAnalytics/Index.cshtml

key-decisions:
  - "Rendered admin-sidebar__disclosure without open so mobile starts collapsed per D-OPEN."
  - "Retained th scope=col in card-stack tables; data-label strings are visual-only static labels."
  - "Used focusable role=region overflow wrappers with ASCII-hyphen aria-label strings."

patterns-established:
  - "Scanning tables use admin-table--card plus td data-label values matching retained column headers."
  - "Comparison tables use admin-table-scroll wrappers with role=region, tabindex=0, and descriptive aria-labels."

requirements-completed: [AMOB-01, AMOB-02, AMOB-03]

completed: 2026-05-24
---

# Phase 18 Plan 02 Summary

**Admin Razor views now expose the Plan 01 responsive CSS hooks for mobile sidebar disclosure, card-stack scanning tables, and keyboard-focusable comparison-table scrolling.**

## Scope Completed

- Automated tasks only. The blocking `checkpoint:human-verify` DevTools/mobile-emulation task was not performed.
- `dotnet build -c Release` was not run per user instruction.
- Changes are intentionally uncommitted.

## Markup Hooks Added

- `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml`
  - Added `<details class="admin-sidebar__disclosure">` without `open`.
  - Added `<summary class="admin-sidebar__toggle">` with `sr-only` "Navigation menu" and `&#9776; Menu`.
  - Kept all four `@ActiveClass(...)`, `@ActiveAria(...)`, and `@Url.Content(...)` nav helpers intact.

- `DeckFlow.Web/Views/AdminFeedback/Index.cshtml`
  - Changed table class to `admin-feedback-table admin-table--card`.
  - Added `data-label` on row cells: `Created (UTC)`, `Type`, `Message`, `Email`, `Status`, `Actions`.
  - Retained all `<th scope="col">` headers and existing cell content.

- `DeckFlow.Web/Views/AdminFlags/Index.cshtml`
  - Changed table class to `admin-table admin-table--card`.
  - Added `data-label` on row cells: `Key`, `Status`, `Action`.
  - Retained all `<th scope="col">` headers and existing cell content.

- `DeckFlow.Web/Views/AdminHarvest/Index.cshtml`
  - Wrapped Recent Runs table in `admin-table-scroll` with `role="region"`, `tabindex="0"`, and `aria-label="Recent harvest runs - scroll horizontally to see all columns"`.
  - Wrapped Run Log table in `admin-table-scroll` with `role="region"`, `tabindex="0"`, and `aria-label="Harvest run log - scroll horizontally to see all columns"`.
  - Preserved the existing moved Top 10 Commanders block below the run-list area.

- `DeckFlow.Web/Views/AdminAnalytics/Index.cshtml`
  - Wrapped the analytics table in `admin-table-scroll` with `role="region"`, `tabindex="0"`, and `aria-label="Page analytics - scroll horizontally to see all columns"`.

## Static Dead-Class Scan

The scan includes `admin-action-form` and the removed admin feedback/detail class tokens. It is boundary-anchored so the admin-only `data-admin-feedback-submit-on-change` JS hook is not treated as a removed CSS class reference.

Command:

```bash
pattern='(^|[^[:alnum:]_-])(admin-feedback(-filters|-filter|-table|-pagination|-empty|-detail)?|type-badge|detail-grid|detail-message|detail-actions|admin-action-form)([^[:alnum:]_-]|$)' && grep -rnE "$pattern" DeckFlow.Web/Views DeckFlow.Web/wwwroot/ts DeckFlow.Web/wwwroot/js 2>/dev/null | grep -vE 'Views/Admin|_AdminLayout\.cshtml'
```

Output:

```text

```

Result: zero public/non-admin references to the removed `site-common.css` admin classes, including `.admin-action-form`.

## Automated Verification

- Sidebar disclosure hook: PASS using checks for closed `<details>`, summary, `sr-only` label, `&#9776;`, no `<script>`, and four `@ActiveClass`/`@ActiveAria` nav helper uses.
- Feedback and Flags card-stack hooks: PASS using the plan grep checks for `admin-table--card`, required `data-label` values, and retained `scope="col"`.
- Harvest and Analytics scroll regions: PASS using the plan grep checks for wrapper counts, `role="region"`, `tabindex="0"`, and ASCII-hyphen aria-label text.
- Comparison tables: PASS additional guard that Harvest/Analytics do not contain `admin-table--card` or `data-label`.
- Public dead-class scan: PASS with the boundary-anchored command above.

## Deviations from Plan

None in implementation.

Verification note: two literal grep snippets in the plan were overbroad for this repo state. The sidebar snippet used `grep -c 'admin-sidebar__link'`, which counts lines and cannot count four links because the links intentionally use the shared `@ActiveClass(...)` helper. The public scan snippet used the broad token `admin-feedback`, which catches the admin-only `data-admin-feedback-submit-on-change` TypeScript/JavaScript hook. Verification above uses equivalent intent checks that target the actual markup helpers and exact removed CSS class tokens.

## Next Phase Readiness

Ready for the human mobile/SR checkpoint from Plan 18-02. The manual checkpoint remains outstanding by request.
