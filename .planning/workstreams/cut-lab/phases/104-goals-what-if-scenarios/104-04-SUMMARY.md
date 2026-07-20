# 104-04 Summary

## What changed

- Folded the named-scenario store directly into `DeckFlow.Web/wwwroot/ts/cut-lab.ts` and published `saveScenario`, `listScenarios`, `loadScenario`, and `deleteScenario` on `window.DeckFlowCutLab` before the existing namespace assignment.
- Stored each scenario as the exact `CutLabStateJson` payload in browser `localStorage`, backed by a 20-slot index and a `crypto.randomUUID()` fallback for environments that do not expose it.
- Applied the `deck-input-store.ts` silent-degrade pattern so disabled storage and quota failures return typed fallbacks instead of throwing.
- Added `DeckFlow.Web/ts-tests/cut-lab-scenarios.test.ts` coverage for round-trip save/load/delete/list behavior, the 20-slot cap, quota exhaustion, disabled-storage degradation, and the UUID fallback path.
- Added a JS-only Scenarios panel to `DeckFlow.Web/Views/Deck/CutLab.cshtml` with `data-cut-lab-scenario-*` controls plus a `<noscript>` warning. By design, this feature has no server endpoint and no non-JS fallback.
- Wired the panel in `cut-lab.ts` so Save snapshots the current DOM into `CutLabStateJson`, Load writes a saved JSON payload back into that hidden input and `requestSubmit()`s the main form, and Delete refreshes the rendered list in place.
- Added the scenario panel layout rules to `DeckFlow.Web/wwwroot/css/site-common.css` only. `site.css` remained untouched.

## Commits

- `a804cfa1` — `test(104-04): cover cut lab scenario storage`
- `619fc3f0` — `feat(cut-lab): add local scenario storage`

## Verification

- `cd DeckFlow.Web && npx --no-install vitest run ts-tests/cut-lab-scenarios.test.ts` passed with `5` tests green.
- `cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit` exited `0`.
- Grep gates passed for `MAX_SCENARIO_SLOTS`, `requestSubmit`, `data-cut-lab-scenario`, and `<noscript>`.
- Confirmed there is no standalone `cut-lab-scenarios.ts`, no `sessionStorage` usage for scenarios, no `site.css` edits, and no compiled JS staged.

## Notes

- Scenario persistence is intentionally browser-local only for this phase. There is no server fallback by design; the `<noscript>` copy is the sanctioned exception called out in the plan.
