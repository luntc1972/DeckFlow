---
quick_id: 260712-l1z
slug: fix-deck-analysis-mobile-overflow-prompt
type: quick
created: 2026-07-12
branch: quick/deck-analysis-ux-fixes
files_modified:
  - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml
  - DeckFlow.Web/wwwroot/css/site-common.css
  - DeckFlow.Web/e2e/deck-analysis-mobile.spec.ts
requirements: [UX research 2026-07-12 HIGH-1, MED-2, MED-3]
---

# Quick Task: Deck-analysis UX fixes (mobile overflow + porthole + context default-open)

Source: `.planning/ui-design/deck-analysis-ux-research.md` (in main repo working tree; findings restated fully below — do not require that file).

## Task 1 (MUST-HAVE): Fix mobile horizontal overflow

**Bug (verified live):** result-state document scrollWidth 479px vs 390px viewport (89px bleed).
Root cause: `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml:441` — `<pre spellcheck="false">@Model.InputSummary</pre>`
inside the Deck Summary panel has `white-space: pre` and no wrap/scroll fallback. Long line
"Target commander bracket: Bracket 1: Exhibition" (needs ~424px vs ~281px available) pushes the
entire document wide.

**Fix:** scoped CSS in `site-common.css` (NEVER site.css — guild themes are standalone forks):
give that summary `<pre>` `white-space: pre-wrap; overflow-wrap: break-word;` (preferred over
`overflow-x: auto` — content is prose-like key: value lines, wrapping reads fine). Scope via the
panel's existing class or add a class to the `<pre>` — do not restyle all `pre` elements globally
(other pages use `<pre>` for decklists where hard whitespace matters).

**Guard:** new `DeckFlow.Web/e2e/deck-analysis-mobile.spec.ts` — drive /deck-analysis at 390x844,
paste fixture `DeckFlow.Web/e2e/fixtures/winota-cedh.txt`, generate the analysis packet (Step 2),
then assert `document.documentElement.scrollWidth <= document.documentElement.clientWidth`.
Reuse navigation/submit setup from an existing deck-analysis e2e spec if one exists; otherwise
mirror the manabase spec setup pattern.

## Task 2 (MED-2): analysis.txt porthole expand affordance

`#analysis-output` textarea (DeckAnalysis.cshtml:463) renders ~51KB/401-line artifacts in a fixed
~240px box with only inner scroll. Add an expand/collapse control: a small toggle button near the
Copy button ("Expand" / "Collapse") that toggles a CSS class growing the textarea to ~60vh
(CSS in site-common.css). Plain progressive enhancement: tiny TS addition is acceptable ONLY if an
existing page TS file naturally hosts it — otherwise prefer a checkbox/details-free CSS approach or
a minimal inline-free script in the page's existing script set (wwwroot/ts — compiled output is
gitignored; never commit .js). Apply the same affordance to the reference.txt and
set-upgrade-analysis.txt textareas if they share the same porthole class (consistency), but
analysis.txt is the must-have.

## Task 3 (MED-3): "Analysis context" details default-open

DeckAnalysis.cshtml:251 `<summary>Analysis context</summary>` — the parent `<details>` defaults
closed although its fields (Format/Deck name/Strategy notes/Meta notes) materially change the AI
output. Make it `open` by default on Step 2. Keep user-toggle behavior otherwise unchanged.

## Constraints

- Preserve LF line endings (repo `.gitattributes` enforces; do not churn EOL).
- Changed-lines format gate applies to C# only; this task is cshtml/css/ts — still touch only lines that need touching.
- No new packages. No lockfile changes. Compiled `wwwroot/js/*.js` never staged.
- Layout CSS → site-common.css only. No theme-token additions.
- Tests: e2e spec (Task 1) is required; no xUnit needed (no C# logic change) — note this in SUMMARY.
- UI change → 2-viewport visual verification (desktop 1280x900 + mobile 390x844) before done, plus 2-theme spot check.

## Acceptance

1. 390px result state: no document horizontal overflow (e2e green).
2. Analysis textarea expandable; default unchanged height; Copy still works.
3. Analysis context open by default.
4. `dotnet build DeckFlow.sln` clean; existing e2e deck-analysis/manabase specs unaffected.
