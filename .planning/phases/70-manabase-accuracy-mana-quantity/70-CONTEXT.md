# Phase 70 — Manabase Accuracy: Mana Quantity & Source Fidelity

**Track:** ad-hoc trunk (`main`) — Manabase Analyzer feature (de-numbered manabase phases precede
this; Cycle 11 owns 64-69 on its own worktree, so 70 is the next free number).
**Created:** 2026-06-22
**Source of work:** Codex efficacy audit (grade C-) + research validation — see
`.planning/captures/manabase-efficacy-findings.md` (the authoritative finding/scoping record).

## Goal
Raise the mana-base analysis from "rough heuristic for normal decks" toward "trustworthy across
real Commander mana patterns" by fixing the verified accuracy defects — without silently re-tuning
the verdict. The single biggest win is modeling **how much** mana a source makes, not just **which**
colors.

## Requirements (mapped to audit findings)
| Req | Finding | What | Sev | Risk |
|-----|---------|------|-----|------|
| MQ-01 | #3 | Commander must not be drawn into the simulated library | High (narrow) | Low |
| MQ-02 | #1 | Model per-source mana **quantity** (Sol Ring=2, Ancient Tomb=2, Gilded Lotus=3) on the affordability/curve side only | High (broad) | **High** |
| MQ-03 | #2 | Stop the regression over-crediting one-shot rituals/Treasures; give surviving ramp credit a modeled mana path so sim ↔ regression agree | High | Med |
| MQ-04 | #6 | Disclose unsupported interactions (hybrid/Phyrexian/X/snow/devotion/…) instead of silently absorbing | Med | Low |
| MQ-05 | #5 | Make the London-mulligan keep heuristic color-aware (not land-count-only) | Med | Med |

Deferred (NOT in this phase): #4 (joint multicolor deficit model) — large, separate decision;
the shipped composition breakdown already makes the limitation visible.

## Key decisions (locked, from Codex review + research)
- **Color-source counting stays "1 source of a color" (Karsten-correct).** Mana quantity affects
  only the sim's affordability/curve math, NOT `EffectiveSources` / `SimRequiredSources` /
  per-color deficit. This avoids re-basing the verdict (Codex rejected that path).
- **MQ-02 must be baseline-diffed and validated vs Salubrious Snail** before it ships; expect cast%
  to rise. Flag-gate if the verdict shift is large.
- **MQ-01 keeps commander sources in `EffectiveSources`** (a commander mana source IS castable);
  only removes them from the **drawable** library.

## Plan breakdown (execution order)
- **70-01 — MQ-01 commander-not-drawable.** Cheap, independent, no verdict shift. DO FIRST.
- **70-02 — MQ-02 per-source mana quantity.** The big one. Own plan; baseline-diff + golden-deck.
- **70-03 — MQ-03 ramp-credit consistency.** After MQ-02 (shares the "modeled mana path" work).
- **70-04 — MQ-04 unsupported-interaction disclosure.** Independent; low-risk UI/notes.
- **70-05 — MQ-05 color-aware mulligan.** Independent of the above; affects absolute cast%.

## Success criteria
1. A Sol Ring / Ancient Tomb deck shows correct on-curve casts (2-drop turn 1 off a turn-0 Ring; a
   4-drop turn-2 off Ancient Tomb + 2 lands) — MQ-02.
2. A mana-creature commander (Selvala, Marwyn) yields identical cast% whether the library is 99 or
   100, and the commander is never "drawn" — MQ-01.
3. A ritual/Treasure-heavy deck no longer gets a softened verdict from un-modeled one-shot mana —
   MQ-03.
4. Cards with hybrid/Phyrexian/X costs are counted in an explicit "N cards with unsupported
   interactions" disclosure — MQ-04.
5. Existing manabase test suite green; MQ-02 baseline diff reviewed and accepted (or flag-gated).

## Constraints
- DeckFlow rules: Claude implements, Codex reviews (until 2026-06-24); web-page changes need
  xUnit + Playwright + theme/mobile; update README/help on behavior change.
- The verdict math (`Health`, `Deficit`) must not silently change except via MQ-02's reviewed
  re-baseline.
