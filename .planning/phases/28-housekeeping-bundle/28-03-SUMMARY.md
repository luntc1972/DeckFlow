---
phase: 28-housekeeping-bundle
plan: "03"
status: complete
requirements-completed: []
requirements-affected: [HSK-02]
decision: redemote
provides: "Codex isolation discovery (28-DISCOVERY.md) + ratified D-03 re-demote decision for HSK-02"
---

# Plan 28-03 Summary

## What was done

**Task 1 — Investigation (commit `a7f2766`):** Investigated the codex CLI 0.136.0 isolation surface per D-01/D-02. Three candidate read-isolation mechanisms were evaluated against the documented/structural evidentiary bar:

1. `--sandbox read-only` — REJECTED: binary-embedded mode description explicitly states "Codex can read files in the current workspace" (blocks writes, not reads).
2. `deny_read` permission globs — REJECTED: requires `codex-linux-sandbox` helper + bubblewrap (neither installed); no documented global read-disable; binary documents that unelevated Windows cannot enforce deny-read at all.
3. No-tools mode — DOES NOT EXIST: no flag in help text; binary always equips `shell_command`, `list_files`, `search`, `apply_patch`.

All three sandbox modes permit filesystem reads. Full evidence (verbatim help text + binary strings) recorded in `28-DISCOVERY.md`, along with the deferred command-spec shape, D-07 model-config convention, D-04 sentinel-exfil test design + live-probe protocol, and the parity checklist for any future re-promotion.

**Task 2 — Decision gate (blocking-human, D-01/D-03):** Presented to the user 2026-06-04. User ratified **re-demote**: HSK-02 returns to backlog; 28-04 implementation does not proceed; nothing ships behind a warning.

## Re-demote application

- ROADMAP backlog "Codex Distill Backend" entry annotated with the 2026-06-04 investigation findings and re-investigation trigger.
- ROADMAP Phase 28 SC #1 amended (codex end-to-end criterion struck; replaced with the decision-gate-ran-with-evidence criterion).
- ROADMAP 28-04 plan entry marked SKIPPED (D-03).
- REQUIREMENTS.md HSK-02 marked re-demoted with pointer to `28-DISCOVERY.md`.

## Deviations

- Task 1 executed in an isolated worktree by a Claude executor (investigation is a Claude-role task); merged via `c431538`.
- Re-demote tracking edits applied by the orchestrator (shared-artifact writes are orchestrator-owned in worktree mode).

## Commits

- `a7f2766` `docs(28-03): record codex isolation discovery decision`
- `c431538` `chore: merge executor worktree (worktree-agent-a8cce4370edf24f42)`
- (this commit) `docs(28-03): apply ratified D-03 re-demote — tracking updates + summaries`

## Self-Check

PASSED — 28-DISCOVERY.md exists with evidence-backed Decision/command-spec/model-config/probe sections; checkpoint presented and resolved by the user before any 28-04 code.
