---
created: 2026-08-02T21:52:20.187Z
title: Consolidate four copy-to-clipboard implementations
area: ui
files:
  - DeckFlow.Web/wwwroot/ts/deck-sync.ts:296-346
  - DeckFlow.Web/wwwroot/ts/card-lookup.ts:134-168,262,282
  - DeckFlow.Web/wwwroot/ts/content-kb.ts:136-147
  - DeckFlow.Web/wwwroot/ts/share-bar.ts:20-24
  - DeckFlow.Web/Views/Shared/_Layout.cshtml:157
  - DeckFlow.Web/wwwroot/css/site-common.css:1551-1577,974-977
  - DeckFlow.Web/wwwroot/css/site-mobile.css:122-128
  - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml:862-866
  - DeckFlow.Web/Views/Deck/DeckSync.cshtml:160-163
---

## Problem

Highest-leverage item in the 2026-08-02 site UI audit
(`.planning/ui-reviews/2026-08-02-site-ui-audit.md`), given the project's stated core value:
*"Every supported workflow must produce output the user can paste into ChatGPT and get back a
useful answer in one round-trip."* The control that performs that paste is the least-supported
control on the site.

**Four implementations, one correct.**

| Implementation | Announces to SR | Manual-select fallback |
|---|---|---|
| `deck-sync.ts:296-346` | yes (`[data-copy-announcer]`) | no |
| `card-lookup.ts:134-168` (a fork of the above) | **no** | no |
| `content-kb.ts:136-147` | no | no |
| `share-bar.ts:20-24` | no | no |

`_Layout.cshtml:157` defines a site-wide `[data-copy-announcer]` live region and exactly one module
reads it.

**The icon variant visually breaks on success.** `card-lookup.ts:134-168` sets
`button.textContent = 'Copied'`, which destroys the `aria-hidden` glyph wrapper. On
`.copy-button--icon` that writes a 6-character word into a fixed 2.25rem square styled
`color: var(--on-accent)` over a `transparent` background (`site-common.css:1551-1577`) — the
feedback both overflows and is near-invisible.

**On mobile it is the smallest tap target on the page — deliberately.**
`site-mobile.css:122-128` raises `.run-button`, `.clear-cache-button`, `select`,
`.df-select__trigger` to 44px and omits `.copy-button`. `site-common.css:974-977`, inside
`@media (max-width: 600px)`, then *shrinks* `.copy-button.copy-button--icon` to 2rem (32px). Every
button around it is 44px.

**Failure has no recovery path.** `deck-sync.ts:343` reports "Copy failed" with no next step. No
implementation reveals the underlying `<textarea>` (already in the DOM but `hidden`, e.g.
`ContentKb/Detail.cshtml:50`) so the user can select manually.

**Missing entirely where it matters most.** `DeckAnalysis.cshtml:862-866` renders generated
decklists — the single most paste-worthy artifact on the site — in a bare `<pre>` with no copy
button and no height clamp. `DeckSync.cshtml:160-163` Reconciliation Report is the only result
panel on its page with no copy affordance. `CedhMetaGap.cshtml:409-668` offers Download and Print
but no Copy for the Top 10 Adds/Cuts.

**Accessible names are generic.** Twelve buttons named "Copy" on `DeckComparison.cshtml`
(`:387,398,409,420,450,464,483,497,514,531,549,564`), six on `DeckAnalysis.cshtml`, three on
`DeckPrimer.cshtml`, two on `DeckSync.cshtml`. A screen-reader button list is unusable.

## Solution

One `df-copy.ts` module with a fixed markup contract, replacing all four:

- Behavior attached via `data-copy-target` (already the dominant convention), supporting both
  `<textarea>` and `<pre>` sources — `deck-sync.ts:296-298` already handles non-textarea targets.
- Success: swap the glyph (clipboard → check), never replace `textContent`; announce through the
  existing `[data-copy-announcer]` region.
- Failure: reveal the hidden `<textarea>`, select its contents, and say "Copy failed — the text is
  selected, press Ctrl+C."
- 44px minimum at every viewport, including the icon variant.
- Require a per-artifact accessible name; default to the panel heading rather than "Copy".

Then add the missing call sites (DeckAnalysis decklists with a clamp + expand, DeckSync
Reconciliation Report, CedhMetaGap adds/cuts) and delete the three redundant implementations.

Sequencing: land this **before** the shared-tab split and the page-restructuring work, since both
touch the same result panels and would otherwise re-copy the old markup. Codex `terra` — changes a
markup contract across ~15 call sites.
