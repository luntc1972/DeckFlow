---
title: Commander Deck Tag Suggestions
summary: Suggest functional categories for a card using multiple data sources.
order: 80
requires_flag: tool.categories.enabled
---

# Commander Deck Tag Suggestions

The Commander Deck Tag Suggestions (Category Suggestions) page supports multiple lookup modes:

- `CachedData`
- `ReferenceDeck`
- `ScryfallTagger`
- `All`

Current behavior:

- `ReferenceDeck` reads exact categories from a supplied Archidekt deck URL or pasted Archidekt text.
- `CachedData` reads category hits from the existing local Archidekt-backed store.
- `ScryfallTagger` returns oracle-tag style suggestions from Scryfall Tagger.
- `All` combines the cached-store path and tagger path, with EDHREC as a fallback when no other source returns anything.

Above the copy box, the suggested categories are shown in a weighted table:

- **Decks / %** — how often the category appears in your crawled decks for that card (the category's deck count over the card's total decks). Shown only for categories found in the cached store; categories that come only from Scryfall Tagger or EDHREC show `—`.
- **Sources** — an `N/M` agreement figure: `N` of the `M` sources that returned anything picked this category. Rows are ranked by agreement first, then by popularity.

The **Copy** button still copies plain category names only — the weights stay on screen so the pasted list stays clean for ChatGPT.

The page also exposes a background Archidekt harvest button so the local category store can be refreshed while the rest of the UI remains usable. Only one harvest runs at a time, and a local browser notification appears when the job completes.

Basic card type categories (Creature, Instant, Sorcery, Enchantment, Artifact, Planeswalker, Battle) are filtered out of cache suggestions.
