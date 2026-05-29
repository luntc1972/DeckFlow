---
title: Deck Primer prompt artifact — design notes
date: 2026-05-29
context: /gsd-explore session — new paste-ready prompt artifact for deck builders
status: design
target_milestone: v1.5
---

# Deck Primer prompt artifact — design notes

## Origin

Explore session 2026-05-29. Question: what other prompt artifacts would help deck
builders? Surfaced pain point = **writing a deck primer for Moxfield**. Most painful
sections to draft by hand: **combo lines (step-by-step)** and **matchup notes**.

Fits DeckFlow core value: produce a ChatGPT-ready artifact the user pastes and gets a
finished primer in one round-trip, no reformatting. Sibling to DeckAnalysis /
DeckComparison / CedhMetaGap workflows (own tab, own packet service).

## Primer section skeleton

Derived from a real ChatGPT-written primer the user pasted (Jeskai spellslinger-combo):

1. **Deck Identity** — one-line archetype ("tempo → explosion → combo"), what it is NOT
2. **Core Game Plan** — early (T1–3) / mid / late phases, goals per phase
3. **Win Conditions** — named combo lines, step-by-step loops, results
4. **Interaction** — protection suite + removal suite
5. **Mulligan Guide** — snap keep / risky keep / mulligan, plus priority order
6. **How to Play vs Strategies** — matchup notes (bracket-routed, see below)
7. **Common Mistakes to Avoid** — sequencing, tutor targets, protection misuse, etc.
8. **How You Win Most Games** — condensed gameplan recap

## Data-grounding map (use DeckFlow data, do not let AI hallucinate)

| Section | DeckFlow data source | Notes |
|---|---|---|
| Deck Identity | commander + colors + category mix | derive archetype label from category weights |
| Win Conditions / combo lines | `CommanderSpellbookService.FindCombosAsync` | returns prerequisites + steps + produces; inject as ground truth |
| Interaction | `CategorySuggestionService` + tagger (protection/removal cats) | classify the deck's interaction cards |
| Mulligan Guide | category data — ramp / draw / payoff buckets | "snap keep" rules built from what the deck actually runs |
| Matchups | bracket-routed (see below) | |
| Mistakes / Gameplan | AI narrative over the above | grounded by injected combo + category context |

## Decisions

- **Combo handling = BOTH** (confirmed):
  - Inject Spellbook's known combos as ground truth → AI narrates readable pilot lines.
  - ALSO ask AI to extend with speculative synergies, flagged "speculative — verify these"
    so invented interactions are visibly fenced.
- **Matchup archetype source = bracket-routed, option A** (confirmed):
  - **Bracket 5 / cEDH** → named meta archetypes from `EdhTop16Client` (already exists).
  - **Brackets 1–4** → 5 generic strategy buckets (Aggro / Control / Midrange / Combo /
    Stax-Hate), exactly as the example primer used. **No EDHREC integration** — keeps this
    shippable on existing data sources.
  - Bracket becomes a **user input** on the primer form that routes the matchup source.
  - EDHREC archetype data explicitly deferred (would be net-new HTTP service + resilience
    pipeline; not worth it for v1 of this feature).

## Open risks → see todo + research question

- Combo data richness for narration → spike `spike-combo-data-to-primer-grounding`.
- Reliable category classification for mulligan buckets → research question logged.

## Related

- [[deck-primer-generator]] — seed for the v1.5 feature
- Reuses paste-ready packet pattern from DeckAnalysisPacketService / DeckComparisonService
