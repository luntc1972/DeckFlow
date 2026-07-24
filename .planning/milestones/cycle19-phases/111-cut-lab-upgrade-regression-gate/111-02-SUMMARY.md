---
phase: 111-cut-lab-upgrade-regression-gate
plan: 02
subsystem: testing
tags: [cut-lab, coverage-audit, vitest, gates, ci, documentation]

requires:
  - phase: 108-110.1
    provides: Cut Lab upgrade surfaces (filters/search, collapse, anchors, oracle disclosures, combo labels, packages)
provides:
  - CLUP-10 coverage matrix mapping all six changed surfaces to named passing tests
  - Canonical runnable full-gate command sequence (111-GATES.md) with MED-1 sequencing + WSL constraints
  - Dedicated combo-label + package-helper-copy Vitest artifact (closed the one real gap)
affects: [111-04]

tech-stack:
  added: []
  patterns: [belt-and-suspenders combo/package smoke as CLUP-10's dedicated artifact]

key-files:
  created:
    - .planning/phases/111-cut-lab-upgrade-regression-gate/111-COVERAGE-MATRIX.md
    - .planning/phases/111-cut-lab-upgrade-regression-gate/111-GATES.md
    - DeckFlow.Web/ts-tests/cut-lab-combo-package-copy.test.ts

key-decisions:
  - "Five surfaces already COVERED by shipped tests (cited in matrix); only package-helper-copy lacked a direct DOM assertion — closed by the new smoke."
  - "Gate doc pins the Windows dotnet.exe path, no-MTG_DATA_DIR, no-Windows-browser, and the shared-:5173 sequential-e2e requirement."

patterns-established:
  - "Coverage matrix is verify-by-opening (not trust-the-list); each surface cites file:test."

requirements-completed: [CLUP-10]

duration: ~12min
completed: 2026-07-24
---

# Phase 111 Plan 02: CLUP-10 Coverage Audit + Canonical Gates

**Every changed Cut Lab surface now maps to a named passing test, and the full regression-gate command sequence is a reproducible artifact.**

## Performance

- **Duration:** ~12 min (Codex dispatch)
- **Tasks:** 3
- **Files created:** 3

## Accomplishments

- **Task 1 — Coverage matrix:** `111-COVERAGE-MATRIX.md` audits all six CLUP-10 surfaces (pool filters/search, collapse state, anchors, oracle disclosures, combo labels, package helper copy) by opening each cited test file. Verdict: all six COVERED.
- **Task 2 — Gap fill:** the only genuine gap was a direct DOM assertion of the **package helper copy**; closed by `cut-lab-combo-package-copy.test.ts`, which also re-asserts the combo badge text ("Combo piece") as CLUP-10's dedicated combo/package artifact.
- **Task 3 — Gates doc:** `111-GATES.md` documents the copy-pasteable run-order sequence (tsc → build → xUnit CutLab → vitest cut-lab → focused e2e) with the MED-1 shared-:5173 sequential-e2e note and WSL constraints (Windows dotnet.exe path, no MTG_DATA_DIR, no Windows browser, no gstack, no new packages).

## Gates

- `tsc -p tsconfig.json --noEmit` → **PASS, 0 diagnostics**.
- `npm test -- cut-lab-combo-package-copy` → **1 file, 1 test PASS**.
- `npm test -- cut-lab` → **13 files, 55 tests PASS**.
- xUnit CutLab filter + e2e: documented in 111-GATES.md; xUnit via CI fallback, e2e run in consolidated Wave-1 pass.

## Verification

- All three new files LF (CR=0). No existing file modified → no EOL churn. No new packages. No production code changed.

## Deviations

- None. (Codex authored artifacts; orchestrator wrote SUMMARY + committed per delegation split.)
