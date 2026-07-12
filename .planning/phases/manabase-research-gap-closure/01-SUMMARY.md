---
phase: manabase-research-gap-closure
plan: 01
status: complete
completed: 2026-07-12
commits:
  - 037bc446 test(manabase): scaffold ConditionalCountLand sim tests, Skip-gated (gap-01)
  - 5a97d8ad feat(manabase): detect all six untapped-land cycles with count-condition metadata (gap-01)
  - 1af282d1 test(manabase): cycle classification tests + oracle canaries + rules doc (gap-01)
executor: codex gpt-5.4 medium (cross-AI); Claude reviewed + committed
verifier: foreman-verifier PASS_WITH_NOTES (2 LOW, both resolved/accepted)
---

# Plan 01 Summary — MBGAP-02 cycle detection + count-condition contract

## What shipped

- **`CountConditionKind` contract** (`ManabaseModels.cs`): `None | FastLand | SlowLand | EldThreshold` + `CountCondition`/`CountThreshold`/`CountTypeFilter` metadata on `ManaSource`. Sim consumes it in plan 02.
- **Six cycle detectors** (`ManabaseClassifier.cs`, new `ClassifySpecialLand`):
  - Fast lands → per-trial metadata (threshold 2, untapped when other-lands ≤ 2)
  - Slow lands → per-trial metadata (threshold 2, untapped when other-lands ≥ 2)
  - ELD threshold (Mystic Sanctuary class) → per-trial metadata + named-basic-type filter (threshold 3)
  - Verge (DSK/DFT 10-card cycle) → always untapped; second color gated on two-type census (≥6), **both** capture groups feed census
  - Training Compound (MSH 5-card allied cycle) → always untapped; {C} unconditional; allied colors gated on new true-Basic-supertype census `CountBasicLands` (≥6)
  - Vivid lands → ETB-tapped base color + 0.25-weight conditional any-color source
- **Canary assertions** for all 6 new regexes in `ManabaseLiveOracleCanaryTests.cs` (verified against live Scryfall wording).
- **Skip-gated scaffold** `ConditionalCountLandTests.cs` (5 facts, plan 02 removes Skip).
- `docs/manabase-analysis-rules.md` updated: six-cycle rules, per-trial vs static split, backlog comment removed.

## Deviations / incidents

1. **Env**: fresh worktree lacked `DeckFlow.Web/node_modules` → `npm ci` (orchestrator).
2. **Inherited break**: main was broken between `d4be87d0` and `d32dea0e` (`LocalStampFailed` removal); ff-merged `d32dea0e` into branch.
3. **Regression caught & fixed (attempt 2)**: first `VergeSecondColorRegex` (`Activate only if you control a ([^.]+)`) false-matched **Nimbus Maze** in the Stale Brago calibration fixture → band Workable→Needs work. Anchored to the two-basic-type "or" template; added `BragoRegressionGuard_NimbusMaze_DoesNotMatchVergePath` negative test.

## Validation

- Build `DeckFlow.sln`: 0 warnings / 0 errors (Windows dotnet).
- Core.Tests Manabase filter: 290 passed / 5 skipped (scaffold) / 0 failed.
- Web.Tests FULL suite: 1349 passed / 14 skipped (env-gated) / 0 failed — incl. all 18 `ManabaseHealthBandRegressionTests`.
- EOL: no CRLF, no whitespace churn (`--stat` == `--ignore-all-space --stat`).
- Blind verify: PASS_WITH_NOTES — LOW-1 missing SUMMARY (this file), LOW-2 canaries packed in one Fact (accepted; numeric criterion met).

## Notes for later plans

- Plan 02 consumes `CountCondition*` in `CastabilitySimulator` and un-Skips the 5 scaffold facts.
- Verge/Training-Compound color gating is static census — flag-ON behavior changed only for decks containing those cycles (accuracy bundle ON per D-08).
