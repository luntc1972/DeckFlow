---
title: Batches D, E, F — reconstructed definitions (originals lost)
date: 2026-08-02
priority: medium
source: RECONSTRUCTED from the 2026-08-02 second front-end audit pass; original definitions lost
target_milestone: TBD — candidate for the post-Cut-Lab consolidation milestone
status: PENDING — needs owner confirmation that these match the original intent
---

# Batches D, E, F — reconstructed

⚠ **These are reconstructions, not the originals.** Quick task `260802-m6s` names four
deferred batches in its "Out of scope" section, but both of its source documents are gone
from disk:

- `.planning/ui-reviews/2026-08-02-site-ui-audit.md` — absent; `.planning/ui-reviews/`
  contains only `.gitignore`
- `.planning/todos/pending/2026-08-02-fix-nine-verified-ui-defects-site-wide.md` — absent;
  the `pending/` directory itself had to be recreated

All that survives of D, E and F is a phrase each. What follows is rebuilt from an
independent second audit run on 2026-08-02, matched to those phrases. **Confirm against
intent before executing** — items may be missing, and some may not have been in the
original scope at all.

**Batch C is unaccounted for.** It is never named in the batch-A plan. Either it was
absorbed into A, or its definition is lost entirely.

---

# Batch D — deep-linking

*Original phrase: "Deep-linking (Batch D)."*

Reconstructed intent: results and workflow steps are not addressable, so a user cannot
bookmark, share, or return to a specific step or result section.

Supporting evidence from the second audit:

- **Workflow step is not in the URL.** `Views/Shared/_WorkflowStepTabs.cshtml:12,36` computes
  `aria-selected` as "first incomplete enabled step", independent of
  `Model.Request.WorkflowStep`, while the visible panel is chosen from the *model's* step by
  `wwwroot/ts/deck-sync.ts:1305`. Before JS runs — and permanently without JS — the selected
  tab and the visible panel **disagree**. A URL-carried step would make one source of truth.
- **`_WorkflowStepTabsModel` does not receive the current step** — the fix for the above is
  to pass it in; that same plumbing is what a `?step=` parameter would bind to.
- **Anchor nav exists but is not shareable.** `Views/Deck/Manabase.cshtml:420` and
  `Views/Deck/CutLab.cshtml:303` both render in-page jump navs; neither reflects position in
  the URL.
- **`/judge-questions` already accepts a deep link** (`?card=`) and is the only tool that
  does — it is the precedent to generalize.

Open question for the owner: was Batch D about workflow-step deep-linking, result-section
anchors, or shareable analysis output? The three have different scopes.

---

# Batch E — result-panel segmentation

*Original phrase: "Result-panel segmentation (Batch E)."*

Reconstructed intent: several result surfaces render as one uninterrupted stack with
everything expanded, producing extreme page lengths. Segment them.

Ranked by severity from the second audit:

**E1 — Cut Lab (worst by an order of magnitude)**
- `Views/Deck/CutLab.cshtml:401-467` — the entire 101–150 card pool renders as one flat
  `<table>`: no pagination, no virtualization, no default collapse, no sort. At ≤600px,
  `site-common.css:1074` turns each row into a ~4-line ~150px card, so the pool table alone is
  roughly **20,000px tall on a phone** (~5,500px desktop).
- Fourteen `<details class="cutlab-collapsible" ... open>` sections are forced open on first
  paint: `:323,470,540,608,648,668,758,854,915,1076,1110,1204,1346,1430`. Combined, the page
  exceeds **25,000px mobile / 10,000px desktop**.
- The `data-cutlab-mobile-collapse` hook already exists but the server-side `open` attribute
  defeats it.
- `:1354-1399` — "Tune quantities" renders a second full table over every legal-multiple card,
  no collapse, no filter, immediately after the pool table. It is only relevant at the 99↔101
  endgame; collapse by default.
- ⚠ **Overlaps Cycle 21 Phase 7 (Cut Lab Workflow UX)** — check before scoping, to avoid
  two plans editing the same view.

**E2 — Deck Primer (~6,000–9,000px)**
- `Views/Deck/DeckPrimer.cshtml:122,155,288` plus `:235` — all three step panels and every
  `details.primer-group` render `open` simultaneously; roughly 40 section checkboxes each with
  a nested `<details>` help disclosure.
- Unlike Deck Analysis / Comparison / Meta Gap there is **no panel hiding at all** —
  `wwwroot/ts/primer-selection.ts:255-260` only scroll-jumps.
- Fix: reuse `showPromptStep()` from `deck-sync.ts:1305`.

**E3 — Mana Base (~5,000–7,000px desktop, 9,000px+ mobile)**
- `Views/Deck/Manabase.cshtml:401-1071` — one uninterrupted stack: verdict → three lenses →
  tap lens → mulligan lens → ramp → command zone → color findings table → castability table →
  restricted-source table → unsupported list → two methodology `<details>`. Mitigated only by
  the anchor nav at `:420`.
- Fix: make the post-verdict lenses collapsible rather than always-rendered.

**E4 — Deck Analysis, two unbounded regions**
- `Views/Deck/DeckAnalysis.cshtml:816-832` — one `.nested-panel` per answered question with no
  cap, while the picker (`:293-418`) allows 40+ selections. Step 3 grows linearly forever.
- `:997-1075` — Step 5 renders every set's `TopAdds` expanded, each containing a full
  `<pre class="oracle-text">`. Fifteen adds × ~8 lines ≈ 3,000px in one panel. Put the oracle
  text behind a per-card disclosure.

**E5 — Deck History timeline**
- `Views/Deck/DeckHistory.cshtml:140-180` — every version rendered, no pagination, no
  newest-first cap. `site-common.css:4110` pins it to `min-width: 42rem`, making it the **only
  remaining forced horizontal-scroll table in the public app** at 390px.
- Fix: cap to the last N with "show older", and card-stack the table with `data-label` the way
  `.manabase-table--card` does (`site-common.css:3203-3255`).

**The pattern to copy already exists in-repo:** `CedhMetaGap.cshtml:260-330` (client
pagination, `pageSize=10`), `Commander/CommanderCategories.cshtml:5-6,99-121` (top-25 plus
overflow `<details>`), `Manabase.cshtml:1044-1051` (bounded rows plus "Show all N").

---

# Batch F — tab partial split

*Original phrase: "Tab partial split (Batch F)."*

Reconstructed intent: `_WorkflowStepTabs.cshtml` is used by two incompatible families of
page, and one family is misusing the ARIA tab pattern. Split the partial.

**The two families:**

1. **Real tabs** — Deck Analysis, Deck Comparison, cEDH Meta Gap. `deck-sync.ts:1305`
   (`showPromptStep`) hides non-current panels. The tab contract holds.
2. **Not tabs** — Cut Lab and Deck Primer. Both use the full
   `role="tablist"` / `role="tab"` / `aria-selected` / `role="tabpanel"` contract but **never
   hide the unselected panels**. Cut Lab has no panel-hiding JS at all;
   `primer-selection.ts:264-271` only scroll-jumps and flips `aria-selected`. Assistive tech
   announces "tab 2 of 4 selected" while all four panels sit in the reading order
   simultaneously.

**Specific defects to resolve in the split:**

- `Views/Deck/CutLab.cshtml:23` — `WorkflowStepTab(4, "Export", … SubmitFormId:
  "cut-lab-export-form")` renders a `role="tab"` that is actually `type="submit"` and posts a
  form (`_WorkflowStepTabs.cshtml:31,40`). **A tab that mutates state violates the pattern
  outright.** Separate the tab from the "Build export" submit at `CutLab.cshtml:1068`.
- `Views/Deck/DeckPrimer.cshtml:122,155,288` — `#primer-step-panel-1..3` are the
  `aria-controls` targets of `role="tab"` buttons (`_WorkflowStepTabs.cshtml:35`) but carry
  **neither `role="tabpanel"` nor `aria-labelledby`**.
- `_WorkflowStepTabs.cshtml:12,36` — `aria-selected` computed independently of
  `Model.Request.WorkflowStep` (see Batch D; the two fixes share plumbing).
- `Views/Deck/DeckAnalysis.cshtml:150,200,515,881,961` (and the same in Comparison and Meta
  Gap) — `.prompt-step-panel` sections render with no server-side `hidden`, so with JS off all
  five stack visible, and with JS on there is a full-page FOUC before `showPromptStep` runs.
  Render `hidden` on non-current panels server-side.

**Recommended shape:** keep `_WorkflowStepTabs` as the real-tab partial for family 1 (adding
server-side `hidden` and the model's current step), and give family 2 a plain
`<nav>` + in-page-link partial with no ARIA tab roles at all. Cut Lab and Deck Primer are
step *navigation*, not tabs.

⚠ Batch A's D5 and D6 already edited `_WorkflowStepTabs.cshtml` (mobile `aria-label`, roving
tabindex, `type="button"` for disabled steps, plus a capture-phase guard in `site.ts`).
Rebase this batch onto that work rather than planning against the pre-A file.
