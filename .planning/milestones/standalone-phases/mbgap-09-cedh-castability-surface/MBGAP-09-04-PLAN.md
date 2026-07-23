---
phase: mbgap-09-cedh-castability-surface
plan: 04
type: execute
wave: 3
depends_on: [02, 03]
files_modified:
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
  - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs
  - DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs
autonomous: true
requirements: [D-14, D-15]
must_haves:
  truths:
    - "A new cEDH-only flag analysis.manabase.cedh-interaction-lens is seeded ON in both dialects (D-15)"
    - "The service reads the flag fail-safe, threads interactionLens into Analyze, and exposes ShowCedhInteractionLens on the result (D-15)"
    - "The swap prompt call site is fed report.InteractionLens so the artifact carries the lens (D-14)"
    - "Flag-off produces byte-identical output (kill switch)"
  artifacts:
    - path: "DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs"
      provides: "seed rows for the new flag, TRUE (Postgres) + 1 (SQLite)"
      contains: "analysis.manabase.cedh-interaction-lens"
    - path: "DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs"
      provides: "flag key const, IsFlagOn read, Analyze thread, ShowCedhInteractionLens, swap-prompt param"
      contains: "CedhInteractionLensFlagKey"
  key_links:
    - from: "ManabaseAnalysisService"
      to: "ManabaseAnalyzer.Analyze"
      via: "interactionLens: IsFlagOn(CedhInteractionLensFlagKey)"
      pattern: "interactionLens:"
    - from: "ManabaseAnalysisService"
      to: "ManabaseSwapPromptBuilder.Build"
      via: "interactionLens: report.InteractionLens"
      pattern: "interactionLens: report.InteractionLens"
---

<objective>
Add the cEDH-only feature flag `analysis.manabase.cedh-interaction-lens` (seeded ON) and thread it through the Web analysis service: read the flag, pass `interactionLens` into `ManabaseAnalyzer.Analyze`, expose `ShowCedhInteractionLens` on the result, and feed the lens into the swap-prompt builder call site. Flag-off = byte-identical current output.

Purpose: The kill-switch flag and its plumbing are the gate for every UI/artifact surface in later plans.
Output: Flag catalog + dual-dialect seed + service wiring + Web tests.
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
From Plan 02: ManabaseAnalyzer.Analyze now accepts `bool interactionLens = false`; report.InteractionLens is populated cEDH-only.
From Plan 03: ManabaseSwapPromptBuilder.Build now accepts `ManabaseInteractionLens? interactionLens = null`.

Anchors (ManabaseAnalysisService.cs):
- Flag const precedent: `public const string RitualBurstFlagKey = "analysis.manabase.ritual-burst-mana";` (218);
  seeded-ON doc style at TapAnalyzerFlagKey/MulliganEvalFlagKey (193-204). CAUTION: those XML comments
  still literally say "seeded OFF" — stale doc drift from before the default flip; the actual seed rows
  are TRUE/1. Follow the actual current seed value (ON) as ground truth for the new flag's doc wording,
  not the stale comment text at those anchors.
- IsFlagOn reads at 282-299 (fail-safe OFF, IsFlagOn helper at 469-472 returns false for a missing key).
- Analyze call at ~305-320 passing ritualBurst:/ritualLandCredit:.
- ManabaseAnalysisResult record with ShowTapAnalyzer/ShowMulliganEval/ShowPlanPresence (115-121); set at
  BOTH assembly sites — early-return branch (332-334) AND normal-path return (430-432).
- Swap-prompt build calls at 414 and 419 (two arms).

Flag seeding (both files move together; FeatureFlagCatalogTests guards that every seeded key has a description):
- FeatureFlagCatalog.cs description dict, mulligan-eval/plan-presence entries (99-110).
- FeatureFlagStore.cs seed SQL, Postgres block (228-233, TRUE) and SQLite block (270-275, 1);
  ON CONFLICT / OR-IGNORE clause preserves operator overrides — do not touch it.

Prompt-cache note (D-15 discretion): grep confirms NO manabase artifact replay cache exists
(PromptMutatingAnalysisFlags is analysis-packet-side only) — no cache-set membership to add here.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Register + seed the flag (catalog description + dual-dialect seed rows)</name>
  <read_first>
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs (description entries 99-110)
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs (Postgres seed 228-233, SQLite seed 270-275, and the conflict clause)
    - DeckFlow.Web.Tests/FeatureFlags/FeatureFlagCatalogTests.cs (the seed<->description guard, if present)
  </read_first>
  <action>
    In FeatureFlagCatalog.cs add a description entry keyed `"analysis.manabase.cedh-interaction-lens"` stating: cEDH-only; gates the "Early interaction" header lens, the full per-card castability table exposure in cEDH mode, and the two prompt-artifact blocks; seeded ON; off = byte-identical output (kill switch). In FeatureFlagStore.cs add `('analysis.manabase.cedh-interaction-lens', TRUE)` to the Postgres seed block and `('analysis.manabase.cedh-interaction-lens', 1)` to the SQLite seed block. Do not modify the ON CONFLICT / OR IGNORE clause. Keep the two files in sync (the catalog guard test fails otherwise).
  </action>
  <verify>
    <automated>grep -rn "analysis.manabase.cedh-interaction-lens" DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs</automated>
  </verify>
  <acceptance_criteria>
    - The key appears once in the catalog description dict and once in EACH dialect seed block (TRUE / 1).
    - The conflict-preservation clause is unchanged.
    - `dotnet build DeckFlow.Web` clean; FeatureFlagCatalogTests seed<->description guard passes.
  </acceptance_criteria>
  <done>The flag is cataloged and seeded ON in both dialects without disturbing operator-override preservation.</done>
</task>

<task type="auto">
  <name>Task 2: Thread the flag through ManabaseAnalysisService (read, Analyze, ShowCedhInteractionLens, swap prompt)</name>
  <read_first>
    - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs (const region 193-225; IsFlagOn reads 282-299; Analyze call 305-320; result record 115-121; assembly sites 332-334 and 430-432; swap-prompt calls 414-419; IsFlagOn helper 469-472)
    - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs (new interactionLens param) and ManabaseSwapPromptBuilder.cs (new interactionLens param)
  </read_first>
  <action>
    Add `public const string CedhInteractionLensFlagKey = "analysis.manabase.cedh-interaction-lens";` with a seeded-ON XML doc (word it like TapAnalyzerFlagKey/MulliganEvalFlagKey, not the seeded-OFF ritual-burst doc). Add `bool interactionLens = IsFlagOn(CedhInteractionLensFlagKey);` in the flag-read block. Pass `interactionLens: interactionLens` into the ManabaseAnalyzer.Analyze call. CRITICAL — plan-role classification gate: the service currently passes `classifyPlanRoles: showPlanPresence` (line 309); if plan-presence is OFF but the interaction lens is ON, PlanRoles would stay None and the lens would falsely report zero qualifying interaction (D-01 violation). Change the argument to `classifyPlanRoles: showPlanPresence || (interactionLens && options.Mode == ManabaseMode.Cedh)` (match however Mode is referenced at that site) so the lens always has role tags when active. Add `public bool ShowCedhInteractionLens { get; init; }` to ManabaseAnalysisResult and set it at BOTH assembly sites to `interactionLens && options.Mode == ManabaseMode.Cedh` (match however Mode is referenced at each site). Feed the swap-prompt builder: at both build calls (414, 419) pass `interactionLens: report.InteractionLens` (which is already null unless cEDH+flag-on, so the cEDH-arm gets data and other arms pass null harmlessly). Do not add a manabase replay-cache set (none exists — note this in the SUMMARY as D-15 discretion resolved).
  </action>
  <verify>
    <automated>grep -n "CedhInteractionLensFlagKey\|ShowCedhInteractionLens\|interactionLens: interactionLens\|interactionLens: report.InteractionLens" DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs</automated>
  </verify>
  <acceptance_criteria>
    - The flag const exists with seeded-ON doc wording; read via IsFlagOn (fail-safe OFF path unchanged).
    - Analyze is called with interactionLens; ShowCedhInteractionLens is set at BOTH result assembly sites, ANDed with cEDH mode.
    - classifyPlanRoles argument is `showPlanPresence || (interactionLens && cEDH-mode)` — grep shows the OR at the call site (line ~309).
    - Both swap-prompt build calls receive interactionLens: report.InteractionLens.
    - `dotnet build DeckFlow.Web` clean, 0 new warnings.
  </acceptance_criteria>
  <done>The service reads the flag, produces the lens cEDH-only, and feeds it to the swap prompt.</done>
</task>

<task type="auto">
  <name>Task 3: Web tests — flag on/off behavior + swap-prompt lens content</name>
  <read_first>
    - DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs (existing flag-gate test conventions; fake flag cache setup)
    - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs (the Task 2 code)
  </read_first>
  <action>
    Add tests: cEDH deck with the flag ON -> result.ShowCedhInteractionLens is true, report.InteractionLens is non-null, and the produced swapPrompt contains the N/M pair (proving the call-site threading). cEDH deck with the flag OFF -> ShowCedhInteractionLens false, report.InteractionLens null, swapPrompt equals the pre-change generic sentence (byte-identical). Casual deck with the flag ON -> ShowCedhInteractionLens false. Plan-role gate test: cEDH deck with plan-presence flag OFF + interaction lens ON containing a known cheap interaction spell -> report.InteractionLens.QualifyingCount > 0 (proves classifyPlanRoles fires for the lens independently of plan-presence). Reuse the existing fake IFeatureFlagCache to toggle the keys.
  </action>
  <verify>
    <automated>build DeckFlow.Web.Tests clean and run the new ManabaseAnalysisServiceTests cases via `dotnet test --filter` (record manual-harness result in SUMMARY if WSL VSTest cannot run).</automated>
  </verify>
  <acceptance_criteria>
    - Flag-on cEDH test asserts ShowCedhInteractionLens true + non-null lens + N/M in swapPrompt.
    - Flag-off test asserts byte-identical swapPrompt and null lens.
    - Casual-flag-on test asserts ShowCedhInteractionLens false.
    - Plan-presence-OFF + lens-ON test asserts QualifyingCount > 0 (role tagging active for the lens).
    - `dotnet build DeckFlow.Web.Tests` clean, 0 new warnings.
  </acceptance_criteria>
  <done>Flag behavior and swap-prompt lens content are test-locked.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| feature-flag store -> analysis path | Flag value controls exposure of the lens/table/artifacts |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-MBGAP09-07 | Elevation of Privilege | Missing flag row defaulting ON unexpectedly | mitigate | IsFlagOn returns false for a missing key (fail-safe OFF); seed explicitly sets ON; test covers both |
| T-MBGAP09-08 | Tampering | Seed clobbering an operator override on re-bootstrap | mitigate | ON CONFLICT / OR IGNORE clause left untouched (verified) |
| T-MBGAP09-SC | Tampering | npm/pip/cargo installs | n/a | No package installs in this phase; no dependency additions |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean.
- FeatureFlagCatalogTests + new ManabaseAnalysisServiceTests green.
- Flag-off swapPrompt byte-identical.
</verification>

<success_criteria>
The flag exists (seeded ON, both dialects), the service threads it end-to-end, and flag-off output is byte-identical.
</success_criteria>

<output>
Create `.planning/phases/mbgap-09-cedh-castability-surface/MBGAP-09-04-SUMMARY.md` when done.
</output>
