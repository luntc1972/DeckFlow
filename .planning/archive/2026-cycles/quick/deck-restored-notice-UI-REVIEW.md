# UI Audit — Restored-Deck Notice (feat/deck-store-clear-and-restored-notice)

**Date:** 2026-06-27
**Change:** `5bd2d928` — Start Over clears carried deck + inline "Restored your last deck." notice;
themed Clear-button consistency fix `c66c0ed9`.
**Method:** Playwright screenshots of `.deck-restored-notice` on `/manabase` (prefilled state)
across 3 representative themes (site, azorius, nyx) × 2 viewports (1280x900 desktop, 390x844 mobile)
+ CSS/TS code review. All 6 shots rendered the notice.

## Overall: 23/24

| Pillar | Score | Note |
|--------|-------|------|
| Copywriting | 4/4 | "Restored your last deck." + "Clear" — concise, accurate, plain ASCII (no em/en dash). |
| Visuals | 4/4 | Minimal accent strip; left accent border signals an info/restore affordance; consistent with panel chrome. |
| Color | 4/4 | Uses theme tokens (--panel, --line, --accent, --muted); correct per theme (blue default, purple nyx). Legible contrast all themes. |
| Typography | 4/4 | Inherits theme font; muted body text readable, button label clear. |
| Spacing | 4/4 | padding 0.7/0.9rem + gap 0.75rem + 0.75rem bottom margin; balanced, no crowding; fits 390px on one row. |
| Experience Design | 4/4 | Appears only on real prefill; Clear empties field + drops store + hides notice; Start Over also clears the carried deck. LOW finding below fixed. |

## Findings

- ✅ RESOLVED (was 🔵 LOW, ED) — The in-notice "Clear" originally reused `clear-cache-button`,
  which guild themes fork (azorius rendered it FILLED primary; default/nyx outlined) — inconsistent
  + over-weighted a dismiss action. Fixed in `c66c0ed9`: dedicated `deck-restored-notice__clear`
  class styled once in site-common.css with tokens (themes don't fork it). Re-screenshot verified:
  consistent OUTLINED secondary across site/azorius/nyx, desktop + mobile; keyboard focus preserved
  via the universal focus baseline. ED now 4/4.
- ⚪ INFO — No auto-dismiss; notice persists until Clear or until the user edits the field/navigates.
  Intended (gives the user agency); acceptable.

## Verdict
PASS. No blocking visual issues. The notice is clean, on-brand, themes correctly, and is
responsive at mobile width. The one LOW finding (themed button weight) is optional polish.

Screenshots: scratchpad `notice-{site,site-azorius,site-nyx}-{desktop,mobile}.png` (ephemeral).
