---
phase: 81-opening-hand-mulligan-evaluator
plan: 01
subsystem: analysis
tags: [manabase, monte-carlo, london-mulligan, castability-simulator, dotnet, xunit]

# Dependency graph
requires:
  - phase: 75-tap-analyzer
    provides: "TAP-02 precedent: pure-observation additive counter (Turn1UntappedTrials) on the existing 20k-trial CastabilitySimulator.Simulate loop, aggregated in ManabaseAnalyzer.ComputeTapAnalysis"
provides:
  - "OpeningHandSample Core DTO + CardCastability keepable/keep-size/opener additive fields"
  - "Two-stage pure-observation opening-hand instrumentation on CastabilitySimulator.Simulate's existing trial loop (no second sim, no new rng draw, cast% byte-identical)"
  - "ManabaseMulliganEvaluation deck-level aggregate (keepable band, keep-size distribution, has-a-plan percent, early-row representative openers) attached to ManabaseReport"
  - "ComputeMulliganEvaluation aggregator + internal test seam (ComputeMulliganEvaluationForTest)"
affects: [81-02-flag-and-artifact, 81-03-on-page-readout]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Two-stage trial instrumentation: STAGE 1 buckets keep-size right after LondonMulligan returns (before SimulateGame); STAGE 2 builds the attributed sample after SimulateGame yields firstCastableTurn"
    - "Bucket by the RETURNED keep value (7/6/5), never the mulligan-depth index, so a singleton's Commander free-mulligan-at-depth-1 keep-7 lands in Kept7Trials"
    - "internal ...ForTest seam (mirrors ColorKeepSatisfiedForTest) exposes a private aggregator to unit tests over hand-constructed rows, no Monte-Carlo required"

key-files:
  created:
    - DeckFlow.Core.Tests/Manabase/CastabilitySimulatorMulliganTests.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseMulliganEvaluationTests.cs
  modified:
    - DeckFlow.Core/Manabase/CastabilitySimulator.cs
    - DeckFlow.Core/Manabase/ManabaseModels.cs
    - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs
    - DeckFlow.Core.Tests/Manabase/CastabilitySimulatorTests.cs

key-decisions:
  - "Keep-size counters bucket by the VALUE LondonMulligan returns (captured as `keptSize` before the tiny-deck Math.Min clamp), never by mulligan-depth index — proven via a singleton-vs-non-singleton same-seed comparison showing the singleton's free depth-1 mulligan credits materially more Kept7Trials"
  - "HasPlan ('workable line') requires >=2 lands AND land-color coverage >= min(DeckColorCount, ColorKeepCap) AND the TRACKED spell's own on-curve castability — never merely 'a non-land is in hand'"
  - "Representative openers are selected from the EARLIEST (lowest ManaValue, then OnCurveTurn) non-commander rows so the surfaced on-curve read is always about a genuine early play, never a late bomb"
  - "Fixed a pre-existing test (SimulateCompanion_UsesDeckLibrarySizeExcludingCommanders) that relied on full-record equality: List<T> has no value equality, so the new RepresentativeOpeners field broke Assert.Equal(direct, viaHelper) even though every scalar field matched exactly — split into a structural IEnumerable comparison for the list plus a `with {}`-normalized equality for the rest"

patterns-established:
  - "Golden/never-contradict pattern for a second read derived from the same Monte-Carlo pass: assert the new figure moves in the SAME direction as the existing cast-rate figure under the same gate (ColorKeepCap), so the two readouts can never disagree"

requirements-completed: [MULLIGAN-01, MULLIGAN-02, MULLIGAN-03, MULLIGAN-04, MULLIGAN-05]

# Metrics
duration: 25min
completed: 2026-07-03
---

# Phase 81 Plan 01: Opening-Hand / Mulligan Sim Instrumentation Summary

**Two-stage pure-observation instrumentation on the existing London-mulligan Monte-Carlo pass surfaces a keepable-hand band, keep-size distribution, and spell-attributed representative openers — no second simulation, cast% byte-identical.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-07-03T15:59:35Z
- **Completed:** 2026-07-03T16:21:02Z
- **Tasks:** 3
- **Files modified:** 6 (2 new test files, 4 modified)

## Accomplishments

- `CastabilitySimulator.Simulate`'s existing 20k-trial loop now collects `KeepableTrials`/`Kept7Trials`/`MulliganTo6Trials`/`MulliganTo5Trials` counters and up to 3 `OpeningHandSample` representative openers, bucketed by the keep VALUE `LondonMulligan` returns (not the mulligan-depth index) — proven correct for the singleton free-mulligan edge case via a directional same-seed comparison test.
- Each representative opener is attributed to the row's own tracked spell (`TrackedSpellName`, `TrackedOnCurveTurn`, `OnCurveCastable`) and gates `HasPlan` on that tracked early play actually being castable on curve — never a generic "a non-land is in hand" claim.
- `ManabaseAnalyzer.ComputeMulliganEvaluation` aggregates a deck-level `ManabaseMulliganEvaluation` (keepable band, keep-size percents, has-a-plan percent, early-row openers) from the already-computed castability rows, attached to `ManabaseReport.MulliganEvaluation` beside `TapAnalysis` — always computed in Core, no second `Simulate` call.
- Golden tests prove the keepable figure moves with (never contradicts) the existing cast-rate figure: a color-screwed 2-color fixture shows a lower `KeepableHandPercent` than a well-fixed one under the same `ColorKeepCap` gate that also lowers cast%.

## Task Commits

1. **Task 1: OpeningHandSample record + CardCastability fields + two-stage instrumentation** - `e6137ece` (feat)
2. **Task 2: ManabaseMulliganEvaluation record + ComputeMulliganEvaluation aggregator** - `bba5f6c4` (feat)
3. **Task 3: Golden reuse + never-contradict + no-second-Simulate regression tests** - `4ccd57c7` (test)

_No plan-metadata commit yet — this SUMMARY.md + STATE.md/ROADMAP.md/REQUIREMENTS.md update is the final commit for this plan._

## Files Created/Modified

- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` - two-stage pure-observation instrumentation on the trial loop; keep-size counters + up to 3 attributed `OpeningHandSample`s per row
- `DeckFlow.Core/Manabase/ManabaseModels.cs` - `OpeningHandSample` + `ManabaseMulliganEvaluation` records; additive `CardCastability` keepable/keep-size/opener fields; `ManabaseReport.MulliganEvaluation`
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` - `ComputeMulliganEvaluation` aggregator (early-row opener selection, non-commander averaging) + `ComputeMulliganEvaluationForTest` seam; wired into `Analyze`
- `DeckFlow.Core.Tests/Manabase/CastabilitySimulatorMulliganTests.cs` - pinned cast% byte-identity, keep-size sum invariant, composition-sums-to-kept-size, determinism, singleton free-mulligan Kept7 crediting, tracked-spell attribution, uncastable-color HasPlan=false
- `DeckFlow.Core.Tests/Manabase/ManabaseMulliganEvaluationTests.cs` - aggregator unit tests (band thresholds, keep-size percents, non-commander averaging, early-row opener selection + truncation, empty/all-commander safe-zero) plus full-`Analyze()` golden/never-contradict/no-second-Simulate tests
- `DeckFlow.Core.Tests/Manabase/CastabilitySimulatorTests.cs` - fixed `SimulateCompanion_UsesDeckLibrarySizeExcludingCommanders` for the new `List<T>`-valued field (see Deviations)

## Decisions Made

- Bucketed keep-size by the RETURNED `LondonMulligan` value (captured before the tiny-deck `Math.Min` clamp) rather than the mulligan-depth index, per the plan's explicit correctness constraint for singleton (Commander) decks.
- Added an `internal ComputeMulliganEvaluationForTest` seam (mirroring the existing `ColorKeepSatisfiedForTest` pattern) so the aggregator's pure math is unit-testable over hand-constructed rows without driving the 20k-trial Monte-Carlo pass.
- Excluded the FORCED final mulligan-to-5 keep from the ">= 2 lands" keep-floor test assertion: the London-mulligan schedule's forced final depth uses `Lo=1` (not `Lo=2`), so a forced 5-card keep can legitimately hold just 1 land — this is correct simulator behavior, not a bug, and the test was adjusted to check the floor only on non-forced (`keep 7` / `mulligan to 6`) openers.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed a pre-existing test broken by full-record equality on the new `List<T>` field**
- **Found during:** Task 1 (adding `RepresentativeOpeners` to `CardCastability`)
- **Issue:** `CastabilitySimulatorTests.SimulateCompanion_UsesDeckLibrarySizeExcludingCommanders` asserted `Assert.Equal(direct, viaHelper)` on two independently-computed (but deterministically identical) `CardCastability` records. Records synthesize member-wise `Equals`, but `List<T>` (the runtime type behind `IReadOnlyList<OpeningHandSample>`) has no value equality — two structurally-identical-but-distinct `List<T>` instances are never `Equal`. The test started failing even though every scalar field (including the new int counters) matched exactly.
- **Fix:** Split the assertion into a structural `IEnumerable<T>`-based comparison for `RepresentativeOpeners` (xUnit's `Assert.Equal` does element-wise comparison for enumerables) plus a `with { RepresentativeOpeners = Array.Empty<...>() }`-normalized `Assert.Equal` for the rest of the record.
- **Files modified:** `DeckFlow.Core.Tests/Manabase/CastabilitySimulatorTests.cs`
- **Verification:** `dotnet test --filter CastabilitySimulatorTests` — 2/2 pass.
- **Committed in:** `e6137ece` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 — bug fix)
**Impact on plan:** Necessary to keep the existing test suite green after adding the plan-mandated `RepresentativeOpeners` field. No scope creep; production behavior is unchanged (the fix is test-only).

## Issues Encountered

The plan's Task 3 behavior text asked for a mono-color fixture where "the representative openers all report >= 2 lands (the keep floor)." Empirical verification showed the FORCED final mulligan-to-5 keep (schedule `Lo=1`) can legitimately hold 1 land — this is correct, intentional London-mulligan behavior (a forced keep must accept whatever hand remains), not a gap in the plan's non-forced-depth reasoning (`Lo=2` at depths 0-2). The test was scoped to assert the floor only on non-forced (`keep 7` / `mulligan to 6`) openers, which is what the plan's underlying schedule actually guarantees.

## User Setup Required

None - no external service configuration required. This plan is pure `DeckFlow.Core` with no HTTP/DI/config surface.

## Next Phase Readiness

- `ManabaseReport.MulliganEvaluation` is populated (always computed in Core, like `TapAnalysis`) and ready for 81-02 to flag-gate at the Web layer (`analysis.mulligan-eval`, seeded OFF both dialects) and thread into the single `ManabaseReportTextBuilder` paste artifact.
- `OpeningHandSample.TrackedSpellName`/`TrackedOnCurveTurn`/`OnCurveCastable`/`HasPlan` give 81-03's on-page lens everything it needs to render a spell-attributed "workable line" read without any new sim call.
- No blockers. Build 0/0 (full solution); Core 1049/1049 pass; Web 1144/1156 pass (12 pre-existing Postgres-integration skips); format-gate clean on all changed lines.

---
*Phase: 81-opening-hand-mulligan-evaluator*
*Completed: 2026-07-03*

## Self-Check: PASSED

All 6 files created/modified confirmed present on disk; all 3 task commits (`e6137ece`, `bba5f6c4`, `4ccd57c7`) confirmed present in git log.
