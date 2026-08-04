# Plan 07-03 Summary

## Built

- Task 1 re-parented every loose Process, Decide, and Goals section under its
  corresponding workflow panel without changing content blocks.
- Tasks 2-3 added `cutLabStep` activation: tabs set `aria-selected`, roving
  `tabindex`, and `.is-active`; activated panels use the native `hidden`
  attribute. Initial server tab state is retained while content remains visible
  until the first activation so the Process-first import workflow remains usable.
- ArrowLeft/ArrowRight/Home/End activate and focus enabled tabs; anchor links
  activate an owning hidden panel before scrolling and focusing their target.
- Reserved Plan slot 3 remains empty and activates as a valid panel. Export
  retains its submit binding and disabled user behavior.
- Task 5 adds focused Vitest coverage for tab activation, disabled tabs,
  keyboard skipping, and no-selection degradation.

## Panel visibility

`hidden` is applied only by script during an activation. It provides computed
`display: none` without CSS and leaves every panel visible without JavaScript.

## Verification

```text
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -v q --nologo
exit 0 — Build succeeded. 0 Warning(s), 0 Error(s).

npm run test
exit 0 — 33 files passed; 127 tests passed.

npm run typecheck:e2e
exit 0

G-1 workflow gate
exit 0 — passed on desktop and mobile.

G-2 workflow gate
exit 0 — passed on desktop and mobile.

G-3 workflow gate
exit 1 — expected `height < limit` at `cut-lab-workflow-ux.spec.ts:111`;
desktop received 10662 (expected red for 07-04/07-05).
```

### Completed by the orchestrator after the executor stopped

The executor stopped before running G-4 and the regression specs. Both were run by
the orchestrator against this same commit; results below. Each gate was run as its
own process with `-g`, and the verdict is the process **exit code** — not the
printed footer, which a suppressed-summary condition had previously rendered
unreliable during 07-02.

```text
G-1 workflow gate   (-g "G-1", chromium-desktop + chromium-mobile)
exit 0 — PASS

G-2 workflow gate   (-g "G-2", chromium-desktop + chromium-mobile)
exit 0 — PASS

G-3 workflow gate   (-g "G-3", chromium-desktop + chromium-mobile)
exit 1 — FAIL at "Decide page height should be below 3000px"
         (expected red; fixed by 07-04/07-05)

G-4 workflow gate   (-g "G-4", chromium-desktop + chromium-mobile)
exit 1 — FAIL at `expect(intakeIsCollapsed).toBe(true)`
         (expected red; fixed by 07-04)

npx playwright test e2e/cut-lab-smoke.spec.ts e2e/cut-lab-structure.spec.ts \
  e2e/cut-lab-export.spec.ts --project=chromium-desktop --workers=1
exit 0 — 19 passed (1.1m). No regression from the tab behavior.

npm run test
exit 0 — 33 files passed; 127 tests passed
         (was 124/32 before this plan; cut-lab-step-tabs.test.ts added 3).
```

**Gate state after 07-03:** G-1 PASS, G-2 PASS, G-3 RED, G-4 RED — the intended
shape. 07-03's own acceptance target was G-2, and it is green.

## Known limitations

- G-3 and G-4 remain red as planned; they are 07-04/07-05's targets.
- The build line above reports `0 Warning(s)` because MSBuild only emits warnings
  for projects it actually recompiles. The true baseline is **0 errors / 9
  pre-existing CS8629 warnings** in
  `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`, visible only
  under `-t:Rebuild`. Unrelated to Phase 7 and untouched by it.
- `e2e/cut-lab-tuning.spec.ts:321` (tuner screenshot matrix) cannot pass locally
  by construction: it is capture-only, asserts nothing, needs ~6 minutes, and runs
  against a 120s budget. `test.skip` hides it on CI, so it fails only on developer
  machines. Pre-existing; not a Phase 7 regression.
- Running the e2e suite rewrites tracked PNGs under
  `.planning/ui-design/cut-lab/screenshots/`. They were reverted rather than
  committed — the UI still changes in 07-04/07-05, so regenerating them now would
  only be superseded. Regenerate once at phase close.
