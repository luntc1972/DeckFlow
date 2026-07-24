---
phase: 111-cut-lab-upgrade-regression-gate
plan: 03
subsystem: testing
tags: [cut-lab, wcag, contrast, themes, focus-visible, playwright, accessibility]

requires:
  - phase: 108-110.1
    provides: Cut Lab Lock-your-pool named elements across 24 guild themes
provides:
  - Reusable WCAG contrast helper (e2e/support/contrast.ts) with pure math + locator resolver
  - Vitest math gate (cut-lab-contrast.test.ts) proving the contrast math
  - All-theme Cut Lab readability + focus-visible AA regression spec over the named element set
affects: [111-04]

tech-stack:
  added: []
  patterns: [type-only Playwright imports so contrast math is importable under Vitest]

key-files:
  created:
    - DeckFlow.Web/e2e/support/contrast.ts
    - DeckFlow.Web/ts-tests/cut-lab-contrast.test.ts
    - DeckFlow.Web/e2e/cut-lab-theme-readability.spec.ts

key-decisions:
  - "Pure math (parseCssColor/relativeLuminance/contrastRatio) uses type-only @playwright/test imports so it is Vitest-gatable outside Playwright (H3)."
  - "Deterministic 'Fast mana' package (Sol Ring + Arcane Signet) created per theme so package named elements are asserted with NO no-package fallback (H1)."
  - "Focus-visible ring contrast (>=3.0 vs own bg) asserted for interactive elements — durable guard for untuned themes gruul/izzet/orzhov/temur."

patterns-established:
  - "Single looped contrast spec guards all theme forks against unreadable Cut Lab elements."

requirements-completed: [CLUP-19]

duration: ~14min
completed: 2026-07-24
---

# Phase 111 Plan 03: CLUP-19 All-Theme Readability + Focus Contrast

**A single looped WCAG spec guards every guild-theme fork so no Cut Lab chip, pill, panel, input, or focus ring can silently ship unreadable.**

## Performance

- **Duration:** ~14 min (Codex dispatch)
- **Tasks:** 2
- **Files created:** 3

## Accomplishments

- **Task 1 — Contrast helper:** `e2e/support/contrast.ts` exports pure math (`parseCssColor`, `relativeLuminance`, `contrastRatio`, WCAG 2.x, ratio in [1,21]) plus an async `resolveContrast`/`effectiveBackgroundColor` locator resolver that composites transparent ancestors to what the user actually sees. Type-only `@playwright/test` imports keep the math Vitest-importable. `cut-lab-contrast.test.ts` gates the math (black/white ≈ 21, x/x === 1, rgb/rgba parse).
- **Task 2 — All-theme spec:** `cut-lab-theme-readability.spec.ts` loops all 24 supported theme cookie values (serial + admin-lock), imports the oversized pool, opens the Lands role group, drives a decide, and **creates a deterministic "Fast mana" package per theme** so package named elements are always present. Asserts visibility + contrast (≥3.0 large/bold UI text, ≥4.5 body text) for role pills, chips, sticky status, structural-findings panel, selects, inputs, primary buttons, and the package helper/panel/toggle/member chip — plus focus-visible ring contrast for interactive elements.

## Gates

- `npm test -- cut-lab-contrast` → **1 file, 3 tests PASS** (pure-math gate).
- e2e `cut-lab-theme-readability.spec.ts`: authored; run in consolidated Wave-1 e2e pass (shared :5173). Per H3, `tsc -p tsconfig.json --noEmit` does NOT type-check e2e/ — Playwright esbuild compiles the spec at run time.

## Verification

- All three new files LF (CR=0). No existing file modified → no EOL churn. No new packages (contrast math hand-written). No production code changed.

## Deviations

- None yet. If the all-theme e2e surfaces a genuine unreadable element, it is a real CSS defect → record as deviation and hand the fix to Codex (per plan).
