# Phase MBGAP-09: cEDH Castability Surface - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-13
**Phase:** MBGAP-09-cedh-castability-surface
**Areas discussed:** Cheap-interaction definition, Metric & math engine, cEDH table & placement, Verdict/prompt + rollout

---

## Cheap-interaction definition

| Option | Description | Selected |
|--------|-------------|----------|
| PlanRole.Interaction, MV≤2 | Reuse classifier's Interaction tag, effective-2 cut (Swan Song / Pyroblast / Silence class) | ✓ |
| PlanRole.Interaction, MV≤3 | Wider net incl. 3-drops | |
| All spells MV≤2 | Role-agnostic cheap-spell cut | |

**Free/alt-cost entry:** Effective cost after overrides (`1*` machinery) ✓ (vs printed MV only / printed + hard-coded free-if-commander cycle)
**Empty state:** Caution warning "no cheap interaction found" ✓ (vs hide silently / neutral note)
**Auditability:** Per-spell rows in lens ✓ (vs aggregate + expandable / aggregate only)

---

## Metric & math engine

| Option | Description | Selected |
|--------|-------------|----------|
| Sim-based per-trial | Reuse CastabilitySimulator untapped tracking; correlation for free | ✓ |
| Analytic Karsten untapped-only | Cheaper, re-introduces ~30-pt independence error | |
| Both | Sim headline + analytic cross-check (M9 two-numbers risk) | |

**Measured quantity:** By-turn-3 holdable — P(castable untapped on ≥1 of turns 1–3) ✓ (vs on-curve only / T1-T3 breakdown)
**Own-curve competition:** Raw availability v1 with caption caveat ✓ (vs after-development residual / you-decide)
**Headline aggregate:** N/M spells on target @ CedhSupportThreshold 88 ✓ (vs worst-spell headline / mean %)

---

## cEDH table & placement

| Option | Description | Selected |
|--------|-------------|----------|
| Full table + lens | Kill v1 note; cEDH gets Casual's table PLUS interaction lens | ✓ |
| Interaction lens only | Table stays Casual-only | |
| Table with interaction pinned | Single table, no lens section | |

**Lens placement:** Third lens in header strip (cEDH-only "Early interaction") ✓ (vs own section below verdict / inside castability panel)
**Row cap:** Worst 5 + native `<details>` "view all" expander ✓ (vs all rows / worst 3 + count)
**Table columns:** Identical to Casual + holdable %/badge on interaction rows ✓ (vs no extras / cEDH-reordered)

---

## Verdict/prompt + rollout

| Option | Description | Selected |
|--------|-------------|----------|
| Informational v1 | Verdict/health untouched this phase | ✓ |
| Corroboration-only input | Strengthen-only, broad-color-access pattern | |
| Full verdict input | Can flip band; needs calibration | |

**Prompt artifacts:** Both — report text block + swap-prompt upgrade with real N/M + worst spells ✓ (vs report-only / UI-only)
**Flag:** New cEDH-only flag, seeded ON (`analysis.manabase.cedh-interaction-lens` suggested), flag-off byte-identical ✓ (vs seed OFF + operator flip / flagless)

---

## Claude's Discretion

- Lens copy/naming; met/short glyph reuse.
- 3-up lens strip CSS, mobile stacking, theme tokens (`--panel` in dark themes).
- MV0-after-override rows presentation (trivially 100% color-holdable).
- Sim bookkeeping shape (ride existing trials vs dedicated counter struct).
- Final flag spelling; verify whether manabase artifacts have a prompt-cache replay set
  needing membership (PromptMutatingAnalysisFlags analogue).

## Deferred Ideas

- Verdict/health integration of the lens (later, calibrated).
- After-development residual hold-up modeling.
- Per-turn T1/T2/T3 breakdown view.
- Casual-mode exposure of the interaction lens.
