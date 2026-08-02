---
gsd_state_version: 1.0
milestone: Cycle 20
milestone_name: Personal Tools
status: paused
stopped_at: Phase 112 in progress (3 of 6 plans) — PAUSED; active development is in the cycle21-cut-lab workstream
last_updated: 2026-08-01T21:45:00.000Z
last_activity: 2026-08-01 — no Cycle 20 activity; all work this session was in the cycle21-cut-lab workstream
progress:
  total_phases: 5
  completed_phases: 1
  total_plans: 11
  completed_plans: 8
  percent: 20
---

> ⚠ **This file tracks the MAIN workspace (Cycle 20) only. It is NOT where active work lives.**
> Active development is the **`cycle21-cut-lab` workstream** — its own state is
> `.planning/workstreams/cycle21-cut-lab/STATE.md`, and the live session handoff is
> `.planning/HANDOFF.json`. Read those two before resuming anything.
>
> Corrected 2026-08-01: this file had been stale since 2026-07-31, claiming Cycle 20 was
> `ready_to_plan` at Phase 112 with an arithmetically impossible `percent: 300`
> (`completed_phases: 6` against `total_phases: 2`). Phase 112 has in fact been under way since
> 2026-07-30.
>
> ⚠ **`ROADMAP.md` line 164 is also stale** — it lists Phase 112 as `0/0 | Not started`, but the
> phase directory holds six plans and three summaries. Not edited here: correcting the roadmap is
> outside this fix's scope, and that file has a history of being clobbered by planner runs.
> Plan counts above come from the phase directory, which is the ground truth.

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** none in this workspace — Cycle 20 is paused. Active focus is the `cycle21-cut-lab` workstream (Phase 4 plan-review convergence).

## Current Position

Phase: 112 — Cycle 17 Code Port
Plan: 3 of 6 complete (`112-01`, `112-02`, `112-03` have summaries; `112-04`–`112-06` do not)
Status: **Paused mid-phase**, not "ready to plan" — plans exist and three have executed
Last activity: 2026-07-30 (waves 1-3, `f23b7580`)

## Active Milestone

**Cycle 20 - Personal Tools**

Scope:

- Phase 112: Cycle 17 Code Port (PORT-01, PORT-02) — Cycle 17's Core engine (Phases 94-98) AND creator-style Web services + seed loader + DI registrations land on `feat/personal-tools`, build clean, DI resolves at startup.
- Phase 113: Shared-Infra Re-derivation (PORT-03) — highest-risk item: `ScryfallCollectionResolver`, `ScryfallLimits`, `CachedNameResolution`, dedicated `archidekt` pipeline re-derived line-by-line against current `main`, not applied wholesale (Cut Lab edited the same files across Cycles 18-19).
- Phase 114: Port Verification & Admin Personal-Tools Surface (PORT-04, PTOOL-01..04) — dead public-surface tests removed (not carried); `/Admin/CreatorStyle` + `/Admin/CreatorProfile` reachable only via BasicAuth; `/Admin` landing gets a personal-tools section.
- Phase 115: Real Data — Stated Rules & Operator Run (PSEED-01..05) — hand-authored stated-rules seed, `creator-style-import-stated` CLI command, `fuse-profile` reproducing the P89/P90 verdicts, operator export + commit of populated seeds, `/Admin/CreatorStyle` renders a real critique.

Source design spec: `docs/research/personal-tools-admin-reframe-design.md` (authoritative)
Planning docs: `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md`

## Open Threads

- **Cycle 17 — Creator-Style (separate worktree/branch `plan/cycle-17-creator-style`)** — superseded by Cycle 20's port-forward approach. Branch stays untouched at origin as historical record; NOT rebased, NOT resumed directly. Do not resume the old "rebase→push, seed export, flag flip" plan from that thread — Cycle 20 replaces it.
- **Cut Lab go-live** — flip `tool.cut-lab.enabled` ON via `/Admin/Tools` after prod UAT; then delete branch `gsd/cycle18-cut-lab`. Independent of Cycle 20.

## Deferred Items

Carried-forward operator gates and descoped items (still open):

| Category | Item | Status |
|----------|------|--------|
| Carry-forward | `deckflow_admin` credential deletion (password rotated) | Operator task |
| Carry-forward | Full dual-dialect branch collapse (PG DDL parity prereq) | Backlog |
| Carry-forward | SEO/growth lane (SEO-01..05) | Deferred |
| Carry-forward | Scheduled/bulk harvest (AUTO-03/04) | Deferred |
| Carry-forward | Matchup / meta-threat read (deepens cedh-meta-gap) | Deferred (separate lane) |
| Carry-forward | Manabase engine refactor (needs numeric-parity harness first) | Deferred (own future cycle) |
| Sync follow-ons | SYNC-F1 (retire DirectPush entirely) | Deferred — later-cycle decision |
| Sync follow-ons | SYNC-F2 (scheduled/automatic reconcile runs) | Deferred — operator-triggered only today |
| Cycle 20 out-of-scope | Distill toolchain install / re-distill 85-video Snail corpus | Deferred — hand-authored seed used instead |
| Cycle 20 out-of-scope | Public launch of creator-style (flag/registry/SEO/help) | Hard constraint (2026-07-19 legal review), not a deferral |
| Cycle 20 out-of-scope | Postgres migration of creator-style stores | Deferred — local `content-kb.db` + git-shipped seeds sufficient |
| Cycle 20 out-of-scope | Pet-card detection | Superseded pending EDHREC integration consideration |
| Test hygiene | `FeatureFlagStoreMigrationTests.Dispose()` calls process-global `SqliteConnection.ClearAllPools()`; under xUnit's parallel class execution it can dispose pooled `sqlite3` handles out from under a concurrently-running test in another class (`ObjectDisposedException: 'SQLitePCL.sqlite3'`). Seen once during Phase 109; did not reproduce on re-run. | Deferred — pre-existing flake, needs collection-fixture isolation |

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260726-mug | Fix hidden-field cache clobber on cEDH Meta Gap page (exclude WorkflowStep/FetchedEntriesJson/MetaGapPromptText from deck-sync.ts generic form persistence) | 2026-07-26 | 71c7b616 | [260726-mug-fix-hidden-field-cache-clobber-on-cedh-m](./quick/260726-mug-fix-hidden-field-cache-clobber-on-cedh-m/) |
| 260726-pxw | Fix hidden-field cache clobber on Cut Lab page (exclude CutLabStateJson from deck-sync.ts generic form persistence) | 2026-07-26 | ca48a41b | [260726-pxw-fix-hidden-field-cache-clobber-on-cut-la](./quick/260726-pxw-fix-hidden-field-cache-clobber-on-cut-la/) |
| 260802-i81 | Add Codex-facing AGENTS.md at repo root (module map + build/test + format carve-outs; cuts Codex repo re-derivation). File is a symlink to `~/.claude/codex/repos/deckflow-AGENTS.md` — versioned in dev-clunt, NOT in this public repo (AGENTS.md stays gitignored here by design) | 2026-08-02 | 75468329 | [260802-i81-add-codex-facing-agents-md-at-repo-root](./quick/260802-i81-add-codex-facing-agents-md-at-repo-root/) |

## Session Continuity

Last session: 2026-08-01T21:45:00.000Z
Stopped at: Nothing in this workspace. The 2026-08-01 session worked entirely in the `cycle21-cut-lab` workstream — folded Phase 4's round-4 plan-review findings (`5172f6d9`) and dispatched two Codex reviews (Phase 4 round 5, Phase 5 owed convergence).
Resume file: **`.planning/HANDOFF.json`** — the live handoff, and the correct entry point for resuming. Cycle 20's own resume point is Phase 112 plan 4 of 6.
Last activity: 2026-08-02 - Completed quick task 260802-i81: Add Codex-facing AGENTS.md at repo root (branch `docs/codex-agents-md`)

## Open Question (carried)

Cycle 20 (Phases 112-115, Personal Tools) has been paused since 2026-07-30 with Phase 112 half
executed. It was never formally deferred — it was overtaken by cycle21-cut-lab. Decide whether to
finish Phase 112 after the cut-lab milestone or move Cycle 20 to Deferred Items.
