---
phase: manabase-research-gap-closure
plan: 04
status: complete
completed: 2026-07-12
commits:
  - c655fdd2 feat(manabase): analysis.manabase.restricted-lands flag, seed OFF (gap-04)
  - e9e8917c feat(manabase): restricted-source disclosure table + panel entry (gap-04)
  - 053ca993 test(manabase): restricted-lands disclosure e2e, desktop+mobile (gap-04)
executor: codex gpt-5.4 medium (cross-AI); Claude reviewed + committed
verifier: foreman-verifier PASS (LOW: summary file -> this file; INFO: frontmatter drift)
---

# Plan 04 Summary — MBGAP-01 flag + disclosure UI

## What shipped

- **`analysis.manabase.restricted-lands`** registered (catalog + PG `FALSE` + SQLite `0` seeds — ships OFF, D-04). Fail-safe-off service read; trailing-optional param `ManabaseAnalysisService` → `ManabaseAnalyzer.Analyze` → `Classify(restrictedLands:)`. CLI paths untouched (default false).
- **Report wiring**: `deck.RestrictedSourceLandNames`/`HasRestrictedSourceApproximation` now copied onto `ManabaseReport` (closes plan-03's dead-surface note).
- **Disclosure UI**: gated compact table naming each restricted land + `†` marker + footnote + one `UnsupportedInteraction` entry. Renders only when flag ON and restricted lands present.
- **Parity tests**: OFF == no-cache baseline (6 report fields), ON != OFF (Cavern deck), OFF name-list empty, ON exact 3-name list; carried-in gap-03 regression (Cavern present + param off keeps weight 1.0).
- **e2e**: `manabase-restricted-lands.spec.ts` — admin flag toggle, deck submit, marker/footnote/panel assertions + no-horizontal-scroll. Run 2/2 green (chromium-desktop + chromium-mobile).
- Mobile fix: Approximation cell wraps (`site-common.css`, scoped `.manabase-restricted-sources`) after visual check caught clipped text.

## Sanctioned deviations

1. Write set + `FeatureFlagStoreSeedTests.cs` (plan body 1(d) vs frontmatter omission).
2. D-05 adaptation: no per-land table exists in the view → compact disclosure table instead of in-table land-row markers (intent preserved: names lands, marker precedent, footnote, panel).
3. + `site-common.css` for the mobile wrap fix (layout CSS belongs there per project rule).

## Validation

- Build 0/0; Core FULL 1387/0; Web FULL 1354/14skip/0 (pre-fix run) + filtered 111/0 post-wiring; e2e 2/2; 2-viewport screenshots reviewed (desktop + mobile v2 after wrap fix).
- EOL clean.

## Flag state

`analysis.manabase.restricted-lands` = OFF everywhere. Operator flip requires golden-deck diff + calibration per D-04 (post-phase).
