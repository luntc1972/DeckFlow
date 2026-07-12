---
phase: manabase-research-gap-closure
plan: 05
status: complete
completed: 2026-07-12
commits:
  - 558d0c32 feat(manabase): cEDH ritual land-target credit, calibration-fit 0.5 cap 3 (gap-05)
  - adbdfe94 feat(manabase): analysis.manabase.ritual-land-credit flag, seed OFF (gap-05)
  - aa183046 feat(cli): RitualCredit column in cedh-land-calibrate + docs (gap-05)
executor: codex gpt-5.4 medium (cross-AI); Claude reviewed + committed
verifier: foreman-verifier PASS_WITH_NOTES (MED evidence-in-VC -> this file + committed artifacts; LOW CedhCalibration aggregation untested -> surfaced)
checkpoint: task 4 human-verify PASSED 2026-07-12 — user accepted weight 0.5 / cap 3.0
---

# Plan 05 Summary — MBGAP-03 ritual land-target credit (cEDH-only)

## What shipped

- **`RitualLandCreditWeight = 0.5` / `RitualLandCreditCap = 3.0`** (`KarstenManabase.cs`): cEDH land target subtracts `min(cap, netPositiveRituals × weight)` BEFORE the floor/ceiling clamp — floor 22 always holds (adversarial floor test green). Ritual count reuses the existing O-4 net-positive one-shot machinery (`deck.OneShots`), no new regex.
- **`analysis.manabase.ritual-land-credit`** flag, seeded OFF both dialects (D-10 — deliberately NOT reusing `ritual-burst-mana`, which is live in prod; both flags read independently: burst→sim, credit→target).
- cEDH-only gate: `ritualLandCredit && mode == Cedh`; non-cEDH targets untouched.
- CLI `cedh-land-calibrate` gained a **RitualCredit** column calling the SAME credit function (no drift math).
- Tests: ON/OFF/non-cEDH/zero-ritual/floor-safe in `CedhLandTargetHybridTests` (15 green); flag parity ON≠OFF + OFF==baseline in service tests.

## Calibration evidence (D-09 blocking gate — PASSED)

Corpus: `_calib6/decks_all.json`, **3281 cEDH decks** (corpus grew past the 1597 figure in the older baseline doc — recorded as-is, not rewritten). Full tables committed alongside: `05-calibration-before.md` / `05-calibration-after.md`.

| Metric | Hybrid (live) | + RitualCredit |
|---|---|---|
| Under-target % | 21.8% | **11.1%** |
| Avg target | 25.4 | 24.7 (−0.7) |
| Newly flagged under | — | **0** |
| Un-flagged | — | 351 |
| Sisay (grindy) under% | 5% | 1% |
| Kinnan under% | 12% | 12% |
| Floor-22 hits | 53 | 53 (floor holds) |

**Checkpoint decision:** user accepted 0.5/3.0 (2026-07-12). Rationale: one-way direction (0 regressions), cap-bounded, grindy commanders healthy; 11.1% under-flag accepted vs 22% precedent because ritual-heavy decks genuinely run lighter.

## Surfaced coverage gap (accepted, not fixed)

`CedhCalibration`'s new RitualCredit aggregation math (~90 lines of mean/percent/un-flag deltas) has no unit tests — it is CLI-calibration tooling, not a prod path; verified empirically against the corpus run. Flag if it grows.

## Validation

Build 0/0; Core FULL 1392/0; Web FULL 1357/14skip/0; EOL clean. Flag OFF in all seeds — deploy-safe; operator flip needs nothing further (constant already user-approved).
