# Phase 30: Content KB Integration - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-05
**Phase:** 30-Content KB Integration
**Areas discussed:** Clip granularity + curation, Commander-name matching, Relevance scoring inputs, Flag flip + harvest ops

---

## Todo Cross-Reference (pre-discussion)

| Option | Description | Selected |
|--------|-------------|----------|
| Skip — Phase 31 | Keyword-noise match; spike blocks deck-primer-generator, resolves_phase: 31 | ✓ |
| Fold into Phase 30 | Treat spike as Phase 30 work despite Phase 31 tag | |

**User's choice:** Skip — `spike-combo-data-to-primer-grounding.md` stays Phase 31.

---

## Clip granularity + curation

### Q1: is_kept model

| Option | Description | Selected |
|--------|-------------|----------|
| Artifact-level | is_kept = existing IsVisible on site-index row; clips parsed from markdown at build time; no schema change | ✓ |
| New per-clip table | Clip DB entity with per-clip keep/drop toggles; matches KBI wording literally; bigger build | |
| Exclusion list hybrid | Artifact gate + slim kept_clips exclusion table for individual bad clips | |

### Q2: What gets injected / clip pick

| Option | Description | Selected |
|--------|-------------|----------|
| Clips only, doc order | Key Clips bullets in doc order, K=5 across artifacts, best-scoring artifact first; Summary never injected | ✓ |
| Clips + summary fallback | Fall back to ≤150-word Summary when no parseable clips | |
| Best clip per artifact | One clip per artifact to maximize channel diversity | |

### Q3: Panel data source

| Option | Description | Selected |
|--------|-------------|----------|
| Persist in artifact/zip | Injected clip metadata stored in packet session + zip; panel always matches prompt; allowlist + round-trip test | ✓ |
| Recompute live | Re-run match at render time; panel can drift from prompt | |

### Q4: Over-length clips

| Option | Description | Selected |
|--------|-------------|----------|
| Truncate at sentence | Trim to last full sentence under 150 words + ellipsis | ✓ |
| Skip oversized clips | Drop and take next candidate | |

---

## Commander-name matching

### Q1: Matching mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Free-text match | Commander name(s) vs title + summary + clip text at build time; no vocab change, no re-distill | ✓ |
| New commander tag dimension | Add to ContentTagVocabulary + re-distill corpus | |
| Title-only match | Cheapest; misses mid-video mentions | |

### Q2: Role in AND gate

| Option | Description | Selected |
|--------|-------------|----------|
| Counts as a dimension | Commander hit + bracket passes the ≥2-dimension gate; tag-only matches still need bracket + archetype | ✓ |
| Boost only | Gate strictly bracket + archetype; commander only re-ranks | |
| Commander hit bypasses gate | Any hit qualifies regardless of bracket/archetype | |

---

## Relevance scoring inputs

### Q1: Deck-side archetype signal

| Option | Description | Selected |
|--------|-------------|----------|
| Derive from deck data | Category-knowledge distribution + commander hit; no new UI | ✓ |
| User-selected archetype | Optional dropdown (15 vocab values) on DeckAnalysis form | |
| Skip archetype dimension | Commander + bracket + card-category only | |

### Q2: Admin score semantics (KBI-06)

| Option | Description | Selected |
|--------|-------------|----------|
| Test-input preview | Admin enters commander + bracket; scores compute live via production scoring path | ✓ |
| Last-injection log | Log real build scores; empty until traffic, stale after | |
| Static quality score | Deck-independent intrinsic signals | |

**Notes:** Score weights/thresholds explicitly left to planner, calibrated against the mandatory live tag-distribution audit.

---

## Flag flip + harvest ops

### Q1: Flip timing

| Option | Description | Selected |
|--------|-------------|----------|
| Flip early | First plan: harvest → commit → deploy → curate → flip → verify browse; KB live while injection is built; audit gets prod data | ✓ |
| Flip last | Ship dark, flip as final UAT step | |

### Q2: Injection gating

| Option | Description | Selected |
|--------|-------------|----------|
| Same flag | Injection + panel check content.kb.enabled; one switch; OFF = empty-state code path | ✓ |
| Separate injection flag | New content.kb.inject flag | |
| No gate once shipped | Always-on injection; no kill switch | |

### Q3: Harvest scope

| Option | Description | Selected |
|--------|-------------|----------|
| Incremental top-up | Local CLI over existing 5 channels for videos since v1.4 run | ✓ |
| Full re-harvest | Re-run whole corpus | |
| Flip with current corpus | Skip harvest; violates fresh-harvest prerequisite | |

---

## Claude's Discretion

- Score weights, dimension weighting, injection threshold values (calibrated against the live tag-distribution audit)
- Commander-name normalization and partner/background handling
- Expert Context block placement within the three decoupled AI prompt variants
- Panel markup/CSS specifics
- Plan split/sequencing beyond "flip first"

## Deferred Ideas

- Expert panel on other result pages (KBI-F01), injection into other builders (KBI-F02), embedding retrieval (KBI-F03), cron harvest (KBI-F04) — all pre-existing v1.6+ deferrals, reconfirmed
- `spike-combo-data-to-primer-grounding.md` — Phase 31
