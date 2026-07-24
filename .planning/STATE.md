---
gsd_state_version: 1.0
milestone: Cycle 19
milestone_name: Cut Lab Upgrade Hardening
status: executing
stopped_at: "Phase 109 executed + verified (2/2 plans). Next: `/gsd-plan-phase 110`."
last_updated: "2026-07-24T01:45:00.000Z"
last_activity: 2026-07-23 -- Phase 109 execution complete (what-if service consolidation)
progress:
  total_phases: 4
  completed_phases: 2
  total_plans: 5
  completed_plans: 5
  percent: 50
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 110 — cut-lab-navigation-and-pool-discovery (next to plan)

## Current Position

Phase: 109 (what-if-service-consolidation) — COMPLETE
Plan: 2 of 2
Status: Phase 109 executed and verified; Phase 110 not yet planned
Last activity: 2026-07-23 -- Phase 109 execution complete (what-if service consolidation)

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

Last session: 2026-07-23
Stopped at: Phase 109 executed + independently verified (2/2 plans, 5 commits). Next: `/gsd-plan-phase 110`.
Resume file: none
