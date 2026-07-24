---
gsd_state_version: 1.0
milestone: Cycle 19
milestone_name: Cut Lab Upgrade Hardening
status: verifying
stopped_at: Phase 110 UI-SPEC approved
last_updated: "2026-07-24T19:36:18.802Z"
last_activity: 2026-07-24 -- Phase 111 execution started
progress:
  total_phases: 5
  completed_phases: 4
  total_plans: 18
  completed_plans: 14
  percent: 78
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 111 shipped (gsd-verifier 6/6). Cycle 19 awaiting: push branch → CI green → merge to main → release 2026.07.9 → flag flip UAT

## Current Position

Phase: 111 (cut-lab-upgrade-regression-gate) — EXECUTING
Plan: 1 of 4
Status: Cycle 19 complete — all phases (108-111) done + verified; ready to merge (user pushes)
Last activity: 2026-07-24 -- Phase 111 execution started

## Active Milestone

**Cycle 19 - Cut Lab Upgrade Hardening**

Scope:

- Phase 108: server-authored Cut Lab UI patch DTOs replace client-side domain re-derivation.
- Phase 109: shared what-if preview/commit service for JSON and no-JS paths.
- Phase 110: Cut-Lab-scoped navigation and pool discovery: anchors, mobile sticky jump navigation, lock-pool filtering/search, collapsible sections, package helper copy, and text-first card/combo context disclosures.
- Phase 111: regression gate covering card-pill locking, Structural evidence pills, themes, xUnit, Vitest, TypeScript, and browser smoke.

Source backlog: `.planning/milestones/ws-cut-lab-2026-07-23/BACKLOG-cut-lab-followups-2026-07-22.md`
Planning docs: `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md`

## Open Threads

- **Cycle 17 — Creator-Style** — code-complete, ON HOLD in a **separate worktree** (its own `.planning`). Was gated on Cut Lab landing on main (now done, 2026-07-23) → unblocked. Resume there: rebase→push, seed export, flag flip, verify-work UAT, post-merge SeoPaths, `/gsd-complete-milestone`.
- **Cut Lab go-live** — flip `tool.cut-lab.enabled` ON via `/Admin/Tools` after prod UAT; then delete branch `gsd/cycle18-cut-lab`. Cycle 19 hardening can run before or after the flag flip, but should not silently broaden go-live scope.

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
| Cut Lab follow-ons | F1 (cut-lab.ts re-derives server state), F2 (what-if validation 3-site split), mobile jump-nav | Promoted to Cycle 19 |
| Test hygiene | `FeatureFlagStoreMigrationTests.Dispose()` calls process-global `SqliteConnection.ClearAllPools()`; under xUnit's parallel class execution it can dispose pooled `sqlite3` handles out from under a concurrently-running test in another class (`ObjectDisposedException: 'SQLitePCL.sqlite3'`). Seen once during Phase 109; did not reproduce on re-run. | Deferred — pre-existing flake, needs collection-fixture isolation |

## Session Continuity

Last session: 2026-07-24T03:44:12.294Z
Stopped at: Phase 110 UI-SPEC approved
Resume file: .planning/phases/110-cut-lab-navigation-and-pool-discovery/110-UI-SPEC.md
