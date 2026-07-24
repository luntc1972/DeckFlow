---
phase: 110
plan: 02
title: Section Collapse with localStorage Persistence (CLUP-13)
status: complete
completed: 2026-07-24
requirements_addressed: [CLUP-13]
executor: codex (gpt-5.4 medium)
verifier: claude
---

# Plan 110-02 Summary — Collapsible Cut Lab Sections

## What was built
Every primary Cut Lab section is now a native `<details class="cutlab-collapsible">`
with a stable id, and collapse state is remembered per page across reloads.

- **Stable ids** on the D-03/D-22 section list: `cut-lab-section-lock-pool`,
  `-structural`, `-role-floors`, `-cut-rounds`, `-tune`, `-cuts-made`, plus the three
  pre-existing collapsibles (`-packages`, `-scenarios`, `-whatif`) that had none (MEDIUM-2).
- **Dangling aria-controls fixed**: added `cut-lab-step-panel-1/2/3` ids (panel-4 already
  had its id) so the step tablist's `aria-controls` all resolve.
- **New `<details>` wrappers** on the not-yet-collapsible sections, reusing the existing
  `.cutlab-collapsible` / `data-cutlab-mobile-collapse` pattern (D-22) — no new collapse CSS.
- **MEDIUM-1 markup ownership**: only the section LABEL moved into `<summary>`; the existing
  `.panel-heading` block stays as a body child inside the opened `<details>` (verified for
  Lock your pool at CutLab.cshtml:238-239), so pool-status text and the 110-04 filter slot survive.
- **Persistence** in cut-lab.ts under the single page-scoped literal `deckflow.cutlab.sections`
  (JSON array of collapsed ids, D-21). Reuses `getLocalStorage()` + `isQuotaExceededError()`;
  parse/quota failures fail open to D-23 defaults. Writes on every toggle. Covers all
  `details[data-cutlab-mobile-collapse]` that carry an id, including the three legacy ones.

## D-23 mobile default (behavior change + test update)
`collapseMobileCollapsiblesOnLoad` was narrowed to collapse only the three auxiliary sections
(`packages`/`scenarios`/`whatif`) on mobile — the new primary sections stay open on mobile.
This intentionally supersedes the old "collapse ALL mobile-collapse details" behavior. The
pre-existing `cut-lab-mobile-collapse.test.ts` asserted the old behavior and was NOT in this
plan's scope fence; as reviewer, Claude updated it to the new contract and added a case
locking "a primary section stays open on mobile" (D-23).

## Tests
- New `cut-lab-section-collapse.test.ts` (jsdom): restore-from-storage, persist-on-toggle,
  parse-failure fallback, quota-error fallback.
- Updated `cut-lab-mobile-collapse.test.ts`: auxiliary section collapses on mobile, primary
  section stays open on mobile, auxiliary stays open on desktop.

## Verification (claude)
- `dotnet build DeckFlow.Web` — clean (Razor compiles), 0/0.
- `npx tsc -p tsconfig.json --noEmit` — clean (strict).
- `npx vitest run` (full suite) — 86/86 pass across 20 files.
- EOL: all files LF, no CRLF churn. cshtml `--stat` vs `--ignore-all-space` gap is
  nesting-indentation from the new `<details>` wrappers, not EOL churn.
- Grep gates: summary=9, step-panels 1-4 present, section ids=9, zero id-less
  `data-cutlab-mobile-collapse`, storage key=1. All pass.

## Files changed
- DeckFlow.Web/Views/Deck/CutLab.cshtml
- DeckFlow.Web/wwwroot/ts/cut-lab.ts
- DeckFlow.Web/ts-tests/cut-lab-section-collapse.test.ts (new)
- DeckFlow.Web/ts-tests/cut-lab-mobile-collapse.test.ts (updated by reviewer — behavior change)

## Note for downstream plans
- 110-03 jump nav links to these ids and must auto-expand a collapsed `<details>` on jump (D-24).
- 110-04 injects its pool filter/search into the surviving `.panel-heading` inside
  `#cut-lab-section-lock-pool`.
- site-common.css was NOT modified — nested `<details>` needed no extra spacing.
