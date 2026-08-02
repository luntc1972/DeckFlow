---
title: Batch B — CSS-location guard, icon-key totality guard, and the four existing layout leaks
date: 2026-08-02
priority: high
source: deferred from quick task 260802-m6s ("Out of scope"), extended with the 2026-08-02 second audit pass
target_milestone: TBD — candidate for the post-Cut-Lab consolidation milestone
status: PENDING
---

# Batch B — CSS location guard + the debt it would immediately catch

Batch A (`260802-m6s`) deferred two guards to Batch B. **A guard alone fails on day one** —
four layout blocks already violate the rule. Fix the leaks and land the guard together, or
the guard cannot be turned on.

⚠ **Re-verify every `file:line` below before planning tasks.**

## Background — why this matters

`wwwroot/css/site.css` is simultaneously the Classic theme **and** the fork source for the
guild themes. Eleven of the 23 guild sheets are standalone forks that do **not**
`@import site.css` (`site-izzet.css` is a single selector; `site-dimir.css` is ten).
Anything living only in `site.css` is therefore missing on roughly half the themes.

An in-repo comment already documents this at `wwwroot/css/site-common.css:1452-1456`:

> "11 of the 22 theme sheets are standalone forks that do NOT `@import` site.css, so
> anything living only in site.css is missing on half the themes."

The rule is written down and unenforced. Hence the guard.

## B1 — Move `.result-panel textarea` to `site-common.css` (HIGHEST IMPACT)

`wwwroot/css/site.css:450-458`:

```
.result-panel textarea { min-height: 16rem; width: 100%; max-width: 100%; box-sizing: border-box }
```

`site-common.css` has **no** base rule for it — only `.field textarea` (`:5644`) and
`.prompt-artifact-textarea--expanded` (`:1446`). On the eleven small themes (azorius, boros,
dimir, golgari, gruul, izzet, orzhov, rakdos, selesnya, simic, temur) every unclassed output
textarea collapses to the UA default of roughly 20 cols × 2 rows.

Affected render sites (15): `DeckSync.cshtml:173,184`; `DeckConvert.cshtml:98`;
`SuggestCategories.cshtml:138`; `Bracket.cshtml:315`; `Manabase.cshtml:1106`;
`CedhMetaGap.cshtml:361,377`; `DeckAnalysis.cshtml:238,505`;
`DeckComparison.cshtml:389,400,411,422,551`.

This is the single highest-impact theme-fork bug in the app — the output box of most tools
is unusable on half the themes.

## B2 — Move `.deckflow-bridge-hint` block

`wwwroot/css/site.css:626-672` — `.deckflow-bridge-hint`, `> summary`, `__body`,
`__download` (`display:flex; flex-wrap; gap`), `__details-link`, `__steps`, `__fallback`.

`Views/Shared/_DeckFlowBridgeHint.cshtml` is included on **10 pages**: Manabase, Bracket,
CutLab, DeckHistory, DeckAnalysis, DeckPrimer, DeckSync (×2), DeckConvert, CedhMetaGap.

## B3 — Move `.moxfield-bulkedit-hint` block

`wwwroot/css/site.css:674-706` — `.moxfield-bulkedit-hint`, `> summary`, `__body`,
`__steps`, `__steps li`. `Views/Shared/_MoxfieldBulkEditHint.cshtml` appears on 8 pages.

## B4 — Move the entire Ask a Judge page layout

`wwwroot/css/site.css:528-624` — `.judge-primary`, `.judge-tips`, `.judge-tips li`,
`.judge-suggested`, `.judge-suggested textarea`, `.judge-divider`,
`.judge-divider::before/::after`, `.judge-divider span`, `.judge-howto`,
`.judge-howto[open] > summary`, `.judge-howto__steps`, `.judge-howto__steps li`,
`.judge-howto__steps kbd`, `.judge-howto__fallback`.

`Views/Deck/JudgeQuestions.cshtml` renders essentially **unstyled on all 23 non-Classic
themes**.

**Bonus defect found in the same sweep:** `wwwroot/css/site-mobile.css:291-297` holds the
**only** `.judge-optional-ref` rules anywhere, and they sit inside
`@media (max-width: 600px)`. So `JudgeQuestions.cshtml:80` has no desktop styling on *any*
theme, Classic included. Move the base rules to `site-common.css`.

## B5 — The CSS-location CI guard (the original Batch B item)

Fail the build when a **layout** property appears in `site.css` for a selector that has no
rule in `site-common.css`.

Property set to police: `display`, `grid-*`, `flex*`, `position`, `width`, `height`,
`min-*`, `max-*`, `padding`, `margin`, `gap`, `overflow`.

Deliberately **not** policed: `color`, `background`, `border-color`, `box-shadow`,
`font-*` — those are legitimately per-theme.

Land B1–B4 first, or the guard fails immediately on existing code.

## B6 — Icon-key totality guard (the other original Batch B item)

Batch A's D1 fixed three landing tiles rendering "?" because `_ToolTileIcon.cshtml` switched
on keys that did not match `ToolRegistry.cs:61` (`IconKey = key`). Two cases had been written
against `HelpSlug`; one key had no case at all.

Add a test asserting **every** registry key has a non-default case in `_ToolTileIcon.cshtml`,
so a newly registered tool cannot ship with a question-mark icon.

## Verification

- `dotnet build` clean, no new warnings (baseline: 0 errors / 9 CS8629)
- Visual check of the output textarea and both hint partials on at least three of the eleven
  standalone themes (izzet, dimir, temur) plus Classic
- Ask a Judge rendered on a standalone theme at desktop **and** ≤600px
- Guard fails on a deliberately-introduced layout rule in `site.css`, passes on a color rule
- **Line endings:** `.gitattributes` pins LF by default; preserve each touched file's
  committed endings exactly
