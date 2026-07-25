# UI-REVIEW — Deck Analysis (mobile)

**Date:** 2026-06-13
**Trigger:** User feedback — mobile UI not intuitive, hard to follow the directions in Deck Analysis.
**Method:** Live audit of https://www.deckflow.gg/deck-analysis at 390×844 (iPhone) and 360×800 (Android), headless Chromium @2x. 6-pillar visual audit grounded in screenshots + source.
**Evidence:** `iphone-390-fold.png`, `iphone-390-full.png`, `android-360-full.png` (this dir).
**Source:** `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml`, `Views/Shared/_WorkflowStepTabs.cshtml`, `_DeckToolTabs`, `wwwroot/css/site-mobile.css` (102 lines), `site-common.css`.

---

## Score: 13 / 24

| Pillar | Score | One-line |
|--------|-------|----------|
| Copywriting | 2/4 | Directions tell-not-show; "tabs below" is wrong (they render above on mobile). |
| Visuals | 2/4 | Two competing filled-primary buttons; two near-identical pill rows. |
| Color | 2/4 | Primary blue overused (download + next + active step) dilutes "what do I tap". |
| Typography | 3/4 | Readable, sane hierarchy; hero shrinks fine. Minor: many same-weight blocks. |
| Spacing | 2/4 | Heavy chrome stacked before the actual task; theme picker eats full width. |
| Experience Design | 2/4 | Verbosity control hidden on mobile; primary action buried; optional action loudest. |

---

## Top fixes (highest leverage first)

### 1. Mobile is locked into the most verbose mode — biggest cause of "too much to follow"
`DeckAnalysis.cshtml:73` — the Full / Compact / Advanced layout picker is `class="toolbar chatgpt-page-toolbar desktop-only"`. Mobile users can NEVER reduce guidance; they always get `data-chatgpt-ui-mode="guided"` (line 84), the most text-heavy mode — on the smallest screen.
**Fix:** expose the layout picker on mobile AND default mobile to `focused`/`compact`. This single change cuts the wall of text the feedback is about. (Verify the `compact`/`expert` CSS classes already collapse guidance — they do on desktop; just unhide the control + flip the mobile default.)

### 2. Two filled-blue primaries compete; the optional one is loudest
`DeckAnalysis.cshtml:85-93` renders "Download session (.zip)" at the TOP of the form using `class="run-button ..."` — same primary style as "Next: Analysis" (`:169`). On mobile the sticky behavior collapses so it sits inline as the most prominent button above the actual task (see `iphone-390-fold.png`).
**Fix:** demote Download to a secondary/ghost style and move it below Step 1 (or into a small "Session" menu / the Resume `<details>`). Reserve the one filled-primary for the single forward action per step.

### 3. Reduce above-the-fold chrome before Step 1
Current mobile DOM order (`:40-135`): hero → busy → tool nav (Analyze/Build/Reference/Categories) → directions banner → download block → resume → step tabs → Step 1. The user scrolls past ~6 chrome blocks before the deck input.
**Fixes:**
- Collapse the directions banner (`:54-56`) into a compact dismissible hint, or fold it into Step 1's heading.
- On mobile, put the Analyze/Build/Reference/Categories nav behind a single "Tools ▾" menu so it stops competing with the progress tabs.
- Move the step-tabs immediately above their panel and fix the note.

### 4. Microcopy: "tabs below" is spatially wrong on mobile
`DeckAnalysis.cshtml:168` — "move through the workflow tabs **below**" but `_WorkflowStepTabs` renders at `:134`, ABOVE this Step 1 panel. On mobile (single column) the tabs are above, so the instruction points the wrong way.
**Fix:** "move through the workflow steps above" — or better, an explicit "Next: Analysis" affordance only (which exists) and drop the directional word.

### 5. Distinguish the two pill rows (nav vs progress)
`_DeckToolTabs` (page nav) and `_WorkflowStepTabs` (Step 1–5 progress) both render as pill rows of similar size/shape (see `iphone-390-fold.png`) — users can't tell navigation from progress.
**Fix:** render progress as a numbered connected stepper (1—2—3—4—5 with state), visually distinct from the nav pills. Adds the "you are here" cue that's currently weak.

### 6. Show-don't-tell the paste loop (the core mental model)
The whole tool is "generate prompt → paste into your AI → paste the reply back here," conveyed only as prose (`:43`, `:55`). New users don't grasp the round-trip.
**Fix:** per-step microcopy with a tiny 3-dot diagram: `Copy prompt → paste into ChatGPT/Claude/Gemini → paste its reply below`. Repeat the pattern at each generate/paste step so the loop is learned by repetition.

---

## Pillar notes

- **Copywriting (2):** Step names are clear ("Step 1: Deck"). But guidance is verbose and prose-only; the directional "below" is wrong on mobile; "Required" badge good. Cut words, add per-step action microcopy.
- **Visuals (2):** Card structure is clean and consistent. Hierarchy fails on buttons (two primaries) and on the duplicate pill rows. No icons to anchor scanning.
- **Color (2):** Theme contrast is fine. Problem is semantic: filled blue = "Download", "Next", AND active step pill — three different meanings, one color. Make primary mean exactly "the next thing to do."
- **Typography (3):** Sizes/leading are comfortable; `hero h1` shrinks to 1.2rem ≤480px (`site-mobile.css:99`). Could add weight contrast between step eyebrow/h2/body to aid scanning, but no real defects.
- **Spacing (2):** `site-mobile.css` is only 102 lines — desktop-first with minimal mobile adaptation. Theme picker is `flex: 1 1 100%` (`:49-52`) so it spans the full top row; combined with stacked chrome the task starts well below the fold. Tighten top region; give Step 1 breathing room as the focal block.
- **Experience Design (2):** The staged workflow concept is sound, but mobile execution buries the primary path, hides the one control (verbosity) that would help, and over-explains in text. Fixes 1–3 address most of the felt "not intuitive."

---

## Suggested sequencing for a fix phase

1. Unhide layout picker on mobile + default to compact (fix 1) — cheapest, biggest win.
2. Demote Download + dedupe primaries (fix 2).
3. Trim above-fold chrome + nav-to-menu on mobile (fix 3).
4. Microcopy + stepper + paste-loop visual (fixes 4–6) — needs a little design.

All are CSS/Razor + microcopy; no backend change. Per project rules, implement via Codex with Claude review, and re-screenshot at 390×844 + 360×800 to verify (CSS changes require visual re-verification, not grep).
