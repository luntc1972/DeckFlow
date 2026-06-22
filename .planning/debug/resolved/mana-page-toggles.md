---
status: fixing
trigger: "mana page desktop the new toggles do not work, the text in them should be centered, the toggle between url or text is far apart on page, fix and verify in mobile"
created: 2026-06-21
updated: 2026-06-21
---

# Debug: mana page toggles

## Symptoms
- Desktop: segmented pill toggles (Casual/cEDH, Central/Standard/Low) "do not work" — clicking gives no visual selected feedback.
- Pill text should be centered.
- "Public URL" / "Paste decklist" radios are far apart (opposite page edges).
- Must verify mobile.

## Root cause
1. `.manabase-pill.is-selected` highlight is rendered by Razor server-side only
   (`@(mode == ... ? "is-selected" : null)`). No client-side state, so clicking a
   radio does not move the highlight until a POST roundtrip → looks broken.
2. `.manabase-pill` sets `align-items:center` but no `justify-content:center`.
3. Input-source radios live in `.toolbar`, which every theme forks as
   `justify-content: space-between` → the two radios get pushed to opposite edges.

## Fix
- site-common.css: add `.manabase-pill:has(> input:checked)` mirroring `.is-selected`
  (instant client-side selection, no JS); add `justify-content:center` to `.manabase-pill`;
  add scoped `.manabase-source-toggle` modifier (`justify-content:flex-start`) — keeps
  layout CSS in site-common.css, no theme forks touched.
- Manabase.cshtml: add `manabase-source-toggle` class to the source `.toolbar` div.

## Verification
- dotnet build clean
- gstack desktop + mobile: pills highlight on click, text centered, source radios adjacent
