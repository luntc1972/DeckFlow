---
created: 2026-08-02T21:52:20.187Z
title: Segment Manabase and Deck Analysis result panels
area: ui
files:
  - DeckFlow.Web/Views/Deck/Manabase.cshtml:282-293,295-353,401-1071,1044-1051
  - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml:200-513,554-873,255-285
  - DeckFlow.Web/Views/Deck/DeckComparison.cshtml:378-425
  - DeckFlow.Web/Views/Deck/DeckPrimer.cshtml:225-279
  - DeckFlow.Web/wwwroot/css/site-common.css:2295-2311,5311-5320
  - DeckFlow.Web/wwwroot/css/site-mobile.css:117-120
---

## Problem

Batch E of the 2026-08-02 site UI audit (`.planning/ui-reviews/2026-08-02-site-ui-audit.md`).
The two largest tools render results as one unbroken scroll.

**Manabase** (`:401-1071`) — a single `result-panel` containing verdict, three lenses, tap-analysis
lens, mulligan lens, ramp, commander castability, color-findings table, restricted-source table, a
castability table of up to ~90 rows, and two methodology `<details>`. 600+ lines of markup, no
segmentation. Supporting problems:
- Anchor nav order does not match DOM order (`:282-293` vs `:521-633`) — the 4th nav item scrolls
  **up** — and the nav omits two rendered sections entirely (`#manabase-untapped-sources` at `:641`,
  `#manabase-opening-hand` at `:746`).
- The "Details" anchor (`:292`) targets a `<details>` closed by default, so it scrolls to a
  collapsed summary.
- "Show all N rows" (`:1044-1051`) renders a **second complete table** with its own `<thead>` and
  independently computed column widths, so expanding produces a visibly misaligned duplicate grid.
- The castability table is neither sortable nor filterable (`:295-353`), so "which cards cast
  worst?" requires reading every row.
- No numeric column uses the existing `.tabular` class (`site-common.css:111`) even though
  comparing numbers is the entire point of the tool. CedhMetaGap does this correctly at
  `:265,299,302`.
- Every cell is `white-space: nowrap` inside an `overflow-x` wrapper with no sticky first column
  (`site-common.css:2305-2311`), so the card name scrolls out of view.

**DeckAnalysis** — Step 2 (`:200-513`) runs instructions, helper disclosure, follow-up prompt,
bracket callout, context fields, candidate toggle, seven question buckets with every question
inline, and a freeform box before reaching the primary action. Step 3 (`:554-873`) stacks ten
result blocks with no sub-nav, no collapse and no truncation. Interaction-audit buckets print every
confident card as an uncapped `<ul>` — five buckets of a 100-card deck is a screen each.

Also: "Advanced" layout mode hides `details.result-panel.nested-panel` wholesale
(`site-common.css:5311-5320`), which removes **functional inputs** — Format, Deck name, Strategy
notes, Meta notes, the condensed set-packet override — not just guidance. The expert mode makes the
tool less capable.

**DeckPrimer** (`:225-279`) renders every section group `open` on desktop — ~40 checkboxes, each
with its own nested "What this adds" `<details>` — and only collapses them at <=600px via
`site.ts:221-239`.

**DeckComparison** Step 2 (`:378-425`) inlines four preset textareas (~140 lines of prompt text);
combined with `site-mobile.css:117-120` (`textarea[readonly]` -> `min-height: 50vh`) that is roughly
ten half-viewport scroll-boxes stacked on a phone.

Note on that 50vh rule: it is a **deliberate fix** (commit `98f525d23`, measured on
`/mechanic-lookup` — an 88px box onto 2,903px of rules text), so it must not be reverted. It needs a
`--longform` opt-in so short outputs like `#merged-categories-output` stop rendering as a
half-viewport of whitespace.

## Solution

Segment rather than shorten — the content is wanted, the single scroll is not.

- **Manabase:** split the result panel into Verdict / Lenses / Card-by-card / Methodology, reusing
  the tab CSS, defaulting to Verdict. Keep the anchor nav as the no-JS fallback but fix its order
  and completeness, and open the target `<details>` before scrolling.
- **DeckAnalysis:** Step 2 shows bracket plus a compact question summary ("12 questions selected —
  Edit") with the buckets behind a drawer; Step 3 gets a sticky sub-nav (Score / Interaction /
  Win Cons / Overview / Q&A / Versions) with everything but Overview collapsed and card lists
  truncated to 6 + "Show all (N)".
- Scope the expert-mode hide to `.prompt-instructions`, `.prompt-context-note`,
  `.prompt-helper-panel` only — never to inputs.
- One castability table with `hidden` toggled on overflow rows, not two tables.
- Extract the sort/pagination logic already in `deck-sync.ts:1993-2055` into a reusable
  `sortable-table.ts` and wire the castability and color-findings tables to it.
- Add `.tabular` to numeric columns and a sticky first column.
- Add `--longform` to the readonly-textarea rule so the 50vh floor applies only where it was
  measured to be needed.

Depends on the `df-copy.ts` consolidation landing first — this work rewrites the same result panels
and would otherwise duplicate the old copy-button markup. Codex `terra`.
