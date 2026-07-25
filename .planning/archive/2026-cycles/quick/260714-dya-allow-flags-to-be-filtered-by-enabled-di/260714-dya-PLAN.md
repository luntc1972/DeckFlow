---
quick_id: 260714-dya
description: allow flags to be filtered by enabled/disabled
date: 2026-07-14
branch: quick/flags-status-filter
must_haves:
  truths:
    - /Admin/Flags offers a status filter (All / Enabled / Disabled) alongside the existing prefix search and namespace chips
    - Status filter composes with prefix chips and search text (AND semantics)
    - Filter state (including status) persists across page reloads via sessionStorage, matching existing search/prefix persistence
    - Count line and empty-row behavior stay correct under the combined filter
  artifacts:
    - DeckFlow.Web/Views/AdminFlags/Index.cshtml (status chip group + data-flag-enabled on rows)
    - DeckFlow.Web/wwwroot/ts/flag-filter.ts (pure statusMatches logic)
    - DeckFlow.Web/wwwroot/ts/admin-flags.ts (status chip wiring + persistence)
    - DeckFlow.Web/ts-tests/flag-filter.test.ts (statusMatches unit coverage)
    - DeckFlow.Web/e2e/admin-flags-filter.spec.ts (status filter e2e + stale-contract fix)
  key_links:
    - View rows carry data-flag-enabled consumed by admin-flags.ts
    - admin-flags.ts calls DeckFlowFlagFilter.statusMatches from flag-filter.ts
---

# Quick Task 260714-dya: Filter flags by enabled/disabled

## Context

/Admin/Flags (AdminFlagsController + Views/AdminFlags/Index.cshtml) renders all
feature flags with a client-side filter: a prefix search box plus namespace
chips (All / service. / analysis.). Pure matching logic lives in
`wwwroot/ts/flag-filter.ts` (global `DeckFlowFlagFilter`, module:"none");
DOM wiring + sessionStorage persistence in `wwwroot/ts/admin-flags.ts`.
Vitest covers the pure logic (`ts-tests/flag-filter.test.ts`); Playwright
covers the page (`e2e/admin-flags-filter.spec.ts`).

Known drift: the e2e spec still targets the old label
("Filter by key prefix, e.g. tool.") and a "tool" chip — the view now says
"e.g. analysis." and tool.* flags were excluded from this page (commit
2026-07-10). The first test is CI-skipped and stale locally.

## Task 1 — Status filter (single task, one commit)

**Files:**
- DeckFlow.Web/Views/AdminFlags/Index.cshtml
- DeckFlow.Web/wwwroot/ts/flag-filter.ts
- DeckFlow.Web/wwwroot/ts/admin-flags.ts
- DeckFlow.Web/ts-tests/flag-filter.test.ts
- DeckFlow.Web/e2e/admin-flags-filter.spec.ts

**Action:**
1. View: add `data-flag-enabled="true|false"` to each `tr[data-flag-key]`.
   Add a second chip group after the namespace chips:
   `<div class="flag-filter__chips" role="group" aria-label="Status filter">`
   with three buttons carrying `data-flag-status` of `""` (All), `"on"`
   (Enabled), `"off"` (Disabled) — reuse `.flag-filter__chip` styling and the
   `is-active`/`aria-pressed` pattern. Buttons must NOT carry
   `data-flag-prefix` (the prefix wiring selects `button[data-flag-prefix]`).
   Label text: "All statuses", "Enabled", "Disabled" (distinct from the
   namespace "All" chip for accessibility/e2e disambiguation).
2. flag-filter.ts: extend `FlagFilterApi` with
   `statusMatches(enabled: boolean, status: string): boolean` —
   `''` → true, `'on'` → enabled, `'off'` → !enabled (unknown values → true).
3. admin-flags.ts: mirror the prefix-chip pattern for status chips
   (`button[data-flag-status]`): `activeStatus` state, `syncActiveChip` per
   group, sessionStorage persistence under `deckflowAdminFlagStatus`,
   restore-on-load with validity check, and
   `isMatch = matchesPrefix && matchesSearch && matchesStatus` where
   `matchesStatus = DeckFlowFlagFilter.statusMatches(row.dataset.flagEnabled === 'true', activeStatus)`.
4. Vitest: add `statusMatches` cases (all/on/off × enabled/disabled, unknown
   status value).
5. E2e: fix the stale contract (label → "Filter by key prefix, e.g. analysis.",
   chips → service/analysis, drop tool expectations) and add status coverage:
   click Enabled → every visible row has `data-flag-enabled="true"`; click
   Disabled → all visible false; combine Disabled + prefix chip and assert
   count line; "All statuses" restores. Keep the CI skip guard on the
   interaction test as-is.

**Verify:** `dotnet build` clean (compiles TS via MSBuild); `npm test`
(vitest) green in DeckFlow.Web; e2e run by orchestrator post-implementation.

**Done:** Status chips filter rows by enabled state, compose with existing
filters, persist across reload; unit + e2e coverage updated.

## Out of scope

- Server-side filtering (client-side only, matches existing pattern)
- tool.* flags page (/Admin/Tools) — separate surface
- CSS changes beyond reusing existing chip classes (add site-common.css rules
  only if two chip groups need spacing)
