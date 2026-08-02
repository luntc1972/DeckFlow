---
created: 2026-08-02T21:52:20.187Z
title: Guard CSS location and icon-key totality in CI
area: tooling
files:
  - DeckFlow.Web/wwwroot/css/site.css
  - DeckFlow.Web/wwwroot/css/site-common.css:1444-1448
  - DeckFlow.Web/Views/Shared/_ToolTileIcon.cshtml:3-53
  - DeckFlow.Web/Services/Tools/ToolRegistry.cs:61
  - .github/workflows/
---

## Problem

Batch B of the 2026-08-02 site UI audit (`.planning/ui-reviews/2026-08-02-site-ui-audit.md`).

Two rules already exist in this project and neither has anything enforcing it, so both have
already drifted in production code.

**Rule 1 — "layout CSS goes in `site-common.css`, not `site.css`."** Stated in CLAUDE.md, restated
as a comment at `site-common.css:1444-1448`. Enforced by nothing.

**11 of 24 theme stylesheets contain zero `@import`** — abzan, bant, esper, grixis, jeskai, jund,
mardu, naya, nyx, planeswalker-dark, sultai. They are full forks that inlined `site.css` at fork
time and diverged; anything added to `site.css` afterward was never backported. A scan of all 617
class tokens used across `Views/` found **17 classes that resolve only in `site.css`**:

| Component | Classes | Pages affected |
|---|---|---|
| Judge page chrome | `judge-divider` `judge-howto` `judge-howto__fallback` `judge-howto__steps` `judge-primary` `judge-secondary` `judge-suggested` `judge-tips` | JudgeQuestions |
| DeckFlow Bridge hint | `deckflow-bridge-hint` + 5 BEM children | Bracket, CedhMetaGap, DeckComparison, DeckConvert, DeckHistory, DeckPrimer, DeckAnalysis, CutLab, Manabase, DeckSync |
| Moxfield bulk-edit hint | `moxfield-bulkedit-hint` + 2 children | same 10 pages |

So on ~46% of themes the Moxfield and Bridge hint blocks render as naked `<details>` on nearly
every deck tool.

**Rule 2 — the tool icon switch must be total over the registry.**
`_ToolTileIcon.cshtml` switches on `ToolDefinition.IconKey`, which `ToolRegistry.cs:61` sets to
`key`. Two cases were written against `helpSlug` instead and one tool has no case, so three live
tiles render the `default:` question-mark SVG. It cannot fail loudly because `default:` returns
valid markup. (The three cases themselves are fixed in the Batch A todo — this todo is about
making it non-recurring.)

Both are the same failure shape: a convention that is true today, has no test, and degrades
silently rather than breaking a build.

## Solution

**CSS-location guard.** The detector is roughly 8 lines of shell and runs in well under a second:

1. Extract every `class="…"` token from `Views/**/*.cshtml`.
2. For each token, check whether it matches `\.<token>[^a-zA-Z0-9_-]` in `site.css` but in **none**
   of `site-common.css`, `site-mobile.css`, or a representative standalone theme (e.g.
   `site-abzan.css`).
3. Fail with the list.

Add it as a CI step alongside the existing `format-gate` (which is already the authoritative
enforcer for the changed-lines format rule, so the pattern and placement are established).

Relocate the 17 offending classes to `site-common.css` first, or the guard fails on introduction.

**Icon totality guard.** A unit test asserting the switch is total over
`ToolRegistry.All.Select(t => t.IconKey)`. Better still, replace the string switch with a lookup
table the test can enumerate directly, so adding a tool without an icon fails at test time rather
than shipping a "?".

Worth considering while in there: `Create()` takes `key` and `helpSlug` as positional strings
eleven parameters apart, and they are identical for 12 of 15 tools. Named arguments or a record
initializer at the call sites would have made the two divergent rows obvious on review.
