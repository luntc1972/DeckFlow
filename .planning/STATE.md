---
gsd_state_version: 1.0
milestone: Cycle 20
milestone_name: Personal Tools
status: Roadmapped
stopped_at: Phase 112 context gathered
last_updated: "2026-07-24T22:49:17.451Z"
last_activity: 2026-07-24 — Cycle 20 roadmap created (Phases 112-115)
progress:
  total_phases: 1
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Cycle 20 (Personal Tools) roadmapped: 4 phases (112-115) on branch `feat/personal-tools`, porting Cycle 17's creator-style engine as admin-only tooling. Cycle 19 closed out. Owed (user): flip `tool.cut-lab.enabled` ON in prod `/Admin/Tools` for go-live UAT (currently OFF); then delete branch `gsd/cycle19-cut-lab-upgrade` after UAT.

## Current Position

Phase: Not started (roadmap approved, planning next)
Plan: —
Status: Roadmapped
Last activity: 2026-07-24 — Cycle 20 roadmap created (Phases 112-115)

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

## Session Continuity

Last session: 2026-07-24T22:49:17.433Z
Stopped at: Phase 112 context gathered
Resume file: .planning/phases/112-cycle-17-code-port/112-CONTEXT.md
