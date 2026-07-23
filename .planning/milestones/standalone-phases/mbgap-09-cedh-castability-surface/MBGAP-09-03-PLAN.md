---
phase: mbgap-09-cedh-castability-surface
plan: 03
type: execute
wave: 2
depends_on: [01]
files_modified:
  - DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs
  - DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseSwapPromptBuilderTests.cs
autonomous: true
requirements: [D-13, D-14]
must_haves:
  truths:
    - "The report-text artifact gains an 'Early interaction (turns 1-3)' block carrying N/M + worst spells (D-14)"
    - "The swap prompt's generic cEDH prose line is upgraded with the real N/M and worst spells (D-14)"
    - "Both builders are byte-identical when interactionLens is null (kill-switch safety)"
    - "The block carries the raw-availability caveat and an informational-only disclaimer (D-07, D-13)"
  artifacts:
    - path: "DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs"
      provides: "AppendInteractionLensBlock + optional interactionLens param on Build"
      contains: "Early interaction"
    - path: "DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs"
      provides: "upgraded cEDH interaction prose fed by lens data"
      contains: "interactionLens"
  key_links:
    - from: "ManabaseReportTextBuilder.Build"
      to: "AppendInteractionLensBlock"
      via: "if (interactionLens is not null) append"
      pattern: "interactionLens is not null"
    - from: "ManabaseSwapPromptBuilder.Build"
      to: "interaction prose"
      via: "real N/M + worst spells when lens present, generic fallback when null"
      pattern: "interactionLens"
---

<objective>
Wire the interaction lens into BOTH prompt artifacts (D-14: a lens ChatGPT cannot see is half-shipped). Add an "Early interaction (turns 1-3)" block to `ManabaseReportTextBuilder` and upgrade the generic cEDH prose line in `ManabaseSwapPromptBuilder` to carry the real N/M number and worst spells. Both stay byte-identical when the lens is null.

Purpose: The pasteable artifacts are the core product value; the lens must appear in them.
Output: Optional `interactionLens` params + block/prose + Core tests asserting null=byte-identical and populated=contains N/M and worst spell names.
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
From Plan 01: ManabaseInteractionLens { QualifyingCount, OnTargetCount, Threshold,
IReadOnlyList<ManabaseInteractionRow> Rows }; ManabaseInteractionRow { Name, HoldablePercent, IsCostOverridden }.

Analogs (do not re-simulate; these are pure formatters):
- ManabaseReportTextBuilder.Build signature carries optional `tap`/`mulligan` params (47-58); the
  gated-append precedent is `if (tap is not null) { AppendTapAnalysisBlock(...); sb.AppendLine(); }` (172-185),
  with AppendTapAnalysisBlock at 248-271 and AppendMulliganEvaluationBlock at 277-322 (note its
  "first-pass read only ... not a recommendation" closing tone — replicate for D-13).
- ManabaseSwapPromptBuilder.Build optional-param precedent at 28-36 (verdict/budget/companionRow); the
  exact cEDH prose to replace is lines 48-53 ("This is a cEDH deck — favor low land counts ...
  prioritize early (turn 1-3) untapped colored access for cheap interaction."). Formatting convention in
  this file: string.Create(CultureInfo.InvariantCulture, $"...") (e.g. 83-84, 97-98).
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Add the "Early interaction (turns 1-3)" block to ManabaseReportTextBuilder</name>
  <read_first>
    - DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs (Build signature 47-58; gated tap append 172-185; AppendTapAnalysisBlock 248-271; AppendMulliganEvaluationBlock 277-322)
    - DeckFlow.Core/Manabase/ManabaseModels.cs (ManabaseInteractionLens/Row)
  </read_first>
  <behavior>
    - interactionLens null -> Build output is byte-identical to today (block absent).
    - interactionLens with QualifyingCount>0 -> output contains a header line naming "Early interaction (turns 1-3)", the "X / Y interaction held up by turn 3" pair, the worst spells with their holdable %, the caveat "assumes you hold mana open", and an informational-only disclaimer.
    - interactionLens with QualifyingCount==0 -> a one-line caution "no cheap interaction found" (D-03), no per-spell rows.
  </behavior>
  <action>
    Add `ManabaseInteractionLens? interactionLens = null` to Build(...), mirroring the tap/mulligan optional-param style. Add `private static void AppendInteractionLensBlock(StringBuilder sb, ManabaseInteractionLens lens)` and call it gated by `if (interactionLens is not null) { AppendInteractionLensBlock(sb, interactionLens); sb.AppendLine(); }`, placed among the existing tap/mulligan blocks. The block: a header, the "OnTargetCount / QualifyingCount interaction held up by turn 3" line, the worst spells (cap the listed rows to the worst several — Rows are already worst-first) each with HoldablePercent, the raw-availability caveat verbatim ("assumes you hold mana open"), and a closing informational-only line echoing AppendMulliganEvaluationBlock's "first-pass read ... not a recommendation" tone (D-13). Handle QualifyingCount==0 with the caution line and no rows. Use string.Create(CultureInfo.InvariantCulture, $"...") for interpolated numbers.
  </action>
  <verify>
    <automated>MISSING — Task 3 creates the tests; for now build DeckFlow.Core clean and grep the new symbols.</automated>
  </verify>
  <acceptance_criteria>
    - Build has an optional `interactionLens` param defaulting null; append is gated on non-null.
    - `grep -n "Early interaction\|assumes you hold mana open\|AppendInteractionLensBlock"` matches.
    - `dotnet build DeckFlow.Core` clean, 0 new warnings.
  </acceptance_criteria>
  <done>The report text artifact carries the lens block when data is present and is unchanged when null.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Upgrade the cEDH interaction prose in ManabaseSwapPromptBuilder</name>
  <read_first>
    - DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs (optional-param precedent 28-36; the target prose block 48-53; InvariantCulture formatting 83-98)
    - DeckFlow.Core/Manabase/ManabaseModels.cs (ManabaseInteractionLens/Row)
  </read_first>
  <behavior>
    - interactionLens null -> the existing generic cEDH sentence (lines 48-53) is emitted verbatim (byte-identical).
    - interactionLens with QualifyingCount>0 -> the sentence is replaced by prose stating the real "N / M" interaction-held-up count and naming the worst spells, still cEDH-gated.
    - interactionLens with QualifyingCount==0 -> prose states no cheap interaction was found.
  </behavior>
  <action>
    Add `ManabaseInteractionLens? interactionLens = null` to Build(...), mirroring the existing optional params (verdict/budget/companionRow). Inside the `if (mode == ManabaseMode.Cedh)` block, when interactionLens is non-null emit the upgraded prose (real OnTargetCount/QualifyingCount + worst spell names from the worst-first Rows) via string.Create(CultureInfo.InvariantCulture, $"..."); when null, keep the current generic sentence exactly as-is so flag-off output is byte-identical. Do not change any non-cEDH branch.
  </action>
  <verify>
    <automated>MISSING — Task 3 creates the tests; build DeckFlow.Core clean and grep for the new param.</automated>
  </verify>
  <acceptance_criteria>
    - Build has an optional `interactionLens` param defaulting null; null path emits the original sentence unchanged.
    - Populated path interpolates OnTargetCount/QualifyingCount and worst spell names.
    - `dotnet build DeckFlow.Core` clean, 0 new warnings.
  </acceptance_criteria>
  <done>The swap prompt carries real lens data cEDH-only, generic prose preserved when null.</done>
</task>

<task type="auto">
  <name>Task 3: Core tests for both builders (null=byte-identical, populated=contains N/M + worst spells)</name>
  <read_first>
    - DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs and ManabaseSwapPromptBuilderTests.cs (existing conventions; if either does not exist, create it mirroring the sibling builder test class)
    - DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs, ManabaseSwapPromptBuilder.cs (the Task 1/2 code)
  </read_first>
  <action>
    Add tests: for each builder, one case with interactionLens null asserting the output equals the pre-change output (capture the current output as the expected baseline, or assert the block/upgraded-prose markers are absent); one case with a populated lens (QualifyingCount>0) asserting the output contains "N / M" (the exact OnTargetCount/QualifyingCount pair), the worst spell name(s), and the caveat string; one empty-state case (QualifyingCount==0) asserting the caution wording appears and no per-spell rows. Construct ManabaseInteractionLens directly in the test (no analyzer/sim dependency).
  </action>
  <verify>
    <automated>build DeckFlow.Core.Tests clean and run the new builder test classes via `dotnet test --filter` (record manual-harness result in SUMMARY if WSL VSTest cannot run).</automated>
  </verify>
  <acceptance_criteria>
    - Null-lens tests prove byte-identical output for both builders.
    - Populated tests assert the exact N/M pair and worst spell name(s) appear; empty-state test asserts the caution wording.
    - `dotnet build DeckFlow.Core.Tests` clean, 0 new warnings.
  </acceptance_criteria>
  <done>Both artifacts are test-locked: kill-switch safety and populated content proven.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| artifact -> user clipboard/ChatGPT | Card names flow into plain-text artifacts; these are text builders, no HTML sink |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-MBGAP09-05 | Tampering | Flag-off artifact drift | mitigate | Null-lens byte-identical tests gate both builders |
| T-MBGAP09-06 | Information Disclosure | Overclaiming certainty in prompt | mitigate | Raw-availability caveat + informational-only disclaimer required in the block (D-07/D-13) |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean.
- Builder tests green (or manual-harness result recorded).
- Null-lens output confirmed byte-identical for both builders.
</verification>

<success_criteria>
Both prompt artifacts carry the lens data when present and are byte-identical when the lens is null.
</success_criteria>

<output>
Create `.planning/phases/mbgap-09-cedh-castability-surface/MBGAP-09-03-SUMMARY.md` when done.
</output>
