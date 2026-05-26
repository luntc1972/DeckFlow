# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- 🚧 **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** — Phases 16-25 (started 2026-05-23)

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

**Phase numbering:** continuous global sequence resumed at Phase 16 (v1.4 = Phases 16-25; v1.3 phase dirs archived to `.planning/milestones/v1.3-phases/`).
**Scope:** 18 REQ-IDs across 6 clusters (MODAL, DOC, AMOB, KB, CAT, AHD). Gemini cluster D dropped per user decision 2026-05-23 → v1.5. CAT bug-fix + AHD admin grid added mid-milestone 2026-05-24 per user request.

- [x] **Phase 16: WDG-04 Focus-Trapped Modal** — Close v1.3 carry-over: replace deferred `onsubmit` confirm in AdminFeedback/Detail with native `<dialog>` focus-trapped modal (completed 2026-05-24; UAT passed; tests 520/3/523; MODAL-01 satisfied)
- [x] **Phase 17: Doc-Comment Backfill — Part 1 (Controllers + Services)** — Backfill XML `<summary>` doc-comments on ~50 of 88 v1.1-era Web types; NoWarn stays in place until Phase 23 (completed 2026-05-24)
- [x] **Phase 18: Admin Mobile-Responsive Sweep** — Factor `admin.css` → `admin-common.css` + `admin-mobile.css` + import shim; sidebar collapse, table strategies, ≥44px touch targets — all scoped to `.admin-shell`
<!-- EXECUTION ORDER ≠ phase-number order (reordered 2026-05-24 per user: Content KB last).
     Phase numbers are stable IDs tied to existing plan dirs/files — NOT renumbered.
     Execute in the listed order below: 25 → 24 → 19 → 20 → 21 → 22 → 23.
     Content KB (19-22) is the final feature block; Phase 23 (strip NoWarn) stays
     last by hard dependency on Phase 22 (must document all new KB types first). -->
- [x] **Phase 25: Admin Harvested-Decks Paged Grid** — Replace admin top-ten-decks list with server-side paged grid over all harvested decks (AHD-01) — *exec #1 (plans Codex-approved)* (completed 2026-05-24)
- [x] **Phase 24: Card Category Lookup Fix — Colorless/Staple Cards** — Bug: category suggestion returns nothing for Sol Ring (colorless artifact ramp staple); investigate with Archidekt harvest service running AND stopped; restore category results (CAT-01) — *exec #2* — **DONE 2026-05-25 (live smoke passed)**
- [ ] **Phase 19: Content KB Foundation — Local Schema + Contracts** — local-harvester SQLite schema (sources/videos/transcripts/spend-log/runs) + `DeckFlow.Core` distill models + AI-prompt artifact file-format spec + slim site-index schema contract; zero outbound HTTP *(re-scoped 2026-05-26: local-harvester model)* — *exec #3*
- [ ] **Phase 20: Content KB Ingestion + Transcription (local)** — YouTube (YoutubeExplode) + Podcast (Syndication) + Whisper (OpenAI 2.10) fallback + plain local spend-log cap check; named HttpClients + Polly pipelines, run locally — *exec #4*
- [ ] **Phase 21: Content KB Distillation + Artifact Emit (local)** — LLM summary + timestamped clips + controlled-vocab tags (OpenAI Structured Outputs) → emit AI-prompt artifact files + slim-index rows; simple local end-to-end orchestration (no advisory lock) — *exec #5*
- [ ] **Phase 22: Content KB Site Integration** — slim index table materialized on Render + browse/filter display + artifact upload-or-serve; CSRF-guarded uploads; `content_kb_enabled` display-gate flag — *exec #6*
- [ ] **Phase 23: Doc-Comment Backfill — Part 2 + Strip NoWarn** — Remaining ~38 types + new v1.4 surface; LAST step strips `NoWarn 1591;1573;1587` from `DeckFlow.Web.csproj` — *exec #7 (depends on Phase 22)*
- [ ] **Phase 26: Category Cache Schema Normalization (fresh-start)** — Normalize repeated deck/card TEXT into integer-keyed dimensions + compact indexes; full DB reset + re-harvest into new schema (no online migration) (DBO-01) — *off critical path; sequence before Phase 27*
- [x] **Phase 27: Deck-Cache Content-Hash Dedup + 5-Day Refresh** — Skip rewriting a deck's rows when cards/categories unchanged (content hash) + re-check after 5 days (CAT-02) — *off critical path; depends on Phase 26* (completed 2026-05-26)

## Phase Details

### Phase 16: WDG-04 Focus-Trapped Modal

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

- [ ] 16-01-PLAN.md — Native <dialog> showConfirm primitive + structural partial + scoped CSS + Detail.cshtml wire-up (MODAL-01)

### Phase 17: Doc-Comment Backfill — Part 1 (Controllers + Services)

**Goal**: Documentation debt is reduced on the highest-traffic Web surface (~50 of 88 v1.1-era types) without flipping the NoWarn gate, so the warning suppression stays in place until Phase 23 lands the rest of the v1.4 surface.
**Depends on**: Nothing (parallelizable; off critical path)
**Requirements**: DOC-01 (partial — Controllers + Services subset)
**Success Criteria** (what must be TRUE):

  1. Every public type under `DeckFlow.Web/Controllers/`, `Controllers/Admin/`, `Controllers/Api/`, `Services/`, and `Services/Http/` carries an ATTACHED doc-comment — either a `<summary>` (interfaces, standalone classes, records, enums) OR an `<inheritdoc/>` (interface-implementation classes per D-01). PER-DECLARATION verification (authoritative): for each in-scope file, every public type DECLARATION must have a `///` comment containing `<summary>` or `<inheritdoc` on the line(s) immediately above it (skipping attribute lines, NOT blank lines) — a comment elsewhere in the file does NOT count. This blind spot is real: `DeckFlowDatabaseConnectionFactory.cs` PASSED the file-level grep (its methods carry summaries) while its TYPE was undocumented, and `ScryfallCard`'s summary was detached by a blank line. The legacy file-level grep `grep -L '<summary>' ...` is a NON-BLOCKING smoke check only: it false-flags inheritdoc-only impls (e.g. `FeedbackStore.cs` documents via `<inheritdoc/>` with no `<summary>` string) and false-passes files with co-located gaps. The per-declaration awk gate is the gate that must pass.
  2. `<param>` + `<returns>` tags on non-trivial public methods (≥2 real params — trailing CancellationToken excluded — OR non-obvious return per D-02); `<inheritdoc/>` on interface-implementation classes AND each of their public members (member-level, not just the type)
  3. `NoWarn 1591;1573;1587` REMAINS in `DeckFlow.Web.csproj` (do not strip until Phase 23); `dotnet build -c Release` stays at 0 warnings / 0 errors
  4. Touch-only-what-you-touch discipline preserved (no Format Document, no `{ get; }` mutations, no `[Attribute]` inlining, no raw-string re-indents per CLAUDE.md R-6)

**Plans**: 2 plans
Plans:

- [x] 17-01-PLAN.md — Controllers cluster: per-type <summary> on 7 undocumented public types across 5 files (CommanderController, FeedbackController, Admin/AdminFeedback {enum+VM+controller — 3 co-located types}, Api/Suggestions, Api/ArchidektCacheJobs) (DOC-01)
- [x] 17-02-PLAN.md — Services cluster: D-01 inheritdoc split on 4 interface/impl pairs (EdhTop16Client, CategoryKnowledgeStore, FeedbackStore, ScryfallSetService) + D-04 summaries on 3 records (ScryfallCardFace, FeedbackRequestContext, ScryfallCard) + D-01a type-level summary on DeckFlowDatabaseConnectionFactory static class; 12 types / 8 files (DOC-01)

**Cross-cutting constraints:**

- NoWarn 1591;1573;1587 still present in DeckFlow.Web.csproj (csproj untouched)
- dotnet build -c Release stays at 0 warnings / 0 errors
- Test suite stays Failed:0 (no runtime change)

> Scope note: 2026-05-24 re-scan found 16 undocumented types remain (15 class/record/interface + 1 enum) — the original "~50 of 88" Goal snapshot was stale; most listed types were already documented in prior sessions. 2026-05-24 (checker round 2): added `DeckFlowDatabaseConnectionFactory` to 17-02 — it was in the CONTEXT undocumented-type inventory but slipped past the file-level SC1 grep because its file already had method-level summaries; SC1 + both plans' acceptance criteria tightened to per-TYPE (declaration-anchored) checking. 2026-05-24 (Codex review rounds): added `ScryfallCard` (its existing summary was detached by a blank line → effectively undocumented) and enumerated member-level D-02/inheritdoc coverage across all 4 interface/impl pairs; authoritative gate is the per-declaration awk checker accepting attached `<summary>` OR `<inheritdoc/>` (file-level `grep -L '<summary>'` is non-blocking — it false-flags inheritdoc-only impls like FeedbackStore). Current authoritative count ≈19 declaration-attached gaps (7 in 17-01 + 12 in 17-02), superseding the stale 45 in 17-CONTEXT.md. NoWarn gate retained (Phase 23 strips it).

### Phase 18: Admin Mobile-Responsive Sweep

**Goal**: Admin shell renders correctly and accessibly on viewports ≥320px wide, with all WCAG 2.5.5 ≥44px touch-target floors met, AND zero CSS regression on the 22 guild themes.
**Depends on**: Phase 16 (modal CSS lands in `admin-common.css` factoring — doing modal after split forces touching two files)
**Requirements**: AMOB-01, AMOB-02, AMOB-03, AMOB-04
**Success Criteria** (what must be TRUE):

  1. `admin.css` is factored into `wwwroot/css/admin-common.css` (layout primitives mirroring `site-common.css` role) + `wwwroot/css/admin-mobile.css` (`@media (max-width: 768px)` rules) + `admin.css` import shim; all new admin selectors are scoped to `.admin-shell` parent class OR wrapped in `@layer admin { ... }` cascade discipline (Pitfall 10)
  2. Sidebar collapses to `<details>`/`<summary>` disclosure below 768px with no-JS fallback; `_AdminLayout.cshtml` markup updated; keyboard + screen-reader operable
  3. Admin tables remain usable on narrow viewports — per-table choice between `overflow-x: auto` (Analytics, HarvestRuns, ContentHarvest — comparison-dense) with `tabindex="0"` for keyboard pan, and card-stack pattern (Feedback list, ContentSources list — scanning); rationale documented per-table
  4. All admin interactive elements (buttons, links, icon-actions, form controls) meet ≥44×44px touch-target floor at narrow viewport; admin forms render single-column ≤768px
  5. Visual regression: homepage + `/sync` photographed in Rakdos + Azorius + Boros + Gruul guild themes at 375px mobile viewport BEFORE and AFTER the sweep — zero pixel diff on non-admin pages (Pitfall 10 detection)

**Plans**: 2 plans
Plans:

- [x] 18-01-PLAN.md — Factor admin.css → admin-common.css + admin-mobile.css + fallback shim; switch _AdminLayout to TWO fingerprinted <link> tags (D-CACHE); migrate dead admin-feedback rules from site-common.css with admin-token substitution + .admin-shell scoping (D-SCOPE); add --danger/--on-accent tokens, the FULL admin interactive-element ≥44px touch-target inventory (WCAG 2.5.5 AAA floor), admin-harvest__panel styling, accessible card-stack header contract (D-A11Y-HEADER — clip not display:none), sidebar/overflow-x CSS contracts (AMOB-04, AMOB-03, AMOB-02, AMOB-01)
- [x] 18-02-PLAN.md — Wire Razor markup to the CSS contracts: sidebar <details>/<summary> disclosure rendered WITHOUT open (collapsed-by-default, D-OPEN), overflow-x scroll regions on comparison tables (Harvest ×2, Analytics, ASCII-hyphen aria-labels), card-stack + data-label on scanning tables (Feedback, Flags) retaining <th scope=col>; static dead-class scan; blocking human-verify at 320/375/768/769px + 4-theme non-admin zero-visible-diff regression (AMOB-01, AMOB-02, AMOB-03)

**UI hint**: yes

### Phase 19: Content KB Foundation — Local Schema + Contracts

> **Re-scoped 2026-05-26** — pivoted to a local-harvester + file-artifact + slim-site-index model. Harvest runs LOCALLY against local SQLite; only a slim index + the display gate land on Render. See REQUIREMENTS.md KB-section note + STATE.md pivot note.

**Goal**: The persistence and contract foundation for the local Content KB harvester is materialized — a local SQLite schema (via `EnsureSchemaAsync` mirroring `HarvestRunStore.cs:436-471`) for sources/videos/transcripts/spend-log/runs, `DeckFlow.Core` distillation record models, the AI-prompt **artifact file-format spec**, and the **slim site-index schema contract** — so the ingestion, distillation, and site-integration phases build on stable shapes. Zero outbound HTTP.
**Depends on**: Nothing within v1.4
**Requirements**: KB-01 (local `content_sources` schema), KB-02 (local `content_harvest_runs` schema), KB-04 (`content_transcripts` schema), KB-05 (`whisper_spend_ledger` local spend-log schema), KB-06 (artifact file-format spec + distill models), KB-07 (`ContentTagVocabulary` + tag schema), KB-08 (slim site-index schema contract)
**Decisions locked in discuss (2026-05-26)**:

  - **PK strategy**: integer surrogate (`BIGINT GENERATED ALWAYS AS IDENTITY` / SQLite `INTEGER PRIMARY KEY`), matching Phase 26 normalization; natural keys (`youtube_video_id`, `rss_guid`) get a UNIQUE for harvest dedup
  - **FK behavior**: `ON DELETE CASCADE` on child FKs (transcripts/summaries/clips/tags → videos; videos → sources). **Landmine**: SQLite enforces FK only with `PRAGMA foreign_keys=ON` per connection — verify the connection factory sets it, else SQLite silently ignores cascades while Postgres enforces
  - **Source disable**: soft-disable via `content_sources.is_enabled BOOLEAN NOT NULL DEFAULT true`; harvest filters `WHERE is_enabled`; hard-delete is a separate rare op that triggers CASCADE; transcript/spend history survives disable
  - **Store granularity**: grouped-by-aggregate (~4 stores: `ContentSourceStore`, `ContentVideoStore` over videos+transcripts+summaries+clips+tags, `WhisperSpendLedger`, `ContentHarvestRunStore`); each store owns its `EnsureSchemaAsync` + own xUnit fixture

**Success Criteria** (what must be TRUE):

  1. Local SQLite schema bootstrap-tested via per-store `EnsureSchemaAsync` (idempotent `CREATE TABLE IF NOT EXISTS`): `content_sources`, `content_videos`, `content_transcripts`, `content_summaries`, `content_clips`, `content_tags`, `whisper_spend_ledger`, `content_harvest_runs` — all created with expected columns + integer surrogate PKs + natural-key UNIQUE + CASCADE FKs (Postgres parity preserved in DDL for the slim-index subset that later ships to Render)
  2. The **slim site-index schema contract** is defined (source/title/url/tags → artifact pointer) as the only shape destined for Render Postgres; heavy tables (transcripts/audio/spend) are explicitly local-only
  3. The **AI-prompt artifact file-format spec** is defined (markdown/text layout for summary + timestamped clips + tags) and documented so Phase 21 emit and Phase 22 site rendering agree by contract
  4. Each store has its own xUnit fixture using own SQLite file OR `:memory:` per-fact scope (F-PROD-CONTRACT 999.6 lesson honored); store tests pass with `Failed:0`
  5. All new record types preserve `{ get; init; }` properties (System.Text.Json silently skips get-only props — already broke `EdhTop16Client` once); all DDL constants and C# raw-string literals byte-preserved (CLAUDE.md formatting rule)
  6. `IWhisperSpendLedger.GetMonthlyTotalAsync(yearMonth)` returns app-side aggregate over the local spend-log `month_key`; plain cap-check helper stubbed for Phase 20 to wire (NO TOCTOU advisory-lock machinery)

**Plans**: 4 plans
Plans:

- [x] 19-01-PLAN.md — DeckFlow.Core/Knowledge contract layer: content record models + ContentTagVocabulary allowlist + AI-prompt artifact file-format spec + slim-index/artifact DTOs (KB-06, KB-07)
- [x] 19-02-PLAN.md — Storage foundation: RelationalDatabaseConnection.OpenConnectionAsync FK-pragma seam (D-03) + split connection factory (CreateLocalContentKbConnection always-SQLite / CreateContentSiteIndexConnection provider-aware, protects D-14) + FK/CASCADE proof at connection layer (KB-01, KB-04)
- [ ] 19-03-PLAN.md — ContentSourceStore (content_sources) + ContentVideoStore aggregate (videos+transcripts+summaries+clips+tags) with CASCADE FKs + D-04 CASCADE proof test (KB-01, KB-04, KB-07)
- [ ] 19-04-PLAN.md — WhisperSpendLedger (+ env-cap stub, KB-05) + ContentHarvestRunStore (KB-02) + slim ContentSiteIndexStore Render-bound index (KB-08)

### Phase 20: Content KB Ingestion + Transcription (local)

**Goal**: The local harvester's upstream surfaces (YouTube captions, podcast RSS + audio, Whisper transcription) are wired through the project's IHttpClientFactory + RestSharp + named-Polly pattern with proven third-party caption coverage, plus a plain local spend-log cap check — so Phase 21 can compose them into end-to-end distillation. All run locally; nothing executes on Render.
**Depends on**: Phase 19 (local schema + spend-log shape must exist for transcript/spend persistence)
**Requirements**: KB-03 (YouTube), KB-04 (Whisper runtime), KB-05 (local cap-check runtime)
**Success Criteria** (what must be TRUE):

  1. **Pitfall 1 mitigation (P1):** `IYouTubeTranscriptFetcher` successfully fetches captions for 5 real third-party MTG channels (MTGGoldfish + The Command Zone + EDHRECast + Tolarian Community College + Playing With Power) via YoutubeExplode 6.6.0 — proven from the **local** harvester environment. `Google.Apis.YouTube.v3.captions.download` is NOT used (returns 403 on third-party content)
  2. **Pitfall 2 mitigation (P2):** `IYouTubeTranscriptFetcher` supports a proxy-pluggable abstraction from day 1 (toggleable via `DECKFLOW_YOUTUBE_TRANSCRIPT_PROVIDER`); a local UAT harvests 5 videos and asserts `whisper_fallback_ratio < 25%`; structured log emits `transcript_source` on every fetch
  3. `IWhisperTranscriptionService` invokes OpenAI 2.10 `AudioClient` via `HttpClientPipelineTransport(httpClient)` seam (stays inside `IHttpClientFactory` lifecycle); files >24MB chunked client-side via ffmpeg before upload (local ffmpeg available — verified at phase start per Pitfall 7); HttpClient timeout = 15min, Polly timeout = 12min
  4. **Plain local cap check:** before any Whisper call, projected monthly total (existing local spend-log sum + duration × $0.006/min estimate) is compared to `DECKFLOW_WHISPER_MONTHLY_CAP_USD` (default $15.00); over-cap skips the call and marks the video `skipped_over_cap`. NO `pg_try_advisory_lock`, NO SERIALIZABLE wrapper, NO kill-switch — single-user local run
  5. Every new HTTP service follows the established convention: named `IHttpClientFactory` client (`youtube`, `podcast-rss`, `podcast-audio`, `openai`, `whisper-api`) + matching named Polly pipeline via `ResiliencePipelineProvider<string>`; public DI ctor + `internal` test ctor with `Func<...>` delegate seam per `CardLookupService.cs:106-121`; ZERO `new HttpClient()`; NO migration to `Microsoft.Extensions.Http.Resilience` standard handler
  6. `OPENAI_API_KEY` read from local environment/secrets (NOT committed); zero secrets in commits

**Plans**: TBD

### Phase 21: Content KB Distillation + Artifact Emit (local)

**Goal**: The local harvester composes the Phase 20 ingestion services into an end-to-end run that distills each video into an AI-prompt artifact file + slim-index rows — LLM summary + timestamped clips + controlled-vocab tags via Structured Outputs — so the user story "I run the harvester locally over my source list and get pasteable prompt artifacts + an index ready for the site" is verifiable end-to-end. Source add/edit/disable management lives here too.
**Mode**: mvp (user-story-first verification: local run → artifacts + index rows)
**Depends on**: Phase 19 (schema + artifact spec), Phase 20 (ingestion services)
**Requirements**: KB-01 (source mgmt runtime), KB-02 (orchestration + local run record), KB-06 (LLM summary + clips → artifacts), KB-07 (tag inference + emit)
**Success Criteria** (what must be TRUE):

  1. **Pitfall 4 mitigation (P4):** `ILlmSummarizationService` uses OpenAI Structured Outputs (`response_format: json_schema`, `strict: true`) for summaries + clip extraction; tag inference rejects LLM-emitted tags outside `static class ContentTagVocabulary` allowlist (3 dimensions: archetype/strategy ~15 values, format/bracket per Wizards Feb 2025, card_category) with WARN log
  2. `IContentHarvestOrchestrator.RunAsync()` flow: enabled sources → list videos → for new videos persist `content_videos` → captions first (free) → Whisper fallback ONLY if captions absent AND local cap check passes → summarize → clip → tag → emit artifact file + slim-index row → mark video `transcript_status` (`captions` | `whisper` | `failed` | `skipped_over_cap`); staged-pipeline persistence (each stage own row + status) so a resumed run never re-Whispers or re-distills completed videos
  3. Each completed video produces a valid **AI-prompt artifact file** (per Phase 19 spec) at the defined repo/`/data` location AND a slim-index row; a local run summary row is written to `content_harvest_runs` (sources/videos/transcripts/Whisper-calls/spend/abort-reason)
  4. Source add/edit/disable is operable from the harvester (CLI verb or app action) and respected by the next run (`is_enabled` filter)
  5. Cap-hit (or `--dry-run`) aborts cleanly with `aborted_reason` recorded; over-cap videos marked `skipped_over_cap`; partial runs leave a consistent local DB

**Plans**: TBD

### Phase 22: Content KB Site Integration

**Goal**: The site surfaces the distilled Content KB — a slim index table materialized on Render Postgres for browse/filter, the prompt artifacts served (committed-in-repo or uploaded to `/data`), behind a feature flag — inheriting Phase 18's responsive admin shell and the Phase 16 modal primitive.
**Depends on**: Phase 18 (responsive admin CSS), Phase 19 (slim-index schema contract + artifact spec), Phase 21 (artifacts + index rows produced locally)
**Requirements**: KB-08 (slim index on Render + browse/filter display), KB-09 (display-gate flag + CSRF on upload)
**Success Criteria** (what must be TRUE):

  1. The slim index schema (from Phase 19 contract) is materialized on Render Postgres via `EnsureSchemaAsync`; locally-produced index rows can be loaded onto Render (artifact upload OR commit-then-deploy path), with NO transcript/audio/spend data uploaded
  2. A browse/filter surface lists indexed content (by source / archetype / bracket / card_category tags) and links each entry to its prompt artifact, rendered for the ChatGPT paste workflow; empty state CTA for zero-content first-run
  3. Prompt artifacts are served correctly whether committed to the repo (like `prompt-templates/`) or uploaded to `/data`; the chosen path is documented
  4. **Pitfall 11 mitigation (P11):** any artifact-upload POST carries `[ValidateAntiForgeryToken]` AND `SameOriginRequestValidator.IsValid(Request)`; CI grep gate returns empty for unguarded upload actions
  5. The display surface is gated behind `content_kb_enabled` `IFeatureFlagStore` flag (default OFF, flipped after first UAT verifies browse + artifact rendering); all new views render correctly at 375px mobile viewport per Phase 18 invariants; zero CSS bleed into 22 guild themes

**Plans**: TBD
**UI hint**: yes

### Phase 23: Doc-Comment Backfill — Part 2 + Strip NoWarn

**Goal**: All remaining ~38 v1.1-era undocumented Web types AND every new v1.4 type (DOC + KB clusters) carry XML `<summary>` doc-comments, and `NoWarn 1591;1573;1587` is stripped from `DeckFlow.Web.csproj` with the warnings-as-future-gate enabled. Lands LAST so v1.4 new types are documented before the gate flips (Pitfall 8 sequencing).
**Depends on**: Phase 17 (Part 1 doc-comment foundation), Phase 22 (final v1.4 surface complete — all new admin controllers + content services + viewmodels exist and need docs)
**Requirements**: DOC-01 (finish all remaining types), DOC-02 (strip NoWarn from csproj)
**Success Criteria** (what must be TRUE):

  1. **Pitfall 8 ordering:** ALL public types under `DeckFlow.Web/{Controllers,Services,Models,Models/Api,Infrastructure,Security,ViewModels}/` (the original ~88 + every new v1.4 type from Phases 16, 18, 20, 21, 22) carry `<summary>` doc-comments BEFORE the NoWarn strip — verified by anchored grep returning empty: `grep -L '<summary>' $(grep -rlE '^public (sealed )?(class|record|interface)' DeckFlow.Web --include='*.cs' | grep -v '/obj/')`. Note: this file-level grep shares the co-located-type blind spot surfaced in Phase 17 — the authoritative gate here is the `dotnet build -warnaserror:CS1591` in SC3, which fails per-type on any undocumented public type once NoWarn is stripped.
  2. `DeckFlow.Web.csproj` `<NoWarn>$(NoWarn);1591;1573;1587</NoWarn>` line removed (1591 may be retained scoped only to compiler-generated Razor partials via `Condition=` if `dotnet build -warnaserror:CS1591 -p:GenerateDocumentationFile=true` from clean `obj/` proves Razor-generated partials still emit CS1591)
  3. `dotnet build -c Release -warnaserror:CS1591` succeeds from clean `obj/` — 0 errors, 0 warnings on user-authored code
  4. Test suite preserved at `Failed: 0` (doc-comment edits cannot regress runtime but verify the gate); touch-only-what-you-touch discipline preserved across every backfilled file (CLAUDE.md R-6)

**Plans**: TBD

### Phase 24: Card Category Lookup Fix — Colorless/Staple Cards

**Goal**: Card category suggestion reliably returns categories for staple cards that must always resolve; the Sol Ring regression (colorless artifact ramp staple currently returning no categories) is fixed without regressing existing coverage.
**Depends on**: Nothing (off critical path; independent bug fix)
**Requirements**: CAT-01
**Status note (2026-05-24)**: Captured for investigation-later per user. The Archidekt harvest/cache job was running when the failure was observed — the running service is a suspected cause. Investigation MUST reproduce in BOTH states: harvest running AND harvest stopped.
**Success Criteria** (what must be TRUE):

  1. Root cause identified for Sol Ring returning empty categories, documented with whether the running harvest/cache job contributes — reproduced (or ruled out) in BOTH harvest-running and harvest-stopped states
  2. Category suggestion returns non-empty, correct categories for Sol Ring (e.g., Ramp / Artifact) and a small regression set of other colorless/staple cards
  3. No regression in existing category coverage for previously-working cards; affected path covered by a regression test
  4. Test suite preserved at `Failed: 0`; touch-only-what-you-touch (CLAUDE.md R-6)

**Plans**: No PLAN (routed through /gsd-debug — fix-in-place, Codex implemented / Claude reviewed).

**Verification status (2026-05-25): ✅ DONE.** Root cause = write-time `CategoryFilter.IsIncluded` dropped literal "Artifact"/"Artifacts" at both harvest write chokepoints → staple cards whose only Archidekt category is their card type got zero observation rows (reproduced in BOTH harvest-running and stopped states — SC1). Fix: removed write-time filter; added read-time `CategoryFilter.IncludedOrFallback` (hides generic buckets only when richer categories exist, else keeps the type label). Commits 14554a1 (RED) / 835c552 (fix) / 9ae049d (debug doc). **Live smoke 2026-05-25** (post Phase-26 reset + re-harvest): card-category AND commander-category lookups return correct non-empty categories in prod (SC2). Regression test added (SC3); build clean. Note: the cat-01 out-of-band index runbook is SUPERSEDED by the Phase 26 normalized schema (compact integer indexes built by EnsureSchema on clean DB).

### Phase 25: Admin Harvested-Decks Paged Grid

**Goal**: The admin harvested-decks surface shows ALL harvested decks via a server-side paged grid (page size, total count, scannable rows) instead of the current top-ten list, inheriting the responsive admin shell.
**Depends on**: Phase 18 (responsive admin shell — grid must be mobile-usable per AMOB patterns)
**Requirements**: AHD-01
**Success Criteria** (what must be TRUE):

  1. Admin harvested-decks view lists ALL harvested decks with server-side paging (not just top 10); page navigation + total count visible
  2. Paging is server-side (query-level LIMIT/OFFSET or keyset) — does not load all rows into memory (Render 512MB cap discipline)
  3. Grid reuses the responsive admin table/card patterns from Phase 18; usable at ≥320px viewport
  4. Test suite preserved at `Failed: 0`; touch-only-what-you-touch (CLAUDE.md R-6)
  5. `/Admin/Harvest` cold-cache load latency is reduced — the stats payload no longer relies on serial full-table scans (see perf note below)

**Perf investigation note (2026-05-24, captured per user during Phase 18 UAT):** `/Admin/Harvest` is slow on a COLD cache (warm loads are fine — `HarvestStatsAggregator` caches the payload 60s). The cold path runs 7 queries SEQUENTIALLY in `HarvestStatsAggregator.BuildAsync`, and the expensive ones are unindexed full scans that grow with harvest size:
  - `SELECT COUNT(1) FROM deck_queue WHERE processed = 1` — no index on `processed`
  - `SELECT COUNT(1) FROM deck_queue WHERE processed = 1 AND inserted_utc >= @cutoff` — no `(processed, inserted_utc)` index
  - `SELECT COUNT(1) FROM card_category_observations` — full COUNT of the largest table (slow in Postgres)
  - `GetTopCommandersAsync`: `GROUP BY commander_name ORDER BY deck_count DESC FROM deck_queue WHERE processed = 1` — full scan + group/sort, no supporting index
  Fix directions for Phase 25 (alongside the paged grid that replaces the top-ten `GROUP BY` scan): add indexes (`deck_queue(processed)`, `deck_queue(processed, inserted_utc)`, `deck_queue(processed, commander_name)`); parallelize the independent stat queries (`Task.WhenAll`) instead of awaiting serially; for the observation count prefer a Postgres `reltuples` estimate or a maintained counter over `COUNT(*)`; optionally precompute the stats payload at harvest-completion rather than on page view. Files: `DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs`, `DeckFlow.Web/Services/CategoryKnowledgeStore.cs`, `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs`.

**Plans**: 2 plans
Plans:

- [x] 25-01-PLAN.md — Data + service layer: paged deck query + 3 deck_queue indexes + reltuples count fast-path + Task.WhenAll parallelization + Fake/repo tests (AHD-01)
- [x] 25-02-PLAN.md — Admin UI: AdminHarvestViewModel paging fields + Index(int page) clamped fetch + paged Harvested Decks grid replacing the top-10 list (AHD-01)

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 16. WDG-04 Focus-Trapped Modal | 0/1 | Planned | - |
| 17. Doc-Comment Backfill Part 1 | 2/2 | Complete   | 2026-05-24 |
| 18. Admin Mobile-Responsive Sweep | 2/2 | Complete | 2026-05-24 |
| 19. Content KB Foundation — Local Schema + Contracts | 2/4 | In Progress|  |
| 20. Content KB Outbound HTTP Services | 0/TBD | Not started | - |
| 21. Content KB Orchestrator + Harvest Runs | 0/TBD | Not started | - |
| 22. Content KB Admin UI | 0/TBD | Not started | - |
| 23. Doc-Comment Backfill Part 2 + Strip NoWarn | 0/TBD | Not started | - |
| 24. Card Category Lookup Fix — Colorless/Staple Cards | 0/TBD | Not started | - |
| 25. Admin Harvested-Decks Paged Grid | 2/2 | Complete    | 2026-05-25 |
| 26. Category Cache Schema Normalization (fresh-start) | 2/2 | Implemented (live verify pending) | - |
| 27. Deck-Cache Content-Hash Dedup + 5-Day Refresh | 1/1 | Complete   | 2026-05-26 |

**Critical path:** Phase 16 → Phase 18 → Phase 19 → Phase 20 → Phase 21 → Phase 22
**Off critical path:** Phase 17 (parallelizable with Phases 16/18/19/20/21/22), Phase 23 (lands last after all v1.4 surface exists), Phase 24 (independent bug fix), Phase 25 (depends only on Phase 18 admin shell)
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

### Phase 26: Category Cache Schema Normalization (fresh-start)

**Goal:** Re-harvested category data lands in a normalized, integer-keyed schema that fits the 256 MB Postgres working set and serves card/commander lookups from compact indexes — replacing the wide TEXT-keyed `card_category_observations` / `card_deck_totals` design. Built fresh (DB wiped + re-harvested), so no in-place migration of existing rows.
**Requirements**: DBO-01
**Depends on:** Nothing (off critical path; fresh-start rebuild — full DB reset authorized 2026-05-24)
**Spec**: `.planning/research/db-storage-query-optimization.md`, `docs/ops/db-full-reset.md`
**Success Criteria** (what must be TRUE):

  1. New schema interns deck identity and card names into integer-keyed dimension tables; fact tables reference them by `int` (no repeated `source` / `card_name` / `normalized_card_name` TEXT per row)
  2. **[AMENDED 2026-05-25 — see note]** After a full wipe + re-harvest, the grain index drops the wide TEXT keys: `ux_obs_grain` interns `source`/`card_name` to `int`, cutting grain-key width ~38% (old composite-TEXT PK ≈89 B/row → measured `ux_obs_grain` 55.5 B/row). The original ≥50% *total* index-footprint target is **NOT MET and unreachable with this design**: `category`+`board` stay TEXT in the grain key, and the 4 secondary integer indexes required for SC3's sargable joins raise total index count vs the old single composite PK (even trimming unused indexes lands ~34%). Hitting ≥50% would require interning `category`/`board` too — a separate redesign. The phase's real footprint win is heap dedup (no repeated `source`/`normalized_card_name` TEXT per row), and the headline win is SC3 latency. *(Old baseline was destroyed in the reset before measurement; the ≈89 B/row old figure is reconstructed from current TEXT column lengths + btree overhead, not directly measured.)*
  3. `GetCategoriesAsync` and `GetCategoryRowsForCommanderAsync` are index-backed (EXPLAIN: index scans) and return the same categories as the old design for a fixed sample (Sol Ring + a commander)
  4. `EnsureSchemaAsync` creates the new schema idempotently on a clean DB; old tables dropped via the full-reset runbook (no data carried over)
  5. Build clean; Core + Web tests pass (except known AdminCssPhase1Tests debt)

**Risk:** Medium — coordinated deploy + wipe + re-harvest (empty-cache window acceptable since data is reset); new write path must reproduce identical lookup results. Own plan + Codex review.
**Plans:** 2 plans

Plans:
- [x] 26-01-PLAN.md — Schema + dialect foundation: IRelationalDialect.SurrogateIdColumnType + normalized integer-keyed star schema (sources + cards dims, slim integer-keyed facts, compact indexes incl. LOWER(commander) expr index, reserved content_hash) + RED parity + SQLite-AUTOINCREMENT harness (DBO-01)
- [x] 26-02-PLAN.md — Port write+read paths to integer keys (intern-on-write RETURNING id, batch resolve per deck, integer commander join replacing string-concat), parity GREEN, PG coverage + full-reset runbook update (DBO-01)

**Verification status (2026-05-25):** Code complete + Codex peer-reviewed (RED iter-1 → YELLOW iter-2, both HIGH resolved → RED→GREEN). Build clean; Core 81/81; Web 463 pass / 13 pre-existing CSS fails / 5 PG-integration skipped. **Prod full-reset done 2026-05-25** (`DROP SCHEMA public CASCADE` + restart rebuilt integer-keyed schema; verified via `information_schema` + `pg_indexes`). Re-harvest stopped intentionally at a partial corpus (≈231 decks processed / 655 queued; obs 20.4k, totals 19.3k, cards 8.1k, sources 230).
  - **SC3 — ✅ PASS (measured):** both hot paths index-only, no seq scans. `GetCategoriesAsync` → `ux_cards_normalized` + `ix_obs_card` nested loop (0.3 ms). `GetCategoryRowsForCommanderAsync` → `ix_deck_queue_processed_commander_lower` + `ix_sources_deck_queue` + `ix_obs_source` (0.66 ms; was the 69 s timeout query pre-normalization).
  - **SC2 — ❌ NOT MET as originally written; criterion amended above.** Grain-key width cut ~38%; total index footprint flat-to-worse (5 indexes vs old PK+normalized index). Unreachable without interning `category`/`board`. Real wins booked under SC3 + heap dedup.
  - **Index-usage audit (partial-corpus, write-path-dominated):** grain uniques + `ix_obs_card` + `ix_obs_source` + `ix_totals_card` + dim uniques are exercised. `ix_obs_card_board` / `ix_totals_card_board` have **no production caller** (board filter param unwired — only `CategorySuggestionService:118` calls, with no board) → safe drop candidates. Fact surrogate `*_pkey` (`id`) never read (no RETURNING on fact inserts; only `cards`/`sources` dims use `RETURNING id`) → drop candidate **but defer**: Phase 27 (content-hash dedup) may need a stable fact row id (`reserved content_hash` in 26-01).

  Phase considered **functionally closed** (SC1/SC3/SC4/SC5 met; SC2 amended to achieved scope). Optional follow-up: index trims (~2 MB/M rows) + Phase 27 decision on fact surrogate `id`.

### Phase 27: Deck-Cache Content-Hash Dedup + 5-Day Refresh

**Goal:** The harvest skips rewriting a deck's cached rows when its cards/categories are unchanged (content hash per deck source), and re-checks a deck only after 5 days — cutting write amplification on the category cache while keeping data fresh.
**Requirements**: CAT-02
**Depends on:** Phase 26 (layers on the normalized schema)
**Spec**: `.planning/specs/deck-cache-content-hash-refresh.md`
**Success Criteria** (what must be TRUE):

  1. Re-harvesting a deck whose cards/categories are unchanged performs NO delete/insert on the fact tables (only `last_checked_utc` updates) — proven by a write-counting test
  2. Re-harvesting a deck whose cards/categories changed DOES rewrite its rows (replace semantics preserved) and updates the stored hash
  3. Content hash is stable and order-independent for the same logical deck content
  4. A processed deck is not re-fetched until 5 days after its last check (`last_checked_utc`-based)
  5. Hash stored idempotently (additive schema); existing NULL-hash rows recompute once without error
  6. Build clean; Core + Web tests pass (except known AdminCssPhase1Tests debt)

**Risk:** Low-medium — additive schema; main care is the requeue predicate using `last_checked_utc` and the hash covering exactly the written shape so a real change is never missed.
**Plans:** 1/1 plans complete

Plans:
- [x] 27-01-PLAN.md — Content-hash dedup write gate (SHA-256 over written shape) + repository hash get/set + 5-day DeckRefreshCooldown + Unchanged telemetry bucket + Core write-counting/stability tests (CAT-02)

---

*v1.0 shipped 2026-05-02 | v1.1 shipped 2026-05-08 | v1.2 shipped 2026-05-13 | v1.3 shipped 2026-05-23 | v1.4 started 2026-05-23 (phase numbering reset)*
