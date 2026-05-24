# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- 🚧 **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** — Phases 1-8 (started 2026-05-23, phase numbering reset)

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

### 🚧 v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup (In Progress)

**Phase numbering reset to Phase 1** (v1.3 phase dirs archived to `.planning/milestones/v1.3-phases/`).
**Scope:** 16 REQ-IDs across 4 clusters (MODAL, DOC, AMOB, KB). Gemini cluster D dropped per user decision 2026-05-23 → v1.5.

- [x] **Phase 1: WDG-04 Focus-Trapped Modal** — Close v1.3 carry-over: replace deferred `onsubmit` confirm in AdminFeedback/Detail with native `<dialog>` focus-trapped modal (completed 2026-05-24; UAT passed; tests 520/3/523; MODAL-01 satisfied)
- [ ] **Phase 2: Doc-Comment Backfill — Part 1 (Controllers + Services)** — Backfill XML `<summary>` doc-comments on ~50 of 88 v1.1-era Web types; NoWarn stays in place until Phase 8
- [ ] **Phase 3: Admin Mobile-Responsive Sweep** — Factor `admin.css` → `admin-common.css` + `admin-mobile.css` + import shim; sidebar collapse, table strategies, ≥44px touch targets — all scoped to `.admin-shell`
- [ ] **Phase 4: Content KB Foundation — Stores + Schema** — 8 new `content_*` Postgres tables + spend ledger via per-store `EnsureSchemaAsync`; zero outbound HTTP
- [ ] **Phase 5: Content KB Outbound HTTP Services** — YouTube (YoutubeExplode) + Podcast (Syndication) + Whisper (OpenAI 2.10) + LLM summary (OpenAI Structured Outputs) + tag inference; named HttpClients + Polly pipelines
- [ ] **Phase 6: Content KB Orchestrator + Harvest Runs** — `ContentHarvestOrchestrator` + `ContentHarvestRunStore`; TOCTOU-safe Whisper cap-gate via `pg_try_advisory_lock`; kill-switch env var
- [ ] **Phase 7: Content KB Admin UI** — `/Admin/ContentSources` CRUD + `/Admin/ContentHarvest` history + `/Admin/ContentSpend` dashboard; CSRF-guarded; `content_kb_enabled` flag gate
- [ ] **Phase 8: Doc-Comment Backfill — Part 2 + Strip NoWarn** — Remaining ~38 types + new v1.4 surface; LAST step strips `NoWarn 1591;1573;1587` from `DeckFlow.Web.csproj`

## Phase Details

### Phase 1: WDG-04 Focus-Trapped Modal
**Goal**: Admin can confirm destructive feedback actions via a styled focus-trapped modal that meets WCAG 2.1.2 / 2.4.3, closing the v1.3 WDG-04 override deferred 2026-05-16.
**Depends on**: Nothing (first phase; lands first as ship-gate proof)
**Requirements**: MODAL-01
**Success Criteria** (what must be TRUE):
  1. Admin opens delete-confirm on `/Admin/Feedback/Detail/{id}` and the native `<dialog>` element opens via `showModal()` — no hand-rolled focus-trap library, no npm dependency added
  2. Tab + Shift+Tab cycle stays inside the modal across native form controls AND nested `df-select` / `df-typeahead` custom elements (verified by HUMAN-UAT in a Razor view containing both)
  3. Escape closes the modal AND restores focus to the trigger button; cancel button + click-outside also close it
  4. New modal CSS lands in `wwwroot/css/admin.css` (or new `_AdminConfirmModal.cshtml` partial), uses existing `:root` tokens, and does not leak any unscoped element selectors into the 22 guild themes (Pitfall 10 mitigation)
**Plans**: 1 plan
Plans:
- [ ] 01-01-PLAN.md — Native <dialog> showConfirm primitive + structural partial + scoped CSS + Detail.cshtml wire-up (MODAL-01)

### Phase 2: Doc-Comment Backfill — Part 1 (Controllers + Services)
**Goal**: Documentation debt is reduced on the highest-traffic Web surface (~50 of 88 v1.1-era types) without flipping the NoWarn gate, so the warning suppression stays in place until Phase 8 lands the rest of the v1.4 surface.
**Depends on**: Nothing (parallelizable; off critical path)
**Requirements**: DOC-01 (partial — Controllers + Services subset)
**Success Criteria** (what must be TRUE):
  1. Every public type under `DeckFlow.Web/Controllers/`, `Controllers/Admin/`, `Controllers/Api/`, `Services/`, and `Services/Http/` has a `<summary>` doc-comment — verified by anchored grep: `grep -L '<summary>' $(grep -lE '^public (sealed )?(class|record|interface)' DeckFlow.Web/{Controllers,Services}/**/*.cs)` returns empty
  2. `<param>` + `<returns>` tags on non-trivial public methods (multi-arg + non-void); `<inheritdoc/>` used on interface implementations
  3. `NoWarn 1591;1573;1587` REMAINS in `DeckFlow.Web.csproj` (do not strip until Phase 8); `dotnet build -c Release` stays at 0 warnings / 0 errors
  4. Touch-only-what-you-touch discipline preserved (no Format Document, no `{ get; }` mutations, no `[Attribute]` inlining, no raw-string re-indents per CLAUDE.md R-6)
**Plans**: 2 plans
Plans:
- [ ] 02-01-PLAN.md — Controllers cluster: <summary> on 7 undocumented public types across 5 files (CommanderController, FeedbackController, Admin/AdminFeedback {enum+VM+controller}, Api/Suggestions, Api/ArchidektCacheJobs) (DOC-01)
- [ ] 02-02-PLAN.md — Services cluster: D-01 inheritdoc split on 4 interface/impl pairs (EdhTop16Client, CategoryKnowledgeStore, FeedbackStore, ScryfallSetService) + D-04 summaries on 2 records (ScryfallCardFace, FeedbackRequestContext); 9 types / 7 files (DOC-01)
> Scope note: 2026-05-24 re-scan found 16 undocumented types remain (15 class/record/interface + 1 enum) — the original "~50 of 88" Goal snapshot was stale; most listed types were already documented in prior sessions. NoWarn gate retained (Phase 8 strips it).

### Phase 3: Admin Mobile-Responsive Sweep
**Goal**: Admin shell renders correctly and accessibly on viewports ≥320px wide, with all WCAG 2.5.5 ≥44px touch-target floors met, AND zero CSS regression on the 22 guild themes.
**Depends on**: Phase 1 (modal CSS lands in `admin-common.css` factoring — doing modal after split forces touching two files)
**Requirements**: AMOB-01, AMOB-02, AMOB-03, AMOB-04
**Success Criteria** (what must be TRUE):
  1. `admin.css` is factored into `wwwroot/css/admin-common.css` (layout primitives mirroring `site-common.css` role) + `wwwroot/css/admin-mobile.css` (`@media (max-width: 768px)` rules) + `admin.css` import shim; all new admin selectors are scoped to `.admin-shell` parent class OR wrapped in `@layer admin { ... }` cascade discipline (Pitfall 10)
  2. Sidebar collapses to `<details>`/`<summary>` disclosure below 768px with no-JS fallback; `_AdminLayout.cshtml` markup updated; keyboard + screen-reader operable
  3. Admin tables remain usable on narrow viewports — per-table choice between `overflow-x: auto` (Analytics, HarvestRuns, ContentHarvest — comparison-dense) with `tabindex="0"` for keyboard pan, and card-stack pattern (Feedback list, ContentSources list — scanning); rationale documented per-table
  4. All admin interactive elements (buttons, links, icon-actions, form controls) meet ≥44×44px touch-target floor at narrow viewport; admin forms render single-column ≤768px
  5. Visual regression: homepage + `/sync` photographed in Rakdos + Azorius + Boros + Gruul guild themes at 375px mobile viewport BEFORE and AFTER the sweep — zero pixel diff on non-admin pages (Pitfall 10 detection)
**Plans**: TBD
**UI hint**: yes

### Phase 4: Content KB Foundation — Stores + Schema
**Goal**: Postgres + SQLite schema for the Content Knowledge Base is materialized via per-store `EnsureSchemaAsync` mirroring `HarvestRunStore.cs:436-471`, with strict `content_*` namespace and F-PROD-CONTRACT-style test isolation, so subsequent phases can rely on a stable persistence layer.
**Depends on**: Nothing within v1.4
**Requirements**: KB-01 (schema half: `content_sources` table), KB-05 (schema half: `whisper_spend_ledger` table + `month_key` generated column)
**Success Criteria** (what must be TRUE):
  1. 8 new tables created and bootstrap-tested on BOTH SQLite (local dev) AND Postgres (CI integration via `[Trait("Category","Postgres")]` bucket): `content_sources`, `content_videos`, `content_transcripts`, `content_summaries`, `content_clips`, `content_tags`, `whisper_spend_ledger`, `content_harvest_runs` — verified by `\dt content_*` against Render Postgres returning all expected tables with expected columns (Pitfall 12)
  2. Zero schema-name collision with v1.1 `harvest_runs` — `ContentHarvestRunStore` is a parallel sibling of `HarvestRunStore`, NOT a subclass; `harvest_runs.kind` CHECK constraint NOT widened
  3. Every new store has its own xUnit fixture using own SQLite file OR `:memory:` per-fact scope (F-PROD-CONTRACT 999.6 lesson honored); store tests pass with `Failed:0` in CI
  4. All new record types preserve `{ get; init; }` properties (System.Text.Json silently skips get-only props — already broke `EdhTop16Client` once); all DDL constants and C# raw-string literals byte-preserved (CLAUDE.md formatting rule)
  5. `IWhisperSpendLedger.GetMonthlyTotalAsync(yearMonth)` returns app-side aggregate over `whisper_spend_ledger.month_key` for both dialects; cap-gate logic stubbed for Phase 5/6 to wire
**Plans**: TBD

### Phase 5: Content KB Outbound HTTP Services
**Goal**: Four new upstream surfaces (YouTube transcript, podcast RSS + audio, Whisper transcription, LLM summary + tag inference) are wired through the project's IHttpClientFactory + RestSharp + named-Polly pattern with proven third-party caption coverage and Structured-Outputs reliability, so the orchestrator in Phase 6 can compose them safely.
**Depends on**: Phase 4 (stores must exist for transcript/summary/spend persistence)
**Requirements**: KB-03 (YouTube), KB-04 (Whisper), KB-06 (LLM summary + clips), KB-07 (tag inference)
**Success Criteria** (what must be TRUE):
  1. **Pitfall 1 mitigation (P1):** `IYouTubeTranscriptFetcher` successfully fetches captions for 5 real third-party MTG channels (MTGGoldfish + The Command Zone + EDHRECast + Tolarian Community College + Playing With Power) via YoutubeExplode 6.6.0 — proven from the deployed Render environment, NOT WSL. `Google.Apis.YouTube.v3.captions.download` is NOT used (returns 403 on third-party content)
  2. **Pitfall 2 mitigation (P2):** `IYouTubeTranscriptFetcher` interface supports a proxy-pluggable abstraction from day 1 (toggleable via `DECKFLOW_YOUTUBE_TRANSCRIPT_PROVIDER` env var); pre-ship UAT harvests 5 videos from deployed Render env and asserts `whisper_fallback_ratio < 25%`; structured log emits `transcript_source` field on every fetch
  3. `IWhisperTranscriptionService` invokes OpenAI 2.10 `AudioClient` via `HttpClientPipelineTransport(httpClient)` seam (stays inside `IHttpClientFactory` lifecycle); files >24MB are chunked client-side via ffmpeg before upload (Dockerfile `apt-get install -y ffmpeg` verified at phase start per Pitfall 7); HttpClient timeout = 15min, Polly timeout = 12min
  4. **Pitfall 4 mitigation (P4):** `ILlmSummarizationService` uses OpenAI Structured Outputs (`response_format: json_schema`, `strict: true`) for summaries + clip extraction; tag inference rejects LLM-emitted tags outside `static class ContentTagVocabulary` allowlist (3 dimensions: archetype/strategy ~15 values, format/bracket per Wizards Feb 2025, card_category) with WARN log; staged-pipeline persistence (transcript → summary → tags each own row + status) so resume never re-Whispers
  5. Every new HTTP service follows the established convention: named `IHttpClientFactory` client (`youtube`, `podcast-rss`, `podcast-audio`, `openai`, `whisper-api`) + matching named Polly pipeline via `ResiliencePipelineProvider<string>`; public DI ctor + `internal` test ctor with `Func<...>` delegate seam per `CardLookupService.cs:106-121`; ZERO `new HttpClient()`; NO migration to `Microsoft.Extensions.Http.Resilience` standard handler
  6. New API keys (`OPENAI_API_KEY`) configured in Render dashboard with `sync: false`; Gitleaks pre-commit hook OR push-protection enabled (Pitfall 15); zero secrets in commits
**Plans**: TBD

### Phase 6: Content KB Orchestrator + Harvest Runs
**Goal**: An admin trigger composes the Phase 5 HTTP services into an end-to-end content harvest run with TOCTOU-safe Whisper spend cap-gating, idempotent re-runs, and partial-success persistence, so the user story "admin clicks Trigger Harvest and gets transcripts + summaries + tags without double-spending the cap" is verifiable end-to-end.
**Mode**: mvp (user-story-first verification: admin can trigger end-to-end harvest)
**Depends on**: Phase 4 (stores), Phase 5 (HTTP services)
**Requirements**: KB-02 (trigger + history runtime), KB-05 (cap-gate runtime — TOCTOU lock + kill-switch), KB-09 (`content_kb_enabled` flag gate + CSRF wiring at orchestrator boundary)
**Success Criteria** (what must be TRUE):
  1. **Pitfall 3 mitigation (P3):** Concurrent test — 5 parallel `POST /Admin/ContentHarvest/Trigger` against stub Whisper client asserts ≤1 harvest run row created AND ≤N seconds billed (N = cap); Postgres `pg_try_advisory_lock(hashtext('whisper-cap-' || to_char(now() at time zone 'UTC', 'YYYY-MM')))` acquired BEFORE any Whisper call; SERIALIZABLE transaction wraps check-and-insert; `DECKFLOW_WHISPER_KILL_SWITCH=true` env var aborts harvest as the very first check
  2. `IContentHarvestOrchestrator.RunAsync()` flow: enabled sources → list videos → for new videos persist `content_videos` → captions first (free) → fallback to Whisper ONLY if captions absent AND `IWhisperSpendLedger.WouldExceedCapAsync(estimate, ct)` returns false → summarize → clip → tag → mark video `transcript_status` (`captions` | `whisper` | `failed` | `skipped_over_cap`); resumable mid-batch without re-Whispering already-transcribed videos
  3. Whisper cap-hit aborts harvest with `aborted_reason='whisper_cap_hit'`; admin sees abort row + reason in run history; `transcript_status='skipped_over_cap'` set on skipped videos; estimate uses duration metadata × $0.006/min BEFORE the API call
  4. **Pitfall 6 mitigation (P6):** NO Postgres connection held across `await` for HTTP calls — every store call follows acquire-do-release pattern; `Maximum Pool Size=10-15` in connection string; single-worker hosted harvest; smoke test asserts 20 concurrent `/feedback` POSTs succeed during a 5-video harvest
  5. `content_kb_enabled` `IFeatureFlagStore` flag (default OFF) gates the orchestrator entry point; orchestrator returns 503 when flag is off; flag flipped only after first admin UAT verifies end-to-end harvest from deployed Render
**Plans**: TBD

### Phase 7: Content KB Admin UI
**Goal**: Admin has a complete UI surface — source CRUD, harvest history + per-run drill-down, and spend dashboard — for operating the Content Knowledge Base, inheriting Phase 3's responsive admin shell and the Phase 1 modal primitive.
**Depends on**: Phase 3 (responsive admin CSS), Phase 4 (schema), Phase 5 (services), Phase 6 (orchestrator)
**Requirements**: KB-01 (CRUD UI half), KB-02 (history UI half), KB-08 (spend dashboard), KB-09 (CSRF + flag gate on UI surface)
**Success Criteria** (what must be TRUE):
  1. Admin can create/edit/disable YouTube channel + podcast RSS sources via `/Admin/ContentSources` (Index/Create/Edit); source-type dropdown reuses v1.3 WDG-02 `df-select` ARIA combobox; delete confirmation reuses the Phase 1 `_AdminConfirmModal` partial; empty state CTA for zero-source first-run
  2. Admin can trigger manual harvest via `POST /Admin/ContentHarvest/Trigger` (returns 202 with run id, live-region announces "harvest started"); `GET /Admin/ContentHarvest` lists run history; `GET /Admin/ContentHarvest/{id}` drills into per-run sources processed, videos processed, transcripts fetched, Whisper calls, spend USD, abort reason
  3. `/Admin/ContentSpend` shows current month + last 6 months Whisper + LLM aggregate (per-provider breakdown via `provider` + `kind` columns), and surfaces an inline "approaching cap" warning when current month consumed >80% of `DECKFLOW_WHISPER_MONTHLY_CAP_USD`
  4. **Pitfall 11 mitigation (P11):** Every `/Admin/Content*` POST carries `[ValidateAntiForgeryToken]` AND `SameOriginRequestValidator.IsValid(Request)`; CI grep gate: `grep -L 'ValidateAntiForgeryToken' DeckFlow.Web/Controllers/Admin/AdminContent*Controller.cs` returns empty
  5. New sidebar nav entries ("Content Sources", "Content Harvest", "Content Spend") added to `_AdminLayout.cshtml`; all new views render correctly at 375px mobile viewport per Phase 3 invariants; zero CSS bleed into 22 guild themes
**Plans**: TBD
**UI hint**: yes

### Phase 8: Doc-Comment Backfill — Part 2 + Strip NoWarn
**Goal**: All remaining ~38 v1.1-era undocumented Web types AND every new v1.4 type (DOC + KB clusters) carry XML `<summary>` doc-comments, and `NoWarn 1591;1573;1587` is stripped from `DeckFlow.Web.csproj` with the warnings-as-future-gate enabled. Lands LAST so v1.4 new types are documented before the gate flips (Pitfall 8 sequencing).
**Depends on**: Phase 2 (Part 1 doc-comment foundation), Phase 7 (final v1.4 surface complete — all new admin controllers + content services + viewmodels exist and need docs)
**Requirements**: DOC-01 (finish all remaining types), DOC-02 (strip NoWarn from csproj)
**Success Criteria** (what must be TRUE):
  1. **Pitfall 8 ordering:** ALL public types under `DeckFlow.Web/{Controllers,Services,Models,Models/Api,Infrastructure,Security,ViewModels}/` (the original ~88 + every new v1.4 type from Phases 1, 3, 5, 6, 7) carry `<summary>` doc-comments BEFORE the NoWarn strip — verified by anchored grep returning empty: `grep -L '<summary>' $(grep -rlE '^public (sealed )?(class|record|interface)' DeckFlow.Web --include='*.cs' | grep -v '/obj/')`
  2. `DeckFlow.Web.csproj` `<NoWarn>$(NoWarn);1591;1573;1587</NoWarn>` line removed (1591 may be retained scoped only to compiler-generated Razor partials via `Condition=` if `dotnet build -warnaserror:CS1591 -p:GenerateDocumentationFile=true` from clean `obj/` proves Razor-generated partials still emit CS1591)
  3. `dotnet build -c Release -warnaserror:CS1591` succeeds from clean `obj/` — 0 errors, 0 warnings on user-authored code
  4. Test suite preserved at `Failed: 0` (doc-comment edits cannot regress runtime but verify the gate); touch-only-what-you-touch discipline preserved across every backfilled file (CLAUDE.md R-6)
**Plans**: TBD

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. WDG-04 Focus-Trapped Modal | 0/1 | Planned | - |
| 2. Doc-Comment Backfill Part 1 | 0/TBD | Not started | - |
| 3. Admin Mobile-Responsive Sweep | 0/TBD | Not started | - |
| 4. Content KB Foundation — Stores + Schema | 0/TBD | Not started | - |
| 5. Content KB Outbound HTTP Services | 0/TBD | Not started | - |
| 6. Content KB Orchestrator + Harvest Runs | 0/TBD | Not started | - |
| 7. Content KB Admin UI | 0/TBD | Not started | - |
| 8. Doc-Comment Backfill Part 2 + Strip NoWarn | 0/TBD | Not started | - |

**Critical path:** Phase 1 → Phase 3 → Phase 4 → Phase 5 → Phase 6 → Phase 7
**Off critical path:** Phase 2 (parallelizable with Phases 1/3/4/5/6/7), Phase 8 (lands last after all v1.4 surface exists)
**Pre-allocated for ship-gate:** A `999.x` test-hardening backlog phase MAY be inserted before milestone-ship per `no-ship-failing-tests` rule (R-5) if any residual failures surface.

## Backlog

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

### Deferred to v1.5 (per 2026-05-23 scope decision)

- **Gemini paste-limit workaround** (cluster D dropped from v1.4; needs split-message vs direct-API path decision)
- **Content KB deck-analysis integration** — prompt-injection + "What experts say" UI panel
- **New-deck-building interactive guide** (wizard) leveraging Content KB tags
- **Scheduled (cron) content harvest cadence**
- IN-01 `_AiSelector` vs view-level Normalize Gemini-flag fallback divergence
- v1.1 phase-dir archive move (06, 07, 07.1, 08 → `.planning/milestones/v1.1-phases/`)
- CSS-class / data-attribute / TS-constant `chatgpt-*` cleanup
- v13-harvest-worker-stalled debug follow-up
- audit-open scanner vocabulary alignment

---

*v1.0 shipped 2026-05-02 | v1.1 shipped 2026-05-08 | v1.2 shipped 2026-05-13 | v1.3 shipped 2026-05-23 | v1.4 started 2026-05-23 (phase numbering reset)*
