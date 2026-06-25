---
status: resolved
trigger: "pills aren't centered on the manabase page on browser not sure about mobile"
created: 2026-06-22
updated: 2026-06-22
---

# manabase-pills-not-centered

## Symptom
Segmented "pill" radios on /manabase (Deck type: Casual/cEDH, commander
importance: Central/Standard/Low) render with label text off-center — most
visible on the filled/selected pill, which shows empty fill beside the text.
Reproduced on Planeswalker Dark (matches the user screenshot); present on every
theme. Desktop and mobile both affected.

## Root cause
`site-theme-overrides.css:53-70` applies a global custom radio/checkbox render:
`input[type="radio"] { appearance:none; width:1.05rem; height:1.05rem;
border:2px; position:relative; ... }` (the native-chrome custom render, edf9afa).
It loads AFTER `site-common.css`. Its selector `input[type="radio"]` (specificity
0,1,1) ties `.manabase-pill > input` (0,1,1) and wins on load order, so the
visually-hidden pill radio is re-inflated to a ~15.75px in-flow box (opacity:0
but still occupying space). That phantom box sits inside the centered flex label
and knocks the span text off-center. Verified live: pre-fix the checked pill
radio measured `{w:15.75, h:15.75, position:relative}`.

## Fix
`DeckFlow.Web/wwwroot/css/site-common.css` — raise the collapse rule specificity
to `.manabase-pill > input[type="radio"]` (0,2,1) so it beats the global override
regardless of load order; also add `border:0; appearance:none` to fully
neutralize the custom render. Scoped to `[type="radio"]`, so the intentionally
visible source-toggle radios (Public URL / Paste decklist) are untouched.

## Verification
- Live DOM injection of the new rule on prod: radio collapses to
  `{w:1, h:1, position:absolute}`; pills render text-centered (screenshots).
- New regression test `segmented pill radios stay visually collapsed so labels
  center` in `manabase-castability.spec.ts` (asserts width<=2, height<=2,
  position:absolute). Passes chromium-desktop + chromium-mobile.
- Full manabase e2e: 38 passed across both viewports and multiple themes; no
  sibling breakage.

## Files changed
- DeckFlow.Web/wwwroot/css/site-common.css
- DeckFlow.Web/e2e/manabase-castability.spec.ts
