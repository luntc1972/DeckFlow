# Research Questions

Open questions surfaced during exploration/planning. Resolve via `/gsd-explore` research
pass, `/gsd-spike`, or a phase researcher.

---

## Deck Primer Generator (v1.5)

- **Mulligan-bucket classification** (2026-05-29, source: /gsd-explore)
  What category/tagger signals reliably classify a card as ramp / draw / protection /
  removal / payoff across *casual* decks (not just cEDH staples)? The primer's Mulligan
  Guide and Interaction sections depend on trustworthy bucketing. Investigate
  `CategorySuggestionService` modes + tagger tag coverage; identify gaps where the AI prompt
  must fall back to inferring from the card list directly.
  Related: [[deck-primer-prompt-design]]
