---
gsd_state_version: 1.0
milestone: Cycle 16
milestone_name: Content-KB Prod↔Git↔Studio Sync Hardening
status: executing
stopped_at: Completed 89-05-PLAN.md
last_updated: "2026-07-07T18:09:23.691Z"
last_activity: 2026-07-07
progress:
  total_phases: 6
  completed_phases: 1
  total_plans: 9
  completed_plans: 8
  percent: 17
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-06)

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything. This cycle protects the Content-KB half of that promise.
**Current focus:** Phase 89 — content-hash-foundation

## Current Position

Phase: 89 (content-hash-foundation) — EXECUTING
Plan: 6 of 6
Status: Ready to execute
Last activity: 2026-07-07

Progress: [█████████░] 89%

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

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 89 P01 | 25m | 2 tasks | 3 files |
| Phase 89 P02 | 35m | 2 tasks | 4 files |
| Phase 89 P03 | 15m | 2 tasks | 3 files |
| Phase 89 P04 | 20min | 2 tasks | 5 files |
| Phase 89 P05 | ~50min | 2 tasks | 6 files |

## Accumulated Context

### Decisions

Full decision log lives in PROJECT.md Key Decisions table. Decisions constraining this milestone:

- **Git = single source of truth for bodies; prod DB row is subordinate and reconstructable from git.** All sync = idempotent one-way keyed upsert (design stance, `docs/research/kb-prod-sync-roadmap.md`).
- **No CDC/queue-based sync** — upsert + hash + expand-contract ordering fits the 512MB Render / single-operator scale.
- **Flags `sync.directpush-gitbody` and `sync.reconcile` seeded OFF** — operator flips on after prod deploy, matching every prior cycle's flag convention.
- **Decisions still owed at plan time** (per research doc, unresolved): (1) confirm approval ownership is local-authoritative for DirectPush (SYNC-04); (2) `sync.*` flag plumbing home — web-DB flag vs Studio config vs both, since Studio doesn't register the web flag system today.
- [Phase 89]: 89-02: SetBodySha256IfNullAsync declared as a throwing default interface method (mirrors DeleteAllRowsAsync) so 12 unrelated IContentSiteIndexStore test doubles compile unchanged
- [Phase 89]: 89-03: Fingerprint deleted; classifier equal-timestamp branch now calls ContentSiteIndexContentSignature.AreContentEqual (SYNC-02/D-03), UTC-direction branches (F-51-PG-01) untouched
- [Phase 89]: 89-04: bodySha256 added to the single shared export factory ContentIndexExportRow.From() (not to CLI/DirectPush consumers) so both inherit it automatically — SYNC-02 one-signature-one-home invariant extended to seed export (D-09)
- [Phase 89]: 89-05: publish-compute and detail render-guard both call ContentSiteIndexContentSignature.ComputeBodySha256, the ONE shared hash helper (D-01); guard is fail-open + structured-log on mismatch OR null/legacy stored hash, detail-render only, no feature flag (D-05/D-06/D-07)

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

Last session: 2026-07-07T18:09:23.671Z
Stopped at: Completed 89-05-PLAN.md
Resume file: None
