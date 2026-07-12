# Phase 96: Stated-Rules Distiller - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-12
**Phase:** 96-stated-rules-distiller
**Areas discussed:** Claimify pipeline shape, Stated-rule schema/vocabulary, content_type + backfill, Card grounding, Backfill scope, Recency

---

## Claimify pipeline shape

| Option | Description | Selected |
|--------|-------------|----------|
| One dedicated LLM stage | Single new stated-rules call, prompt does Select→Disambiguate→Decompose in one pass. Cheapest. | |
| Multi-pass (3 sub-calls) | Literal Claimify: separate Select, Disambiguate, Decompose calls per chunk + reduce/dedupe. Higher fidelity, ~3x cost. | ✓ |
| Two-pass hybrid | Map: extract+disambiguate per chunk; Reduce: decompose+dedupe. Middle ground. | |

**User's choice:** Multi-pass (3 sub-calls)
**Notes:** Fidelity/falsifiability preferred over token cost. Reconciled with the no-backfill decision below so the 3x cost lands on new distills + the golden test, not a 106-artifact sweep.

---

## Stated-rule schema / metric vocabulary

| Option | Description | Selected |
|--------|-------------|----------|
| Shared metric enum + band | New metric vocab mirroring MeasuredMetric names; band value for ranges. | |
| Free-ish category + single value | ContentTagVocabulary category + single value/comparator; ranges = two rules. | |
| Defer vocab to planner | Lock shape {category,metric,value|band,comparator,condition,clip_ts}; metric allowlist derived at plan time from P95 metric names + Snail 27 rules. | ✓ |

**User's choice:** Defer vocab to planner
**Notes:** Shape locked (incl. band + condition for the P97 applies_when seed). Planner derives the metric allowlist from Phase 95 MeasuredMetric keys UNION the ~27 Snail prototype rules; aligning stated metric names to MeasuredMetric names is mandatory for P97 join.

---

## content_type + backfill mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| New LLM classify call + full re-distill | content_type own constrained call; retrofit all artifacts via full re-distill. | |
| Reuse existing classify + heuristic | Derive content_type from existing tags + classifier verdict + clip density; append-only backfill, no re-LLM. | ✓ |
| New call, incremental backfill | New LLM call, retrofit only re-distilled artifacts. | |

**User's choice:** Reuse existing classify + heuristic
**Notes:** No new LLM call for content_type. Heuristic over existing signals.

---

## Card grounding (unrecognized card names)

| Option | Description | Selected |
|--------|-------------|----------|
| Flag, keep rule | Mark unrecognized, keep rule; P98 does hard reject. | |
| Reject rule | Drop any rule with an unresolved card name. | |
| Fuzzy-correct then flag | Scryfall fuzzy auto-correct first; rewrite on match, keep+flag if still unresolved. | ✓ |

**User's choice:** Fuzzy-correct then flag
**Notes:** Best recall; protects real rules against auto-caption typos (e.g. "Dockside Extortonist" → "Dockside Extortionist"). Hard reject deferred to Phase 98 guard. Reuses cached Scryfall lookup + ScryfallThrottle.

---

## Backfill scope over existing ~106 artifacts

| Option | Description | Selected |
|--------|-------------|----------|
| Snail only now | Re-distill ~39 Snail artifacts; others deferred. Unblocks P97. | |
| Full corpus now | Re-distill all ~106; complete coverage, biggest LLM bill. | |
| Pipeline only, no backfill | New distills emit stated_rules; zero backfill this phase. | ✓ |

**User's choice:** Pipeline only, no backfill
**Notes:** Claude flagged this risks starving P97 fusion of stated input. Reconciled: the re-distill MECHANISM ships (satisfies CS-11a/b "via one re-distill pass"), but executing the sweep is operator-driven and deferred — matching prior-cycle re-distill handling. Recorded as D-05-DEP: P97 must run a Snail re-distill before fusion has real stated data. Golden test still exercises the pipeline on a real Snail transcript.

---

## Recency / provenance

| Option | Description | Selected |
|--------|-------------|----------|
| Carry date, no superseding here | Stamp each rule with video publish date; superseding is P97's job. | ✓ |
| Also compute supersedes now | Resolve newer-wins at distill time. | |

**User's choice:** Carry date, no superseding here
**Notes:** "Also compute supersedes now" flagged as scope creep against the substrate-only boundary and rejected. This phase only records video_date + clip_ts.

---

## Claude's Discretion

- Chunk size / map-reduce chunk boundaries.
- Reduce-pass dedupe key/threshold (likely metric+condition).
- content_type heuristic thresholds.
- confidence scale/encoding.
- card_grounded flag representation.

## Deferred Ideas

- Mass re-distill backfill of the ~106 existing artifacts (mechanism ships; execution operator-driven, deferred).
- Superseding / newer-wins conflict resolution — Phase 97 (fusion).
- Multi-creator stated-rule onboarding beyond Snail — manual/deferred.
