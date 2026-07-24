---
phase: 111-cut-lab-upgrade-regression-gate
plan: 01
subsystem: testing
tags: [cut-lab, vitest, xunit, playwright, structural-evidence, locking, jsdom]

requires:
  - phase: 110.1-combo-intelligence
    provides: structural-findings evidence chips + card->combo lookup shipped
provides:
  - Regression coverage proving Structural-evidence pills lock/unlock the canonical pool card
  - Coverage proving unmatched Structural evidence renders inert (span, no aria-pressed, click no-op)
  - Three-layer proof: xUnit (server FindLockableEvidenceCard), Vitest (client render+click), e2e (end-to-end)
affects: [111-02, 111-04]

tech-stack:
  added: []
  patterns: [structural-findings JS patch fixture reused from cut-lab-structural-cardtext.test.ts]

key-files:
  created:
    - DeckFlow.Web/ts-tests/cut-lab-structural-evidence-lock.test.ts
  modified:
    - DeckFlow.Web.Tests/CutLabViewModelWordingTests.cs
    - DeckFlow.Web/e2e/cut-lab-pill-interactions.spec.ts

key-decisions:
  - "Deterministic inert proof owned by Vitest + xUnit; e2e inert step is corroborating smoke only (a live decide cannot guarantee an unmatched evidence item)."
  - "Codex authored test code (CLAUDE.md delegation); orchestrator verified EOL + gates."

patterns-established:
  - "Structural-evidence lock regression asserted at all three layers (server/client/e2e)."

requirements-completed: [CLUP-09]

duration: ~10min
completed: 2026-07-24
---

# Phase 111 Plan 01: CLUP-09 Structural Evidence Pill-Lock Regression

**Locked in three-layer regression coverage proving Structural-evidence pills drive the canonical pool checkbox, and unmatched evidence stays inert.**

## Performance

- **Duration:** ~10 min (Codex dispatch)
- **Tasks:** 3 (2 fully gated, 1 e2e authored)
- **Files modified:** 3 (1 new)

## Accomplishments

- **Task 1 (Vitest):** `cut-lab-structural-evidence-lock.test.ts` — matched evidence ("Counterspell") renders as `button[data-cut-lab-chip-card]` with `aria-pressed`; unmatched ("Curve congestion at MV 2") renders as inert `span.kb-chip`; click on matched flips canonical checkbox + aria-pressed (lock/unlock); click on inert span is a no-op.
- **Task 2 (xUnit):** extended `CutLabViewModelWordingTests` — `FindLockableEvidenceCard` returns the pool card for exact-name and mana-value forms; returns null for commander-only, off-pool, and non-card phrases.
- **Task 3 (e2e):** added one test to `cut-lab-pill-interactions.spec.ts` (oversized pool → JS decide → assert lockable structural-evidence button set non-empty → click locks/unlocks canonical card; inert invariant smoke). **Execution deferred** to the consolidated Wave-1 e2e pass (plans 01/02/03 share port 5173; `run-web-test.sh` runs `fuser -k`).

## Gates

- `npm test -- cut-lab-structural-evidence-lock` → **1 file, 2 tests PASS**.
- `dotnet build DeckFlow.Web.Tests` → **0 warnings, 0 errors**.
- `dotnet test --filter CutLabViewModelWordingTests` → **32 tests PASS**.
- e2e: authored, TypeScript parse OK; run pending in consolidated Wave-1 pass.

## Verification

- EOL preserved: `git diff --stat` == `git diff --ignore-all-space --stat` (no churn); edited files LF (CR=0 = HEAD), new file LF.
- Scope: only the 3 declared files touched. No new packages. No production code changed.

## Deviations

- Codex declined to author the SUMMARY under the 3-file scope fence; orchestrator wrote it.
