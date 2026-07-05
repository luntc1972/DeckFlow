---
phase: 86-ui-audit-re-score-studio-stage-4-admin-flags-closeout
plan: 01
subsystem: ui
tags: [css, theming, color-mix, accent-tokens, accessibility]

# Dependency graph
requires: []
provides:
  - "prompt-layout-segment hover/active tint derived from var(--accent) in site-common.css"
  - "ui-mode-button.is-active + clear-cache-button:hover tint from var(--accent) in site.css"
  - "sticky-download button hover tint from var(--accent) in site-mobile.css"
  - "same fix mirrored into site-bant.css / site-mardu.css / site-naya.css standalone forks"
affects: [86-02, 86-03, 86-04, 86-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Inline color-mix(in srgb, var(--accent) N%, transparent) accent-tint at call sites (no new named token) — reuses the pattern already present at site-mobile.css:357"

key-files:
  created: []
  modified:
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Web/wwwroot/css/site.css
    - DeckFlow.Web/wwwroot/css/site-mobile.css
    - DeckFlow.Web/wwwroot/css/site-bant.css
    - DeckFlow.Web/wwwroot/css/site-mardu.css
    - DeckFlow.Web/wwwroot/css/site-naya.css

key-decisions:
  - "Used inline color-mix(in srgb, var(--accent) N%, transparent) at each call site per plan design note — not a new named --accent-tint-* token."
  - "Left the .is-active color: var(--accent-strong, var(--accent)) declaration untouched (Phase-84 D2 decorative-brand carve-out); only the background literal was replaced."

patterns-established:
  - "Accent-derived color-mix tint (8% for hover/subtle states, 15% for active/emphasis states) is now the established replacement for any hardcoded blue literal in theme CSS; plan 86-04 reuses this."

requirements-completed: [UIAUDIT-02]

# Metrics
duration: 10min
completed: 2026-07-05
---

# Phase 86 Plan 01: Bug B — Accent-Derived Color-Mix Replaces Hardcoded Jeskai-Blue Summary

**Replaced all 8 hardcoded `rgba(43, 108, 176, …)` literals (base + mobile + 3 forks) with inline `color-mix(in srgb, var(--accent) N%, transparent)`, so every non-Jeskai theme now tints hover/active states with its own accent instead of a fixed blue.**

## Performance

- **Duration:** 10 min
- **Started:** 2026-07-05T20:32:00Z
- **Completed:** 2026-07-05T20:42:00Z
- **Tasks:** 2 completed
- **Files modified:** 6

## Accomplishments
- Eliminated the widest accent-leak in the codebase: `.prompt-layout-segment:hover/:focus-visible` and `.is-active` in `site-common.css` (global, zero theme overrides, inherited by all 24 themes) now derive their tint from `var(--accent)`.
- Fixed the `[data-prompt-ui-mode-button].is-active` background and `.clear-cache-button:hover` background in `site.css` (Classic theme) and its three literal-bearing forks (bant, mardu, naya).
- Fixed the sticky-download button hover/focus background in `site-mobile.css` (same literal, mobile-only rule, not a ui-mode button as initially named in some notes).
- Confirmed via grep that `site-jeskai.css`'s four `rgba(43,108,176,…)` uses are untouched — those are Jeskai's own legitimate accent, correctly excluded from the gate.

## Task Commits

Each task was committed atomically:

1. **Task 1: Replace the literal in the global base + mobile rules** - `dd3c69c3` (fix)
2. **Task 2: Mirror the color-mix into the three literal-bearing forks (bant, mardu, naya)** - `1dbe26b2` (fix)

## Files Created/Modified
- `DeckFlow.Web/wwwroot/css/site-common.css` - `.prompt-layout-segment` hover/focus-visible bg → `color-mix(... 8% ...)`; `.is-active` bg → `color-mix(... 15% ...)`
- `DeckFlow.Web/wwwroot/css/site.css` - `[data-prompt-ui-mode-button].is-active` bg and `.clear-cache-button:hover` bg → `color-mix(... 8% ...)`
- `DeckFlow.Web/wwwroot/css/site-mobile.css` - `.prompt-sticky-download__button:hover/:focus-visible` bg → `color-mix(... 8% ...)`
- `DeckFlow.Web/wwwroot/css/site-bant.css` - same two rules as site.css mirrored
- `DeckFlow.Web/wwwroot/css/site-mardu.css` - same two rules as site.css mirrored
- `DeckFlow.Web/wwwroot/css/site-naya.css` - same two rules as site.css mirrored

## Decisions Made
- Followed the plan's explicit design note: inline `color-mix` at each call site, not a new named token — "accent-derived color-mix," not "tokenize."
- Kept the adjacent `color: var(--accent-strong, var(--accent))` line on `.prompt-layout-segment.is-active` completely untouched per the Phase-84 D2 carve-out noted in the plan's interfaces block.

## Deviations from Plan

None - plan executed exactly as written. Both `<automated>` verify gates passed on the first attempt for each task.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Bug B (accent-leak on layout-segment/ui-mode-button/clear-cache) is fully closed; the `color-mix` pattern is now proven at 8 call sites for plan 86-04 (Bug D) to reuse.
- Visual/human confirmation of the tint rendering correctly across themes is deferred to the consolidated human-verify checkpoint in plan 86-05, per the plan's own verification note.
- No blockers for 86-02 (Bug A / filled-pill active tab), which is independent of this plan's files.

---
*Phase: 86-ui-audit-re-score-studio-stage-4-admin-flags-closeout*
*Completed: 2026-07-05*

## Self-Check: PASSED
- FOUND: dd3c69c3 (Task 1 commit)
- FOUND: 1dbe26b2 (Task 2 commit)
- FOUND: DeckFlow.Web/wwwroot/css/site-common.css
- FOUND: .planning/phases/86-ui-audit-re-score-studio-stage-4-admin-flags-closeout/86-01-SUMMARY.md
