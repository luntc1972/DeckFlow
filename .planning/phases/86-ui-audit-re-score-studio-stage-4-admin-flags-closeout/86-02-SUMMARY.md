---
phase: 86-ui-audit-re-score-studio-stage-4-admin-flags-closeout
plan: 02
subsystem: ui
tags: [css, themes, wcag, accessibility, step-tab, accent-contrast]

# Dependency graph
requires:
  - phase: 86-01
    provides: "Bug B accent color-mix tokens (non-conflicting file set)"
provides:
  - "Filled-accent-pill active step-tab (.prompt-step-tab.is-active) in base site.css"
  - "Filled-accent-pill mirrored into all 12 standalone theme forks (terminal/winning block in each)"
  - "--accent-contrast token on every theme whose measured white-on-accent contrast fails WCAG 4.5:1"
affects: [86-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Filled-accent-pill active-state template (site-mobile.css:353) now reused base + 12 forks"
    - "Per-theme --accent-contrast token, empirically measured (not from a static checklist)"

key-files:
  created: []
  modified:
    - DeckFlow.Web/wwwroot/css/site.css
    - DeckFlow.Web/wwwroot/css/site-dimir.css
    - DeckFlow.Web/wwwroot/css/site-golgari.css
    - DeckFlow.Web/wwwroot/css/site-abzan.css
    - DeckFlow.Web/wwwroot/css/site-bant.css
    - DeckFlow.Web/wwwroot/css/site-commander-table.css
    - DeckFlow.Web/wwwroot/css/site-esper.css
    - DeckFlow.Web/wwwroot/css/site-grixis.css
    - DeckFlow.Web/wwwroot/css/site-jeskai.css
    - DeckFlow.Web/wwwroot/css/site-jund.css
    - DeckFlow.Web/wwwroot/css/site-mardu.css
    - DeckFlow.Web/wwwroot/css/site-naya.css
    - DeckFlow.Web/wwwroot/css/site-nyx.css
    - DeckFlow.Web/wwwroot/css/site-planeswalker-dark.css
    - DeckFlow.Web/wwwroot/css/site-sultai.css

key-decisions:
  - "Re-measured white-on-accent WCAG contrast for all 24 themes instead of trusting the plan's checklist (which the plan itself flagged as non-authoritative); found 2 additional forks (abzan 2.84:1, esper 2.78:1) that fail 4.5:1 beyond the checklist's planeswalker-dark/nyx, and added --accent-contrast to those too, per Rule 2 (accessibility correctness required by must_haves)."
  - "abzan/jund/sultai each declared .prompt-step-tab.is-active twice; replaced BOTH occurrences in each file with the identical canonical filled-pill block so the terminal (winning) block always resolves correctly regardless of cascade order."
  - "grixis and jeskai previously keyed border-color/color off secondary --grixis-blue/--jeskai-blue tokens instead of the guild's own --accent; switched both to --accent to match the plan's canonical rule and the verify gate."
  - "--accent-contrast values chosen from each theme's own existing dark palette (--bg or a nearby dark token) where that already cleared 4.5:1; nyx needed a custom slightly-darker value since its own --bg (4.46:1) fell just short."

requirements-completed: [UIAUDIT-02]

duration: ~25min
completed: 2026-07-05
---

# Phase 86 Plan 02: Filled-Accent-Pill Active Step-Tab + Dark-Theme Contrast Summary

**Replaced the low-salience active step-tab (same bg as inactive) with a filled `var(--accent)` pill across base site.css + 12 standalone forks, and added empirically-measured `--accent-contrast` tokens to 6 themes whose white-on-accent text fails WCAG 4.5:1.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-07-05T20:44:52Z
- **Tasks:** 2
- **Files modified:** 15

## Accomplishments

- Base `.prompt-step-tab.is-active` in `site.css` is now a filled-accent pill: `border-color: var(--accent); background: var(--accent); color: var(--accent-contrast, #fff); font-weight: 600; box-shadow: 0 0 0 2px color-mix(in srgb, var(--accent) 20%, transparent);` — inherited free by all 11 `@import` themes.
- Mirrored the identical rule into all 12 standalone forks that duplicate the base rule (`site-abzan`, `site-bant`, `site-commander-table`, `site-esper`, `site-grixis`, `site-jeskai`, `site-jund`, `site-mardu`, `site-naya`, `site-nyx`, `site-planeswalker-dark`, `site-sultai`). `site-rakdos.css` left untouched (its existing filled pill already renders acceptably, per plan).
- Fixed the duplicate-late-override hazard: `site-abzan.css` (lines 324 and 1270), `site-jund.css` (lines 323 and 1215), `site-sultai.css` (lines 323 and 1215) each declared `.prompt-step-tab.is-active` twice — replaced **both** occurrences in each file with the identical canonical block, so the terminal (cascade-winning) block is always correct.
- Converted `site-grixis.css` and `site-jeskai.css` off their legacy `--grixis-blue`/`--jeskai-blue` secondary tokens onto the guild's own `--accent`, matching the canonical rule.
- Empirically re-measured WCAG contrast (white text vs. each theme's own `--accent`) for all 24 themes rather than trusting the plan's checklist. Confirmed checklist failures (dimir 3.80:1, golgari 2.88:1, planeswalker-dark 3.43:1, nyx 4.19:1) and discovered two the checklist omitted (abzan 2.84:1, esper 2.78:1). Added `--accent-contrast` to all six.
- Live spot-checked (headless Chromium via Playwright against the running dev server) computed `background-color`/`color` for golgari, dimir, planeswalker-dark, abzan, esper, and nyx active tabs — all six render exactly the intended `--accent` background and `--accent-contrast` text color.

## Task Commits

1. **Task 1: Base filled pill + @import-theme contrast tokens** - `7f9b71ec` (fix)
2. **Task 2: Mirror the filled pill into the 12 standalone forks (terminal block) + fork contrast tokens** - `26d5d33b` (fix)

_Plan metadata commit follows this summary._

## Files Created/Modified

- `DeckFlow.Web/wwwroot/css/site.css` - Base `.prompt-step-tab.is-active` filled-accent pill
- `DeckFlow.Web/wwwroot/css/site-dimir.css` - `--accent-contrast: #0b1020` (3.80:1 -> 4.98:1)
- `DeckFlow.Web/wwwroot/css/site-golgari.css` - `--accent-contrast: #0b0f0a` (2.88:1 -> 6.70:1)
- `DeckFlow.Web/wwwroot/css/site-abzan.css` - filled pill mirrored at BOTH occurrences (:324, terminal :1270); `--accent-contrast: #080905` (2.84:1 -> 7.04:1)
- `DeckFlow.Web/wwwroot/css/site-bant.css` - filled pill mirrored
- `DeckFlow.Web/wwwroot/css/site-commander-table.css` - filled pill mirrored
- `DeckFlow.Web/wwwroot/css/site-esper.css` - filled pill mirrored; `--accent-contrast: #06080c` (2.78:1 -> 7.21:1)
- `DeckFlow.Web/wwwroot/css/site-grixis.css` - filled pill mirrored; switched off `--grixis-blue` onto `--accent` (5.20:1, no token needed)
- `DeckFlow.Web/wwwroot/css/site-jeskai.css` - filled pill mirrored; switched off `--jeskai-blue` onto `--accent` (5.70:1, no token needed)
- `DeckFlow.Web/wwwroot/css/site-jund.css` - filled pill mirrored at BOTH occurrences (:323, terminal :1215); no token needed (6.63:1)
- `DeckFlow.Web/wwwroot/css/site-mardu.css` - filled pill mirrored
- `DeckFlow.Web/wwwroot/css/site-naya.css` - filled pill mirrored; no token needed (4.57:1)
- `DeckFlow.Web/wwwroot/css/site-nyx.css` - filled pill mirrored; `--accent-contrast: #0a0910` (4.19:1 -> 4.73:1; theme's own `--bg` only cleared 4.46:1, so a slightly darker custom value was used)
- `DeckFlow.Web/wwwroot/css/site-planeswalker-dark.css` - filled pill mirrored; `--accent-contrast: #1a1e2e` (3.43:1 -> 4.82:1)
- `DeckFlow.Web/wwwroot/css/site-sultai.css` - filled pill mirrored at BOTH occurrences (:323, terminal :1215); no token needed (4.89:1)

## Decisions Made

- Empirical re-measurement over the checklist (as explicitly instructed by the plan): added `--accent-contrast` to abzan and esper beyond the plan's named list because live WCAG math showed both fail 4.5:1 (2.84:1 and 2.78:1). This satisfies the plan's own `must_haves` truth ("EVERY theme" clears 4.5:1) which takes precedence over the illustrative checklist.
- Chose `--accent-contrast` values from each theme's own dark palette (usually `--bg`) for thematic consistency, except nyx where `--bg` alone (4.46:1) fell just under the threshold, so a slightly darker custom hex was used instead.
- Replaced full duplicate blocks (not just the differing properties) in abzan/jund/sultai's second occurrence, to eliminate any ambiguity about which properties cascade-win.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added `--accent-contrast` to abzan and esper (not in the plan's checklist)**
- **Found during:** Task 2
- **Issue:** The plan's contrast checklist named only planeswalker-dark and nyx as forks needing a contrast token, explicitly flagging itself as "a checklist, not the source of truth." Live measurement showed abzan (2.84:1) and esper (2.78:1) also fail WCAG 4.5:1 for white-on-accent text — an accessibility correctness gap the plan's own `must_haves` require to be closed ("EVERY theme" clears 4.5:1).
- **Fix:** Added `--accent-contrast` tokens to both files' `:root`, using each theme's own `--bg` (7.04:1 and 7.21:1 respectively).
- **Files modified:** `DeckFlow.Web/wwwroot/css/site-abzan.css`, `DeckFlow.Web/wwwroot/css/site-esper.css`
- **Verification:** Recomputed WCAG contrast ratio via standard sRGB relative-luminance formula; confirmed live via headless Playwright computed-style spot-check.
- **Committed in:** `26d5d33b` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 missing-critical/accessibility gap, affecting 2 files)
**Impact on plan:** Necessary for WCAG correctness per the plan's own explicit instruction to re-measure rather than trust the checklist. No scope creep — same task, same file set, same requirement (UIAUDIT-02).

## Issues Encountered

None. Both tasks' `<automated>` verify gates passed on first attempt; build stayed 0 warnings / 0 errors throughout.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The visual-regression + WCAG Playwright specs that formally enforce this contrast fix (`theme-active-affordance.spec.ts`) are created in plan 86-05, per the phase's validation strategy — this plan's grep/contrast gates are the interim proof.
- Live spot-check (headless Chromium, 6 themes) confirms correct rendering ahead of that formal e2e coverage.
- `site-rakdos.css` intentionally untouched; no residual work needed there.

---
*Phase: 86-ui-audit-re-score-studio-stage-4-admin-flags-closeout*
*Completed: 2026-07-05*

## Self-Check: PASSED

All 15 modified files found on disk; both task commits (`7f9b71ec`, `26d5d33b`) found in git log.
