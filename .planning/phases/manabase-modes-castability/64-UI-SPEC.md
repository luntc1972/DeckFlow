# Phase 64 — UI-SPEC (Mana Base page: modes, commander importance, castability)

Design contract for the Wave 2 view changes. Page: `Views/Deck/Manabase.cshtml`. All new
layout CSS goes in `site-common.css` (NOT `site.css` or a theme file) per the theme-system
constraint; colors use existing semantic theme tokens, never hardcoded hex.

## 1. Inputs (added to the existing form, above submit)

**Mode — segmented radio** (`Casual` | `cEDH`):
- Default **Casual** checked. Label: "Deck type". Helper: "cEDH = lower land count, fast-mana heavy."
- Renders as a 2-segment pill control; persists selection on postback.

**Commander importance — segmented radio / select** (`Central` | `Standard` | `Low`):
- Default **Standard**. Label: "How important is casting your commander?"
- Option helper text (tooltip or sub-label):
  - Central — "Must cast ASAP, every game (e.g. Brago)."
  - Standard — "Matters, cast when convenient."
  - Low — "Optional / late value."
- Disabled/hidden when the deck has no commander (rare for the Commander audience — keep enabled, no-op if 0 commanders).
- Persists on postback.

Both controls sit in one responsive row that wraps to stacked on narrow viewports.

## 2. Results header

One line echoing context next to the land summary:
`Mode: Casual · Commander: Central` — plain text, so it is screen-reader friendly and
theme-agnostic.

## 3. Color findings (updated — COLOR-AGG-01)

Each color row shows the population view, not just one spell:
- `{Color}: {ActualSources:F1} sources · needs {RequiredSources} for {WorstSpell} · {UnderSupportedCount} of {TotalForColor} cards under-supported`
- The **weakest color** row is visually emphasized (left accent border + `--color-danger` token) and named in the summary.
- Commander colors get a small crown/star glyph so the user sees which colors are the identity.

## 4. Castability table (Casual mode only — CAST-03)

Heading: **"Castability — chance to cast on curve"** + one-line caveat:
> Estimate, on the play, on its mana-value turn. Counts ramp; mana rocks/dorks aren't listed.

Table columns: `Card | MV | Cast on curve | Limiting`
- **Commander row(s) pinned to the top**, flagged with a crown/star glyph + `IsCommander`
  styling (subtle highlighted row), regardless of sort.
- Remaining rows sorted **worst cast% first**.
- `Cast on curve` = `{CastPercent}%`, with a semantic color chip AND text (never color alone):
  - `< 70%` → danger token, label "low"
  - `70–89%` → warning token, label "ok"
  - `≥ 90%` → success token, label "good"
- `Limiting` → friendly text: `mana`, `color: U`, `mana + color`.
- Long card names truncate with ellipsis + title attr; never force horizontal overflow.

cEDH mode: castability table hidden (v1); show a one-line note "Castability view is available in Casual mode."

## 4b. Two expandable "formula" panels (FORMULA-01)

Below the results, two collapsible `<details>`/`<summary>` panels (both collapsed by default):

**"How the analysis works"** (static methodology, no deck needed — also shown on the empty form):
- The Karsten land-count regression (the actual coefficients) + the cEDH adjustment.
- The castability model in plain terms: a Monte-Carlo simulation (London mulligan, joint mana+color, ramp deployed in-sim, weighted/conditional sources, fetchlands credited to their fetchable colors), reported as "% castable on its on-curve turn."
- The colored-source aggregation (worst-driver + population) and commander-importance weighting.
- Credit Frank Karsten; note it's an estimate validated against community tools.

**"This deck's numbers"** (the formula *with the entered deck* — only when a result exists):
- Land target with the regression terms plugged in for THIS deck: `target = f(avgMV=…, ramp=…, fastMana=…, MDFC=…, commanders=…)` showing each term's contribution and the final number vs actual lands.
- Per-color source tally (W/U/… counts, incl. how duals/any-color/fetch sources were credited).
- Sim parameters used (turns, on-the-play, mulligan band, trials) and the effective on-curve turns after any cost reduction.
- Essentially "show the work" so a user can audit why a verdict came out as it did.

Both panels live in `site-common.css`; use native `<details>` (keyboard-accessible, no JS needed). Mobile: full-width, no overflow.

## 5. States (must all be designed)

- **Empty** — form only, no results block.
- **Loading** — existing submit/spinner pattern (unchanged).
- **Error** — existing `ErrorMessage` banner (timeout / validation / upstream) unchanged.
- **Success, no commander** — castability table renders, no pinned row, importance control no-ops.
- **Unresolved cards** — existing unresolved notice still shown above results.
- **cEDH success** — land target + color findings + mode echo; castability replaced by the note.

## 6. Responsive (desktop + mobile, all 24 themes)

- Input row: flex-wrap; segmented controls stack vertically under ~480px.
- Castability table wrapped in `.castability-scroll { overflow-x: auto; }` so 4 columns never
  blow out the viewport; on very narrow screens the table may scroll horizontally within its
  own wrapper only (page itself must not gain a horizontal scrollbar).
- Acceptance: `document.scrollWidth ≈ clientWidth` (no page-level horizontal scroll) at desktop
  AND mobile viewport, verified in the Playwright e2e across at least the default + one alt theme.

## 7. Accessibility

- Radio groups have a `<fieldset>` + `<legend>` (the labels above), each input a real `<label>`.
- Castability `<table>` uses `<th scope="col">`; cast% conveyed by **text label + %**, not color alone.
- Pinned commander row marked with visible glyph + `aria-label="commander"` (not color alone).
- Tooltips are supplementary; the same info exists as visible helper text.

## 8. Theme

- Segmented pills, table, badges, accent borders styled with existing tokens
  (`--color-danger`, `--color-warning`, `--color-success`, surface/border tokens). If a token is
  missing, add it to `:root` of EACH theme file (not inline) per the theme constraint.
- No new theme fork; this is shared chrome in `site-common.css`.

## 9. Definition of done (UI)

- Renders correctly across all guild themes, light + dark, desktop + mobile — no overflow, no
  unreadable contrast (web-page-change DoD rule).
- Playwright smoke asserts: both selectors present + persist; castability table present in Casual
  with commander pinned + worst-first; absent in cEDH; no page horizontal scroll at 2 viewports.
