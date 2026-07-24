---
phase: 110
plan: 03
title: Jump Navigation with Collision Resolution (CLUP-06/07/14)
status: complete
completed: 2026-07-24
requirements_addressed: [CLUP-06, CLUP-07, CLUP-14]
executor: codex (gpt-5.4 medium)
verifier: claude
---

# Plan 110-03 Summary — Cut Lab Jump Navigation

## What was built
A Cut-Lab-only `<nav class="cutlab-anchor-nav" aria-label="Jump to section">` patterned on
Manabase's anchor-nav, sticky on mobile, linking to the section ids created in 110-02.
Progressive enhancement only — the shared `_WorkflowStepTabs` partial is untouched (D-01).

- **Markup (CutLab.cshtml):** "On this page" nav with 10 in-page links — 4 step panels
  (Process/Decide/Goals/Export) + 6 sub-sections (lock-pool, structural, role-floors,
  cut-rounds, tune, cuts-made). Plain hash anchors, no `aria-current`/per-link state (D-11).
- **CSS (site-common.css):** static desktop treatment reusing `.manabase-anchor-nav` values;
  inside `@media (max-width: 640px)` the nav is `position: sticky; top: 0; z-index: 15` with an
  opaque `var(--panel-soft-bg, var(--panel))` background + matching box-shadow, rendered as a
  horizontally-scrollable pill row, height budget = `--cutlab-anchor-nav-stuck-height: 4rem`.
- **TS (cut-lab.ts):** `attachAnchorNavHandler` scoped to `.cutlab-anchor-nav a[href^="#"]`:
  expands a collapsed target `<details>` (reusing the 110-02 collapse module, D-24), scrolls
  with `prefers-reduced-motion` honored (mirrors admin-harvest.ts), then sets `tabindex="-1"`
  and `.focus({preventScroll:true})` so keyboard/SR users land in the section (D-12). No scroll
  listener added (D-08); step-tab submit seam never re-derived (D-04).

## LOW collision fix — LAYOUT, not z-index
The sticky nav and the existing `.cutlab-sticky-bar` no longer share `top:0`. In the mobile
breakpoint `.cutlab-sticky-bar` gets `top: var(--cutlab-anchor-nav-stuck-height, 4rem)` so the
two stuck bands are disjoint (nav 0–4rem; bar starts at 4rem). z-index (15 < the bar's 20) is
paint-order only, not the fix. 110-06 asserts non-overlapping rectangles across themes.
(Note: the var is scoped to the nav; the bar resolves the 4rem *fallback*, which equals the
declared budget — outcome is correct and stable.)

## Tests
New `cut-lab-jump-nav.test.ts` (jsdom): click expands a collapsed target before focusing;
focus lands on the target (tabindex -1); reduced-motion selects 'auto'; step-tab submit
buttons are never modified by the handler.

## Verification (claude)
- `dotnet build DeckFlow.Web` — clean 0/0.
- `npx tsc --noEmit` — clean.
- `npx vitest run` (full) — 89/89 across 21 files.
- EOL: all four files LF, no churn.
- Grep gates: nav=1, aria-label present, hrefs=10 (all resolve to real ids), _WorkflowStepTabs
  unchanged, site.css anchor-nav=0, collision-offset var present, reduced-motion + tabindex
  present, 0 new scroll listeners. All pass.

## Files changed
- DeckFlow.Web/Views/Deck/CutLab.cshtml
- DeckFlow.Web/wwwroot/css/site-common.css
- DeckFlow.Web/wwwroot/ts/cut-lab.ts
- DeckFlow.Web/ts-tests/cut-lab-jump-nav.test.ts (new)
