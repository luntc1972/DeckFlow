# Phase 97: Profile Fusion + Conflict Ledger - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-12
**Phase:** 97-profile-fusion-conflict-ledger
**Areas discussed:** CI-2 calibration timing, Conflict threshold form, Weighting + fusion resolution, Conflict ledger surface

---

## CI-2 Calibration Timing

First asked as a clean "when do we run the re-distill" choice; user picked **Re-distill now, this session**. On investigation the prerequisite failed (no harvested Snail transcript in any DB, harvest tooling not on PATH, live DB mid-write by another session), so the choice was re-presented honestly with the prototype-grounding option surfaced.

| Option | Description | Selected |
|--------|-------------|----------|
| Ground on prototype now, gate distill in plan-phase | Use the real P89/P90 prototype fusion table to lock threshold form + provisional numbers now; plan-phase adds an isolated harvest+distill pre-step (temp DB) to confirm P96 prompts reproduce the ~27 rules before final numbers lock. | ✓ |
| Do a full isolated harvest+distill now | Add Snail to a temp DB, verify tooling, harvest 1 video, distill with claude provider this session. Truest but heavy/uncertain (network + Whisper/ffmpeg). | |
| Prototype-only, no distill gate | Treat prototype table as sufficient; skip any P96-prompt validation entirely. | |

**User's choice:** Ground on prototype now, gate distill in plan-phase (after the initial "re-distill now" was found infeasible).
**Notes:** Live studio DB has no Snail source; in-repo Snail transcript is a 6-line synthetic stub; git-shipped Snail artifacts are pre-P96 (no stated_rules). Prototype doc holds REAL fusion numbers on 39 decks + 27 rules → sufficient to ground design; a temp-DB distill validates the shipped prompts at plan time.

---

## Conflict Threshold Form

| Option | Description | Selected |
|--------|-------------|----------|
| Band-relative % beyond edge | Conflict when measured is outside [min,max] by >X% of band width/edge. Scale-free across metric magnitudes. | ✓ |
| Absolute delta beyond edge | Conflict when measured is >N units outside the band; needs a per-metric N table. | |
| Significance-aware (sample-weighted) | Fold measured reliability into the threshold itself. | |

**User's choice:** Band-relative % beyond edge.

### Follow-up: low-coverage guard

| Option | Description | Selected |
|--------|-------------|----------|
| Coverage floor gates conflict | Conflict fires only if measured meets a min effective-sample/coverage floor (reuse P95 EffectiveSampleSize); below floor → 'insufficient-measured' row, not a conflict. | ✓ |
| Single global % only | One band-relative % uniformly; no coverage gate. | |
| Per-metric threshold table | Different % per metric family. | |

**User's choice:** Coverage floor gates conflict.
**Notes:** Prototype flags the measured leg as sparse (draw 28%, wipes 3% labeled) — the floor prevents false hypocrisy verdicts.

---

## Weighting + Fusion Resolution

| Option | Description | Selected |
|--------|-------------|----------|
| Hard partition by metric key | Static observable/philosophy classification; observables → measured target (stated band as guard), philosophy/stated-only → stated, never conflict. Deterministic. | ✓ |
| Confidence-scaled blend | Blend stated+measured by confidence × coverage into a synthetic number. | |
| Measured-always for observables | Measured is always the target for any measured metric. | |

**User's choice:** Hard partition by metric key.

### Follow-up: supersession (D-04 deferred from P96)

| Option | Description | Selected |
|--------|-------------|----------|
| Recency-collapse before fusion | Same (metric, condition) → keep newest by video_date, superseded kept as ledger history. Retires D-04. | ✓ |
| Keep all, annotate recency | Carry all rules; ledger shows the timeline; newest is active. | |
| Defer supersession again | Assume one rule per (metric, condition); punt to a later phase. | |

**User's choice:** Recency-collapse before fusion.

---

## Conflict Ledger Surface

| Option | Description | Selected |
|--------|-------------|----------|
| Studio Blazor page | New read-only Studio page (neighbor to CreatorSources/Harvest/Publish), loopback-only. | ✓ |
| Web /Admin page | Add to the deployed BasicAuth /Admin area. | |
| Core read-model only, no page this phase | Ship Core read-model + CLI dump; defer the page to P99/P100. | |

**User's choice:** Studio Blazor page.

### Follow-up: row contract

| Option | Description | Selected |
|--------|-------------|----------|
| Full say-vs-do row | Per (metric, condition): stated band, measured+numDecks/coverage, resolved target, verdict badge (agree/conflict/insufficient-measured/philosophy-stated-only), source-clip link + video_date. | ✓ |
| Conflicts-only compact | Show only rows where a conflict fired. | |
| Grouped by verdict | Same full data, sectioned into buckets. | |

**User's choice:** Full say-vs-do row.
**Notes:** Full row keeps the prototype's case-(ii) "right by their own philosophy" story visible (board wipes) — a conflicts-only view would hide it.

---

## Claude's Discretion

- Exact numeric X for the band-relative % and the coverage-floor value — empirical, locked at plan/executor time against prototype data + the confirmation distill.
- Precise additive field names/shape on `FusedTarget` (additive-only, round-trip-preserving).
- Studio page layout beyond the full-row contract.

## Deferred Ideas

- Mass corpus backfill (~106 artifacts) — operator-driven, deferred per P96 D-05.
- Syncing fused profiles to the deployed app — no consumer until P99/P100.
- Card-level grounding of stated rules — Phase 98's guard.
