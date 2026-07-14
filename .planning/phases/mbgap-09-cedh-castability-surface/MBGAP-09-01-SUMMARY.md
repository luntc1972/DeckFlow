---
phase: mbgap-09-cedh-castability-surface
plan: 01
status: complete
executor: codex gpt-5.4 (cross-AI), reviewed + committed by Claude
commits:
  - ff2ecfcf feat(MBGAP-09-01): add interaction-lens contracts to manabase models
  - e356a563 feat(MBGAP-09-01): record per-trial by-turn-3 holdable in simulator
key-files:
  created: []
  modified:
    - DeckFlow.Core/Manabase/ManabaseModels.cs
    - DeckFlow.Core/Manabase/CastabilitySimulator.cs
    - DeckFlow.Core.Tests/Manabase/CastabilitySimulatorTests.cs
---

# MBGAP-09-01 Summary — Core contracts + simulator by-turn-3 bookkeeping

## What was built

**Task 1 — model contracts (ff2ecfcf):**
- `CardCastability.ByTurn3HoldableTrials { get; init; }` — additive counter, safe default 0, placed after `Turn1UntappedTrials` with mirrored doc wording.
- `ManabaseInteractionRow` (required `Name`, required `HoldablePercent` 0-100, `IsCostOverridden`) and `ManabaseInteractionLens` (required `QualifyingCount`, `OnTargetCount`, `Threshold`, `Rows` worst-holdable-first). `QualifyingCount == 0` is a valid populated empty state (D-03) — not null-modeled.
- `ManabaseReport.InteractionLens` nullable slot next to `TapAnalysis`/`MulliganEvaluation`, defaults null.
- All new properties `{ get; init; }` (carve-out honored; CarveOutGuard green).

**Task 2 — simulator bookkeeping (e356a563):**
- Per-trial `hadByTurn3Holdable` out-flag in `SimulateGame`, evaluated on turns 1-3 BEFORE the `if (currentTurn < turn) continue;` early-exit (hadUntappedT1 precedent), so it fires independent of the spell's own effective turn (D-06/D-07 raw availability).
- Builds its own online-source view per turn via new `BuildOnlineSourceView` helper (mirrors — does not share — `availableColors`, which is stale behind the early-exit). The existing `availableColors` rebuild was refactored onto the same helper (behavior-identical).
- Castability check = effective mana quantity (`TotalMana >= effectiveCost`) AND full pip-count coverage (`ColorsCoverable`) from untapped/online sources — a UU spell needs two untapped blue-capable sources.
- No new RNG draw; counter accumulated into `ByTurn3HoldableTrials` on the returned `CardCastability`. Metric is mode-agnostic; cEDH gate lives in Plan 02.

## Verification
- `dotnet build` Core + Core.Tests: 0 warnings, 0 errors.
- Full Core suite: **1419/1419 pass** (Windows dotnet.exe; includes new `Simulate_ByTurn3HoldableTrials_AreDeterministicAndRespectUntappedCoverage`).
- Pinned expectations: mono-U UU 98-100%, scarce-W strictly lower, colorless MV2 exactly 100%, determinism (two identical Simulate calls → identical counters).
- EOL check: no churn (diff --stat == ignore-all-space stat; 0 CR before/after).

## Deviations
None.

## Self-Check: PASSED
