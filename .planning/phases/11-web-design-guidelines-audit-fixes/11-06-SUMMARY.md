---
phase: 11
plan: 06
subsystem: ui-accessibility
tags: [a11y, html, razor, screen-reader, tables, caption, scope]
dependency_graph:
  requires:
    - 11-03 (Razor selected=@(condition) sweep — preserved in AdminHarvest + DeckSync)
    - 11-04 (CSP inline-handler removal — preserved in AdminFeedback Index data-attr migration)
  provides:
    - WDG-06 compliant table semantics across all audit-flagged tables
    - .sr-only utility available in site-common.css (22 themes) + admin.css
  affects:
    - DeckFlow.Web/Views/AdminFlags/Index.cshtml
    - DeckFlow.Web/Views/AdminFeedback/Index.cshtml
    - DeckFlow.Web/Views/AdminHarvest/Index.cshtml
    - DeckFlow.Web/Views/Deck/DeckSync.cshtml
    - DeckFlow.Web/Views/Commander/CommanderCategories.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Web/wwwroot/css/admin.css
tech_stack:
  added: []
  patterns:
    - "<caption class=\"sr-only\"> for screen-reader-only table description"
    - "<th scope=\"col\"> on every column-header cell"
    - ".sr-only utility lives in site-common.css (per CLAUDE.md D-07) with mirror in admin.css since admin shell does not load site-common.css"
key_files:
  created: []
  modified:
    - DeckFlow.Web/Views/AdminFlags/Index.cshtml
    - DeckFlow.Web/Views/AdminFeedback/Index.cshtml
    - DeckFlow.Web/Views/AdminHarvest/Index.cshtml
    - DeckFlow.Web/Views/Deck/DeckSync.cshtml
    - DeckFlow.Web/Views/Commander/CommanderCategories.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Web/wwwroot/css/admin.css
decisions:
  - "Use sr-only captions everywhere rather than visible captions — descriptive labels would compete with the existing <h2>/<h3> section headings and the result-panel headings just above each table. Screen readers still announce the caption text when entering the table; sighted users already see the heading context."
  - "Add .sr-only to site-common.css AND admin.css rather than relying on the existing copy in site.css. Admin shell does not load site-common.css or site.css — it loads only admin.css. The existing site.css copy also only covered 13 of 25 guild theme files (12 theme forks are missing it). Per CLAUDE.md D-07, cross-cutting utilities belong in site-common.css so all 22 guild themes inherit; admin.css gets a parallel declaration since it is a standalone shell."
  - "Add caption + scope=col to BOTH DeckSync conflict tables (live JS results AND noscript fallback) rather than only the JS path. The noscript table has 4 columns and is rendered when JavaScript is disabled — a11y must work without JS."
metrics:
  duration: "~25min"
  completed: 2026-05-13
  tasks_completed: 2
  files_modified: 8
---

# Phase 11 Plan 06: Sweep 6 — Table Caption + Scope a11y Sweep — Summary

Add `<caption>` + `<th scope="col">` semantics to all audit-flagged tables (AdminFlags, AdminFeedback Index, AdminHarvest recent runs + run log, DeckSync conflict tables, CommanderCategories, ChatGptCedhMetaGap reference picker) so screen readers announce table purpose and associate data cells with the correct column header. Closes WDG-06.

## Tasks Completed

### Task 1: Add captions + scope="col" to AdminFlags, AdminFeedback Index, AdminHarvest tables

Updated four admin tables (one in AdminFlags, one in AdminFeedback Index, two in AdminHarvest) to include a `<caption class="sr-only">` as the first child of `<table>` and `scope="col"` on every column-header `<th>`. Verified the 11-03 `selected="@(condition ? "selected" : null)"` patterns in AdminHarvest (lines 40, 90) remained intact and the 11-04 `data-admin-feedback-submit-on-change` data-attribute migration in AdminFeedback Index was not regressed.

Also added the `.sr-only` utility class to `site-common.css` (so all 22 guild themes inherit per CLAUDE.md D-07) and mirrored it to `admin.css` (admin shell does not load site-common.css). The site.css copy at line 76 remains in place but only covered 13 of 25 guild themes prior to this plan — promoting the rule to site-common.css fixes the 12 themes that did not previously have it.

**Files modified:** `DeckFlow.Web/Views/AdminFlags/Index.cshtml`, `DeckFlow.Web/Views/AdminFeedback/Index.cshtml`, `DeckFlow.Web/Views/AdminHarvest/Index.cshtml`, `DeckFlow.Web/wwwroot/css/site-common.css`, `DeckFlow.Web/wwwroot/css/admin.css`
**Commit:** `9e86076`
**Verification:** `dotnet build DeckFlow.sln --configuration Release` → 0 warnings, 0 errors. All 8 grep acceptance assertions pass (caption + scope on each file, AdminHarvest >=2 captions, 11-03 + 11-04 guards intact).

### Task 2: Add captions + scope="col" to DeckSync, CommanderCategories, ChatGptCedhMetaGap tables

Updated three result-panel tables across the deck-tools surface to add `<caption class="sr-only">` and `scope="col"` on column-header cells. DeckSync has two conflict tables (live JS results path + noscript fallback) — both got the same treatment for a11y parity in JS-on and JS-off rendering. CommanderCategories has a single 3-column category breakdown table. ChatGptCedhMetaGap has the EDH Top 16 reference deck picker table (9 columns).

The 11-03 `selected="@(condition ? "selected" : null)"` patterns in DeckSync are preserved — `grep -c` shows 13 occurrences (well above the 5 floor in the acceptance criteria).

**Files modified:** `DeckFlow.Web/Views/Deck/DeckSync.cshtml`, `DeckFlow.Web/Views/Commander/CommanderCategories.cshtml`, `DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml`
**Commit:** `665f118`
**Verification:** `dotnet build DeckFlow.sln --configuration Release` → 0 warnings, 0 errors. All 6 grep acceptance assertions pass.

## Verification

| Check | Result |
| ----- | ------ |
| `dotnet build DeckFlow.sln --configuration Release` | 0 warnings, 0 errors |
| AdminFlags: caption + scope=col present | OK |
| AdminFeedback Index: caption + scope=col present | OK |
| AdminHarvest: >=2 captions + scope=col present | OK (2 captions, recent runs + run log) |
| AdminHarvest 11-03 `selected="@(` preserved | OK |
| AdminFeedback 11-04 `onchange="this.form.submit()"` removed | OK (data-attr instead) |
| DeckSync: caption + scope=col present | OK (both tables) |
| DeckSync 11-03 `selected="@(` >=5 occurrences | OK (13 occurrences) |
| CommanderCategories: caption + scope=col present | OK |
| ChatGptCedhMetaGap: caption + scope=col present | OK |

UAT deferred per phase D-03 — phase-end UAT will spot-check screen reader announcement on one admin table + one deck-view table after all 10 sweeps land.

## Deviations from Plan

### Filename mismatch (documented, not a regression)

**1. [Rule 3 - Blocking] Plan listed `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml`; the file on disk is `ChatGptCedhMetaGap.cshtml`**
- **Found during:** Task 2 (pre-edit read)
- **Issue:** The plan + plan frontmatter both name `CedhMetaGap.cshtml`. That filename does not exist; the actual file is `ChatGptCedhMetaGap.cshtml`. The plan was written anticipating Phase 12's AI-agnostic URL/file rename, which has not landed yet (Phase 12 is still upcoming per ROADMAP.md).
- **Fix:** Edited the actual file `ChatGptCedhMetaGap.cshtml` rather than creating a new `CedhMetaGap.cshtml`. Phase 12 will rename it later as part of the AI-agnostic rename; the caption + scope=col additions will travel through the rename intact.
- **Files modified:** `DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml`
- **Commit:** `665f118`

### Out-of-plan addition (documented as deviation)

**2. [Rule 2 - Missing Critical Functionality] Added `.sr-only` to both `site-common.css` and `admin.css`**
- **Found during:** Task 1 read of `site-common.css` (search for `.sr-only`)
- **Issue:** `.sr-only` was already defined in `site.css` (line 76) and 12 of the 24 guild theme forks — but NOT in `site-common.css` and NOT in `admin.css`. Admin pages load only `admin.css` (`_AdminLayout.cshtml:14`), so admin captions using `class="sr-only"` would have been visible (a regression). 12 guild themes (Azorius, Dimir, Rakdos, Gruul, Selesnya, Orzhov, Izzet, Golgari, Boros, Simic, Temur, Commander Table) were also missing the utility entirely.
- **Fix:** Added `.sr-only` to `site-common.css` under a Sweep 6 banner (so all 22 guild themes inherit per CLAUDE.md D-07) AND to `admin.css` as a parallel declaration. The existing site.css definition is unchanged.
- **Files modified:** `DeckFlow.Web/wwwroot/css/site-common.css`, `DeckFlow.Web/wwwroot/css/admin.css`
- **Commit:** `9e86076`

## Threat Flags

No new security-relevant surface introduced. Caption/scope additions are pure markup metadata.

## Self-Check: PASSED

- DeckFlow.Web/Views/AdminFlags/Index.cshtml: FOUND
- DeckFlow.Web/Views/AdminFeedback/Index.cshtml: FOUND
- DeckFlow.Web/Views/AdminHarvest/Index.cshtml: FOUND
- DeckFlow.Web/Views/Deck/DeckSync.cshtml: FOUND
- DeckFlow.Web/Views/Commander/CommanderCategories.cshtml: FOUND
- DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml: FOUND
- DeckFlow.Web/wwwroot/css/site-common.css: FOUND
- DeckFlow.Web/wwwroot/css/admin.css: FOUND
- Commit 9e86076 (Task 1): FOUND in git log
- Commit 665f118 (Task 2): FOUND in git log
