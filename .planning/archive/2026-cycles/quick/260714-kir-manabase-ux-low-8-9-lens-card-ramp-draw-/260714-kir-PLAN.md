# Quick Task 260714-kir: Manabase UX LOW-8/9 — PLAN

**Date:** 2026-07-14
**Branch:** `quick/manabase-ux-low89` (off local main `75157a92`)
**Source:** `.planning/captures/manabase-backlog-2026-07-13.md` §4 / `.planning/ui-design/manabase-ux-research.md` LOW items 8-9.
**Nature:** Visual-only polish. No engine/report changes. LOW-10 explicitly out of scope.

## Side Effects Report

**Files/modules affected (direct):**
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` (both LOW-8 sections + LOW-9 line)
- `DeckFlow.Web/wwwroot/css/site-common.css` (lens harmonization for the two sections; layout CSS lives here per project rule)
- `DeckFlow.Web/Models/ManabaseDisplay.cs` (one new pure helper for the LOW-9 distribution line)
- `DeckFlow.Web.Tests` (unit tests for the new helper)
- `DeckFlow.Web/e2e/` (assert new lens classes; existing locators must keep passing)

**Files/modules affected (transitive):** None — `report.Castability` / `RampDrawBudget` / commander rows already reach the view; no controller/service change.

**Shared state touched:** None.

**External surfaces:** None (no API, no DB). Prompt artifacts untouched (visual only).

**Contract changes:** None. Section ids `#manabase-ramp-draw` and `#manabase-command-zone-castability` (anchor nav + e2e) and class `.manabase-cmd-castability` (e2e locator, DOM-order assertion in `manabase-commander-callout.spec.ts`) MUST be preserved.

**Tests requiring updates or additions:** New xUnit tests for the distribution-line helper; e2e additions for lens-class presence; existing e2e (`manabase-commander-callout.spec.ts`, `manabase-mulligan.spec.ts`, `manabase-lens-visual.spec.ts`) must pass unmodified unless an assertion targets a changed style hook — flag any such edit explicitly.

**Backward compatibility risks:** Theme regressions (lens classes already themed — reusing them is the point); mobile layout (lens grid collapses at 640px — new sections sit OUTSIDE `.manabase-twolens`, they adopt lens *anatomy* not the two-lens grid).

**Open questions / assumptions:** Ramp/draw lens gets no big-number (label + lines + note is valid lens anatomy — taplens/mulliganlens precedent). Command-zone gets a soft big number only when a single commander row exists; partners keep per-commander lines.

## LOW-8 — lens-card fold (Manabase.cshtml + site-common.css)

### 8a: Ramp/draw advisory (`Manabase.cshtml:714-792`, both budget and fallback variants)
- Section keeps `id="manabase-ramp-draw"`; class becomes `manabase-lens manabase-rampdraw`.
- `h3` class `manabase-verdict-heading` → `manabase-lens-label` (text unchanged).
- Existing `.manabase-rampdraw-line` / `.manabase-rampdraw-note` content stays; restyle `.manabase-rampdraw-note` to match `.manabase-lens-note` scale (or swap class to `manabase-lens-note` if identical usage).
- CSS: `.manabase-rampdraw` keeps only what `.manabase-lens` doesn't provide (drop duplicated panel/padding/radius rules it now inherits). **Keep the section margin** — `.manabase-lens` provides none (plan-review LOW).
- **Third render path (plan-review MED):** in the budget path, `RenderRampBreakdownDetails(rampSummary)` currently renders AFTER/OUTSIDE the section (~lines 781-790). Move it INSIDE the ramp/draw lens section (after the note) so the whole ramp/draw area reads as one card, in both budget and fallback paths. Preserve its content/classes.

### 8b: Command-zone castability (`Manabase.cshtml:736-756`)
- Section keeps `id="manabase-command-zone-castability"` AND class `manabase-cmd-castability` (e2e locator); add `manabase-lens`.
- `h3` → `manabase-lens-label`.
- Single-commander case: render the commander's `@c.CastPercent%` as `manabase-lens-big manabase-lens-big--soft` with sub-span "by on-curve turn @c.OnCurveTurn" (or equivalent existing copy), then the existing detail line(s). Partner case (2 rows): keep per-commander `.manabase-cmd-castability-line` rows, no big number.
- CSS: drop `.manabase-cmd-castability` rules duplicated by `.manabase-lens`; **explicitly retain the left accent border, the section margin, and `.manabase-cmd-castability-line`** (e2e locator). Section stays a DIRECT child of `.result-panel` — the commander-callout DOM-order assertion depends on it; do not add wrappers. Heading visible text unchanged.

## LOW-9 — cast-rate distribution shape (view + ManabaseDisplay + tests)

- New pure static helper in `ManabaseDisplay`: `CastRateShapeText(IReadOnlyList<CardCastability> rows)` (exact signature per existing conventions) — buckets the GIVEN rows by the EXISTING thresholds `GoodCastabilityThreshold` (90) and `OkCastabilityThreshold` (70) and returns the keep-size-style dot-separated line, e.g. `"≥90% cast: 41 spells · 70–89%: 12 · <70%: 4"`. Empty/edge: returns empty string when no rows (view omits the line).
- **Row-set consistency (plan-review LOW):** the view passes the SAME filtered row set the `trackedNonCommander` pill / headline already use — including its existing all-commander fallback — so the shape line, pill count, and headline never disagree. Do not re-derive the filter inside the helper.
- Render inside the "Simulated cast rate" lens (`Manabase.cshtml:534-543`) directly under the big number, mirroring the mulligan keep-size line pattern (line ~646) and its text styling (reuse the same class the mulligan distribution line uses, or `manabase-lens-note` if that line has no dedicated class).
- xUnit tests: bucket boundaries (90 exactly → good bucket, 70 exactly → mid, 69 → low), empty list → empty string, single-row, and the all-commander fallback path at the VIEW's row-set derivation level if testable (else document in e2e).

## Verification gates (UI change — all mandatory)

1. Build clean (Windows dotnet.exe); Web.Tests green incl. new helper tests; Core.Tests untouched-green.
2. `tsc` clean if any TS touched (none expected).
3. Playwright e2e: full manabase specs pass; the three named specs must pass with at most additive edits.
4. Screenshots desktop (1280px) + mobile (~390px) × themes classic, nyx, azorius minimum — server via `scripts/run-web-test.sh` (auto-browser suppressed), WSL headless (`env -u DISPLAY -u WAYLAND_DISPLAY`). Verify: both sections read as lens cards, no layout break at 640px, no theme with unreadable contrast (dark themes use `--panel`).
5. EOL preserved (LF); changed-lines format gate; carve-outs honored.
6. README/help: no behavior change → no edit needed; confirm help pages don't screenshot these sections (if they describe them textually, no change required).

## Acceptance criteria

1. Ramp/draw + Command-zone sections visually match the lens-card system (label style, panel, spacing) on all themes/viewports.
2. Anchor ids and `.manabase-cmd-castability` locator unchanged; commander-callout DOM-order e2e passes.
3. Cast-rate lens shows the distribution line using existing 90/70 thresholds; numbers sum to tracked non-commander spell count.
4. No engine/report/controller diffs; no prompt-artifact diffs.
5. All gates above green; screenshots archived under `.planning/ui-design/low89/screenshots/`.
