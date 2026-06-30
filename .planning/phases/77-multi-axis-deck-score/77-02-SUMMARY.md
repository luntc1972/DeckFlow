---
phase: 77-multi-axis-deck-score
plan: 02
subsystem: analysis
tags: [deck-score, multi-axis, scorer, band-derivation, bracket-cross-check, core]

# Dependency graph
requires:
  - phase: 77-01
    provides: DeckStatSummary.Tutors/FastMana/RampDrawUnderThreeMv/Counters signals + DeckStatClassifier predicates
provides:
  - DeckMultiAxisScore + DeckScoreRationale records (DeckFlow.Core/Analysis/MultiAxisScore.cs)
  - MultiAxisScorer.Score() four-axis band derivation + BandLabel() (DeckFlow.Core/Analysis/MultiAxisScorer.cs)
  - bracket cross-check (ScoreAlignsBracket + BracketCrossCheckText)
affects: [77-04 packet-service score wiring + BuildScoreBlockText, 77-05/77-06 view/CSS]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pure static Core scorer (no DI/HTTP), chained-if threshold gates per axis, single switch only for BandLabel"
    - "Null-vs-empty combo semantics surfaced as a comboDetectionAvailable bool -> 'combo data unavailable' rationale"

key-files:
  created:
    - DeckFlow.Core/Analysis/MultiAxisScore.cs
    - DeckFlow.Core/Analysis/MultiAxisScorer.cs
    - DeckFlow.Core.Tests/MultiAxisScorerTests.cs
  modified: []

key-decisions:
  - "Band derivations use chained-if threshold gates (NOT switch expressions) per the re-indent carve-out; BandLabel is the only switch expression in the scorer"
  - "Every band clamped to [0,5] via Math.Clamp after derivation; no decimals ever exposed (SCORE-01)"
  - "Rationale strings are ASCII (commas, plain hyphens, no em/en dashes) and InvariantCulture-formatted decimals so output is locale-deterministic and paste-safe"
  - "Cross-check misaligns only on gross contradiction: Power>=4 with bracket<=2, or Power<=1 with bracket>=4 (UI-SPEC heuristic disclosure)"

patterns-established:
  - "Pattern: golden cEDH-vs-battlecruiser separation test guards calibration; per-axis Control/Consistency derivation tests + BandLabel theory"

metrics:
  duration: ~10 min
  completed: 2026-06-29
---

# Phase 77 Plan 02: Multi-Axis Deck Scorer Summary

Created the deterministic pure-Core heart of the feature: a `DeckMultiAxisScore` record and a static `MultiAxisScorer` that maps the existing + Phase-77-01 deck signals into four coarse 0-5 magnitude bands (Power/Speed/Control/Consistency) with inline rationale and a bracket cross-check. Calibrated band cutpoints so the golden cEDH-vs-battlecruiser separation holds on first run.

## What Was Built

### Task 1 - MultiAxisScore records + MultiAxisScorer (feat `5a2bda0b`)
- `DeckFlow.Core/Analysis/MultiAxisScore.cs`: `public sealed record DeckMultiAxisScore(...)` (positional, XML doc on type + every param) carrying four band ints, four `DeckScoreRationale`, the `BracketNumber`, `BracketCrossCheckText`, and `ScoreAlignsBracket`; plus `public sealed record DeckScoreRationale(string SignalText)`.
- `DeckFlow.Core/Analysis/MultiAxisScorer.cs`: `public static DeckMultiAxisScore Score(DeckStatSummary stats, int gameChangerCount, int twoCardComboCount, bool comboDetectionAvailable, int bracketNumber)` guarded by `ArgumentNullException.ThrowIfNull(stats)`.
  - **Power** — GC-dominant (10+ GC => 5; 4-9 GC + combo/fast-mana => 4) with combo + fast-mana modifiers.
  - **Speed** — `AverageManaValue` primary driver gated with `FastMana` + `RampDrawUnderThreeMv` thresholds.
  - **Control** — `Interaction` + `Wipes` + `Counters`.
  - **Consistency** — `Tutors` + `twoCardComboCount` redundancy + low-`AverageManaValue` smoothness.
  - All four bands `Math.Clamp(..., 0, 5)`. `BandLabel(int)` is the documented switch expression (`0..5` => None/Low/Modest/Moderate/High/Extreme, `_ => "Extreme"`).
  - Combo-unavailable (`comboDetectionAvailable == false`) emits `combo data unavailable` in the Power + Consistency rationale instead of asserting `0 two-card combos`.
  - Cross-check: `ScoreAlignsBracket = !((powerBand >= 4 && bracketNumber <= 2) || (powerBand <= 1 && bracketNumber >= 4))`; agree text vs divergence text (names the contradiction) per UI-SPEC §6/§10, ASCII ` - ` hyphens.

### Task 2 - MultiAxisScorerTests + calibration (test `3f0b27d4`)
- `DeckFlow.Core.Tests/MultiAxisScorerTests.cs` (`public sealed class`, `using DeckFlow.Core.Analysis;`): `CedhStats()`/`CasualStats()`/`ControlStats()`/`ConsistencyStats()` `DeckStatSummary` factories using init-field syntax for the four new signals.
- `BandLabel_MapsCorrectly` `[Theory]` (7 rows incl. `6 => Extreme` clamp); golden `Score_CedhDeck_ScoresPowerAndSpeedHigh` (>=4) + `Score_CasualDeck_ScoresPowerAndSpeedLow` (<=2) + an explicit separation test; `Score_DenseInteraction_ScoresControlHigh` (>=4); `Score_ManyTutorsAndCombos_ScoresConsistencyHigh` (>=4); combo-disclosure, rationale signal-carry, align/divergence cross-check, all-bands-in-range, and null-stats guard tests.
- **18 tests GREEN on first calibration run** — no cutpoint retuning was needed.

## Verification
- `dotnet.exe build DeckFlow.Core`: Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~MultiAxisScorer"`: 18 passed, 0 failed.
- Full `dotnet.exe test DeckFlow.Core.Tests`: 944 passed, 0 failed (was 926 before this plan; +18 new).
- `grep -c "switch" MultiAxisScorer.cs` -> 1 (BandLabel only; band derivations use chained-if per carve-out).
- Changed-lines format gate (`scripts/format-check-changed.sh staged`): clean on all three files.

## Deviations from Plan
None - plan executed exactly as written. Band cutpoints in the RESEARCH tables were used as starting points and passed the golden test on the first run, so no calibration deviation was required.

## Known Stubs
None.

## Threat Flags
None - pure Core deterministic mapping over integer signals + a bracket number; no I/O, DI, network, or NuGet packages (matches plan threat_model T-77-02-01 accept / T-77-SC mitigate).

## Self-Check: PASSED
- Files: MultiAxisScore.cs, MultiAxisScorer.cs, MultiAxisScorerTests.cs all present.
- Commits: 5a2bda0b (feat), 3f0b27d4 (test) both present in git log.
