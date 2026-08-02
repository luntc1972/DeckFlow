---
created: 2026-08-02T21:52:20.187Z
title: Fix nine verified UI defects site-wide
area: ui
files:
  - DeckFlow.Web/Views/Shared/_ToolTileIcon.cshtml:3-53
  - DeckFlow.Web/Services/Tools/ToolRegistry.cs:17,25,26,61
  - DeckFlow.Web/Views/Deck/Home.cshtml:20,37
  - DeckFlow.Web/Program.cs:215
  - DeckFlow.Web/Views/Feedback/Index.cshtml:20,50
  - DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml:31-34
  - DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml:11-12
  - DeckFlow.Web/Views/Deck/DeckPrimer.cshtml:76
  - DeckFlow.Web/wwwroot/css/site-mobile.css:122-128,352-354
  - DeckFlow.Web/wwwroot/css/site-common.css:974-977,1345-1352
  - DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml:255,601
---

## Problem

Batch A of the 2026-08-02 site UI audit (full report:
`.planning/ui-reviews/2026-08-02-site-ui-audit.md`). Nine independently verified defects that
need no design decision — roughly 30 lines of change total, closing three HIGH accessibility
defects and two visible landing-page bugs.

Each was confirmed by reading the code path, not inferred:

1. **Three landing-page tiles render a "?" icon.** `ToolRegistry.cs:61` sets `IconKey = key`, and
   `Create(key, …)` takes key first while `helpSlug` is the 11th positional arg. Two icon switch
   cases in `_ToolTileIcon.cshtml` were written against the helpSlug (`"ask-a-judge"`,
   `"category-suggestions"`) instead of the key (`judge-questions`, `suggest-categories`), and
   `deck-history` has no case at all. All three fall to `default:` (`:52`), which emits a valid
   question-mark SVG — so nothing fails loudly. For 12 of 15 tools key and helpSlug are identical
   strings, which is why only these two drifted.

2. **`Home.cshtml` has no `<h1>`.** First heading in the document is the section `<h2>` at `:37`.

3. **404s bypass the branded error page.** `Program.cs:215` wires only
   `UseExceptionHandler("/Deck/Error")`; there is no `UseStatusCodePagesWithReExecute`, so a
   mistyped URL or any 403 renders the bare framework page. The most common error a visitor hits
   is the one that isn't handled.

4. **Feedback validation is entirely inert.** `Feedback/Index.cshtml:20` sets `novalidate` and
   `:50` loads only `feedback.js` — no validation scripts partial. `required`, `minlength="10"`,
   and `type="email"` never fire, so every mistake costs a server round trip and the 10-char
   minimum enforced at `FeedbackSubmission.cs:14` is never communicated before submit.

5. **Workflow step tabs have no accessible name on mobile.** `_WorkflowStepTabs.cshtml:34` puts the
   text in `.prompt-step-tab__label` and marks `.prompt-step-tab__num` `aria-hidden="true"`;
   `site-mobile.css:352-354` sets `.prompt-step-tab__label { display: none }` at <=600px. The
   AT-visible child is hidden from CSS and the visible child is hidden from AT, so every workflow
   tab on 5 pages (DeckAnalysis, DeckComparison, CedhMetaGap, DeckPrimer, CutLab) announces as an
   unnamed button on phones.

6. **Disabled tabs break roving-tabindex.** `_WorkflowStepTabs.cshtml:31-32` emits both
   `aria-disabled` and the real `disabled` attribute. A `disabled` button is unfocusable, so arrow
   traversal dead-ends at the first incomplete step.

7. **`_DeckToolTabs` menu toggle controls itself.** `:11-12` — the
   `<button aria-controls="deck-tool-nav">` is nested inside `<nav id="deck-tool-nav">`. Same line
   carries a `hidden` attribute that nothing ever removes (`site.ts:178-182` only toggles
   `aria-expanded`), overridden by `site-mobile.css:147-149` `display: inline-flex !important` —
   semantically hidden, visually present, on every page with the tool nav.

8. **Primer "Start Over" silently discards the carried deck.** `DeckPrimer.cshtml:76` opens
   `<form method="post"` with no `data-cache-key`, so `attachGenericPersistedForms`
   (`deck-sync.ts:970`) never binds the clear handler at `:984` that resets and navigates. But
   `deck-input-store.ts:208-218` registers a document-level listener for any `[data-clear-cache]`
   click and calls `clearLastDeck()` unconditionally. Net: the deck is wiped from sessionStorage,
   the page does not navigate, the form does not reset, and nothing visibly changes. The same
   missing attribute is also why the primer has no form-state persistence at all.

9. **Sub-44px tap targets and iOS auto-zoom.** `site-mobile.css:122-128` raises `.run-button`,
   `.clear-cache-button`, `select`, `.df-select__trigger` to 44px but omits `.copy-button`; worse,
   `site-common.css:974-977` (inside `@media (max-width: 600px)`) actively shrinks
   `.copy-button.copy-button--icon` to 2rem/32px. Also under 44px: `.prompt-step-tab`
   (`site-mobile.css:336`, 40px), `.share-bar__button` (`site-common.css:4059-4061`, 40px),
   `.ai-selector__option-label` (`site-mobile.css:394-398`, 40px), `.feedback-submit`
   (`site-common.css:1354-1363`, ~38px, no mobile override). Separately,
   `site-common.css:1345-1352` gives feedback inputs `font: inherit` against a `--fs-base` of
   0.95rem (~15.2px), which triggers iOS Safari's auto-zoom-on-focus.

Two more one-character/one-rule fixes belong in the same pass:

- `CedhMetaGap.cshtml:255` uses `class="table-wrapper"`, and **no CSS rule for that class exists in
  any stylesheet**. The 9-column reference table has no scroll container between 601px and desktop.
- `CedhMetaGap.cshtml:601` guards with `@if (item.RefCount >= 0)` — always true for a non-negative
  int — so cuts with zero references print "found in 0 reference deck(s)". Its sibling at `:574`
  correctly uses `> 0`.

## Solution

One branch, one `/gsd-quick` pass, Codex `luna` (localized, tight-spec, no design decisions).

- Icons: add the three missing cases, keyed off `Key` not `HelpSlug`.
- `<h1>DeckFlow — Magic: The Gathering deck tools</h1>` on Home.
- `app.UseStatusCodePagesWithReExecute("/Deck/Error", "?code={0}")`, branch Error copy on 404.
- Drop `novalidate` from the feedback form (browser validation is enough here).
- `aria-label="@step.Label"` on the step-tab button, or make the label `.sr-only` instead of
  `display:none`.
- Drop the real `disabled` attribute from step tabs; keep `aria-disabled` and no-op the handler.
- Wrap the tool-nav groups in `<div id="deck-tool-nav-groups">` and point `aria-controls` at it;
  gate toggle visibility with a class instead of the `hidden` attribute.
- Add `data-cache-key="deck-primer"` to `DeckPrimer.cshtml:76` — this one attribute repairs both
  the dead Start Over button and the missing form persistence.
- Add `.copy-button` to the 44px selector list; delete the `--icon` mobile shrink; raise the four
  other named controls; `font-size: max(16px, 1em)` on feedback inputs.
- `.table-wrapper { overflow-x: auto; }` plus `tabindex="0" role="region"`.
- `RefCount > 0`.

Note the theme-fork constraint: layout CSS goes in `site-common.css`, not `site.css` — 11 of 24
theme sheets do not `@import` (see the CI-guard todo).

Verify tap targets and the mobile tab name in a headless Playwright pass before closing; every
finding above is static analysis, not observed in a browser.
