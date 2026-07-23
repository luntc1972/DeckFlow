---
phase: manabase-research-gap-closure
plan: 03
status: complete
completed: 2026-07-12
commits:
  - eba26805 feat(manabase): composition-gated restricted-land modeling (gap-03)
  - 50b01b26 test(manabase): restricted-land census tests + canaries + rules doc (gap-03)
executor: codex gpt-5.4 medium (cross-AI); Claude reviewed + committed
verifier: foreman-verifier PASS_WITH_NOTES (1 LOW carried to plan 04)
---

# Plan 03 Summary — MBGAP-01 restricted-land classification

## What shipped

- **Composition-gated modeling** (`ClassifySpecialLand`, D-03): Cavern of Souls / Unclaimed Territory weighted by `DominantTypeShare` (quantity-weighted creature-subtype histogram over creatures only); Ancient Ziggurat weighted by `CreatureShare`; Nykthos = 0.25-weight conditional source (`NykthosDevotionRegex`). `RestrictedLandMinWeight` clamp. NOT flat discount, NOT sim masks.
- **Disclosure surfaces** (D-05): deck-level `RestrictedSourceLandNames` + `HasRestrictedSourceApproximation` on `ManabaseDeck` AND `ManabaseReport` (report copy is dead until plan 04 wires analyzer→report — intentional scoping, flagged for plan 04's verifier).
- **Gate:** new trailing `restrictedLands: false` param on `Classify` — all callers omit it; off path byte-identical. Plan 04 registers `analysis.manabase.restricted-lands` and wires it.
- Canaries: +3 (Cavern/Ziggurat "creature spell" wording, Nykthos "devotion to that color").
- Adversarial regex check (verifier): `SpendOnlyCreatureRegex` requires literal "creature spell" — Shrine of the Forsaken Gods class does NOT match.

## Validation

- Build 0/0; Core FULL 1387/0/0; Web FULL 1350/14skip/0.
- EOL clean. Write set exactly 5 files.

## Carried forward to plan 04

- LOW: add regression test "Cavern present + restrictedLands=false → weight stays 1.0" (goes with plan 04's flag parity test).
- Wire `deck.RestrictedSourceLandNames` → `ManabaseReport` in analyzer (04's task; verifier must confirm).
