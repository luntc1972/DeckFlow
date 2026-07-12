# Phase 95: Measured-Style Extractor - Research

**Researched:** 2026-07-11
**Domain:** Archidekt HTTP crawl + Core-vs-Web statistics extraction (DeckFlow)
**Confidence:** MEDIUM — code-verified seams are HIGH; live Archidekt endpoint shape is CITED (P88 prototype, not re-probed live this session); the CS-07 lift-denominator gap and the D-05 staple-vocabulary gap are confirmed absent in code (HIGH confidence *that they're missing*).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** `CreatorProfileDeckCrawler` resolves a creator via the Archidekt `ownerUsername` endpoint (feasibility-confirmed `/api/decks/v3/?ownerUsername=<u>&pageSize=&page=`) and loads each deck through the existing `ArchidektApiDeckImporter`. Creator→profile mapping lives in a **NEW creator-profile-source table** (slug + platform + profile URL/username) — NOT `CreatorSourceStore` (wrong shape, Codex MED). Manual per-creator URL list is the fallback if the endpoint regresses (re-verify at plan time).
- **D-02:** Reuse the existing Polly resilience pipelines; **cache the crawled deck set** mirroring `ArchidektDeckCacheSession` so re-running against the same creator does not re-hit Archidekt.
- **D-03:** Filter **>105-card maybeboard-contaminated decks OUT first** (before any per-deck ratio); dedup near-precon lists (Precon Effect); tag each deck with a confidence marker.
- **D-04:** **Graded folder weights, MANUALLY CURATED per creator.** Weights are NOT auto-derived from folder-name keywords — curated per creator and stored alongside the creator-profile-source mapping. Known Snail map: Current/Secondary = 1.0, Budget + In-consideration = 0.25–0.5, Other = 0.5. `parentFolder` (id + name) is captured from the Archidekt payload for every deck. **Default when a creator's weights are uncurated: 1.0 (full weight)** plus a "weights uncurated" flag on the profile.
- **D-05:** **HYBRID staple-strip, applied BEFORE any ratio is computed:** always strip the curated `ContentTagVocabulary` staple set (ubiquitous lands/rocks) **UNION** any card appearing in **>60%** of the creator's crawled decks. Canonical category bucket-mapping via `ContentTagVocabulary` is an explicit **prereq** before trusting measured ratios (Codex MED).
- **D-06:** Category tags come from `CardCategoryRepository` + Scryfall Tagger oracle tags. **Multi-category cards are counted in EVERY bucket they qualify for** (Command Zone "New Era" rule), not just their first match. `CardCategoryRepository` over `artifacts/category-knowledge.db` covers ~97% of Snail's cards; Tagger fills the tail.
- **D-07:** **Lift metric = creator-numerator / global-baseline.** `Pr(A∩B)` computed from the creator's OWN crawled decks; `Pr(A)·Pr(B)` from the global `CategoryKnowledgeRepository` history (322k obs). NOT raw co-occurrence.
- **D-08:** Combo density via `CommanderSpellbookService.FindCombosAsync`.
- **D-09:** Karsten land/curve consistency scoring reuses `DeckFlow.Core/Manabase/KarstenManabase` + `ManabaseAnalyzer` (pure Core, already unit-tested). Falsifiable targets.
- **D-10:** Every emitted `MeasuredMetric` carries **raw `NumDecks` (int — P94-locked top-level field, = count of crawled decks contributing to that metric)**. The **folder-weighted EFFECTIVE sample (fractional double, e.g. 8.5)** is stored in `MeasuredMetric`'s **nested extensible object** — planner chooses placement **WITHOUT adding a new top-level `MeasuredMetric` property**. If a genuinely new nested slot is required, raise it as a P94 nested-extension decision at plan time.
- **D-11:** Pure extraction logic (staple-strip, category counting, lift math, folder weighting, Karsten scoring) sits behind a **narrow host-agnostic contract**, unit-testable with no `HttpClient`/AspNet. The orchestrator/crawler that pulls decks + calls Spellbook + Tagger lives in `DeckFlow.Web`. Feed the pure algorithm plain in-memory deck + category data.
- **D-12:** **Salubrious Snail (39 public Commander decks)** is the seed + validation corpus. Multi-creator generalization is via the manual creator-profile-source mapping (D-01) — arbitrary creators onboarded manually, not auto-discovered, this phase.

### Claude's Discretion
- Exact dedup similarity threshold for near-precon lists (D-03) — planner/researcher picks a reasonable Jaccard-style cut.
- Concrete shape of the narrow extraction contract (D-11) — planner designs the seam.
- Per-deck confidence marker representation (D-03).

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope. (Moxfield crawl remains out of MVP per prior cycle decision; multi-creator auto-discovery is explicitly manual this cycle.)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CS-04a | `CreatorProfileDeckCrawler` — Archidekt profile URL → deck IDs → decks via `ArchidektApiDeckImporter`; new creator-profile-source table. | §Deck Sourcing below: exact existing seams (`ArchidektApiUrl`, `ArchidektApiDeckImporter`) + the two NET-NEW endpoints (`/api/users/?username=`, `/api/decks/v3/?ownerUsername=`) that have zero existing code today. |
| CS-04b | Rate-limit + resilience via existing Polly pipelines; cache crawled deck sets (mirror `ArchidektDeckCacheSession`). | §Reuse Seams: `ResiliencePipelineFactory` named pipelines, `ArchidektDeckCacheSession` pattern (no ready-made "cache-by-creator" variant — must be composed new). |
| CS-04c | Dedup near-precon lists; tag confidence; filter >105-card maybeboard-contaminated decks before per-deck ratios. | §Deck Sourcing: `ArchidektApiDeckImporter` board classification (`mainboard`/`maybeboard`/`commander`/`sideboard`) already separates maybeboard entries — size filter is a straightforward pre-check on imported entries. |
| CS-04d | Capture `parentFolder` (id+name); weight/segment profile by folder. | §Deck Sourcing: `parentFolderId` field confirmed in P88 prototype list-endpoint payload; NOT yet parsed by any existing importer — net-new field extraction. |
| CS-05 | Staple-strip before stats; reuse/extend `ContentTagVocabulary`. | §Staple-Strip Gap: `ContentTagVocabulary` has NO staple/land/rock list today — only `Archetypes`, `Brackets`, `CardCategories` (functional buckets). This is a genuine extension, not reuse of an existing set. |
| CS-06 | Category tagging via `CardCategoryRepository` + Scryfall Tagger; multi-category counting. | §Category Tagging: exact 3-layer seam (`GetCategoriesAsync`, `ScryfallTaggerLookupService.LookupOracleTagsAsync`) confirmed; `CategoryFilter.IncludedOrFallback` already does multi-category-preserving filtering (not first-match). |
| CS-07 | Lift metric `Pr(A∩B)/(Pr(A)·Pr(B))` from crawled history via `CategoryKnowledgeRepository`. | §Lift Denominator Gap: confirmed **no existing method computes global per-category marginal Pr(A) or joint Pr(A∩B) across all corpus decks** — every existing query is per-card or per-commander scoped. Concrete gap + required read-shape documented. |
| CS-08 | Combo density via `CommanderSpellbookService.FindCombosAsync`. | §Reuse Seams: exact signature captured, Web-host-only (DI ctor is `internal`, DI-registered as `ICommanderSpellbookService`). |
| CS-09 | Karsten land/curve consistency scoring, falsifiable targets. | §Reuse Seams: exact `KarstenManabase`/`ManabaseAnalyzer`/`ManabaseClassifier` signatures + the `CardFact` input shape and the existing Web-host mapping chain (`ScryfallCardFactMapper.ToCardFacts`) that the crawler must replicate for creator decks. |
| CS-10 | Emit `MeasuredMetric[]` with `numDecks` on every stat. | §P94 Nested-Extension Placement: exact record shapes reproduced; concrete candidate slots enumerated for the D-10 effective-sample double. |
</phase_requirements>

## Summary

Phase 95 sits at the intersection of two already-proven halves and one confirmed gap. The **Archidekt fetch mechanics** (P88 prototype) and the **category-tagging 3-layer merge** (P88 CS-06 finding) are both validated against live/real data and have clean code seams to compose (`ArchidektApiDeckImporter`, `CardCategoryRepository.GetCategoriesAsync`, `ScryfallTaggerLookupService.LookupOracleTagsAsync`). The **Karsten/ManabaseAnalyzer** path is fully proven pure-Core code with an existing Web-host mapping chain (`ScryfallCardFactMapper.ToCardFacts`) to imitate. The **Commander Spellbook combo-density** call is a one-line reuse of `ICommanderSpellbookService.FindCombosAsync`.

The two real gaps this research surfaces, both flagged as "verify at plan time" in CONTEXT.md:

1. **No owner→deck-list HTTP plumbing exists in code today.** `ArchidektApiUrl`/`ArchidektApiDeckImporter`/`ArchidektRecentDecksImporter` only fetch a single deck by ID or scrape the site-wide recent-decks HTML feed. The `/api/users/?username=` and `/api/decks/v3/?ownerUsername=&pageSize=&page=` endpoints (P88's feasibility finding) have **zero existing callers, models, or tests** — this is net-new HTTP code, not composition of an existing helper. The per-deck full-card fetch (step 3 of the crawl) DOES reuse `ArchidektApiDeckImporter.ImportAsync(deckId)` verbatim.
2. **`CategoryKnowledgeRepository` cannot answer "Pr(category A present) across the whole corpus" or "Pr(A∩B)" today.** Every read method is scoped to a single card (`GetCategoryRowsForCardAsync`) or a single commander (`GetCategoryRowsForCommanderAsync`) — there is no deck-level category-presence aggregate across all ~decks in `card_category_observations`. D-07's global baseline needs a new read-shape (documented below); this is a genuine gap, not a missing call-site.

A third smaller gap: `ContentTagVocabulary` has no staple/land/rock list — `Archetypes`/`Brackets`/`CardCategories` are the only three dimensions, none of which enumerate ubiquitous staples (Sol Ring, Command Tower, basics). D-05's "curated `ContentTagVocabulary` staple set" does not exist yet; it must be added as new content, not merely referenced.

**Primary recommendation:** Plan the crawler's owner-resolve + deck-list HTTP calls as net-new work (small — two GET endpoints, JSON parsing, pagination-follow on `next`), reuse `ArchidektApiDeckImporter` unchanged for per-deck card fetch, and treat the lift-metric global baseline as requiring either (a) a new read method on `CategoryKnowledgeRepository`/`CardCategoryRepository` that aggregates deck-level category presence, or (b) an in-memory computation the extractor does itself by pulling raw `card_category_observations` rows joined to `deck_queue` and aggregating client-side. Both are viable; the planner should pick based on corpus size (322k observations may be too large to pull wholesale into memory every profile computation — a SQL aggregate is likely cheaper).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Creator resolve + deck-list pagination (Archidekt HTTP) | API / Backend (`DeckFlow.Web`) | — | Needs `IHttpClientFactory` + Polly; no existing Core seam touches `ownerUsername`. |
| Per-deck card fetch | API / Backend (`DeckFlow.Web`, via `DeckFlow.Core` importer) | — | `ArchidektApiDeckImporter` already lives in `DeckFlow.Core/Integration` but itself uses RestSharp directly (Core project already has an HTTP exception carved out for `Integration/*` — see CLAUDE.md Key Dependencies: "RestSharp... used by every... `DeckFlow.Core/Integration/*ApiDeckImporter.cs`"). The crawler orchestrator (creator resolve, pagination, folder capture) is new Web-host code per D-11. |
| Staple-strip / category counting / lift math / folder weighting | Database / Storage-adjacent pure logic (`DeckFlow.Core`) | — | D-11 mandates HttpClient-free, unit-testable pure functions; input is plain in-memory deck+category data. |
| Karsten land/curve scoring | Database / Storage-adjacent pure logic (`DeckFlow.Core`) | API / Backend (Scryfall card-fact resolution) | `KarstenManabase`/`ManabaseAnalyzer`/`ManabaseClassifier` are pure Core; the CardFact input requires a Web-host Scryfall resolution step exactly like `ManabaseAnalysisService` does today. |
| Combo density | API / Backend (`DeckFlow.Web`) | — | `CommanderSpellbookService` is a Web-host-only service (internal ctor, HTTP-backed); no Core equivalent exists or should exist. |
| Global lift baseline (Pr(A)·Pr(B)) | Database / Storage (`DeckFlow.Core.Knowledge`) | — | Lives in `CategoryKnowledgeRepository`'s SQLite/Postgres store; requires a new aggregate read method (gap documented below), not a Web-tier computation, to avoid pulling 322k rows over the wire. |
| Creator-profile-source persistence (new table) | Database / Storage (`DeckFlow.Core.Content`) | — | Mirrors `CreatorStyleProfileStore`/`CreatorSourceStore` dialect-guarded pattern; the crawler orchestrator reads/writes it via a repository interface, not raw SQL in Web. |
| `MeasuredMetric[]` persistence into `CreatorStyleProfile` | Database / Storage (`DeckFlow.Core.Content`) | — | `CreatorStyleProfileStore.UpsertAsync` (P94) is the existing write path; P95 populates the `MeasuredMetrics` section and calls it unchanged. |

## Standard Stack

No new external packages needed. This phase composes existing in-solution dependencies only (RestSharp, Polly, Dapper, System.Text.Json — all already referenced by `DeckFlow.Core`/`DeckFlow.Web`). Per CLAUDE.md, no new NuGet packages without explicit user approval, and none are needed here.

### Core (existing, reused)
| Library | Version (installed) | Purpose | Why Standard |
|---------|---------|---------|--------------|
| RestSharp | 114.0.0 [VERIFIED: repo `.csproj`/CLAUDE.md] | HTTP calls for the two net-new Archidekt endpoints, following `ArchidektApiDeckImporter`'s existing `RestClient` pattern | House HTTP abstraction; CLAUDE.md forbids `new HttpClient()` |
| Polly | 8.x [VERIFIED: repo `.csproj`/CLAUDE.md] | Resilience for the crawl (reuse or add a named pipeline in `ResiliencePipelineFactory`) | House resilience pattern; CLAUDE.md forbids per-call pipeline construction |
| Dapper | (existing, via `RelationalDatabaseConnection`) | New creator-profile-source table CRUD | Matches `CreatorStyleProfileStore`/`CreatorSourceStore` pattern |
| System.Text.Json | (BCL, existing) | Archidekt JSON parsing (same idiom as `ArchidektApiDeckImporter.ImportAsync`) | Already the house JSON approach for this integration |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| SQL aggregate for global Pr(A)/Pr(A∩B) | Pull all `card_category_observations` rows into memory and aggregate in C# | SQL aggregate is far cheaper at 322k-observation scale and keeps the pure-Core boundary honest (aggregation logic can still be pure — only the row-fetch is I/O); in-memory pull risks large payloads and duplicates existing repository-layer aggregation conventions (`GetCategoryRowsForCommanderAsync` already aggregates server-side) |

**Installation:** No new packages; nothing to install.

## Package Legitimacy Audit

Not applicable — no external packages are introduced by this phase. All dependencies (RestSharp, Polly, Dapper, System.Text.Json) are pre-existing, already-vetted repo dependencies reused as-is.

## Architecture Patterns

### System Architecture Diagram

```
Creator-profile-source table (NEW)
   (slug, platform, profile URL/username, folder-weight map, "weights uncurated" flag)
              |
              v
CreatorProfileDeckCrawler (DeckFlow.Web, NEW orchestrator)
   1. GET /api/users/?username=<u>        --> resolve canonical username/id      [NET-NEW HTTP call]
   2. GET /api/decks/v3/?ownerUsername=<u>&pageSize=&page=<n>  --> paginate list  [NET-NEW HTTP call]
        follow `next` until null; capture id, size, parentFolderId per deck
   3. for each deck id: ArchidektApiDeckImporter.ImportAsync(deckId)  --> List<DeckEntry>  [REUSED]
              |
              v
   Filter: size > 105  --> drop (CS-04c)
   Dedup: near-precon Jaccard cut  --> tag confidence marker (CS-04c)
   Cache: mirror ArchidektDeckCacheSession pattern, keyed by creator  --> avoid re-hitting Archidekt (CS-04b/D-02)
              |
              v
Pure extraction layer (DeckFlow.Core, NEW, HttpClient-free — D-11 contract)
   Input: IReadOnlyList<CreatorDeckSample> { DeckEntries, FolderId, FolderWeight, ConfidenceMarker }
          + per-card category lookups (already resolved by Web tier via CardCategoryRepository/Tagger)
          + global lift-baseline Pr(A), Pr(A∩B) (already resolved by Web/Core repository call)
   1. Staple-strip: ContentTagVocabulary staple set UNION >60%-of-decks frequency cut   (CS-05)
   2. Category counting: multi-bucket, every category a card qualifies for              (CS-06)
   3. Lift math: creator Pr(A∩B) / global Pr(A)·Pr(B)                                   (CS-07)
   4. Folder-weighted effective sample (fractional double)                              (D-10)
   5. Karsten/ManabaseAnalyzer scoring (pure Core call, needs CardFact[] built by Web)   (CS-09)
              |
              v
   Web tier composes: CommanderSpellbookService.FindCombosAsync per deck (combo density) (CS-08)
              |
              v
   MeasuredMetric[] --> CreatorStyleProfileStore.UpsertAsync (P94, REUSED unchanged)      (CS-10)
```

### Recommended Project Structure
```
DeckFlow.Core/
├── Knowledge/
│   ├── CreatorStyleProfile.cs          # existing (P94) — MeasuredMetric/MetricDistribution consumed, not modified structurally
│   ├── CategoryKnowledgeRepository.cs  # existing — extend with a new global lift-baseline read method (gap)
│   ├── ContentTagVocabulary.cs         # existing — EXTEND with a new staple-card set (gap, see below)
│   └── MeasuredStyleExtraction/        # NEW — pure, HttpClient-free extraction algorithm (D-11 contract)
│       ├── ICreatorDeckSample.cs (or record) — plain in-memory deck+category+folder input shape
│       ├── StapleStripper.cs
│       ├── CategoryCounter.cs
│       ├── LiftCalculator.cs
│       └── FolderWeighting.cs
├── Content/
│   ├── CreatorStyleProfileStore.cs     # existing (P94), reused unchanged
│   └── CreatorProfileSourceStore.cs    # NEW — mirrors CreatorStyleProfileStore's dialect-guarded shape
DeckFlow.Web/
└── Services/
    └── CreatorStyle/                   # NEW — Web-host orchestrator (D-11's Web side)
        ├── CreatorProfileDeckCrawler.cs   # owner-resolve + paginate + per-deck fetch + cache
        └── MeasuredStyleProfileBuilder.cs # composes pure Core extraction + CommanderSpellbookService + Karsten CardFact resolution
```

### Pattern 1: Web-host Scryfall-resolution-then-pure-Core-classify (the D-11/D-09 analog)
**What:** Web tier resolves external data (Scryfall cards, category tags) into plain Core DTOs, then hands off to pure, HttpClient-free Core static classes for the math.
**When to use:** Any time Core logic needs externally-sourced data but must stay unit-testable without network access.
**Example (existing, verbatim pattern to imitate):**
```csharp
// Source: DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:462-463
IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(deckEntries);
ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true, rampCreditV2: rampCreditV2, landRampSim: landRampSim);
// ...then: ManabaseAnalyzer.Analyze(deck, mode, importance, ...) — pure Core, no HttpClient
```
The measured extractor's Karsten scoring (CS-09) should follow this exact chain: Web resolves creator decks' cards via the existing Scryfall lookup path (same `ICardLookupService`/`ScryfallCardResolver` used by `ManabaseAnalysisService`), maps to `CardFact[]`, classifies, and calls `ManabaseAnalyzer.Analyze` per deck — unchanged, reused code.

### Pattern 2: Dialect-guarded store with test-seam ctor (creator-profile-source table)
**What:** `internal` ctor overload accepting `Func<CancellationToken, Task<DbConnection>>? connectionFactoryOverride`, `SemaphoreSlim` schema gate, `ensureSchemaEnabled` no-op flag.
**When to use:** Any new persisted table this phase introduces (creator-profile-source).
**Example:**
```csharp
// Source: DeckFlow.Core/Content/CreatorStyleProfileStore.cs:47-64 (verbatim pattern to mirror)
internal CreatorStyleProfileStore(
    RelationalDatabaseConnection connectionInfo,
    bool ensureSchemaEnabled,
    Func<CancellationToken, Task<DbConnection>>? connectionFactoryOverride)
{ /* ... schema gate + directory creation for SQLite ... */ }
```

### Anti-Patterns to Avoid
- **Writing crawled creator decks into the global `CategoryKnowledgeRepository` corpus:** `PersistDeckAsync`/`ReplaceSourceRowsAsync` exist on `CategoryKnowledgeRepository`/`ArchidektDeckCacheSession`, but using them for the creator's OWN 39 decks would pollute the global 322k-observation baseline that D-07's `Pr(A)·Pr(B)` denominator depends on (the creator's own decks would inflate their own baseline). Keep creator-crawled data in a separate in-memory/creator-scoped structure; only READ from `CategoryKnowledgeRepository` for the global baseline and the harvested-category fill (layer 2 of D-06).
- **New HttpClient() for the owner-resolve/deck-list calls:** Follow `ArchidektApiDeckImporter`'s existing `RestClient` construction (injectable, testable) rather than ad-hoc HTTP.
- **First-match category assignment:** D-06 requires every qualifying bucket, not the first. `CategoryFilter.IncludedOrFallback`'s existing behavior (returns ALL non-generic categories, only falls back to the raw set if all categories were excluded) is compatible — do not add a `.First()`/`.Take(1)` anywhere in the category pipeline.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Land/curve consistency math | A new Karsten-style regression or hypergeometric model | `DeckFlow.Core/Manabase/KarstenManabase` + `ManabaseAnalyzer` | Already pure Core, already unit-tested (`DeckFlow.Core.Tests/Manabase/*`), already calibrated (per STATE.md, cEDH land-target recalibration shipped 2026-07-11) |
| Combo detection | A hand-rolled combo-piece heuristic | `CommanderSpellbookService.FindCombosAsync` | Already resilient (Polly `spellbook` pipeline), cached (30-min `IMemoryCache`), and graceful-degrades to null on failure — reinventing this loses all three properties |
| Card→category tagging | A new NLP/oracle-text classifier | `CardCategoryRepository.GetCategoriesAsync` (97% coverage) + `ScryfallTaggerLookupService.LookupOracleTagsAsync` (tail fallback) | P88 CS-06 finding: this exact 3-layer merge already covers 97% of Snail's 2,726 unique cards for free; building new tagging duplicates existing harvested data |
| Deck fetch/parse | A new Archidekt deck-JSON parser | `ArchidektApiDeckImporter.ImportAsync(deckId)` | Already handles category extraction, board classification (commander/mainboard/maybeboard/sideboard), foil detection, set/collector-number — reuse verbatim for the per-deck fetch step |
| Dialect-guarded persistence | Raw ADO.NET per-dialect branching | Mirror `CreatorStyleProfileStore`'s `internal` test-seam ctor + `EnsureSchemaAsync` gate pattern | House convention; also matches CLAUDE.md's Dapper dialect-guarded store requirement |

**Key insight:** This phase's actual net-new surface is smaller than CS-04a..CS-10 suggests — most of the ratio/scoring math already exists as tested Core code (Karsten) or Web services (Spellbook, CardCategoryRepository, Tagger). The genuinely new work is (1) two small Archidekt HTTP endpoints with pagination, (2) the pure staple-strip/lift/folder-weight orchestration logic, and (3) a global category co-occurrence read-shape that does not exist yet.

## Common Pitfalls

### Pitfall 1: Assuming an owner→deck-list helper already exists
**What goes wrong:** A plan or implementer searches for "ArchidektOwnerImporter" or similar and, finding nothing, either invents ad-hoc scraping OR (worse) mistakes `ArchidektRecentDecksImporter` (site-wide recent-decks HTML scrape, unrelated to a specific owner) for the owner-scoped endpoint.
**Why it happens:** `ArchidektRecentDecksImporter` exists in the same file area and superficially looks similar (paginated Archidekt crawler with Polly retry) but hits a completely different endpoint (`websockets.archidekt.com/search/decks`, HTML regex scrape) for a different purpose (harvest job's global recent-decks queue, not a specific creator's public decks).
**How to avoid:** Build the owner-resolve (`/api/users/?username=`) and owner deck-list (`/api/decks/v3/?ownerUsername=&pageSize=&page=`) calls as new methods using JSON parsing (mirroring `ArchidektApiDeckImporter`'s `JsonDocument.Parse` idiom), NOT by extending `ArchidektRecentDecksImporter`'s regex-HTML-scrape idiom.
**Warning signs:** Any code path building `IArchidektRecentDecksImporter` implementations for creator-specific crawling, or regex-parsing `href="/decks/..."` links for a creator profile.

### Pitfall 2: Global lift baseline computed by scanning all 322k rows per profile build
**What goes wrong:** Naively calling something like `GetCategoryRowsForCommanderAsync`-style full-table reads and aggregating client-side on every profile recompute is correctness-fine but potentially slow/expensive at 322k-observation scale, especially on the 512MB Render web tier (CLAUDE.md hosting constraint).
**Why it happens:** No existing method computes the aggregate server-side; the naive fallback is "read everything, aggregate in C#."
**How to avoid:** Push the aggregation into SQL (a `GROUP BY category` / self-join for co-occurrence, computed server-side) rather than pulling raw rows. This also matches the existing repository convention (all current methods aggregate server-side via `SUM`/`COUNT` in the query, never pull raw rows for client aggregation).
**Warning signs:** A new repository method that returns `IReadOnlyList<CardCategoryObservationRow>` unaggregated for "all decks."

### Pitfall 3: Polluting the global corpus with the creator's own crawled decks
**What goes wrong:** Reusing `ArchidektDeckCacheSession`/`CategoryKnowledgeRepository.ReplaceSourceRowsAsync` verbatim for the creator crawl (as D-02's "mirror `ArchidektDeckCacheSession`" instruction could be misread) would insert the creator's 39 decks into the SAME `card_category_observations` table that backs the D-07 global baseline — inflating the creator's own Pr(A)/Pr(B) denominator with their own numerator data (self-referential bias, however small at 39/322k).
**Why it happens:** `ArchidektDeckCacheSession` is the closest existing "cache a crawled Archidekt deck set" pattern, but its actual purpose IS to grow the global corpus — that's a different goal from D-02's "cache so we don't re-hit Archidekt for the SAME creator."
**How to avoid:** D-02's caching should be a creator-scoped cache (e.g., a new lightweight table or JSON blob keyed by creator slug + a freshness timestamp, NOT a write into `card_category_observations`/`sources`/`deck_queue`). Only mirror `ArchidektDeckCacheSession`'s *shape* (idle-poll loop, hash-based change detection via `DeckCategoryCacheWriter.ComputeCanonicalHash`) — not its *target table*.
**Warning signs:** Any call to `CategoryKnowledgeRepository.ReplaceSourceRowsAsync`/`PersistDeckCategoryBatchAsync`/`AddDeckIdsAsync` from the P95 crawler code path.

### Pitfall 4: `ContentTagVocabulary` staple set assumed to already exist
**What goes wrong:** A plan references "the curated `ContentTagVocabulary` staple set" as if it's a lookup that already exists (D-05's own wording implies reuse). It does not — `ContentTagVocabulary` today has exactly three dimensions (`Archetypes`, `Brackets`, `CardCategories`), none of which list staple cards (Sol Ring, Command Tower, basic lands, Arcane Signet, etc.).
**Why it happens:** D-05's phrasing ("reuse/extend `ContentTagVocabulary`") is ambiguous between "there's a set to reuse" and "extend the class with a new set."
**How to avoid:** Treat this as **extend**, not reuse: the planner must add a new curated staple-card list (or a new tag dimension) to `ContentTagVocabulary`, seeded from the P88 prototype's observed staples (Command Tower 23/39, Sol Ring 19/39, basics 15-17/39, Exotic Orchard 14/39, Negate 13/39, Arcane Signet 10/39, Rogue's Passage 10/39) as a starting curated set, separate from the per-creator >60%-frequency cut.
**Warning signs:** A task description that says "load the staple list from `ContentTagVocabulary`" without a corresponding task to first author that list.

### Pitfall 5: Precon-dedup threshold treated as precisely specified
**What goes wrong:** CONTEXT.md explicitly delegates "exact dedup similarity threshold for near-precon lists" to Claude's discretion — a plan that treats a specific Jaccard cutoff as already locked risks over-specifying a number nobody validated against real data.
**Why it happens:** The rest of D-03 (>105-card filter) IS a precise, locked number; it's easy to assume the dedup threshold is equally locked.
**How to avoid:** Document the chosen threshold explicitly in the plan as a Claude's-Discretion decision with rationale (e.g., ">70% card-overlap Jaccard similarity between two decks in the same creator's corpus flags a near-precon duplicate"), not as a re-derivation of a "locked" number.

## Code Examples

### Existing Archidekt per-deck fetch (reuse verbatim for step 3 of the crawl)
```csharp
// Source: DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs:42-106
public async Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
{
    if (!ArchidektApiUrl.TryGetDeckId(urlOrDeckId, out var deckId))
    {
        throw new InvalidOperationException($"Unable to determine Archidekt deck id from: {urlOrDeckId}");
    }
    var response = await RetryPolicy.ExecuteAsync(ct => _restClient.ExecuteAsync(CreateDeckRequest(deckId), ct), cancellationToken);
    // ... parses cards[], categories[], board classification (commander/maybeboard/sideboard/mainboard) ...
}
```
The crawler passes each `id` returned by the owner deck-list endpoint straight into `ImportAsync(deckId)` — no changes needed to this class.

### Existing category multi-bucket read (CS-06's "every qualifying bucket" requirement, already correct)
```csharp
// Source: DeckFlow.Core/Knowledge/CardCategoryRepository.cs:36-57
internal async Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
{
    // ... SELECT o.category ... GROUP BY o.category (returns ALL categories, not one)
    return CategoryFilter.IncludedOrFallback(categories);
}
```
`CategoryFilter.IncludedOrFallback` (in `DeckFlow.Core/Reporting/CategoryFilter.cs`) already returns every non-generic category for a card (excluding only card-type labels like "Creature"/"Artifact"), falling back to the raw set only when everything was excluded — this is already Command-Zone-New-Era-compatible (multi-bucket), no change needed.

### Existing Web-host Scryfall→CardFact→Karsten chain (the CS-09 pattern to replicate for creator decks)
```csharp
// Source: DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:408-463
ScryfallCardNameIndex index = await ResolveCardsAsync(deckCards, cancellationToken).ConfigureAwait(false);
// ... resolve each DeckEntry to ScryfallCardData, building DeckCardEntry list ...
IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(deckEntries);
ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true, rampCreditV2: rampCreditV2, landRampSim: landRampSim);
// then: ManabaseAnalyzer.Analyze(deck, mode, ...) — pure Core
```

### Missing global lift-baseline query shape (GAP — does not exist; illustrative target shape only)
```csharp
// NOT EXISTING CODE — illustrates the read-shape the extractor needs (D-07).
// CategoryKnowledgeRepository has no equivalent of this today; nearest analogs are
// GetCategoryRowsForCardAsync (per-card) and GetCategoryRowsForCommanderAsync (per-commander).
//
// Needed: for the WHOLE corpus (all deck_queue rows with processed=1, i.e. all crawled/harvested
// decks), per category: COUNT(DISTINCT deck) containing >=1 card tagged that category (-> Pr(A)),
// and per category PAIR: COUNT(DISTINCT deck) containing >=1 card in EACH category (-> Pr(A ∩ B)),
// each divided by COUNT(DISTINCT deck) total.
//
// SELECT o.category, COUNT(DISTINCT s.deck_queue_id) AS decks_with_category
// FROM card_category_observations o
// JOIN sources s ON s.id = o.source_id
// WHERE s.deck_queue_id IS NOT NULL
// GROUP BY o.category;
// -- divide by: SELECT COUNT(DISTINCT id) FROM deck_queue WHERE processed = 1;
//
// Pair co-occurrence requires a self-join on (source_id/deck_queue_id) grouped by category pair —
// no existing repository method does this; it is new SQL, new method, or client-side aggregation
// (see Pitfall 2 for the tradeoff).
```

## State of the Art

| Old Approach (pre-P95) | Current/Planned Approach | When Changed | Impact |
|--------------------|------------------|--------------|--------|
| `CategoryKnowledgeRepository` used only for global harvested-category lookups (read) and the recent-decks harvest job (write) | Same repository now ALSO consulted (read-only) as the global baseline for a per-creator lift metric | This phase (P95) | New read pattern; do not add a new write path from the creator crawler into this table (Pitfall 3) |
| No creator-specific Archidekt crawl exists in code | `CreatorProfileDeckCrawler` (net-new) | This phase (P95) | First Web-host consumer of the `ownerUsername` endpoint family |

**Deprecated/outdated:** None — this is greenfield within an established codebase; no prior measured-style extractor code exists to deprecate.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The Archidekt `/api/users/?username=` and `/api/decks/v3/?ownerUsername=&pageSize=&page=` endpoints still behave as documented in the 2026-07-04 P88 probe (unauthenticated, `next`-paginated, `parentFolderId` present per deck) | §Deck Sourcing, §Architecture Diagram | If Archidekt changed the endpoint shape or added auth/rate-limit since 2026-07-04, the crawler design needs live re-verification before implementation — CONTEXT.md D-01 already flags this as "re-verify at plan time" and CS-04a mandates a manual-URL fallback if it regressed |
| A2 | Salubrious Snail's Archidekt username/profile URL is unchanged and still resolves to the same 39-deck public corpus | §Reuse Seams, D-12 | If the corpus has grown/shrunk or the account changed, the P88/P89 validation numbers (18% label coverage, 97% harvested coverage, folder weights) are stale reference points, not live facts |
| A3 | 322k `card_category_observations` rows is still an accurate order-of-magnitude for the global corpus size (used to justify "aggregate in SQL, don't pull client-side" in Pitfall 2) | §Common Pitfalls, Pitfall 2 | If the corpus has grown substantially since the P88 prototype (2026-07-04), the performance argument only strengthens (more reason to aggregate server-side), so this assumption is low-risk even if stale |

**If this table is empty:** N/A — see rows above; all are dated live-data assumptions from the P88/P89 prototypes (2026-07-04/05), not this session's own verification (this session did not re-probe the live Archidekt API).

## Open Questions

1. **Should the creator-scoped deck cache (D-02) be a new table, or reuse `creator-profile-source`'s row with an embedded JSON blob of deck IDs + a freshness timestamp?**
   - What we know: `ArchidektDeckCacheSession`'s *shape* (hash-based change detection via `DeckCategoryCacheWriter.ComputeCanonicalHash`) is the pattern to mirror; its *target table* must NOT be reused (Pitfall 3).
   - What's unclear: Whether a dedicated `creator_deck_cache` table (deck_id, creator_slug, content_hash, folder_id) or a JSON blob column on the new creator-profile-source row is the better fit — this affects whether re-running the crawler diffs at the deck level or the whole-profile level.
   - Recommendation: Planner's call; a dedicated table (mirroring `deck_queue`'s shape but scoped to `creator_slug`) is more consistent with house conventions (P94's own D-01 rejected JSON-blob-only for anything needing per-row semantics), but a lighter JSON blob may suffice given only 39-ish decks per creator.

2. **Where exactly does the D-10 folder-weighted effective-sample double live inside `MetricDistribution`/a new nested record?**
   - What we know: `MetricDistribution` today has exactly `Mean`, `Min`, `Max`, `StdDev` (all `required double`) — no existing field represents "effective weighted sample size." `FusedConflict` is unrelated (P97-owned).
   - What's unclear: Whether to add a 5th field to `MetricDistribution` (e.g., `EffectiveSampleSize`) or introduce a new nested record type swapped into the `Distribution` slot (which is `MetricDistribution?` typed — changing its type is a bigger structural move than adding a field to the existing record).
   - Recommendation: Adding an optional field to `MetricDistribution` (`double? EffectiveSampleSize { get; init; }`) is the lower-risk path — it's additive, does not change `MeasuredMetric`'s top-level shape (satisfies D-10's explicit constraint), and `MetricDistribution`'s own doc comment already says P95/P97 "may extend the nested record" (94-CONTEXT.md D-08). Swapping the `Distribution` slot's type entirely would be a bigger, less obviously "nested extension" move. Planner should confirm this reading against 94-CONTEXT.md D-08 wording ("Their internal shape is defined here as minimal substrate; P95/P97 may extend the nested record but MUST keep the CS-01 top-level field names") — this permits adding fields to `MetricDistribution`, which is exactly what's needed.

3. **Does the >105-card maybeboard-contamination filter apply to raw Archidekt `size` (list-endpoint field, no per-deck fetch needed) or to the imported mainboard+commander count post-fetch?**
   - What we know: The P88 list endpoint returns `size` per deck without a card fetch (cheap pre-filter). `ArchidektApiDeckImporter.ImportAsync` separately classifies board membership (`mainboard`/`maybeboard`/`commander`/`sideboard`) after a full fetch.
   - What's unclear: Whether ">105 cards" (CS-04c) refers to the cheap `size` field (avoids ever fetching contaminated decks) or the post-import mainboard+commander count (more precise but requires the fetch first, defeating the "filter first" framing in D-03).
   - Recommendation: Use the cheap `size` field as the FIRST-PASS filter (matches D-03's "filter... OUT first" wording and P88's "37×100, 1×90, 1×101" observed sizes), since it avoids wasted fetches; a smaller planner discretion is what secondary check (if any) to run post-fetch for decks that pass the `size` filter but still have unusually large maybeboards.

## Environment Availability

Not applicable in the tool-availability sense (no new CLI/database/runtime dependency) — this phase's only external dependency is the live Archidekt HTTP API, which is a runtime network call already covered by the existing Polly resilience pipelines and is not a local-environment concern. No local tool probing was performed since nothing new needs installing.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 [VERIFIED: CLAUDE.md Key Dependencies] |
| Config file | none — standard `dotnet test` per project (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`) |
| Quick run command | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~MeasuredStyle"` (or equivalent per new test file) |
| Full suite command | `dotnet test` (solution-wide) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CS-04a | Owner-resolve + deck-list pagination parses `next`, `id`, `size`, `parentFolderId` correctly from a fixture JSON payload | unit | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CreatorProfileDeckCrawler"` | ❌ Wave 0 — no existing test file for this class |
| CS-04c | >105-card decks filtered before ratio computation; near-precon dedup tags confidence | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~MeasuredStyle"` | ❌ Wave 0 |
| CS-04d | `parentFolder` id+name captured; folder weight applied (curated + default-1.0-uncurated-flagged cases) | unit | same as above | ❌ Wave 0 |
| CS-05 | Staple-strip: curated set UNION >60% frequency cut, applied before any ratio | unit | same as above | ❌ Wave 0 |
| CS-06 | Multi-category counting (every qualifying bucket); 3-layer priority merge (creator label → harvested → Tagger tail) | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CategoryKnowledgeRepositoryTests"` (extend existing file) + new pure-extraction test | ✅ existing file for repository-level tests / ❌ new pure-extraction tests |
| CS-07 | Lift = creator Pr(A∩B) / global Pr(A)·Pr(B); demotes staples vs raw co-occurrence | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~MeasuredStyle"` | ❌ Wave 0 (plus a new repository-level test if a new global-baseline method is added to `CategoryKnowledgeRepositoryTests.cs`) |
| CS-08 | Combo density derived from `FindCombosAsync` result, degrades gracefully on null | unit | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CommanderSpellbookServiceTests"` (existing) + new orchestrator test | ✅ existing service tests / ❌ new orchestrator-level test |
| CS-09 | Karsten/ManabaseAnalyzer scoring reused correctly per creator deck | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~Manabase"` (existing, extend if new call shape) | ✅ existing suite (`DeckFlow.Core.Tests/Manabase/*`) already covers the underlying math; new tests only needed for the orchestration glue |
| CS-10 | `MeasuredMetric[]` round-trips through `CreatorStyleProfileStore.UpsertAsync`/`GetBySlugAsync` with `NumDecks` + nested effective-sample populated | unit (both dialects) | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CreatorStyleProfileStoreTests"` (existing file, extend) | ✅ existing file (`CreatorStyleProfileStoreTests.cs`, `CreatorStyleProfileTestData.cs`) — P94 already built the round-trip harness; P95 adds populated `MeasuredMetrics` fixtures to it |

### Sampling Rate
- **Per task commit:** targeted `dotnet test <Project> --filter <FullyQualifiedName~...>` for the touched area
- **Per wave merge:** `dotnet test` (full solution)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] New test file(s) under `DeckFlow.Core.Tests/` (folder TBD by planner — no existing `Knowledge`-mirroring subfolder for measured-style extraction pure logic; `CategoryKnowledgeReporterTests.cs`/`CategoryKnowledgeRepositoryTests.cs` live flat in `DeckFlow.Core.Tests/`, not in a subfolder) covering staple-strip, category counting, lift math, folder weighting
- [ ] New test file under `DeckFlow.Web.Tests/` for `CreatorProfileDeckCrawler` (owner-resolve + pagination + per-deck fetch composition), likely using `RichardSzalay.MockHttp` (already a Web.Tests dependency per CLAUDE.md) to fixture the two new Archidekt endpoints
- [ ] Extend `DeckFlow.Core.Tests/CreatorStyleProfileStoreTests.cs` / `CreatorStyleProfileTestData.cs` (P94-built harness) with populated `MeasuredMetrics` fixtures exercising the new nested effective-sample field
- [ ] If a new `CategoryKnowledgeRepository` global-baseline method is added: extend `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` with a multi-deck fixture proving Pr(A)/Pr(A∩B) aggregate correctly
- [ ] Framework install: none — xUnit/MockHttp already present

## Security Domain

> `security_enforcement` key absent from `.planning/config.json` — treated as enabled per instructions.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | This phase adds no auth surface — substrate only, no page/controller/user-facing endpoint (per CONTEXT.md phase boundary: "no user-visible surface, no page, no flag this phase") |
| V3 Session Management | No | Same as above |
| V4 Access Control | No | Same as above; no new endpoint to gate |
| V5 Input Validation | Yes | The Archidekt owner-resolve query parameter (creator username) and the manual-URL fallback both accept operator-supplied strings that get embedded in outbound HTTP requests — validate/encode via `RestSharp`'s `AddQueryParameter` (already does URL-encoding), never string-interpolate into a raw URL. `ArchidektApiUrl.TryGetDeckId` already demonstrates the house pattern (parse-and-validate before use) for the per-deck ID case; the new owner/username input needs an equivalent guard. |
| V6 Cryptography | No | No secrets, tokens, or crypto operations introduced by this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SSRF via operator-controlled Archidekt username/profile-URL fallback value being used to build an outbound request | Tampering/Elevation | Constrain the manual-URL fallback (D-01) to Archidekt's own domain via `ArchidektApiUrl`-style parsing (reject anything that doesn't resolve to `archidekt.com`), never accept an arbitrary user-supplied URL and fetch it verbatim |
| Unbounded pagination loop (malicious or malformed `next` field looping forever) | Denial of Service | Cap total pages/decks fetched per crawl run (mirror `ArchidektRecentDecksImporter.ImportRecentDeckIdsAsync`'s `count` ceiling) rather than trusting `next` unconditionally |
| Over-fetching / hammering Archidekt beyond ToS | Denial of Service (against a third party) | Reuse the named Polly pipeline + rate-limit discipline already established for `banlist`/`spellbook`/`scryfall` (CS-04b); do not bypass with raw unthrottled HTTP calls |

## Sources

### Primary (HIGH confidence — code read directly this session)
- `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` — per-deck fetch/parse, exact reuse target
- `DeckFlow.Core/Integration/ArchidektApiUrl.cs` — deck-ID parsing pattern
- `DeckFlow.Core/Integration/ArchidektRecentDecksImporter.cs` — confirms the "recent decks" scrape is a DIFFERENT endpoint than owner-scoped listing (Pitfall 1)
- `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` — cache-pattern shape to mirror (not its target table — Pitfall 3)
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` + `CardCategoryRepository.cs` — confirmed absence of a global Pr(A)/Pr(A∩B) read method (the CS-07 gap)
- `DeckFlow.Core/Knowledge/CategoryCacheSchema.cs` — confirmed `card_category_observations`/`sources`/`deck_queue` table shapes underlying the gap analysis
- `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` — confirmed absence of a staple-card list (the D-05 gap)
- `DeckFlow.Core/Content/CreatorSourceStore.cs` — confirmed "wrong shape" (channel-ref-keyed, YouTube/content-source-oriented) per CONTEXT.md D-01
- `DeckFlow.Core/Content/CreatorStyleProfileStore.cs` — P94 persistence pattern, dialect-guarded ctor shape to mirror for the new table
- `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` — exact `MeasuredMetric`/`MetricDistribution` record shapes for D-10
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` — exact `FindCombosAsync` signature and Web-host-only nature (D-08)
- `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` — named Polly pipeline registration pattern (D-02/CS-04b)
- `DeckFlow.Web/Services/Scryfall/ScryfallTaggerLookupService.cs` — exact `LookupOracleTagsAsync` signature and session/CSRF handling (D-06 tail layer)
- `DeckFlow.Core/Manabase/KarstenManabase.cs`, `ManabaseAnalyzer.cs`, `ManabaseClassifier.cs`, `CardFact.cs` — exact pure-Core signatures for D-09
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` (lines 400-463) — the exact Web-host Scryfall→CardFact→Karsten chain to replicate for creator decks (D-09/D-11 pattern)
- `DeckFlow.Core/Reporting/CategoryFilter.cs` — confirmed multi-category (not first-match) filtering already exists (D-06)
- `.planning/phases/95-measured-style-extractor/95-CONTEXT.md`, `.planning/phases/94-style-profile-foundation/94-CONTEXT.md`, `.planning/REQUIREMENTS.md`, `.planning/STATE.md` — locked decisions, requirement text, project history
- `grep` scan of `DeckFlow.Core`/`DeckFlow.Web` for `ownerUsername`/`api/users`/`parentFolder`/`api/decks/v3` — confirmed ZERO existing references (net-new plumbing claim)

### Secondary (MEDIUM confidence — prior-session prototype findings, not re-verified live this session)
- `docs/research/p88-archidekt-crawl-feasibility.md` — the `/api/users/?username=` and `/api/decks/v3/?ownerUsername=` endpoint spec, probed live 2026-07-04 (not re-probed this session — see Assumption A1)
- `docs/research/p88-measured-style-prototype-snail.md` — measured metrics computed from live 39-deck Snail corpus, 2026-07-04
- `docs/research/p88-cs06-harvested-category-fill.md` — 97% harvested-coverage finding, validated against live data 2026-07-04
- `docs/research/p89-p90-prototype-snail.md` — folder segmentation + stated-vs-measured fusion prototype, 2026-07-05

### Tertiary (LOW confidence)
None — no unverified WebSearch-only claims were used; all findings trace to either direct code reads (this session) or prior-session prototypes run against live data.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages, all reused dependencies directly confirmed in code
- Architecture: HIGH for reuse seams (directly read), MEDIUM for the two identified gaps' exact resolution shape (documented as open questions, correctly flagged as planner decisions)
- Pitfalls: HIGH — all five pitfalls trace to a specific, named, currently-existing code artifact that could be mistaken for the right tool

**Research date:** 2026-07-11
**Valid until:** 30 days for the code-verified seams (stable, in-repo); 7 days for the live-Archidekt-endpoint assumption (A1) if not re-verified before implementation — CONTEXT.md D-01 already flags this as a required plan-time re-check
