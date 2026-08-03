# Phase 7 (Cut Lab Workflow UX) — Claim-vs-Code Plan Review

**Round 1 — 2026-08-03. Reviewer: Codex `gpt-5.6-sol`, effort medium, `-s read-only`, rooted at
`gsd/cycle21-cut-lab` (HEAD `21ad9c53`, rebased onto `main` `ea3dca2a` the same day).
Verdict: CHANGES REQUIRED. 10 HIGH · 3 MEDIUM · 2 LOW. Not folded.**

## Why this round exists

**No review of any kind had ever been run against these six plans** — no cross-AI review and no
same-family checker pass was recorded for the phase. This is the first reader other than the author.

Almost every finding below is a **census failure**: a count, a selector list, or an "already works"
claim that is wrong about the repository. None of them is a reasoning error that reading the plan
more carefully would catch — each one requires grepping the code. That is the entire value of a
claim-vs-code round.

## HIGH

**H-1 — `07-01-PLAN.md:53` — the four-panel contract leaves no slot for Phase 8.** The gate requires
exactly four panels, but the Cycle 21 roadmap requires Phase 7 to reserve a wizard-step slot for
Phase 8. `08-07` will stop because no reserved slot exists, and adding one later also breaks G-1.
*Fix:* define and test a five-slot step contract now, including the reserved element, id and index
that Phase 8 consumes.

**H-2 — `07-04-PLAN.md:42` — decision D-1 is resolved too late.** Option 2 requires skipping the
panel-hiding that `07-03` implements, and contradicts G-2's "exactly one panel visible" assertion.
Executing `07-03` before choosing Option 2 guarantees rework and mutually incompatible acceptance
gates. *Fix:* resolve D-1 before `07-01`, then make `07-01/03/04/05` and their gates consistently
describe the chosen model.

**H-3 — `README.md:54` — the Phase 4 dependency is prose-only.** Phase 7's hard Phase 4 gate exists
only in narrative text while `07-01` declares `depends_on: []`. HEAD has 04-04 Tasks 1-2 committed,
but Task 3's authoritative human density/layout checkpoint is explicitly NOT STARTED, so an executor
can start Phase 7 against an unapproved and still-changing Cut Lab view. *Fix:* add an executable
phase gate to `07-01`; do not baseline E-1..E-4 until 04-04 Task 3 is approved.

**H-4 — `07-03-PLAN.md:83` — activation never toggles `.is-active`.** The plan updates
`aria-selected` and `tabindex`, but all active-tab styling is keyed to `.prompt-step-tab.is-active`,
not `[aria-selected=true]`. Tabs would function with no active visual treatment whatsoever. *Fix:*
toggle `.is-active` during both initial and interactive activation, and assert it in Vitest and
Playwright.

**H-5 — `07-04-PLAN.md:108` — the collapse mapping covers only two of four panels.** Primary
sections are named for Process and Decide only, so activating Goals or Export leaves their primary
`<details>` closed — contradicting "only the active step's primary section is open". *Fix:* map all
four panels (`lock-pool`, `cut-rounds`, `goals`, `export`) and update defaults on activation change
unless persisted state exists.

**H-6 — `07-04-PLAN.md:98` — selector census incomplete; executing this breaks the suite.** The plan
folds away the pool sticky bar and lock-count location but preserves only the Decide-strip
selectors. Production code and tests also consume `data-cut-lab-lock-count`,
`data-cut-lab-pool-sticky-count` and `data-cut-lab-pool-sticky-breakdown`. Executing as written
breaks `CutLabViewRenderTests`, lock-interaction Vitest and smoke coverage. *Fix:* preserve those
selectors on the unified component, or explicitly migrate the TypeScript and every affected test.

**H-7 — `07-04-PLAN.md:126` — the claimed contrast gates cannot fail.** `cut-lab-contrast.test.ts`
tests contrast math only, and `cut-lab-theme-readability.spec.ts` has no intake-summary or
progress-strip locators. Both suites pass while the new UI is unreadable in dark themes. *Fix:*
extend the readability spec with component-specific computed-contrast checks at desktop and 390px.

**H-8 — `07-05-PLAN.md:7` — file inventory omits files Task 2 necessarily changes.**
`CutLabViewModel.cs`, `CutLabViewModelWordingTests.cs` and `cut-lab.ts` are all absent. The existing
AJAX renderer reconstructs the old flat proposal and places actions after all evidence on every
decision, so the pinned split and glance line disappear or flatten after the first AJAX update.
*Fix:* add those files and client tests, and make `renderProposalCard` reproduce the
pinned-header/scrolling-body contract with server/client copy parity.

**H-9 — `07-06-PLAN.md:50` — the copy change covers Razor only.** `cut-lab.ts:2483` independently
renders "N of 7 metric families changed meaningfully" after every AJAX decision, so the old internal
vocabulary survives on the **highest-frequency path** and violates the plan's own zero-occurrence
criterion. *Fix:* include `cut-lab.ts` and its proposal tests; update both the initial and patched
renderers.

**H-10 — `07-06-PLAN.md:100` — the mobile a11y fallback already works, so the gate cannot fail.**
`_WorkflowStepTabs.cshtml` already renders `aria-label="@step.Label"`, and existing e2e coverage
already asserts every shared tab has a name. The plan therefore passes without fixing the measured
**sighted-user** defect of bare numerals. *Fix:* require visible labels or another visible step-name
treatment at 390px, add `site-mobile.css` to `files_modified`, and verify all five consumers.

## MEDIUM

**M-1 — `07-05-PLAN.md:90`** — G-5 checks only that the Accept button's top coordinate is inside the
viewport, so a partially clipped button, or one overlapped by the 4rem sticky anchor nav, still
passes. *Fix:* assert the complete rectangle (`y >= stickyOffset` and `y + height <= innerHeight`)
plus hit-testability.

**M-2 — `07-04-PLAN.md:126`** — layout verification uses 1280px while this repository's desktop UX
target is ~1440px, and `07-05`'s manual wrapping check names only 390px. Desktop sticky, density and
wrapping behavior at the target width is unverified. *Fix:* add a ~1440x900/1000 pass alongside
390x844.

**M-3 — `07-06-PLAN.md:7`** — `CutLabCutRoundEngine.cs` is listed for modification but no task
changes an engine constant; banner bodies are switch-expression literals, not constants. This
creates unexplained service-layer scope in a copy/Razor plan. *Fix:* remove the file and the
misleading environment instruction, or explicitly name and test the intended engine-copy edits.

## LOW

**L-1 — `07-02-PLAN.md:126`** — claims 18 pre-existing `cut-lab-*.test.ts` files; HEAD has 17. The
eighteenth is created later by `07-03`. *Fix:* correct the pre-`07-03` census to 17.

**L-2 — `07-CONTEXT.md:67`** — the stated 17-row fixture composition is wrong.
`cut-lab-smoke.spec.ts` contains one commander, three stacked basic rows and thirteen other rows —
not five stacked basics plus twelve spells. *Fix:* correct the evidence description so future
fixture-equivalence checks compare the actual input.

## Status

**Not folded.** H-1 and H-3 are cross-phase and should be folded together with the Phase 8 findings
that face them from the other side. H-2 (decision D-1) gates the shape of H-5 and parts of H-6, so
resolve it first — folding the others before D-1 risks a second round on the same lines.
