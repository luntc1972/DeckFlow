---
title: Creator philosophy-profile distillation (style-card + RAG)
trigger_condition: After KB value is validated (see todo validate-kb-value) AND the next Content KB phase is scoped
planted_date: 2026-06-09
---

# Creator philosophy-profile distillation

## Idea
Replace (or layer over) the current per-video "clips + tags" distillation with a per-creator
**philosophy profile** that conditions ChatGPT to advise in that creator's voice — the actual
reason the Content KB exists (creator-as-lens, see note content-kb-creator-as-lens).

## Shape (hybrid, research-backed)
1. **Distilled style-card** per creator — a persona system-prompt of recurring deckbuilding
   principles/heuristics/biases, synthesized across the whole channel.
2. **RAG grounding** — retrieve relevant transcript passages / existing clips at query time so the
   persona's claims are evidence-backed, not free-floating.
3. **Do NOT fine-tune.**

## Must-handle (the hard parts)
- **Principle-level + provenance**: each heuristic carries source video id + date.
- **Contradictions preserved, not averaged**: where the creator conflicts with himself, surface the
  tension ("generally favors X, but argued against it for aggro").
- **Temporal drift**: recency-weight by default; keep older principles dated so an era can be scoped.
- **Refresh on cadence**: incrementally fold new videos into the profile (reuse existing 5-day
  refresh / harvest pipeline), re-checking which principles still hold.
- **Hallucination gate (critical)**: every stated principle must trace to a verified transcript
  passage; reject invented beliefs.

## Reuses
Transcripts (corpus) + clips (RAG/grounding) + the harvest/refresh pipeline already exist. New work
= the profile synthesizer + persona injection into DeckAnalysis prompt + provenance/contradiction model.

## Also reconsider sourcing granularity
Curate at the **video** level, not whole-channel (we already do this manually for trinket-mage's
~690-video "Ranking All Legends" rating series). Consider per-deck targeted pull and/or
user-supplied creators as breadth options beyond a fixed admin default list.
