---
phase: 86-ui-audit-re-score-studio-stage-4-admin-flags-closeout
plan: 03
subsystem: ui
tags: [css, razor, accessibility, theming, deckflow-analysis]

# Dependency graph
requires:
  - phase: 86-02
    provides: filled-accent-pill active step-tab restyle (prior wave in this phase)
provides:
  - Borderless, higher-contrast chevron style for the analysis-questions bucket toggle
  - aria-label on the bucket toggle button (closes an a11y gap)
  - Mirrored fix across the 12 standalone theme forks + the site-rakdos.css @import override
affects: [86-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Standalone-fork CSS fix fan-out: base site.css rule change mirrored verbatim into all 13 duplicating forks"
    - "@import theme override neutralization via a same-specificity follow-up rule (site-rakdos.css) rather than editing the shared multi-selector block"

key-files:
  created: []
  modified:
    - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml
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
    - DeckFlow.Web/wwwroot/css/site-rakdos.css

key-decisions:
  - "site-rakdos.css: rather than remove prompt-question-bucket__toggle from its shared multi-selector rule (which would have dropped the required grep-verified string and left an ambiguous diff), added a small same-specificity follow-up rule that resets border/background just for the toggle — the plan's own Task 2 automated gate requires the class name string to remain present in all 13 files."
  - "Forks previously hardcoded font-size: 0.65rem for the toggle instead of using a design token; replaced with var(--fs-base) (the same token now used in the restyled site.css base rule) rather than another hardcoded literal, since all forks already define --fs-base in their :root."

requirements-completed: [UIAUDIT-02]

# Metrics
duration: ~20min
completed: 2026-07-05
---

# Phase 86 Plan 03: Bucket-Toggle Chevron + Accessible Name Summary

**Restyled the empty analysis-questions bucket toggle from a bordered grey pill into a borderless, higher-contrast chevron and gave it an aria-label, mirrored across site.css + all 13 duplicating theme files (12 standalone forks + site-rakdos.css).**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-07-05T20:54:33Z
- **Tasks:** 2 completed
- **Files modified:** 15

## Accomplishments
- `DeckAnalysis.cshtml:306` bucket toggle button now carries `aria-label="Toggle {bucket.Label} questions"`, giving screen readers an accessible name it previously lacked entirely.
- `site.css` base `.prompt-question-bucket__toggle` rule restyled: `border: 0` (was `1px solid var(--line)` + `border-radius: 4px`), caret raised from `var(--fs-xs)`/`var(--muted)` to `var(--fs-base)`/`var(--ink)` for size + contrast.
- All 12 standalone forks (abzan, bant, commander-table, esper, grixis, jeskai, jund, mardu, naya, nyx, planeswalker-dark, sultai) mirrored identically, replacing each fork's hardcoded `font-size: 0.65rem` with the same `var(--fs-base)` token.
- `site-rakdos.css` (the one @import theme that overrode this element via a shared multi-selector border/background rule) got a follow-up rule clearing the border/background just for the toggle, so it now falls through to the plain-chevron look on its dark maroon background instead of keeping a filled pill.
- The 10 bare `@import` themes (azorius, boros, dimir, golgari, gruul, izzet, orzhov, selesnya, simic, temur) inherit the fix for free from base `site.css` — untouched, per plan.

## Task Commits

1. **Task 1: aria-label markup + base chevron restyle** - `8a70842e` (fix)
2. **Task 2: Mirror the chevron restyle into the 12 forks + rakdos** - `13bf6ebf` (fix)

_No plan-metadata commit yet — STATE/ROADMAP updates follow this summary and will be captured in the final docs commit._

## Files Created/Modified
- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` - added `aria-label` to the bucket toggle button
- `DeckFlow.Web/wwwroot/css/site.css` - base chevron restyle (border removed, caret size/contrast raised)
- `DeckFlow.Web/wwwroot/css/site-{abzan,bant,commander-table,esper,grixis,jeskai,jund,mardu,naya,nyx,planeswalker-dark,sultai}.css` - mirrored chevron restyle (12 standalone forks)
- `DeckFlow.Web/wwwroot/css/site-rakdos.css` - added follow-up rule so the shared border/background override no longer re-fills the toggle with a maroon pill

## Decisions Made
- **site-rakdos.css structural choice:** the toggle's border/background previously came from a shared rule also styling `.tool-nav__trigger`, `.prompt-step-tab`, `.clear-cache-button`, `.swap-direction-button`. Removing `.prompt-question-bucket__toggle` from that list entirely would have satisfied the visual goal but caused the file to no longer contain the literal string `prompt-question-bucket__toggle` — failing the plan's own Task 2 automated grep gate (`grep -rl ... | wc -l -eq 13`). Kept the class in the shared list (so the string requirement holds and the other three elements keep their unrelated styling) and added an explicit same-specificity follow-up rule (`border: 0; background: none;`) directly after it, which wins by source order and neutralizes the fill for just the toggle. This is the minimal, non-structural way to satisfy both the plan's literal verification gate and the "no filled pill" visual intent.
- **Token choice for raised caret size:** used `var(--fs-base)` (already defined in every fork's `:root`, same as base `site.css` now uses) rather than inventing a new size, keeping the fix token-consistent rather than another hardcoded literal.

## Deviations from Plan

None - plan executed exactly as written. The rakdos handling above is an implementation-detail choice within Task 2's explicit instruction ("mirror the chevron restyle... drop the border... raise the caret"), not a deviation from the plan's intent or scope.

## Issues Encountered
- **Build lock (environment, not code):** first `dotnet build` failed with `MSB3027`/`MSB3021` because a stale `DeckFlow.Web.exe` (PID 391616) from an earlier dev-server session held a file lock on the output binary. Terminated the stale process (`taskkill.exe /PID 391616 /F`) and rebuilt — clean 0 Warnings / 0 Errors on the second run. Not caused by this plan's changes; no code was affected.
- The changed-lines format gate (`scripts/format-check-changed.sh staged`) only inspects `*.cs` files; this plan touches only `.cshtml`/`.css`, so the gate correctly reports "no changed C# files" and is not a meaningful check here — confirmed by reading the script rather than skipped blindly.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 86-05's consolidated e2e/human-verify step can now assert: (a) the bucket toggle has an accessible name, (b) no bordered grey pill remains on any of the 24 themes (base site.css theme, 12 forks, 11 @import themes including rakdos).
- No blockers for 86-04 or 86-05.

---
*Phase: 86-ui-audit-re-score-studio-stage-4-admin-flags-closeout*
*Completed: 2026-07-05*

## Self-Check: PASSED
All created/modified artifacts and both task commits verified present via `git log --oneline --all` and filesystem checks.
