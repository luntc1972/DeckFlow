# Plan 103-01 Summary

## Measured Timing

- Measured `ManabaseAnalyzer.Analyze` wall-clock on a 147-card singleton pool at the default trial count (`20_000`): `5164 ms`. (Fixture sums to 147 cards, not the plan's nominal ~130 — conservative: larger pool measures slower, so the reduced in-loop trial choice below is safe.)

## In-Loop Trial-Count Decision

- Plan `103-05` should reduce in-loop delta analyses to `4000` trials.
- Keep the default `20_000` trials for the baseline snapshot and round-summary passes.
- Reason: `5164 ms` is well above the ~`1s` target and the `3s` hard cap for per-decision work; scaling the in-loop pass down to `4000` trials preserves the existing engine while moving the hot path close to the target budget.

## Tasks Completed

1. Defined the shared 7-family Cut Lab metric contract, named noise-floor constants, and fixed Phase-103 category-by-turn defaults.
2. Added contract-shape guards for family count, `Flood`/`Screw`/`Curve`, and the named noise-floor constants.
3. Added determinism guards for repeat analysis and shuffled-input analysis against the existing engine.
4. Added a timing spike fact that measures one default-trial analyze pass on a realistic oversized singleton pool and emits the elapsed milliseconds.

## Files Changed

- `DeckFlow.Web/Models/CutLab/CutLabMetrics.cs`
- `DeckFlow.Web.Tests/CutLabMetricsContractTests.cs`
- `DeckFlow.Web.Tests/CutLabEngineDeterminismTests.cs`
- `.planning/workstreams/cut-lab/phases/103-simulation-engine-guided-cut-rounds/103-01-SUMMARY.md`

## Deviations

- The required verify commands were run exactly as written.
- One additional detailed `dotnet test` invocation was run after the required Task 4 verify command so the passing xUnit timing output would be visible and could be recorded here.
