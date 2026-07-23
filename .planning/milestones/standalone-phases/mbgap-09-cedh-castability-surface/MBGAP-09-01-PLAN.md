---
phase: mbgap-09-cedh-castability-surface
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Core/Manabase/ManabaseModels.cs
  - DeckFlow.Core/Manabase/CastabilitySimulator.cs
  - DeckFlow.Core.Tests/Manabase/CastabilitySimulatorTests.cs
autonomous: true
requirements: [D-04, D-05, D-06, D-07, D-08]
must_haves:
  truths:
    - "CardCastability carries a per-trial by-turn-3 holdable counter, additive with safe default 0 (D-05)"
    - "CastabilitySimulator records, per trial, whether the spell was castable from untapped/online sources on at least one of turns 1-3 (D-06, D-07)"
    - "A new ManabaseInteractionLens record and a nullable InteractionLens slot on ManabaseReport exist as contracts for downstream plans (D-04, D-08)"
  artifacts:
    - path: "DeckFlow.Core/Manabase/ManabaseModels.cs"
      provides: "CardCastability.ByTurn3HoldableTrials field, ManabaseInteractionLens + ManabaseInteractionRow records, ManabaseReport.InteractionLens slot"
      contains: "ByTurn3HoldableTrials"
    - path: "DeckFlow.Core/Manabase/CastabilitySimulator.cs"
      provides: "per-trial by-turn-3 holdable bookkeeping inside the existing SimulateGame loop"
      contains: "ByTurn3Holdable"
  key_links:
    - from: "CastabilitySimulator.SimulateGame trial loop"
      to: "CardCastability.ByTurn3HoldableTrials"
      via: "per-trial 0/1 accumulation returned in the Simulate result"
      pattern: "ByTurn3Holdable"
    - from: "ManabaseReport"
      to: "ManabaseInteractionLens"
      via: "nullable InteractionLens slot"
      pattern: "InteractionLens"
---

<objective>
Lay the Core contracts and simulator bookkeeping for the cEDH early-interaction lens. Extend the existing Monte-Carlo `CastabilitySimulator` with a per-trial "by-turn-3 holdable" observation (no second engine, per D-05), surface it as an additive counter on `CardCastability`, and define the `ManabaseInteractionLens` DTO + its nullable slot on `ManabaseReport` that Plans 02-06 consume.

Purpose: Everything downstream (analyzer aggregation, prompt artifacts, view) reads these contracts. Defining them first prevents a scavenger hunt in later plans.
Output: New model fields/records + populated simulator counter + a hand-checked Core unit test that pins the metric.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/phases/mbgap-09-cedh-castability-surface/MBGAP-09-CONTEXT.md
@.planning/phases/mbgap-09-cedh-castability-surface/MBGAP-09-PATTERNS.md

<interfaces>
Existing contracts to extend (from the codebase):

CardCastability (ManabaseModels.cs:205-248) already carries: Name, ManaValue, OnCurveTurn,
IsCommander, IsCostOverridden, LimitingFactor, AverageDelay, and the additive counter
`int Turn1UntappedTrials { get; init; }` (lines 242-248) — the exact precedent to copy.

ManabaseReport nullable-slot precedent (ManabaseModels.cs:1179): `ManabaseTapAnalysis? TapAnalysis { get; init; }`.

ManabasePlanPresence (ManabaseModels.cs:1387-1423) is the closest structural analog for the new
lens record: a PlanRole-scoped deck-level Monte-Carlo read with a headline count/percent + per-spell rows.

Simulator seam: Simulate(...) (CastabilitySimulator.cs:244-254) returns CardCastability; the
per-trial loop is SimulateGame (CastabilitySimulator.cs:1092-1300). hadUntappedT1 is set at
lines 1230-1233 BEFORE the `if (currentTurn < turn) continue;` early-exit (line 1236), so it
always fires regardless of the spell's own effective turn. HasColorMatchedUntappedT1
(CastabilitySimulator.cs:1628-1660) is the color-match helper to generalize.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Define lens model contracts (CardCastability counter, ManabaseInteractionLens, report slot)</name>
  <read_first>
    - DeckFlow.Core/Manabase/ManabaseModels.cs (read CardCastability 205-248; the ManabaseReport slot region near 1170-1195; ManabasePlanPresence 1387-1423; PlanRole enum ~153)
    - ./CLAUDE.md (the `{ get; init; }` carve-out: never `{ get; }` — System.Text.Json drops get-only props in .NET 9+; CarveOutGuard enforces it)
  </read_first>
  <action>
    In ManabaseModels.cs make three additive edits, all properties declared `{ get; init; }` (never `{ get; }`):
    (1) Add `int ByTurn3HoldableTrials { get; init; }` to `CardCastability`, immediately after `Turn1UntappedTrials`, with a doc-comment mirroring Turn1UntappedTrials' "Additive — safe default 0 so existing construction/serialization is unaffected" wording, stating it counts trials in which the spell was castable from untapped/online sources on at least one of turns 1-3.
    (2) Add a `public sealed record ManabaseInteractionRow` with `required string Name`, `required int HoldablePercent` (0-100), and `bool IsCostOverridden`. Add a `public sealed record ManabaseInteractionLens` with `required int QualifyingCount`, `required int OnTargetCount`, `required int Threshold`, and `required IReadOnlyList<ManabaseInteractionRow> Rows` (worst-holdable first). QualifyingCount == 0 is a valid populated state (the empty-state caution, D-03) — do NOT model empty as null here.
    (3) Add `public ManabaseInteractionLens? InteractionLens { get; init; }` to `ManabaseReport`, next to `TapAnalysis`/`MulliganEvaluation`, with a doc-comment mirroring the TapAnalysis slot ("Additive — defaults null so existing serialization/tests are unaffected. Populated by ManabaseAnalyzer cEDH-only when the interaction-lens flag is on.").
    No simulator or analyzer changes in this task — contracts only.
  </action>
  <verify>
    <automated>grep -n "ByTurn3HoldableTrials\|record ManabaseInteractionLens\|record ManabaseInteractionRow\|InteractionLens { get; init; }" DeckFlow.Core/Manabase/ManabaseModels.cs</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c "{ get; }" ` on the three added blocks returns 0 (all new props are `{ get; init; }`).
    - ManabaseInteractionLens exposes QualifyingCount, OnTargetCount, Threshold, Rows; ManabaseInteractionRow exposes Name, HoldablePercent, IsCostOverridden.
    - `dotnet build DeckFlow.Core` succeeds with 0 new warnings.
    - CarveOutGuard test still passes.
  </acceptance_criteria>
  <done>Model contracts compile; new props are init-settable; report has a nullable InteractionLens slot defaulting null.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Record per-trial by-turn-3 holdable in CastabilitySimulator + hand-checked test</name>
  <read_first>
    - DeckFlow.Core/Manabase/CastabilitySimulator.cs (Simulate 244-360; SimulateGame loop 1092-1300, esp. hadUntappedT1 at 1223-1239 and the availableColors greedy coverage 1241-1300; HasColorMatchedUntappedT1 1628-1660)
    - DeckFlow.Core/Manabase/ManabaseModels.cs (CardCastability with the Task 1 counter)
    - DeckFlow.Core.Tests/Manabase/CastabilitySimulatorTests.cs (existing test conventions, fixed-seed determinism, deck-builder helpers)
  </read_first>
  <behavior>
    - A 2-pip interaction spell (e.g. UU) on a mono-blue all-untapped-source deck yields ByTurn3HoldablePercent at or near 100.
    - The same spell on a deck whose blue sources are scarce/tapped early yields a materially lower percent (strictly less than the mono-U case).
    - Determinism: two Simulate calls with the same inputs return identical ByTurn3HoldableTrials (stable per-spell seed, no new RNG draw).
    - A colorless MV<=2 spell (no colored pips) is holdable whenever any untapped source is online by turn 3.
  </behavior>
  <action>
    In SimulateGame, add a per-trial boolean (default false) that becomes true the first turn in {1,2,3} on which the spell is castable using ONLY untapped/online sources available that turn — full colored-pip coverage AND effective mana quantity, evaluated against an online-source set with the SAME shape the on-curve check builds (availableColors) — but do NOT literally reuse that list: it is only populated on the spell's own effective turn behind the early-exit, so for a spell with effective turn > 3 the turns 1-3 reads would see a stale/unset list. Build the by-turn-3 check's own online-source view unconditionally on each of turns 1-3 (mirroring, not sharing, the availableColors construction), not merely single-color access. Evaluate it BEFORE the `if (currentTurn < turn) continue;` early-exit (mirroring the hadUntappedT1 placement at 1230) so it fires independent of the spell's own effective turn — this is a property of the spell's OWN pips (D-06/D-07 raw availability). Do NOT introduce a second RNG draw. Generalize HasColorMatchedUntappedT1 to a by-turn variant (or add a full-coverage helper) that OR-accumulates across turns 1-3; keep the existing `<= 1` colorless/color-mask semantics for the color-match portion but require the pip COUNT to be met from untapped sources so a UU spell needs two untapped blue-capable sources. Accumulate the per-trial 0/1 into a counter and set `ByTurn3HoldableTrials` on the returned CardCastability. The metric is computed unconditionally (mode-agnostic); the cEDH gate lives in the analyzer (Plan 02).
    Add a CastabilitySimulatorTests case that pins the number for a fixed deck/library config with a fixed trial budget, asserting the mono-U ~100 vs scarce-white lower ordering and determinism.
  </action>
  <verify>
    <automated>MISSING — this task creates the test; build DeckFlow.Core.Tests clean and run the new CastabilitySimulatorTests case (VSTest is unreliable in WSL — prefer `dotnet build` clean plus a targeted `dotnet test --filter` run; if the runner cannot execute, document the manual harness result in the SUMMARY).</automated>
  </verify>
  <acceptance_criteria>
    - New test asserts a concrete expected ByTurn3HoldablePercent (within a stated +/- tolerance) for the fixed config, and asserts mono-U >= scarce-white ordering.
    - Determinism assertion: two identical Simulate calls return equal ByTurn3HoldableTrials.
    - `dotnet build DeckFlow.Core` and `dotnet build DeckFlow.Core.Tests` succeed, 0 new warnings.
    - No new RNG draw added inside the trial loop (grep shows no new `rng.` / `new Random` in the added block).
  </acceptance_criteria>
  <done>Simulator populates ByTurn3HoldableTrials; a hand-checked Core test pins the metric and its determinism.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| none new | Pure in-process CPU math over already-loaded deck data; no new input surface, no I/O, no serialization sink reachable by an attacker in this plan |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-MBGAP09-01 | Tampering | New `{ get; init; }` JSON round-trip of ManabaseInteractionLens | mitigate | Carve-out enforced: init-only props keep System.Text.Json round-trip intact; CarveOutGuard test gates it |
| T-MBGAP09-02 | Denial of Service | Extra per-trial observation in the hot sim loop | accept | O(1) per turn over existing board state, no extra draws/allocation growth; trial budget unchanged |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean (0 new warnings).
- New CastabilitySimulator test passes (or manual-harness result recorded in SUMMARY per WSL VSTest caveat).
- CarveOutGuard test green.
</verification>

<success_criteria>
CardCastability.ByTurn3HoldableTrials is populated by the simulator, ManabaseInteractionLens/Row records and the ManabaseReport.InteractionLens slot exist, and the metric is pinned by a deterministic hand-checked test.
</success_criteria>

<output>
Create `.planning/phases/mbgap-09-cedh-castability-surface/MBGAP-09-01-SUMMARY.md` when done.
</output>
