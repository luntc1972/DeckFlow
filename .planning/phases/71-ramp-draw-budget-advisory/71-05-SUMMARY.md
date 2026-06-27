---
phase: 71-ramp-draw-budget-advisory
plan: 05
type: checkpoint
status: complete
---

# 71-05 SUMMARY — Human visual verify (operator sign-off)

## Outcome: PASSED

Operator ran the live (Scryfall-backed) visual verification of the plain-language layer on `/manabase`
with the `manabase.plain-language-verdict` flag, across desktop + mobile and light + dark themes.

### Verified
- Flag OFF: page byte-identical to before (no glosses / verdict / ramp-draw block).
- Flag ON, Casual issue-deck: metric glosses under each lens, "Reading your deck" prioritized lines
  (land/color -> ramp -> draw, capped 3), ramp/draw budget line with "(N do both)" + proxy disclosure
  + "community heuristic, not Karsten math" label.
- Flag ON, Casual clean-deck: specific why-it-is-fine (cleared colors + cast rate + Casual).
- Flag ON, cEDH: glosses render, ramp/draw budget + verdict suppressed.
- Desktop (~1280px) and mobile (~390px), one light + one dark guild theme: no two-lens/overflow break,
  readable contrast.
- Copy-for-ChatGPT prompt carries the verdict + ramp/draw line (Casual).

### Related fix during verify
Operator reported an intermittent disclosure (`details.manabase-unsupported`) not expanding on first
load. Root cause not reproducible (native `<details>` + CSS proven clean via isolated Playwright; both
operator and AI repros came up clean). Defensive hardening shipped anyway: commit `3769891b`
`fix(web): force-hide busy overlay on load when a result is present` — bootstrap force-hides the
`#busy-indicator` overlay when a rendered result is present, closing the only plausible mechanism
(a stuck overlay intercepting clicks). Guarded by `e2e/busy-overlay-guard.spec.ts` (2 passed,
CI-runnable).

## Self-Check: PASSED
