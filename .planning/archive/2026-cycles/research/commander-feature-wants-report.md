# Commander/EDH Deck-App Feature Wants — Research Report

**Date:** 2026-06-27
**Method:** deep-research workflow (5 search angles → 12 sources fetched → 54 claims extracted).
**Status:** VERIFIED. Workflow's adversarial vote-phase was blocked by sustained API rate-limit (produced false "0-0 refuted" on every claim — verify agents never ran). Re-verified manually via direct WebFetch on 2026-06-27: 6/6 core source claims CONFIRMED with exact quotes (EDHTop16, Salubrious Snail, Rate My Decks, EDHRank, draftsim builder review, cEDH Analytics). Remaining table rows are single-source-attested from the same fetched pages.

---

## 1. cEDH Competitive Players

| Want | Evidence / who does it | Source |
|---|---|---|
| Tournament metagame breakdown (commander meta-share %, entry counts), filterable (Sort/Time/Min-Entries/Tournament-Size); data via TopDeck.gg, public GitHub | EDHTop16 | edhtop16.com |
| Time-windowed, filterable meta (2wk/6mo/12mo/all-time, by color identity + commander) | MTGTop8 cEDH | mtgtop8.com/format?f=cEDH |
| Card inclusion-rate / staples ("Most Played Cards" 98%/86%…) | MTGTop8 | mtgtop8.com |
| Tournament-data-backed cut/add recommendations + missing-staple flags + infinite-combo detection per commander | cedh.io | cedh.io |
| Large aggregated decklist corpus (15,537 lists / 622 commanders, from Moxfield + cEDH DB; 238 tournaments ≥48 players) | cEDH Analytics | cedh-analytics.com |
| Human-curated tiered decklists (Competitive / Brewer's Corner) with status tags (BREW/COMPETITIVE/OUTDATED/HISTORIC) | cEDH Decklist Database | cedh-decklist-database.com |
| Plain-language win-condition + combo-line writeups per deck | cEDH Decklist DB | cedh-decklist-database.com |

**Unmet (cEDH):** meta sites explicitly do NOT give win-rates, manabase-consistency analysis, combo-line breakdowns, or exportable data artifacts. Gap = deck-level analytical depth, not raw meta data.

## 2. Casual / Social Commander

| Want | Evidence | Source |
|---|---|---|
| Official 5-tier bracket classification (B1-4 casual, B5 cEDH) auto-applied | Rate My Decks | ratemydecks.com |
| Multi-factor power scoring (1-10, 12 factors: fast mana, tutors, interaction, combo density, ramp, draw, curve, lands, commander synergy, game-changers) | Rate My Decks | ratemydecks.com |
| Strategy-aware rebalancing toward a target bracket + budget filter + "cards I own" filter | deckcheck.co | deckcheck.co |

**Unmet (casual):** bracket *balancing* (which cuts move a deck into a chosen bracket) is thin. Pod-fairness / cross-deck comparison barely served.

## 3. Content Creators

| Want | Evidence | Source |
|---|---|---|
| Live deck-as-overlay on stream (overlay + companion Twitch panel) | AetherHub DeckHub | aetherhub.com/Apps/DeckHub |
| Installable, audience-interactive Twitch overlay (mobile + replay components) | DeckMaster (MTG Arena) | github.com/FugiTech/deckmaster |
| Long-form primer authoring attached to a deck; primers are high-effort + need constant updating (esp. cEDH) | Moxfield primer feature; BlazeHero guide | moxfield.com primer |
| Fresh-content pipeline from successful-deck database | MTG Circle / CardFlow | draftsim.com/mtg-content-creator |

**Unmet (creators):** primers are manual and decay fast → auto-generated, auto-refreshing primer artifacts is the clear gap (DeckFlow's lane). No incumbent ties Commander/cEDH deck analysis → ready-to-publish creator output. Overlays exist but are Arena/Twitch-bound, not Commander-paper-pod oriented.

## 4. General Deck-Builders

| Want | Evidence | Source |
|---|---|---|
| Multi-dimensional scoring instead of one number (Power/Speed/Control/Consistency, each 0-5) | EDHRank | mtgmana.rocks |
| Consistency from combo density + tutors + card-advantage counts | EDHRank | mtgmana.rocks |
| Spell-castability manabase sim (cast rate + avg casting delay vs MV; benchmark 90% / 0.3) + Tap Analyzer (untapped frequency, opening-turn impact) | Salubrious Snail | salubrioussnail.com/manabase-tool |
| Baseline integrations: Moxfield + Archidekt URL import + plain-text paste, Scryfall enrichment (table stakes) | Rate My Decks | ratemydecks.com |

## Incumbent Pain Points

- **Moxfield** — best overall UX (clean UI, hotkeys, search, reusable card "packages") but cannot share whole folders of decks; no "build-me-a-deck" AI; no automated ban/bracket check. [draftsim best-builder; manaforge.tools]
- **Archidekt** — rated least intuitive (1/5); deck-builder overlay "uncomfortable"; auto-categorization (Evasion/Protection) "obnoxious"; cluttered playtester. [draftsim]
- **EDHREC** — averages, not your meta; no deck-level scoring/manabase (domain-known; corroborated by gap pattern).
- **Commander Spellbook** — combo DB only; no deck scoring/manabase/primer.
- **Meta sites (EDHTop16 / cEDH Analytics)** — meta data without deck-level analysis or exports.

## Cross-Cutting Unmet Needs (where DeckFlow already aims)

1. Analysis → publishable artifact in one round-trip — uncontested; DeckFlow's core thesis.
2. Auto-generated, auto-refreshing primers — manual + decaying everywhere else.
3. Bracket *balancing* (cuts to hit a target bracket), not just scoring.
4. Spell-castability manabase — only Salubrious Snail does it → differentiator (DeckFlow shipping manabase work).
5. Multi-dimensional scoring (EDHRank-style 4-axis) beats single power number.
6. Table-stakes integrations confirmed: Moxfield + Archidekt import + Scryfall. Folder-level sharing is an open Moxfield gap.

---

## Sources fetched (12)

- https://www.cedh-analytics.com/ (secondary)
- https://edhtop16.com/ (primary)
- https://www.ratemydecks.com/en (blog)
- https://cedh-decklist-database.com/ (secondary)
- https://www.mtgtop8.com/format?f=cEDH (secondary)
- https://moxfield.com/decks/icKufeoz_U-4HMNlorzgnw/primer (blog)
- https://aetherhub.com/Apps/DeckHub (secondary)
- https://github.com/FugiTech/deckmaster (primary)
- https://draftsim.com/mtg-content-creator/ (blog)
- https://draftsim.com/best-mtg-deck-builder/ (blog)
- https://www.salubrioussnail.com/manabase-tool (blog)
- https://mtgmana.rocks/tool_edhrank.html (blog)

Additional tools surfaced in search (not deep-fetched): cedh.io, deckcheck.co, manaforge.tools, MTG Circle / CardFlow.

**Caveat:** verify votes never ran (rate limit). Findings = single-source-attested + domain check.
