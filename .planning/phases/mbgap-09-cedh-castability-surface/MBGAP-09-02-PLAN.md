---
phase: mbgap-09-cedh-castability-surface
plan: 02
type: execute
wave: 2
depends_on: [01]
files_modified:
  - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerTests.cs
autonomous: true
requirements: [D-01, D-02, D-08, D-13, D-15]
must_haves:
  truths:
    - "Qualifying spells = PlanRole.Interaction with effective MV <= 2 after cost overrides (D-01, D-02)"
    - "The lens is computed cEDH-only; Casual and flag-off return null InteractionLens = byte-identical (D-15)"
    - "Headline is N/M spells at target using the existing CedhSupportThreshold (88), never a forked constant (D-08)"
    - "Zero qualifying spells produces a populated lens with QualifyingCount 0 (empty-state caution), not null (D-03)"
  artifacts:
    - path: "DeckFlow.Core/Manabase/ManabaseAnalyzer.cs"
      provides: "ComputeInteractionLens helper + interactionLens param + cEDH gate + Analyze wiring"
      contains: "ComputeInteractionLens"
  key_links:
    - from: "ManabaseAnalyzer.Analyze"
      to: "ManabaseReport.InteractionLens"
      via: "interactionLensActive ? ComputeInteractionLens(...) : null"
      pattern: "InteractionLens = interactionLensActive"
    - from: "ComputeInteractionLens"
      to: "castability rows + deck.Spells"
      via: "name join to filter PlanRole.Interaction and effective MV <= 2"
      pattern: "PlanRole.Interaction"
---

<objective>
Compute the cEDH early-interaction lens in the analyzer by deriving it from the already-built per-spell castability rows (no second simulation, per D-05), gated cEDH-only, and thread it into the `ManabaseReport`. This is the aggregation layer between Plan 01's simulator counter and the view/prompt consumers.

Purpose: Turn per-trial holdable counts into the "N / M interaction held up by turn 3" headline plus worst-first per-spell rows, with the qualifying-spell filter (D-01/D-02) and the reused threshold (D-08).
Output: `ComputeInteractionLens` + a new `interactionLens` param on `Analyze` + Core tests pinning the filter, gate, and aggregate.
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
From Plan 01: CardCastability.ByTurn3HoldableTrials (int), ManabaseInteractionLens
{ QualifyingCount, OnTargetCount, Threshold, Rows }, ManabaseInteractionRow
{ Name, HoldablePercent, IsCostOverridden }, ManabaseReport.InteractionLens (nullable).

Existing analyzer anchors (ManabaseAnalyzer.cs):
- `private const int CedhSupportThreshold = 88;` (line 17) — reuse, do NOT fork.
- cEDH gate precedent: `bool ritualBurstActive = ritualBurst && mode == ManabaseMode.Cedh;` (line 179);
  `bool ritualLandCreditActive = ritualLandCredit && mode == ManabaseMode.Cedh;` (line 169).
- `deck = ApplyCostOverrides(deck, costOverrides, out ...)` runs at line 163 — so deck.Spells and the
  castability rows built at line 184 are ALREADY effective-cost (override-aware); the row's OnCurveTurn
  is the override-aware effective cast turn (D-02 satisfied for free).
- castability rows: `IReadOnlyList<CardCastability> castability = BuildCastability(...)` (line 184).
- "derive, don't re-simulate" precedent: ComputeTapAnalysis (1035-1081), ComputeMulliganEvaluation (1090-1163).
- Analyze return construction sets `TapAnalysis = ...` / `MulliganEvaluation = ...` (lines 247-250).
- CardCastability has NO PlanRoles field — join rows to deck.Spells by Name (SpellRequirement.PlanRoles at ManabaseModels.cs:189).
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add interactionLens param, cEDH gate, ComputeInteractionLens, and Analyze wiring</name>
  <read_first>
    - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs (Analyze signature 143-155; gates 163-184; ComputeTapAnalysis 1035-1081; Analyze return 226-266)
    - DeckFlow.Core/Manabase/ManabaseModels.cs (SpellRequirement.PlanRoles line 189; CardCastability incl. Plan 01 counter; ManabaseInteractionLens/Row)
  </read_first>
  <action>
    Add a new `bool interactionLens = false` parameter to the full `Analyze(...)` overload (ManabaseAnalyzer.cs:143-155), placed alongside `ritualBurst`/`ritualLandCredit`, with an XML `<param>` doc stating flag-off (or non-cEDH) leaves output byte-identical (default false). Add `bool interactionLensActive = interactionLens && mode == ManabaseMode.Cedh;` next to the ritual gates.
    Add `private static ManabaseInteractionLens ComputeInteractionLens(ManabaseDeck deck, IReadOnlyList<CardCastability> castability, int defaultTrials, int threshold)`:
    - Build a Name -> SpellRequirement lookup from deck.Spells (case-insensitive, mirroring the analyzer's existing name-match rule near line 341).
    - Qualifying rows = castability rows whose matched spell `PlanRoles.HasFlag(PlanRole.Interaction)` AND whose effective MV <= 2, using the row's OnCurveTurn (override-aware) as the effective-MV signal per D-02. Exclude commander rows if the commander is not itself interaction (natural fallout of the PlanRole filter).
    - For each qualifying row: HoldablePercent = defaultTrials > 0 ? (int)Math.Round(100.0 * row.ByTurn3HoldableTrials / defaultTrials) : 0 (mirror ComputeTapAnalysis' averaging shape).
    - OnTargetCount = count of qualifying rows with HoldablePercent >= threshold. QualifyingCount = qualifying-row count. Rows sorted ascending by HoldablePercent (worst-first, matching the castability sort contract). Threshold = the passed-in value.
    - Return a populated lens even when QualifyingCount == 0 (empty rows list) so the view renders the D-03 caution; never return null from this method.
    In the Analyze return object, add `InteractionLens = interactionLensActive ? ComputeInteractionLens(deck, castability, CastabilitySimulator.DefaultTrials, CedhSupportThreshold) : null,` next to TapAnalysis/MulliganEvaluation. Pass CedhSupportThreshold (do not hardcode 88 again).
    Do NOT touch verdict/health/land-target math (D-13 informational v1).
  </action>
  <verify>
    <automated>grep -n "interactionLens\|ComputeInteractionLens\|InteractionLens = interactionLensActive\|CedhSupportThreshold" DeckFlow.Core/Manabase/ManabaseAnalyzer.cs | grep -v '^#'</automated>
  </verify>
  <acceptance_criteria>
    - `Analyze` has an `interactionLens` bool param defaulting false; `interactionLensActive` ANDs it with `mode == ManabaseMode.Cedh`.
    - ComputeInteractionLens filters on PlanRole.Interaction AND OnCurveTurn <= 2; passes CedhSupportThreshold through (no literal 88 in the new code).
    - Analyze return sets InteractionLens only when interactionLensActive, else null.
    - `dotnet build DeckFlow.Core` clean, 0 new warnings; no changes to ComputeTargetLands / verdict code.
  </acceptance_criteria>
  <done>The analyzer produces a populated ManabaseInteractionLens for cEDH+flag-on and null otherwise.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Core tests — filter, cEDH gate, effective-MV override, empty-state, N/M aggregate</name>
  <read_first>
    - DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerTests.cs (existing Analyze test setup, deck builders, cost-override and PlanRole tagging helpers)
    - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs (the Task 1 code)
  </read_first>
  <behavior>
    - Casual mode with interactionLens:true -> report.InteractionLens is null.
    - cEDH mode with interactionLens:false -> report.InteractionLens is null (byte-identical guard).
    - cEDH + interactionLens:true, a PlanRole.Interaction spell with a cost-override to effective MV 0 (Fierce-Guardianship-style) -> appears in Rows; a printed-MV3 interaction spell with no override -> excluded.
    - Zero qualifying spells -> InteractionLens is non-null with QualifyingCount 0 and empty Rows (D-03 empty-state).
    - OnTargetCount counts only rows with HoldablePercent >= 88; the headline pair is (OnTargetCount / QualifyingCount).
  </behavior>
  <action>
    Add ManabaseAnalyzerTests cases covering each bullet in the behavior block. Reuse existing deck/spell builders and the cost-override input path so the MV0-override case is exercised through the real ApplyCostOverrides machinery (not a fabricated row). Assert Rows are sorted worst-first. Keep existing default-Casual assertions intact (additive tests only).
  </action>
  <verify>
    <automated>MISSING — this task creates the tests; build DeckFlow.Core.Tests clean and run the new ManabaseAnalyzerTests cases via `dotnet test --filter` (record manual-harness result in SUMMARY if WSL VSTest cannot run).</automated>
  </verify>
  <acceptance_criteria>
    - Tests assert null for Casual and for cEDH-flag-off; non-null with QualifyingCount 0 for empty-state.
    - Override test proves effective-MV<=2 qualification comes through ApplyCostOverrides, not a hand-set field.
    - `dotnet build DeckFlow.Core.Tests` clean, 0 new warnings.
  </acceptance_criteria>
  <done>The filter (D-01/D-02), cEDH gate (D-15), empty-state (D-03), and N/M aggregate (D-08) are pinned by tests.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| none new | In-process analyzer aggregation over trusted, already-computed rows; no new external input |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-MBGAP09-03 | Information Disclosure | Lens leaking into Casual output | mitigate | Hard `mode == ManabaseMode.Cedh` gate mirrors ritual-burst precedent; tests assert null in Casual |
| T-MBGAP09-04 | Tampering | Forked threshold drifting from Karsten lens | mitigate | Reuse CedhSupportThreshold constant; no literal 88 in new code (grep-verified) |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean.
- New analyzer tests green (or manual-harness result recorded).
- Casual and flag-off paths produce null InteractionLens.
</verification>

<success_criteria>
ComputeInteractionLens produces a correctly filtered, worst-first, threshold-aggregated lens cEDH-only; Casual and flag-off remain byte-identical (null).
</success_criteria>

<output>
Create `.planning/phases/mbgap-09-cedh-castability-surface/MBGAP-09-02-SUMMARY.md` when done.
</output>
