---
phase: 86-ui-audit-re-score-studio-stage-4-admin-flags-closeout
plan: 04
subsystem: ui
tags: [css, theming, layout-picker, accessibility, ux]

# Dependency graph
requires:
  - phase: 86-03
    provides: bucket-toggle chevron restyle mirrored into base + 12 forks (same fan-out pattern reused here)
provides:
  - Guaranteed, measurable per-mode CSS delta for the Full/Compact/Advanced (guided/focused/expert) layout
    picker on the empty Step-1 landing, keyed to the always-rendered `.prompt-instructions` element
  - A positive accent style for guided/Full (accent left-border + accent-tinted background) instead of a
    do-nothing default
  - Mirrored mode-delta CSS across base `site.css` + all 12 standalone-fork theme files
affects: [86-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Per-mode structural delta keyed to an always-present element (`.prompt-instructions`) rather than
      sparse/optional text, so the layout picker is regression-testable even on an empty form"
    - "color-mix(in srgb, var(--accent) N%, var(--theme-surface)) accent-tint pattern (reused from 86-01/86-03)
      applied to the guided/Full positive style"

key-files:
  created: []
  modified:
    - DeckFlow.Web/wwwroot/css/site.css
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
  - "expert (Advanced) now sets `.prompt-instructions { display: none; }` — a guaranteed box collapse on an
    element that is always rendered on Step 1, instead of relying on sparse secondary text being present"
  - "focused (Compact) shrinks `.prompt-instructions` padding further (0.6rem/0.7rem vs guided's 0.95rem/1rem)
    and tightens `.prompt-step-heading` gap/margin, producing a measurably smaller box than guided"
  - "guided (Full) gets a positive accent style: `border-left: 4px solid var(--accent)` +
    `background: color-mix(in srgb, var(--accent) 6%, var(--theme-surface))` on `.prompt-instructions`,
    replacing the prior do-nothing default"
  - "Each of the 12 forks uses its own `var(--accent)` / `var(--theme-surface)` tokens for the guided tint —
    no new fork-specific literals introduced"
  - "The prior fork-specific `background: var(--<fork>-...)` override on expert's `.prompt-instructions` was
    removed (folded into the new `display: none` rule) since it is now moot"

patterns-established:
  - "Mode-delta CSS must key off an always-rendered anchor element, not optional/sparse text, to remain
    perceptible and regression-testable regardless of form content"

requirements-completed: [UIAUDIT-02]

# Metrics
duration: 4min
completed: 2026-07-05
---

# Phase 86 Plan 04: Layout Picker Mode Delta Summary

**Full/Compact/Advanced now produce an unmistakable, measurable box delta on `.prompt-instructions` (always rendered on Step 1), and Full gets a positive accent style instead of a do-nothing default — mirrored across base `site.css` and all 12 standalone-fork themes.**

## Performance

- **Duration:** ~4 min (14:57 -> 15:01 MDT, git commit timestamps)
- **Started:** 2026-07-05T20:57:22Z (branch tip before this plan)
- **Completed:** 2026-07-05T21:01:33Z
- **Tasks:** 2/2 completed
- **Files modified:** 13 (1 base + 12 forks)

## Accomplishments
- Root cause fixed: the layout picker's CSS effect was imperceptible on the empty Step-1 landing because the
  hidden elements (`eyebrow`/`badge`/`note`/`context-note`) are sparse/optional. `expert` now collapses the
  always-present `.prompt-instructions` panel entirely — a guaranteed box delta regardless of deck content.
- `focused` now measurably shrinks (not just hides sparse text): tighter `.prompt-instructions` padding plus a
  tightened `.prompt-step-heading` gap/margin, distinct from both `guided` and `expert`.
- `guided`/Full changed from a do-nothing default to a positive marker: an accent left-border + accent-tinted
  background on `.prompt-instructions`, using the existing `color-mix(in srgb, var(--accent) N%, ...)` pattern.
- All three modes now key off the SAME always-rendered anchor element (`.prompt-instructions`), guaranteeing
  the interaction e2e in plan 86-05 has a stable box/visibility assertion target.
- Mirrored identically into all 12 standalone-fork theme CSS files (abzan, bant, commander-table, esper,
  grixis, jeskai, jund, mardu, naya, nyx, planeswalker-dark, sultai); each fork keeps its own `--accent` /
  `--theme-surface` tokens. The 11 `@import` themes inherit the base change for free.

## Task Commits

Each task was committed atomically:

1. **Task 1: Base — guaranteed layout delta per mode + positive Full style** - `43c23852` (fix)
2. **Task 2: Mirror the mode styling into the 12 standalone forks** - `09e697fc` (fix)

**Plan metadata:** (this commit, following) - `docs(86-04): complete layout-picker mode-delta plan`

## Files Created/Modified
- `DeckFlow.Web/wwwroot/css/site.css` - guided/focused/expert mode-delta rules rewritten (site.css:388-435 area)
- `DeckFlow.Web/wwwroot/css/site-abzan.css` - mirrored mode-delta rules
- `DeckFlow.Web/wwwroot/css/site-bant.css` - mirrored mode-delta rules
- `DeckFlow.Web/wwwroot/css/site-commander-table.css` - mirrored mode-delta rules
- `DeckFlow.Web/wwwroot/css/site-esper.css` - mirrored mode-delta rules
- `DeckFlow.Web/wwwroot/css/site-grixis.css` - mirrored mode-delta rules
- `DeckFlow.Web/wwwroot/css/site-jeskai.css` - mirrored mode-delta rules
- `DeckFlow.Web/wwwroot/css/site-jund.css` - mirrored mode-delta rules
- `DeckFlow.Web/wwwroot/css/site-mardu.css` - mirrored mode-delta rules
- `DeckFlow.Web/wwwroot/css/site-naya.css` - mirrored mode-delta rules
- `DeckFlow.Web/wwwroot/css/site-nyx.css` - mirrored mode-delta rules
- `DeckFlow.Web/wwwroot/css/site-planeswalker-dark.css` - mirrored mode-delta rules
- `DeckFlow.Web/wwwroot/css/site-sultai.css` - mirrored mode-delta rules

## Decisions Made
- Anchor the guaranteed delta to `.prompt-instructions` (always rendered on Step 1) rather than introducing a
  new element or restructuring the form column layout — smaller blast radius, satisfies the plan's
  `key_links` requirement (`.prompt-packets-form[data-prompt-ui-mode='expert']` -> `.prompt-instructions`
  structural collapse).
- Reused the existing `color-mix(in srgb, var(--accent) N%, ...)` accent-tint idiom (established in 86-01/86-03)
  for the guided positive style instead of introducing a new CSS custom property.
- Removed the now-moot fork-specific `background: var(--<fork>-...)` override under `expert .prompt-instructions`
  since the element is now fully hidden — avoids dead/unreachable declarations.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched the plan's file list and verification gates without
requiring architectural changes or additional fixes.

## Known Stubs

None.

## Threat Flags

None - CSS-only change, no new network endpoints, auth paths, file access, or schema surface. Matches the
plan's threat_model disposition (`accept`/`mitigate`, no new surface).

## Verification Results

- Task 1 automated gate: `grep -n 'data-prompt-ui-mode="expert"\|data-prompt-ui-mode="focused"\|data-prompt-ui-mode="guided"' site.css` — PASS (19 matches, includes new `guided` selector).
- Task 2 automated gate: `test $(grep -rl 'data-prompt-ui-mode="expert"' <12 forks> | wc -l) -eq 12` — PASS.
- Build: `dotnet.exe build DeckFlow.sln` — **0 Warnings, 0 Errors** (run after each task).
- LF line endings preserved on all 13 modified files (verified via `grep -c $'\r'` == 0 on each).
- No TypeScript/JavaScript files touched (`deck-sync.ts` and compiled `wwwroot/js/*.js` untouched) — confirmed
  via `git diff --stat` across both commits (13 CSS files only).
- No files outside the plan's `files_modified` list were touched; working tree clean after each commit.
- Full xUnit + Playwright e2e suite deferred to plan 86-05 per the phase's validation strategy (86-05 owns the
  new `layout-mode-interaction.spec.ts` that asserts the measurable delta this plan enables, plus the
  consolidated full-suite gate before `/gsd:verify-work`).

## Self-Check: PASSED
