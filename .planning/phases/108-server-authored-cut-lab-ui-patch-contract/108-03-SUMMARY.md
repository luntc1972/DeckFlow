---
phase: 108
plan: 03
title: TypeScript Patch Rendering
status: complete
completed: 2026-07-23
requirements_addressed: [CLUP-02, CLUP-03]
executor: codex (gpt-5.4 medium)
verifier: claude
---

# Plan 108-03 Summary — TypeScript Patch Rendering

## What was built
Cut Lab browser code now renders server-authored patch fields after live JSON mutations
and no longer re-derives domain state in success paths.

- `interface CutLabUiPatch` (+ tuner row interface) mirroring `CutLabUiPatchDto` camel-cased,
  and `applyServerPatch(patch, antiForgeryToken, options)` — one adapter that writes hidden
  state, sticky bar, proposal, cuts made, structural findings, what-if options, export enabled.
- **Tuner tbody full reconciliation**: inserts a `<tr>` for a now-present basic with no row,
  removes rows for absent basics, updates qty/disabled on the rest, and **rebuilds the
  "Add a basic land" dropdown from `patch.addableBasics`** (show/hide by list emptiness).
  Fixes the pre-existing bug where a freshly-added basic didn't appear until reload
  (old `patchAdjustRow` no-op'd for a not-yet-present basic).
- `handleDecisionSubmit` / `handleAdjustSubmit` / `handleWhatifKeep` render from `data.patch`,
  require `patch.cutLabStateJson` (malformed-response guard, T-108-05), preview path unchanged.

## Key files
- modified: `DeckFlow.Web/wwwroot/ts/cut-lab.ts`
- modified: `DeckFlow.Web/ts-tests/cut-lab-proposal.test.ts`
- modified: `DeckFlow.Web/ts-tests/cut-lab-adjust.test.ts`
- modified: `DeckFlow.Web/ts-tests/cut-lab-whatif.test.ts`

## Verification (Self-Check: PASSED)
- `tsc --noEmit`: clean. Vitest (cut-lab-adjust/proposal/whatif): 19 passed / 0 failed.
- Legacy helpers (`currentCountFromSerializedState`, `cardsRemainingFromSerializedState`,
  `buildCutsMadeFromSerializedState`, `rebuildWhatifSelectOptionsFromState`, `patchAdjustRow`)
  independently confirmed ABSENT from all three live success handlers; retained only for
  scenario/local hidden-state paths.
- No compiled `wwwroot/js/*.js` staged (gitignored, rebuilt at deploy).
- EOL: all files LF (CR=0). Churn is TS reindent only (not EOL — CR=0), ESLint not gated.

## Deviations
- None.

## Commits
- `6a5b941e` feat(cut-lab): add TS server-patch interfaces and render adapter
- `805a8323` feat(cut-lab): render decide/adjust from server patch
- `41d60589` feat(cut-lab): render what-if commit from server patch
