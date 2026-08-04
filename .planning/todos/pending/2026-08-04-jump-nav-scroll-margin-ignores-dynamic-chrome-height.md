---
created: 2026-08-04T20:45:00.000Z
title: Jump-nav lands sections behind sticky chrome on mobile (scroll-margin-top is static, chrome height is dynamic)
area: ui
files:
  - DeckFlow.Web/wwwroot/css/site-common.css:122
  - DeckFlow.Web/wwwroot/ts/cut-lab.ts:487
  - DeckFlow.Web/wwwroot/ts/cut-lab.ts:943-959
---

## Problem

Cut Lab's in-page anchor nav scrolls a section heading to a position that is **behind** the sticky
chrome on mobile. Jumping to *Cut rounds* or *Tune quantities* at 390px buries the heading ~98px
under `nav.cutlab-anchor-nav` + `.cutlab-sticky-bar`.

Measured during the 07-04/07-05 UAT (`UAT-07-04-07-05.md`, Finding 2):

| Project | chrome height | scroll-margin | landed at | hidden behind chrome |
|---|---|---|---|---|
| chromium-mobile 390×844 | 158.5px | 60px | y=60 | **98px** |
| chromium-desktop 1280×900 | 44px | 60px | y=60 | none (16px clear) |

## Root cause

`cut-lab.ts:487` calls `scrollIntoView({ block: 'start' })` and relies on the global
`scroll-margin-top` rule at `site-common.css:122`, which is a **hardcoded `4rem`**. The actual chrome
height is computed at runtime and published as `--cutlab-pinned-offset` (`cut-lab.ts:943-959`,
summing `nav.cutlab-anchor-nav` and `.cutlab-sticky-bar`). The two numbers were never wired
together, so the static value only happens to be right at desktop widths where the chrome does not
wrap.

**This is PRE-EXISTING, not a 07-05 regression.** By `git log -S`, `scroll-margin-top` came from
`f8492dc93` (v1.3); both sticky elements predate the phase. The pinned card is a *consumer* of
`--cutlab-pinned-offset`, not a contributor to it. 07-05 only made the mismatch measurable.

## Likely fix

Have the scroll target's `scroll-margin-top` consume the custom property that already exists rather
than adding a second measurement:

```css
scroll-margin-top: var(--cutlab-pinned-offset, 4rem);
```

Two things to check before doing that:

1. **`site-common.css:122` is a GLOBAL rule** — it is not Cut Lab-scoped. Census every page that
   relies on it before changing the shared declaration; a Cut Lab-scoped override is the safer
   shape if other tools depend on the `4rem`.
2. `--cutlab-pinned-offset` is only set on pages that run `cut-lab.ts`. The fallback must stay
   correct everywhere else.

## Acceptance

- Jump-nav lands every Cut Lab section heading fully clear of sticky chrome at 390×844 and
  1280×900, proven by measurement (heading `top` >= chrome `bottom`), not by eye.
- The fix reads the runtime chrome height; it does not hardcode a second magic number.
- Any page outside Cut Lab that consumes `site-common.css:122` is enumerated and verified unchanged.
- Mutation-proven: forcing the chrome taller must still land the heading clear.

## Context

- Source: `.planning/workstreams/cycle21-cut-lab/phases/07-cutlab-workflow-ux/UAT-07-04-07-05.md`,
  Finding 2. HEAD at time of measurement `0267ed0a`, code at `06b377d6`.
