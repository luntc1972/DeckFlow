---
phase: 48-ui-audit-remediation
plan: "02"
subsystem: ui
tags: [css, svg-icons, typography, elevation, short-form-ux]
dependency_graph:
  requires: [48-01]
  provides: [F1-icons, F2-elevation, F4-typography, F6-cap, F7-empty-state]
  affects: [Home.cshtml, CardLookup.cshtml, JudgeQuestions.cshtml, site-common.css]
tech_stack:
  added: []
  patterns: [inline-svg-icons, css-baseline-elevation, typography-hierarchy, razor-partial]
key_files:
  created:
    - DeckFlow.Web/Views/Shared/_ShortFormFooter.cshtml
  modified:
    - DeckFlow.Web/Views/Deck/Home.cshtml
    - DeckFlow.Web/Views/Deck/CardLookup.cshtml
    - DeckFlow.Web/Views/Deck/JudgeQuestions.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css
decisions:
  - ".hub-group__title restructured to flex row so icon sits inline with section label text"
  - "F2 resting shadow on .hub-card/.deck-form/.result-panel with no !important — theme overrides accepted"
  - "F4 selectors target .field > span / label.field > span / .panel-heading h2 in site-common.css baseline (site.css sets color only, not weight/spacing)"
  - "_ShortFormFooter uses a string @model so callers pass page copy directly as PartialAsync second arg"
metrics:
  duration: "~25 minutes"
  completed: "2026-06-16"
  tasks_completed: 3
  files_changed: 5
---

# Phase 48 Plan 02: UI Audit Remediation — Icons, Elevation, Typography, Short-Form UX Summary

**One-liner:** Inline-SVG icons on all hub cards/section headers (F1), resting card elevation baseline (F2), weight/letter-spacing typography hierarchy (F4), and short-form example panel + content cap (F6/F7) — all CSS in site-common.css, zero theme files touched.

## Tasks Completed

| # | Task | Commit | Files |
|---|------|--------|-------|
| 1 | F1+F2: inline-SVG hub icons + resting elevation | 01b1b10 | Home.cshtml, site-common.css |
| 2 | F4: heading/label typography weight+letter-spacing | 550d369 | site-common.css |
| 3 | F6+F7: short-form cap/center + _ShortFormFooter partial | 3df8a90 | _ShortFormFooter.cshtml, CardLookup.cshtml, JudgeQuestions.cshtml, site-common.css |

## What Was Built

### Task 1 — F1 (Icons) + F2 (Elevation)

**Home.cshtml** — Every hub card and section header gained a hand-authored inline `<svg>` icon:
- 4 section headers (Analyze, Build, Reference, Categories): `<span class="hub-group__icon">` with 16×16 SVG
- 10 hub cards + 1 status div: `<span class="hub-card__icon">` with 20×20 SVG
- All 17 SVGs use `stroke="currentColor"` or `fill="currentColor"` (no hard-coded hex), `aria-hidden="true" focusable="false"` (decorative)
- Icons chosen by tool type: magnifier (lookup/search), chart (analysis), two-columns (comparison), sync-arrows (deck sync/convert), document (primer), question-circle (judge), folder (categories), book (KB), tag/folder (category suggestions)

**site-common.css** additions:
- `.hub-group__title`: `display:flex; align-items:center; gap:0.4em` so icon sits inline with label
- `.hub-group__icon`: `inline-flex; flex-shrink:0; color:var(--muted)`
- `.hub-card__icon`: `inline-flex; flex-shrink:0; color:var(--accent); margin-bottom:0.25rem`
- F2 resting elevation: `.hub-card, .deck-form, .result-panel { box-shadow: 0 1px 2px rgba(0,0,0,0.04), 0 2px 8px rgba(0,0,0,0.04) }` — baseline for themes that do not set their own; no `!important`

### Task 2 — F4 (Typography Hierarchy)

**site-common.css** — Three-tier weight/spacing treatment (system font only, no web-font):
- **Display tier** — `.hero h1`: `font-weight:700; letter-spacing:-0.01em` (tightened display heading)
- **Section-label tier** — `.hub-group__title, .hub-hero__eyebrow`: bumped to `font-weight:700; letter-spacing:0.08em` (consistent eyebrow treatment; was 600 on hub-group__title)
- **Field-label tier** — `.field > span, .field > label > span, label.field > span, .panel-heading h2`: `font-weight:600; letter-spacing:0.02em` (labels separate from body copy)

### Task 3 — F6 (Cap/Center) + F7 (Empty-State Panel)

**`_ShortFormFooter.cshtml`** (new) — Reusable server-side partial accepting a `string` model. Renders a `.short-form-footer` panel with an "Example" eyebrow heading and the caller-supplied hint text. Falls back to a generic prompt when no model supplied.

**CardLookup.cshtml** — Footer partial appended before `@section Scripts` with Sol Ring example text.

**JudgeQuestions.cshtml** — Footer partial appended before `@section Scripts` with judge chat / ChatGPT prompt workflow hint.

**site-common.css** additions:
- `.short-form`: `max-width:64ch; margin:auto` — F6 content cap (optional class for short tool pages)
- `.short-form-footer`: border+radius+panel bg+resting elevation panel matching `.hub-card` baseline
- `.short-form-footer__heading`: 700/uppercase/0.06em (section-label tier within the panel)
- `.short-form-footer__hint`: fs-sm/muted/line-height 1.45

## Verification Results

| Check | Result |
|-------|--------|
| `grep -c "<svg"` Home.cshtml | 17 |
| `grep -c "currentColor"` Home.cshtml | 17 |
| `grep -c 'aria-hidden="true"'` Home.cshtml | 17 |
| `.hub-card__icon` + `.hub-group__icon` in site-common.css | PRESENT |
| Resting elevation selectors in site-common.css | PRESENT |
| `grep -c "letter-spacing"` site-common.css | 13 (was 6) |
| `_ShortFormFooter.cshtml` exists | PRESENT |
| Both CardLookup + JudgeQuestions reference `_ShortFormFooter` | PRESENT |
| `.short-form` + `.short-form-footer` in site-common.css | PRESENT |
| CSS files changed (only site-common.css) | 1 — site-common.css only |
| site.css modified | 0 lines |
| Theme files modified | 0 files |
| Build (Release) | 0 errors, 1 pre-existing CS1574 warning (unrelated) |

## Deviations from Plan

None — plan executed exactly as written.

The plan's `<interfaces>` block noted correctly that `.panel` does not exist as a standalone class; the action used `.hub-card`, `.deck-form`, and `.result-panel` as specified.

## Known Stubs

None. The `_ShortFormFooter` partial has real page-specific copy passed in from each caller. No placeholder text left in shipped markup.

## Threat Flags

None. This plan adds purely presentational CSS and a server-rendered Razor partial with no network endpoints, auth paths, file access patterns, or schema changes.

## Self-Check: PASSED

| Item | Status |
|------|--------|
| DeckFlow.Web/Views/Shared/_ShortFormFooter.cshtml | FOUND |
| DeckFlow.Web/Views/Deck/Home.cshtml | FOUND |
| DeckFlow.Web/Views/Deck/CardLookup.cshtml | FOUND |
| DeckFlow.Web/Views/Deck/JudgeQuestions.cshtml | FOUND |
| DeckFlow.Web/wwwroot/css/site-common.css | FOUND |
| .planning/phases/48-ui-audit-remediation/48-02-SUMMARY.md | FOUND |
| Commit 01b1b10 (Task 1) | FOUND |
| Commit 550d369 (Task 2) | FOUND |
| Commit 3df8a90 (Task 3) | FOUND |
