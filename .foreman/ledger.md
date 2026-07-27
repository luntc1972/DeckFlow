# Foreman Ledger — Cycle 20 "Personal Tools" milestone setup

**Run started:** 2026-07-24
**Mode:** Codex-boosted (Agent tool + real shell + consented Codex CLI 0.145.0)
**LEAD:** Opus 5 (frontier-class)
**Baseline commit:** `6ebfc69a`
**Branch:** `feat/personal-tools`
**Tracked tree at baseline:** clean (56 untracked — pre-existing planning screenshots + archived ledgers)

## Authoritative input

`docs/research/personal-tools-admin-reframe-design.md` (committed `6ebfc69a`) — design spec approved
by user 2026-07-24. Admin-only reframe of Cycle 17 creator-style; fresh-branch code port;
hand-authored stated-rules seed; Deck Tendencies included.

## Routing decisions

- Milestone decomposition kept at LEAD — frontier judgment, not delegable without lowering the bar
  (First Law). No fan-out for this step.
- Codex seats per user's `CLAUDE.md`: review/planning `gpt-5.5` medium, coding `gpt-5.4` medium.
  Confirmed by user this session ("pass" = keep defaults).
- Codex enters at plan-review, per the standing "Codex codes, Claude reviews" + "Codex reviews
  plans" rules.

## Tasks

| ID | Task | Seat | Status |
|----|------|------|--------|
| T1 | `/gsd-new-milestone` — establish Cycle 20 "Personal Tools", phases 112+ | LEAD (Opus 5) | IN PROGRESS |
| T2 | Create Cycle 20 roadmap from 13 requirements, phases start at 112 | `gsd-roadmapper` (sonnet) | DISPATCHED |

## Attempts (append-only)

- Baseline snapshotted: HEAD `6ebfc69a`, branch `feat/personal-tools`, tracked tree clean.
- Prior `.foreman/ledger.md` (Phase 108 plan-review, RUN COMPLETE) archived to
  `.foreman/ledger-108-planreview-2026-07-19.md` — not clobbered.
- T1 dispatched: `/gsd-new-milestone` at LEAD seat.

- User confirmed milestone summary + CalVer `2026.07.10`; research SKIPPED (Cycle 17 research corpus
  reused — 4 researchers would re-derive known ground).
- PROJECT.md updated + STATE.md reset via `gsd-sdk state.milestone-switch` (avoided Bug #2630
  hand-edit trap). Committed `14c94573`.
- Pre-overwrite checks: `.planning/phases/` empty (0 files), Phase-111 VERIFICATION confirmed
  present in `.planning/milestones/cycle19-phases/`, Cycle 19 REQUIREMENTS/ROADMAP confirmed
  archived. Nothing destroyed.
- 13 requirements defined (PORT-01..04, PTOOL-01..04, PSEED-01..05); prefixes verified
  collision-free against the existing corpus. User confirmed all 13 in scope, PORT-03 kept,
  no gaps. Committed `16b31fc6`.
- T2 dispatched: gsd-roadmapper (sonnet), phase numbering continues at 112.
- T2 COMPLETE (gsd-roadmapper r1): 5 phases 112-116, 13/13 requirements mapped. Verified against
  the diff, not the report: ROADMAP.md 109→183 lines, all 19 prior milestone entries intact,
  Cycle 15 detail block intact, carry-forward backlog intact AND correctly extended with the
  Cycle 20 out-of-scope items. No clobber (the memory-flagged planner hazard did not fire).
- User approved WITH revision: merge old 112+113 (both additive-only ports, no gate between them)
  → 4 phases 112-115. LEAD also caught a stale SC ("branch exists off main" — it already exists).
- T2 resumed (not re-spawned) with revision ticket: cheaper than a fresh sonnet spawn and keeps the
  3-file renumber consistent in one context.
- T2 COMPLETE (r2 revision): 4 phases 112-115. LEAD-verified against the files, not the report —
  phase headers, Depends chain, Milestones entry, Execution Order, Traceability (13/13 remapped),
  STATE `total_phases: 4`. Preservation re-checked after the rewrite: 18 milestone entries,
  carry-forward section + all 5 legacy items, 17 completed progress rows, Cycle 15 block. Only the
  3 intended files changed. Committed `db0e7116`.
- VERIFICATION DISCLOSURE: no blind `foreman-verifier` was run. This change is planning documents
  only — no logic content — and every claim was checked by deterministic grep/diff against the
  files by LEAD. Recorded as LEAD-verified, NOT blind-verified.
- Step 10.5 (link pending todos to phases): 0 pending todos, skipped per workflow.
- RUN COMPLETE. HEAD `db0e7116`, branch `feat/personal-tools`, tracked tree clean.
  T1 status: DONE. T2 status: DONE.
