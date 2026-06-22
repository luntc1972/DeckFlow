# Follow-up — Local source cache from prod deck-harvest (HARVEST-CACHE)

**Idea (user, 2026-06-21):** Pull the commonly-used mana rocks + lands from the production deck
harvest and store them locally, so the manabase classifier resolves common cards without a
Scryfall round-trip every analysis.

## Why
- Speed: most decks share the same ~150 staple lands/rocks (Command Tower, signets, talismans,
  fetches, shocks, duals, Sol Ring, etc.). Caching their facts skips the Scryfall `cards/collection`
  call for the bulk of a typical deck.
- Fewer upstream calls / less rate-limit pressure; partial offline capability.
- A curated set also lets us hand-fix edge cases (fetch colors, any-color sources) once.

## Open question to resolve first
Does the prod harvest DB hold per-card FACTS (type line, oracle text, produced_mana) or only card
NAMES + frequencies? Harvest is deck/video content — likely names + counts, NOT Scryfall facts.
- If names only: query prod for the top-N most-frequent land/rock card names → fetch each once from
  Scryfall → bake a seed file. Prod gives the *frequency ranking*; Scryfall gives the *facts*.
- If facts present: build the seed directly from prod.

## Sketch
1. Read-only query against prod Postgres (per memory: `mcp__render__query_render_postgres`,
   postgresId `dpg-d7oj8iugvqtc73fso0g0-a` — read-only by design; NEVER the write string).
   Find the table holding harvested deck cards; rank land + mana-rock names by occurrence.
2. Resolve the top-N (≈150–300) via Scryfall once; map to the classifier's `CardFact` shape.
3. Ship as a checked-in seed (e.g. `DeckFlow.Core/Manabase/data/common-sources.json`) loaded at
   startup; `ManabaseAnalysisService` checks the cache first, falls back to Scryfall for misses.
4. Refresh script (manual/periodic) to re-rank + re-bake as the meta shifts.

## Scope
Separate from phase 64 (modes + castability). Its own small phase. Needs: prod schema spike →
decide names-only vs facts → seed format + loader → cache-then-Scryfall wiring + tests.

**Next step:** read-only spike of the prod harvest schema to answer the open question.
