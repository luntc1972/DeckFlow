---
phase: manabase-research-gap-closure
plan: 02
status: complete
completed: 2026-07-12
commits:
  - 8c74695b feat(manabase): resolve fast/slow/ELD tapped-state per trial in sim (gap-02)
  - 2d3a2066 test(manabase): accuracy-off parity for six-cycle deck + rules doc (gap-02)
  - 843a82ef fix(manabase): gate six-cycle classification behind accuracy bundle (gap-02)
executor: codex gpt-5.4 medium (cross-AI); Claude reviewed + committed
verifier: foreman-verifier FAIL (2 HIGH) -> fix 843a82ef -> re-verify PASS (harness-reproduced)
---

# Plan 02 Summary — MBGAP-02 per-trial sim resolution

## What shipped

- **`CardKind.ConditionalCountLand`** in `CastabilitySimulator`: fast (untapped iff other-lands ≤ 2), slow (≥ 2), ELD (≥ 3 in-play lands bearing the named basic type) resolved **per trial at land-play time**, before the new land joins `landsOnBoard` (correct "other lands" semantics). D-07 satisfied — no static-census fallback.
- **`PlayedLand.BasicTypeMask`**: every played land carries a 5-bit basic-type mask. Approximation: monocolor land → its basic type; multicolor/colorless → untyped unless classifier supplied `CountTypeFilter`. Conservative (ELD under-counts typed duals → tapped more often).
- **Gating architecture** (post-verifier fix): six-cycle classification gated ONCE at `ManabaseClassifier.ClassifySpecialLand` via the existing `checkLandUntapped` accuracy-bundle param; sim keys purely on `CountCondition != None` metadata presence. No new flag threading.
- Scaffold tests enabled (5 real facts + new ELD-below-threshold tapped case = 6).
- Real parity test: accuracy ON ≠ OFF diverges on a six-cycle deck; OFF == no-flag-cache baseline byte-identical.
- `docs/manabase-analysis-rules.md`: per-trial path + gate documented.

## Verification incident (the reason this plan has 3 commits)

Blind verifier **FAILED** the first pass with 2 HIGH:
1. Sim path keyed on `gateRampOnCastable` (hardcoded `true` since R2 M3) → six-cycle behavior ran with accuracy OFF (12-pt CastPercent swing on a 3-fast-land deck, proven via throwaway console harness). Plan-01's classifier call was also ungated.
2. The parity test compared two accuracy-OFF configs — vacuous.

Fix `843a82ef`; re-verify PASS with the harness reproducing both the original bug (pre-fix OFF=29 vs baseline 25) and the fix (OFF=25==25; ON=29 diverges).

## Validation

- Build 0/0; Core.Tests FULL 1381/0/0; Web.Tests FULL 1350 passed / 14 env-skipped / 0 failed.
- EOL clean (no CRLF, no whitespace churn).

## Notes

- `analysis.manabase.accuracy` is ON in prod → six-cycle modeling goes live on deploy (intended, D-08).
- Deviation: fix commit expanded write set to ManabaseClassifier.cs + ManabaseClassifierTests.cs (gating belongs to plan-01 files) — deliberate, logged.
