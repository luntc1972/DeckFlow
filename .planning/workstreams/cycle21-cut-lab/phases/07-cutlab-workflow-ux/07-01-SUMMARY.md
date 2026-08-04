# Plan 07-01 Summary

## Built

- Added `DeckFlow.Web/e2e/cut-lab-workflow-ux.spec.ts` with four independent gates:
  five-slot DOM order, native-dispatch tab activation, page-height headroom, and
  collapsed intake/commander summary.
- Reused the 17-row fixture and the `setToolEnabled` / admin-lock setup from
  `cut-lab-smoke.spec.ts`.
- Preserved `test.describe.configure({ mode: 'serial' })`.

## Pre-change gate failures

The required two-project command was run on unmodified application behavior. Because
Playwright serial mode stops subsequent tests after the first failure, that command
reported G-1 failing on both projects and six tests did not run. Each remaining gate
was then run independently with the same two projects. The actual failure output was:

### G-1

```text
Error: expect(received).resolves.toEqual(expected) // deep equality

- Expected  - 2
+ Received  + 1

  Array [
    "cut-lab-step-panel-1",
-   "cut-lab-step-panel-2",
    "cut-lab-step-panel-3",
    "cut-lab-step-panel-4",
-   "cut-lab-step-panel-5",
+   "cut-lab-step-panel-2",
  ]

2 failed
  [chromium-desktop] › G-1 preserves the five-slot wizard contract in document order
  [chromium-mobile] › G-1 preserves the five-slot wizard contract in document order
6 did not run
```

### G-2

```text
Error: expect(locator).toHaveAttribute(expected) failed

Locator:  getByRole('tab').filter({ hasNot: locator('[disabled]') }).first()
Expected: "true"
Received: "false"

2 failed
  [chromium-desktop] › G-2 activates exactly one panel through native tab dispatch
  [chromium-mobile] › G-2 activates exactly one panel through native tab dispatch
```

### G-3

```text
Error: Decide page height should be below 3000px

Expected: < 3000
Received:   10735

Error: Decide page height should be below 4000px

Expected: < 4000
Received:   16924

2 failed
  [chromium-desktop] › G-3 keeps Decide page bulk below the desktop and mobile headroom thresholds
  [chromium-mobile] › G-3 keeps Decide page bulk below the desktop and mobile headroom thresholds
```

### G-4

```text
Error: expect(received).toBe(expected) // Object.is equality

Expected: true
Received: false

2 failed
  [chromium-desktop] › G-4 collapses intake after import and exposes the commander summary
  [chromium-mobile] › G-4 collapses intake after import and exposes the commander summary
```

## CI pickup verification

Verified rather than assumed. `DeckFlow.Web/playwright.config.ts` sets
`testDir: './e2e'`; `chromium-desktop` and `chromium-mobile` have no `testMatch`
restriction. `.github/workflows/ci.yml` runs the existing `npm run e2e` job. No
workflow file was changed.

## Deviations

- Root `AGENTS.md` was requested but is absent in this checkout.
- Playwright serial mode prevents all four failing tests from appearing in one full
  run after the first failure; G-2 through G-4 were independently baselined and all
  failed on both projects.
- `npm run typecheck:e2e` could not run to completion because the environment lacks
  the existing Node type definitions: `error TS2688: Cannot find type definition file for 'node'.`

## Known limitations

- The full baseline command reports skipped tests after the first serial failure;
  the independent gate runs are the evidence for G-2 through G-4.
- This plan adds regression gates only; production behavior remains unchanged, so
  the gates are expected to remain red until later Phase 7 plans land.
