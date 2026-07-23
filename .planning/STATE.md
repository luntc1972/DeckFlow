---
gsd_state_version: 1.0
milestone: none
milestone_name: Between milestones (mainline)
status: between_milestones
stopped_at: Cut Lab (Cycle 18) shipped to main as 2026.07.8; planning reverted to flat mode
last_updated: 2026-07-23T16:45:00.000Z
last_activity: 2026-07-23 -- Cut Lab merged to main, released 2026.07.8, docs updated, planning tree cleaned
progress:
  total_phases: 0
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Mainline is between milestones. Next active work is resuming Cycle 17 (Creator-Style) in its separate worktree, then a new milestone.

## Current Position

No active milestone in flat (mainline) planning. The two most recent milestones both shipped:

- **Cycle 18 — Cut Lab** (Phases 101-107) — merged to main and released **2026.07.8** on 2026-07-23; deployed **dark** (`tool.cut-lab.enabled` seeds OFF, flip after prod UAT). Archived to `milestones/ws-cut-lab-2026-07-23`.
- **Cycle 16 — Content-KB Prod↔Git↔Studio Sync Hardening** (Phases 88-93) — shipped 2026-07-11 (`2026.07.3`). Archived to `milestones/cycle16-ROADMAP.md` + `milestones/cycle16-phases/`.

Full milestone history: `.planning/ROADMAP.md` (Milestones section) and `.planning/MILESTONES.md`.

## Open Threads

- **Cycle 17 — Creator-Style** — code-complete, ON HOLD in a **separate worktree** (its own `.planning`). Was gated on Cut Lab landing on main (now done, 2026-07-23) → unblocked. Resume there: rebase→push, seed export, flag flip, verify-work UAT, post-merge SeoPaths, `/gsd-complete-milestone`.
- **Cut Lab go-live** — flip `tool.cut-lab.enabled` ON via `/Admin/Tools` after prod UAT; then delete branch `gsd/cycle18-cut-lab`.

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
| Cut Lab follow-ons | F1 (cut-lab.ts re-derives server state), F2 (what-if validation 3-site split), mobile jump-nav | Backlog — `milestones/ws-cut-lab-2026-07-23/BACKLOG-cut-lab-followups-2026-07-22.md` |

## Session Continuity

Last session: 2026-07-23
Stopped at: Cycle 18 close-down + planning-tree sweep complete (docs updated, phase dirs archived, STATE refreshed). Awaiting quick-task ledger triage decision.
Resume file: none
