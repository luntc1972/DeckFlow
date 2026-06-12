# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- ✅ **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** — Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) — see `.planning/milestones/v1.4-ROADMAP.md`
- ✅ **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** — Phases 28-33 (shipped 2026-06-10) — see `.planning/milestones/v1.5-ROADMAP.md`
- 🔵 **v1.6 Content KB Retrieval Fix + Value Re-Validation** — Phases 34-37 (active)

## Phases

<details>
<summary>✅ v1.0 Polish & Quality (Phases 1-5) — SHIPPED 2026-05-02</summary>

- [x] Phase 1: Visual System Tokens — 3/3 plans (UI-VS-01..04)
- [x] Phase 2: Layout, Hierarchy & UX Copy — 3/3 plans (UI-LH-01..02, UX-01..03)
- [x] Phase 3: Tech-Debt Cleanup — 4/4 plans (TD-01..04)
- [~] Phase 4: Security & Bug Fixes — 4/4 plans, ABANDONED 2026-05-02 (rerouted to Phase 5)
- [x] Phase 5: Security & Bug Fixes v2 — 3/3 plans (BUG-01, BUG-02, TD-04 patch + integration test)

Verification: 27/27 must-haves passed. 15/15 v1 requirements shipped.
Full archive: `.planning/milestones/v1.0-ROADMAP.md`

</details>

<details>
<summary>✅ v1.1 Admin Console (Phases 6-8) — SHIPPED 2026-05-08</summary>

- [x] Phase 6: Admin Shell + Flags Foundation — 7/7 plans (ADMIN-01..05, FLAG-01..05)
- [x] Phase 7: Harvest Controls + Stats — 7/7 plans (HARV-01..07)
- [x] Phase 7.1: Categories Flag + SameOrigin AJAX Fix — 2/2 plans (inserted hotfix)
- [x] Phase 8: Analytics — 5/5 plans (ANL-01..05)

</details>

<details>
<summary>✅ v1.2 Multi-AI Prompts (Phases 9-10) — SHIPPED 2026-05-13</summary>

- [x] Phase 9: Bracket UX + AI Selector Foundation — 3/3 plans (BRKT-01, AISEL-01, AISEL-04 Packets portion)
- [x] Phase 10: Claude + Gemini Artifact Optimization — 5/5 plans (AISEL-02, AISEL-03, AISEL-04 Comparison + CedhMetaGap)

Full archive: `.planning/milestones/v1.2-ROADMAP.md`
Audit: `.planning/milestones/v1.2-MILESTONE-AUDIT.md` — documentation-only gaps, all 5 v1.2 reqs functionally satisfied via manual T1-T8 + filename verify.

</details>

<details>
<summary>✅ v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene (Phases 11-15, 999.1-999.8) — SHIPPED 2026-05-23</summary>

**Production phases (5):**

- [x] Phase 11: Web Design Guidelines Audit Fixes — 10/10 plans (WDG-01..10)
- [x] Phase 12: AI-Agnostic URL + Page Rename — 5/5 plans (RENAME-01..03)
- [x] Phase 13: ChatGpt* Class Rename + Summary Doc Comments — 4/4 plans (CLASSRENAME-01..03)
- [x] Phase 14: Broader Codebase Name-vs-Behavior Audit — 4/4 plans (AUDIT-01..03)
- [x] Phase 15: AiPlatform Value Object Refactor — 3/3 plans (AIPLATFORM-01..03)

**Backlog phases (8) — closed v1.3 quality debt:**

- [x] Phase 999.1: AI-Agnostic Prose Adaptation in Razor Views — 7/7 plans
- [x] Phase 999.2: Claude `<result>` Wrapper — Direct JSON Output — 1/1 plan
- [x] Phase 999.3: Packet Download Session Cache — 3/4 plans (P01 rolled into P02-04)
- [x] Phase 999.4: Truncated-JSON Response UX — 1/1 plan
- [x] Phase 999.5: v1.3 Backlog Catch-up + Test Hardening — 4/4 plans
- [x] Phase 999.6: v1.3 Ship-Gate Test Residual Cleanup — 3/3 plans (9→0 failures; 8/8 SECURITY threats CLOSED)
- [x] Phase 999.7: v1.3 Audit Cleanup — 4/4 plans (closed audit findings F-01, F-02, WDG checkbox + STATE arithmetic drift, 999.5-UAT status)
- [x] Phase 999.8: Remove Legacy `chatgpt-*` 301 Redirects — 1/1 plan (22 lines deleted, 0 added)

**Final test gate:** `Failed: 0, Passed: 497, Skipped: 3, Total: 500` preserved across all closure phases.
**Requirements coverage:** 22/22 SATISFIED (10 WDG + 3 RENAME + 3 CLASSRENAME + 3 AUDIT + 3 AIPLATFORM).
**Final audit:** PASSED (re-audit 2026-05-23 supersedes 2026-05-22 tech_debt; all 7 prior findings closed by 999.7 + 999.8).

Full archive: `.planning/milestones/v1.3-ROADMAP.md`
Requirements archive: `.planning/milestones/v1.3-REQUIREMENTS.md`
Audit archive: `.planning/milestones/v1.3-MILESTONE-AUDIT.md`

</details>

<details>
<summary>✅ v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup (Phases 16-27, 21.1-21.2) — SHIPPED 2026-06-03</summary>

- [x] Phase 16: WDG-04 Focus-Trapped Modal — 1/1 plans (MODAL-01)
- [x] Phase 17: Doc-Comment Backfill Part 1 — 2/2 plans (DOC-01 partial)
- [x] Phase 18: Admin Mobile-Responsive Sweep — 2/2 plans (AMOB-01..04)
- [x] Phase 19: Content KB Foundation — Local Schema + Contracts — 4/4 plans (KB schema/contracts; re-scoped 2026-05-26 to local-harvester model)
- [x] Phase 20: Content KB Ingestion + Transcription (local) — 4/4 plans (KB-03/04/05; UAT 2026-05-27: 5-channel harvest, 10/10 captions)
- [x] Phase 21: Content KB Distillation + Artifact Emit (local) — 4/4 plans (KB-01/02/06/07)
- [x] Phase 21.1: Phase 21 Live-UAT Gate (INSERTED) — 1/1 plans (satisfied via 21.2 live claude-distill UAT, 10/10 at $0)
- [x] Phase 21.2: Pluggable LLM Distill CLI Backends (INSERTED) — 2/2 plans (KB-10/11; codex backend → KB-12 backlog)
- [x] Phase 22: Content KB Site Integration — 4/4 plans (KB-08/09; both UAT checkpoints passed; prod flag flip pending)
- [x] Phase 23: Doc-Comment Backfill Part 2 + Strip NoWarn — 5/5 plans (DOC-01/02; gate scoped to DeckFlow.Web, Core 186 sites deferred)
- [x] Phase 24: Card Category Lookup Fix — quick-fix (CAT-01; live smoke passed 2026-05-25)
- [x] Phase 25: Admin Harvested-Decks Paged Grid — 2/2 plans (AHD-01)
- [x] Phase 26: Category Cache Schema Normalization — 2/2 plans (DBO-01; SC2 amended per b1a5cc8, SC3 69s→0.66ms)
- [x] Phase 27: Deck-Cache Content-Hash Dedup + 5-Day Refresh — 1/1 plans (CAT-02)

**Stats:** 343 commits, 638 files, +54,651/−4,726 LOC, 2026-05-23 → 2026-06-03 (11 days).
**Requirements:** 20/20 active v1.4 REQ-IDs satisfied (KB-12 → backlog). Final tests: Core 257/257, Web 528 pass / 5 PG-skips.
**Audit:** tech_debt (0 critical gaps; artifact-hygiene items carried to v1.5 — see audit archive).

Full archive: `.planning/milestones/v1.4-ROADMAP.md`
Requirements archive: `.planning/milestones/v1.4-REQUIREMENTS.md`
Audit archive: `.planning/milestones/v1.4-MILESTONE-AUDIT.md`

</details>

<details>
<summary>✅ v1.5 Deck Primer Generator + Content KB Integration + Housekeeping (Phases 28-33) — SHIPPED 2026-06-10</summary>

**Milestone Goal:** Ship the Deck Primer Generator as a fourth paste-ready workflow, wire Content KB knowledge into deck-analysis prompts, and clear v1.4 quality debt.

- [x] Phase 28: Housekeeping Bundle — HSK-03/04 (HSK-02 re-demoted) — completed 2026-06-04
- [x] Phase 29: Core XML-Doc Backfill + Gate Widen — HSK-01 — completed 2026-06-05
- [x] Phase 30: Content KB Integration — KBI-01..06; prod UAT passed — completed 2026-06-07
- [x] Phase 32: Expert Context Selection — SEL-01..06; pin/follow/evergreen 4-tier merge — completed 2026-06-08
- [x] Phase 33: Admin Content KB Curation UX — KBUX-01/02 — completed 2026-06-09
- [x] Phase 31: Deck Primer Generator — PRM-01..12; fourth paste-ready workflow — completed 2026-06-09

**Audit:** passed — 30/30 requirements; SEL-02 expert-pin bug diagnosed + fixed at close (`a106c6a`). Full phase details: `.planning/milestones/v1.5-ROADMAP.md`.

</details>

---

## v1.6 Content KB Retrieval Fix + Value Re-Validation

- [x] **Phase 34: KB Retrieval Fix** — KBR-01, KBR-02, KBR-03, KBR-04 (VERIFICATION: passed 2026-06-10)
- [x] **Phase 35: Value Re-Validation Gate** — KBV-01..04 — **MARGINAL** (gate NOT cleared; 2026-06-10). Pivot = **retire clip-injection**. See `35-GATE-VERDICT.md`.
- [~] **Phase 36: Creator Philosophy-Profile + KB Un-Dark** — **SKIPPED** (gate MARGINAL). PHIL-01..04 + KBD-01/02 not built; KB stays dark. Pivot recorded: retire the whole-channel clip-injection feature.
- [x] **Phase 37: Retire Clip-Injection + Un-Dark KB** — RET-01..06 *(remove the gate-condemned injection, un-dark the `/content-kb` browse, add a deck-analysis pointer to the KB's copyable prompts)* (completed 2026-06-10)
- [x] **Phase 37.5: Rebuild KB Corpus** — REBUILD-01..05 *(reset corpus + re-harvest salubrious-snail under a deck-advice/philosophy LLM+manual quality filter; fix the `[00:00]` clip-extraction defect)* (completed 2026-06-11; live pilot PASS local+prod; 3 UAT defects fixed)
- [x] **Phase 37.6: Harvest Video Block + Hard-Delete** — VBLK-01..04 *(admin blocks a YouTube video by id so the harvester never re-ingests it, and hard-deletes its rows)* (INSERTED) (completed 2026-06-11)
- [x] **Phase 38: Controller SRP Split** — SRP-01, SRP-02, SRP-03 *(milestone closer — runs after the KB work so it splits slimmed controllers)* (completed 2026-06-12)

---

## Phase Details

### Phase 34: KB Retrieval Fix
**Goal**: The Content KB retriever selects diverse, topically relevant clips with injection-safe text — no single video monopolizes results, off-topic content is penalized, and transcript-derived text cannot act as instructions
**Depends on**: Nothing (first phase, unconditional)
**Requirements**: KBR-01, KBR-02, KBR-03, KBR-04
**Success Criteria** (what must be TRUE):
  1. Running retrieval against an Atraxa deck yields clips from at least 2 distinct video sources; the Kaalia/Animar tangential video from Spike 001 Run 2 is excluded or capped to at most 1 clip
  2. A video with broad tags (e.g. "Glass Cannon Commanders") does not outscore a video whose content directly addresses the deck's archetype when the two compete for the same slot
  3. The injected `## Expert Context` block in the rendered prompt is wrapped in a structural boundary and all clip text has passed the prompt-injection regex sanitizer before reaching the LLM
  4. A regression test reproducing the Spike 001 Run-2 Atraxa scenario passes: asserts per-video cap, asserts topical exclusion of the commander-leakage video, and is part of the standard test run
**Plans**: 2 plans (1 wave, parallel)
- [x] 34-01-PLAN.md — Retrieval algorithm fix in ContentKbRelevanceService: per-video clip cap (KBR-01) + topical content-overlap scoring, other-commander demotion, relevance floor, null-on-no-match (KBR-02) + mandatory Spike 001 Atraxa regression tests (KBR-04)
- [x] 34-02-PLAN.md — Prompt-injection mitigation (KBR-03): regex sanitizer (ContentKbClipSanitizer) + structural data fence around the ## Expert Context block across all three analysis variants; keeps Spike 001 harness runnable

### Phase 35: Value Re-Validation Gate
**Goal**: A blind, multi-deck A/B verdict is recorded against the fixed retriever — the gate either clears (VALIDATED → proceed to Phase 36) or fails (MARGINAL → KB stays dark, pivot decision recorded, milestone proceeds to Phase 37 only)
**Depends on**: Phase 34 (gate tests the fixed retriever; running it on the broken retriever would reproduce the NEGATIVE Spike 001 Run 2 result)
**Requirements**: KBV-01, KBV-02, KBV-03, KBV-04
**Success Criteria** (what must be TRUE):
  1. The `Spike001KbValueAbHarness` runs against at least 3 representative decks with distinct commanders and archetypes (not just Atraxa), emitting a `baseline.txt` and `with-context.txt` per deck
  2. Baseline AI answers are scored against the rubric (specificity, creator-voice, novel signal, actionability) **before** the with-context answers are read — the blind protocol is explicitly documented in VERDICT.md
  3. VERDICT.md contains per-deck rubric scores for all 3+ decks and declares a single outcome: `VALIDATED` (≥3/4 dimensions score 3+ for the majority of decks, no quality loss vs. baseline) or `MARGINAL`
  4. The gate outcome routes the milestone explicitly: VALIDATED → Phase 36 proceeds; MARGINAL → Phase 36 is skipped, pivot decision (fix-again / per-deck retrieval pivot / retire) is recorded in VERDICT.md, and the milestone closes after Phase 37
**Plans**: 2 plans (2 waves; gate-locked sequential)
- [x] 35-01-PLAN.md — Extended Spike001KbValueAbHarness to 5 bracket-spanning decks; real fixed retriever over rebuilt corpus emitted baseline + with-context prompts per deck (KBV-01) [Codex-executed]
- [x] 35-02-PLAN.md — Scored AI answers on the 4-dim rubric (5 isolated-pass judgments), recorded MARGINAL verdict in 35-GATE-VERDICT.md, routed the milestone (KBV-02/03/04) [Claude-executed]

### Phase 36: Creator Philosophy-Profile + KB Un-Dark
**Goal**: Per-creator philosophy profiles ground deck-analysis prompts in a creator's distilled heuristics — each principle traced to a verified source passage; the Content KB is flipped ON in production and the expert-pin carry-forward is re-confirmed live
**Depends on**: Phase 35 gate = VALIDATED (this phase is CONDITIONAL and is SKIPPED if the gate verdict is MARGINAL)
**Requirements**: PHIL-01, PHIL-02, PHIL-03, PHIL-04, KBD-01, KBD-02
**Success Criteria** (what must be TRUE):
  1. Running the `synthesize-philosophy` CLI command for a creator with ≥10 substantive harvested videos produces a style-card where every principle carries a non-nullable `source_video_id` + `source_timestamp_s` — a principle with no citable passage is not stored
  2. A deck-analysis prompt generated with a creator's profile active contains a `## Creator Heuristics` sub-section with attributed principles; a contradictory pair of opinions from different videos appears as two separate entries (not merged)
  3. A principle whose only supporting passage is from a video published more than 18 months ago is deprioritized at injection time; the `content.kb.profiles.enabled` flag controls the entire sub-section and is null-graceful when absent
  4. `content.kb.enabled` is ON in production, and a pinned video from the SEL-02 expert-pin scenario appears in the Expert Context block of a live deck-analysis run
**Plans**: TBD
**UI hint**: yes

### Phase 37: Retire Clip-Injection + Un-Dark KB Browse
**Goal**: The gate-condemned clip-injection into deck-analysis prompts is fully removed (the `## Expert Context` block, the expert-selection widget, the "What Experts Say" panel, the retriever services), the KB-as-reference (`/content-kb` browse) is kept and un-darked, and the deck-analysis page points users to the KB's copyable prompts
**Depends on**: Phase 35 (executes the KBV-04 retire pivot). Runs BEFORE Phase 38 so the SRP split operates on already-slimmed DeckController/DeckAnalysisPacketService.
**Requirements**: RET-01..06 — see `phases/37-retire-clip-injection/37-CONTEXT.md`
**Scope (A-only, full removal):** delete the 3 retriever services + ContentKbExcerpt + expert-selection types/endpoints/TS + `_ContentKbPanel`; strip injection params from the 3 prompt variants + DeckAnalysisPacketService + DeckController; remove the admin relevance-score preview. KEEP the browse-site, admin curation grid, harvest/distill CLI, Core content stores, corpus. Flip `content.kb.enabled` ON (un-dark) + verify browse views HTML-encode harvested text (XSS). Add a deck-analysis note linking to `/content-kb` (where each entry already has a "Copy artifact" button).
**Success Criteria** (what must be TRUE):
  1. A generated deck-analysis prompt (all 3 AI variants) contains NO `## Expert Context` block; the DeckAnalysis page has no expert-selection accordion and no experts panel, but does carry a note linking to the KB's copyable prompts
  2. Solution builds clean (0 new warnings); grep proves no `ContentKbRelevanceService`/`ContentKbExcerpt`/`ExpertSelection` references remain outside removed files
  3. The KB reference is intact: `DeckFlow.CLI harvest`/`distill` still populate the corpus and `/content-kb` browse (now un-darked) lists/renders the distilled entries with harvested text HTML-encoded (XSS-safe)
  4. A pre-retire packet zip carrying `ExpertSelectionJson` still loads without error (graceful ignore of the removed field)
**Plans**: 2 plans (2 waves; wave 2 depends on wave 1)
- [x] 37-01-PLAN.md — Wave 1 (full removal, build-green): delete 3 retriever services (incl. IContentKbRelevanceService) + ContentKbExcerpt + typeahead API + _ContentKbPanel + kb-selection.ts + content-kb-admin.ts; de-wire prompt variants/DeckAnalysisPacketService/request/viewmodel/packet store+cache/DeckController; pull-forward the admin score-preview removal AND the /content-kb browse-page selection strip AND dead expert-selection CSS (so the interface deletion + ALL consumers land in one build-green unit); prune 8 tests + edit 6 (incl. AdminContentKbControllerTests); new RET-01 (no ## Expert Context) + RET-05 (legacy zip loads) assertions (RET-01/02/05)
- [x] 37-02-PLAN.md — Wave 2 (un-dark + repoint, independent files only): flip content.kb.enabled seed default ON + RET-04 Markdig .DisableHtml() XSS-regression test + DeckAnalysis KB pointer note (add-only) + fix injection-advertising Home/nav copy (RET-03/04/06)

### Phase 37.5: Rebuild KB Corpus — High-Signal Re-Harvest
**Goal**: The KB corpus is reset and rebuilt with deck-advice/philosophy content only, with the `[00:00]` clip-extraction defect fixed so clips carry real mid-video timestamps — feeding the un-darked browse-site as a curated reference
**Depends on**: Phase 37 (KB is browse-only + un-darked). Runs before/around Phase 38 (which splits `CommandRunners` that this phase edits).
**Requirements**: REBUILD-01..05 — see `phases/37.5-rebuild-kb-corpus/37.5-CONTEXT.md`
**Scope:** purge all `content_*` tables (local + prod); re-harvest Salubrious Snail first; HYBRID quality filter (distill-time LLM classifier drops trivia/news/meta/intro, operator confirms survivors in admin curation); FIX the distiller so clips have real timestamps (`TimestampSeconds ?? 0` root cause — feed timestamped transcript, require real per-clip times, reject all-zero distills). Other creators = later pass.
**Success Criteria** (what must be TRUE):
  1. Corpus reset to empty, then re-harvested: only quality-filtered Salubrious Snail deck-advice/philosophy videos indexed; a sampled junk video (trivia/news/intro) is demonstrably dropped
  2. Published entries' clips have real, distinct, non-zero timestamps at the advice moment (spot-check vs the video); an all-timestamp-0 distill is rejected
  3. `/content-kb` lists the rebuilt entries; Detail renders summary/clips HTML-encoded with a working "Copy artifact"
**Plans**: 3 plans
- [x] 37.5-01-PLAN.md — corpus-reset CLI command + 3-site clip-timestamp fix (fetcher [mm:ss] + clips prompt + all-zero rejection) + store purge methods (REBUILD-01, REBUILD-03)
- [x] 37.5-02-PLAN.md — LLM quality classifier gate in distill flow + 'filtered' distill status w/ idempotent CHECK migration (REBUILD-02)
- [x] 37.5-03-PLAN.md — live pilot: reset (local+prod) → re-harvest snail → distill → operator publish → browse/copy verify (REBUILD-01..05, manual checkpoints)

### Phase 37.6: Harvest Video Block + Hard-Delete (INSERTED)
**Goal**: An admin can permanently suppress a harvested YouTube video — its rows are hard-deleted AND its video id is recorded so a later harvest never re-ingests it; benign videos are unaffected
**Depends on**: Phase 37 (KB un-darked, browse-only). Independent of Phase 37.5 (corpus rebuild) and Phase 38 (SRP split).
**Requirements**: VBLK-01, VBLK-02, VBLK-03, VBLK-04 — see `phases/37.6-harvest-video-block-and-hard-delete/37.6-CONTEXT.md`
**Architecture (LOCAL-FIRST via CLI):** RESEARCH proved full content + harvester live in local-only `content-kb.db` (never on Render); prod web has only the slim site-index. So the feature is a `DeckFlow.CLI` operation run where harvest runs, NOT a prod-admin action.
**Scope:** new `blocked_videos` table in local `content-kb.db` (YouTube id STRING key + reason + blocked_at; reset-proof name, carved out of the 37.5 `content_*` purge); ingest skip-check in `CommandRunners` harvest path (before `InsertVideoAsync`); CLI `block-video`/`unblock-video` (+ optional `list-blocked`) that insert the block row FIRST then hard-delete via `IContentVideoStore.DeleteVideoAsync` + a NEW `IContentSiteIndexStore.DeleteByIdAsync` (site-index is a separate DB DeleteVideoAsync can't reach). NO Web/prod-admin changes.
**Success Criteria** (what must be TRUE):
  1. CLI `block-video <youtube-id>` → its `content_videos` (+ FK-cascaded transcript/summary/clip rows) AND its `content_site_index` entry are gone, AND a `blocked_videos` row exists for the YouTube id
  2. A subsequent harvest re-encountering that YouTube id does NOT re-insert it (skip-check before `InsertVideoAsync`); a non-blocked video in the same run still ingests
  3. The block list survives a corpus reset (lives in `content-kb.db` but is named/contracted outside the 37.5 `content_*` reset so re-harvest stays clean)
  4. `unblock-video <youtube-id>` removes the block row so a later harvest re-ingests it (does not resurrect deleted rows)
**Plans**: 1 plan, 1 wave
- [x] 37.6-01-PLAN.md — blocked_videos store + site-index DeleteByIdAsync + harvest skip-check + block/unblock/list-blocked CLI

### Phase 38: Controller SRP Split
**Goal**: `DeckController` and `CommandRunners` are decomposed into focused, single-responsibility units — all existing URLs and CLI commands preserved unchanged, no user-visible behavior altered
**Depends on**: Phase 37 (split operates on the post-retire, slimmed controllers; otherwise independent of all KB phases)
**Requirements**: SRP-01, SRP-02, SRP-03
**Success Criteria** (what must be TRUE):
  1. Every URL that existed before the split returns the same response after the split — a pre-split URL list compared against a post-split URL list shows zero removals or changes
  2. All existing controller tests pass against the split controllers with only logger-generic type references updated; no new test failures introduced, no new compiler warnings
  3. `DeckFlow.CLI` `CommandRunners.cs` is split at the content-KB boundary: deck-domain runners and KB runners live in separate classes; all commands still registered and invocable
**Plans**: 6 plans
- [x] 38-01-controller-base-shell-PLAN.md — DeckToolControllerBase + ShellController scaffold (D-03); move Home/Error/set-options out of DeckController
- [x] 38-02-sync-convert-judge-PLAN.md — extract DeckSyncController, DeckConvertController, JudgeQuestionsController
- [x] 38-03-lookup-categories-PLAN.md — extract DeckLookupController + DeckCategoriesController (adopts base timeout helper)
- [x] 38-04-packet-primer-PLAN.md — extract DeckPacketController + DeckPrimerController; delete emptied DeckController
- [x] 38-05-commandrunners-split-PLAN.md — CLI two-commit split: ContentKbCliPaths, then DeckCommandRunners + ContentKbCommandRunners (SRP-02)
- [x] 38-06-tests-route-parity-PLAN.md — mirror-split DeckControllerTests (SRP-03) + SC1 pre/post route-parity gate

### Phase 39: Architecture Review + Top-Finding Refactor
**Goal**: A whole-solution architecture audit produces a ranked refactor backlog (read-only findings + ADR stubs), then the single highest-priority finding is refactored and verified — leaving the codebase measurably more cohesive without altering user-visible behavior
**Depends on**: Phase 38 (audit reads the post-SRP-split structure; the controller/CLI decomposition is the new baseline)
**Requirements**: ARCH-01 (audit + ranked backlog), ARCH-02 (execute top finding)
**Success Criteria** (what must be TRUE):
  1. A whole-solution audit (all 5 projects) is captured as refreshed `.planning/codebase/` docs + a ranked refactor backlog scoring SRP/cohesion/coupling per area, with the top finding explicitly justified
  2. The top finding is refactored: build clean (0 new warnings), all existing tests pass, and any public surface it touches is provably behavior-preserving (route/CLI/contract parity as applicable)
  3. The remaining ranked findings are recorded as backlog (ADR stubs / `/gsd:review-backlog` entries) for a future milestone — not silently dropped
**Plans**: ARCH-02 = 3 plans / 3 waves (Finding A, pure behavior-preserving refactor)
- [ ] 39-01-deck-loader-extraction-PLAN.md — extend IDeckEntryLoader (source-autodetect + fallback notice); route all 4 packet services through it; delete 4 private LoadDeckEntriesAsync
- [ ] 39-02-scryfall-resolver-comparison-metagap-PLAN.md — extract IScryfallCardResolver (collection batch + shared fuzzy fallback); migrate Comparison + Meta-Gap; move their Scryfall Func seams onto the resolver
- [ ] 39-03-scryfall-resolver-analysis-PLAN.md — add Analysis 3-stage fallback + named seam to the resolver; migrate Deck Analysis; delete its 3 Func seams + private SearchFallbackCardAsync

---

## Progress

**Execution Order (v1.6):**
Phase 34 → Phase 35 (gate). Gate = MARGINAL → Phase 36 SKIPPED. Pivot → Phase 37 (retire injection + rehabilitate KB) → Phase 37.5 (rebuild corpus) → Phase 37.6 (harvest block + hard-delete) → Phase 38 (SRP split) → Phase 39 (architecture review + top-finding refactor). Milestone closes after Phase 39.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 34. KB Retrieval Fix | 2/2 | ✅ Complete (VERIFICATION passed) | 2026-06-10 |
| 35. Value Re-Validation Gate | 2/2 | ✅ Complete — **MARGINAL** | 2026-06-10 |
| 36. Creator Philosophy-Profile + KB Un-Dark *(CONDITIONAL)* | — | ⊘ SKIPPED (gate MARGINAL) | - |
| 37. Retire Clip-Injection + Un-Dark KB | 2/2 | Complete   | 2026-06-10 |
| 37.5. Rebuild KB Corpus (re-harvest snail) | 0/TBD | Scoped (CONTEXT ready) | - |
| 37.6. Harvest Video Block + Hard-Delete | 1/1 | Complete   | 2026-06-11 |
| 38. Controller SRP Split | 6/6 | Complete   | 2026-06-12 |
| 39. Architecture Review + Top-Finding Refactor | 0/TBD | Audit in progress | - |

---

## Backlog

### Codex Distill Backend (BACKLOG — low priority; was Phase 21.3, demoted 2026-06-01; re-demoted 2026-06-04 after Phase 28 discovery)

> Investigation 2026-06-04 (codex 0.136.0, Phase 28-03 / `28-DISCOVERY.md`): `--sandbox read-only` documented as "can read files in workspace" (structural evidence from binary). No `--no-tools` flag exists. `deny_read` glob mechanism requires `codex-linux-sandbox` + bubblewrap infrastructure not present, with no documented global read disable. Re-investigable when a future codex version provides documented read-blocking. D-03 re-demote applied; user ratified 2026-06-04.

**Goal:** Add the `codex` provider to the Phase 21.2 distill backend factory, with a PROVEN tool/read-isolation boundary for untrusted transcript input. codex `exec` is an agent and `--sandbox read-only` blocks writes but not reads, so a prompt-injected transcript could read+echo local files; ship codex only once the read boundary is demonstrably closed (verified no-tools mode, OR a sandbox/container exposing only stdin). claude backend (Phase 21.2) already covers the subscription-distill use case, so codex is a nice-to-have second provider — low priority.
**Requirements:** KB-12 (codex CLI distill backend with proven untrusted-input read isolation)
**Depends on:** Phase 21.2 (provider factory + CliCommandSpec + CLI service seam — shipped)
**Plans:** 0 plans

Acceptance when promoted: `DECKFLOW_LLM_PROVIDER=codex` works via the existing factory/CliCommandSpec seam (no new arch; openai+claude unchanged); the codex spawn provably cannot read arbitrary local files under a malicious transcript (documented isolation + sentinel-file exfil test); same JSON-repair/ValidateTags/timeout/ledger-bypass guarantees; live codex distill over the UAT db emits valid artifacts + spend=0; E5/E6 human sample passes. Promote via `/gsd-phase` (renumber) when prioritized.

### edhtop16 Filter Defaults vs DeckFlow Filter Defaults (BACKLOG — unnumbered, was 999.3 before collision with active Packet Download Session Cache phase; renumber when promoted)

**Goal:** [Captured for future planning]
**Requirements:** TBD
**Plans:** 0 plans

Captured 2026-05-17 during Phase 13 UAT T5. cEDH Meta-Gap fails to find Plagon, Lord of the Beach decks even though edhtop16.com shows multiple recent entries (2025-05 through 2026-01). DeckFlow filters (Six Months + Top Performing + minEventSize) return zero matches; edhtop16.com site UI likely uses different default filter window/event-size threshold/standing cutoff.

Repro (2026-05-17 14:18:57 + 14:19:09 in `web-20260517.log`):

- Commander: "Plagon, Lord of the Beach"
- Filters: SixMonths, TopPerforming, minEventSize=default, maxStanding=default
- Result: `InvalidOperationException` at `MetaGapService.cs:160` — "No EDH Top 16 decks matched your filters..."
- edhtop16.com browser shows entries from 2026-01-04, 2026-01-18, 2025-09-27, 2025-05-24

Pre-existing — predates Phase 13 (MetaGapService logic unchanged by rename). Investigate:

1. edhtop16 GraphQL `commander(name)` lookup: does "Plagon, Lord of the Beach" match the stored canonical name exactly?
2. Default DeckFlow form filter values vs site UI defaults — alignment audit.
3. minEventSize=50 default may be too restrictive — site UI may use 30.
4. timePeriod=SixMonths may map to ≤180 days where site uses calendar months (sometimes 183-184 days).

Plans:

- [ ] TBD (promote with /gsd:review-backlog when ready)

### Phase 39 architecture findings — deferred (ARCH-02 executes Finding A; rest below per SC3)

See `.planning/phases/39-architecture-review/39-AUDIT.md` + `39-AUDIT-CODEX.md` (two independent audits).
- **B — Split `CategoryKnowledgeRepository`** (1276 LOC, 24 methods → Schema / DeckQueue / CardCategory). Live-path Core god-repo; strong existing test net (17 facts + parity + dedup). Effort L / Risk M.
- **C — Split `ContentKbCommandRunners`** (1508 LOC, 5 sub-domains → Harvest / Distill / Source runners). Internal seams pin behavior. Effort M / Risk M.
- **D — Finish `Services/` concern-foldering + extract `Program.cs` `AddDeckFlowXxx()`** (48 flat files; empty `Services/Content/` = stalled migration). Pure file/namespace moves. Effort M / Risk L.
- **E — Relocate misplaced domain logic to `DeckFlow.Core`** (deck-stat classifiers in DeckComparisonService; distill cost/validation in ContentKbCommandRunners). Effort M / Risk L.
- **F — Strengthen dual-dialect abstraction** (33 `IsPostgres`/`IsSqlite` branches across 7 stores → dialect render methods; remove Web `Feedback*` SQL from Core `IRelationalDialect`). ⚠ Postgres path has no automated test. Effort M / Risk M.
- **ADR-note tier:** G packet cache-key `IPacketCacheKeyStrategy` · H `IScryfallThrottle` seam · I `IMemoryCache` SizeLimit doc · J `System.CommandLine` beta4 deliberate-pin ADR · K residual test gaps (middleware-ordering integration test; Polly policy-shape assertion).

### Deferred to v1.6+ (per v1.5 scope decision)

- **Gemini paste-limit workaround** (`DECKFLOW_GEMINI_ENABLED` stays flag-gated; needs split-message vs direct-API path decision)
- **SpellbookCombo ranking fields** (PRM-08 — parser drops `manaValueNeeded`/`popularity`/`uses`; priority ranking degraded)
- **Embedding/vector retrieval** (pgvector / ONNX sentence-transformers) — deferred until corpus >~500 videos; RAM-cap risk at current size

---

*v1.0 shipped 2026-05-02 | v1.1 shipped 2026-05-08 | v1.2 shipped 2026-05-13 | v1.3 shipped 2026-05-23 | v1.4 shipped 2026-06-03 | v1.5 shipped 2026-06-10 | v1.6 active*
