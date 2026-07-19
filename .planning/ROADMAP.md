# Roadmap: DeckFlow

## Milestones

- [ACTIVE] **Cycle 17 - Creator-Style Deck Intelligence** - Phases 94-100 (started 2026-07-11)
- [SHIPPED] **Cycle 16 - Content-KB Prod<->Git<->Studio Sync Hardening** - Phases 88-93 (shipped 2026-07-11, `2026.07.3`) - see `.planning/milestones/cycle16-ROADMAP.md`
- [SHIPPED] **2026.07.2 Cycle 15 - Cleanup, Refactor & Visual Polish** - Phases 82-87 (shipped 2026-07-05) - see `.planning/milestones/2026.07.2-ROADMAP.md`
- [SHIPPED] **Cycle 14 - Deeper Deck Evaluation** - Phases 79-81 (shipped 2026-07-03, `2026.07.1`) - see `.planning/milestones/cycle14-ROADMAP.md`
- [SHIPPED] **Cycle 13 - Deck Evaluation & Creator Output** - Phases 75-78 (shipped 2026-06-30, `2026.06.10`) - see `.planning/milestones/cycle13-ROADMAP.md`
- [SHIPPED] **Cycle 12 - Manabase Accuracy, Command-Zone Awareness & Cross-Tool Persistence** - Phases 70-74 + flag-key namespacing (shipped 2026-06-27, `2026.06.9`)
- [SHIPPED] **Cycle 11 - Security, Visibility Control & Creator-Lens** - Phases 64-69 (shipped 2026-06-25, `2026.06.8`) - see `.planning/milestones/cycle11-ROADMAP.md`
- [SHIPPED] **Cycle 10 - Studio Automation, Sync & Polish** - Phases 59-63 (shipped 2026-06-21, `2026.06.6`) - see `.planning/milestones/cycle10-ROADMAP.md`
- [SHIPPED] **Cycle 9 - Content Pipeline & Publish-Tracking** - Phases 55-58 (shipped 2026-06-19, `2026.06.5`) - see `.planning/milestones/cycle9-ROADMAP.md`
- [SHIPPED] **Cycle 8 - Hardening & Backlog Burn-down** - Phases 51-54 (shipped 2026-06-17, `2026.06.4`) - see `.planning/milestones/cycle8-ROADMAP.md`
- [SHIPPED] **v1.7 Local Harvest & Publish Studio** - Phases 41-50 (shipped 2026-06-17) - see `.planning/milestones/v1.7-ROADMAP.md`
- [SHIPPED] **v1.6 Content KB Retrieval Fix + Value Re-Validation** - Phases 34-40 (shipped 2026-06-12) - see `.planning/milestones/v1.6-ROADMAP.md`
- [SHIPPED] **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** - Phases 28-33 (shipped 2026-06-10) - see `.planning/milestones/v1.5-ROADMAP.md`
- [SHIPPED] **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** - Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) - see `.planning/milestones/v1.4-ROADMAP.md`
- [SHIPPED] **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** - Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) - see `.planning/milestones/v1.3-ROADMAP.md`
- [SHIPPED] **v1.2 Multi-AI Prompts** - Phases 9-10 (shipped 2026-05-13) - see `.planning/milestones/v1.2-ROADMAP.md`
- [SHIPPED] **v1.1 Admin Console** - Phases 6-8 (shipped 2026-05-08)
- [SHIPPED] **v1.0 Polish & Quality** - Phases 1-5 (shipped 2026-05-02) - see `.planning/milestones/v1.0-ROADMAP.md`

## Design Stance (Cycle 17)

Prompt-artifact-first, deterministic C# rubric scoring, **no autonomous agent**. Persona is encoded as weighted numeric targets + real exemplars, never adjectives (anti-caricature). Everything flag-gated and namespaced `creator.*`. Byte-identical gate on any existing prompt-prose change. Min-deck floor before trusting a profile; lift-over-raw-synergy; `numDecks`/confidence next to every stat.

**Granularity note:** `config.json` sets `granularity: coarse` (typically 3-5 phases). Cycle 17 has 7 because two hard constraints from the Codex gpt-5.4-high plan-review override the compression guidance: (1) the Creator-Style Prompt Artifact is mandated as **at least 2 phases** (artifact engine vs. tool surface — HIGH finding), and (2) the wave-sequenced dependency chain (Foundation -> {Extractor, Distiller} -> {Fusion, Guard} -> Artifact Engine -> Tool Surface) is itself 6 natural delivery boundaries before the mandatory split. The phase count reflects the locked arc in `docs/research/creator-style-roadmap.md`, not arbitrary subdivision.

**Core-vs-Web layering (Codex HIGH, resolved):** rather than adding a dedicated host-abstraction phase, Phase 95 (Measured-Style Extractor) explicitly lives in `DeckFlow.Web` — it needs `CommanderSpellbookService` and the Scryfall Tagger service, both Web-host services — behind a narrow contract so the extraction algorithm's pure-logic pieces stay Core-testable. Phases 94/96/97/98 stay Core-centric per the original arc.

## Phases

**Phase Numbering:**
- Integer phases (94, 95, ...): Planned milestone work
- Decimal phases (94.1, 94.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order. Numbering continues from Cycle 16's Phase 93.

- [x] **Phase 94: Style-Profile Foundation** - Persisted creator-style schema (stated/measured/fused) + dialect-guarded store; substrate only.
- [ ] **Phase 95: Measured-Style Extractor** - Compute a creator's measured style from their own Archidekt decks (staple-stripped, lift-weighted, folder-segmented); substrate only.
- [x] **Phase 96: Stated-Rules Distiller** - Transcripts to structured, clip-tied stated rules; substrate only.
- [ ] **Phase 97: Profile Fusion + Conflict Ledger** - Reconcile stated vs measured into weighted numeric targets + say-vs-do ledger; admin-visible, no public UI.
- [x] **Phase 98: Card-Grounding Guard** - Reusable Scryfall validator + constrained-selection whitelist so no hallucinated/illegal card ever ships; cross-cutting substrate.
- [ ] **Phase 99: Creator-Style Artifact Engine** - Deterministic C# rubric diffing a submitted deck vs fused targets + assembled artifact content; no LLM call, no page yet.
- [ ] **Phase 100: Creator-Style Tool Surface** - The $0 paste-ready tool page/controller, flag `creator.style-artifact` (OFF), full web bundle. **Only phase that ships user-visible value.**

<details>
<summary>Cycle 16 (Phases 88-93) - SHIPPED 2026-07-11 (2026.07.3)</summary>

- [x] Phase 88 - Index-Row Integrity Hotfix
- [x] Phase 89 - Content-Hash Foundation
- [x] Phase 90 - DirectPush Correctness + Seed Sync (flag sync.directpush-gitbody)
- [x] Phase 91 - Reconcile + Seed Lifecycle (flag sync.reconcile)
- [x] Phase 92 - Pull Hardening
- [x] Phase 93 - Round-Trip Integration Test

Full details: .planning/milestones/cycle16-ROADMAP.md

</details>

<details>
<summary>2026.07.2 Cycle 15 (Phases 82-87) - SHIPPED 2026-07-05</summary>

- [x] Phase 82 - Refactor-Review Sweep & UI Baseline Audit (completed 2026-07-04)
- [x] Phase 83 - Packet-Service SRP Split (completed 2026-07-04)
- [x] Phase 84 - Theme Semantic-Token Migration (completed 2026-07-05)
- [x] Phase 85 - `chatgpt-*` Naming Cleanup (completed 2026-07-05)
- [x] Phase 86 - UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout (completed 2026-07-05)
- [x] Phase 87 - Creator-Source Model Hardening (completed 2026-07-05)

</details>

## Phase Details

### Phase 94: Style-Profile Foundation
**Goal**: Persist a creator style profile schema — stated rules, measured metrics, fused targets — behind a dialect-guarded store keyed by creator slug. Pure substrate: no UI, no crawler, no distiller yet. Unlocks every downstream phase in this cycle.
**Depends on**: Nothing (first phase of Cycle 17)
**Requirements**: CS-01, CS-02, CS-03, CS-04
**Success Criteria** (what must be TRUE):
  1. `CreatorStyleProfile` / `StatedRule{category,targetMetric,targetValue,comparator,sourceClip,confidence}` / `MeasuredMetric{metric,value,numDecks,distribution}` / `FusedTarget{metric,value,weight,source,conflict?}` record types exist in `DeckFlow.Core/Knowledge/` with exactly the fields specified in CS-01.
  2. `ICreatorStyleProfileStore` persists and retrieves a profile keyed by creator slug (reusing `SlugifySourceName`) on both SQLite and Postgres dialects, mirroring the `ContentSiteIndexStore` migration pattern.
  3. A profile backed by fewer decks than the min-deck-floor constant is marked `insufficient_sample` rather than silently trusted.
  4. xUnit round-trip tests pass on both dialects, proving write-then-read fidelity of the full profile shape.
**Plans**: 3 plans
- [x] 94-01-PLAN.md — CS-01 record set (StatedRule/MeasuredMetric/FusedTarget) + MinDeckFloor const + JSON-section helpers
- [x] 94-02-PLAN.md — CS-02 ICreatorStyleProfileStore + dialect-guarded creator_style_profile DDL/UPSERT (mirror ContentSiteIndexStore)
- [x] 94-03-PLAN.md — CS-04 xUnit round-trip tests (SQLite unconditional + gated Postgres via Testcontainers)

### Phase 95: Measured-Style Extractor
**Goal**: Compute a creator's measured style profile from their OWN Archidekt decklists — staple-stripped, lift-weighted (not raw synergy), folder-segmented, every stat carrying `numDecks`. Substrate only (feeds Phase 97); no user-visible surface. Lives in `DeckFlow.Web` (needs `CommanderSpellbookService` + the Scryfall Tagger service, both Web-host services) behind a narrow contract so the extraction algorithm's pure logic stays testable independent of the host — the Codex-flagged Core-vs-Web layering resolution for this cycle.
**Depends on**: Phase 94 (consumes the profile schema + store)
**Requirements**: CS-04a, CS-04b, CS-04c, CS-04d, CS-05, CS-06, CS-07, CS-08, CS-09, CS-10
**Success Criteria** (what must be TRUE):
  1. `CreatorProfileDeckCrawler` resolves a creator's Archidekt profile URL to a deck-ID list and loads each deck via the existing `ArchidektApiDeckImporter` — or, if Archidekt exposes no public profile-to-deck-list endpoint, falls back to an explicit manual per-creator URL list (verified at plan time).
  2. Crawled decks are folder-segmented and weighted (Current/Secondary favored over Budget/In-consideration pools), staple-stripped before any ratio is computed, and >105-card maybeboard-contaminated decks are filtered out first.
  3. Emitted `MeasuredMetric[]` use a lift metric (`Pr(A∩B)/(Pr(A)·Pr(B))`, not raw co-occurrence) for synergy, include Commander-Spellbook combo density and Karsten curve/land-consistency scoring, and every stat carries `numDecks`.
  4. Category tagging reuses `CardCategoryRepository` + Scryfall Tagger oracle tags and counts multi-category cards in every bucket they qualify for (not just their first match).
  5. Crawling reuses the existing Polly resilience pipelines and a cached deck set (mirroring `ArchidektDeckCacheSession`) so re-running against the same creator does not re-hit Archidekt.
**Plans**: 7 plans
- [x] 95-01-PLAN.md — Nested EffectiveSampleSize field (D-10) + dialect-guarded creator_profile_source store (CS-04a, CS-10)
- [x] 95-02-PLAN.md — Creator-scoped deck cache store, content-hash freshness, no corpus pollution (CS-04b)
- [x] 95-03-PLAN.md — ContentTagVocabulary.Staples set + server-side global lift-baseline aggregate (CS-05, CS-07)
- [x] 95-04-PLAN.md — Pure extraction contract + StapleStripper + FolderWeighting (CS-05, CS-04c, CS-04d)
- [x] 95-05-PLAN.md — Pure CategoryCounter (multi-bucket) + LiftCalculator (CS-06, CS-07)
- [x] 95-06-PLAN.md — CreatorProfileDeckCrawler: ownerUsername crawl, SSRF guard, cache read-through (CS-04a/b/c/d)
- [x] 95-07-PLAN.md — MeasuredStyleProfileBuilder: Karsten+combo+category -> MeasuredMetric[] persisted (CS-06/08/09/10)

### Phase 96: Stated-Rules Distiller
**Goal**: Turn a creator's transcripts into structured, measurable stated rules — each tied to a clip, a confidence, and a recency date so a creator's later self supersedes an earlier one. Substrate only (feeds Phase 97).
**Depends on**: Phase 94 (consumes the `StatedRule` schema); independent of Phase 95 — both can execute in parallel.
**Requirements**: CS-11, CS-11a, CS-11b, CS-11c, CS-12, CS-13, CS-14, CS-15
**Success Criteria** (what must be TRUE):
  1. The distill template emits a `stated_rules:` YAML block (`{category, metric, value, comparator, condition, clip_ts}`) plus a `content_type:` frontmatter field (`deckbuilding-theory|deck-tech|meta-commentary|gameplay`); a single re-distill pass populates both on existing artifacts.
  2. Map-reduce hierarchical chunking plus Claimify-style Select->Disambiguate->Decompose over a real creator transcript yields measurable rules and discards irreducibly ambiguous statements.
  3. Every extracted rule validates against a strict JSON schema (constrained decoding, extending `DistillationSchemas`/`DistillationValidation`) and carries `sourceClip`, `confidence`, and the source video's date (recency/provenance).
  4. A minimal Scryfall card-name grounding pass flags or rejects unrecognized card names inside distilled rules, ahead of the full Phase 98 guard.
  5. A golden regression test on the new schema passes using the existing UTF-8-safe harness (the `CliLlmDistillationService` CP437 lesson).
**Plans**: 8 plans
- [x] 96-01-PLAN.md — StatedRuleCandidate DTO + closed metric vocabulary + deterministic reducer
- [x] 96-02-PLAN.md — TranscriptChunker + content_type heuristic + ICardNameGrounder seam
- [x] 96-03-PLAN.md — 4 Claimify schemas/prompts + ValidateStatedRules/SanitizeStatedRules
- [x] 96-04-PLAN.md — CLI Select/Disambiguate/Decompose/Reduce stage methods (UTF-8 harness)
- [x] 96-05-PLAN.md — content_stated_rules table + additive content_type:/stated_rules: frontmatter
- [x] 96-06-PLAN.md — Web ScryfallCardNameGrounder (cached fuzzy grounding)
- [x] 96-07-PLAN.md — StatedRulesExtractor multi-pass coordinator + D-06 Snail golden test
- [x] 96-08-PLAN.md — Orchestrator wiring: content_type + extractor + persist + emit

### Phase 97: Profile Fusion + Conflict Ledger
**Goal**: Reconcile stated (Phase 96) vs measured (Phase 95) into one fused profile of weighted numeric targets, with a say-vs-do conflict ledger. The deterministic rubric's intellectual core. Admin/Studio-visible ledger; still no public UI.
**Depends on**: Phase 95 (measured input) and Phase 96 (stated input)
**Requirements**: CS-16, CS-16a, CS-17, CS-18, CS-19, CS-20
**Success Criteria** (what must be TRUE):
  1. For each metric, a conflict is computed whenever the measured value falls outside the stated band by a defined threshold, and both the stated and measured numbers are retained on the resulting `FusedTarget`.
  2. Fusion weights toward measured for observable metrics (counts/curve/ratios) and toward stated only for philosophy that cannot be measured.
  3. Rules carry `applies_when` conditionality so a per-archetype or per-curve rule does not produce a false conflict against an unconditional aggregate (CS-16a, the highest-risk modeling decision this cycle).
  4. The fused profile is encoded as weighted numeric targets — never prose — and the conflict ledger is viewable read-only in Studio/admin.
  5. Fusion logic lives entirely in `DeckFlow.Core`, has zero Web/HTTP dependency, and is fully unit-tested (deterministic, falsifiable).
**Plans**: 7 plans
- [ ] 97-01-PLAN.md — Additive FusedTarget/FusedConflict schema extension + P94 round-trip guard (CS-18, CS-16)
- [ ] 97-02-PLAN.md — StatedMetricKeyMapper + observable/philosophy MetricClassification (CS-16a, CS-17)
- [ ] 97-03-PLAN.md — Stated-rules read path GetStatedRulesBySourceSlugAsync (CS-16a)
- [ ] 97-04-PLAN.md — StatedRuleRecencyCollapser + ConflictCalculator, prototype-grounded goldens (CS-16, CS-16a)
- [ ] 97-05-PLAN.md — ProfileFusionEngine deterministic (metric,condition) join (CS-16a, CS-17, CS-20)
- [ ] 97-06-PLAN.md — CLI fuse-profile trigger: read -> fuse -> persist (CS-16, CS-20)
- [ ] 97-07-PLAN.md — Read-only Studio say-vs-do ledger page + DI + nav (CS-19, CS-18)

### Phase 98: Card-Grounding Guard
**Goal**: Guarantee no hallucinated or illegal card ever ships in a creator-style artifact or critique — a single reusable, cached validation service. Cross-cutting substrate; independent of Fusion, needs only Scryfall, so it can execute in parallel with Phases 95-97.
**Depends on**: Nothing internal to this milestone beyond the existing Scryfall client + `ScryfallThrottle`
**Requirements**: CS-21, CS-22, CS-23, CS-24, CS-25
**Success Criteria** (what must be TRUE):
  1. A Scryfall `/cards/named?fuzzy=` validator returns ok for exactly one match and rejects 404/ambiguous/none.
  2. A constrained-selection whitelist builder assembles only legal, real candidate cards for any downstream suggestion surface to pick from (never a free-text card name).
  3. Singleton-legality, color-identity, and castability checks reject any suggestion that fails them.
  4. The guard is one reusable, cached service (not duplicated per caller), consumed by Phase 99, with tests including known-hallucination fixtures that previously would have shipped a fake or illegal card.
**Plans**: 4 plans
- [x] 98-01-PLAN.md — Core contracts + pure legality/identity/singleton/castability rules (CS-23)
- [x] 98-02-PLAN.md — Strict CardGroundingGuard + Legalities/ScryfallErrorResponse DTOs + DI (CS-21, CS-24)
- [x] 98-03-PLAN.md — CreatorWhitelistPoolBuilder: corpus-only, frequency-ranked, guard-validated (CS-22)
- [x] 98-04-PLAN.md — Known-hallucination fixture regression suite (CS-25)

### Phase 99: Creator-Style Artifact Engine
**Goal**: A deterministic C# rubric that scores a submitted deck against a creator's fused profile and assembles the artifact content to inject — no LLM call, no user-facing page yet. This is the "engine" half of the Codex-mandated 2-phase split of the prompt-artifact deliverable.
**Depends on**: Phase 97 (Fusion) and Phase 98 (Guard)
**Requirements**: CS-26, CS-27, CS-28, CS-29
**Success Criteria** (what must be TRUE):
  1. `CreatorStylePacketService` (mirroring `DeckAnalysisPacketService`'s shape) deterministically diffs a submitted deck against the creator's fused targets plus Karsten math, with zero LLM calls anywhere in the path.
  2. Assembled artifact content includes all five required elements: fused profile as weighted numeric targets, 2-3 real creator-deck exemplars, validated synergy/combo context, rubric scores, and a "critique only with the provided cards" instruction.
  3. Every card referenced anywhere in the assembled content has passed the Phase 98 card-grounding guard before assembly returns.
  4. xUnit tests cover rubric scoring and artifact assembly in isolation, with no controller or page dependency.
**Plans**: 3 plans
- [x] 99-01-PLAN.md — Pure Core rubric scorer + models + exemplar-deck selector (CS-27, CS-28)
- [x] 99-02-PLAN.md — SubmittedDeckStatsBuilder: apples-to-apples deck stats + card-grounding deck context (CS-27, CS-29)
- [x] 99-03-PLAN.md — CreatorStylePacketService: 5-element artifact assembly + fail-closed guard gate + DI tripwire (CS-26, CS-28, CS-29)

### Phase 100: Creator-Style Tool Surface
**Goal**: Ship the $0 paste-ready Creator-Style tool end-to-end — new page, controller, flag `creator.style-artifact` (seeded OFF), packet-cache-bypass wiring, and the full web-change bundle. **The only phase in this milestone that ships user-visible value.**
**Depends on**: Phase 99 (Artifact Engine)
**Requirements**: CS-30, CS-31
**Success Criteria** (what must be TRUE):
  1. With `creator.style-artifact` ON, a user can pick a creator, submit a deck, and receive a ChatGPT-ready critique packet in one round-trip on a new tool page.
  2. Flag `creator.style-artifact` is seeded OFF on both dialects at ship; toggling it changes only whether the tool is reachable — every existing artifact stays byte-identical.
  3. The flag is registered in the packet prompt-mutating cache-bypass set, so a stale cached packet can never be served across a flag flip.
  4. xUnit + Playwright e2e (desktop + mobile, across themes) cover the new page/controller; README documents the new workflow; the byte-identical prose gate passes on every pre-existing artifact.
**Plans**: TBD
**UI hint**: yes

## Progress

**Execution Order (Cycle 17):**
Phase 94 -> {Phase 95, Phase 96 in parallel} -> {Phase 97 (needs 95+96), Phase 98 (independent, can run anytime after/parallel to 94)} -> Phase 99 (needs 97+98) -> Phase 100 (needs 99)

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|-----------------|--------|-----------|
| 94. Style-Profile Foundation | Cycle 17 | 0/0 | Not started | - |
| 95. Measured-Style Extractor | Cycle 17 | 7/7 | Complete   | 2026-07-12 |
| 96. Stated-Rules Distiller | Cycle 17 | 8/8 | Complete   | 2026-07-12 |
| 97. Profile Fusion + Conflict Ledger | Cycle 17 | 0/7 | Not started | - |
| 98. Card-Grounding Guard | Cycle 17 | 0/4 | Not started | - |
| 99. Creator-Style Artifact Engine | Cycle 17 | 3/3 | Complete    | 2026-07-19 |
| 100. Creator-Style Tool Surface | Cycle 17 | 0/0 | Not started | - |

Cycle 16 progress (Phases 82-93): see `.planning/milestones/cycle16-ROADMAP.md` and `.planning/milestones/2026.07.2-ROADMAP.md`.

---

## Carry-forward backlog (not in Cycle 17)

- **Tier-2 in-app LLM critique (CS-32..36)** — deferred to fast-follow; needs new-package approval (pgvector / `Microsoft.Extensions.VectorData`) and net-new web-side spend/vector infra. Seeded in `docs/research/creator-style-roadmap.md` as the next phase after this milestone (was "P93" in the stale doc numbering).
- **Moxfield as a corpus source** — Moxfield blocks datacenter IPs; a Moxfield crawler is a separate later hardening phase. MVP crawler is Archidekt-only.
- **Multi-creator auto-resolution** — creator-to-profile stays manual per creator (no clean auto-resolvable profile for every creator).
- Scheduled/bulk harvest (AUTO-03/04)
- SEO/growth lane (SEO-01..05)
- Matchup / meta-threat read (deferred - deepens cedh-meta-gap, a separate lane)
- **ADMIN-01** - `/Admin/Flags` sortable by on/off (enabled) state (descoped from Cycle 15, user decision 2026-07-05; view-only, no flag semantics change)
- Manabase engine refactor (CastabilitySimulator / ManabaseAnalyzer / ManabaseClassifier SRP split) - deferred: behavior-critical Monte-Carlo + Karsten scoring, no byte-identical gate. Needs a numeric-parity harness built FIRST. Candidate for a dedicated future refactor cycle.
- **KB "commander advice" content class for filtered videos** - give general-commander-advice videos (meta essays, budget philosophy, card evaluations) a distinct KB content type/home instead of dropping them at distill time.
- **SYNC-F1** - Retire DirectPush entirely (fold into Publish) - later-cycle decision.
- **SYNC-F2** - Scheduled/automatic reconcile runs.
