# Requirements: DeckFlow — Cycle 17 (Creator-Style Deck Intelligence)

**Status:** DEFINING (milestone started 2026-07-11)
**Defined:** 2026-07-11
**Source:** `docs/research/creator-style-roadmap.md` (locked MVP scope) + Fable 5 report `docs/research/creator-style-llm-system.md`, incl. Codex gpt-5.4-high plan-review + P89 Fable prototype on real Archidekt data.

**Core Value:** Every supported workflow must produce output the user can paste
into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip. This
cycle adds a new such workflow: a $0 paste-ready packet that critiques a
submitted deck in a chosen creator's deckbuilding style.

**Design stance:** Prompt-artifact first, deterministic C# rubric scoring, **no
autonomous agent**. Persona = weighted numeric targets + real exemplars, never
adjectives (anti-caricature). Everything flag-gated + namespaced (`creator.*`),
byte-identical gate on prompt-prose changes. Min-deck floor before trusting a
profile; lift over raw synergy; confidence/`numDecks` next to every stat.

## Cycle 17 Requirements (MVP)

### Style-Profile Foundation
<!-- Core lib + storage dialect, no UI. Substrate for the whole cycle. -->

- [ ] **CS-01**: `CreatorStyleProfile` record set in `DeckFlow.Core/Knowledge/` — `StatedRule{category,targetMetric,targetValue,comparator,sourceClip,confidence}`, `MeasuredMetric{metric,value,numDecks,distribution}`, `FusedTarget{metric,value,weight,source,conflict?}`.
- [ ] **CS-02**: `ICreatorStyleProfileStore` + dialect-guarded DDL (mirror `ContentSiteIndexStore` migration), keyed by creator source slug (reuse `SlugifySourceName`).
- [ ] **CS-03**: min-deck floor constant (report: EDHREC uses ≥5); profile marked `insufficient_sample` below it.
- [ ] **CS-04**: xUnit round-trip tests, both dialects.

### Measured-Style Extractor (Archidekt-only MVP)
<!-- Compute the measured profile from the creator's OWN decklists. -->

- [ ] **CS-04a**: `CreatorProfileDeckCrawler` — Archidekt profile URL → deck IDs → decks loaded via existing `ArchidektApiDeckImporter`. Creator→profile-URL mapping stored in a **new creator-profile-source table** (slug + platform + profile URL), NOT `CreatorSourceStore` (Codex MED — wrong shape). Verify Archidekt exposes a public profile→deck-list endpoint at plan time; else fall back to a manual per-creator URL list.
- [ ] **CS-04b**: rate-limit + resilience via existing Polly pipelines; respect upstream ToS; cache crawled deck sets (mirror `ArchidektDeckCacheSession`).
- [ ] **CS-04c**: attribution/dedup — de-dup near-precon lists (Precon Effect); tag each deck with confidence; filter maybeboard-contaminated (>105-card) decks before per-deck ratios.
- [ ] **CS-04d**: capture `parentFolder` (id+name from Archidekt API) and **weight/segment the profile by folder** — down-weight Budget + In-consideration pools, prefer Current+Secondary as canonical style.
- [ ] **CS-05**: **staple-strip before stats** (the report's single most important lesson) — drop ubiquitous lands/rocks; reuse/extend `ContentTagVocabulary`. Category bucket-mapping canonicalization is an explicit prereq before trusting measured ratios (Codex MED).
- [ ] **CS-06**: category tagging (ramp/removal/draw/wipe) via `CardCategoryRepository` + Scryfall Tagger oracle tags; count multi-category cards in each bucket (Command Zone New Era rule).
- [ ] **CS-07**: **lift metric, not raw synergy** — `Pr(A∩B)/(Pr(A)·Pr(B))` from crawled deck history (`CategoryKnowledgeRepository`); demotes staples.
- [ ] **CS-08**: combo density via `CommanderSpellbookService.FindCombosAsync`.
- [ ] **CS-09**: Karsten land/curve consistency scoring (falsifiable targets).
- [ ] **CS-10**: emit `MeasuredMetric[]` with `numDecks` on every stat.

### Stated-Rules Distiller
<!-- Transcripts → structured, measurable stated rules, each tied to a clip. -->

- [ ] **CS-11**: map-reduce hierarchical chunking over creator transcripts (extends the existing distill pipeline).
- [ ] **CS-11a**: add a `stated_rules:` YAML block to the distill template (`{category, metric, value, comparator, condition, clip_ts}`); retrofit existing artifacts via one re-distill pass.
- [ ] **CS-11b**: add `content_type:` frontmatter (`deckbuilding-theory|deck-tech|meta-commentary|gameplay`) — gives a clean coverage denominator (~14% of artifacts have zero deckbuilding signal).
- [ ] **CS-11c**: rule provenance/recency — carry video date; newer positions supersede older (the creator revises himself).
- [ ] **CS-12**: Claimify-style Select→Disambiguate→Decompose; drop irreducibly ambiguous statements.
- [ ] **CS-13**: strict JSON schema via constrained decoding — extend `DistillationSchemas` / `DistillationValidation`.
- [ ] **CS-14**: each rule carries `sourceClip` (KB pipeline already emits clips) + `confidence`.
- [ ] **CS-15**: reuse the UTF-8 harness fix (`CliLlmDistillationService` — CP437 lesson); golden regression test on the new schema. Add a minimal Scryfall card-name grounding pass here (Codex MED — ground earlier than P92).

### Profile Fusion + Conflict Ledger
<!-- Reconcile stated (distiller) + measured (extractor) into one profile. The intellectual core. -->

- [ ] **CS-16**: for each metric, compute conflict = measured outside the stated band by threshold; record both numbers.
- [ ] **CS-16a**: conditionality is first-class — rules are per-archetype/curve; the ledger carries `applies_when` or fusion emits false deltas (prototype hit this on the draw metric). **Highest-risk modeling decision.**
- [ ] **CS-17**: **weight toward measured** for observables (counts/curve/ratios); toward stated only for un-measurable philosophy.
- [ ] **CS-18**: encode the fused profile as **weighted numeric targets, not prose** (models override conflicting prose with priors).
- [ ] **CS-19**: conflict ledger surfaced in Studio/admin (say-vs-do view — novel; no published audit exists).
- [ ] **CS-20**: pure-Core, fully unit-tested (this is the rubric — deterministic, falsifiable).

### Card-Grounding Guard (the report's #1 pitfall)
<!-- No hallucinated/illegal card ever ships in an artifact or critique. -->

- [ ] **CS-21**: Scryfall `/cards/named?fuzzy=` validator — one match = ok, 404/ambiguous/none = reject; wrap the existing Scryfall client + `ScryfallThrottle`.
- [ ] **CS-22**: **constrained-selection whitelist** builder — assemble legal real candidate cards; suggestions must pick from it (DeepMTG pattern).
- [ ] **CS-23**: singleton-legality + color-identity + castability checks.
- [ ] **CS-24**: reusable service consumed by the artifact tool (and future critique); cache validated names.
- [ ] **CS-25**: tests incl. known-hallucination fixtures.

### Creator-Style Prompt Artifact (flag `creator.style-artifact`, OFF) — primary $0 deliverable
<!-- Codex HIGH: this is ≥2 phases — split artifact engine vs tool surface/cache/flag at roadmap time. -->

- [ ] **CS-26**: new tool page + `CreatorStylePacketService` (mirror `DeckAnalysisPacketService`).
- [ ] **CS-27**: deterministic C# rubric scoring — diff submitted deck vs fused targets + Karsten math (no LLM).
- [ ] **CS-28**: artifact injects (a) fused profile as weighted numeric targets, (b) 2–3 real creator-deck exemplars, (c) validated synergy/combo context (CS-21..25), (d) rubric scores, (e) "critique only with the provided cards" instruction.
- [ ] **CS-29**: all cards validated via the card-grounding guard pre-ship.
- [ ] **CS-30**: flag `creator.style-artifact` seeded OFF (both dialects); operator flips in prod. Prompt-mutating flag → wire into the packet cache-bypass set.
- [ ] **CS-31**: full web-change bundle — xUnit + Playwright e2e desktop+mobile across themes; README; byte-identical prose gate.

## Traceability

<!-- Filled by the roadmapper: every CS-* mapped to exactly one phase. -->

| Requirement | Phase | Status |
|-------------|-------|--------|
| CS-01..31 (+ CS-04a..d, CS-11a..c, CS-16a) | TBD | Pending roadmap |

**Coverage:** 39 requirements defined, mapping pending roadmap.

## Cross-Phase Guardrails (woven into every phase)

- Min-deck floor before trusting a creator profile (CS-03).
- Lift over raw synergy (CS-07).
- Weekly Scryfall bulk refresh; never let parametric card memory override the snapshot.
- Confidence/`numDecks` shown next to every stat (CS-10).
- Persona = weighted targets + exemplars, never adjectives (anti-caricature).
- **Core-vs-Web layering (Codex HIGH):** the measured extractor wants `CommanderSpellbookService` + Scryfall Tagger, both Web-host services, while P87/P90 are Core-centric. Resolve before the extractor plan — host-abstraction phase OR keep the extractor in Web behind a narrow Core contract.

## Future Requirements (deferred — fast-follow next cycle)

**P93 — Optional Tier-2 In-App Critique** (flag `creator.style-critique`, OFF, metered):

- **CS-32**: one server-side LLM call, Structured Outputs schema; each claim cites a rubric line + a real card.
- **CS-33**: re-validate every card in the response (CS-21..25) before display.
- **CS-34**: retrieval only if context > ~200k tokens — pgvector on Render Postgres (vectors server-side, NOT in the 512MB web proc) + BM25/FTS5 for exact card names. Likely unneeded for v1.
- **CS-35**: `ILlmSpendLedger` accounting (exists in Core; NOT registered in Web — net-new web-side infra); flag OFF; no agent loop.
- **CS-36**: graceful degradation → fall back to the CS-26 artifact on failure.

## Out of Scope (this cycle, with reasoning)

- **Tier-2 in-app LLM critique (CS-32..36)** — deferred to fast-follow; needs new-package approval (pgvector / `Microsoft.Extensions.VectorData`) and net-new web-side spend/vector infra.
- **Moxfield as a corpus source** — Codex HIGH: Moxfield blocks datacenter IPs; the importer falls back to Commander Spellbook which drops printings/tags/sideboards (fine for deck loading, wrong for a style corpus). Moxfield crawler = separate later hardening phase. MVP crawler is Archidekt-only.
- **Multi-creator auto-resolution** — creator→profile stays manual per creator (RebellLily/Baumi have no clean auto-resolvable Archidekt profile).
- **Autonomous agent / tool-use loop** — the report's explicit recommendation is workflow-first, agent-never.

---
*Requirements defined 2026-07-11; phase mapping pending the Cycle 17 roadmap (phases continue from 93 → 94+).*
