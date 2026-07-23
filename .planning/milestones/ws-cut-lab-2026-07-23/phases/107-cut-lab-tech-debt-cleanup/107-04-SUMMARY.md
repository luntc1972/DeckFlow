# 107-04 Summary

## Scope executed

- Completed Task 1, Task 2, and Task 3 from `107-04-PLAN.md`.
- Scope remained decide-only as planned: `/api/cut-lab/decide` now returns structural findings payloads for the already-computed after-state, and the JS decide flow live-patches the Structural findings section in place.
- `/api/cut-lab/adjust` remains intentionally unchanged. The adjust-path findings gap is still accepted and documented here as out of scope for this plan.

## What changed

- Added `CutLabDecideFindingDto` and `CutLabDecideFindingGroupDto` to the decide response model, plus `StructuralFindings`, `ComboDataAvailable`, and `CategoryDataAvailable` on `CutLabDecideApiResponse`.
- Reused `CutLabViewModel.BuildFindingGroups` as the single grouping source for the WeakFloorCase merge and shared the finding-to-view formatting path so the API and Razor render the same grouped output.
- Marked up the Structural findings section additively with:
  - `data-cut-lab-structural-findings`
  - `data-cut-lab-structural-findings-body`
  - `data-cut-lab-findings-count-slot`
  - `data-cut-lab-degradation="combo"` / `"category"`
- Added `renderStructuralFindings(response)` in `DeckFlow.Web/wwwroot/ts/cut-lab.ts`, rebuilding only the body node via typed DOM APIs and wiring one call into the decide success path after `renderCutsMade`.
- Added controller/unit coverage for the grouped decide payload, a Vitest renderer test for 0→N/N→0 badge behavior plus note toggling, and a Playwright e2e that proves the findings section updates after a JS decide without a reload.

## Verification run for Tasks 1-3

- `dotnet build DeckFlow.sln` passed.
- `dotnet test DeckFlow.sln --filter "FullyQualifiedName~CutLabApiControllerTests"` passed.
- `cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit` passed.
- `cd DeckFlow.Web && npx --no-install vitest run` passed.
- Optional e2e attempt run locally:
  - `scripts/run-web-test.sh`
  - `cd DeckFlow.Web && npx --no-install playwright test e2e/cut-lab-structure.spec.ts`
  - Result: passed (`22 passed`).
- Headless test server was stopped after the Playwright run.

## Task 4 status — end-of-phase gate (orchestrator, 2026-07-22)

Codex executed Tasks 1-3; the orchestrator ran the authoritative Task 4 phase gate. **PASS:**

- **Full dotnet:** `dotnet test DeckFlow.sln` — DeckFlow.Studio.Tests 426/0 (4 skipped), DeckFlow.Core.Tests 1612/0, DeckFlow.Web.Tests 1950/0 (16 skipped). 0 failed. (9 pre-existing CS8629 warnings in the untouched `ManabaseBaselineWeightingTests.cs` — not from Phase 107.)
- **TypeScript:** `tsc -p tsconfig.json --noEmit` clean; `vitest run` 73/73 (18 files, incl. the new findings-renderer test).
- **Playwright e2e (headless, WSL):** all cut-lab specs pass. The new `cut-lab-structure.spec.ts:139` ("live-patches the structural findings section after a JS decide without a reload") passes. One flake in `cut-lab-export.spec.ts:123` under 40-spec parallel load passed cleanly on isolated re-run (4/4, 7.1s) — the known cut-lab-export cold-start decide-timing flake, not a regression.
- **Hygiene:** `git diff --check` clean; no compiled `wwwroot/js/*.js` staged; all touched `.cs/.ts/.cshtml/.css` files remain LF (0 CRLF introduced); the real-vs-`--ignore-all-space` diff gap (26 lines) is legitimate re-indentation from wrapping the findings loop in the new `data-cut-lab-structural-findings-body` div, not EOL churn.

**All six ROADMAP items landed:** 1 (dead DI fields, 107-01), 2 (commander-inclusive chip, 107-03), 3 (dark-theme delta AA tokens, 107-02), 4 (xmldoc + Manabase-copy closed-already-fixed; Nyx badge closed-with-screenshot; mobile label; button pill — 107-02), 5 (pluralizer + dual path-base + cacheKey note, 107-03), 6 (structural live-patch, 107-04). Accepted documented gaps: adjust-path chip staleness (107-03) and adjust-path findings gap (107-04, decide-only scope).
