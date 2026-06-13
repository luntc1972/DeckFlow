# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- ✅ **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** — Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) — see `.planning/milestones/v1.4-ROADMAP.md`
- ✅ **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** — Phases 28-33 (shipped 2026-06-10) — see `.planning/milestones/v1.5-ROADMAP.md`
- ✅ **v1.6 Content KB Retrieval Fix + Value Re-Validation** — Phases 34-40 (shipped 2026-06-12) — see `.planning/milestones/v1.6-ROADMAP.md`
- 🔵 **v1.7 Local Harvest & Publish Studio** — Phases 41-48 (in progress)

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

<details>
<summary>✅ v1.6 Content KB Retrieval Fix + Value Re-Validation (Phases 34-40) — SHIPPED 2026-06-12</summary>

- [x] Phase 34: Content KB Retrieval Fix — KBR-01..04 — completed 2026-06-10
- [x] Phase 35: Value Re-Validation Gate — KBV-01..04; gate = MARGINAL → Phase 36 skipped — completed 2026-06-10
- [~] Phase 36: Creator Philosophy-Profile + KB Un-Dark — SKIPPED (gate = MARGINAL; see 35-GATE-VERDICT.md)
- [x] Phase 37: Retire KB Clip-Injection — RET-01..05 — completed 2026-06-10
- [x] Phase 37.5: Rebuild KB Corpus — corpus re-distilled high-signal — completed 2026-06-11
- [x] Phase 37.6: Harvest Video Block + Hard-Delete — VBLK-01..04 — completed 2026-06-11
- [x] Phase 38: DeckController + CommandRunners SRP Split — SRP-01..03; route-parity + live smoke — completed 2026-06-12
- [x] Phase 39: Architecture Review Refactor (Finding A) — extract IDeckEntryLoader + IScryfallCardResolver — completed 2026-06-12
- [x] Phase 40: Core.Tests Health Pass — 320/0 deterministic — completed 2026-06-12

**Stats:** ~122 commits, 2026-06-10 → 2026-06-12. Gate-driven: MARGINAL → retire-pivot. Audit: passed.
Full archive: `.planning/milestones/v1.6-ROADMAP.md`

</details>

---

## v1.7 Local Harvest & Publish Studio (Phases 41-48) — IN PROGRESS

**Goal:** A standalone local Blazor Server tool to discover YouTube videos, harvest + distill them, review/approve in a UI, and publish approved entries to deckflow.gg — via repo-commit→Render deploy and/or direct prod-DB push.

### Phase Checklist

- [x] **Phase 41: Studio Scaffold + Secrets Wiring** — Blazor Server project in solution, user-secrets wired, gitignore hardened; prod connection string has a safe home
- [ ] **Phase 42: Orchestrator Extraction** — Harvest/distill/export domain logic moves from DeckFlow.CLI into DeckFlow.Core as IContentKbOrchestrator; CLI becomes thin adapters; closes v1.6 god-class backlog item
- [ ] **Phase 43: Approval Status + Safe Upsert** — approval_status column (self-healing migration), safe content-only-columns upsert overload (preserves is_visible/is_evergreen), and filtered export prerequisite; unblocks both publish paths
- [ ] **Phase 44: Admin Grid Lazy Paging** — /Admin/Harvest initial load goes from synchronous count+aggregate to AJAX on-demand; LOWER(commander_name) index fixes the slow query at the source
- [ ] **Phase 45: Harvest + Distill UI** — Operator can paste video URLs/IDs, browse channels, trigger harvest+distill with live progress and spend dry-run gate; all wired through IContentKbOrchestrator
- [ ] **Phase 46: Review Queue + Commit-Publish Path** — Operator can approve/reject distilled entries in a UI queue; approved seed exports LF-normalized; two-stage commit/push with diff preview
- [ ] **Phase 47: Direct Prod-DB + SCP Publish Path** — File-first SCP then Postgres upsert (safe overload); dry-run diff shows exactly what will change; partial-failure surfaces clearly
- [ ] **Phase 48: UI Audit + Remediation** — Updated 6-pillar visual audit of deployed deckflow.gg; high/medium findings remediated to reach ≥20/24; browser-verified at mobile + desktop viewports

### Phase Details

#### Phase 41: Studio Scaffold + Secrets Wiring
**Goal**: Operator can run `dotnet run` in DeckFlow.Studio and reach a Blazor Server UI in the browser; secrets are routed through user-secrets and no connection string has a safe path into git
**Depends on**: Nothing (first phase)
**Requirements**: STU-01, STU-02, STU-03
**Success Criteria** (what must be TRUE):
  1. `dotnet run --project DeckFlow.Studio` starts a Blazor Server app reachable at http://localhost:{port} with a first page rendered
  2. `dotnet user-secrets list --project DeckFlow.Studio` is the only place the prod Postgres connection string can be stored; no appsettings file in the project tree contains it
  3. `git log --all -- "**/secrets.json"` returns no commits; `grep -r "postgres\|password\|Host=" DeckFlow.Studio/` returns nothing in tracked files
  4. `dotnet build DeckFlow.sln` succeeds with the Studio project present; `dotnet restore DeckFlow.Web/DeckFlow.Web.csproj` (Dockerfile path) is unchanged and does not pull in Studio
**Plans**: 1 plan
- [ ] 41-01-PLAN.md — Scaffold DeckFlow.Studio (net10.0 Blazor Server, Core ref), wire user-secrets, harden .gitignore, lock Dockerfile restore

---

#### Phase 42: Orchestrator Extraction
**Goal**: Harvest, distill, block/unblock, and export domain logic lives in DeckFlow.Core as IContentKbOrchestrator; CLI command behavior is unchanged; Studio can reference Core without referencing CLI
**Depends on**: Phase 41
**Requirements**: ORCH-01, ORCH-02
**Success Criteria** (what must be TRUE):
  1. DeckFlow.Core contains `IContentKbOrchestrator` and `ContentKbOrchestrator`; all existing CLI `internal static` domain methods have moved to Core
  2. `ContentKbCommandRunners` public Run*Async methods are thin adapters (construct stores from paths, delegate to orchestrator, convert result to exit code); no domain logic remains in CLI
  3. All existing CLI tests pass unchanged; `dotnet build DeckFlow.sln` produces 0 errors / 0 new warnings; Core.Tests green
  4. DeckFlow.Studio can reference Core and call `IContentKbOrchestrator` from a Blazor service with no direct CLI project reference
**Plans**: 5 plans (4 waves: contracts / impl / adapters + DI + anchor / Studio smoke + parity tests)
- [x] 42-01-PLAN.md — IContentKbOrchestrator facade + sub-interfaces + null-safe result records + synchronous progress sink (Wave 1)
- [x] 42-02-PLAN.md — Lift CLI domain bodies into ContentKbOrchestrator + consolidate validators/constants D-07 (Wave 2)
- [ ] 42-03-PLAN.md — CLI thin adapters + AddContentKbOrchestrator() DI ext (forwards all slices) + re-point test anchor + Throwing* doubles D-09 (Wave 3)
- [ ] 42-04-PLAN.md — Studio smoke service resolves IContentMaintenanceOrchestrator slice + full ctor wired with real local SQLite stores, no CLI ref D-08 (Wave 4)
- [ ] 42-05-PLAN.md — CLI exit-code/output parity tests + byte-identical JSON-seed golden-fixture test (Wave 4)

---

#### Phase 43: Approval Status + Safe Upsert
**Goal**: The content_site_index has an approval_status column that drives the review queue; a safe content-only upsert overload exists that never clobbers is_visible or is_evergreen; the export path is filtered to approved rows only
**Depends on**: Phase 42
**Requirements**: REVQ-01, PUB-01, PUB-02
**Success Criteria** (what must be TRUE):
  1. `approval_status` column exists on `content_site_index` via the self-healing ALTER migration pattern; column is present after `EnsureSchemaAsync` runs on both a fresh SQLite and a fresh Postgres connection
  2. `UpsertContentColumnsOnlyAsync` exists on `IContentSiteIndexStore`; an integration test sets `is_visible=TRUE`, calls the new overload, and asserts `is_visible` remains TRUE after the call
  3. `GetApprovedRowsAsync` returns only rows where `approval_status='approved'`; the seed export calls this method (not `GetAllRowsAsync`), so rejected/pending rows never appear in `index-seed.json`
  4. The distill pipeline sets newly-inserted `content_site_index` rows to `approval_status='pending'`; rows that existed before the migration are treated as pending (no data loss on migration)
**Plans**: TBD

---

#### Phase 44: Admin Grid Lazy Paging
**Goal**: Navigating to /Admin/Harvest no longer runs the slow count+aggregate query synchronously on initial page load; all commander grid pagination happens via AJAX partial requests; the underlying slow query is also fixed with an index
**Depends on**: Nothing (independent of Studio track)
**Requirements**: GRID-01, GRID-02
**Success Criteria** (what must be TRUE):
  1. Initial GET /Admin/Harvest returns the page skeleton (stats, recent runs, schedule sections) without executing `GetDistinctProcessedCommanderCountAsync` or `GetPagedProcessedCommandersAsync`; the commander grid section is an empty placeholder on first render
  2. The commander grid populates automatically after page load via a `GET /Admin/Harvest/commanders?page=1` AJAX request; pagination clicks replace only the grid section without a full-page reload
  3. A partial expression index on `LOWER(commander_name) WHERE processed=1` exists in `CategoryKnowledgeRepository`; the distinct-count query no longer full-scans the table
  4. The new partial endpoint is guarded by `SameOriginRequestValidator`; direct browser navigation to `/Admin/Harvest/commanders` returns 403
**Plans**: TBD
**UI hint**: yes

---

#### Phase 45: Harvest + Distill UI
**Goal**: Operator can discover videos (channel browse or URL/ID paste), see harvested/distilled status per video, and trigger harvest + distill from the Studio UI with live progress and a dry-run spend gate before any LLM cost is incurred
**Depends on**: Phase 41, Phase 42, Phase 43
**Requirements**: HARV-01, HARV-02, HARV-03, HARV-04, HARV-05
**Success Criteria** (what must be TRUE):
  1. Operator can paste a YouTube channel handle/URL and see a list of that channel's recent videos; each video shows a harvested/distilled status badge; already-harvested videos are visually distinguished before selection
  2. Operator can paste individual YouTube video URLs or IDs to add specific videos to a queue; videos already in the local DB are flagged as duplicates before harvest runs
  3. Operator can trigger harvest on selected videos and see live progress updates in the UI without the browser tab freezing; cancelling or closing the tab stops the in-flight operation (CancellationToken wired to component disposal)
  4. Before distill runs, the operator sees an estimated LLM spend projection (dry-run output); already-distilled videos show a "Re-distill" warning with explicit secondary confirmation before re-queuing; the confirm step is required before `dryRun:false` executes
  5. Actual spend is shown after distill completes; the monthly cap from the existing spend ledger is enforced; already-distilled videos are not silently re-distilled without the explicit Re-distill flow
**Plans**: TBD
**UI hint**: yes

---

#### Phase 46: Review Queue + Commit-Publish Path
**Goal**: Operator can review distilled entries in a queue, approve or reject them, then publish approved entries to deckflow.gg via a git commit with a diff preview and a two-stage commit/push separation that prevents accidental Render auto-deploy
**Depends on**: Phase 43 (approval_status column + filtered export), Phase 45 (harvest+distill produces entries)
**Requirements**: REVQ-02, REVQ-03, PUB-03
**Success Criteria** (what must be TRUE):
  1. The review queue lists `approval_status='pending'` entries; each entry shows the video summary, timestamped clips, and tags; approving or rejecting an entry immediately updates its status in the queue
  2. Operator can approve or reject individual entries and batch-approve/reject filtered sets; the queue supports filtering by status (pending/approved/rejected)
  3. The publish page shows a diff of what will change in `index-seed.json` vs HEAD before any commit is initiated (added/updated/removed row counts from `git diff`)
  4. Stage 1 (git commit) and Stage 2 (git push) are separate UI actions; Stage 2 requires a checkbox "I have reviewed the diff above" to be checked before the push button is enabled; push does not happen automatically after commit
  5. The exported `index-seed.json` contains only `approval_status='approved'` rows and is LF-normalized (running `file index-seed.json` on Linux reports `ASCII text`, not `ASCII text, with CRLF line terminators`)
**Plans**: TBD
**UI hint**: yes

---

#### Phase 47: Direct Prod-DB + SCP Publish Path
**Goal**: Operator can publish approved entries straight to prod Render Postgres + /data disk without waiting for a Render deploy cycle; the write is file-first (SCP before DB); partial failure surfaces clearly for manual reconcile
**Depends on**: Phase 43 (UpsertContentColumnsOnlyAsync safe overload), Phase 46 (commit path proven)
**Requirements**: PUB-04, PUB-05
**Success Criteria** (what must be TRUE):
  1. Before any write, the operator sees a diff of approved local rows vs prod Postgres (new/updated rows from querying prod via the user-secrets connection string); no write occurs until the operator explicitly confirms
  2. Step 1 (SCP artifacts to Render /data) and Step 2 (Postgres upsert) are sequential and gated: the Step 2 button is unreachable unless Step 1 completed successfully; the UI shows each step's success/failure before advancing
  3. Prod Postgres upsert uses `UpsertContentColumnsOnlyAsync` exclusively; after a direct push, `is_visible` and `is_evergreen` on pre-existing prod rows are unchanged (operator can verify by querying prod before and after)
  4. If Step 1 or Step 2 fails, the UI displays which rows/files succeeded and which did not, with enough detail for manual reconcile without re-running the full set
  5. The prod connection string never appears in any log line, UI text, or error message; Studio logs show "Prod connection: configured" / "not configured" only
**Plans**: TBD
**UI hint**: yes

---

#### Phase 48: UI Audit + Remediation
**Goal**: The deployed deckflow.gg site reaches ≥20/24 on the 6-pillar visual audit; Color and Typography (the two weakest pillars since v1.0) are improved; all remediation is confirmed in the browser at mobile and desktop viewports before close
**Depends on**: Nothing (independent of Studio track)
**Requirements**: UIR-01, UIR-02, UIR-03
**Success Criteria** (what must be TRUE):
  1. An updated 6-pillar visual audit document exists with findings scored against the live deployed deckflow.gg site; the v1.0 baseline (16/24) is noted and prioritized findings are labeled HIGH/MEDIUM/LOW
  2. All HIGH and MEDIUM findings from the audit are remediated; the total audit score reaches ≥20/24
  3. Each remediated finding is verified with browser screenshots at ≥2 viewports (mobile ≤768px and desktop ≥1024px); no finding is closed on grep or static analysis alone
  4. Layout CSS changes go into `site-common.css`; new or modified design tokens go into the `:root` block of each guild theme file; no layout rules are added to `site.css`
**Plans**: TBD
**UI hint**: yes

---

### Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 41. Studio Scaffold + Secrets Wiring | 0/1 | Not started | - |
| 42. Orchestrator Extraction | 2/5 | In Progress|  |
| 43. Approval Status + Safe Upsert | 0/TBD | Not started | - |
| 44. Admin Grid Lazy Paging | 0/TBD | Not started | - |
| 45. Harvest + Distill UI | 0/TBD | Not started | - |
| 46. Review Queue + Commit-Publish Path | 0/TBD | Not started | - |
| 47. Direct Prod-DB + SCP Publish Path | 0/TBD | Not started | - |
| 48. UI Audit + Remediation | 0/TBD | Not started | - |

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
- **C — Split `ContentKbCommandRunners`** (1508 LOC, 5 sub-domains → Harvest / Distill / Source runners). Internal seams pin behavior. Effort M / Risk M. **NOTE: v1.7 Phase 42 (ORCH-01) addresses Finding C as a side-effect by extracting domain logic to Core.**
- **D — Finish `Services/` concern-foldering + extract `Program.cs` `AddDeckFlowXxx()`** (48 flat files; empty `Services/Content/` = stalled migration). Pure file/namespace moves. Effort M / Risk L.
- **E — Relocate misplaced domain logic to `DeckFlow.Core`** (deck-stat classifiers in DeckComparisonService; distill cost/validation in ContentKbCommandRunners). Effort M / Risk L.
- **F — Strengthen dual-dialect abstraction** (33 `IsPostgres`/`IsSqlite` branches across 7 stores → dialect render methods; remove Web `Feedback*` SQL from Core `IRelationalDialect`). ⚠ Postgres path has no automated test. Effort M / Risk M.
- **ADR-note tier:** G packet cache-key `IPacketCacheKeyStrategy` · H `IScryfallThrottle` seam · I `IMemoryCache` SizeLimit doc · J `System.CommandLine` beta4 deliberate-pin ADR · K residual test gaps (middleware-ordering integration test; Polly policy-shape assertion).

### Deferred to v1.7+ (per v1.5/v1.6 scope decisions)

- **Gemini paste-limit workaround** (`DECKFLOW_GEMINI_ENABLED` stays flag-gated; needs split-message vs direct-API path decision)
- **SpellbookCombo ranking fields** (PRM-08 — parser drops `manaValueNeeded`/`popularity`/`uses`; priority ranking degraded)
- **Embedding/vector retrieval** (pgvector / ONNX sentence-transformers) — deferred until corpus >~500 videos; RAM-cap risk at current size

---

*v1.0 shipped 2026-05-02 | v1.1 shipped 2026-05-08 | v1.2 shipped 2026-05-13 | v1.3 shipped 2026-05-23 | v1.4 shipped 2026-06-03 | v1.5 shipped 2026-06-10 | v1.6 shipped 2026-06-12 | v1.7 in progress 2026-06-13*
