# P88 Feasibility — Archidekt Profile Crawl (Salubrious Snail)

*Recon for Cycle 16 P88 `CreatorProfileDeckCrawler`. Probed the live Archidekt public API 2026-07-04. No auth required.*

## Verdict: FEASIBLE ✅

Archidekt exposes a public, unauthenticated JSON API that resolves a creator by username and enumerates their public decks with rich metadata. Salubrious Snail yields **39 crawlable Commander decks** — well above the ≥5 min-deck floor (CS-03) — enough for a real measured-style profile.

## Endpoint spec (for CS-04a crawler)

1. **Resolve creator → canonical username + id:**
   `GET https://archidekt.com/api/users/?username=<query>`
   → `{count, results:[{id, username, deckCount, ...}]}`. Salubrious Snail = `username=SalubriousSnail`, `id=59324`, `deckCount=167`.

2. **Enumerate public decks (paginated):**
   `GET https://archidekt.com/api/decks/v3/?ownerUsername=<username>&pageSize=50&page=<n>`
   → `{count, next, results:[deck]}`. Follow `next` until null.
   Per-deck fields usable WITHOUT fetching cards: `id, name, size, deckFormat, edhBracket, colors{W,U,B,R,G}, private, unlisted, viewCount, tags, updatedAt, createdAt, parentFolderId`.

3. **Full card list per deck:** reuse existing `ArchidektApiUrl.BuildDeckApiUri(id)` + `ArchidektApiDeckImporter`.

### Privacy is respected for free
`deckCount=167` (profile total) vs `count=39` (owner-filtered list). The 128 difference = private/unlisted/theorycrafted decks the API does NOT return. Crawler only ever sees public decks — no ToS/privacy risk. (0 private/unlisted in the returned list.)

### deckFormat codes seen
`3 = Commander/EDH`. Filter `deckFormat==3` for this app.

## Salubrious Snail corpus (the P88 starter dataset)

- **39 decks, 100% Commander/EDH**, all public.
- Sizes: 37×100, 1×90, 1×101 (standard 100-card + occasional maybeboard).
- **edhBracket:** 29 unset, 7 bracket-3, 2 bracket-2, 1 bracket-4 → mostly mid-power (bracket 2-3), not cEDH.
- **Color identity: fully spread** — every WUBRG combo represented, no single-color dominance (UB×4 top). Signals a broad *brewer*, not a one-archetype player.
- Deck names confirm the style: "Biblically Accurate Beatdown", "Equipment are the best mana rocks", "Midrange Eggs", "Group Hellbent", "Radha's Explosive Vegetables" → off-meta, theme-first, midrange brewer.
- Top by views: "Buffs by Hans" (393k), "Radha's Explosive Vegetables" (167k, br-3), "Corsairs of Chronology" (110k).

### Free measured signal already in the list endpoint
`colors` (pip identity), `size`, `edhBracket`, `viewCount` come back without fetching cards — partial CS-05..10 features at near-zero cost. Full category/curve/lift stats still need the per-deck card fetch.

## Implications for P88 plan
- Crawler = 2 API calls to list + N per-deck fetches (39 here). Cache the set (mirror `ArchidektDeckCacheSession`); rate-limit via existing Polly pipeline.
- Style profile for Salubrious Snail will read as "broad midrange brewer, bracket 2-3, theme-driven" — good test case because it's NOT a narrow cEDH optimizer (stresses the say-vs-do fusion).
- Manual-URL fallback (CS-04a) is unnecessary for Archidekt; keep it only for creators without an Archidekt profile.
- **Open:** does Moxfield expose an equivalent public owner→deck-list endpoint? Verify before committing the crawler abstraction (Salubrious Snail is Archidekt-primary per the KB).

## Starter recommendation
Salubrious Snail is a solid **first creator** for P88/P92: 39 decks, one format, broad color coverage, distinct brewer voice, and 85 distilled KB video artifacts already on hand for the stated-rules half (P89). Both halves of the fused profile are sourceable today.
