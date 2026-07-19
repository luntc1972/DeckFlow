# Cut Lab (Workstream: cut-lab)

## What This Is

A deterministic decision-support lane for DeckFlow: help a Commander builder reduce an oversized 110–150-card pool to a legal 100-card deck through evidence-backed, measurable tradeoffs — combined with a Goal-Based Consistency Lab so every cut or swap immediately shows its consequences against the builder's own stated goals.

This is **Cycle 18**, a priority-interrupt milestone planned in isolation from Cycle 17 (Creator-Style, phases 94–100, active in `../deckflow-cycle17`). Cut Lab phases start at **101+**. Cycle numbers record kickoff order; CalVer records actual ship order — Cut Lab takes the next available CalVer tag when it ships.

## Core Value

The builder makes the decisions; DeckFlow makes them legible. Never claim one card is objectively worse — show the measurable tradeoff (commander timing, keepable-hand rate, mana/color reliability, early interaction, plan presence, category-by-turn availability, flood/screw/curve risk) and let the builder decide. No AI dependency anywhere in the loop; AI stays an optional explanation layer at most.

## Current Milestone: Cycle 18 — Cut Lab + Goal-Based Consistency Lab

**Goal:** Ship the combined Cut Lab + Goal-Based Consistency Lab as the strongest non-AI product loop: oversized list in → evidence-backed cuts → simulated finished deck → builder-compatible export.

**Target features:**
- Oversized decklist intake with deck intent/goals capture (primary plan, secondary plan, bracket, desired play experience)
- Protected cards and packages (lock commanders, pet cards, lands, essential packages)
- Functional slot competition grouping + structural detection (curve congestion, stranded subthemes, redundant finishers, weak floors, enabler-starved cards)
- Configurable structural role floors (lands, ramp, draw, interaction, protection, engines, payoffs, win conditions)
- Cut rounds: obvious → structural → preference, with measurable consequences shown after every proposed cut
- User-defined goals ("cast commander by turn 3", "hold interaction by turn 2", …) with saved scenarios and what-if swap recalculation
- Live before/after simulation via existing Monte Carlo / mulligan / castability / plan-presence engines
- Export: Moxfield/Archidekt-compatible final list plus add/cut patch

**Out of scope (this milestone):** Deck Experiment Journal, Pod Fit / Rule Zero Passport, collection management, complete deck generation, universal power scoring, AI-generated cut decisions.

## Constraints

- All existing root PROJECT.md constraints apply (ASP.NET 10 + Razor, Render 512MB, theme CSS rules, RestSharp+Polly pattern, public repo, LF endings, changed-lines format gate).
- Reuse existing engines (parsing, role classification, categories, mana simulation, combo data, bracket rules, diff/export) — most new effort is defensible comparison rules and interaction design.
- Planning isolation: all Cycle 18 artifacts live under `.planning/workstreams/cut-lab/`; never mutate root `.planning/` milestone state or the Cycle 17 worktree.

## Key Decisions

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-07-18 | Combine Cut Lab + Goal-Based Consistency Lab into one milestone | Research: best balance of demand (5/5 + 4.5/5), medium effort, engine reuse, zero AI dependence |
| 2026-07-18 | Cycle 18 = priority interrupt; Cycle 17 keeps phases 94–100; Cut Lab starts at Phase 101 | Parallel planning isolation; ship whichever is product-priority first |
| 2026-07-18 | Deterministic tradeoff display, never verdicts | Product stance: preserve user agency; cEDH/Commander community rejects AI-slop framing |
| 2026-07-18 | Reuse 2026-07-18 feature-priority research; no new research fan-out | Research complete and preserved at `research/2026-07-18-commander-feature-priorities.md` |
| 2026-07-18 | SEED-001 fulfilled and unrelated; no seeds included | Confirmed during init |

## Evolution

This document evolves at phase transitions and milestone boundaries (see root PROJECT.md Evolution rules).

---
*Last updated: 2026-07-18 — Cycle 18 milestone initialization*
