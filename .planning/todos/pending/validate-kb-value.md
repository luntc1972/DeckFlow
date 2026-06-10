---
title: Validate Content KB value — A/B ChatGPT output with vs without expert context
date: 2026-06-09
priority: high
---

# Validate Content KB value (gating experiment)

## Why
`content.kb.enabled` is OFF in prod and the Content KB subsystem is unproven. Before investing in
the creator philosophy-profile redesign (seed: creator-philosophy-profile), prove the KB actually
makes ChatGPT's deck analysis **better**. If the lift is marginal, the curated-channel +
harvest/distill machinery isn't worth the maintenance.

## Experiment
- Pick a handful of representative decks (mix of commanders/archetypes).
- For each, generate the analysis prompt **twice**: once with expert-context clips injected, once
  without (baseline).
- Run both through ChatGPT; compare answer quality (signal beyond ChatGPT's own MTG knowledge,
  actionable specificity, creator-voice presence).
- Judge blind if feasible (don't label which is which).

## Decision criteria
- Clear lift → green-light the philosophy-profile build (style-card + RAG) and flipping
  `content.kb.enabled` ON.
- Marginal/no lift → reconsider the whole KB; possibly retire whole-channel pre-distill in favor of
  per-deck targeted retrieval or user-supplied sources.

## Spike-able
Run via /gsd-spike when ready. Lightweight, no production code required to start.
