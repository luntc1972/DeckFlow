---
phase: 110
plan: 04
title: Pool Table Filtering and Search (CLUP-11/12)
status: complete
completed: 2026-07-24
requirements_addressed: [CLUP-11, CLUP-12]
executor: codex (gpt-5.4 medium)
verifier: claude
---

# Plan 110-04 Summary — Lock-Your-Pool Filter & Search

## What was built
Locked-state filtering (All / Locked / Unlocked) and case-insensitive card-name search on the
"Lock your pool" table, scoped to that table only (D-17/D-20), with a live match count and
empty-state row. Rows are hidden, never detached (D-13), so lock/package state still serializes.

- **Markup (CutLab.cshtml):** `<div class="cutlab-pool-filter" hidden>` inside the post-110-02
  `.panel-heading` (body child of `#cut-lab-section-lock-pool`, so controls collapse with the
  section, D-19) — Show group (All/Locked/Unlocked), search input ("Search card name…"),
  `.cutlab-pool-match-count`, and a hidden `<tr class="cutlab-pool-empty-row"><td colspan="4">No
  cards match.</td></tr>`. Whole container `hidden` by default (no dead controls with no JS, D-14).
- **CSS (site-common.css):** compound `.cutlab-pool-*` selectors reusing the form-control
  treatment (44px targets, `var(--line)` border, `var(--panel-soft-bg, var(--panel))`), match
  count in `--muted`/`--fs-sm`. Layout CSS in site-common.css only.
- **TS (cut-lab.ts):** `attachPoolFilterHandlers` reveals the controls, AND-combines lock-state
  (from each row's `input[data-cut-lab-lock-card]`) with name substring, toggles `hidden` on
  `tr[data-cut-lab-card]` (never `remove`), updates "Showing N of M cards", shows the empty row
  at N==0. Reuses the existing cssEscape helper. Does NOT touch `data-cut-lab-lock-count` or
  role-group counts (D-18). No persistence — resets on load (D-15).

## D-13 / D-18 / MEDIUM-3 proven via public surfaces
`cut-lab-pool-filter.test.ts` (jsdom) asserts through DOM + the public `buildCutLabStateJson`
(NOT the private `getPoolRows()`): after filtering to a single card, `querySelectorAll('tr[data-
cut-lab-card]').length` stays 3, the two non-matches are `hidden` (not removed), the serialized
pool still lists all three cards with correct `isLocked`, the `data-cut-lab-lock-count` summary
text is byte-identical, and the empty row appears at zero matches.

## Verification (claude)
- `dotnet build DeckFlow.Web` — clean 0/0.
- `npx tsc --noEmit` — clean.
- `npx vitest run` (full) — 90/90 across 22 files.
- EOL: all four files LF, no churn.
- Grep gates: pool-filter=1 (hidden), empty-row=1, placeholder present, site.css pool=0,
  ts tokens=3, 0 detach calls added, 0 getPoolRows refs in test. All pass.

## Files changed
- DeckFlow.Web/Views/Deck/CutLab.cshtml
- DeckFlow.Web/wwwroot/css/site-common.css
- DeckFlow.Web/wwwroot/ts/cut-lab.ts
- DeckFlow.Web/ts-tests/cut-lab-pool-filter.test.ts (new)
