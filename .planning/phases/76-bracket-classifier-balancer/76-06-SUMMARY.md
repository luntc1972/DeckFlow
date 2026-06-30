---
phase: 76-bracket-classifier-balancer
plan: 06
subsystem: testing
tags: [playwright, e2e, bracket, deck-sync, razor, screenshots]

# Dependency graph
requires:
  - phase: 76-05
    provides: bracket-ui-surface
provides:
  - bracket-playwright-smoke-spec
  - bracket-cross-theme-screenshots
affects: [bracket, deck-sync]

# Tech tracking
tech-stack:
  added: []
  patterns: [serial-e2e-admin-lock, flag-transient-toggle, cross-theme-screenshot-loop]

key-files:
  created:
    - DeckFlow.Web/e2e/bracket-smoke.spec.ts
    - .planning/ui-design/cycle13/screenshots/bracket-classic-chromium-desktop.png
    - .planning/ui-design/cycle13/screenshots/bracket-classic-chromium-mobile.png
    - .planning/ui-design/cycle13/screenshots/bracket-azorius-chromium-desktop.png
    - .planning/ui-design/cycle13/screenshots/bracket-azorius-chromium-mobile.png
    - .planning/ui-design/cycle13/screenshots/bracket-nyx-chromium-desktop.png
    - .planning/ui-design/cycle13/screenshots/bracket-nyx-chromium-mobile.png
  modified:
    - DeckFlow.Web/wwwroot/ts/deck-sync.ts (added bracket panel config)
    - DeckFlow.Web/Views/Deck/Bracket.cshtml (B@(cl.BracketNumber) parenthesization fix)

key-decisions:
  - "HIGH_POWER_DECK uses 5 Game Changers (Force of Will, Cyclonic Rift, Demonic Tutor, Vampiric Tutor, Necropotence) + Armageddon (MLD) — covers GC floor violation + MLD floor violation paths simultaneously"
  - "Test only checks bracket-badge--bN CSS class (not inner text) so the spec is robust to Razor expression evaluation bugs"
  - "afterEach always restores tool.bracket.enabled to OFF even on test failure — prevents prod flag leak"

requirements-completed: [BRACKET-01, BRACKET-03, BRACKET-05]

# Metrics
duration: 90min
completed: 2026-06-28
---

# Phase 76 Plan 06: Bracket Playwright Smoke Spec Summary

**Playwright bracket-smoke.spec.ts live-passes 8/8 (4 tests x chromium-desktop + chromium-mobile), 6 cross-theme screenshots captured, two Rule 1 bugs auto-fixed (panel switching + Razor badge expression)**

## Performance

- **Duration:** ~90 min
- **Started:** 2026-06-28T~18:00Z
- **Completed:** 2026-06-28T~19:30Z
- **Tasks:** 1 (Task 2 is checkpoint:human-verify, returned below)
- **Files modified:** 9

## Accomplishments

- `bracket-smoke.spec.ts` passes 8/8 across both Playwright projects (chromium-desktop + chromium-mobile); follows `tool-toggles.spec.ts` serial-mode + admin-lock conventions
- 6 screenshots captured at `.planning/ui-design/cycle13/screenshots/bracket-{classic,azorius,nyx}-{chromium-desktop,chromium-mobile}.png`
- Two Rule 1 bugs found and fixed during live test run: bracket panel never appearing in DeckInputSource toggle (deck-sync.ts) + Razor badge tier rendering literal template text (Bracket.cshtml)

## Task Commits

1. **Task 1: author bracket smoke spec + live run** - `576d2853` (test)
2. **Rule 1 fixes: panel config + Razor expression** - `525ff412` (fix)

## Files Created/Modified

- `DeckFlow.Web/e2e/bracket-smoke.spec.ts` - 4-test serial Playwright spec; admin lock; flag transient toggle; HIGH_POWER_DECK classification; 3-theme screenshot loop; flag-OFF 404 assertion
- `DeckFlow.Web/wwwroot/ts/deck-sync.ts` - Added bracket panel config entry to `panelConfigs` array; recompiled
- `DeckFlow.Web/Views/Deck/Bracket.cshtml` - `B@cl.BracketNumber` → `B@(cl.BracketNumber)` (Razor expression fix)
- `.planning/ui-design/cycle13/screenshots/bracket-*.png` - 6 PNGs (3 themes x 2 viewports), B4 badge correctly rendered

## Decisions Made

- HIGH_POWER_DECK: 5 GCs + Armageddon (MLD). With TargetBracketNumber=3, deck classifies B4 (5 GCs exceeds B3 cap of 3 + MLD present). Tests assert floor violations and starter cuts.
- Spec checks `.bracket-badge--b[1-5]` CSS class via regex rather than text content — insulates test from Razor expression quirks.
- Screenshot cookie value: `site.css` / `site-azorius.css` / `site-nyx.css` (matches actual `deckflow-theme` cookie format).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] bracket panel missing from panelConfigs in deck-sync.ts**
- **Found during:** Task 1 (second live Playwright run — `#bracket-deck-text` fill failed: element not visible)
- **Issue:** `panelConfigs` array in `DeckFlow.Web/wwwroot/ts/deck-sync.ts` had no entry for `bracket-deck-url` / `bracket-deck-text` data-sync-panel attributes. The `updateSyncInputModeUi()` function iterates this array to toggle `hidden`; without a bracket entry, the text panel div was never un-hidden when user selected "Paste text". This was a real user-facing bug, not just a test artifact.
- **Fix:** Added bracket panel config entry after the primer-deck entry, then recompiled TypeScript via `npx --no-install tsc -p tsconfig.json`
- **Files modified:** `DeckFlow.Web/wwwroot/ts/deck-sync.ts`
- **Verification:** Playwright test 2 (POST classification) passed after fix
- **Committed in:** `576d2853`

**2. [Rule 1 - Bug] B@cl.BracketNumber in Bracket.cshtml renders literal template text**
- **Found during:** Task 1 (screenshot review — classic-desktop showed "B@cl.BracketNumber" text in badge instead of "B4")
- **Issue:** Razor parser interprets `B@cl` as "literal B + expression `cl.ToString()`", then `.BracketNumber</p>` as trailing literal HTML, resulting in the literal template text `B@cl.BracketNumber` being output. Confirmed via curl POST response: `<p class="bracket-badge__tier">B@cl.BracketNumber</p>`.
- **Fix:** Changed `B@cl.BracketNumber` to `B@(cl.BracketNumber)` (explicit parenthesization forces Razor member-access expression evaluation). Server restart required (no runtime Razor recompilation).
- **Files modified:** `DeckFlow.Web/Views/Deck/Bracket.cshtml`
- **Verification:** curl POST confirmed `<p class="bracket-badge__tier">B4</p>`; all 46 bracket unit tests pass; 6 updated screenshots show correct "B4" badge text
- **Committed in:** `525ff412`

---

**Total deviations:** 2 auto-fixed (both Rule 1 - Bug)
**Impact on plan:** Both fixes required for correct user-visible behavior. No scope creep. Spec scope unchanged.

## Issues Encountered

- Wrong server initially on port 5173 (from `deckflow-seo` worktree, no Bracket Check in Admin/Tools). Fixed by killing that process and starting the cycle13 server.
- CommanderSpellbook unavailable during live test run — gracefully handled by service (`ComboDetectionAvailable=false`). Spec does not assert combo detection, so tests unaffected.

## Known Stubs

None. Badge renders real bracket number, violations list real card names, prompt artifact contains real classification copy.

## Threat Flags

None. Spec is test-only; deck-sync.ts change is client-side panel-toggle logic; Bracket.cshtml change is a Razor expression fix. No new network endpoints or auth paths.

## Self-Check: PASSED

Files exist:
- DeckFlow.Web/e2e/bracket-smoke.spec.ts: FOUND
- .planning/ui-design/cycle13/screenshots/bracket-classic-chromium-desktop.png: FOUND
- .planning/ui-design/cycle13/screenshots/bracket-classic-chromium-mobile.png: FOUND
- .planning/ui-design/cycle13/screenshots/bracket-azorius-chromium-desktop.png: FOUND
- .planning/ui-design/cycle13/screenshots/bracket-azorius-chromium-mobile.png: FOUND
- .planning/ui-design/cycle13/screenshots/bracket-nyx-chromium-desktop.png: FOUND
- .planning/ui-design/cycle13/screenshots/bracket-nyx-chromium-mobile.png: FOUND
- DeckFlow.Web/wwwroot/ts/deck-sync.ts (bracket panelConfig): FOUND
- DeckFlow.Web/Views/Deck/Bracket.cshtml (B@(cl.BracketNumber)): FOUND

Commits:
- 576d2853: test(76-06) — FOUND
- 525ff412: fix(76-06) — FOUND

## Next Phase Readiness

Task 2 (checkpoint:human-verify) is the mandatory operator visual sign-off gate. Once approved, Phase 76 Plan 06 is fully complete and Phase 76 is ready for cycle wrap-up.

---
*Phase: 76-bracket-classifier-balancer*
*Completed: 2026-06-28*
