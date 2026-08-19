# Integrations

DeckFlow integrations, card data, APIs, command-line, and browser extension guidance.

## Moxfield–Archidekt Deck Sync

The Moxfield–Archidekt Deck Sync page (`/sync`) compares two decks and generates the delta import needed to bring the target deck in line with the source.

Supported sync directions:

| Direction | Description |
|---|---|
| MoxfieldToArchidekt | Moxfield as source, Archidekt as target |
| ArchidektToMoxfield | Archidekt as source, Moxfield as target |
| MoxfieldToMoxfield | Compare two Moxfield decks |
| ArchidektToArchidekt | Compare two Archidekt decks |

For same-system comparisons, column labels update dynamically to reflect the source and target platform.

---

## Card Lookup

The Card Lookup page (`/card-lookup`) has two modes:

- **Single Card** (default; the only mode visible on mobile) — type a card name, get live Scryfall suggestions once you've entered 4+ characters, and picking a suggestion (or pressing Look Up) renders that card's Oracle text plus WOTC rulings inline via `GET /card-lookup/single`.
- **Card List** (desktop-only) — paste up to 100 card names and download the full Scryfall output as `.txt` (`POST /card-lookup/download`) or structured `.json` (`POST /card-lookup/download-json`). The inline line editor with per-row autocomplete is still available for editing before downloading.

Under the hood all modes use the same `ICardLookupService`: the card collection is fetched via `POST /cards/collection` in batches of 75, and rulings are fetched per-card via `GET /cards/{id}/rulings`.

The Single Card result panel also detects keyword mechanics and ability words on the resolved card, looks up the current official WOTC rules text for each detected term, and renders those entries in a separate **Keyword Rules** panel below the card text. This is intentionally limited to Single Card mode so large list downloads do not fan out into extra mechanic-rule lookups.

The Single Card result panel includes an "Ask a rules question about this card →" link that deep-links into `/judge-questions?card=<name>`.

---

## Mechanic Rules

The Mechanic Rules page (`/mechanic-lookup`) looks up the current official Wizards Comprehensive Rules text for a keyword mechanic or rules term.

Behavior:

- Exact rules sections such as `Prowess` return the matching numbered section and summary.
- Glossary terms such as `Battle` resolve through the glossary and, when the glossary points to a major rules section like `310`, the page now returns the full referenced section body rather than only the glossary sentence or section header.
- The Clear button clears the saved input, summary block, and rendered rules text together.
- The rules text renders as an auto-growing block sized to its content — the same `pre.oracle-text` treatment Card Lookup uses — so the page scrolls rather than a short inner scrollbox. It was previously a fixed-height `textarea`, which on mobile left roughly a five-line window onto the full section.

The service caches the parsed Wizards rules document in memory for 6 hours so repeated lookups do not keep re-downloading the full rules text file.

---

## Ask a Judge

The Ask a Judge page (`/judge-questions`) leads with a prominent link to the live community judge chat at [`chat.magicjudges.org/mtgrules`](https://chat.magicjudges.org/mtgrules/) — a 24/7 IRC channel (`#magicjudges-rules` on Libera.Chat) staffed by certified judges and rules experts. This is the authoritative path. When the page is opened with a `?card=<name>` query parameter (e.g. from Card Lookup), it pre-formats a `!CardName — ` opening message ready to copy into the chat.

A clearly labeled **secondary** ChatGPT prompt generator is provided below for casual play and quick second opinions. It carries a prominent disclaimer ("ChatGPT can be confidently wrong about MTG rules") and, if a reference card is supplied, fetches that card's Oracle text and rulings via `GET /card-lookup/single` and embeds them in the generated prompt. The prompt itself starts with the same warning so ChatGPT cannot bury it.

---

## Commander Category Reference

The Commander Category Reference page shows the Archidekt tags that appear most often on decks where a given card is listed as the commander. It reports what observers assigned, not what the app infers.

The `% of decks` column is the share of that commander's harvested decks that run at least one card in the category — each deck is counted once, no matter how many of its cards carry the tag.

---

## Commander Deck Tag Suggestions

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

---

## Archidekt category cache
- Run `dotnet run --project DeckFlow.CLI -- archidekt-cache --minutes 5` to keep the local cache fed with the latest public decks.
- The CLI runs a dedicated cache session that respects rate limits via Polly, records skips for noisy decks, and persists card/category observations to `artifacts/category-knowledge.db`.
- The background hosted service reuses the same session logic to keep the cache fresh (the user-triggered harvest button was removed in v1.4).
- The cache session now stays alive for the requested harvest window even when the queue runs dry, and it retries transient recent-page fetch failures instead of ending the whole job early.
- Basic card type categories (Creature, Instant, Sorcery, Enchantment, Artifact, Planeswalker, Battle) are filtered out of cache suggestions.

---

## Web API
Swagger UI is available at `/swagger` when running in Development mode.

### Category suggestion
```
POST /api/suggestions/card
Content-Type: application/json

{
  "mode": "CachedData",
  "archidektInputSource": "PublicUrl",
  "archidektUrl": "",
  "archidektText": "",
  "cardName": "Guardian Project"
}
```

### Commander category lookup
```
POST /api/suggestions/commander
Content-Type: application/json

{
  "commanderName": "Bello, Bard of the Brambles"
}
```

### Archidekt cache background jobs
Start a background harvest:
```
POST /api/archidekt-cache-jobs
Content-Type: application/json

{
  "durationSeconds": 300
}
```

Poll a specific job:
```
GET /api/archidekt-cache-jobs/{jobId}
```

Get the currently active job, if any:
```
GET /api/archidekt-cache-jobs/active
```

### cURL examples
```bash
curl -X POST http://localhost:5000/api/suggestions/card \
  -H "Content-Type: application/json" \
  -d '{"mode":"CachedData","archidektInputSource":"PublicUrl","cardName":"Guardian Project"}'

curl -X POST http://localhost:5000/api/suggestions/commander \
  -H "Content-Type: application/json" \
  -d '{"commanderName":"Bello, Bard of the Brambles"}'
```

---

## Scryfall usage
- Scryfall is used for card-name autocomplete, commander autocomplete, the Card Lookup page, card reference resolution in the Deck Analysis workflow, and async set catalog loading.
- All Scryfall clients send a real `User-Agent`, an explicit `Accept` header, and use `https`.
- Card lookup uses `POST /cards/collection` in batches of 75 identifiers.
- The Card Lookup page is capped at 100 non-empty input lines per submission (at most two `cards/collection` requests plus one `cards/{id}/rulings` request per unique resolved card, all throttled). The cap is enforced **server-side** in `DeckLookupController`, not only in `card-lookup.ts`, so a direct POST cannot bypass it; both sides count non-empty lines the same way.
- The AI workflow uses the same batch endpoint to resolve authoritative Oracle text for all deck cards.
- The set catalog is fetched via `GET /sets` and cached in memory for 6 hours; the web UI loads it asynchronously via `/api/set-options`.

### Rate limiting
- Scryfall enforces a soft cap of 10 requests per second at the Cloudflare edge (no proactive `X-RateLimit-*` headers on 200 responses; only `Retry-After` on 429).
- `DeckAnalysisPacketService` throttles all Scryfall calls to ~110ms apart (≈9 req/s) via a process-wide semaphore so batched collection lookups plus per-card fallback searches stay under the cap.
- On a 429 the wrapper reads `Retry-After` and retries once if the cooldown is ≤5 seconds; longer cooldowns surface as a friendly "Scryfall returned HTTP 429. Try again shortly." error instead of being misattributed to card/commander validation.
- The CLI ships a diagnostic `scryfall-probe` command that calls Scryfall and dumps status, headers, and body — useful for reproducing rate-limit responses. Example: `dotnet run --project DeckFlow.CLI -- scryfall-probe --endpoint random --repeat 25` (intentionally triggers 429).

---

## CLI usage examples
```bash
dotnet run --project DeckFlow.CLI -- compare \
  --moxfield my.deck --archidekt other.deck --out diff.txt

dotnet run --project DeckFlow.CLI -- archidekt-cache --minutes 10

dotnet run --project DeckFlow.CLI -- category-find \
  --card "Guardian Project" --cache-seconds 20
```

Content KB distill selects its LLM backend with `DECKFLOW_LLM_PROVIDER` (`openai` default, `claude` for the local CLI subscription backend). See [`docs/ops/content-kb-llm-cli-backends.md`](ops/content-kb-llm-cli-backends.md) for exact WSL, Windows, and Windows `dotnet.exe` from WSL commands.

---

## Browser Extension

The **DeckFlow Bridge** Chrome/Edge extension lets DeckFlow fetch Moxfield decks through your logged-in browser session when direct server-side requests fail.

See [`browser-extensions/deckflow-bridge/README.md`](../browser-extensions/deckflow-bridge/README.md) for load-unpacked installation instructions, or open `/deckflow-bridge` in the running app to download the current ZIP package.

---
