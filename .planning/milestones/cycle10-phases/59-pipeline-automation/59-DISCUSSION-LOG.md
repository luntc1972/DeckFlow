# Phase 59: Pipeline Automation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-20
**Phase:** 59-pipeline-automation
**Areas discussed:** Quality signal, Threshold config, Spend gate + auto-distill, Scope + failure UX

---

## Quality signal (AUTO-02 confidence)

| Option | Description | Selected |
|--------|-------------|----------|
| Composite heuristic | Score from clip-count + tag coverage + summary completeness (0-1) | |
| Clip count only | Distills with ≥ N well-timestamped clips auto-approve | ✓ |
| Ask model for a score | Add a confidence field to the distill schema (model self-rates) | |

**User's choice:** "Do clip count until A/b is worked on" — clip count now; composite deferred to the future KBVAL A/B harness.
**Notes:** No confidence signal exists in distill output today; must be derived. No provider/model/schema change. Keep swappable for a later composite.

### Follow-up: clip cutoff

| Option | Description | Selected |
|--------|-------------|----------|
| ≥ 5 clips | Upper-middle of 3-8 range; 3-4-clip distills stay in review | ✓ |
| ≥ 6 clips | Stricter, more land in review | |
| ≥ 4 clips | Looser, only thinnest reviewed | |

**User's choice:** ≥ 5 clips (operator-adjustable).

---

## Threshold config (AUTO-02 adjustability)

| Option | Description | Selected |
|--------|-------------|----------|
| Harvest page controls | On/off + clip-cutoff panel on the Harvest page, point-of-use | ✓ |
| Env var only | Read at startup; no UI; restart to change | |
| New Studio settings page | Dedicated /settings page | |

**User's choice:** Harvest page controls.

### Follow-up: default state + persistence

| Question | Selected |
|----------|----------|
| Default state: ON vs OFF | **ON by default** |
| Persistence: persist across restarts vs session-only | **Persist across restarts** |

---

## Spend gate + auto-distill (AUTO-01 / SC4)

| Option | Description | Selected |
|--------|-------------|----------|
| Auto $0, confirm metered | Inline auto-distill only for subscription ($0); metered keeps dry-run→confirm gate | ✓ |
| Pre-harvest cap-check | Metered auto-distills but aborts if projected > remaining cap | |
| Always require confirm | Auto-distill never bypasses confirm for any provider | |

**User's choice:** Auto $0, confirm metered.
**Notes:** Honors SC4 (no silent spend). Operator's live backend is subscription claude-CLI, so inline is the common path; metered confirm is the guard rail. Auto-approve still applies to metered distills after completion.

---

## Scope + failure UX

| Option | Description | Selected |
|--------|-------------|----------|
| Per-video outcome summary | One result card: harvested/distilled/auto-approved/in-review/dropped/failed (with ids) | ✓ |
| Just counts, no per-video | Aggregate counts only | |
| Stop on first failure | Abort batch on any distill error | |

**User's choice:** Per-video outcome summary (continue-on-failure). Reuse existing `DistillResult` fields.

### Follow-up: keep manual Distill section?

| Option | Description | Selected |
|--------|-------------|----------|
| Keep manual as fallback | One-click default; manual stays for re-distill, metered confirm, restart loader | ✓ |
| Replace with auto only | Remove manual distill UI | |

**User's choice:** Keep manual as fallback.

---

## Claude's Discretion

- Local persistence mechanism for the auto-approve settings (lightest option per existing Studio config/DB conventions).
- Single vs batch transactional `SetApprovalStatusAsync` wiring.
- Whether the clip-count signal is computed in Core orchestrator vs Studio — keep it swappable for a future composite.

## Deferred Ideas

- Composite quality signal (clip-count + tag coverage + summary completeness, or model confidence) — revisit with KBVAL A/B harness (v2).
- Model-returned confidence field in the distill schema — rejected this phase.
- New Studio /settings page — not warranted for two settings; revisit if more global knobs accrue (Phase 62 SUI).
- Scheduled/cron harvest (AUTO-03) and bulk creator onboarding (AUTO-04) — explicitly v2.
