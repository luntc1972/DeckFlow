# Roadmap: DeckFlow

## Milestones

- [ACTIVE] **Cycle 20 - Personal Tools** - Phases 112-115 (started 2026-07-24) - see Phase Details below
- [SHIPPED] **Cycle 19 - Cut Lab Upgrade Hardening** - Phases 108-111 (shipped 2026-07-24, `2026.07.9`) - see `.planning/milestones/cycle19-ROADMAP.md`
- [SHIPPED] **Cycle 16 - Content-KB Prod<->Git<->Studio Sync Hardening** - Phases 88-93 (shipped 2026-07-11, `2026.07.3`) - see `.planning/milestones/cycle16-ROADMAP.md`
- â **2026.07.2 Cycle 15 â Cleanup, Refactor & Visual Polish** â Phases 82â87 (shipped 2026-07-05) â see .planning/milestones/2026.07.2-ROADMAP.md
- â **Cycle 14 â Deeper Deck Evaluation** â Phases 79-81 (shipped 2026-07-03, `2026.07.1`) â see `.planning/milestones/cycle14-ROADMAP.md`
- â **Cycle 13 â Deck Evaluation & Creator Output** â Phases 75-78 (shipped 2026-06-30, `2026.06.10`) â see `.planning/milestones/cycle13-ROADMAP.md`
- â **Cycle 12 â Manabase Accuracy, Command-Zone Awareness & Cross-Tool Persistence** â Phases 70-74 + flag-key namespacing (shipped 2026-06-27, `2026.06.9`)
- â **Cycle 11 â Security, Visibility Control & Creator-Lens** â Phases 64-69 (shipped 2026-06-25, `2026.06.8`) â see `.planning/milestones/cycle11-ROADMAP.md`
- â **Cycle 10 â Studio Automation, Sync & Polish** â Phases 59-63 (shipped 2026-06-21, `2026.06.6`) â see `.planning/milestones/cycle10-ROADMAP.md`
- â **Cycle 9 â Content Pipeline & Publish-Tracking** â Phases 55-58 (shipped 2026-06-19, `2026.06.5`) â see `.planning/milestones/cycle9-ROADMAP.md`
- â **Cycle 8 â Hardening & Backlog Burn-down** â Phases 51-54 (shipped 2026-06-17, `2026.06.4`) â see `.planning/milestones/cycle8-ROADMAP.md`
- â **v1.7 Local Harvest & Publish Studio** â Phases 41-50 (shipped 2026-06-17) â see `.planning/milestones/v1.7-ROADMAP.md`
- â **v1.6 Content KB Retrieval Fix + Value Re-Validation** â Phases 34-40 (shipped 2026-06-12) â see `.planning/milestones/v1.6-ROADMAP.md`
- â **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** â Phases 28-33 (shipped 2026-06-10) â see `.planning/milestones/v1.5-ROADMAP.md`
- â **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** â Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) â see `.planning/milestones/v1.4-ROADMAP.md`
- â **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** â Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) â see `.planning/milestones/v1.3-ROADMAP.md`
- â **v1.2 Multi-AI Prompts** â Phases 9-10 (shipped 2026-05-13) â see `.planning/milestones/v1.2-ROADMAP.md`
- â **v1.1 Admin Console** â Phases 6-8 (shipped 2026-05-08)
- â **v1.0 Polish & Quality** â Phases 1-5 (shipped 2026-05-02) â see `.planning/milestones/v1.0-ROADMAP.md`

## Phases

**Phase Numbering:**
- Integer phases (112, 113, ...): Planned Cycle 20 milestone work
- Decimal phases (112.1, 112.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order. Numbering continues after shipped Cycle 19 phases 108-111.

- [x] **Phase 111.1: Cut Lab Scryfall Burst Hotfix (INSERTED)** - Cut Lab's Import-pool intake stops emitting a ~21-request Scryfall burst and stops rendering a 429 error banner to users; prod hotfix, runs before Phase 112
- [ ] **Phase 112: Cycle 17 Code Port** - Cycle 17's Core engine (profile records/store, measured + stated extraction, fusion, grounding guard) and creator-style Web services/seed loader/DI registrations land on `feat/personal-tools` and build clean
- [ ] **Phase 113: Shared-Infra Re-derivation** - Cycle 17's shared-infrastructure refactors are re-derived line-by-line against current `main`, not applied wholesale from the stale branch
- [ ] **Phase 114: Port Verification & Admin Personal-Tools Surface** - Ported suites are clean of dead public-surface tests; both personal tools are reachable only through the BasicAuth-gated `/Admin` surface
- [ ] **Phase 115: Real Data - Stated Rules & Operator Run** - The operator runs the pipeline end to end so `/Admin/CreatorStyle` renders a real critique

<details>
<summary>Cycle 19 (Phases 108-111) - SHIPPED 2026-07-24 (2026.07.9)</summary>

- [x] Phase 108 - Server-Authored Cut Lab UI Patch Contract
- [x] Phase 109 - What-If Service Consolidation
- [x] Phase 110 - Cut Lab Navigation and Pool Discovery
- [x] Phase 110.1 - Cut Lab Combo Intelligence (INSERTED)
- [x] Phase 111 - Cut Lab Upgrade Regression Gate (gsd-verifier 6/6; a11y defects caught+fixed)

Full details: .planning/milestones/cycle19-ROADMAP.md

</details>

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
<summary>â 2026.07.2 Cycle 15 (Phases 82â87) â SHIPPED 2026-07-05</summary>

- [x] Phase 82 â Refactor-Review Sweep & UI Baseline Audit (completed 2026-07-04)
- [x] Phase 83 â Packet-Service SRP Split (completed 2026-07-04)
- [x] Phase 84 â Theme Semantic-Token Migration (completed 2026-07-05)
- [x] Phase 85 â `chatgpt-*` Naming Cleanup (completed 2026-07-05)
- [x] Phase 86 â UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout (completed 2026-07-05)
- [x] Phase 87 â Creator-Source Model Hardening (completed 2026-07-05)

</details>

## Phase Details

### Phase 111.1: Cut Lab Scryfall Burst Hotfix (INSERTED)
**Goal**: A Cut Lab "Import pool" of a normal Commander pool resolves without tripping Scryfall rate limiting and without rendering an error banner, by cutting the redundant per-miss Scryfall call and by not failing the whole import closed on a transient 429.
**Depends on**: None (prod hotfix; runs before Phase 112)
**Requirements**: Diagnosed in `.planning/debug/cutlab-import-scryfall-429.md` (status `root_cause_found`)
**Success Criteria** (what must be TRUE):
  1. `ScryfallCardResolver.ResolveSingleAsync`, when reached as a post-batch-miss fallback, no longer re-POSTs `cards/collection` for an identifier that already failed on that endpoint in the batch call — the worst-case per-miss live-call count drops from 2 to 1.
  2. A Scryfall `429` during Cut Lab pool intake no longer aborts the entire import: `CutLabPageService.ResolveEntriesAsync`'s fail-closed policy distinguishes a transient 429 from a permanent 404, matching the fail-open behavior every other Cut Lab resolution path already uses.
  3. The banner text "Scryfall returned HTTP 429. Try again shortly." is no longer reachable from a pool import whose cards all exist.
  4. A decision is recorded (ADR or documented decline) on the match-key asymmetry: `ScryfallReferenceResolver` matches batch hits on the RAW name (`:136`) while `ScryfallCardResolver` matches on `CardNormalizer.Normalize` (`:117-118`), manufacturing phantom misses. The `:52-61` remarks mark this LOAD-BEARING / "do not fix" and it spans 4 other services, so it is changed only with an explicit decision, never as a drive-by.
  5. The contradiction between `NormalizeForScryfall`'s xmldoc ("so DFC cards resolve on the first attempt instead of cascading into per-card fallbacks") and `ResolveBatchAsync`'s ("Never affects the match key") is resolved — one of the two is factually wrong and misleads the next reader.
  6. Regression tests cover the reduced call count and the 429-vs-404 distinction; existing Web + Core suites stay green.
  7. `ScryfallThrottle.MinInterval` paces at the rate Scryfall actually documents for the endpoints in use — 500ms (2/sec) for `/cards/collection`, `/cards/search`, `/cards/named`, `/cards/random` — instead of 200ms. The stale code comment at `ScryfallThrottle.cs:13` (which cites the "all other methods" 10/sec ceiling as if it applied here) is corrected. Because the throttle is a process-wide static gate, the latency impact on the highest-volume caller is measured and reported BEFORE the change is accepted (research assumption A2, unquantified). User decision 2026-07-31: fold in, global.
**Plans**: 5 plans (4 waves) — revised 2026-07-31 after review round 1 (`111.1-REVIEWS.md`)

Plans:
- [x] 111.1-01-PLAN.md — Hotfix: drop the redundant per-miss cards/collection POST (SC-1), fail open on a transient 429 during pool intake (SC-2), and stop caching a rate-limited pool as if Scryfall confirmed the misses (review blocker B-1) with a user-visible degraded-import warning (W-1)
- [x] 111.1-02-PLAN.md — Lock the 429 banner as unreachable end to end at ProcessAsync on a 101-card pool, with route-correct 503 inverse locks (SC-3); full Web + Core suite gate (SC-6)
- [x] 111.1-03-PLAN.md — ADR 0004 on the match-key asymmetry AND its implementation: an additive punctuation-tolerant second pass over the batch response already in hand (SC-4); correct the NormalizeForScryfall xmldoc (SC-5). Assumption A1 is discharged by the live probes in `111.1-REVIEWS.md` §0 — no further probe
- [x] 111.1-04-PLAN.md — Measure the MinInterval 200ms -> 500ms latency impact per flow against the post-01/post-03 call counts, then a blocking acceptance checkpoint (SC-7, measurement half)
- [x] 111.1-05-PLAN.md — Apply the 500ms pacing floor + correct both stale rate-limit comments; re-run gates against final state (SC-7, SC-6)

### Phase 112: Cycle 17 Code Port
**Goal**: Cycle 17's Core engine (Phases 94-98 — profile records and store, measured extraction, stated-rules extraction, profile fusion, card-grounding guard) AND the creator-style Web services, seed loader, and DI registrations land on `feat/personal-tools` and the solution builds clean.
**Depends on**: Nothing (first phase)
**Requirements**: PORT-01, PORT-02
**Success Criteria** (what must be TRUE):
  1. The profile records/store, measured extraction, stated-rules extraction, fusion engine, and card-grounding guard code from Phases 94-98 (Core), plus `Services/CreatorStyle/*` and the creator-style seed loader (Web), are present on `feat/personal-tools`.
  2. `dotnet build` on the solution completes with no new errors and no new warnings.
  3. The application starts locally with all creator-style services resolving through DI (no missing-registration failures at startup).
  4. The ported Core test suite for the creator-style engine runs and passes.
**Plans**: 6 plans

Plans:
- [ ] 112-01-PLAN.md — Pre-port baseline capture and RESEARCH manifest drift preflight
- [ ] 112-02-PLAN.md — Core file allowlist checkout, Postgres test trims, distillation-stack hunks
- [ ] 112-03-PLAN.md — Remaining Core hunks, format/path gates, Commit 1
- [ ] 112-04-PLAN.md — Web file allowlist checkout, seed placeholders, Web hunks, archidekt pipeline
- [ ] 112-05-PLAN.md — AddDeckFlowCreatorStyle DI extension and the two Program.cs edits
- [ ] 112-06-PLAN.md — Real-ArchidektOwnerClient DI test, headless boot smoke, Commit 2

### Phase 113: Shared-Infra Re-derivation
**Goal**: Cycle 17's shared-infrastructure refactors — the neutral `ScryfallCollectionResolver`, `ScryfallLimits.CollectionBatchSize`, and shared `CachedNameResolution` — are re-derived against current `main` line by line, not applied wholesale from a branch that Cycles 18-19 have since edited underneath. (The `archidekt` resilience pipeline moved to Phase 112 — see below.)
**Depends on**: Phase 112
**Requirements**: PORT-03
**Success Criteria** (what must be TRUE):
  1. `ScryfallCollectionResolver` exists as a single neutral collaborator on `main`'s current shape — no duplicate copy is reintroduced into `ManabaseAnalysisService` or elsewhere.
  2. ~~A dedicated `archidekt` resilience pipeline is registered~~ — **moved to Phase 112** by ratified decision D-17 (2026-07-24). Phase 112 research proved Polly 8.6.6's `GetPipeline<T>` throws `KeyNotFoundException` in `ArchidektOwnerClient`'s constructor on the unregistered key, so the registration had to land with the code that resolves it or Phase 112's DI success criterion could not pass. Phase 113 only needs to confirm the archidekt import path *uses* the already-registered pipeline; it does not register it.
  3. The manabase test suite passes unchanged, with no regression against Cut Lab's Cycle 18/19 edits to the same files.
  4. The Scryfall-related test suites pass unchanged.
**Plans**: TBD

### Phase 114: Port Verification & Admin Personal-Tools Surface
**Goal**: The port is verified clean of dead public-surface tests, and both creator-style and Deck Tendencies are reachable only through the existing BasicAuth-gated `/Admin` branch, with no public plumbing left behind.
**Depends on**: Phase 113
**Requirements**: PORT-04, PTOOL-01, PTOOL-02, PTOOL-03, PTOOL-04
**Success Criteria** (what must be TRUE):
  1. The full ported Core and Web test suites pass, and Phase 100's public-surface tests (feature-flag lockstep, `ToolRegistry` counts, route-gate coverage, sitemap assertions) are absent from the suite rather than failing.
  2. An unauthenticated request to `/Admin/CreatorStyle` is refused by the existing BasicAuth branch; an authenticated request renders the page.
  3. A repo-wide check confirms no `tool.creator-style.enabled` flag, no `ToolRegistry` entry, no sitemap/`SeoPaths` entry, no public help topic, and no `PacketSessionCache` bypass-list entry exist for creator-style.
  4. The `/Admin` landing page shows a personal-tools section linking to both `/Admin/CreatorStyle` and `/Admin/CreatorProfile`.
  5. `/Admin/CreatorProfile` (Deck Tendencies) is reachable and linked from that same section.
**Plans**: TBD
**UI hint**: yes

### Phase 115: Real Data - Stated Rules & Operator Run
**Goal**: The operator runs the stated-rules import, fusion, and export sequence end to end so `/Admin/CreatorStyle` renders a real critique of a submitted deck rather than an empty-store state.
**Depends on**: Phase 114
**Requirements**: PSEED-01, PSEED-02, PSEED-03, PSEED-04, PSEED-05
**Success Criteria** (what must be TRUE):
  1. `content-kb/seed/creator-stated-rules.json` is committed, hand-authored from the P89/P90 prototype, with every rule marked `Provenance = "hand-authored"`.
  2. The `creator-style-import-stated` CLI command loads that seed into `content_stated_rules` and the rules read back intact.
  3. `fuse-profile` produces `FusedTarget[]` plus a conflict ledger that reproduces the P89/P90 prototype verdicts, including the board-wipe "agreement, not hypocrisy" result.
  4. `creator-style-profiles.json` and `creator-deck-cache.json` are populated with real data (not `[]` placeholders) and committed to the repository.
  5. `/Admin/CreatorStyle` renders a real critique of a submitted deck against the seeded profile.
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 111.1 -> 112 -> 113 -> 114 -> 115

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 111.1. Cut Lab Scryfall Burst Hotfix (INSERTED) | Cycle 20 | 5/5 | Complete | 2026-07-31 |
| 112. Cycle 17 Code Port | Cycle 20 | 0/0 | Not started | - |
| 113. Shared-Infra Re-derivation | Cycle 20 | 0/0 | Not started | - |
| 114. Port Verification & Admin Personal-Tools Surface | Cycle 20 | 0/0 | Not started | - |
| 115. Real Data - Stated Rules & Operator Run | Cycle 20 | 0/0 | Not started | - |
| 108. Server-Authored Cut Lab UI Patch Contract | Cycle 19 | 3/3 | Complete   | 2026-07-23 |
| 109. What-If Service Consolidation | Cycle 19 | 2/2 | Complete   | 2026-07-23 |
| 110. Cut Lab Navigation and Pool Discovery | Cycle 19 | 6/6 | Complete   | 2026-07-24 |
| 110.1. Cut Lab Combo Intelligence | Cycle 19 | 3/3 | Complete   | 2026-07-24 |
| 111. Cut Lab Upgrade Regression Gate | Cycle 19 | 4/4 | Complete | 2026-07-24 |
| 82. Refactor-Review Sweep & UI Baseline Audit | 2026.07.2 | 3/3 | Complete | 2026-07-04 |
| 83. Packet-Service SRP Split | 2026.07.2 | 7/7 | Complete | 2026-07-04 |
| 84. Theme Semantic-Token Migration | 2026.07.2 | 2/2 | Complete | 2026-07-05 |
| 85. `chatgpt-*` Naming Cleanup | 2026.07.2 | 5/5 | Complete | 2026-07-05 |
| 86. UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout | 2026.07.2 | 5/5 | Complete | 2026-07-05 |
| 87. Creator-Source Model Hardening | 2026.07.2 | 1/1 | Complete | 2026-07-05 |
| 88. Index-Row Integrity Hotfix | Cycle 16 | 3/3 | Complete | 2026-07-06 |
| 89. Content-Hash Foundation | Cycle 16 | 6/6 | Complete   | 2026-07-07 |
| 90. DirectPush Correctness + Seed Sync | Cycle 16 | 7/7 | Complete   | 2026-07-08 |
| 91. Reconcile + Seed Lifecycle | Cycle 16 | 9/9 | Complete | 2026-07-09 |
| 92. Pull Hardening | Cycle 16 | 2/2 | Complete | 2026-07-10 |
| 93. Round-Trip Integration Test | Cycle 16 | 3/3 | Complete   | 2026-07-11 |

---

## Carry-forward backlog (not in Cycle 20)

- Installing the distill toolchain (`yt-dlp`/`ffmpeg`/`whisper`) or re-distilling the 85-video Snail corpus — deferred until a future cycle if the hand-authored stated rules prove insufficient.
- Any public launch of creator-style, including sitemap/SEO wiring — off the table per the 2026-07-19 legal review; not a deferral, a hard constraint.
- Postgres migration of the creator-style stores — local `content-kb.db` only; production hydrates from git-shipped seeds.
- Pet-card detection — spec superseded pending the EDHREC integration under consideration for a later cycle.
- Scheduled/bulk harvest (AUTO-03/04)
- SEO/growth lane (SEO-01..05)
- Matchup / meta-threat read (deferred â deepens cedh-meta-gap, a separate lane)
- **ADMIN-01** â `/Admin/Flags` sortable by on/off (enabled) state (descoped from Cycle 15, user decision 2026-07-05; view-only, no flag semantics change)
- Manabase engine refactor (CastabilitySimulator / ManabaseAnalyzer / ManabaseClassifier SRP split) â deferred out of Cycle 15: behavior-critical Monte-Carlo + Karsten scoring, no byte-identical gate, just heavily worked in Cycles 12/14. Needs a numeric-parity harness built FIRST. Candidate for a dedicated future refactor cycle.
- **KB "commander advice" content class for filtered videos** â the distill classifier filters out videos that lack actionable deckbuilding decisions (slot/cut/synergy on a real list), discarding them entirely. But many are still valuable *general commander advice*: meta/format philosophy, budget-building mindset, card evaluations. Give these a distinct KB content type/home instead of dropping them, so they can be surfaced (and pasted into ChatGPT) as advice rather than deckbuilding lessons. Needs: a second classifier verdict ("advice" vs "filtered"), its own artifact shape/prompt, and a browse surface. Observed 2026-07-04 re-distill filtered 3 such videos: `D5XXv7BzmZw` (The Midrange-ification of Commander â format meta essay), `GGoQxBP3DcE` (budget-deck pep talk / "Rock Lee of Commander"), `s_B1wCIWGR0` (Top 10 Lands for EDH â card eval + pricing).
- ~~**Manabase research-gap closure**~~ ✅ SHIPPED 2026-07-13 (plans 01-10 live in prod `61595280`; flags `restricted-lands`/`ritual-land-credit` seeded OFF awaiting flip). Continuation backlog: `.planning/captures/manabase-backlog-2026-07-13.md`.
- **Manabase backlog (post gap-closure)** — flag flips (ritual-credit ready; restricted-lands needs golden diff), MBGAP-09 cEDH castability surface (own phase, D-02), Tier-3 minors (MBGAP-06/07/08/10), UX LOW 8-10, 3 refactor follow-ups. Details: `.planning/captures/manabase-backlog-2026-07-13.md`.
- **SYNC-F1** â Retire DirectPush entirely (fold into Publish) â this cycle makes the two paths consistent; retirement is a later-cycle decision.
- **SYNC-F2** â Scheduled/automatic reconcile runs (this cycle ships operator-triggered reconcile only).
