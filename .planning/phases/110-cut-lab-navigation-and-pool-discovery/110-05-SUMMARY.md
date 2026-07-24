---
phase: 110
plan: 05
title: Text-First Card Disclosures and Package Help (CLUP-15/16)
status: complete
completed: 2026-07-24
requirements_addressed: [CLUP-15, CLUP-16]
executor: codex (gpt-5.4 medium)
verifier: claude
---

# Plan 110-05 Summary — Card-Text Disclosures + Package Help

## What was built
Text-first `<details class="cutlab-card-text">` disclosures in pool rows and under Structural
evidence chips (fed by 110-01's `CardTextByCardName`), plus the package-assignment help copy.

- **Shared Razor helper (CutLab.cshtml):** a `RenderCardTextDisclosure(string cardName)` local
  function emits the disclosure with identical class names/markup (D-26), invoked in BOTH
  placements — pool-row Card cell (`card.Name`) and under each lockable evidence chip
  (`evidenceCard.Name`) — so both render server-side on the initial no-JS load. Body = meta line
  (type line · mana cost · set #collector) in `--muted` + oracle text in `--ink`, `var(--fs-sm)`.
  **Fail-open (D-31):** returns with no disclosure when the lookup misses; each field guarded by
  `IsNullOrWhiteSpace`; renders nothing when neither meta nor oracle is available. All text is
  normal Razor HTML-encoded — no `Html.Raw` (T-110-09).
- **Package help (Task 2):** static `.cutlab-package-help` block at the top of the Packages
  `<details>` — heading "How packages work" + the exact D-34-first body copy. A single inline hint
  (`<p class="manabase-help">`, reusing the theme-safe class) sits once above the pool table near
  the pool status text (line 317, above the table at 380; not per-row — D-33). Both strings are
  verbatim from the UI-SPEC Copywriting Contract.
- **CSS:** compound `.cutlab-card-text*` rules in site-common.css (quiet secondary chrome, no
  accent fill, `--ink`/`--muted` summary, 44px target). Layout CSS in site-common.css only.

## HIGH-2 — structural disclosures survive AJAX patch (Task 3)
`renderStructuralFindings` rebuilds the findings body from patch DTOs on every decide/adjust,
wiping the Razor-rendered structural disclosure. Fix is VIEW-LAYER ONLY: when the applier builds
a lockable evidence chip, it deep-clones the matching pool-row disclosure
(`tr[data-cut-lab-card="<name>"] .cutlab-card-text`, `open` stripped) and appends it under the
chip. No card text added to `CutLabStateJson`, `CutLabUiPatchDto`, or `CutLabUiPatchBuilder`
(grep gate == 0) — the pool-row DOM is the single source (T-110-12). Fail-open: no pool-row
disclosure → attach nothing.

## Tests
New `cut-lab-structural-cardtext.test.ts` (jsdom) drives the existing decide-flow harness: seeds a
pool row with a `.cutlab-card-text` for card X, mocks a patch whose structural findings include
evidence card X, dispatches the submit, and asserts a `.cutlab-card-text` is present under the
rebuilt chip after the patch.

## Verification (claude)
- `dotnet build DeckFlow.Web` — clean 0/0.
- `npx tsc --noEmit` — clean.
- `npx vitest run` (full) — 91/91 across 23 files.
- EOL: all four files LF, no churn.
- Grep gates: shared helper renders card-text in both placements, 0 Html.Raw added, site.css
  card-text=0, ts re-attach reads `tr[data-cut-lab-card=`, 0 DTO/state card-text surface, exact
  help + hint copy present, hint above the pool table and single. All pass.

## Files changed
- DeckFlow.Web/Views/Deck/CutLab.cshtml
- DeckFlow.Web/wwwroot/css/site-common.css
- DeckFlow.Web/wwwroot/ts/cut-lab.ts
- DeckFlow.Web/ts-tests/cut-lab-structural-cardtext.test.ts (new)
