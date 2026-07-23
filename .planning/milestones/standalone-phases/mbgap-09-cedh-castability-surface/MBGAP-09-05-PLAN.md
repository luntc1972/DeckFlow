---
phase: mbgap-09-cedh-castability-surface
plan: 05
type: execute
wave: 4
depends_on: [03, 04]
files_modified:
  - DeckFlow.Web/Models/ManabaseViewModel.cs
  - DeckFlow.Web/Models/ManabaseDisplay.cs
  - DeckFlow.Web/Controllers/ManabaseController.cs
  - DeckFlow.Web.Tests/Manabase/ManabaseViewModelTests.cs
  - DeckFlow.Web.Tests/Manabase/ManabaseDisplayTests.cs
autonomous: true
requirements: [D-09, D-10, D-11, D-12, D-14]
must_haves:
  truths:
    - "ShowCastability is mode-aware: cEDH renders the table only when the interaction-lens flag is on (D-09)"
    - "ManabaseViewModel exposes ShowCedhInteractionLens fed from the analysis result (D-10)"
    - "Display helpers exist for the holdable badge (thresholded at 88), the caveat gloss, and worst-5 capping (D-11, D-12)"
    - "The report-text artifact call site is fed the lens so the pasteable report carries it (D-14)"
  artifacts:
    - path: "DeckFlow.Web/Models/ManabaseViewModel.cs"
      provides: "ShowCedhInteractionLens property + mode-aware ShowCastability"
      contains: "ShowCedhInteractionLens"
    - path: "DeckFlow.Web/Models/ManabaseDisplay.cs"
      provides: "interaction holdable marker + gloss + worst-visible count helper"
      contains: "InteractionHoldable"
  key_links:
    - from: "ManabaseController"
      to: "ManabaseViewModel.ShowCedhInteractionLens"
      via: "result.ShowCedhInteractionLens assignment on normal-path view model"
      pattern: "ShowCedhInteractionLens = result.ShowCedhInteractionLens"
    - from: "ManabaseController"
      to: "ManabaseReportTextBuilder.Build"
      via: "interactionLens argument fed from result.Report.InteractionLens"
      pattern: "interactionLens:"
---

<objective>
Wire the Web presentation layer below the view: make `ShowCastability` mode-aware (cEDH shows the table under the flag, D-09), expose `ShowCedhInteractionLens` on the view model, add the display helpers the view needs (holdable badge thresholded at 88, caveat gloss, worst-5 cap), and feed the lens into the report-text builder call site (D-14).

Purpose: Give Plan 06's Razor view a complete, tested contract of properties + helpers so the .cshtml is pure markup.
Output: View model + display helpers + controller wiring + Web unit tests.
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
From Plan 04: ManabaseAnalysisResult.ShowCedhInteractionLens (bool).
From Plan 03: ManabaseReportTextBuilder.Build accepts `interactionLens:` (optional).
From Plan 01/02: report.InteractionLens (ManabaseInteractionLens?, populated cEDH+flag-on).

Anchors:
- ManabaseViewModel.cs: ShowTapAnalyzer/ShowMulliganEval/ShowPlanPresence bools (56-63);
  current Casual-only gate `public bool ShowCastability => Report is { Mode: ManabaseMode.Casual, Castability.Count: > 0 };` (110).
- ManabaseController.cs: normal-path view model construction sets `ShowTapAnalyzer = result.ShowTapAnalyzer` (117);
  ManabaseReportTextBuilder.Build call at 152-154 passing `tap: result.ShowTapAnalyzer ? result.Report.TapAnalysis : null`.
- ManabaseDisplay.cs: TapMarker/KeepableMarker return (Css, Marker) reusing manabase-lens-met/manabase-lens-short (107-121);
  gloss consts KarstenSourceGloss/CastRateGloss/TapAnalyzerGloss (30-49); capped-table helpers
  DefaultVisibleCastabilityCount + CastabilitySummaryText (184-236) — the "worst N + <details>" precedent.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Mode-aware ShowCastability + ShowCedhInteractionLens on the view model, wired from the controller</name>
  <read_first>
    - DeckFlow.Web/Models/ManabaseViewModel.cs (bools 56-63; ShowCastability 106-110)
    - DeckFlow.Web/Controllers/ManabaseController.cs (view model construction ~104-130; report-text Build call 152-154; other view model sites 64, 263-292)
    - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs (ShowCedhInteractionLens on the result)
  </read_first>
  <action>
    In ManabaseViewModel.cs add `public bool ShowCedhInteractionLens { get; init; }` next to the other Show* bools. Change ShowCastability so it renders when castability rows exist AND (Mode == Casual OR (Mode == Cedh AND ShowCedhInteractionLens)) — Casual behavior stays exactly as today; cEDH renders the table only when the flag is on (D-09, kill switch). In ManabaseController.cs, on the normal-path view model (the branch that sets ShowTapAnalyzer at 117) set `ShowCedhInteractionLens = result.ShowCedhInteractionLens`. Also update the ManabaseReportTextBuilder.Build call (152-154) to pass `interactionLens: result.ShowCedhInteractionLens ? result.Report.InteractionLens : null` (D-14 report artifact). Do not set ShowCedhInteractionLens on the early error/empty view-model branches (they carry no report).
  </action>
  <verify>
    <automated>grep -n "ShowCedhInteractionLens\|ShowCastability\|interactionLens:" DeckFlow.Web/Models/ManabaseViewModel.cs DeckFlow.Web/Controllers/ManabaseController.cs</automated>
  </verify>
  <acceptance_criteria>
    - ShowCastability returns true for Casual with rows (unchanged) and for cEDH with rows only when ShowCedhInteractionLens is true.
    - Controller sets ShowCedhInteractionLens from the result and feeds interactionLens into the report-text builder.
    - `dotnet build DeckFlow.Web` clean, 0 new warnings.
  </acceptance_criteria>
  <done>The view model exposes the gate and the report artifact carries the lens.</done>
</task>

<task type="auto">
  <name>Task 2: Display helpers — holdable marker, caveat gloss, worst-visible count</name>
  <read_first>
    - DeckFlow.Web/Models/ManabaseDisplay.cs (TapMarker 107-121; gloss consts 30-49; capped-table helpers 184-236)
  </read_first>
  <action>
    Add `public static (string Css, string Marker) InteractionHoldableMarker(int holdablePercent, int threshold)` returning `("manabase-lens-met", "✓")` when holdablePercent >= threshold else `("manabase-lens-short", "⚠")` — reuse the existing CSS classes/glyphs, no new tokens (mirror TapMarker; threshold passed in from the lens's Threshold so 88 is not re-hardcoded). Add a `CedhInteractionLensGloss` const (plain-English one-liner) carrying the raw-availability caveat "assumes you hold mana open" (D-07), alongside KarstenSourceGloss/CastRateGloss. Add `public const int DefaultVisibleInteractionCount = 5;` (D-11 worst-5) and, if helpful, a small summary-text helper mirroring CastabilitySummaryText that states the hidden remainder count (never silent truncation, L2). Keep everything pure/static.
  </action>
  <verify>
    <automated>grep -n "InteractionHoldableMarker\|CedhInteractionLensGloss\|DefaultVisibleInteractionCount" DeckFlow.Web/Models/ManabaseDisplay.cs</automated>
  </verify>
  <acceptance_criteria>
    - InteractionHoldableMarker returns met above/at threshold and short below, reusing manabase-lens-met/short.
    - CedhInteractionLensGloss contains "assumes you hold mana open".
    - DefaultVisibleInteractionCount == 5.
    - `dotnet build DeckFlow.Web` clean, 0 new warnings.
  </acceptance_criteria>
  <done>Display helpers exist for the badge, caveat, and worst-5 capping.</done>
</task>

<task type="auto">
  <name>Task 3: Web unit tests for the gate + helpers</name>
  <read_first>
    - DeckFlow.Web.Tests/Manabase/ManabaseViewModelTests.cs and ManabaseDisplayTests.cs (existing conventions; create if absent, mirroring sibling tests)
    - DeckFlow.Web/Models/ManabaseViewModel.cs, ManabaseDisplay.cs (Task 1/2 code)
  </read_first>
  <action>
    Add ViewModel tests: Casual + rows -> ShowCastability true (unchanged); cEDH + rows + ShowCedhInteractionLens false -> ShowCastability false; cEDH + rows + ShowCedhInteractionLens true -> ShowCastability true. Add Display tests: InteractionHoldableMarker returns met at 88 and 90 with threshold 88, short at 87; gloss contains the caveat string. Construct view models/lens objects directly (no controller/service needed).
  </action>
  <verify>
    <automated>build DeckFlow.Web.Tests clean and run the new ManabaseViewModelTests/ManabaseDisplayTests via `dotnet test --filter` (record manual-harness result in SUMMARY if WSL VSTest cannot run).</automated>
  </verify>
  <acceptance_criteria>
    - Gate tests cover the three cEDH/Casual states above.
    - Marker threshold boundary (87 vs 88) asserted.
    - `dotnet build DeckFlow.Web.Tests` clean, 0 new warnings.
  </acceptance_criteria>
  <done>The presentation contract is test-locked for Plan 06 to consume.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| analysis result -> view model | Booleans control which surfaces render; card names not yet rendered here (Plan 06 owns markup) |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-MBGAP09-09 | Information Disclosure | cEDH table showing when flag off | mitigate | ShowCastability ANDs cEDH with ShowCedhInteractionLens; unit-tested |
| T-MBGAP09-10 | Tampering | Re-hardcoding threshold 88 in Web | mitigate | Marker takes threshold as a param sourced from lens.Threshold |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean.
- New ViewModel/Display tests green.
- Casual ShowCastability behavior unchanged.
</verification>

<success_criteria>
ShowCastability is correctly mode+flag-aware, ShowCedhInteractionLens flows from result to view model, helpers exist and are tested, and the report artifact call site is fed the lens.
</success_criteria>

<output>
Create `.planning/phases/mbgap-09-cedh-castability-surface/MBGAP-09-05-SUMMARY.md` when done.
</output>
