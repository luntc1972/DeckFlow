---
title: Deck Primer prompt artifact — design notes
date: 2026-05-29
updated: 2026-05-29
context: /gsd-explore sessions — new paste-ready prompt artifact for deck builders
status: design
target_milestone: v1.5
---

# Deck Primer prompt artifact — design notes

## Origin

Explore sessions 2026-05-29. Question: what other prompt artifacts would help deck
builders? Surfaced pain point = **writing a deck primer for Moxfield**. Most painful
sections to draft by hand: **combo lines (step-by-step)** and **matchup notes**.

Fits DeckFlow core value: produce a ChatGPT-ready artifact the user pastes and gets a
finished primer in one round-trip, no reformatting. Sibling to DeckAnalysis /
DeckComparison / CedhMetaGap workflows (own tab, own packet service).

Section catalog below is the **union of 4 real primers** the user pasted:
- Jeskai spellslinger-combo (casual, generic-bucket matchups)
- Plagon Azorius flicker (cEDH — named-archetype matchups + counter cheat sheets)
- Edgin/Bohn Izzet foretell (bracket 3 — power-level statement, meta positioning)
- Celes aristocrats (multi-axis — pivot-plan section, mental model)

## Master section catalog (31 sections)

Grounding = where DeckFlow data feeds the prompt vs pure-AI narrative.
Bracket scope = which brackets a section applies to (drives preset defaults).

| # | Section | Grounding | Bracket scope |
|---|---|---|---|
| 1 | Overview / TL;DR pitch | AI narrative | any |
| 2 | Deck Identity / archetype label | commander + colors + categories | any |
| 3 | Deck Philosophy / Mental Model | AI | any |
| 4 | Power Level / Bracket statement | user input | any |
| 5 | Strengths | AI | any |
| 6 | Weaknesses | AI | any |
| 7 | Skill Expression / what it rewards | AI | any |
| 8 | Engine Breakdown (by role/sub-engine) | category + tagger | any |
| 9 | Individual Card Roles (card-by-card) | category + tagger | any |
| 10 | Win Conditions (summary) | Spellbook | any |
| 11 | Core Combo Lines (pieces→line→result) | Spellbook (ground truth) + speculative-extend | any |
| 12 | Combo Priority Framework (ranked lines) | AI over Spellbook | any |
| 13 | Core Game Plan (early/mid/late) | AI | any |
| 14 | Mulligan Guide (general) | category buckets (ramp/draw/payoff) | any |
| 15 | Opening Hand Heuristics | AI | any |
| 16 | Decision Framework (pre-tutor / pre-win) | AI | any |
| 17 | Tutor Priority Guide/Tree | category (tutors) | any |
| 18 | Advanced Sequencing / Play Patterns | AI | any |
| 19 | When to Pivot Plans (role/plan switching) | AI | multi-axis decks |
| 20 | What to Protect (priority) | Spellbook (combo pieces) | any |
| 21 | Strategic Identity / Role in Pod | AI | cEDH |
| 22 | Matchup Overview (Favored/Even/Underdog) | EdhTop16 / generic buckets | bracket-routed |
| 23 | Per-archetype Matchup Notes / Mulligan | EdhTop16 / generic buckets | bracket-routed |
| 24 | Must-Counter Guide (Always/Usually/…) | AI + meta | **cEDH only** |
| 25 | Counter Cheat Sheet by Deck | EdhTop16 | **cEDH only** |
| 26 | Meta Positioning (Strong vs / Weak vs) | generic buckets | **casual only** |
| 27 | Common Mistakes / Misplays | AI | any |
| 28 | Card-Specific Optimization (per-key-card) | AI | any |
| 29 | Top Cuts / Top Additions (upgrade path) | category gaps | any |
| 30 | Key Concepts (deck-specific named) | AI | any |
| 31 | Final Thoughts / Closing / Philosophy | AI | any |

## Section selection model — B+C hybrid (decided)

31 sections is too many for a flat checklist (fails the no-fiddle core value).

- **Bracket preset drives defaults** — user picks bracket on the form → pre-checks a sane
  section set. Paste deck → good primer with zero fiddling.
- **Rendered as 5 collapsible groups** so it's not a wall of toggles:
  - **Identity** (#1–7)
  - **Combos** (#10–12, #20)
  - **Gameplay** (#13–19)
  - **Matchups** (#21–26)
  - **Maintenance** (#8, #9, #27–31)
- User expands a group only to tweak; per-section on/off retained.

### Presets (v1 ships 2; Minimal later)

- **cEDH (bracket 5)**: checks #24/#25 + named-archetype matchups (#22/#23 via EdhTop16);
  hides/unchecks #26.
- **Casual / Upgraded (brackets 1–4)**: matchups via generic strategy buckets (#22/#23 +
  #26); unchecks #24/#25.
- **Minimal (later)**: #2 Identity + #10 Win Cons + #11 Combo Lines + #14 Mulligan only —
  quick primer.

## Decisions

- **Combo handling = BOTH**: inject Spellbook combos as ground truth + ask AI to extend with
  speculative synergies under a clearly-labeled "speculative — verify these" heading so
  invented interactions stay visibly fenced.
- **Matchup archetype source = bracket-routed (option A)**:
  - Bracket 5 / cEDH → named meta archetypes from `EdhTop16Client` (already exists).
  - Brackets 1–4 → 5 generic strategy buckets (Aggro / Control / Midrange / Combo /
    Stax-Hate). **No EDHREC integration** — ships on existing data sources.
  - Bracket is a user input that routes both matchup source AND preset defaults.
- **Selection = B+C hybrid** (preset defaults + grouped collapsible toggles), above.

## Open risks → see todo + research question

- Combo data richness for narration → spike `spike-combo-data-to-primer-grounding`.
- Reliable category classification for mulligan / engine-breakdown buckets → research
  question logged.

## Related

- [[deck-primer-generator]] — seed for the v1.5 feature
- [[spike-combo-data-to-primer-grounding]] — de-risk hardest section
- Reuses paste-ready packet pattern from DeckAnalysisPacketService / DeckComparisonService
