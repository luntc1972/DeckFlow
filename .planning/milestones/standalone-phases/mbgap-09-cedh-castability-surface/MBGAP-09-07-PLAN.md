---
phase: mbgap-09-cedh-castability-surface
plan: 07
type: execute
wave: 4
depends_on: [04]
files_modified:
  - DeckFlow.Web/Help/manabase.md
  - README.md
autonomous: true
requirements: [D-01, D-02, D-07, D-08, D-13, D-15]
must_haves:
  truths:
    - "Help/manabase.md documents the lens, its qualifying-spell definition, the 88 threshold, and the raw-availability caveat verbatim (mandatory closing task, M12)"
    - "README carries a behavior-change entry naming the flag, its ON default, cEDH-only scope, and byte-identical-off property (mandatory closing task, D-15)"
  artifacts:
    - path: "DeckFlow.Web/Help/manabase.md"
      provides: "cEDH: Early interaction help subsection"
      contains: "assumes you hold mana open"
    - path: "README.md"
      provides: "changelog entry for analysis.manabase.cedh-interaction-lens"
      contains: "analysis.manabase.cedh-interaction-lens"
  key_links:
    - from: "Help/manabase.md subsection"
      to: "Step 3 formula panels"
      via: "cross-reference to This deck's numbers panel"
      pattern: "This deck's numbers"
---

<objective>
Ship the mandatory documentation for the interaction lens (M12 precedent: help-doc must not under- or over-claim). Add a "cEDH: Early interaction" subsection to `Help/manabase.md` and a behavior-change entry to `README.md`.

Purpose: The lens is not "done" without docs (CONTEXT Mandatory Closing Tasks). Doc-overclaim is a tracked findings class (M12).
Output: Help subsection + README entry.
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
Facts to document (locked by CONTEXT + Plans 01-04):
- Flag key: analysis.manabase.cedh-interaction-lens, seeded ON, cEDH-only.
- Qualifying spells: PlanRole.Interaction with effective MV <= 2 after cost overrides (D-01/D-02).
- Headline: "N / M interaction held up by turn 3" at the CedhSupportThreshold = 88 (D-08).
- Caveat (verbatim, D-07): "assumes you hold mana open".
- Informational v1: does NOT change land count, color counts, castability math/sort/percentages, or health verdict (D-13). It DOES newly expose the full castability table in cEDH mode and adds the holdable badge on interaction rows (D-09/D-12) — do not claim the table is untouched.

Help doc analogs (Help/manabase.md): "Untapped-source (Tap) analyzer" (83-93) and "Opening hand
and plan presence" (95-111) each follow: mechanism paragraph -> "By default (the analysis.manabase.X
flag, on/off) ... adds" -> bulleted numbers -> scope disclaimer sentence -> cross-ref to formula
panels. Step 3 description at lines 123-129.

README analogs: ritual-burst-mana entry (~801, cEDH-only sim metric), plan-presence (~823),
"all manabase display/verdict reads now default ON" (~802, the ships-ON framing to mirror for D-15).
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add the "cEDH: Early interaction" subsection to Help/manabase.md</name>
  <read_first>
    - DeckFlow.Web/Help/manabase.md (Tap analyzer subsection 83-93; plan-presence 95-111; Step 3 formula-panel description 123-129)
  </read_first>
  <action>
    Add a new subsection (title referencing cEDH early interaction) placed near the other flagged-feature subsections, following the exact five-part shape: (1) mechanism paragraph — the sim measures, per cheap interaction spell, the chance it is holdable (untapped colored access sufficient to cast) on at least one of turns 1-3; (2) flag-state framing naming analysis.manabase.cedh-interaction-lens, seeded ON, cEDH-only; (3) bulleted numbers — the qualifying-spell definition (PlanRole.Interaction, effective MV <= 2 after cost overrides), the "N / M interaction held up by turn 3" headline at the 88% threshold (reuse the threshold wording style from the Health-verdict bullet), the worst-5 + view-all disclosure, and the empty-state caution; (4) the raw-availability caveat verbatim "assumes you hold mana open"; (5) scope disclaimer — informational only, never changes land count, color counts, the castability math/sort/percentages, or the health verdict (D-13), while noting the table itself is newly VISIBLE in cEDH mode with a holdable badge on interaction rows (D-09/D-12) — plus a cross-reference to Step 3's two formula panels. Also add a one-line mention in the Step 3 description (123-129) that the two panels cover the interaction metric in cEDH mode.
  </action>
  <verify>
    <automated>grep -n "cedh-interaction-lens\|assumes you hold mana open\|held up by turn 3\|effective MV" DeckFlow.Web/Help/manabase.md</automated>
  </verify>
  <acceptance_criteria>
    - Subsection names the flag, the qualifying definition (PlanRole.Interaction + effective MV <= 2), the 88 threshold, the worst-5 disclosure, and the empty-state caution.
    - The verbatim caveat "assumes you hold mana open" appears.
    - Scope disclaimer states no change to land/color/castability/verdict; cross-references the formula panels.
  </acceptance_criteria>
  <done>Help documents the lens accurately without over-claiming (M12).</done>
</task>

<task type="auto">
  <name>Task 2: Add the README behavior-change entry</name>
  <read_first>
    - README.md (ritual-burst-mana entry ~801; plan-presence ~823; ships-ON entry ~802)
  </read_first>
  <action>
    Add a changelog/behavior entry mirroring the ritual-burst-mana entry structure but with the ships-ON framing: name the flag analysis.manabase.cedh-interaction-lens, state it is seeded/ships ON by default, cEDH-only, adds the "Early interaction" header lens + the full per-card castability table in cEDH mode + the two prompt-artifact blocks; state explicitly that it does NOT change land count, color counts, or the health verdict (informational v1, D-13); and that flag-off output is byte-identical (kill switch). Place it with the other analysis.manabase.* entries.
  </action>
  <verify>
    <automated>grep -n "analysis.manabase.cedh-interaction-lens" README.md</automated>
  </verify>
  <acceptance_criteria>
    - Entry names the flag + ON default + cEDH-only scope.
    - States no land/color/verdict change and byte-identical-off.
    - Placed among the existing analysis.manabase.* changelog bullets.
  </acceptance_criteria>
  <done>README reflects the shipped behavior change per project rule.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| none | Documentation-only; no code, input, or runtime surface |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-MBGAP09-11 | Repudiation | Help doc over-claiming the metric's certainty | mitigate | Verbatim raw-availability caveat + informational-only disclaimer required (M12 findings class) |
</threat_model>

<verification>
- grep confirms the flag key, caveat, and threshold wording present in both docs.
- No code files touched (docs-only plan).
</verification>

<success_criteria>
Help and README accurately document the lens, its threshold, caveat, scope, and flag default without over-claiming.
</success_criteria>

<output>
Create `.planning/phases/mbgap-09-cedh-castability-surface/MBGAP-09-07-SUMMARY.md` when done.
</output>
