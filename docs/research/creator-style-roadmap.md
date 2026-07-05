# Cycle 16 — Creator-Style Deck Intelligence (proposed roadmap)

*Turns the Fable 5 research report (`creator-style-llm-system.md`) into a phased milestone. Grounded in existing DeckFlow services. Planning artifact only — not yet executed. Target tag: `2026.07.3` (after Cycle 15 `2026.07.2`).*

## Design stance (from report §6)

Prompt-artifact first, deterministic C# rubric scoring, **no autonomous agent**. Optional single-shot server critique as a flag-gated second tier. Everything flag-gated + namespaced (`creator.*`, `analysis.*`) per project convention. Byte-identical gate on prompt prose changes.

## Phase arc (7 phases, 3 waves)

| Phase | Name | Report § | Ships value? | Flag |
|---|---|---|---|---|
| **P87** | Style Profile Foundation | §3 data model | No (substrate) | — |
| **P88** | Measured-Style Extractor | §2 | No (feeds P90) | — |
| **P89** | Stated-Rules Distiller | §1 | No (feeds P90) | — |
| **P90** | Profile Fusion + Conflict Ledger | §3 | Admin-visible profile | — |
| **P91** | Card-Grounding Guard | §5 pitfall #1 | Cross-cutting safety | — |
| **P92** | Creator-Style Prompt Artifact | §4, §6B | **YES — $0 user-paste tool** | `creator.style-artifact` |
| **P93** | Optional Tier-2 In-App Critique | §4, §6B.6 | YES — in-app critique | `creator.style-critique` |

### Wave sequencing
- **Wave 1 (parallel):** P87 → then P88 + P89 concurrently (both consume P87's schema, independent of each other).
- **Wave 2 (parallel):** P90 (needs P88+P89) + P91 (independent, needs only Scryfall).
- **Wave 3 (serial):** P92 (needs P90 + P91) → P93 (needs P92).

---

## P87 — Style Profile Foundation

**Goal:** persisted schema for a creator style profile: a stated-rules ledger, a measured-style profile, and a fused profile with conflict list. Core lib + storage dialect (SQLite/Postgres), no UI.

**Requirements**
- CS-01: `CreatorStyleProfile` record set in `DeckFlow.Core/Knowledge/` — `StatedRule{category,targetMetric,targetValue,comparator,sourceClip,confidence}`, `MeasuredMetric{metric,value,numDecks,distribution}`, `FusedTarget{metric,value,weight,source,conflict?}`.
- CS-02: `ICreatorStyleProfileStore` + dialect-guarded DDL (mirror `ContentSiteIndexStore` migration pattern), keyed by creator source slug (reuse `SlugifySourceName`).
- CS-03: min-deck floor constant (report: EDHREC uses ≥5); profile marked `insufficient_sample` below it.
- CS-04: xUnit round-trip tests both dialects.

**Touchpoints:** `DeckFlow.Core/Knowledge/`, `Storage/` dialect. **Blast radius:** new files only.

---

## P88 — Measured-Style Extractor

**Goal:** from a creator's OWN decklists, compute the measured profile — category ratios, curve, pip balance, wincon type, combo density, characteristic high-lift cards.

**Requirements**
- CS-05: **staple-strip before stats** (report's single most important lesson) — drop ubiquitous lands/rocks; reuse/extend `ContentTagVocabulary`.
- CS-06: category tagging (ramp/removal/draw/wipe) via existing `CardCategoryRepository` + Scryfall Tagger oracle tags; count multi-category cards in each (Command Zone New Era rule).
- CS-07: **lift metric, not raw synergy** — `Pr(A∩B)/(Pr(A)·Pr(B))` from crawled deck history (`CategoryKnowledgeRepository`); demotes staples.
- CS-08: combo density via `CommanderSpellbookService.FindCombosAsync`.
- CS-09: Karsten land/curve consistency scoring (falsifiable targets).
- CS-10: emit `MeasuredMetric[]` with `numDecks` on every stat.

**DECK SOURCE (decided): crawl the creator's profile.** New profile-crawler discovers a creator's own decks from their Archidekt/Moxfield profile, then reuses existing `ArchidektApiDeckImporter`/`MoxfieldApiDeckImporter` to load each. Adds:
- CS-04a: `CreatorProfileDeckCrawler` — profile URL → list of deck IDs → loaded decks. Store creator→profile-URL mapping (extend `CreatorSourceStore`).
- CS-04b: rate-limit + resilience via existing Polly pipelines; respect upstream ToS; cache crawled deck sets (mirror `ArchidektDeckCacheSession`).
- CS-04c: attribution/dedup — de-dup near-precon lists (Precon Effect); tag each deck with confidence.
- ⚠ profile-listing endpoints differ from single-deck import — verify Archidekt/Moxfield expose a public profile/deck-list API before P88 plan; if not, fall back to manual URL list per creator.

**Touchpoints:** new `CreatorProfileDeckCrawler`, `CreatorSourceStore`, `Integration/*ApiDeckImporter`, `Knowledge/CategoryKnowledgeRepository`, `CardCategoryRepository`, Scryfall Tagger service, `CommanderSpellbookService`.

---

## P89 — Stated-Rules Distiller

**Goal:** transcripts → structured, measurable stated rules, each tied to a clip.

**Requirements**
- CS-11: map-reduce hierarchical chunking over creator transcripts (extends existing distill pipeline).
- CS-12: Claimify-style Select→Disambiguate→Decompose; drop irreducibly ambiguous statements.
- CS-13: strict JSON schema via constrained decoding — extend `DistillationSchemas`/`DistillationValidation`.
- CS-14: each rule carries `sourceClip` (KB pipeline already emits clips) + `confidence`.
- CS-15: reuse UTF-8 harness fix (`CliLlmDistillationService` — CP437 lesson); golden regression test on new schema.

**Touchpoints:** `Knowledge/DistillationSchemas.cs`, `DistillationValidation.cs`, `ContentArtifactSpec`, distill CLI.

---

## P90 — Profile Fusion + Conflict Ledger

**Goal:** reconcile stated (P89) + measured (P88) into one fused profile; the intellectual core.

**Requirements**
- CS-16: for each metric, compute conflict = measured outside stated band by threshold; record both numbers.
- CS-17: **weight toward measured** for observables (counts/curve/ratios); toward stated only for un-measurable philosophy.
- CS-18: encode fused profile as **weighted numeric targets, not prose** (models override conflicting prose with priors — report §3/[50]).
- CS-19: conflict ledger surfaced in Studio/admin (say-vs-do view — novel; report notes no published audit exists).
- CS-20: pure-Core, fully unit-tested (this is the rubric — deterministic, falsifiable).

**Touchpoints:** new `Core/Knowledge/StyleProfileFusion.cs`; admin/Studio read-only view.

---

## P91 — Card-Grounding Guard (report's #1 pitfall)

**Goal:** no hallucinated/illegal card ever ships in an artifact or critique.

**Requirements**
- CS-21: Scryfall `/cards/named?fuzzy=` validator — one match=ok, 404 ambiguous/none=reject; wrap existing Scryfall client + `ScryfallThrottle`.
- CS-22: **constrained-selection whitelist** builder — assemble legal real candidate cards; suggestions must pick from it (DeepMTG pattern).
- CS-23: singleton-legality + color-identity + castability checks.
- CS-24: reusable service consumed by P92 + P93; cache validated names.
- CS-25: tests incl. known-hallucination fixtures.

**Touchpoints:** `Services/Scryfall*`, new `CardGroundingGuard`. Cross-cutting.

---

## P92 — Creator-Style Prompt Artifact (primary deliverable, $0 operator cost)

**Goal:** new prompt-artifact tool: pick a creator, submit a deck, get a ChatGPT-ready packet critiquing it in that creator's style.

**Requirements**
- CS-26: new tool page + `CreatorStylePacketService` (mirror `DeckAnalysisPacketService`).
- CS-27: deterministic C# rubric scoring — diff submitted deck vs fused targets + Karsten math (no LLM).
- CS-28: artifact injects (a) fused profile as weighted numeric targets, (b) 2–3 real creator-deck exemplars, (c) validated synergy/combo context (P91), (d) rubric scores, (e) "critique only with provided cards" instruction.
- CS-29: all cards validated via P91 pre-ship.
- CS-30: flag `creator.style-artifact` (OFF, operator flips in prod).
- CS-31: full web-change bundle — xUnit + Playwright e2e desktop+mobile across themes; README; byte-identical prose gate.

**Touchpoints:** new controller/view/service; `PacketSessionCache`; prompt templates. **⚠ >5 files — full side-effects report at plan time.**

---

## P93 — Optional Tier-2 In-App Critique (flag OFF, metered) — ⏸ DEFERRED to fast-follow

*Out of Cycle 16 MVP scope (decided). Kept here as the next cycle's seed. pgvector package approval owed if/when built.*


**Goal:** single server-side LLM call returning a schema'd, cited, card-validated critique in-app.

**Requirements**
- CS-32: one call, Structured Outputs schema; each claim cites a rubric line + a real card.
- CS-33: re-validate every card in the response (P91) before display.
- CS-34: retrieval only if context > ~200k tokens — **pgvector on existing Render Postgres** (vectors server-side, NOT in 512MB web proc) + BM25/FTS5 for exact card names. Likely unneeded for v1.
- CS-35: `ILlmSpendLedger` accounting (exists); flag `creator.style-critique` OFF; no agent loop.
- CS-36: graceful degradation → fall back to P92 artifact on failure.

**Touchpoints:** new critique endpoint; `LlmSpendLedger`; optional pgvector (`Microsoft.Extensions.VectorData` — new pkg = ASK first).

---

## Cross-phase guardrails (woven into every phase)
- Min-deck floor before trusting a creator profile (CS-03).
- Lift over raw synergy (CS-07).
- Weekly Scryfall bulk refresh; never let parametric card memory override snapshot.
- Confidence/`numDecks` shown next to every stat (CS-10).
- Persona = weighted targets + exemplars, never adjectives (anti-caricature).

## Decisions (locked)
1. ✅ **P88 decklist source = crawl the creator's profile** (Archidekt/Moxfield). New `CreatorProfileDeckCrawler`; verify profile-list API exists at P88 plan, else fall back to manual URL list.
2. ✅ **Scope = MVP P87–P92**, tag `2026.07.3`. P93 deferred to fast-follow.
3. P93 pgvector package approval — deferred with P93.

## Still open
- Which creators first? Salubrious Snail / RebellLily have most artifacts (85/149) — but P88 crawl needs their Archidekt/Moxfield profile URLs. Confirm a starter creator + profile URL at P87/P88 discuss.
- Verify Archidekt & Moxfield expose a public profile→deck-list endpoint (crawler feasibility).

## Scope (locked): MVP = P87–P92
Prompt-artifact, $0 operator cost, the core value. Matches report's "workflow-first, agent-never" recommendation. P93 = fast-follow next cycle.

---

## Plan-review adjustments (Codex gpt-5.4-high + folder finding + P89 prototype, 2026-07-05)

### P88 folder segmentation (NEW — from live Archidekt data)
Snail's 39 decks span 5 personal folders — **none patron** (all `owner=SalubriousSnail`, zero patron/review keywords; patron-reviewed decks live on patrons' accounts, not his): Current Decks (5), Secondary decks (5), Budget deck pool (10), Decks in consideration (7), Other (12). Pooling all 39 equally distorts the profile — Budget-pool skews cheap, In-consideration = WIP.
- **CS-04d (new):** capture `parentFolder` (id+name, in the Archidekt API) and **weight/segment the profile by folder** — down-weight Budget + In-consideration, prefer Current+Secondary as the canonical style. Improves on the prototype's equal-pool.

### P89 feasibility = YES (Fable prototype confirmed)
27 measurable stated-rules extracted from Snail's artifacts (37-42 lands, 8-14 removal, ≥8 counters, 3-5 wipes, copy-benchmark tables); fusion works and discriminates — the board-wipe underweight is an **agreement with his stated philosophy** (not hypocrisy), which is the differentiating signal. **Gaps P89 must close:**
- **CS-11a:** add a `stated_rules:` YAML block to the distill template (`{category, metric, value, comparator, condition, clip_ts}`) — near-free at distill time; retrofit existing artifacts via one re-distill pass.
- **CS-11b:** add `content_type:` frontmatter (`deckbuilding-theory|deck-tech|meta-commentary|gameplay`) — ~14% of artifacts have zero deckbuilding signal; gives a clean coverage denominator.
- **CS-16a (highest-risk modeling decision):** conditionality is first-class — rules are per-archetype/curve; the ledger needs `applies_when` or the fusion emits false deltas (the prototype hit this on the draw metric).
- **CS-11c:** rule provenance/recency — carry video date; the creator revises his own positions, newer supersedes.
- **Measured side is the WEAKER leg** (Archidekt labels sparse) — P88 must lean on oracle/name classifiers (harvested store + CS-06), and **filter the 19 >105-card maybeboard-contaminated decks** before computing per-deck ratios.

### Codex adjustments (fold into phases)
- **HIGH — Moxfield is NOT an MVP source.** Repo README documents Moxfield blocks datacenter IPs; the importer falls back to Commander Spellbook which drops printings/tags/sideboards — fine for deck loading, wrong for a style corpus. → **Archidekt-only MVP crawler**, manual deck-list fallback; Moxfield = separate hardening phase. (Matches the multi-creator finding: RebellLily/Baumi have no clean auto-resolvable Archidekt profile anyway — creator→profile is manual.)
- **HIGH — Core-vs-Web layering.** The measured extractor wants `CommanderSpellbookService` + `ScryfallTaggerLookupService`, but both live in the Web host; P87/P90 are Core-centric. → add a host-abstraction phase OR keep the extractor in Web behind a narrow Core contract. Resolve before P88 plan.
- **HIGH — P92 is ≥2 phases.** "Mirror `DeckAnalysisPacketService`" = flag seeding + tool registry + cache-bypass for prompt-mutating flags + packet persistence + controller/view/help + big regression surface. → split **P92a artifact engine** + **P92b tool surface + cache/flag integration**.
- **MED — `CreatorSourceStore` is the wrong shape** for profile URLs (single normalized `channel_ref`, not multi-platform identity). → **new creator-profile-source table** (slug + platform + profile URL) or anchor off `content_sources`. Supersedes CS-04a's "extend CreatorSourceStore".
- **MED — card grounding earlier.** Distillation validates schema/tags only, not card names; the summary prompt preserves uncertain names. → a minimal Scryfall grounding pass into **P89/P90**, not just P92/P91.
- **MED — P88's real work is canonicalization.** The existing `ContentTagVocabulary` allowlist is narrow and a different vocabulary than the prototype uses. → make the category bucket-mapping an explicit prereq before trusting measured ratios.
- **LOW — P93 is web-side net-new.** `ILlmSpendLedger` isn't registered in Web; no vector packages in either csproj. Keep deferred; write the web-side spend/infra as net-new, not fast-follow glue.

### Verdict
Cycle 16 has **no hard code dependency on Cycle 17**, but should follow it — creator-style builds on the KB substrate Cycle 17 stabilizes. Prep work allowed in parallel: Archidekt-only crawler spike + P89 schema/rule-extraction design (both already prototyped here).
