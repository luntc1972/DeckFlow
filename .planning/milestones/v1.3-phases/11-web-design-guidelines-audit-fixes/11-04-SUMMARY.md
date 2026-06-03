---
phase: 11
plan: 04
subsystem: web-design-guidelines
tags: [wdg, csp, accessibility, admin-feedback, razor, typescript, css]
requires: [11-03]
provides:
  - "Inline style= removed from Views/Deck/Error.cshtml; equivalent rules live in site-common.css under .error-page__panel + .error-page__title"
  - "Inline onchange='this.form.submit()' removed from Views/AdminFeedback/Index.cshtml; behavior wired by new wwwroot/ts/admin-feedback.ts via a data-admin-feedback-submit-on-change hook"
  - "AdminFeedback Detail Delete inline onsubmit retained per D-05; deferral comment added per D-06 referencing 260513-wdg-FINDINGS.md"
affects:
  - "App is one step closer to strict CSP (script-src 'self' + style-src 'self') — only the deferred Detail Delete onsubmit remains as a known inline-handler exception"
tech-stack:
  added:
    - "wwwroot/ts/admin-feedback.ts (new module following the admin-analytics.ts / admin-harvest.ts IIFE + DOMContentLoaded pattern)"
  patterns:
    - "Razor view + data-* attribute hook + per-view @section Scripts wiring + TypeScript change listener (replaces inline onchange)"
    - "Scoped CSS class in site-common.css (NOT site.css, per CLAUDE.md D-07) replacing inline style= attributes"
    - "Razor server comment (@* ... *@) for deferral notes — does not ship to browser"
key-files:
  created:
    - "DeckFlow.Web/wwwroot/ts/admin-feedback.ts"
  modified:
    - "DeckFlow.Web/Views/Deck/Error.cshtml"
    - "DeckFlow.Web/wwwroot/css/site-common.css"
    - "DeckFlow.Web/Views/AdminFeedback/Index.cshtml"
    - "DeckFlow.Web/Views/AdminFeedback/Detail.cshtml"
decisions:
  - "Chose Razor server comment @* ... *@ over HTML <!-- ... --> for the Detail.cshtml deferral note — server comments do not ship to the browser, so the audit trail stays in source without bloating delivered HTML. D-06 explicitly allowed Claude's discretion on this."
  - "Used data-admin-feedback-submit-on-change as the hook attribute name (not a more generic data-submit-on-change) to keep the contract explicit and avoid accidental rewiring by future shared modules."
  - "Wired admin-feedback.js via @section Scripts on AdminFeedback/Index.cshtml (per-view), matching the established pattern in AdminAnalytics/Index.cshtml and AdminHarvest/Index.cshtml — _AdminLayout.cshtml exposes RenderSection('Scripts', required: false)."
  - "Used class naming .error-page__panel / .error-page__title (BEM-style, matching site-common.css conventions like .hub-card__title, .about-page__meta, .admin-feedback__... etc.)."
metrics:
  duration: "~6 min"
  completed: "2026-05-13T22:40:21Z"
  tasks_completed: 3
  files_changed: 4
  files_created: 1
---

# Phase 11 Plan 04: WDG Sweep 4 — Inline-handler / inline-style removal Summary

Land WDG-04 by removing inline `style=` from `Views/Deck/Error.cshtml` (replaced with `.error-page__*` classes in `site-common.css`) and inline `onchange="this.form.submit()"` from `Views/AdminFeedback/Index.cshtml` (replaced by a new TypeScript change listener in `wwwroot/ts/admin-feedback.ts` wired via a `data-admin-feedback-submit-on-change` hook). The AdminFeedback Detail Delete `onsubmit="return confirm(...)"` is intentionally retained per D-05 and now carries a single-line Razor deferral comment per D-06; v1.4 will replace it with a styled focus-trapped modal.

## Tasks Executed

| # | Name                                                                       | Commit  | Files                                                                                                                  |
| - | -------------------------------------------------------------------------- | ------- | ---------------------------------------------------------------------------------------------------------------------- |
| 1 | Replace inline style in Error.cshtml with scoped CSS classes               | 18cb742 | `DeckFlow.Web/Views/Deck/Error.cshtml`, `DeckFlow.Web/wwwroot/css/site-common.css`                                     |
| 2 | Remove inline onchange from AdminFeedback/Index.cshtml + wire TS listener  | a207daa | `DeckFlow.Web/Views/AdminFeedback/Index.cshtml`, `DeckFlow.Web/wwwroot/ts/admin-feedback.ts` (new)                     |
| 3 | Add D-06 deferral comment to AdminFeedback/Detail.cshtml                   | d294ad7 | `DeckFlow.Web/Views/AdminFeedback/Detail.cshtml`                                                                       |

## Verification

- `dotnet build DeckFlow.sln --configuration Release` — **Build succeeded · 0 Warning(s) · 0 Error(s)** (run after each task; final run after Task 3 also clean).
- Detail.cshtml diff vs. plan base shows **1 insertion, 0 removals** — the inline `onsubmit` attribute is byte-identical pre/post (verified with `git diff 72bb07d..HEAD -- DeckFlow.Web/Views/AdminFeedback/Detail.cshtml`).
- `admin-feedback.ts` compiled cleanly under TypeScript 6 `strict: true` (no new warnings); output `wwwroot/js/admin-feedback.js` produced by the existing `CompileTypeScriptAssets` MSBuild target (output dir is `.gitignore`d as expected).
- All three per-task acceptance criteria blocks pass:
  - **Task 1:** no `style=` in Error.cshtml; `class="error-page__*"` references resolve to `.error-page__panel` + `.error-page__title` rules in `site-common.css`.
  - **Task 2:** no `onchange="this.form.submit()"` in Index.cshtml; `data-admin-feedback-submit-on-change` selector ties Razor → TS; `~/js/admin-feedback.js` referenced via `@section Scripts`.
  - **Task 3:** Detail.cshtml has `Phase 11`, `D-05`, and `260513-wdg-FINDINGS.md` in the new comment; `onsubmit="return confirm` is still present; `git diff --shortstat` shows 1 insertion / 0 removals.
- No UAT in this plan per D-03 — phase-end UAT will visually confirm Error page styling unchanged and AdminFeedback Index select still submits the form on change.

## Deviations from Plan

None — plan executed exactly as written. Two minor discretionary choices, both explicitly licensed by CONTEXT.md "Claude's Discretion" (§Decisions):

- Used Razor server comment `@* ... *@` rather than HTML `<!-- ... -->` for the Detail.cshtml deferral note — keeps the audit trail in source only, not in delivered HTML. The plan suggested `<!-- ... -->` but D-06 explicitly says "single-line `<!-- … -->` is fine" (i.e., a suggestion, not a mandate); the chosen Razor form is functionally equivalent for audit purposes and strictly preferable for CSP-conscious work.
- Chose `data-admin-feedback-submit-on-change` (per plan's primary suggestion) rather than a shorter generic name, for explicit ownership semantics.

## Authentication / Manual Gates

None encountered.

## Known Stubs

None. No TODO / FIXME / placeholder text added; the deferral comment on `Detail.cshtml` is an intentional, documented forward reference (v1.4 modal pattern) — not a stub.

## Threat Flags

None. The changes do not introduce new network endpoints, auth paths, file-access patterns, or schema changes. They reduce attack surface (closer to strict CSP) rather than expand it.

## Self-Check

- `[ -f DeckFlow.Web/Views/Deck/Error.cshtml ]` — FOUND
- `[ -f DeckFlow.Web/wwwroot/css/site-common.css ]` — FOUND
- `[ -f DeckFlow.Web/Views/AdminFeedback/Index.cshtml ]` — FOUND
- `[ -f DeckFlow.Web/Views/AdminFeedback/Detail.cshtml ]` — FOUND
- `[ -f DeckFlow.Web/wwwroot/ts/admin-feedback.ts ]` — FOUND (created in Task 2)
- `git log --oneline | grep 18cb742` — FOUND (Task 1 commit)
- `git log --oneline | grep a207daa` — FOUND (Task 2 commit)
- `git log --oneline | grep d294ad7` — FOUND (Task 3 commit)

## Self-Check: PASSED
