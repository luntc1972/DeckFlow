---
phase: 72-command-zone-commander-castability
plan: 06
subsystem: ui
tags: [razor, css, playwright, manabase, commander, companion]
requires:
  - phase: 72-05
    provides: ShowCommanderCastability, CompanionCallout, and Request.CompanionName view-model contracts
provides:
  - Command-zone castability callout above the per-card castability table
  - Display-only commander filtering from the visible castability table and average
  - Manual companion designator input bound to Request.CompanionName
  - Always-on manabase beta notice in the results region
  - Live Playwright spec covering G-01 through G-04
affects: [manabase, casual-mode-ui, commander-castability, live-e2e]
tech-stack:
  added: []
  patterns: [Razor display-only filtering, token-based panel styling in site-common.css, live-gated Playwright coverage]
key-files:
  created: [DeckFlow.Web/e2e/manabase-commander-callout.spec.ts]
  modified: [DeckFlow.Web/Views/Deck/Manabase.cshtml, DeckFlow.Web/wwwroot/css/site-common.css]
key-decisions:
  - "Filtered commanders through a typed castRows display list so visible averages and table rows change without mutating report.Castability."
  - "Kept the beta notice always visible whenever a report is present, independent of the commander-castability flag."
  - "Used Razor auto-encoding for commander and companion names; no Html.Raw rendering was introduced."
patterns-established:
  - "Commander-specific result callouts should mirror the existing verdict panel tokens in site-common.css."
  - "Live manabase E2E coverage stays DECKFLOW_LIVE_E2E-gated and uses pasted decklists to avoid external deck-host dependencies."
requirements-completed: [D-01, D-02, G-01, G-02, G-03, G-04, B-06, BETA-01]
duration: 35min
completed: 2026-06-27
---

# Phase 72: Command-Zone Commander Castability Summary

**Command-zone castability now surfaces as its own Casual-mode callout, with commander rows hidden from the visible castability table, a manual companion designator, and an always-on beta notice above results.**

## Performance

- **Duration:** 35 min
- **Started:** 2026-06-27T15:22:00Z
- **Completed:** 2026-06-27T15:56:44Z
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments
- Added a result-level beta notice plus a command-zone castability panel that renders between ramp/draw and the per-card castability table.
- Switched the visible castability table and avg-on-curve lens to a display-only `castRows` filter when commander castability is enabled.
- Added a manual companion designator input and a live-only Playwright spec for commander/companion callout behavior.

## Task Commits

Each task was committed atomically:

1. **Task 1-3: UI callout, designator, beta notice, and live spec** - `1368d259` (feat)

**Plan metadata:** `1d5c1a65` (docs: complete plan)

## Files Created/Modified
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` - Adds the beta notice, command-zone callout, companion designator, filtered castability rows, and mirrored companion hidden field for downloads.
- `DeckFlow.Web/wwwroot/css/site-common.css` - Adds token-based styling for the beta notice and command-zone castability callout.
- `DeckFlow.Web/e2e/manabase-commander-callout.spec.ts` - Adds a DECKFLOW_LIVE_E2E-gated spec for G-01 through G-04.
- `.planning/phases/72-command-zone-commander-castability/72-06-SUMMARY.md` - Records the implementation outcome and remaining operator verification.

## Decisions Made
- Used the existing `castRows` lens input as the single display filter so the visible average and table stay consistent under the flag.
- Placed the command-zone callout after the ramp/draw advisory and before the castability heading to match the intended results ordering.
- Preserved the underlying `report.Castability` data for downstream logic and flag-OFF behavior.

## Deviations from Plan

None - plan executed exactly as written for Tasks 1, 2, and 3.

## Issues Encountered

- The initial spec refinement introduced an invalid TypeScript expression in the DOM-order assertion; it was corrected within the scoped file and re-verified with `tsc`.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Task 4 remains pending operator verification: cross-theme visual review plus a live Playwright run with the app running and the feature flag enabled.
- The scoped UI/code work for Tasks 1, 2, and 3 is ready for that manual checkpoint.

---
*Phase: 72-command-zone-commander-castability*
*Completed: 2026-06-27*
