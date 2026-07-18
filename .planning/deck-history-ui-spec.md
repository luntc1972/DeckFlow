# Deck History — UI Design Spec

**Date:** 2026-07-16
**Companion to:** `.planning/deck-history-design.md` (feature spec), `.planning/deck-history-plan.md` (Task 7)
**Page:** `/deck-history` · Nav section: Build · Tab: Deck History

## Design intent

The page must read as "your deck's logbook". One column, form-then-results, exactly
like Bracket Check — no wizard, no tabs. The emotional hook is ownership: the file is
yours, DeckFlow keeps nothing. Say that above the fold.

Visual language: reuse Bracket Check's page skeleton verbatim (panel cards, section
headings, form rows, copy-button prompt box). Zero new visual vocabulary; only two
new composite blocks (timeline table, pair-diff columns), both styled with existing
theme tokens in `site-common.css`.

## Layout — desktop (≥ 900px)

```
┌──────────────────────────────────────────────────────────────┐
│ [_DeckToolTabs strip — Deck History active]                  │
├──────────────────────────────────────────────────────────────┤
│ Deck History                                     (h1)        │
│ Track your deck's evolution in a file you own. DeckFlow      │
│ never stores your history — download it, keep it, bring it   │
│ back next time.                                  (intro p)   │
├──────────────────────────────────────────────────────────────┤
│ PANEL: Load                                                  │
│ ┌ History file (optional) ─────────────────────────────────┐ │
│ │ [Choose file…  deck-history-tivit-20260716.json]         │ │
│ │ hint: First visit? Skip this — just import your deck     │ │
│ │ below and download your new history file.                │ │
│ └───────────────────────────────────────────────────────────┘ │
│ ┌ Current deck ─────────────────────────────────────────────┐ │
│ │ [Use public deck URL ▾]                                   │ │
│ │ [https://moxfield.com/decks/… or archidekt.com/decks/…  ] │ │
│ │ (textarea shown instead when "Paste text" selected)       │ │
│ │ Deck name [Tivit Ad Nauseam        ]                      │ │
│ └───────────────────────────────────────────────────────────┘ │
│ ┌ This version ─────────────────────────────────────────────┐ │
│ │ Label (optional) [post-ban        ]                       │ │
│ │ Notes — why did the deck change?                          │ │
│ │ [ Cut Dockside after the ban; leaning harder into      ]  │ │
│ │ [ the Ad Nauseam line.                                 ]  │ │
│ └───────────────────────────────────────────────────────────┘ │
│ [_AiSelector]                [ Update history ]  (primary)   │
├──────────────────────────────────────────────────────────────┤
│ (results — only after POST)                                  │
│ ⚠ notice list (identical-deck warning, repair warnings)      │
│ PANEL: Timeline                                              │
│ │ V# │ Date       │ Label    │ Notes            │ Cards │Δ │ │
│ │ 3  │ 2026-07-16 │ post-ban │ Cut Dockside …   │ 100   │+1 −1│
│ │ 2  │ 2026-07-01 │          │ Added Remora …   │ 100   │+1   │
│ │ 1  │ 2026-06-01 │          │ Initial list.    │ 100   │ —  │ │
│   (newest first; Notes cell wraps, never truncates)          │
│ PANEL: Compare versions                                      │
│ [V1 (2026-06-01) ▾]  →  [V3 (2026-07-16) ▾]   [Compare]     │
│ ┌ Adds ────────┬ Cuts ─────────────┬ Qty changes ─────────┐  │
│ │ Mystic Remora│ Dockside Extort.  │ Island 8→7           │  │
│ └──────────────┴───────────────────┴──────────────────────┘  │
│ PANEL: AI prompt — "How has this deck evolved?"              │
│ [readonly textarea, ~14 rows]                    [Copy]      │
│ PANEL: Save your history                                     │
│ Download the updated file and keep it with your deck.        │
│ [ Download deck-history-….json ]  (data-prompt-download)     │
└──────────────────────────────────────────────────────────────┘
```

## Layout — mobile (≤ 640px)

- All panels full-width, stacked in the same order.
- Timeline table drops the Label column (label folds into the Notes cell as a
  bold prefix); horizontal scroll inside the panel if still tight
  (`overflow-x: auto` on the table wrapper — page body never scrolls sideways).
- Pair-diff three columns stack vertically: Adds, then Cuts, then Qty changes,
  each with its heading.
- Download button full-width. Download uses the `deck-sync.js` fetch/blob
  intercept, so a mobile pull-to-refresh never re-triggers it (house pattern).

## States

| State | What shows |
|---|---|
| First visit (GET) | Form only. No results panels. File input empty, URL mode preselected. |
| New history created | Green-tinted notice "Started a new history — version 1 saved." + all four result panels. Compare panel hidden (needs ≥ 2 versions); in its place a muted line: "Add a second version to compare." |
| Appended | Notice "Version N added." + all panels; compare defaults to N−1 → N. |
| Inspect (file only, no deck) | No append notice. Timeline + compare + prompt + download (re-serialized, deltas recomputed). |
| Identical deck | Amber notice "The imported deck is identical to the latest version — no new snapshot was added." Panels still render. |
| Repair warnings | Amber notice list, e.g. "Version ids were repaired (renumbered in date order)." Non-blocking. |
| Errors (bad file / wrong major / import failure / timeout / >1 MB) | Standard red `ErrorMessage` alert above the form, form values preserved, no result panels. |

## Components + classes

| Block | Class (new, in `site-common.css`) | Notes |
|---|---|---|
| Notices | `.history-warnings` | `<ul>`, amber left-border, token `var(--warning, var(--accent))` fallback chain matching existing notice styling |
| Timeline | `.history-timeline` | Table inside `overflow-x:auto` wrapper; row zebra via existing table tokens; Δ column renders `+a −c` counts with `.history-delta-add` / `.history-delta-cut` colors (reuse the add/cut colors deck-sync's diff output uses) |
| Pair diff | `.history-diff` | CSS grid `grid-template-columns: repeat(3, 1fr)`; `@media (max-width: 640px)` → `1fr` |
| Prompt box | existing copy-box pattern | Same markup as Bracket prompt (`copy-button`, `data-copy-target="#deck-history-prompt"`) |
| Download | existing button styles | `data-prompt-download-submit` marked form |

Everything else (panels, form rows, selects, textareas, submit button, busy
indicator) = existing classes as used in `Bracket.cshtml`. No new tokens; do not
touch per-theme CSS files.

## Copy (exact strings)

- Intro: "Track your deck's evolution in a file you own. DeckFlow never stores your history — download the file, keep it with your deck, and bring it back whenever the list changes."
- File hint: "First visit? Skip this — import your deck below and download your new history file."
- Notes label: "Notes — why did the deck change?"
- Download panel: "Download the updated file and keep it with your deck. Re-upload it next time to add the next version."
- Empty-compare line: "Add a second version to compare."

## Theming + verification

- Both viewports (1280 / 390) × Classic, Azorius, Nyx themes — screenshots via the
  Task 8 e2e spec into `.planning/ui-design/deck-history/screenshots/`.
- Dark themes: panels use `var(--panel)` (never `--theme-surface`).
- Checkboxes/selects: native-chrome-free per house checkbox/select conventions
  (`data-df-select` on selects, as Bracket does).

## Accessibility

- File input, selects, textareas all have `<label for>`.
- Timeline is a real `<table>` with `<th scope="col">`.
- Diff columns are three `<section>`s with `<h3>` headings (screen-reader order matches stacked mobile order).
- Notices use `role="status"`; the error alert uses the site's existing alert markup (`role="alert"`).
- Copy button already carries accessible text ("Copy").
