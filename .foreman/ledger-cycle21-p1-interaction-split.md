# Foreman Ledger — Cycle 21 Phase 1: Interaction Taxonomy Split

BASELINE: 77bdbd4e | clean except 4 untracked (RESEARCH-FINDINGS.md/.json, role-floor-research-run.exit, _role-floor-research/) — all pre-existing, out of scope | 2026-07-26

MODE: Codex-boosted (Agent tool + real shell + consented Codex CLI 0.145.0)
LEAD SEAT: Opus 5 (1M) — FRONTIER, confirmed
WORKTREE: /mnt/c/users/chrislunt/source/personal/deckflow-role-floors
PLAN: .planning/workstreams/cycle21-cut-lab/phases/01-interaction-taxonomy-split/01-01-PLAN.md

## Plan

| # | Task | Class |
|---|------|-------|
| 1 | Codex plan review → convergence (gate before code) | FRONTIER (Codex top tier) |
| 2 | Execute the split: 6 production sites + tests + e2e | WORKHORSE (Codex mid tier) |
| 3 | Blind verification, fresh context, no executor reasoning | FRONTIER (Claude foreman-verifier) |
| 4 | EOL + changed-lines format gate | LEAD (Claude, deterministic) |

## Routing

| Task | Seat | Effort | Why |
|------|------|--------|-----|
| 1 | Codex `gpt-5.4` read-only | medium | Project rule: Codex is authoritative plan reviewer. Cross-family second read on a plan Claude authored. |
| 2 | Codex `gpt-5.4` danger-full-access | medium | Project rule: Codex codes, Claude reviews. Plan is fully specified — WORKHORSE work, not judgment. |
| 3 | Claude `foreman-verifier` | — | Cross-family verification: Claude grades Codex output. Blind by construction. |
| 4 | Claude LEAD inline | — | Deterministic shell checks; no judgment, no delegation warranted. |

Note: `-codex` model variants 400 on this ChatGPT-account login (established 2026-06-09). Plain `gpt-5.4` for all seats.

## Tasks

| id | state | owned paths | job id |
|----|-------|-------------|--------|
| T1 | **DONE — CONVERGED** | (read-only) | 3 dispatches |
| T2 | PENDING | plan frontmatter files_modified (13 files) | — |
| T3 | PENDING | (read-only) | — |
| T4 | PENDING | (normalization only) | — |

## Attempts

| task | # | seat + effort | ticket rev | outcome | checks | evidence | timestamp |
|------|---|---------------|-----------|---------|--------|----------|-----------|
| T1 | 1 | Codex gpt-5.4 read-only, medium | r1 | NEEDS-CHANGES — 1 BLOCKER, 1 HIGH, 2 MEDIUM | consumer grep, ClampFloors read, floor arithmetic | .foreman/scratch/c21p1-planreview.out.txt | 2026-07-26 |
| T1 | 2 | Codex gpt-5.4 read-only, medium | r2 (4 findings folded) | NEEDS-CHANGES — BLOCKER not fully resolved (category-path wipe leak), 1 NEW MEDIUM (unreachable test), 1 NEW LOW | re-ran discovery greps, DeckStatClassifier read | .foreman/scratch/c21p1-planreview-r2.out.txt | 2026-07-26 |
| T1 | 3 | Codex gpt-5.4 read-only, medium | r3 (all folded) | **CONVERGED** — no BLOCKER, no HIGH; 2 MEDIUM + 1 LOW non-blocking, all fixed post-verdict | all 3 discovery greps run by reviewer | .foreman/scratch/c21p1-planreview-r3.out.txt | 2026-07-26 |

## Decisions

- Codex consent: pre-granted this session — user selected Codex model defaults explicitly and CLAUDE.md mandates Codex-codes routing. No separate consent prompt required.
- Sequential dispatch throughout. T1→T2 is a hard gate (no code before plan converges); T2→T3 is a hard gate (nothing verified before it exists). No parallelism available or wanted.
- DO NOT COMMIT (user instruction). Changes left staged for user review.
- Pre-existing untracked artifacts (fabricated RESEARCH-FINDINGS, bulk card JSON) are OUT of every write set. They belong to Phase 2 and must survive this phase untouched.

## Scratch

- Ticket + output paths: `.foreman/scratch/`
