# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- ✅ **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** — Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) — see `.planning/milestones/v1.4-ROADMAP.md`
- 🚧 **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** — Phases 28-31 (in progress)

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

### 🚧 v1.5 Deck Primer Generator + Content KB Integration + Housekeeping (In Progress)

**Milestone Goal:** Ship the Deck Primer Generator as a fourth paste-ready workflow, wire Content KB knowledge into deck-analysis prompts, and clear v1.4 quality debt.

- [x] **Phase 28: Housekeeping Bundle** — KB-12 codex distill backend + VERIFICATION.md hygiene + artifact hygiene (completed 2026-06-04)
- [ ] **Phase 29: Core XML-Doc Backfill + Gate Widen** — 90 DeckFlow.Core doc sites (probe-derived; was 186 at Phase 23) + editorconfig gate widen
- [ ] **Phase 30: Content KB Integration** — prod flag flip + expert-context injection + "What Experts Say" panel
- [ ] **Phase 31: Deck Primer Generator** — fourth paste-ready workflow, 31-section catalog, combo grounding, bracket routing

## Phase Details

### Phase 28: Housekeeping Bundle

**Goal**: v1.4 quality debt is cleared — KB-12 codex distill backend works, VERIFICATION.md files are accurate, and milestone artifact gaps are closed
**Depends on**: Nothing (off critical path, no web surface)
**Requirements**: HSK-02, HSK-03, HSK-04
**Success Criteria** (what must be TRUE):

  1. ~~`DECKFLOW_LLM_PROVIDER=codex` distills a test transcript end-to-end via the codex branch of `CliLlmDistillationService` (no `NotSupportedException` thrown); existing openai and claude paths unchanged~~ — AMENDED 2026-06-04: HSK-02 re-demoted to backlog per D-03 (28-03 discovery found no provable read-isolation boundary in codex 0.136.0; user ratified re-demote). Replacement criterion: the ship/re-demote decision gate ran with documented evidence and the outcome is recorded in `28-DISCOVERY.md` + backlog note
  2. All 7 previously-missing v1.4 VERIFICATION.md files exist and stale UAT labels (human_needed / partial / unknown) reflect actual shipped state
  3. P26 missing SUMMARY files, P24 quick-fix artifact chain, and dual artifact-tree drift items from the v1.4 audit are resolved

**Plans**: 4 plans (2 waves)

Plans:
**Wave 1**

- [x] 28-01-PLAN.md — VERIFICATION.md hygiene: back-fill 7 retro VERIFICATION files + correct stale Phase 20 status labels (HSK-03)
- [x] 28-02-PLAN.md — Artifact hygiene: dual-tree CLI fix (D-11) + retro P26/P24 SUMMARYs (D-12) + audit dedup (D-13) (HSK-04)
- [x] 28-03-PLAN.md — Codex isolation discovery + ship/re-demote decision gate (HSK-02, D-01/D-02/D-03)

**Wave 2** *(blocked on Wave 1 completion)*

- [ ] ~~28-04-PLAN.md — Codex distill backend implementation + sentinel-exfil regression (HSK-02; depends on 28-03 ship decision)~~ — SKIPPED 2026-06-04: 28-03 decision gate resolved "re-demote" (D-03); implementation must not proceed

### Phase 29: Core XML-Doc Backfill + Gate Widen

**Goal**: DeckFlow.Core is fully XML-documented and the doc-warning gate covers both projects — build is clean at 0 CS1591 warnings across the entire solution
**Depends on**: Nothing (parallel to Phase 28; must complete before any Core files are touched in Phases 30-31)
**Requirements**: HSK-01
**Success Criteria** (what must be TRUE):

  1. All previously-undocumented DeckFlow.Core public sites have `<summary>` XML doc comments — `dotnet build -warnaserror:CS1591` passes from a clean `obj/`. *(Probe-derived authoritative scope: 90 unique sites across 29 files as of 2026-06-04; the "186" figure was accurate at Phase 23 research time but Phases 19–28 documented much new Core code. Executors re-run the probe to get the live list.)*
  2. `.editorconfig` CS1591 gate is widened to `[DeckFlow.Core/**.cs]` as the final commit of this phase; existing `[DeckFlow.Web/**.cs]` gate unchanged
  3. Build is clean (0 errors, 0 new warnings) across both `DeckFlow.Core` and `DeckFlow.Web` after the gate widen

**Plans**: 5 plans (2 waves)

Plans:
**Wave 1** *(parallel — non-overlapping file sets; each re-runs the doc-warning probe at start)*

- [x] 29-01-PLAN.md — Storage folder doc backfill (IRelationalDialect, Postgres/Sqlite dialects, RelationalDatabaseConnection; ~21 sites) (HSK-01)
- [x] 29-02-PLAN.md — Reporting + Filtering doc backfill (ReconciliationReporter raw-string-safe, Category* reporters, DeckEntryFilter; ~16 sites) (HSK-01)
- [x] 29-03-PLAN.md — Knowledge doc backfill (CategoryKnowledgeRepository incl. 5 CS1573, BoardCategoryComparer, ArchidektDeckCacheSession; ~12 sites) (HSK-01)
- [x] 29-04-PLAN.md — Integration + Exporting + Parsing + Models + Normalization + Diffing doc backfill (17 files incl. 1 CS1573 + 2 enums; ~41 sites) (HSK-01)

**Wave 2** *(blocked on all Wave 1 plans — the FINAL commit of the phase)*

- [ ] 29-05-PLAN.md — `.editorconfig` gate widen to `[DeckFlow.Core/**.cs]` (non-autonomous; inject-probe proof + full-solution build guard + human-verify checkpoint) (HSK-01)

### Phase 30: Content KB Integration

**Goal**: Curated expert knowledge is injected into deck-analysis prompts and surfaced in a "What Experts Say" panel — `content.kb.enabled` is ON in production with verified live content
**Depends on**: Phase 28 (prod flag flip is a housekeeping step; Phase 28 verifies the KB artifact state is clean before the flag goes live)
**Requirements**: KBI-01, KBI-02, KBI-03, KBI-04, KBI-05, KBI-06
**Success Criteria** (what must be TRUE):

  1. `content.kb.enabled` is flipped ON in prod after a fresh harvest run; at least one clip is visible on the public KB browse page
  2. A generated deck-analysis prompt artifact includes a `## Expert Context` block with up to 5 curated clip excerpts (block-quoted, attributed, `is_kept = true` only) when matching clips exist; the block is absent — not empty — when no clips match
  3. The DeckAnalysis result page shows a collapsed "What Experts Say" panel with source channel, title, timestamp deep-link, and harvest date for each injected clip; the panel is hidden entirely when no clips matched
  4. Admin sources view displays a per-clip relevance match score for curation tuning

**Plans**: TBD
**UI hint**: yes

### Phase 31: Deck Primer Generator

**Goal**: Users can generate a complete, paste-ready Moxfield primer prompt from a decklist and bracket selection — the fourth workflow tab, peer of DeckAnalysis, DeckComparison, and CedhMetaGap
**Depends on**: Phase 29 (Core must be doc-clean before new Core-touching services are added); combo-data spike (PRM-01) runs as the first execution unit
**Requirements**: PRM-01, PRM-02, PRM-03, PRM-04, PRM-05, PRM-06, PRM-07, PRM-08, PRM-09, PRM-10, PRM-11, PRM-12
**Success Criteria** (what must be TRUE):

  1. A "Deck Primer" tab appears in the workflow nav; user can load a decklist via URL or paste using the same import flow as other workflows
  2. Selecting bracket 1–5 pre-applies a section preset (cEDH for 5, Casual/Upgraded for 1–4); user can then toggle individual sections from 5 collapsible groups; collapsed group headers show a selected-count badge
  3. Generated prompt grounds combo sections with Commander Spellbook data as verified truth, structurally fenced from a labeled speculative-synergies ask; when Spellbook returns null, an explicit disclosure replaces the grounded block
  4. Matchup sections are bracket-routed: EdhTop16 named archetypes for bracket 5; five generic strategy buckets (Aggro / Control / Midrange / Combo / Stax-Hate) for brackets 1–4
  5. Per-AI artifacts (ChatGPT / Claude / Gemini) are generated and stored via `PacketArtifactStore` with a working zip round-trip — re-uploading a session restores bracket and section selections; section selections persist in localStorage across visits

**Plans**: TBD
**UI hint**: yes

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

## Progress

**Execution Order (v1.5):**
Phase 28 and Phase 29 can run in parallel (independent tracks). Phase 30 depends on Phase 28 (KB artifact state clean before flag flip). Phase 31 depends on Phase 29 (Core doc-clean before new Core services added) and on the PRM-01 spike completing inside Phase 31.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 28. Housekeeping Bundle | 4/4 | Complete    | 2026-06-04 |
| 29. Core XML-Doc Backfill + Gate Widen | 4/5 | In Progress|  |
| 30. Content KB Integration | 0/TBD | Not started | - |
| 31. Deck Primer Generator | 0/TBD | Not started | - |

---

*v1.0 shipped 2026-05-02 | v1.1 shipped 2026-05-08 | v1.2 shipped 2026-05-13 | v1.3 shipped 2026-05-23 | v1.4 shipped 2026-06-03 | v1.5 started 2026-06-03*
