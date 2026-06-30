---
phase: 75-tap-analyzer-surface
plan: 04
subsystem: ui
tags: [manabase, tap-analyzer, razor, css, feature-flag, xunit, render-test, a11y]

# Dependency graph
requires:
  - phase: 75-01
    provides: ShowTapAnalyzer on ManabaseViewModel, TapMarker + TapAnalyzerGloss helpers
  - phase: 75-02
    provides: ManabaseReport.TapAnalysis (per-color ColorTap + turn-1 + overall untapped)
  - phase: 75-03
    provides: flag registered + seeded OFF, fail-safe-OFF read threaded service→result→ViewModel
provides:
  - Full-width "Untapped sources" tap-analyzer card on /manabase (under the two-lens grid), flag-gated
  - Two layout-only CSS classes (.manabase-taplens, .manabase-taplens-split) in site-common.css with 640px collapse
  - IRazorViewEngine render test proving OFF=no markup / ON=markup (page byte-identity CI-enforced)
affects: [76 (bracket surface reuses .manabase-lens chrome vocabulary), 77 (deck-score tile patterns)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Flag-gated view card: entire markup inside @if (Model.Show... && Model.HasResult && report.X is { } y) — no whitespace/comment outside the block keeps the page byte-identical when OFF"
    - "Full-page Razor render test: IRazorViewEngine.FindView(controller=Deck, view=Manabase, isMainPage:false) + register the @inject services the shared partials need (IToolRegistry, IFeatureFlagCache)"
    - "Layout-only theme-safe CSS: new classes carry spacing/grid only; all color flows from the composed .manabase-lens classes so the card re-skins across all 22 themes with no per-theme fork edit"

key-files:
  created:
    - DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs
    - .planning/phases/75-tap-analyzer-surface/75-04-SUMMARY.md
  modified:
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Web/Views/Deck/Manabase.cshtml

key-decisions:
  - "Guard uses report.TapAnalysis (not report?.TapAnalysis) — report is provably non-null under the outer result guard, so the null-conditional was dropped to avoid a new CS8602 (see Deviations)"
  - "Per-color row list gated by report.ColorFindings.Count > 1 — mono-color decks show only the turn-1 headline + Overall row (no per-color noise)"
  - "cEDH renders the FULL card (locked decision D2); ✓/⚠ via the flat-80% TapMarker (D4), informational only"
  - "Page byte-identity (TAP-04) is enforced by an automated render test AND a manual OFF view-source check — not manual-only"

patterns-established:
  - "Render-asserting a flag-gated view block end-to-end (not a source-text scan) is the correct way to prove an OFF page invariant, since the markup literal always exists in the .cshtml"

requirements-completed: [TAP-01, TAP-02, TAP-04]

# Metrics
duration: ~30min
completed: 2026-06-28
---

# Phase 75 Plan 04: Tap Analyzer Surface (Wave 3 — Page Card + CSS) Summary

**Surfaced the flag-gated "Untapped sources" card on /manabase — turn-1 untapped headline + Overall + per-color ✓/⚠ rows reusing the established `.manabase-lens` chrome — with two layout-only CSS classes (no theme-fork edits) and an IRazorViewEngine render test that CI-enforces the OFF page byte-identity, closing TAP-04.**

## Performance

- **Duration:** ~30 min (incl. orchestrator-driven visual checkpoint)
- **Tasks:** 3 auto + 1 human-verify checkpoint (approved)
- **Files modified:** 4 (2 created, 2 modified)

## Accomplishments
- **Layout-only CSS (theme-safe):** `.manabase-taplens` (full-width margin tie-in) + `.manabase-taplens-split` (headline | per-color grid) added to `site-common.css`, collapsing to one column at `max-width: 640px` on the same breakpoint as `.manabase-twolens`. No new color token, no per-theme fork touched — all color flows from the composed `.manabase-lens` classes.
- **Flag-guarded card:** full-width `.manabase-lens manabase-taplens` card with `role="group"` / `aria-label="Untapped sources"`, inserted between the two-lens close and `.manabase-context`. Turn-1 headline (`Turn1UntappedPercent%` + "turn-1 untapped" + pill), an Overall untapped row, and — only when `ColorFindings.Count > 1` — one row per color with `(F1 / F1)` counts and the `TapMarker` ✓/⚠ glyph (`aria-hidden` glyph paired with an `.sr-only` "meets target"/"below target" word). Always-on `.manabase-lens-note`; gloss only when `showPlainLanguage`. Entire markup inside the `@if` → byte-identical page when OFF.
- **Automated OFF/ON render guard:** `ManabaseViewRenderTests` renders the real `Deck/Manabase` view via `IRazorViewEngine` (isMainPage:false); OFF asserts no `manabase-taplens` / `aria-label` / `turn-1 untapped`, ON asserts `manabase-taplens` + `aria-label="Untapped sources"` + turn-1 microcopy + the Overall `82% untapped` line. Page byte-invariant is now CI-enforced, not manual-only.
- **Verified GREEN:** `dotnet build DeckFlow.sln` 0/0; full Manabase Web suite 140/140 (incl. the 2 new render facts); no existing tests broken.

## Task Commits

Each task was committed atomically:

1. **Task 1: layout-only tap-analyzer CSS classes** - `a29cb7d2` (feat)
2. **Task 2: flag-guarded tap-analyzer card in Manabase.cshtml** - `1f54bb51` (feat)
3. **Task 3: OFF/ON render test (ManabaseViewRenderTests)** - `6232e876` (test)

## Files Created/Modified
- `DeckFlow.Web/wwwroot/css/site-common.css` - `.manabase-taplens` + `.manabase-taplens-split` + 640px collapse (layout-only).
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` - flag-guarded "Untapped sources" card (purely additive, +39 lines, 0 deletions).
- `DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs` - IRazorViewEngine OFF/ON render facts + multi-color fixture + harness registering the partial's `@inject` services.

## Decisions Made
- **`report.TapAnalysis` instead of the planned `report?.TapAnalysis`** in the `@if` guard — see Deviations.
- **Per-color list gated on `ColorFindings.Count > 1`** so mono-color decks show only the headline + Overall row.
- **cEDH renders the full card (D2)**; the threshold marker is the flat-80% `TapMarker` (D4), informational only.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Dropped the null-conditional in the card guard to avoid a new CS8602 warning**
- **Found during:** Task 2 (flag-guarded card in Manabase.cshtml)
- **Issue:** The planned guard `@if (Model.ShowTapAnalyzer && Model.HasResult && report?.TapAnalysis is { } tap)` introduced a NEW `warning CS8602` at the pre-existing `report.Mode` line (~:268). The null-conditional `?.` downgraded the compiler's tracked null-state for `report` to "maybe null" and that downgrade flowed past the `@if` block — even though `report` is guaranteed non-null by the outer `@if (Model.HasResult && report is not null)` guard at line 157.
- **Fix:** Changed the guard to `report.TapAnalysis is { } tap` (no `?.`). `report` is provably non-null under the outer result guard, so this is functionally identical; the flag + result + tap-non-null semantics and the `manabase-taplens` markup are unchanged.
- **Files modified:** `DeckFlow.Web/Views/Deck/Manabase.cshtml`
- **Verification:** `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` → 0 warnings / 0 errors (was 1 warning before the fix); `dotnet build DeckFlow.sln` 0/0.
- **Committed in:** `1f54bb51` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug — a planned snippet that introduced a compiler warning).
**Impact on plan:** Required to satisfy the "no new build warnings" gate (CLAUDE.md Definition of Done). No behavioral or scope change.

## Issues Encountered
- **Full-page render harness needed the shared partials' injected services.** Rendering `Deck/Manabase` standalone failed with `No service for type 'IToolRegistry'` (the shared `_DeckToolTabs` partial `@inject`s `IToolRegistry` + `IFeatureFlagCache`). Resolved per the plan's guidance ("fix the harness, do NOT fall back to a source-text scan") by registering the real `ToolRegistry` (parameterless) and the existing `FakeFeatureFlagCache`, plus `AddDataProtection()` for the view's antiforgery tokens. The test then renders the real view and genuinely discriminates OFF vs ON.

## Human Verification (Task 4 — approved)
Orchestrator-driven visual verification (headless server + Playwright, live Scryfall) was **APPROVED**:
- Multi-color card renders and re-skins across Classic (`site.css`) / Azorius / Nyx; turn-1 headline + Overall + per-color rows (Blue/White) with `(24.0 / 24.0)` counts and ✓ markers.
- Single-color (mono) deck correctly omits the per-color list (Overall row only) — confirmed in raw HTML.
- Mobile ≤640px collapses to one column, no horizontal scroll.
- Flag OFF → no `manabase-taplens` markup (card absent).
- Screenshots saved under `.planning/ui-design/cycle13/screenshots/` (`tap-multi-*`, `tap-mono-*`, `tap-multi-site-mobile.png`).

## Next Phase Readiness
- **Phase 75 complete (4/4 plans).** TAP-01, TAP-02, TAP-04 satisfied (page + artifact behind the OFF-seeded flag; byte-identity render-test enforced). TAP-03 was satisfied in 75-02 (single-pass, single source of truth).
- Flag `analysis.manabase.tap-analyzer` remains seeded OFF in prod — operator flips it on after deploy.
- No blockers. Ready for Phase 76 (Bracket Classifier + Balancer).

---
*Phase: 75-tap-analyzer-surface*
*Completed: 2026-06-28*

## Self-Check: PASSED

- Created/modified files present: `site-common.css`, `Manabase.cshtml`, `ManabaseViewRenderTests.cs`, `75-04-SUMMARY.md`.
- All three task commits exist in history: `a29cb7d2`, `1f54bb51`, `6232e876`.
- Full solution builds 0/0; Manabase Web suite 140/140 green (incl. 2 new render facts).
