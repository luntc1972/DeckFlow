# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- ✅ **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** — Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) — see `.planning/milestones/v1.4-ROADMAP.md`
- ✅ **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** — Phases 28-33 (shipped 2026-06-10) — see `.planning/milestones/v1.5-ROADMAP.md`
- ✅ **v1.6 Content KB Retrieval Fix + Value Re-Validation** — Phases 34-40 (shipped 2026-06-12) — see `.planning/milestones/v1.6-ROADMAP.md`
- ✅ **v1.7 Local Harvest & Publish Studio** — Phases 41-50 (shipped 2026-06-17) — see `.planning/milestones/v1.7-ROADMAP.md`
- ✅ **Cycle 8 — Hardening & Backlog Burn-down** — Phases 51-54 (shipped 2026-06-17, `2026.06.4`) — see `.planning/milestones/cycle8-ROADMAP.md`
- ✅ **Cycle 9 — Content Pipeline & Publish-Tracking** — Phases 55-58 (shipped 2026-06-19, `2026.06.5`) — see `.planning/milestones/cycle9-ROADMAP.md`
- ✅ **Cycle 10 — Studio Automation, Sync & Polish** — Phases 59-63 (shipped 2026-06-21, `2026.06.6`) — see `.planning/milestones/cycle10-ROADMAP.md`

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

<details>
<summary>✅ v1.7 Local Harvest & Publish Studio (Phases 41-50) — SHIPPED 2026-06-17</summary>

**Goal:** A standalone local Blazor Server tool to discover YouTube videos, harvest + distill them, review/approve in a UI, and publish approved entries to deckflow.gg — via repo-commit→Render deploy and/or direct prod-DB push.

- [x] Phase 41: Studio Scaffold + Secrets Wiring (1/1) — completed 2026-06-13
- [x] Phase 42: Orchestrator Extraction (5/5) — completed 2026-06-13
- [x] Phase 43: Approval Status + Safe Upsert (2/2) — completed 2026-06-13
- [x] Phase 44: Admin Grid Lazy Paging (3/3) — completed 2026-06-14
- [x] Phase 45: Harvest + Distill UI (4/4) — completed 2026-06-15
- [x] Phase 46: Review Queue + Commit-Publish Path (5/5) — completed 2026-06-16
- [x] Phase 47: Direct Prod-DB + SCP Publish Path (3/3) — completed 2026-06-16
- [x] Phase 48: UI Audit + Remediation (3/3) — completed 2026-06-17
- [x] Phase 49: Dapper Data-Access Adoption (5/5) — completed 2026-06-14
- [x] Phase 50: Code-Style Enforcement — ReSharper Reconciliation + PR Gate (4/4) — completed 2026-06-14

Verification: 10/10 phases verified (4 passed, 6 human_needed = automated PASS, operator smoke deferred). 23/23 requirements satisfied. Audit: tech_debt (integration clean). Full detail: `.planning/milestones/v1.7-ROADMAP.md`.

</details>

---

<details>
<summary>✅ Cycle 8 — Hardening & Backlog Burn-down (Phases 51-54) — SHIPPED 2026-06-17, `2026.06.4`</summary>

- [x] Phase 51: Verify v1.7 on main + non-prod UAT (HARD-01, HARD-03, OPS-01) — completed 2026-06-17
- [x] Phase 52: Live prod-publish verification (HARD-02) — completed 2026-06-17
- [x] Phase 53: Architecture backlog burn-down (ARCH-01, ARCH-02) — completed 2026-06-17
- [x] Phase 54: Feature debt — SpellbookCombo ranking + Gemini size-verify (FEAT-01, FEAT-02) — completed 2026-06-17

Full archive: `.planning/milestones/cycle8-ROADMAP.md`
Requirements archive: `.planning/milestones/cycle8-REQUIREMENTS.md`

</details>

---

<details>
<summary>✅ Cycle 9 — Content Pipeline & Publish-Tracking (Phases 55-58) — SHIPPED 2026-06-19, `2026.06.5`</summary>

- [x] Phase 55: Publish-State Foundation (PUB-01, PUB-02) — completed 2026-06-18
- [x] Phase 56: Studio Surfaces (BROWSE-01/02/03, REM-01/02, ADD-01, PUB-03) — completed 2026-06-18
- [x] Phase 57: Admin Surface + Distill Quality (SITE-01, DIST-01) — completed 2026-06-18
- [x] Phase 58: Dogfood (DOGFOOD-01) — completed 2026-06-19 (all 4 SCs PASS; SC2 DirectPush publish-visible fix + SECURED 9/9)

Full archive: `.planning/milestones/cycle9-ROADMAP.md`
Requirements archive: `.planning/milestones/cycle9-REQUIREMENTS.md`

</details>

---

## ✅ Cycle 10 — Studio Automation, Sync & Polish — Phases 59-63 (SHIPPED 2026-06-21, `2026.06.6`)

Full detail archived in `.planning/milestones/cycle10-ROADMAP.md`. Shipped phases: 59 Pipeline Automation (AUTO-01/02), 60 Pull-from-Prod Reconcile (SYNC-01/02/03), 61 Creator Sources & Selection (SRC/HSEL), 62 Studio UI Polish (SUI-01..06), 63 Studio Self-Contained Executable (DIST-01).

<details>
<summary>Phase detail (collapsed — shipped)</summary>

**Goal:** Cut manual steps from the Studio harvest→publish pipeline, give the operator a true prod↔local reconcile view, and make video selection + pipeline state fast and obvious.

**Granularity:** coarse — 4 phases is the natural minimum. The two automation requirements (Core orchestrator slice) and the novel prod-read sync lane each earn their own phase; the eleven Studio-surface requirements split into one selection-mechanics pass (persisted data + behavior) and one presentation-polish pass over the same `Harvest.razor` / Studio shell. No standalone dogfood phase — validation folds into per-phase operator success criteria.

**Coverage:** 16/16 requirements mapped (AUTO, SYNC, SRC, HSEL, SUI).

### Phase Summary

- [x] **Phase 59: Pipeline Automation** — Harvest auto-distills; high-confidence distills auto-approve, low-confidence enter the review queue (AUTO-01, AUTO-02) (✅ COMPLETE 2026-06-20; verified PASS 14/14, SC1-4; operator end-to-end checkpoint approved)
- [x] **Phase 60: Pull-from-Prod Reconcile** — Operator pulls live prod content down, sees per-entry diffs, and resolves each from Studio (SYNC-01, SYNC-02, SYNC-03)
- [x] **Phase 61: Creator Sources & Selection** — Saved creator list + dropdown picker; unharvested-only default with skip/ignore + un-skip (SRC-01, SRC-02, HSEL-01, HSEL-02, HSEL-03)
- [x] **Phase 62: Studio UI Polish** — Consistent status badges, tighter flow, better feedback states, creator filtering, navigation cleanup (SUI-01..06) (completed 2026-06-21)

### Phase Details

#### Phase 59: Pipeline Automation
**Goal**: The operator harvests a video and gets a distilled, review-ready (often already-approved) entry in one action — no separate manual distill step, no rubber-stamping high-quality distills.
**Depends on**: Nothing new (builds on the existing Cycle 9 Core orchestrator distill/approve slice)
**Requirements**: AUTO-01, AUTO-02
**Success Criteria** (what must be TRUE):
  1. Operator harvests a video from Studio and a distilled entry appears review-ready in one action, with no separate "Distill" click required.
  2. A distill whose quality/confidence signal is at or above the configured threshold lands auto-approved (skips the manual review queue); a distill below the threshold remains in the review queue.
  3. Operator can adjust the auto-approve threshold and can turn auto-approval off entirely; with it off, every distill enters the review queue.
  4. The harvest action still respects the existing spend dry-run / cap gate — auto-distill does not bypass the spend ceiling, and the existing distill provider is used unchanged (no model / provider swap).
**Plans**: 3 plans
  - [x] 59-01-PLAN.md — Core auto-approve signal seam (clip-count, swappable) + per-video clip counts on DistillResult
  - [x] 59-02-PLAN.md — Persisted auto-approve settings (on/off + cutoff, default ON/5) + Harvest-page Auto-approve panel
  - [x] 59-03-PLAN.md — One-click harvest→auto-distill→auto-approve flow + per-video outcome summary (manual Distill fallback intact)
**UI hint**: yes
**Open risk**: A per-distill quality/confidence signal may not exist yet. This phase owns deriving or adding one from existing distill output (e.g. tag-count / clip-count / summary-completeness heuristics, or a returned model confidence). No distill provider or model swap is permitted to obtain the signal.

#### Phase 60: Pull-from-Prod Reconcile
**Goal**: Studio reflects what is actually live — the operator can pull prod content down, see exactly what is out of sync per entry, and reconcile each diff without dropping to the CLI or hand-editing the DB.
**Depends on**: Phase 59 (so reconcile classification accounts for auto-distilled / auto-approved local state); independently plannable. Most novel/risky lane — stays its own phase.
**Requirements**: SYNC-01, SYNC-02, SYNC-03
**Success Criteria** (what must be TRUE):
  1. Operator triggers a "Pull from Prod" action in Studio and the live prod `content_site_index` rows plus their published artifacts are pulled down to local (a read mirror of the existing DirectPush write path: SSH.NET SCP from Render `/data` + a Postgres read of `content_site_index`).
  2. Studio shows, per entry, a diff classification of prod-newer / missing-locally / local-only / diverged, so the operator can see exactly what is out of sync.
  3. For each surfaced diff, the operator can pick a resolution (adopt prod / keep local) from Studio and have Studio apply it — no CLI, no manual DB edit.
  4. The pull path is read-only against prod (no write-back to prod from this lane) and uses the operator-local secret connection convention already established for DirectPush.
**Plans**: 4 plans
- [ ] 60-01-PLAN.md — ContentSyncDiffClassifier + SyncDiffEntry/SyncDiffKind in Core + xUnit (SYNC-02)
- [ ] 60-02-PLAN.md — SCP download pair (ISshArtifactDownloader/SftpArtifactDownloader) + read-only IProdContentReader prod reader (SYNC-01)
- [ ] 60-03-PLAN.md — PullFromProd.razor 2-stage page + nav + DI + bUnit tests + README (SYNC-01, SYNC-03)
- [ ] 60-04-PLAN.md — Operator live pull verification checkpoint (SC1 + SC4 read-only invariant)
**UI hint**: yes

#### Phase 61: Creator Sources & Selection
**Goal**: The operator manages a curated creator list and picks from it to browse, sees only the videos still worth harvesting by default, and can quietly skip candidates without the heavyweight Block path.
**Depends on**: Phase 59 (harvested/distilled state drives the unharvested-only filter)
**Requirements**: SRC-01, SRC-02, HSEL-01, HSEL-02, HSEL-03
**Success Criteria** (what must be TRUE):
  1. Operator can add, view, and remove curated creators/channels in Studio, and the list persists across Studio restarts.
  2. When browsing videos to harvest, operator selects a creator from a dropdown of the saved list instead of pasting a channel URL — with paste-URL still available as a one-off fallback.
  3. The creator video-selection list defaults to showing only not-yet-harvested videos, with a toggle to show all (including harvested/distilled/published).
  4. Operator can skip/ignore a candidate so it no longer appears in selection — with no artifact hard-delete and no harvest blocklist entry (distinct from Block).
  5. Operator can view the skipped/ignored list and un-skip an entry to bring it back into selection (parity with the existing Block/Unblock pair).
**Plans**: TBD
**UI hint**: yes

#### Phase 62: Studio UI Polish
**Goal**: Pipeline state is obvious at a glance and the harvest→review→publish flow is fast, clear, and forgiving — fewer clicks, better feedback, cleaner navigation, and creator-based filtering throughout.
**Depends on**: Phase 61 (status badges and creator filtering build on the surfaces and creator list settled in 59-61); presentation pass runs over the now-stable Studio surfaces.
**Requirements**: SUI-01, SUI-02, SUI-03, SUI-04, SUI-05, SUI-06
**Success Criteria** (what must be TRUE):
  1. Pipeline status is clear at a glance on the main Studio pages via consistent status badges (harvested / distilled / approved / publish-state), reusing the Cycle 9 `PublishStateDeriver` / `VideoStatusResolver` (no duplicate status logic).
  2. The harvest → review → publish flow takes fewer clicks (multi-select ergonomics, sensible defaults, less back-and-forth between pages) than before this phase.
  3. Studio actions show improved loading, error, and success feedback — including harvest/distill spend warnings and clear failure messages. (Operator request 2026-06-21: add a LIVE progress/console view on the Pull from Prod page — stream the existing stage + per-artifact `IProgress` reports into a scrolling UI panel as the pull runs, instead of only spinners + final table, so the operator can watch progress without tailing the Serilog log file.)
  4. Operator can filter/group video and entry lists by creator to see which videos belong to which creator.
  5. Studio layout and inter-page navigation read as denser and clearer, and the `MainLayout.razor` "About" link points at a real, relevant target instead of the ASP.NET docs scaffold placeholder.
**Plans**: TBD
**UI hint**: yes

### Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 59. Pipeline Automation | 3/3 | ✅ Complete — verified PASS, operator approved | - |
| 60. Pull-from-Prod Reconcile | 4/4 | ✅ Complete — operator-verified PASS (60-04) | 2026-06-21 |
| 61. Creator Sources & Selection | 4/4 | ✅ Executed (Claude-coded) | 2026-06-21 |
| 62. Studio UI Polish | 4/4 | Complete   | 2026-06-21 |

### Phase Ordering Rationale

- **59 first**: Auto-distill / auto-approve sits in the Core orchestrator distill/approve slice and changes the meaning of "harvested" vs "review-ready" vs "approved" state. The selection filter (HSEL-01) and the status badges (SUI-01) both read that state, so it lands first. Carries the AUTO-02 quality-signal open risk — isolating it keeps that risk contained.
- **60 second, own phase**: Pull-from-Prod is the most novel/risky lane — a NEW authenticated prod READ path mirroring DirectPush. It shares nothing with the Harvest.razor UX work and must not be diluted into a polish phase.
- **61 third**: Creator-source management + harvest selection (persisted creator list, dropdown picker, unharvested-only default, skip/ignore + un-skip) is the data-and-behavior pass over `Harvest.razor`. Depends on Phase 59's harvested-state definition for the default filter.
- **62 last**: The presentation pass (status badges, flow tightening, feedback states, layout/nav, creator filtering, the one-line MainLayout About-link fix) runs over the now-settled surfaces so polish isn't redone after 61 reshapes them. SUI-01 reuses the existing status engine; SUI-06 is a one-line fix.

**No separate dogfood phase**: coarse granularity. Validation folds into per-phase operator success criteria — Phases 59 and 60 each carry observable operator gates (one-action harvest+distill, threshold on/off, live prod pull + per-entry diff resolution) that constitute their own end-to-end checks.

</details>

---

## Backlog

### Harden deck-source host matching (SSRF/abuse) (BACKLOG — captured 2026-06-20; HIGH priority — address at next milestone START)

**Goal:** Close a host-spoofing hole in deck-URL loading shared by every deck tool. Origin: Codex code review of the mana-base feature (2026-06-20), deferred out of that feature branch because the fix is in shared code touching all deck tools.

**Problem:** Platform detection uses substring host matching — `DeckFlow.Core/Loading/DeckEntryLoader.cs` `LoadFromSourceAsync` (~L121/L127) and `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` (~L105) test `uri.Host.Contains("moxfield.com")` / `Contains("archidekt.com")`. So a hostile URL like `https://moxfield.com.evil.tld/decks/123` is treated as a trusted deck source on anonymous public endpoints (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`, `/manabase`). In the Moxfield-fallback path the original attacker URL is then forwarded to Commander Spellbook as `url=…`, widening the abuse surface.

**Fix:** Require exact host or approved-subdomain matching (`host == "moxfield.com" || host.EndsWith(".moxfield.com")`; same for archidekt.com), and on the Moxfield fallback always reconstruct the canonical `https://moxfield.com/decks/{deckId}` URL instead of forwarding the submitted one. Add regression tests for spoofed hosts (`moxfield.com.evil.tld`, `evilmoxfield.com`, `moxfield.com@evil.tld`).

**Requirements:** TBD
**Plans:** 0 plans

Scope note: shared Core code — affects all deck tools, needs its own change + regression tests. Promote via `/gsd-review-backlog` at next cycle open.

### Prod artifact gap — 86 of 109 content rows have no .md on Render /data (BACKLOG — high priority; found 2026-06-21 during Phase 60 live verify)

**Problem:** Prod `content_site_index` has 109 rows, but Render's `/data/content-kb` only holds artifacts for **3 creators** (`salubrioussnail`, `the-command-zone`, `based-deck-department` = 23 rows). The other **86 rows** reference `.md` artifact files that are missing from the prod disk entirely (e.g. `content-kb/commander-baumi/*`). Discovered via SFTP probe during Phase 60's Pull-from-Prod operator verify — the Pull page correctly reported "not downloaded" for all 86.

**Impact:** Any feature that needs the artifact body for those 86 rows (Pull-from-Prod artifact adopt; potentially the live content-kb if it serves from `/data` rather than the DB content column) is broken for them. The DB rows exist but the source files don't.

**Likely causes (investigate):** (a) Render `/data` disk was reset/lost while the DB persisted; (b) DirectPush upserted DB rows without uploading artifacts for non-3 creators; (c) those artifacts only ever lived locally and were never pushed. Confirm whether the live site reads content from `/data` files or the DB (decides severity).

**Fix options:** re-upload the missing 86 artifacts from a local source if they exist; OR reconcile the DB down to the 23 rows that have artifacts; OR (if content lives in the DB) downgrade this to cosmetic. Promote via `/gsd-review-backlog`.

### Validate Content KB value — A/B ChatGPT output with vs without expert context (BACKLOG — high priority; was todo, moved 2026-06-19 at Cycle-9 close)

**Goal:** Gating experiment — prove the Content KB actually makes ChatGPT's deck analysis *better* before investing further (e.g. the creator philosophy-profile redesign). `content.kb.enabled` is OFF in prod and the subsystem is unproven on end-output quality (Cycle 9 validated the *pipeline* produces cleaner distills, but not that injected context lifts the final ChatGPT answer).

**Experiment:** For a handful of representative decks, generate the analysis prompt twice — with vs without expert-context clips — run both through ChatGPT, compare answer quality (signal beyond ChatGPT's own MTG knowledge, actionable specificity, creator-voice). Judge blind if feasible.

**Decision criteria:** Clear lift → green-light philosophy-profile build + flip `content.kb.enabled` ON. Marginal → reconsider the KB (per-deck targeted retrieval or user-supplied sources instead of whole-channel pre-distill).

**Spike-able** via `/gsd-spike` — lightweight, no production code to start. Origin: Phase 30 UAT (2026-06-09). Promote via `/gsd-review-backlog`. (Deferred again at Cycle 10 scoping — KBVAL-01/02 are Cycle 10 v2.)

### Studio "Pull from Prod" — prod→local sync (PROMOTED to Cycle 10 Phase 60 — SYNC-01/02/03, 2026-06-20)

> Promoted into Cycle 10 as Phase 60 (Pull-from-Prod Reconcile). Original backlog note retained below for design context; resolve open design questions during `/gsd-plan-phase 60`.

**Goal:** Make the Studio local store reflect current production data. Today Studio is strictly one-way (DirectPush local→prod); the local `content-kb.db` drifts from prod. Add a "Pull from Prod" page/action — the inverse of `DirectPush.razor` — so an operator can mirror prod into local.

**Scope (Option B — full pull; Option A read-only "Prod vs Local" drift view was the cheaper alternative):**
- Read prod rows via `prodStore.GetAllRowsAsync` (plumbing already exists — DirectPush Stage 1 uses it for the diff).
- Upsert them into the local `IContentSiteIndexStore` (`UpsertRowAsync` exists).
- SCP-**download** the artifact `.md` files from prod `/data` into local `content-kb/` — **new**: only SCP upload exists today (`ISshArtifactUploader`); needs a download counterpart.
- Read-only prod DB connection acceptable for the row read; SCP read for artifacts. AI-never-writes-prod rule unaffected (this only writes LOCAL).

**Open design questions (resolve when planned):**
- Merge semantics: prod-wins vs local-wins for in-flight local edits.
- `approval_status` handling (prod defaults `pending`; local may be `approved`) — don't clobber local approvals?
- `is_visible` / `is_hidden` / `pushed_to_prod_utc` reconciliation.

Origin: operator wants Studio to mirror prod (Phase 58 dogfood, 2026-06-19).

### Studio UI polish pass (PARTIALLY PROMOTED to Cycle 10 Phase 62 — SUI-01..06, 2026-06-20)

> The P2 status-badge/per-page-consistency, channel/source column + filter, and MainLayout cleanup items are promoted into Cycle 10 Phase 62 (Studio UI Polish). The full design-system tier (P1 shell/tokens/dashboard) and P3 responsive/dark-mode remain backlog for a future `/gsd-ui-phase` if demand surfaces. Original note retained for context.

**Goal:** Give DeckFlow.Studio a real design pass. Today it's the **stock Blazor Server template** — default nav sidebar (`MainLayout`/`NavMenu`), default 64-line `site.css` (stock `#1b6ec2` blue), a 19-line placeholder `Home.razor`, and 6 pages each hand-rolling raw Bootstrap (`Harvest.razor` is 1651 lines). Functional but unbranded and inconsistent. The public site got a UI audit (Phase 48, 16→20/24); Studio never has.

**Scope — three tiers (operator-only desktop tool; no functional/feature changes):**

*P1 — shell + design system:*
- Shared Studio design tokens (color/spacing/type) in one stylesheet; align with deckflow.gg brand (logo/title, accent, optional dark mode).
- Replace the stock template chrome: app header/nav, page-title pattern, consistent `.content` layout container.
- Turn `Home.razor` into a real landing/dashboard — pipeline state at a glance (counts by VideoStatus / PublishState, quick links to Harvest/Review/Publish).

*P2 — per-page consistency:*
- Unify status/publish-state badges into shared CSS classes driven by `VideoStatus` / `PublishState` (today colors are defined ad-hoc per page in Review/Publish/Harvest/Blocked).
- Consistent table, form, alert, button-hierarchy, and primary-action patterns across all 6 pages (Home/Harvest/Review/Publish/DirectPush/Blocked).
- Systematic loading + empty states (some spinners exist; not uniform).

*P2 — channel/source column + filter (concrete, highest-value ask):*
- **Show the source/channel each video came from in the grids.** `ContentSiteIndexRow.Source` already holds it (e.g. "The Command Zone") but it is **not surfaced** — the Review grid shows only Title/Tags/Status/Publish State/Actions and `ReviewViewModel` doesn't even expose `Source`. Add a **Source/Channel column** to the Review grid (and wherever a per-row list exists).
- **Filter by channel/source** in those grids — a dropdown/segmented filter populated from the distinct `Source` values (mirror the web `/Admin/ContentKb` creator-filter behavior; that grid is source-grouped). Lets the operator focus one channel at a time.
- Low-risk: data + persistence already exist; this is ViewModel field + column + client-side filter state.

*P3 — optional:*
- Responsive / table-overflow handling, dark mode, keyboard affordances.

**Approach:** run as a `/gsd-ui-phase` (UI-SPEC design contract) → implement → `/gsd-ui-review` audit, same flow as Phase 48 for the public site.

**Explicitly out:** no new features, no behavior change; Studio stays a local operator tool. Promote via `/gsd-review-backlog`.

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
- **B — Split `CategoryKnowledgeRepository`** (1276 LOC, 24 methods → Schema / DeckQueue / CardCategory). Live-path Core god-repo; strong existing test net (17 facts + parity + dedup). Effort L / Risk M. **DONE — Cycle 8 Phase 53 (ARCH-01).**
- **C — Split `ContentKbCommandRunners`** (1508 LOC, 5 sub-domains → Harvest / Distill / Source runners). Internal seams pin behavior. Effort M / Risk M. **NOTE: v1.7 Phase 42 (ORCH-01) addresses Finding C as a side-effect by extracting domain logic to Core.**
- **D — Finish `Services/` concern-foldering + extract `Program.cs` `AddDeckFlowXxx()`** (48 flat files; empty `Services/Content/` = stalled migration). Pure file/namespace moves. Effort M / Risk L. **DONE — Cycle 8 Phase 53.**
- **E — Relocate misplaced domain logic to `DeckFlow.Core`** (deck-stat classifiers in DeckComparisonService; distill cost/validation in ContentKbCommandRunners). Effort M / Risk L. **DONE — Cycle 8 Phase 53.**
- **F — Strengthen dual-dialect abstraction** (33 `IsPostgres`/`IsSqlite` branches across 7 stores → dialect render methods; remove Web `Feedback*` SQL from Core `IRelationalDialect`). ⚠ Postgres path has no automated test. Effort M / Risk M. **PARTIAL — Cycle 8 Phase 53 (Feedback leak removed; full branch collapse deferred on PG DDL parity test).**
- **ADR-note tier:** G packet cache-key `IPacketCacheKeyStrategy` · H `IScryfallThrottle` seam · I `IMemoryCache` SizeLimit doc · J `System.CommandLine` beta4 deliberate-pin ADR · K residual test gaps (middleware-ordering integration test; Polly policy-shape assertion).

### Deferred to v1.7+ (per v1.5/v1.6 scope decisions)

- **Gemini paste-limit workaround** (`DECKFLOW_GEMINI_ENABLED` stays flag-gated; needs split-message vs direct-API path decision)
- **Embedding/vector retrieval** (pgvector / ONNX sentence-transformers) — deferred until corpus >~500 videos; RAM-cap risk at current size
- **Scheduled/cron harvest cadence (AUTO-03)** + **bulk creator-source onboarding (AUTO-04)** — Cycle 10 v2 (operator prefers manual curation this cycle)
- **KB-value A/B harness + `content.kb.enabled` decision gate (KBVAL-01/02)** — Cycle 10 v2

### Phase 63: Studio Self-Contained Executable — package DeckFlow.Studio as a self-contained single-file win-x64 executable the operator runs without a .NET install; produce the publish profile/script and document build+run steps (DIST-01)

**Goal:** The operator can run DeckFlow.Studio on a clean Windows box (no .NET installed) by launching a single self-contained `win-x64` executable produced by a repeatable, documented publish step.
**Requirements**: DIST-01
**Depends on:** Phase 62
**Status:** ✅ COMPLETE 2026-06-20 — verified PASS 7/7; operator clean-machine smoke passed (+ crash logging + browser auto-open)
**Plans:** 1/1 plans complete

Plans:
- [x] 63-01-PLAN.md — Self-contained win-x64 publish profile + publish scripts (ps1/sh) + Kestrel port pin + standalone-exe docs (DIST-01)

### Phase 70: Manabase Accuracy — Mana Quantity & Source Fidelity (ad-hoc trunk / main)

**Goal:** Raise the mana-base analysis from "rough heuristic" toward "trustworthy across real
Commander mana patterns" by fixing the verified Codex-audit accuracy defects — biggest win is
modeling how MUCH mana a source makes, not just which colors.
**Requirements:** MQ-01 (commander not drawable) · MQ-02 (per-source mana quantity) · MQ-03
(ramp-credit consistency) · MQ-04 (unsupported-interaction disclosure) · MQ-05 (color-aware
mulligan). **#4 joint-multicolor deficit deferred.**
**Source:** `.planning/captures/manabase-efficacy-findings.md` (Codex efficacy audit + research).
**Status:** 🚧 IN PROGRESS 2026-06-22 — **PRIORITY over Cycle 11** (operator 2026-06-22; Cycle 11 paused). Executing 70-01.
**Context:** `.planning/phases/70-manabase-accuracy-mana-quantity/70-CONTEXT.md`

Plans:
- [x] 70-01-PLAN.md — MQ-01 commander not drawn into the simulated library (done, `043a9157`)
- [x] 70-02-PLAN.md — MQ-02 per-source mana quantity — implemented behind `manabase.source-mana-quantity` flag (seeded OFF); Codex-approved; 155 Core tests green. Baseline diff run on real Brago deck (Sol Ring only → +1/+2 top-end, color/land/verdict unchanged). NOTE: Salubrious Snail / ScrollVault is a color-source-only tool and **cannot** validate the mana-quantity dimension — MQ-02 rests on golden-deck unit tests + magnitude sanity instead. Flag-default decision (keep OFF vs flip ON) is a judgment call, no longer gated on an external cross-check.
- [x] 70-03-PLAN.md — MQ-03 ramp-credit consistency (defect 2): narrowed `IsRampOrDraw` → repeatable ramp + draw only, behind `manabase.ramp-credit-v2` flag (seeded OFF); Codex-approved plan; `93afdbdf`, Core 164 + Web 75 green. ⏳ baseline-diff before flag defaults ON. Defect 1 (model land-ramp in sim) → 70-03b.
- [ ] 70-03b — MQ-03 defect 1: model credited land-ramp on the ramp-spell deploy event (quantity-only/colorless) so sim ↔ regression agree. Not started.
- [x] 70-04 — MQ-04 unsupported-interaction disclosure (done, `24aed27f` + `824a1c3a`)
- [x] 70-05-PLAN.md — MQ-05 color-aware London mulligan: a non-forced keep of a 2+ color deck also requires the opening lands to show >=2 distinct colors (KCap=2), behind `manabase.color-aware-mulligan` flag (seeded OFF). Cast%-affecting on multi-color decks only; mono decks byte-identical even flag-ON; verdict/color-count math untouched (probe path stays count-only). Codex-approved plan + diff; Core 10 + Web 2 tests green, full Core/Web suites clean. ⏳ baseline-diff before flag defaults ON; README/help update deferred until flip.
- [ ] 70-06-PLAN.md — Two-lens result header (Karsten source check + simulated cast rate); view-only, ships with the MQ changes. Mockup approved 2026-06-23.

---

*v1.0 shipped 2026-05-02 | v1.1 shipped 2026-05-08 | v1.2 shipped 2026-05-13 | v1.3 shipped 2026-05-23 | v1.4 shipped 2026-06-03 | v1.5 shipped 2026-06-10 | v1.6 shipped 2026-06-12 | v1.7 shipped 2026-06-17 | Cycle 8 shipped 2026-06-17 | Cycle 9 shipped 2026-06-19 | Cycle 10 shipped 2026-06-21 (`2026.06.6`)*
