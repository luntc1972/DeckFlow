---
phase: 93-round-trip-integration-test
plan: 03
subsystem: testing
tags: [operator-checklist, content-kb, feature-flags, sync-16, documentation]

# Dependency graph
requires:
  - phase: 90-directpush-correctness-seed-sync
    provides: FU-1/FU-2 deferred follow-ups (90-FOLLOWUPS.md) and the sync.directpush-gitbody flag
  - phase: 91-reconcile-seed-lifecycle
    provides: FU-3 deferred follow-up (91-09) and the sync.reconcile flag
provides:
  - "93-PREFLIP-CHECKLIST.md — single operator artifact consolidating FU-1/FU-2/FU-3 as explicit pre-flip decisions/actions, plus live flip steps for both sync.directpush-gitbody and sync.reconcile"
affects: [operator flag-flip workflow, cycle-16 close-out]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Operator pre-flip gate as a standalone checklist artifact (not appended to an existing doc) — one reviewable file per D-08"

key-files:
  created:
    - .planning/phases/93-round-trip-integration-test/93-PREFLIP-CHECKLIST.md
  modified: []

key-decisions:
  - "Checklist lives as its own new file (93-PREFLIP-CHECKLIST.md), not appended to 90-FOLLOWUPS.md or 93-CONTEXT.md, per D-08 planner's-call — keeps the operator gate self-contained and reviewable independent of phase context docs."

requirements-completed: [SYNC-16]

# Metrics
duration: ~10min
completed: 2026-07-10
---

# Phase 93 Plan 03: Operator Pre-Flip Checklist Summary

**Single D-08 operator artifact (93-PREFLIP-CHECKLIST.md) consolidating FU-1/FU-2/FU-3 from 90-FOLLOWUPS.md into explicit pre-flip decisions plus live flip steps for both `sync.directpush-gitbody` and `sync.reconcile` — doc-only, no code touched.**

## Performance

- **Duration:** ~10 min
- **Tasks:** 1 completed
- **Files modified:** 1 (new file)

## Accomplishments

- Created `.planning/phases/93-round-trip-integration-test/93-PREFLIP-CHECKLIST.md` covering:
  - D-07 gate note: SYNC-16 test is a local/manual `[PostgresFact]` Docker gate that auto-skips in CI (CI has no `DECKFLOW_POSTGRES_TESTS=1`/Docker), with the exact local run command
  - FU-1 (stale-visible-on-update): mechanism explained, explicit Option A (accept-by-design + fix Stage-4 copy) vs Option B (hide-then-reconfirm) decision checkboxes, code fix stated as deferred either way
  - FU-2 (indeterminate-flag strand): mechanism, safe/recoverable framing, decision/action checkbox on whether to build a resume-triggers-redeploy fix (deferred)
  - FU-3 (live reconcile walk): 5 ordered ACTION checkboxes (dry-run live → review counts vs 2026-07-05 baseline → review D-06 report → confirm no prod write → scoped Apply confirming seed-owned-only soft-hide + prod-owned-stays-visible)
  - Live flip steps for `sync.directpush-gitbody` ON (5-step ordered checklist ending in a post-flip smoke)
  - Live flip steps for `sync.reconcile` ON (3-step ordered checklist)

## Task Commits

1. **Task 1: Write the operator pre-flip checklist (D-08)** - `b188d973` (docs)

## Files Created/Modified

- `.planning/phases/93-round-trip-integration-test/93-PREFLIP-CHECKLIST.md` - New operator-actionable Markdown checklist; consolidates FU-1/FU-2/FU-3 into explicit decision/action checkboxes and gives live flip steps for both prod flags shipped OFF this cycle.

## Decisions Made

- Checklist created as its own standalone file rather than appended to `90-FOLLOWUPS.md` or `93-CONTEXT.md` — keeps the operator gate self-contained (Claude's Discretion item in `93-CONTEXT.md`, "where the pre-flip checklist lives... planner's call").

## Deviations from Plan

None - plan executed exactly as written. All acceptance criteria satisfied:
- File exists at the specified path.
- Covers FU-1, FU-2, FU-3 verbatim (grep confirms all three labels present).
- Contains live flip steps for both `sync.directpush-gitbody` and `sync.reconcile` (grep confirms both flag names present).
- States FU-1/FU-2 code stays deferred and that the SYNC-16 test is a local Docker gate that auto-skips in CI (grep confirms "auto-skip" present, case-insensitive).
- No code file modified; no `.github/workflows/` change (only the one new `.planning` doc was touched).

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required. This document itself is the operator's action item; nothing here requires immediate setup, only future manual review before flag flips.

## Next Phase Readiness

- Cycle 16's DOC-ONLY deliverable for Phase 93 is complete. Combined with 93-01 (harness infrastructure, already executed) and 93-02 (round-trip assertions, not yet executed per plan-checker gate noted at dispatch time), this closes out the phase's third and final plan.
- Operator now has a single reviewable gate (this checklist) to walk through before flipping either `sync.directpush-gitbody` or `sync.reconcile` in prod.
- No blockers introduced by this plan; 93-02's execution status is independent of this doc-only deliverable.

---
*Phase: 93-round-trip-integration-test*
*Completed: 2026-07-10*
