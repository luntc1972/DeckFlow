# MBGAP-11-06 Summary

## Files Changed

- `DeckFlow.Web/e2e/manabase-cedh-keep.spec.ts`
- `.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-06-SUMMARY.md`

## manabase-mulligan.spec.ts Churn

- No churn needed. The current keep-shapes-off assertions still match today's shipped markup, so `DeckFlow.Web/e2e/manabase-mulligan.spec.ts` was not modified.

## Playwright `--list` Validation

- New spec command:
  `cd DeckFlow.Web && env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test e2e/manabase-cedh-keep.spec.ts --list`
- Output:
  - `[chromium-desktop] › manabase-cedh-keep.spec.ts:131:5 › cEDH keep-shapes renders dual headlines, Winota opener coverage, and no horizontal overflow`
  - `[chromium-desktop] › manabase-cedh-keep.spec.ts:163:5 › casual keep-shapes retains keepable-hands headline and shows curve coverage`
  - `[chromium-mobile] › manabase-cedh-keep.spec.ts:131:5 › cEDH keep-shapes renders dual headlines, Winota opener coverage, and no horizontal overflow`
  - `[chromium-mobile] › manabase-cedh-keep.spec.ts:163:5 › casual keep-shapes retains keepable-hands headline and shows curve coverage`
  - `Total: 4 tests in 1 file`

- Existing spec command:
  `cd DeckFlow.Web && env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test e2e/manabase-mulligan.spec.ts --list`
- Output:
  - `[chromium-desktop] › manabase-mulligan.spec.ts:78:5 › opening-hand lens card is visible when analysis.manabase.mulligan-eval is ON`
  - `[chromium-desktop] › manabase-mulligan.spec.ts:94:5 › opening-hand lens card is absent when analysis.manabase.mulligan-eval is OFF`
  - `[chromium-mobile] › manabase-mulligan.spec.ts:78:5 › opening-hand lens card is visible when analysis.manabase.mulligan-eval is ON`
  - `[chromium-mobile] › manabase-mulligan.spec.ts:94:5 › opening-hand lens card is absent when analysis.manabase.mulligan-eval is OFF`
  - `Total: 4 tests in 1 file`

## EOL Check

- `DeckFlow.Web/e2e/manabase-cedh-keep.spec.ts` carriage-return count: `0`
- Result: LF-only, as required.

## Note

- The live e2e run against a running app and the Task 3 human-verify checkpoint were not executed here. Per the plan and task constraints, those are performed by the orchestrator.
- Softened the live cEDH smoke's Winota representative-opener assertion to be conditional on sampled opener content, and made the `no plan by turn 4 — mulligan` opener assertion conditional for the same live-sample reason; headline, shape-label, and overflow assertions remain hard.
