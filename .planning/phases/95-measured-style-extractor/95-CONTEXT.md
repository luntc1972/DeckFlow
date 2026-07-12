# Phase 95: Measured-Style Extractor - Context

**Gathered:** 2026-07-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Compute a creator's **measured** style profile from their OWN Archidekt decklists and emit `MeasuredMetric[]` (Phase 94 schema) — staple-stripped, lift-weighted, folder-segmented, every stat carrying `numDecks`. **Substrate only** — feeds Phase 97 (fusion); no user-visible surface, no page, no flag this phase.

Lives in `DeckFlow.Web` (needs `CommanderSpellbookService` + Scryfall Tagger, both Web-host services) behind a **narrow contract** so the pure extraction algorithm (ratios / lift / weighting / staple-strip) stays unit-testable independent of the host — the Codex-flagged Core-vs-Web layering resolution for this cycle.

Requirements: CS-04a, CS-04b, CS-04c, CS-04d, CS-05, CS-06, CS-07, CS-08, CS-09, CS-10.
</domain>

<decisions>
## Implementation Decisions

### Deck sourcing & crawl (CS-04a/b/c)
- **D-01:** `CreatorProfileDeckCrawler` resolves a creator via the Archidekt `ownerUsername` endpoint (feasibility-confirmed `/api/decks/v3/?ownerUsername=<u>&pageSize=&page=`) and loads each deck through the existing `ArchidektApiDeckImporter`. Creator→profile mapping lives in a **NEW creator-profile-source table** (slug + platform + profile URL/username) — NOT `CreatorSourceStore` (wrong shape, Codex MED). Manual per-creator URL list is the fallback if the endpoint regresses (re-verify at plan time).
- **D-02:** Reuse the existing Polly resilience pipelines; **cache the crawled deck set** mirroring `ArchidektDeckCacheSession` so re-running against the same creator does not re-hit Archidekt.
- **D-03:** Filter **>105-card maybeboard-contaminated decks OUT first** (before any per-deck ratio); dedup near-precon lists (Precon Effect); tag each deck with a confidence marker.

### Folder segmentation & weighting (CS-04d)
- **D-04:** **Graded folder weights, MANUALLY CURATED per creator.** Very few creators use Salubrious Snail's Current/Secondary/Budget/In-consideration/Other scheme, so weights are NOT auto-derived from folder-name keywords — they are curated per creator and stored alongside the creator-profile-source mapping. Known Snail map: Current/Secondary = 1.0, Budget + In-consideration = 0.25–0.5, Other = 0.5. `parentFolder` (id + name) is captured from the Archidekt payload for every deck. **Default when a creator's weights are uncurated: 1.0 (full weight) so nothing is silently dropped, plus a "weights uncurated" flag** on the profile so a consumer knows the segmentation hasn't been tuned.

### Staple-strip (CS-05)
- **D-05:** **HYBRID staple-strip, applied BEFORE any ratio is computed:** always strip the curated `ContentTagVocabulary` staple set (ubiquitous lands/rocks) **UNION** any card appearing in **>60%** of the creator's crawled decks (personal-staple frequency cut). Canonical category bucket-mapping via `ContentTagVocabulary` is an explicit **prereq** before trusting measured ratios (Codex MED).

### Category tagging (CS-06)
- **D-06:** Category tags (ramp/removal/draw/wipe/…) come from `CardCategoryRepository` + Scryfall Tagger oracle tags. **Multi-category cards are counted in EVERY bucket they qualify for** (Command Zone "New Era" rule), not just their first match. `CardCategoryRepository` over `artifacts/category-knowledge.db` covers ~97% of Snail's cards; Tagger fills the tail.

### Metrics (CS-07/08/09/10)
- **D-07:** **Lift metric = creator-numerator / global-baseline.** `Pr(A∩B)` computed from the creator's OWN crawled decks; `Pr(A)·Pr(B)` from the global `CategoryKnowledgeRepository` history (322k obs). This measures the creator's pairings vs meta expectation and demotes staples — the most discriminating of the options. NOT raw co-occurrence.
- **D-08:** Combo density via `CommanderSpellbookService.FindCombosAsync`.
- **D-09:** Karsten land/curve consistency scoring reuses `DeckFlow.Core/Manabase/KarstenManabase` + `ManabaseAnalyzer` (pure Core, already unit-tested). Falsifiable targets.
- **D-10:** Every emitted `MeasuredMetric` carries **raw `NumDecks` (int — the P94-locked top-level field, = count of crawled decks contributing to that metric)**. The **folder-weighted EFFECTIVE sample (fractional double, e.g. 8.5)** is stored in `MeasuredMetric`'s **nested extensible object** — planner chooses placement (extend `MetricDistribution` or introduce a nested confidence record swapped into the existing `Distribution` slot) **WITHOUT adding a new top-level `MeasuredMetric` property** (respects the P94/D-08 top-level-names lock). If a genuinely new nested slot is required, raise it as a P94 nested-extension decision at plan time.

### Layering (Core vs Web)
- **D-11:** Pure extraction logic (staple-strip, category counting, lift math, folder weighting, Karsten scoring) sits behind a **narrow host-agnostic contract** and is unit-testable with no `HttpClient`/AspNet. The orchestrator/crawler that pulls decks + calls Spellbook + Tagger lives in `DeckFlow.Web`. Feed the pure algorithm plain in-memory deck + category data.

### Seed / validation corpus
- **D-12:** **Salubrious Snail (39 public Commander decks)** is the seed + validation corpus (prototype-proven; >> the ≥5 MinDeckFloor). Tests validate extractor output against it. Multi-creator generalization is via the manual creator-profile-source mapping (D-01) — arbitrary creators are onboarded manually, not auto-discovered, this phase.

### Claude's Discretion
- Exact dedup similarity threshold for near-precon lists (D-03) — planner/researcher picks a reasonable Jaccard-style cut.
- Concrete shape of the narrow extraction contract (D-11) — planner designs the seam.
- Per-deck confidence marker representation (D-03).
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 94 schema (locked substrate this phase writes into)
- `.planning/phases/94-style-profile-foundation/94-CONTEXT.md` — D-04..D-08 schema locks (top-level field names, MinDeckFloor=5, InsufficientSample, empty-not-null sections).
- `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` — the `MeasuredMetric` / `MetricDistribution` records this phase populates (nested internals extendable; top-level names locked).
- `DeckFlow.Core/Content/CreatorStyleProfileStore.cs` — persistence pattern to mirror for the NEW creator-profile-source table.

### Crawl feasibility & measured-style prototype (grounding)
- `docs/research/p88-archidekt-crawl-feasibility.md` — Archidekt `ownerUsername` endpoint spec + Snail corpus (39 public decks).
- `docs/research/p88-measured-style-prototype-snail.md` — live measured-style run over 39 decks; 18%-tag-coverage lesson.
- `docs/research/p88-cs06-harvested-category-fill.md` — `CardCategoryRepository` covers 97% of Snail's cards; 3-layer priority merge (creator labels → harvested → Tagger tail).
- `docs/research/p89-p90-prototype-snail.md` — folder segmentation (5 folders), deck-ownership (all Snail's own), fusion say-vs-do findings.
- `docs/research/creator-style-llm-system.md`, `docs/research/creator-style-roadmap.md` — origin report + locked arc.

### Requirements
- `.planning/REQUIREMENTS.md` — CS-04a..CS-10 (this phase's requirement text + Codex-review resolutions).
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs`, `ArchidektApiUrl.cs`, `ArchidektRecentDecksImporter.cs` — deck fetch/parse; the crawler composes these.
- `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` — cache pattern to mirror for crawled-deck-set caching (D-02).
- `DeckFlow.Core/Knowledge/CardCategoryRepository.cs`, `CategoryKnowledgeRepository.cs`, `ContentTagVocabulary.cs` — category tags (D-06), global lift baseline (D-07), staple/bucket vocabulary (D-05).
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` — combo density (D-08); Web-host service (drives D-11 layering).
- `DeckFlow.Core/Manabase/KarstenManabase.cs`, `ManabaseAnalyzer.cs` — Karsten curve/land scoring (D-09), pure Core, already tested.

### Established Patterns
- Dialect-guarded Dapper store (P94 `CreatorStyleProfileStore`) — mirror for the NEW creator-profile-source table (D-01).
- Pure-Core-logic + Web-host-orchestrator seam (existing across `DeckFlow.Core` vs `DeckFlow.Web/Services`) — the D-11 narrow contract follows it.
- Polly named resilience pipelines (`ResiliencePipelineFactory`) — reuse for crawl (D-02).

### Integration Points
- Writes `MeasuredMetric[]` into a `CreatorStyleProfile` via `CreatorStyleProfileStore.UpsertAsync` (P94).
- Reads global co-occurrence from `CategoryKnowledgeRepository` for the lift denominator (D-07).
- `CreatorSourceStore` exists but is the WRONG shape — build a NEW creator-profile-source table (D-01), do not overload it.
</code_context>

<specifics>
## Specific Ideas

- Frequency staple cut is specifically **>60%** of the creator's crawled decks (24+/39 for Snail) — chosen as the balanced middle over >80% (too lax) and >50% (over-strips a small sample).
- Folder weights are a **manual curation artifact per creator**, not an algorithm — because almost no creators replicate Snail's folder taxonomy.
</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. (Moxfield crawl remains out of MVP per prior cycle decision; multi-creator auto-discovery is explicitly manual this cycle.)
</deferred>

---

*Phase: 95-measured-style-extractor*
*Context gathered: 2026-07-11*
