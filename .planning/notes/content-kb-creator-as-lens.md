---
title: Content KB sourcing — creator-as-lens reframe
date: 2026-06-09
context: Explore session questioning whether "default creators" is the right model for sourcing YouTube content into the Content KB
---

# Content KB sourcing — creator-as-lens reframe

## The reframe
The Content KB's value is **not** facts ChatGPT lacks — ChatGPT already knows MTG broadly.
Its value is a specific creator's **deckbuilding philosophy / voice / style**: "run my deck
through Salubrious Snail's worldview." It is a *lens*, not a fact database.

This makes channel-based sourcing fundamentally **right** (you want a creator's whole
worldview), but exposes the current *representation* as the wrong shape.

## Why "clips + tags" is the wrong shape
Today each video distills into timestamped **clips + archetype tags** — a per-video,
fact-shaped representation. But a creator's philosophy lives in the **recurring principles
across the whole catalog** ("protect your win-cons," "10+ interaction"), not in any single
clip. Clip-injection makes ChatGPT *infer* the style from quotes instead of *applying* it.

## Two hard problems the user named
1. **Internal inconsistency** — a creator's videos contradict each other; philosophy isn't monolithic.
2. **Drift over time** — advice evolves; a static profile blends stale + current takes.

## Target representation (validated by research)
- **Hybrid**: a distilled **style-card** (persona system-prompt of recurring principles) **+ RAG
  over transcripts** for grounding. **Do not fine-tune** (overkill; bakes in stale opinions).
- **Principle-level extraction with provenance** — each heuristic tagged with source video + date.
- **Date & version contradictions; recency-weight** but **surface** conflicts rather than averaging
  ("early X favored A; by 2025 X shifted to B").
- **Biggest risk = hallucinated principles** → gate every stated heuristic to a verified transcript
  passage; never let the model invent a belief the creator never voiced.

## Reuse, not rebuild
Existing assets map cleanly: transcripts = corpus; clips = the RAG/grounding layer; the new piece
is the per-creator distilled philosophy profile + persona injection into the analysis prompt.

## Open strategic gate
`content.kb.enabled` is still OFF in prod and the subsystem is unproven. Validate the KB actually
improves ChatGPT output (A/B with vs without expert context) **before** building the philosophy-profile
layer. See todo: validate-kb-value. Follow-on design: seed creator-philosophy-profile.

Research sources: distilled style-card + RAG hybrid (favored over fine-tune); temporal-conflict QA
(arXiv 2506.07270); claim-evidence provenance (FRONT 2408.04568, PaperTrail); persona drift.
