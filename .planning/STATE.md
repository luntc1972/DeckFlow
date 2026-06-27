---
gsd_state_version: 1.0
milestone: Cycle 11
milestone_name: Security, Visibility Control & Creator-Lens
status: executing
stopped_at: "Phase 69 PLANNED + DUAL-GATE CLEAN (fc83aebd): Claude plan-check PASSED + Codex PASS after closing dark-mode blocker over 3 revisions (bs-var bridge + base .table + form-check-input + .bg-light regression). Ready /gsd-execute-phase 69."
last_updated: "2026-06-27T19:13:19.693Z"
last_activity: 2026-06-27 -- Phase 73 planning complete
progress:
  total_phases: 10
  completed_phases: 3
  total_plans: 15
  completed_plans: 13
  percent: 30
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 72 SHIPPED to main (manabase command-zone commander castability); next is Phase 73 (deck-analysis command-zone awareness)

## Branch reconciliation (resolved 2026-06-22; re-merged 2026-06-25)

- The shipped **Manabase Analyzer** feature (built ad-hoc on `main`, deployed to prod 2026-06-22) is MERGED into `cycle11`. Tracked as un-numbered trunk features: `phases/manabase-modes-castability`, `phases/manabase-accuracy`, `phases/manabase-alt-cost` (de-numbered out of slot 64 so Cycle 11 owns phase 64).
- **2026-06-25 re-merge:** `main` had advanced 92 commits (Phase 70 manabase-accuracy: health-band coupling, ramp disclosure, manabase download action, set-packet scoring, Admin/Flags descriptions, SQLitePCLRaw 3.0.3 override). Merged main→cycle11. Only STATE.md conflicted (planning — took cycle11). The 3 overlapping code files (ManabaseController.cs, FeatureFlagStore.cs, admin-common.css) auto-merged; integration follow-up: Phase 70's new `/manabase` action(s) must carry the Phase 66 `[FeatureFlagGate]` or `ToolRouteGateCoverageTests` fails (the gate doing its job).
- Going forward: Cycle 11 ships via `cycle11→main` once Phase 69 lands. main is now merged in, so the ship is a fast-forward (no manabase loss).

## Current Position

Phase: 72 (command-zone commander castability) — SHIPPED to origin/main 5a78ffc2 (FF, CI green)
Plan: 7 of 7 complete
Status: Ready to execute
Last activity: 2026-06-27 -- Phase 73 planning complete

## Roadmap Summary

| # | Phase | Requirements | Status |
|---|-------|-------------|--------|
| 64 | Deck-Source Host Hardening | SEC-01, SEC-02, SEC-03 | ✅ Complete |
| 65 | Prod Content Artifact Reconcile | DATA-01, DATA-02 | ✅ Executed (operator-gated reconcile) |
| 66 | Admin Tool-Visibility Toggles + Tool Registry | TOGGLE-01..07 | ✅ Code-complete + reviewed (operator UAT pending) |
| 67 | Content KB Value A/B Validation | KBVAL-01, KBVAL-02 | ✅ Complete (decision: MARGINAL/NEGATIVE) |
| 68 | Creator-Philosophy Representation Research | CREATOR-01 (conditional on 67) | ⊘ Dropped (67 marginal) |
| 69 | Studio UI Design Pass — Shell, Dashboard & Responsive | STUI-01, STUI-02, STUI-03 | ✅ Complete + verified (11/11; sweep PASSED) |

**Phase ordering rationale:**

- **64 first**: SSRF/host-spoof is HIGH priority and lives in shared Core (`DeckEntryLoader`, `MoxfieldApiDeckImporter`) touching every deck tool; isolate it so the fix + regression tests land clean before other phases churn those files.
- **65 second**: Prod artifact gap is HIGH but largely investigation + an operator-run reconcile (AI stays read-only against prod); independent of the code phases.
- **66 third**: Largest feature (tool registry + nav/tiles/help/admin cascade + empty-section collapse); self-contained. Runs after the security fix so it isn't interleaved with shared-Core edits.
- **67 gate**: KBVAL must complete before creator-philosophy; its lift/marginal verdict decides whether Phase 68 runs at all and whether `content.kb.enabled` flips.
- **68 conditional**: Drops if KBVAL-02 is marginal. Research/design only — no production build of the philosophy layer this cycle.
- **69 last**: Studio UI pass is independent of the public-site work; presentation polish runs over settled surfaces last.

## Performance Metrics

**Velocity (Cycle 9 reference — most recent shipped):**

- 4 phases (55-58), 11 plans (2026-06-18 → 2026-06-19)
- Cross-AI execution: Codex codes, Claude reviews (TEMP OVERRIDE: Claude implements until ~2026-06-24)
- Final test gate: Core 475/475, Studio 49/49; build 0/0

## Accumulated Context

### Key Decisions

- **Phase 64 Plan 02 (4-site adoption + SC2 fix, 2026-06-21):** Spoof-URL test asserts null-capture (importer not called), not InvalidOperationException — MoxfieldParser accepts URL strings as implicit-quantity-1 card names, so both parsers succeed and no exception is thrown. Canonical Spellbook URL (`https://moxfield.com/decks/{deckId}`) always reconstructed from the already-parsed deckId, never forwarded from originalUrl. Three commits (aebfd8e8, de6d212a, 934b6789). 612 Core + 677 Web tests green.
- **Phase 64 Plan 01 (DeckSourceHost predicate, 2026-06-21):** `DeckSourceHost.IsMoxfield(Uri)` / `IsArchidekt(Uri)` use exact-or-approved-subdomain matching (`host == apex || host.EndsWith("." + apex)`). No `TrimEnd('.')` — trimming trailing dot would re-open confusable-domain surface. The `-warnaserror` flag cannot be used as the local gate because pre-existing NU1903/CS0618/CS1574 warnings are present; CI is the authoritative gate. 16/16 acceptance tests pass locally.
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

Last session: 2026-06-25T23:12:56.817Z
Stopped at: Phase 69 PLANNED + DUAL-GATE CLEAN (fc83aebd): Claude plan-check PASSED + Codex PASS after closing dark-mode blocker over 3 revisions (bs-var bridge + base .table + form-check-input + .bg-light regression). Ready /gsd-execute-phase 69.
Resume: Cycle 11 here on `cycle11`. Next: `/gsd-verify-work 64` then `/gsd-plan-phase 65`.

## Operator Next Steps

- Cycle 11 work continues HERE in `../deckflow-cycle11` (branch `cycle11`).
- Next: `/gsd-verify-work 64`, then `/gsd-plan-phase 65`.
- Push when ready: `cycle11` (this merge) and `main` (de-number commit `64fc72e9`) — AI awaits explicit push confirmation.
- `.manabase-brago-facts.json` at repo root is a harness cache — `git clean` it or add to `.gitignore` (do-not-modify list → operator).
