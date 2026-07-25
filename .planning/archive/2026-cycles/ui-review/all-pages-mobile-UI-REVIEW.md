# UI-REVIEW — All public pages (mobile)

**Date:** 2026-06-13
**Method:** Live audit at 390×844 (iPhone @2x), headless Chromium, 6-pillar scoring (1–4/pillar, /24 total). 16 public pages. Evidence: `/tmp/ui-audit/p-*.png` (full set), key shots copied here.
**Trigger:** Feedback that mobile UI is not intuitive (started on Deck Analysis, extended to all pages).

---

## Scoreboard (worst → best)

| Page | Score | Type |
|------|-------|------|
| deck-primer | 12/24 | staged workflow |
| **deck-analysis** | 13/24 | staged workflow |
| sync | 13/24 | staged workflow |
| deck-comparison | 13/24 | staged workflow |
| cedh-meta-gap | 13/24 | staged workflow |
| home | 14/24 | hub |
| judge-questions | 14/24 | form |
| content-kb | 16/24 | browse list |
| convert | 17/24 | form |
| mechanic-lookup | 17/24 | form |
| suggest-categories | 17/24 | form |
| feedback | 17/24 | form |
| card-lookup | 18/24 | form |
| commander-categories | 18/24 | reference |
| help | 19/24 | docs |
| about | 20/24 | docs |

**The pattern:** the 5 lowest pages (12–13) are the **staged workflow** pages, and they score low for the *same shared reasons*. The high scorers (18–20) are simple single-action or content pages that skip the workflow chrome. **Fixing the shared chrome lifts all 5 worst pages at once** — that's where the leverage is.

---

## Systemic findings (fix once in the shared layer → lifts many pages)

### S1. Theme picker eats the top of EVERY page (all 16)
`.theme-picker` is `flex: 1 1 100%` on mobile (`site-mobile.css:49-52`), so the FIRST interactive element on every page is the theme switcher — irrelevant to every task. On form/staged pages it pushes the real first action below the fold.
**Fix:** on mobile, collapse the theme picker to a small icon/menu in the header (or move to footer). `Views/Shared/_Layout.cshtml` + `site-mobile.css`.

### S2. Mobile is locked into the most verbose mode (5 staged pages)
The Full/Compact/Advanced layout picker is `desktop-only` (`DeckAnalysis.cshtml:73` and the sibling staged views). Phone users always get the wall-of-text "guided" mode.
**Fix:** expose the picker on mobile + default mobile to compact. Single biggest lever for "too much to follow."

### S3. Two competing filled-blue primaries (5 staged pages)
"Download …session (.zip)" renders at the top of the form using `run-button` — same primary style as "Next/Generate" (`run-button`, `site-common.css:163`). The optional save action is the loudest button on screen.
**Fix:** demote every "Download (.zip)" to secondary/outline and move it below the active step; reserve one filled primary = the next action.

### S4. Two near-identical pill rows (5 staged pages)
Page nav (`_DeckToolTabs`: Analyze/Build/Reference/Categories) sits directly above the Step 1–N progress strip (`_WorkflowStepTabs`). On mobile they read as one confusing 7-item menu. (Proven: pages with `_DeckToolTabs` but NO step strip — suggest-categories, commander-categories — scan noticeably better.)
**Fix:** render progress as a numbered connected stepper (visually distinct), and on mobile collapse the Analyze/Build/Reference/Categories nav into a single "Tools ▾" menu.

### S5. Intro prose + duplicate info/caveat boxes before the task (most pages)
Staged + several form pages stack 1–3 explanatory/caveat boxes (often repeating the same point) above the first input. suggest-categories repeats the caching note 3×; commander-categories, mechanic-lookup, content-kb, sync all front-load callouts.
**Fix:** collapse intro to one line; dedupe info boxes; make caveats a `<details>`; hoist the first input/form toward the fold.

### S6. site-mobile.css is too thin (102 lines, desktop-first) (all)
No mobile rules for textareas (render ~8 rows tall → huge scroll), list rows (dense, untappable), or card padding (large empty bands).
**Fix:** add mobile rules: cap textarea height, list/option rows ≥44px tap height, tighter card padding.

---

## Per-page deltas

(Shared issues abbreviated as S1–S6; see above.)

### deck-primer — 12/24 — Copy 3·Vis 2·Col 2·Type 2·Sp 2·Exp 2
- Worst scroll of all: full category taxonomy rendered as one long flat list of single-line rows on a 390px screen → group into collapsible sections / multi-select (`DeckPrimer.cshtml`).
- Category rows have small line-height, no tap padding — read as text not controls; ≥44px rows (S6).
- Shares S1, S2, S3 (Download .zip), S4.

### deck-analysis — 13/24 — Copy 2·Vis 2·Col 2·Type 3·Sp 2·Exp 2
- See dedicated report `deck-analysis-mobile-UI-REVIEW.md`. Shares S1–S6. Extra: "tabs below" microcopy wrong (tabs render above on mobile); paste-loop mental model only in prose.

### sync — 13/24 — Copy 3·Vis 2·Col 2·Type 3·Sp 2·Exp 2
- Fold is all chrome + a yellow caveat; the Source/Target paste boxes are 2+ screens down → inputs first, caveat collapsible.
- Two tall empty textareas stacked inflate scroll massively (S6: cap height).
- Shares S1, S4.

### deck-comparison — 13/24 — Copy 3·Vis 2·Col 2·Type 3·Sp 2·Exp 2
- Fold entirely chrome (theme + 2 pill rows + 2 intro paras + yellow box + ZIP card) → Step 1 + first field below fold. Hoist form, trim intro.
- "Want your Moxfield tags…" extension nag repeats one already shown above — dedupe.
- Shares S1, S2, S3, S4.

### cedh-meta-gap — 13/24 — Copy 3·Vis 2·Col 2·Type 3·Sp 2·Exp 2
- Same chrome stack pushes "Step 1: Load reference lists" + input below fold.
- Four EDH Top 16 filter selects stack full-width with big gaps → 2-col grid / tighter spacing (`CedhMetaGap.cshtml`).
- Shares S1, S2, S3, S4.

### home — 14/24 — Copy 3·Vis 2·Col 2·Type 3·Sp 2·Exp 2
- Flat stack of ~11 identical white link-cards, zero weighting → no clear single first action. Give the "Analyze Your Deck" hero card a primary CTA treatment + one accent color.
- Taxonomy shown twice: 4 nav pills, then the same 4 words as section headers down the page.
- ~3.5-screen scroll of equal-priority cards → 2-up grid on category sections. Shares S1.

### judge-questions — 14/24 — Copy 4·Vis 3·Col 2·Type 3·Sp 2·Exp 2
- Two filled-blue primaries: "Open Judge Chat" + "Generate Prompt" both `run-button`, but copy says judge chat is the authoritative path → make Generate Prompt secondary.
- Large optional "Reference card" textarea pushes submit below fold → collapse behind "+ add reference card".
- Light-blue warning callout reads as decoration on near-white page → amber/border treatment. Shares S1. (No step strip — good.)

### content-kb — 16/24 — Copy 4·Vis 3·Col 3·Type 4·Sp 2·Exp 2
- Four full-width filter selects + search push the first KB entry ~1.7 screens down → collapse filters behind a "Filters" sheet so results sit near the fold.
- Tag pills wrap to 3 rows per entry (heavy noise) → cap visible tags (3 + "…") on mobile.
- Only the title is tappable → make whole card a tap target, ≥44px. Shares S1.

### convert — 17/24 — Copy 3·Vis 3·Col 3·Type 3·Sp 3·Exp 2
- "Convert" primary sits ABOVE the textarea it needs → move button below input.
- Bulk-edit link + Commander autocomplete hidden at the very bottom → hoist near input. Shares S1, S6 (tall textarea).

### mechanic-lookup — 17/24 — Copy 3·Vis 3·Col 3·Type 3·Sp 3·Exp 2
- Large blue info box between nav and input pushes the field below fold → collapsible / below field.
- Heavy card padding inflates a 1-field form (S6). Info box same hue as the action button → differentiate. Shares S1.

### suggest-categories — 17/24 — Copy 3·Vis 3·Col 3·Type 3·Sp 2·Exp 3
- Clear single primary ("Suggest") near fold — good (no step strip). 
- Three boxes repeat the caching message → collapse to one line. "Lookup mode" select needs inline helper. Shares S1.

### feedback — 17/24 — Copy 4·Vis 3·Col 3·Type 4·Sp 3·Exp 3
- Clean single-primary form. Form vertically centered with big empty top margin → start higher.
- Footer shows a "Feedback" button while already on /feedback → suppress/mark current. Shares S1 (theme picker pure distraction on a form).

### card-lookup — 18/24 — Copy 3·Vis 3·Col 3·Type 3·Sp 3·Exp 3
- Best form: one field, clear primary, all above fold.
- Description mentions desktop-only paste-list/.txt/.json features invisible on mobile → trim desktop clause on small screens. Inline `.txt`/`.json` code styling breaks sentence flow. Shares S1.

### commander-categories — 18/24 — Copy 3·Vis 3·Col 3·Type 3·Sp 3·Exp 3
- Clean reference page, obvious primary, no step strip.
- Two partly-duplicate info boxes → fold the typeahead hint into the input as placeholder/helper. Typeahead has no visible affordance → add cue. Shares S1.

### help — 19/24 — Copy 4·Vis 3·Col 3·Type 4·Sp 3·Exp 4
- Cleanest interaction; correctly omits workflow chrome.
- Topic links are tight text rows → ≥44px padded rows; add dividers/gap for scannability.

### about — 20/24 — Copy 4·Vis 3·Col 3·Type 4·Sp 3·Exp 3
- Fine as a credits page.
- GitHub URL wraps mid-token → use a labeled "View on GitHub" link. External link list rows tight → add padding.

---

## Recommended fix plan (leverage-ordered)

**Phase 1 — shared layer (lifts the 5 worst pages + touches all 16), no backend:**
1. S1 theme picker → mobile icon/menu (`_Layout.cshtml`, `site-mobile.css`).
2. S2 expose layout picker on mobile + default compact (staged views).
3. S3 demote all "Download (.zip)" to secondary, move below active step.
4. S4 progress → numbered stepper; nav → "Tools ▾" menu on mobile.
5. S6 mobile CSS: cap textarea height, ≥44px tap rows, tighter card padding.

**Phase 2 — per-page polish:** S5 dedupe/collapse info boxes + hoist forms; deck-primer & content-kb collapsible lists + capped tags; home hero CTA + 2-up grid; judge-questions primary hierarchy; small fixes (convert button order, feedback top margin, about GitHub link).

Per project rules: implement via Codex + Claude review, re-screenshot at 390×844 + 360×800 to verify (CSS needs visual re-check, not grep). Phase 1 alone should move the staged pages from ~13 to high-teens.
