---
phase: mbgap-09-cedh-castability-surface
plan: 02
status: complete
executor: codex gpt-5.4 (cross-AI), reviewed + committed by Claude
commits:
  - 38c8af07 feat(MBGAP-09-02): compute cEDH early-interaction lens in analyzer
  - dfdb9525 test(MBGAP-09-02): pin interaction-lens filter, gate, and aggregate
key-files:
  created: []
  modified:
    - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerTests.cs
---

# MBGAP-09-02 Summary — Analyzer aggregation

## What was built
- `Analyze(...)` gains `bool interactionLens = false` (placed with the ritual flags, XML-doc'd for byte-identical flag-off) and `interactionLensActive = interactionLens && mode == ManabaseMode.Cedh` mirroring the ritual-gate precedent.
- `ComputeInteractionLens(deck, castability, defaultTrials, threshold)`: case-insensitive Name→SpellRequirement join; qualifies rows on `PlanRoles.HasFlag(PlanRole.Interaction)` AND post-override `SpellRequirement.ManaValue <= 2` (never OnCurveTurn — D-02 reducer exclusion); `HoldablePercent = Round(100.0 * ByTurn3HoldableTrials / defaultTrials)`; rows worst-first (percent asc, then ordinal name for stable ties); `OnTargetCount` counts rows ≥ threshold; `CedhSupportThreshold` (88) passed through, no forked literal. QualifyingCount==0 returns a populated lens (D-03).
- Analyze return: `InteractionLens = interactionLensActive ? ComputeInteractionLens(...) : null` next to TapAnalysis/MulliganEvaluation. Verdict/health/land-target math untouched (D-13).

## Verification
- LEAD call-site audit: new param inserted before `useHealthBandCastability`; both external callers (Web ManabaseAnalysisService, CLI ManabaseCommandRunner) use named arguments — no silent positional shift.
- Codex run: Core + Core.Tests builds 0 warnings; ManabaseAnalyzerTests 66/66 pass (4 new: Casual-null, cEDH-flag-off-null, filter+sort, empty-state).
- Full-suite authoritative run deferred to the wave-2 post-merge gate (runs after Plan 03 lands; single working tree).
- EOL: no churn (stat == ignore-all-space stat; 0 CR both sides).

## Deviations
None.

## Self-Check: PASSED
