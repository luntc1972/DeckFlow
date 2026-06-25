---
gsd_state_version: 1.0
milestone: Cycle 10
milestone_name: — Studio Automation, Sync & Polish
status: phase_70_in_progress
stopped_at: Phase 70 health-band coupling shipped LOCAL (3 commits unpushed); Cycle 10 (59-63) shipped separately as 2026.06.6
last_updated: 2026-06-25T17:50:00.000Z
last_activity: 2026-06-25 — Health-band headline-floor done (bd26ac4b); tracked the 9 offline calibration fixtures the regression suite needs (were gitignored → CI/clone would fail) + committed manabase debug/nsm planning artifacts. main ahead 3, unpushed.
progress:
  total_phases: 5
  completed_phases: 5
  total_plans: 16
  completed_plans: 34
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 70 — Manabase Accuracy (ad-hoc trunk on `main`, NOT part of Cycle 10).
Health-band headline-floor coupling (the deferred 6th efficacy-audit defect) is DONE + committed
LOCAL (`bd26ac4b` + 2 docs commits, default ON, flag `manabase.health-band-headline-floor`); main
ahead 3, UNPUSHED. PRIORITY over Cycle 11 (operator decision 2026-06-22). **Cycle 11 is PAUSED** on
its `cycle11` worktree (was Phase 64 done → next 65; resume after Phase 70 ships). Phase 70 fixes the
verified Codex efficacy-audit defects — see `.planning/phases/70-manabase-accuracy-mana-quantity/`
and `.planning/captures/manabase-efficacy-findings.md`.

## ⚠ Branch divergence (read first)

Two parallel tracks exist — do not conflate:

- **`main` (this tree):** Cycle 10 shipped (`2026.06.6`). SINCE then a **Manabase Analyzer** feature was built and committed DIRECTLY on main (27 commits past the tag) and **deployed to prod 2026-06-22 ~12:33**. This violated the milestone-branch rule but is already live. Tracked (post-hoc, as un-numbered ad-hoc trunk features) under `phases/manabase-modes-castability`, `phases/manabase-accuracy`, `phases/manabase-alt-cost` — SUMMARYs reconstructed 2026-06-22. De-numbered out of slot 64 on 2026-06-22 so Cycle 11 owns phase 64.
- **`cycle11` worktree (`../deckflow-cycle11`):** official **Cycle 11 — Security, Visibility Control & Creator-Lens** (phases 64-69; phase 64 = *Deck-Source Host Hardening*). 35% done; Phase 64 complete+verified+secured+pushed; next `/gsd-plan-phase 65`.

**Reconciliation decided 2026-06-22:** (1) manabase de-numbered out of slot 64 (no more clash); (2) merge `main` → `cycle11` so cycle11 is the superset integration branch (manabase + security). Code is fully disjoint (no overlapping files). Cycle 11 ships via cycle11→main fast-forward when phases 65-69 are done.

## Current Position

Phase: 70 (Manabase Accuracy — ad-hoc trunk on main; Cycle 10 phases 59-63 shipped 2026.06.6)
Plan: Health-band headline-floor coupling — DONE, committed local, unpushed
Status: Phase 70 work shipped LOCAL — main ahead 3 of origin, awaiting operator push (Render deploy gated on checksPass)
Last activity: 2026-06-25 - Health-band floor (bd26ac4b) + tracked 9 calibration fixtures (gitignore negation) + committed debug/nsm planning artifacts

## Roadmap Summary

| # | Phase | Requirements | Status |
|---|-------|-------------|--------|
| 59 | Pipeline Automation | AUTO-01, AUTO-02 | ✅ Complete |
| 60 | Pull-from-Prod Reconcile | SYNC-01, SYNC-02, SYNC-03 | ✅ Complete |
| 61 | Creator Sources & Selection | SRC-01, SRC-02, HSEL-01, HSEL-02, HSEL-03 | ✅ Executed |
| 62 | Studio UI Polish | SUI-01, SUI-02, SUI-03, SUI-04, SUI-05, SUI-06 | ✅ Complete |
| 63 | Studio Self-Contained Executable | DIST-01 | ✅ Complete |

**Phase ordering rationale:**

- **59 first**: Auto-distill/auto-approve lives in the Core orchestrator distill/approve slice and redefines "harvested" vs "review-ready" vs "approved" state. HSEL-01's unharvested-only filter and SUI-01's status badges both read that state. Carries the AUTO-02 quality-signal open risk — isolating it contains that risk.
- **60 second, own phase**: Pull-from-Prod is the most novel/risky lane — a NEW authenticated prod READ path mirroring DirectPush (SSH.NET SCP from Render `/data` + Postgres read of `content_site_index`). Shares no surface with the Harvest.razor UX work; must not be diluted into a polish phase.
- **61 third**: Creator-source management + harvest selection (persisted creator list, dropdown picker, unharvested-only default, skip/ignore + un-skip) — the data-and-behavior pass over `Harvest.razor`. Depends on Phase 59's harvested-state definition for the default filter.
- **62 last**: Presentation pass (status badges, flow tightening, feedback states, layout/nav, creator filtering, the one-line MainLayout About-link fix) runs over the now-settled surfaces so polish isn't redone after 61 reshapes them. SUI-01 reuses the existing status engine; SUI-06 is a one-line fix.
- **No separate dogfood phase**: coarse granularity. Validation folds into per-phase operator success criteria (Phases 59 and 60 each carry observable operator gates).

## Performance Metrics

**Velocity (Cycle 9 reference — most recent shipped):**

- 4 phases (55-58), 11 plans (2026-06-18 → 2026-06-19)
- Cross-AI execution: Codex codes, Claude reviews (TEMP OVERRIDE: Claude implements until ~2026-06-24)
- Final test gate: Core 475/475, Studio 49/49; build 0/0

## Accumulated Context

### Key Decisions

- **Phase 63 added 2026-06-20 (DIST-01):** Package DeckFlow.Studio as a self-contained single-file win-x64 executable runnable without a .NET install; produce publish profile/script + document build/run steps. Last phase of Cycle 10 (after Phase 62 Studio UI Polish). Not planned yet → `/gsd-plan-phase 63`.
- **Branch rule reaffirmed 2026-06-20:** ALWAYS branch per milestone — Cycle 10 work belongs on its own `cycle10` branch, never piled on local main. ⚠ ANOMALY: a concurrent session switched the tree to `feat/analysis-prompt-recency-gate` mid phase-59 execution and interleaved unrelated manabase/analysis-prompt commits with the 59 commits. Needs operator decision to re-home Cycle 10 onto a clean `cycle10` branch once the concurrent session settles.
- **Cycle 10 roadmap created 2026-06-20:** 4 phases (59-62), 16/16 requirements mapped (AUTO, SYNC, SRC, HSEL, SUI). Phase numbering continues from 58. (Phase 63 DIST-01 appended 2026-06-20.)
- **Granularity = coarse:** 4 phases is the natural minimum. Automation (Core orchestrator slice) and the novel prod-read sync lane each earn their own phase; the 11 Studio-surface requirements split into one selection-mechanics pass (SRC + HSEL — persisted data + behavior) and one presentation-polish pass (SUI — over the same `Harvest.razor`/Studio shell).
- **Phase 59 owns the AUTO-02 quality-signal open risk:** A per-distill quality/confidence signal may not exist yet. Phase 59 must derive or add one from existing distill output (tag/clip/summary-completeness heuristics or returned model confidence) — NO distill provider/model swap permitted.
- **Phase 60 = Pull-from-Prod (promoted from backlog):** Read mirror of the existing DirectPush write path; read-only against prod (no write-back from this lane); uses the operator-local secret connection convention. Most novel/risky lane → isolated phase.
- **SRC/HSEL co-locate in Phase 61, SUI in Phase 62:** Both touch `Harvest.razor` + the Studio shell, but 61 is data/behavior (creator store, skip/ignore store, default filter) and 62 is presentation. Surface-grouping avoids redoing polish after 61 reshapes the surfaces.
- **SUI-01 reuses the Cycle 9 status engine** (`PublishStateDeriver` / `VideoStatusResolver`) — no duplicate status logic. **SUI-06 is a one-line MainLayout.razor About-link fix.**
- **Phase 59 Plan 03 (one-click harvest→auto-distill→auto-approve, AUTO-01/AUTO-02) executed 2026-06-20:** New Studio Harvest "Harvest + Auto-distill" button (beside the kept "Harvest Selected" fallback, D-12). Subscription path: harvest → `ListPendingDistillAsync` ∩ selected = harvest-ready ids (HIGH #2/D-10, excludes skipped/no-caption/already-distilled) → inline `DistillAsync(dryRun:false)` → shared `ApplyAutoApproveAsync` (batch `SetApprovalStatusAsync('approved')` for distills ≥ persisted cutoff via `IAutoApproveSignal`; approval_status only, T-59-06). Metered providers harvest then STOP with a requires-subscription message — no DistillAsync, no silent spend (D-08 AMENDED, SC4; Core refuses at ContentKbOrchestrator.cs:244). `ApplyAutoApproveAsync` is SHARED — also wired into manual `RunDistillStageBAsync` so a manual subscription distill auto-approves too (D-09 reuse); metered auto-approve DEFERRED. Single per-video outcome card, all counts canonical-sourced (D-11/HIGH #3). `IAutoApproveSignal` DI registered. Studio.Tests HarvestPage 18/18 green (8 one-click + 1 manual Stage B added); build 0err. README documents the subscription-only one-click default + auto-approve panel. Commits 7a5cbd95 (RED) + e9ae01c9 (GREEN) + 729b5e4d (unpushed, on branch feat/analysis-prompt-recency-gate). **Task 3 = operator end-to-end human-verify checkpoint (blocking) PENDING.**
- **Phase 59 Plan 02 (auto-approve settings UI, AUTO-02) executed 2026-06-20:** `AutoApproveSettings` record (Default ON/5 sourced from `ClipCountAutoApproveSignal.DefaultCutoff`, D-03/D-06) + file-backed `AutoApproveSettingsStore` (`auto-approve-settings.json` in studio data dir, persists across restarts D-07) with a semantic clamp applied on Load AND Save (negative→5, >MaxCutoff 1000→1000, T-59-03; corrupt JSON → safe defaults, never throws). Harvest-page "Auto-approve" panel (toggle + cutoff, disabled-when-off, saved on commit not keystroke — Codex MEDIUM). DI singleton wired. Studio.Tests 8 store + 3 bUnit green; no regression. Commits a881c4c1 + 6360aaa8 (unpushed). Plan 03 reads these settings to drive one-click auto-approval.
- **Phase 59 Plan 01 (AUTO-02 signal) executed 2026-06-20:** Auto-approve decision isolated behind `IAutoApproveSignal` (swappable, D-02) with `ClipCountAutoApproveSignal` (`clipCount >= cutoff`, `DefaultCutoff = 5`, D-03). `DistillResult.DistilledVideos` surfaces natural key (YouTube OR podcast) + clip count per distilled video (D-01, D-11). Distill schema/provider/model unchanged — no confidence field (SC4). Core records the count only; approval_status flip is the Studio host's job (Plan 03). Core.Tests 511/0. Commits 44d9d630 + e1c15c79 (unpushed).

### Key Pitfalls to Watch

- **AUTO-01 must keep the spend dry-run / cap gate:** Auto-distill on harvest cannot bypass the existing spend ceiling or the `LlmDistillationProviderFactory` (no provider/model swap).
- **AUTO-02 signal sourcing:** Derive the quality/confidence signal from existing distill output only. If no usable signal exists, deriving a heuristic is in-scope; a model swap to obtain one is NOT.
- **SYNC lane is prod-READ-only:** The pull path must never write back to prod (AI-never-writes-prod rule). It writes LOCAL only. The artifact pull needs a NEW SCP-download counterpart — only SCP upload (`ISshArtifactUploader`) exists today.
- **SYNC merge semantics unresolved:** `approval_status` (prod `pending` vs local `approved`), `is_visible`/`is_hidden`/`pushed_to_prod_utc` reconciliation, prod-wins vs local-wins — resolve at `/gsd-plan-phase 60`.
- **HSEL skip/ignore ≠ Block:** Skip is a lightweight "don't surface this candidate again" — NO artifact hard-delete, NO blocklist entry. Block (hard-delete + blocklist, Phase 37.6 / Cycle 9) stays separate.
- **Studio is the stock Blazor template:** Per-page badge colors are defined ad-hoc today; SUI-01 must unify them through the shared status engine, not add yet another ad-hoc color set.

### Pending Todos

- 15 pre-v1.5 open artifacts (stale 999.x/v13 debug sessions, May quick-task refs, empty todos) — acknowledged cruft; clean via `/gsd-cleanup` when convenient.

### Blockers/Concerns

- **AUTO-02 quality/confidence signal may not exist yet** — flagged as Phase 59 open risk; resolution (derive vs add heuristic) decided at plan-phase.

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260624-kpg | Fix DFC/transform cards excluded from set-packet top-60 via face-aware scoring | 2026-06-24 | a01473cc | [260624-kpg-fix-dfc-transform-cards-excluded-from-se](./quick/260624-kpg-fix-dfc-transform-cards-excluded-from-se/) |
| 260624-opb | Download manabase analysis as paste-ready .txt via ManabaseReportTextBuilder | 2026-06-24 | 3c5155c3 | [260624-opb-be-able-to-download-the-manabase-analysi](./quick/260624-opb-be-able-to-download-the-manabase-analysi/) |

### Carry-Forward (still open from prior cycles)

| Item | Status |
|------|--------|
| `deckflow_admin` credential deletion (password rotated; deletion owed by operator) | Operator task — not a code requirement |
| Operator live Gemini paste + `DECKFLOW_GEMINI_ENABLED` prod flip | Operator manual verification — out of Cycle 10 scope |
| Full dual-dialect branch collapse (gated on PG DDL parity test) | Backlog — deferred |
| Prod-DB dedup SQL for @salubrioussnail duplicate grid rows | Operator task — tracked in `project_cycle8_contentkb_cleanup.md` |
| Prod harvest green-run not yet observed since F-51-PG-01 deploy | Operator verification — may surface during Cycle 10 dogfooding |
| `e3qGnuupp8U` durability (in prod DB, not git seed) | A future reset+reseed omits it until a full git-Publish — Phase 60 Pull-from-Prod may surface this |
| Phase 62-studio-ui-polish P02 | 40m | 7 tasks | 6 files |
| Phase 62-studio-ui-polish P03 | 25m | 7 tasks | 4 files |
| Phase 62-studio-ui-polish P04 | 25m | 6 tasks | 5 files |

## Deferred Items

**Cycle 10 v2 (deferred this cycle, tracked in REQUIREMENTS.md):**

- KBVAL-01/02 — KB-value A/B harness + `content.kb.enabled` decision gate
- AUTO-03 — scheduled/cron harvest cadence
- AUTO-04 — bulk/at-scale creator-source onboarding

**Acknowledged at Cycle 10 close (2026-06-21):**

| Category | Item | Status |
|----------|------|--------|
| uat | Phase 62 live-UI operator smoke (creator filters Harvest+Review, Pull-from-Prod live streaming, grouped nav + About link) | DEFERRED — operator validates live later; backed by bUnit + 62-VERIFICATION 6/6 |
| uat | Phase 51 `51-UAT-RESULTS.md` / `51-STUDIO-UAT-RESULTS.md` (stale Cycle-8 artifacts) | 0 pending scenarios — informational only |
| backlog | Prod-artifact gap: 86/109 content rows have no `.md` on Render `/data` | HIGH priority — see ROADMAP Backlog |

**Open / carried forward:**

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| tech_debt | Gemini paste-limit workaround | DEFERRED (flag-gated `DECKFLOW_GEMINI_ENABLED`) | v1.5 scoping |
| arch | Full dual-dialect branch collapse (PG DDL parity prereq) | DEFERRED (backlog) | Cycle 8 Phase 53 |
| housekeeping | 15 pre-v1.5 open artifacts | ACKNOWLEDGED — clean via `/gsd-cleanup` | v1.5 close 2026-06-10 |
| ops | SEL-02 expert-pin live-pin re-confirm | PENDING — needs KB-enable window | v1.5 close |
| ops | `deckflow_admin` credential deletion | Operator task (password already rotated) | Cycle 8 Phase 52 |
| growth | SEO/growth/ops lane (SEO-01..05) | DEFERRED — separate lane | Cycle 10 scoping |

## Session Continuity

Last session: 2026-06-22T19:00:00Z
Stopped at: Reconciled main planning — wrote reconstructed manabase SUMMARYs (64-01, 64-02, manabase-accuracy, manabase-alt-cost), updated STATE, deleted the stale Cycle-10 phase-60 HANDOFF.json. Untracked at repo root: `.manabase-brago-facts.json` (harness cache — left untracked, candidate for .gitignore).
Resume: main is honest. Decide branch reconciliation (manabase-on-main vs Cycle 11 worktree), then either resume Cycle 11 (`/gsd-plan-phase 65` in `../deckflow-cycle11`) or start a Cycle 12 to absorb manabase.

## Operator Next Steps

- Decide how to reconcile main's shipped manabase feature with the Cycle 11 worktree track.
- To continue Cycle 11: work in `../deckflow-cycle11`, run `/gsd-plan-phase 65`.
- `.manabase-brago-facts.json` at repo root is a harness cache — add to `.gitignore` (operator: that file is in the do-not-modify list) or `git clean` it.
