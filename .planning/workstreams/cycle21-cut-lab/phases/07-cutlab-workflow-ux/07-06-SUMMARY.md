# Plan 07-06 Summary

Written retroactively on 2026-08-22, when the plan's work was re-landed. The implementation dates
from 2026-08-04.

## Built

- **Task 1 — the per-cut line.** `cutLabWhatifPreviewSummaryCopy` and the delta summary now read
  `"{n} of 7 deck numbers moved."` instead of `"metric families changed meaningfully"`. That line is
  read on every single cut, and "metric family" is internal vocabulary. The C# xmldoc comments still
  use the internal term to describe `changedFamilyCount`; those are API documentation, not user copy.
- **Task 2 — the restart control.** The button reads *"Re-run rounds 1 & 2 — your accepted cuts are
  kept"* and the confirm dialog now repeats the same promise: *"Re-run rounds 1 & 2? Your accepted
  cuts are kept — only the rejected and deferred cards get re-scored against today's findings."*
  Previously the dialog mentioned only reconsidering rejected and deferred cards, so the control read
  as destructive.
- **Task 3 — one lock explanation.** The parenthetical *"(protected from any future cut)"* was
  repeated four times on one panel; it now appears once, beside the Lock column header.
- **Task 4 — named staleness.** The Goals and Compare-to-baseline current columns are marked as
  point-in-time. **Shipped as markers only** — automatic recompute on accept was deliberately
  deferred; it needs engine timing measurements first, tracked in
  `.planning/todos/pending/2026-08-04-recompute-goals-and-compare-automatically-on-accept.md`.
- **Task 5 — mobile step labels.** Visible step names return at 390px via an opt-in
  `UseMobileLabeledTabs` flag on `WorkflowStepTabsModel`, defaulting to `false`. `_WorkflowStepTabs`
  is shared by five tools; only Cut Lab's short step names fit the labelled pill treatment, so
  `CedhMetaGap`, `DeckPrimer`, `DeckComparison` and `DeckAnalysis` keep bare numerals. Guarded by
  `ui-responsive.spec.ts`.
- **Task 6 — docs.** The help page and README describe the shipped five-slot wizard.

## The commits are not the original ones

The original `deebf5ad` (tasks 1-6) and `175b8f40` (restart dialog + smoke assertion) were
un-committed by a stray `git reset` on 2026-08-04 and the whole working tree was parked in a stash.
Both objects survived. The work was re-landed on 2026-08-22 as **`a5eeb59d`** and **`5e4104e0`**,
split along the same seam, on `fix/cycle21-blockers`.

Two Vitest fixture edits existed only in the stash and in no commit — they removed the stale
`<span>(protected from any future cut)</span>` from `cut-lab-pool-filter.test.ts` and
`cut-lab-lock-interactions.test.ts`. They are now in `5e4104e0`.

**`175b8f40`'s message carried a wrong claim** — that the smoke spec could not exercise the rewritten
lock assertion because it failed earlier at `expandCutLabSection`. It can: `cut-lab-smoke.spec.ts:98`
reaches and passes it. Only `:139` hits `expandCutLabSection`, for an unrelated pre-existing reason.
The corrected statement is in `5e4104e0`'s message.

## Verification

Build 0 errors / 15 warnings (9 pre-existing CS8629 + NU1903 package advisories). Vitest 33 files /
131 tests. Web xUnit 2302 passed / 0 failed / 16 skipped. Zero CR bytes; `git diff` and
`git diff --ignore-all-space` identical, so no EOL churn.

**Not yet done: a human UAT of the copy changes in a browser.** The copy is user-visible and only
automated checks have run. The restart button and dialog pair were checked for consistency in the
*served* bundle at `/js/cut-lab.js`, with the old string absent (count 0) — not merely in source.

## Review

First independent read of this code is the 2026-08-22 gate,
`.planning/reviews/2026-08-22-cycle21-0706-code-gate.md`. The 2026-08-16 gate could not see it: the
work was parked in a stash and invisible to a diff-based review.
