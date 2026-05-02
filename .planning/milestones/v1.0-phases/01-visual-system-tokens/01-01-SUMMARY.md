---
phase: 01-visual-system-tokens
plan: 01
subsystem: visual-tokens
tags: [css, design-tokens, type-scale, ui-vs-01]
requires: []
provides:
  - "site.css :root --fs-xs/sm/base/lg/xl/2xl"
  - "var(--fs-*) consumers in site.css and site-common.css"
affects:
  - DeckFlow.Web/wwwroot/css/site.css
  - DeckFlow.Web/wwwroot/css/site-common.css
tech-stack:
  added: []
  patterns:
    - "CSS custom property type scale rooted in default theme :root"
key-files:
  created: []
  modified:
    - DeckFlow.Web/wwwroot/css/site.css
    - DeckFlow.Web/wwwroot/css/site-common.css
decisions:
  - "Tokens live alongside existing color tokens in classic site.css :root (not a new file or block) so they cascade with --accent-strong et al."
  - "em values left intentionally relative; only absolute rem literals were migrated."
  - "Rounding to nearest of 6 steps applied silently for canonical mappings (0.65→0.75, 0.68→0.75, 0.78→0.85, 0.8→0.85, 0.875→0.85, 0.9→0.95, 1→0.95, 1.4→1.5); each delta is flagged below for the user's smoke check."
metrics:
  duration: "~3 minutes execution time"
  completed: "2026-04-30T17:43:28Z"
  task_count: 3
  file_count: 2
requirements:
  - UI-VS-01
---

# Phase 01 Plan 01: visual-system-tokens — type-scale Summary

Established a 6-step semantic font-size scale (`--fs-xs/sm/base/lg/xl/2xl`) on `site.css :root` and migrated every absolute `font-size` literal in both `site.css` and `site-common.css` to `var(--fs-*)`. The classic-theme rem base (`html { font-size: 15px; }`) and intentionally-relative `em` values were preserved unchanged. Build is clean.

## What Shipped

| Task | Description | Commit |
| ---- | ----------- | ------ |
| 1 | Add 6-step type-scale tokens to `site.css :root` | `3f1dc96` |
| 2 | Replace 21 rem literals in `site.css` with `var(--fs-*)` | `3259e37` |
| 3 | Replace 19 rem literals in `site-common.css` with `var(--fs-*)` | `0b6d901` |

## Token Block

Inserted at the end of the existing `:root` declaration in `DeckFlow.Web/wwwroot/css/site.css` (lines 33–39 of the post-edit file), grouped under a `/* type scale (UI-VS-01) */` comment, immediately after the panel-token block:

```css
  /* type scale (UI-VS-01) */
  --fs-xs:   0.75rem;
  --fs-sm:   0.85rem;
  --fs-base: 0.95rem;
  --fs-lg:   1.05rem;
  --fs-xl:   1.5rem;
  --fs-2xl:  1.9rem;
```

`html { font-size: 15px; }` (now line 47) is untouched — it remains the rem base for classic-theme parity.

## Replacement Counts

| File | rem literals before | rem literals after | `var(--fs-*)` refs after | em values preserved |
| ---- | ------------------- | ------------------ | ------------------------ | ------------------- |
| `site.css` | 21 | 0 | 21 | 2 (`0.75em`, `0.85em`) |
| `site-common.css` | 19 | 0 | 19 | 2 (`0.95em`, `0.75em`) |

`site.css` also retains the lone `font-size: 15px;` rem-base declaration (line 47).

## Rounding Flags

Each entry below is a literal that did NOT map cleanly to one of the 6 token steps and was rounded to the nearest. User should spot-check at smoke-test time on classic theme:

| File | Line (post-edit) | Selector | Before | After (token) | Delta |
| ---- | ---------------- | -------- | ------ | ------------- | ----- |
| `site.css` | 230 | `.info-tooltip` | `0.65rem` | `var(--fs-xs)` = `0.75rem` | +0.10rem (+15%) — small badge text on tooltip; only one site.css use |
| `site.css` | 326 | `.chatgpt-step-eyebrow` | `0.8rem` | `var(--fs-sm)` = `0.85rem` | +0.05rem (+6%) — uppercase eyebrow label |
| `site.css` | 347 | `.chatgpt-step-badge` | `0.8rem` | `var(--fs-sm)` = `0.85rem` | +0.05rem (+6%) — pill badge text |
| `site.css` | 935 | (form/control selector around L935) | `0.8rem` | `var(--fs-sm)` = `0.85rem` | +0.05rem (+6%) |
| `site.css` | 734 | (panel detail around L734) | `0.9rem` | `var(--fs-base)` = `0.95rem` | +0.05rem (+5%) — body-adjacent copy |
| `site.css` | 783 | (panel detail around L783) | `0.9rem` | `var(--fs-base)` = `0.95rem` | +0.05rem (+5%) |
| `site.css` | 1150 | (mobile/responsive override ~L1150) | `0.9rem` | `var(--fs-base)` = `0.95rem` | +0.05rem (+5%) |
| `site.css` | 1293 | (form/listing rule ~L1293) | `0.9rem` | `var(--fs-base)` = `0.95rem` | +0.05rem (+5%) |
| `site-common.css` | 61, 928 | (small label/badge selectors) | `0.68rem` | `var(--fs-xs)` = `0.75rem` | +0.07rem (+10%) — small caption text |
| `site-common.css` | 70, 211, 230 | (label/eyebrow selectors) | `0.78rem` | `var(--fs-sm)` = `0.85rem` | +0.07rem (+9%) |
| `site-common.css` | 590 | `.feedback-error` | `0.875rem` | `var(--fs-sm)` = `0.85rem` | -0.025rem (-3%) — error text, visible on /feedback |
| `site-common.css` | 610 | `.type-badge` | `0.8rem` | `var(--fs-sm)` = `0.85rem` | +0.05rem (+6%) — inline badge |
| `site-common.css` | 183 | (body-copy selector ~L183) | `1rem` | `var(--fs-base)` = `0.95rem` | -0.05rem (-5%) — body copy; **most visible drift** |
| `site-common.css` | 31 | (heading selector ~L31) | `1.4rem` | `var(--fs-xl)` = `1.5rem` | +0.10rem (+7%) — section heading |
| `site-common.css` | 367, 498 | (rules at L367, L498) | `0.9rem` | `var(--fs-base)` = `0.95rem` | +0.05rem (+5%) |
| `site-common.css` | 642, 689, 818 | (rules at those lines) | `0.9rem` | `var(--fs-base)` = `0.95rem` | +0.05rem (+5%) |

**Most likely visible drift to spot-check:**
1. `site-common.css:183` — body-copy declaration shrank from `1rem` to `0.95rem` (about 1px at 15px rem base). If site-common.css line 183 is a primary content selector (e.g. `body`, `main`, page wrapper), this affects everything globally.
2. `site-common.css:31` — heading bumped from `1.4rem` to `1.5rem` (about 1.5px). Will be visible on whichever heading uses it.
3. `site-common.css:590 .feedback-error` — error text shrank from `0.875rem` to `0.85rem` (sub-pixel; minor).

## Build Status

- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj --no-restore` → **0 Warning, 0 Error** (verified after each task and at end).
- TypeScript `tsc -p tsconfig.json` ran clean as part of the build target.
- `ZipDeckFlowBridge` ran clean.

## Verification Results

| Gate | Expected | Actual |
| ---- | -------- | ------ |
| `grep -cE 'font-size:\s*[0-9.]+rem' site.css` | 0 | 0 |
| `grep -cE 'font-size:\s*[0-9.]+rem' site-common.css` | 0 | 0 |
| `grep -cE '^\s*font-size:\s*15px;' site.css` | 1 | 1 |
| `grep -E '^\s*--fs-(xs\|sm\|base\|lg\|xl\|2xl):' site.css \| wc -l` | 6 | 6 |
| `grep -cE 'font-size:\s*var\(--fs-' site.css` | ≥ 21 | 21 |
| `grep -cE 'font-size:\s*var\(--fs-' site-common.css` | ≥ 19 | 19 |
| `grep -cE 'font-size:\s*[0-9.]+em' site.css` | 2 | 2 |
| `grep -cE 'font-size:\s*[0-9.]+em' site-common.css` | ≥ 2 | 2 |
| `dotnet build DeckFlow.Web` | exit 0 | exit 0 |

## Deviations from Plan

None. Plan was executed exactly as written:
- Tokens placed at the end of the existing `:root` block (one of the two placement options the plan offered) so they sit alongside the existing panel-token group rather than splitting the color block.
- All flagged rounding deltas (the canonical mappings the plan pre-approved) were applied silently per the plan's instruction; every delta is documented in the **Rounding Flags** section above for the user's smoke check.

## Open Questions / Visual Drift Risks

1. **`site-common.css:183` body-copy shrink** (`1rem` → `0.95rem`) is the most likely-to-be-noticed change. If this declaration governs the global `body` font on classic theme, every page renders ~1px smaller.
2. **`.feedback-error` text** (`site-common.css:590`) shrank by sub-pixel; should be invisible.
3. **Heading at `site-common.css:31`** grew from `1.4rem` → `1.5rem` (~1.5px); reviewer should verify it doesn't push layout.
4. **`.info-tooltip`** (`site.css:230`) grew from `0.65rem` → `0.75rem` (~1.5px); the tiniest text on the page now displays a touch larger — likely an accessibility *win* but flag it anyway.
5. Theme files were not touched in this plan. Guild-theme overrides of `--fs-*` (if any are added in later plans) are not yet present; classic Jeskai theme only uses the values declared in `site.css :root`.

## Phase Smoke-Check Reminder

Per plan `<verification>`, the user should at end of phase load classic theme and visually compare to production on:
`/`, `/feedback`, `/help`, `/about`, `/sync`.

Pay closest attention to body copy size (likely tied to `site-common.css:183`) and the `/feedback` form's error styling.

## TDD Gate Compliance

N/A — plan is `type: execute`, not `type: tdd`. No RED/GREEN/REFACTOR gates required.

## Self-Check: PASSED

- [x] `DeckFlow.Web/wwwroot/css/site.css` exists and contains 6 `--fs-*` tokens in `:root`.
- [x] `DeckFlow.Web/wwwroot/css/site-common.css` exists and contains 0 rem font-size literals.
- [x] Commit `3f1dc96` exists in git log.
- [x] Commit `3259e37` exists in git log.
- [x] Commit `0b6d901` exists in git log.
- [x] `dotnet build DeckFlow.Web` exits 0 with 0 warnings.
- [x] `## Rounding Flags` section header present (acceptance criterion task 3).
