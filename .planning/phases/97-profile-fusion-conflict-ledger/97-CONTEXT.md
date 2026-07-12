# Phase 97: Profile Fusion + Conflict Ledger - Context (PRE-PLANNING STUB)

**Status:** NOT yet gathered via /gsd-discuss-phase. This stub exists to carry two
pre-planning findings surfaced during the Phase 96 plan review (2026-07-12) so the
planner does not rediscover them. Run /gsd-discuss-phase 97 to complete this file;
do NOT treat this stub as a finished CONTEXT.

Requirements: CS-16, CS-16a, CS-17, CS-18, CS-19, CS-20.

---

## Carry-in Findings (from Phase 96 plan review — MUST address at plan time)

### CI-1 (BLOCKER for fusion correctness): the P94 rule/target records are too narrow for what fusion needs

**Problem.** Phase 94 locked these shapes *before* the Phase 96 discussion surfaced
bands, conditionality, and provenance:

- `StatedRule{category, targetMetric, targetValue, comparator, sourceClip, confidence}`
  — single `double targetValue`, NO band (min/max), NO `condition`/`applies_when`, NO `video_date`.
- `FusedTarget{metric, value, weight, source, conflict?}` — a SINGLE `value`.

Phase 96 could not use the P94 `StatedRule` and deliberately forked its own
band-capable `StatedRuleCandidate` (see `96-01-PLAN.md`: "the P94 double-only record
cannot hold a band"), persisting to the new `content_stated_rules` table + the
`stated_rules:` artifact block instead of the P94 store. Net effect entering P97:

1. **Stated input for fusion comes from the Phase 96 artifact/DB contract, NOT the P94
   `ICreatorStyleProfileStore` stated slot.** The single source P97 parses is the
   `stated_rules:` frontmatter block (snake_case keys locked in `96-05-PLAN.md`
   HIGH-3: `category, metric, value, value_min, value_max, comparator, condition,
   clip_ts, source_clip, confidence, card_reference, card_grounded, video_date`) and/or
   the `content_stated_rules` rows. The P94 stated-rules profile slot is currently lossy
   dead weight.
2. **`FusedTarget` cannot satisfy CS-16 as written.** CS-16 requires "both the stated
   AND measured numbers are retained on the resulting FusedTarget" and conflict computed
   when "the measured value falls outside the stated **band** by a defined threshold." A
   single `value` field holds neither a band nor both numbers.

**Required decision at plan time (do NOT defer past planning):**
- **Extend `FusedTarget` additively** to retain: the stated band (`statedMin`/`statedMax`
  or a stated-rule reference), the measured value + `numDecks`/distribution, the resolved
  target, `weight`, `source`, and a populated `conflict` payload (stated-vs-measured
  numbers + which won). Additive-only — do not break P94 round-trip tests already green.
- **Decide the stated ingestion path explicitly:** parse the P96 `stated_rules:` artifact
  contract (recommended — it is the single locked contract, byte-checked in 96-05) vs. the
  `content_stated_rules` table vs. the (lossy) P94 store slot. Pick ONE; document it.
- **Carry `condition`/`applies_when` end-to-end (CS-16a).** P96 captured `condition`
  (`archetype:control`, `curve:low`, `bracket:2`) on every rule specifically so P97 can
  avoid false conflicts between a conditional rule and an unconditional aggregate. The
  fusion join MUST key on `(metric, condition)`, never `metric` alone — CS-16a is called
  out in the ROADMAP as "the highest-risk modeling decision this cycle."
- **Metric join key.** Stated `metric` vocabulary was aligned to the Phase 95
  `MeasuredMetric` keys ON PURPOSE (`96-01` D-02a: category tokens, `karsten:*`,
  `combo_density:included_per_deck`, plus stated-only keys `land_count`/`interaction`/
  `opener_probability`/`pip_distribution`/`power_level_philosophy`). Join stated↔measured
  on these exact keys. Stated-only keys (no P95 counterpart) route to the CS-17
  "weight-toward-stated for unmeasurable philosophy" branch, never produce a conflict.
- **`confidence` is an uncalibrated LLM scalar** — treat as coarse bands (low/med/high),
  not a precise multiplicative weight.

### CI-2 (verify the data before locking thresholds): Phase 96's gate never ran the real prompts

**Problem.** The Phase 96 phase-gate golden test (`96-07`, D-06) runs the full pipeline
with **canned CLI responses** through the real UTF-8 harness. It proves plumbing +
harness + validation — it does NOT prove the real Select/Disambiguate/Decompose prompts
actually extract sane rules from a real transcript. Phase 96 also runs **no mass backfill**
(D-05); `stated_rules` exist on NO persisted artifact until an operator runs a re-distill
(D-05-DEP). So entering P97, there is **zero real stated-rules data** and **zero evidence
the prompts extract well**.

**Required action BEFORE P97 planning locks any fusion/conflict threshold:**
- **Run ONE real Salubrious Snail video through the live subscription-CLI distill**
  (the D-05 re-distill mechanism shipped in Phase 96 — `ContentKbOrchestrator`). ~7 CLI
  calls, $0 on the subscription plan. This is the D-05-DEP Snail re-distill the ROADMAP
  already says P97 must trigger.
- **Inspect the emitted `stated_rules:` block** against the ~27 prototype rules in
  `docs/research/p89-p90-prototype-snail.md` (37-42 lands, 8-14 removal, ≥8 counters, 3-5
  wipes). If the real prompts under-extract or hallucinate, fix the P96 prompts BEFORE
  P97 builds fusion around the shape of the data. Do not plan fusion thresholds against
  imaginary data.
- Only after a real Snail profile exists (measured from P95 + stated from this re-distill)
  should P97's conflict-threshold and weighting decisions be locked — they are empirical
  calibration decisions, not pure-design decisions.

---

## Dependencies (confirmed)
- **Phase 95 (measured input):** `MeasuredStyleProfileBuilder` → `MeasuredMetric[]` keys
  are the join target. COMPLETE + verified.
- **Phase 96 (stated input):** `stated_rules:` artifact contract + `content_stated_rules`
  table. Substrate COMPLETE once Phase 96 executes; real DATA needs the CI-2 re-distill.

*Phase: 97-profile-fusion-conflict-ledger*
*Stub created: 2026-07-12 (Phase 96 plan-review carry-in). Complete via /gsd-discuss-phase 97.*
