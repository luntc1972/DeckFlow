# Phase 95: Measured-Style Extractor - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-11
**Phase:** 95-measured-style-extractor
**Areas discussed:** Staple-strip definition, Lift metric population, Folder weighting, Per-metric sample confidence

---

## Staple-strip definition (CS-05)

| Option | Description | Selected |
|--------|-------------|----------|
| Freq threshold from crawled history | Strip cards in >X% of creator's own decks | |
| Curated ContentTagVocabulary list | Fixed staple set | |
| EDHREC-rank cutoff | Strip below rank threshold | |
| Hybrid: vocab list + freq | Curated staples ∪ over-frequency cards | ✓ |

**User's choice:** Hybrid (vocab list ∪ frequency cut).
**Follow-up — frequency cutoff:** **>60%** (over >80% too lax / >50% over-strips a 39-deck sample).
**Notes:** Staple-strip runs BEFORE any ratio. Belt-and-suspenders: always-strip curated vocab staples plus per-creator over-frequency cards.

---

## Lift metric population (CS-07)

| Option | Description | Selected |
|--------|-------------|----------|
| Creator's own crawled decks | Both terms from ~39 decks | |
| Global CategoryKnowledgeRepository | Both terms from 322k-obs history | |
| Creator numerator, global baseline | Pr(A∩B) creator, Pr(A)·Pr(B) global | ✓ |

**User's choice:** Creator numerator / global baseline.
**Notes:** Most discriminating — creator's actual pairings vs meta expectation; demotes staples.

---

## Folder weighting (CS-04d)

| Option | Description | Selected |
|--------|-------------|----------|
| Graded weights | Current/Secondary 1.0, Budget/In-consideration 0.25–0.5, Other 0.5 | ✓ |
| Hard include/exclude | Only Current+Secondary count | |
| Flat (no weighting) | parentFolder for provenance only | |

**User's choice:** Graded weights — **manually curated per creator**.
**Follow-up — unknown folder default:** User noted almost no creators use Snail's folder taxonomy, so weights must be manually curated per creator (not auto-derived). Uncurated creators default to 1.0 (nothing silently dropped) with a "weights uncurated" flag.
**Notes:** Weight map stored alongside the creator-profile-source mapping.

---

## Per-metric sample confidence (CS-10)

| Option | Description | Selected |
|--------|-------------|----------|
| numDecks only (raw) | Consumers decide trust | |
| numDecks + per-metric floor flag | Below-floor bool per metric | |
| Weighted-effective count | numDecks as folder-weighted effective sample | ✓ |

**User's choice:** Weighted-effective count.
**Follow-up — P94 int lock reconciliation:** MeasuredMetric.NumDecks is a locked `int` top-level field. Chose: **NumDecks stays raw int (crawled-deck count); fractional weighted-effective count lives in MeasuredMetric's nested extensible object** — no P94 top-level lock break.
**Notes:** Planner decides nested placement (extend MetricDistribution vs nested confidence record).

---

## Claude's Discretion

- Near-precon dedup similarity threshold (CS-04c).
- Concrete shape of the narrow Core-vs-Web extraction contract (D-11).
- Per-deck confidence-marker representation.

## Deferred Ideas

None — discussion stayed within phase scope. Moxfield crawl stays out of MVP; multi-creator auto-discovery remains explicitly manual this cycle.
