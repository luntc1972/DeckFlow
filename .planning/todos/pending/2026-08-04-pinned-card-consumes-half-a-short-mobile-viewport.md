---
created: 2026-08-04T20:46:00.000Z
title: Cut Lab pinned card + sticky chrome occupy 52% of a 664px-tall viewport (advisory)
area: ui
files:
  - DeckFlow.Web/Views/Deck/CutLab.cshtml
  - DeckFlow.Web/wwwroot/css/site-mobile.css
  - DeckFlow.Web/wwwroot/ts/cut-lab.ts:943-959
---

## Problem

On a short mobile viewport — **390×664**, the iPhone 13 / SE class — the Cut Lab sticky chrome plus
the pinned Decide proposal occupy y=0…343 of a 664px viewport, i.e. **52% of the screen**. Anything
the page centres with `scrollIntoView({ block: 'center' })` lands at y≈332 and is therefore under
the pinned card:

```json
{ "targetTop": 310, "pinnedBottom": 343, "pinnedStuck": true,
  "atScrollLimit": false, "hit": "DIV.cutlab-proposal--pinned" }
```

## Why this is advisory, not a defect

Three qualifications, each measured during the 07-04/07-05 UAT:

1. **Not a WebKit bug.** Chromium at 390×664 reproduces it identically (`hit:
   DIV.cutlab-proposal--pinned`); Chromium at 390×844 does not (`hit: SUMMARY.`). The variable is
   viewport **height**, not engine.
2. **Not a scroll-clamp artifact.** `atScrollLimit: false` — the page had room to scroll further.
3. **Content stays reachable.** Nudging the scroll by the overlap clears it and the click lands on
   the target (`reachable: true`). Nothing is permanently unreachable.

This is inherent to any sticky element of this size and did not block 07-05.

## Decision required before any work

This is a **design tradeoff, not a bug** — no CSS change fixes it while keeping the pinned card at
its current mobile height. Pick one, then implement:

- **Accept as-is.** 664px is the short tail of the mobile range and content is reachable by
  scrolling. Close this ticket.
- **Shrink the pinned card on short viewports** — e.g. collapse the glance line / drop the evidence
  row below some `@media (max-height: ...)` threshold.
- **Un-stick below a height threshold** — let the card scroll normally when it would claim more
  than ~⅓ of the viewport.

## Acceptance (if implemented)

- At 390×664 the sticky chrome + pinned card claim no more than a stated fraction of the viewport,
  and that fraction is written down as the rule the CSS enforces.
- `scrollIntoView({ block: 'center' })` targets are not under the pinned card at 390×664.
- 390×844 and desktop are unchanged — verified, not assumed.

## Context

- Source: `.planning/workstreams/cycle21-cut-lab/phases/07-cutlab-workflow-ux/UAT-07-04-07-05.md`,
  Finding 1. HEAD at time of measurement `0267ed0a`, code at `06b377d6`.
- Related but distinct: the jump-nav `scroll-margin-top` mismatch is a genuine pre-existing defect,
  ticketed separately as `2026-08-04-jump-nav-scroll-margin-ignores-dynamic-chrome-height.md`.
