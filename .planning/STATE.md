---
gsd_state_version: 1.0
milestone: Cycle 16
milestone_name: Content-KB Prod↔Git↔Studio Sync Hardening
status: Executed on cycle16 branch — awaiting operator ff→main + push (D-16)
stopped_at: Phase 89 context gathered
last_updated: "2026-07-07T03:06:54.182Z"
last_activity: "2026-07-06 -- Phase 88 executed: approval mirror + serve filters + composite keying + prod DDL guard"
progress:
  total_phases: 6
  completed_phases: 1
  total_plans: 3
  completed_plans: 3
  percent: 17
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-06)

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything. This cycle protects the Content-KB half of that promise.
**Current focus:** Phase 88 — Index-Row Integrity Hotfix (ships first — live prod bugs)

## Current Position

Phase: 88 of 93 (Index-Row Integrity Hotfix) — EXECUTED (3/3 plans)
Plan: 88-01 + 88-02 + 88-03 complete (commits 82b6b911, b5396e46, a19735b1)
Status: Executed on cycle16 branch — awaiting operator ff→main + push (D-16)
Last activity: 2026-07-06 -- Phase 88 executed: approval mirror + serve filters + composite keying + prod DDL guard

Progress: [██░░░░░░░░] 17%

## Roadmap Summary

| # | Phase | Requirements | Flag | Status |
|---|-------|-------------|------|--------|
| 88 | Index-Row Integrity Hotfix | SYNC-04, SYNC-05, SYNC-06 | — | ✅ Complete |
| 89 | Content-Hash Foundation | SYNC-01, SYNC-02, SYNC-03 | — | Not started |
| 90 | DirectPush Correctness + Seed Sync | SYNC-07, SYNC-08, SYNC-09, SYNC-10 | `sync.directpush-gitbody` | Not started |
| 91 | Reconcile + Seed Lifecycle | SYNC-17, SYNC-11, SYNC-12 | `sync.reconcile` | Not started |
| 92 | Pull Hardening | SYNC-13, SYNC-14, SYNC-15 | — | Not started |
| 93 | Round-Trip Integration Test | SYNC-16 | — | Not started |

**Phase ordering rationale** (Codex-revised sequencing from `docs/research/kb-prod-sync-roadmap.md`):

- **88 first**: SYNC-04/05/06 are live prod correctness bugs (visible-while-pending rows, PinId collision risk) — ship ahead of the hash foundation per Codex's MED finding that the hotfix slice is immediately user-visible.
- **89 second**: the unified `body_sha256` signature is a hard prerequisite for Phase 90's hash-gated expand-contract ordering.
- **90 third**: split per Codex HIGH into one phase covering both the DirectPush architecture flip (bodies via git only) and the ordering/stamping fix — sequenced as explicit sub-scopes within the phase, not two phases, to keep coverage 1:1 with requirements while preserving the internal order.
- **91 fourth**: SYNC-17's seed-ownership marker is a hard prereq (Codex HIGH) before SYNC-11's reconciler or SYNC-12's seed-delete can ship; internal order within the phase is marker → reconciler (dry-run) → gated delete.
- **92 fifth**: reuses the composite-key diffing (Phase 88) and reconcile discrepancy vocabulary (Phase 91).
- **93 last**: the round-trip test exercises every prior phase's fix; it cannot be written until all of them exist.

## Performance Metrics

**Velocity (Cycle 15 reference — most recent shipped):**

- Phases 82-87; 22 plans, 42 tasks; build 0/0 at close
- Claude implements + reviews code; Codex (gpt-5.4 medium) reviews plans + code (delegation rule per CLAUDE.md)

No plans executed yet this cycle — velocity table resets at first plan completion.

## Accumulated Context

### Decisions

Full decision log lives in PROJECT.md Key Decisions table. Decisions constraining this milestone:

- **Git = single source of truth for bodies; prod DB row is subordinate and reconstructable from git.** All sync = idempotent one-way keyed upsert (design stance, `docs/research/kb-prod-sync-roadmap.md`).
- **No CDC/queue-based sync** — upsert + hash + expand-contract ordering fits the 512MB Render / single-operator scale.
- **Flags `sync.directpush-gitbody` and `sync.reconcile` seeded OFF** — operator flips on after prod deploy, matching every prior cycle's flag convention.
- **Decisions still owed at plan time** (per research doc, unresolved): (1) confirm approval ownership is local-authoritative for DirectPush (SYNC-04); (2) `sync.*` flag plumbing home — web-DB flag vs Studio config vs both, since Studio doesn't register the web flag system today.

### Pending Todos

None yet — milestone just started.

### Blockers/Concerns

- **Live prod drift exists today** (2026-07-05 read-only audit): 106 prod rows with only 36 in the approved seed (70 not reconstructable from a reset), 57 hidden+pending rows re-accumulated after a manual delete, ~328 file-without-row orphans, 32 mojibake bodies (15 prod-visible, repaired out-of-band). This is the motivating evidence for the cycle, not new risk introduced by it — Phase 91's reconciler and Phase 89's body-hash are the systemic fixes.
- **`sync.*` flag plumbing is undecided** — resolve before/during Phase 90 planning (see Decisions above).

## Deferred Items

Carried forward from Cycle 15 close (2026-07-05) — none are Cycle-16 gaps:

| Category | Item | Status |
|----------|------|--------|
| Carry-forward | `deckflow_admin` credential deletion (password rotated) | Operator task |
| Carry-forward | Full dual-dialect branch collapse (PG DDL parity prereq) | Backlog |
| Carry-forward | SEO/growth lane (SEO-01..05) | Deferred |
| Carry-forward | Scheduled/bulk harvest (AUTO-03/04) | Deferred |
| Carry-forward | Matchup / meta-threat read (deepens cedh-meta-gap) | Deferred (separate lane) |
| Carry-forward | Manabase engine refactor (needs numeric-parity harness first) | Deferred (own future cycle) |
| Carry-forward | ADMIN-01 (`/Admin/Flags` on/off sorting) | Descoped to backlog |
| Sync follow-ons | SYNC-F1 (retire DirectPush entirely) | Deferred — later-cycle decision |
| Sync follow-ons | SYNC-F2 (scheduled/automatic reconcile runs) | Deferred — this cycle ships operator-triggered only |

## Session Continuity

Last session: 2026-07-07T03:06:54.142Z
Stopped at: Phase 89 context gathered
Resume file: .planning/phases/89-content-hash-foundation/89-CONTEXT.md
