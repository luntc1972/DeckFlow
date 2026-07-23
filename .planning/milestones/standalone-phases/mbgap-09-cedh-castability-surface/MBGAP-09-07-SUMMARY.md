---
phase: mbgap-09-cedh-castability-surface
plan: 07
status: complete
executor: codex gpt-5.4 (cross-AI), reviewed + committed by Claude
commits:
  - c34a3bc8 docs(MBGAP-09-07): document cEDH early-interaction lens in manabase help
  - 6e0ae3d2 docs(MBGAP-09-07): README behavior entry for cedh-interaction-lens
key-files:
  created: []
  modified:
    - DeckFlow.Web/Help/manabase.md
    - README.md
---

# MBGAP-09-07 Summary — Mandatory documentation

## What was built
- **Help/manabase.md** "cEDH: Early interaction" subsection following the five-part flagged-feature shape: mechanism paragraph (holdable = untapped colored access on at least one of turns 1-3); flag framing (`analysis.manabase.cedh-interaction-lens`, seeded ON, cEDH-only); bullets for the qualifying definition (PlanRole.Interaction + effective MV ≤ 2 after cost overrides), the "N / M interaction held up by turn 3" headline at the 88% threshold, worst-5 + view-all disclosure, and the empty-state caution; verbatim caveat "assumes you hold mana open" (D-07); scope disclaimer — informational only, never changes land count/color counts/castability math/sort/percentages/health verdict (D-13), while noting the castability table is newly VISIBLE in cEDH mode with the holdable badge (D-09/D-12) — plus cross-refs to both Step-3 formula panels, whose descriptions now mention the cEDH interaction metric.
- **README.md** behavior entry among the analysis.manabase.* bullets, ships-ON framing: flag name, default ON, cEDH-only, adds lens header + full cEDH castability table + both prompt-artifact blocks; no land/color/verdict change; flag-off byte-identical (kill switch).

## Verification
- LEAD accuracy review against locked facts from Plans 01-04 (M12 no under/over-claim): all claims match shipped behavior.
- grep evidence: flag key, "effective MV <= 2", "held up by turn 3", verbatim caveat all present.
- Docs-only; no builds required. EOL: no churn (LF preserved).

## Deviations
None.

## Self-Check: PASSED
