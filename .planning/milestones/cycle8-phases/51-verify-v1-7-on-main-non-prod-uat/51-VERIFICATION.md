---
phase: 51-verify-v1-7-on-main-non-prod-uat
status: passed
verified: 2026-06-17
requirements:
  HARD-01: passed
  HARD-03: passed
  OPS-01: passed
---

# Phase 51 Verification — Verify v1.7 on main + non-prod UAT

**Goal:** Confirm v1.7 works end-to-end on `main` (deferred operator-UAT smokes recorded),
Postgres Dapper parity confirmed, and Render deploying from `main`.

**Outcome: PASSED.** All three requirements met. HARD-03 initially surfaced a real defect (the
purpose of the gate); it was fixed same-session (F-51-PG-01, commit `c4b625e`) and the gated
suite re-ran green (19/19).

## Requirement results

### HARD-01 — deferred non-prod operator-UAT smokes — PASSED
- P44 `/Admin/Harvest` lazy-grid (51-01): PASS — no scroll-jump, AJAX lazy-load, pagination swap, cross-origin 403.
- P41 Studio render (51-02): PASS — all routes render, presence-only secret logging.
- P45 re-distill (51-02): PASS live (`redistill=true`, `videos_distilled=1`, $0, summary overwritten). Cap-raise PASS. Cap-block-enforcement WAIVED (provider-gated, needs metered openai). Cancel-on-dispose WAIVED (timing; unit-tested).
- P46 Review (51-02): PASS — tabs/counts/filter/expand-preview/approve all verified. Publish render + no-push confirmed; real commit WAIVED per operator.

### HARD-03 — Postgres-gated parity suite — PASSED (after same-session fix)
- Initial run: 16/19; 3 failed with one root cause: `CategoryKnowledgeRepository.AddDeckIdsAsync` compared the TEXT `last_checked_utc` column against a timestamptz-bound `DateTime` param → Npgsql `42883`. Real Phase 49 Dapper parity gap on the live prod Postgres path.
- **Fixed same-session** (F-51-PG-01, commit `c4b625e`): cast the column to `timestamptz` in that comparison on Postgres only (dialect-guarded; no schema migration / data backfill). Re-ran the gated suite → **19/19**; SQLite CategoryKnowledge tests 20/20 unchanged. HARD-03 CLOSED.

### OPS-01 — Render deploy branch → main + branch base — PASSED
- Render `DeckFlow` deploys from `main`; latest deploy live; site 200; deployed tree identical to v1.7 squash. Cycle 8 branch base confirmed (v1.7 squash is ancestor of `origin/main`, CalVer tag present).

## Follow-ups
- **F-51-PG-01** — RESOLVED same-session (commit `c4b625e`). No open follow-up.
- (optional, Phase 53) deeper schema parity: `deck_queue`/`last_seen_utc` datetime columns are TEXT on both dialects, unlike `ContentVideoStore` which uses provider-split `TIMESTAMPTZ` on Postgres. The cast fix closes the bug without a migration; aligning the DDL is a non-urgent arch-consistency item.

## Evidence
`51-UAT-RESULTS.md`, `51-STUDIO-UAT-RESULTS.md`, `51-PG-PARITY-RESULTS.md`, `51-OPS-RESULTS.md`.
