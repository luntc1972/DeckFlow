---
created: 2026-08-02T21:52:20.187Z
title: Split workflow step tabs into tablist and jump-nav
area: ui
files:
  - DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml:15,24,30-34
  - DeckFlow.Web/Views/Deck/DeckPrimer.cshtml:119,121,154,287
  - DeckFlow.Web/wwwroot/ts/primer-selection.ts:252-256,279-303
  - DeckFlow.Web/wwwroot/ts/deck-sync.ts:1318-1323,1739-1744,1888-1893
  - DeckFlow.Web/wwwroot/css/site-mobile.css:331-341,352-354
---

## Problem

Batch F of the 2026-08-02 site UI audit (`.planning/ui-reviews/2026-08-02-site-ui-audit.md`).

`_WorkflowStepTabs.cshtml` is used for **two incompatible interaction patterns**, and its keyboard
contract is implemented four separate times in TypeScript — correctly once.

**Two patterns, one partial.** On DeckAnalysis, DeckComparison, CedhMetaGap and CutLab it is a true
tablist: one panel visible at a time. On DeckPrimer all three panels are visible simultaneously and
the "tabs" are jump-links — `primer-selection.ts:252-256` says so in a comment. That is not the
tablist pattern at all, so DeckPrimer's missing `role="tabpanel"` (`:121,154,287`) is not really a
markup omission to patch; it is the wrong component.

**Four keyboard implementations.** The partial ships roving `tabindex` (`:30`) but no arrow-key
handler. Only `primer-selection.ts:279-303` implements Left/Right/Home/End. The four pages driven
by `deck-sync.ts` (`:1318-1323`, `:1739-1744`, `:1888-1893`) set `aria-selected` and `tabindex` on
**click only**, so a keyboard user can reach exactly one tab and cannot move between steps.

**`role="tab"` buttons submit forms.** `:24` renders
`type="@(step.SubmitFormId is null ? "button" : "submit")"` with `form="@step.SubmitFormId"` at
`:33`. Activating a "tab" submits a form and navigates. ARIA promises the user a panel reveal and
they get a page load.

**Collateral, same root cause:**
- `:31-32` emits both `aria-disabled` and the real `disabled` attribute; a `disabled` button is
  unfocusable, so roving traversal dead-ends. (Quick-fixable — in the Batch A todo.)
- `:34` + `site-mobile.css:352-354` — no accessible name on mobile. (Also Batch A.)
- `:10-12` — the server picks `currentStep` as the first incomplete enabled step, which is not
  guaranteed to match the panel `deck-sync.ts` actually un-hides on load, leaving
  `aria-selected="true"` on a `hidden` panel.
- `site-mobile.css:336` — 2.5rem (40px) tabs in the same file that enforces 44px at `:126`.

Batch A patches the symptoms that are one-liners. This todo is the structural fix; do it after A,
and expect A's `aria-label` and `disabled` changes to move into the new partial.

## Solution

Split into two partials:

**`_WorkflowStepTabs.cshtml`** — a real tablist for the four one-panel-at-a-time pages. It owns its
own keyboard handler (one shared script keyed off `[role="tab"]` inside `.prompt-step-nav`,
replacing the four TS copies), emits `type="button"` only, and never carries `form=`. Move the
step-submitting buttons out of the tablist into the panels' own footers. Have the partial either
emit the panel contract (`role="tabpanel"` + `aria-labelledby`) or assert loudly when a caller
omits it — DeckPrimer's bug was invisible precisely because the partial trusted its callers.

**`_WorkflowStepJumpNav.cshtml`** — `<nav aria-label="Primer steps">` with
`<a href="#primer-step-panel-N">` anchors, for DeckPrimer. No tab roles, no roving tabindex, no
`aria-selected`. Keep `scroll-margin-top` on the panel headings so anchor jumps do not tuck under
the sticky bar.

Single source of truth for `currentStep`: pass one value into the model and have the client derive
panel visibility from the same value rather than recomputing it.

Codex `terra` — shared partial, 5 callers, changes a markup contract.
