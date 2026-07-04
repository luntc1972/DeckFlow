---
phase: 81-opening-hand-mulligan-evaluator
plan: 03
subsystem: ui
tags: [manabase, razor, feature-flags, playwright, xunit, mulligan]

# Dependency graph
requires:
  - phase: 81-01
    provides: "ManabaseMulliganEvaluation deck-level aggregate + OpeningHandSample (TrackedSpellName/TrackedOnCurveTurn/OnCurveCastable/HasPlan), always computed in Core and attached to ManabaseReport.MulliganEvaluation"
  - phase: 81-02
    provides: "analysis.mulligan-eval flag (seeded OFF both dialects) + ManabaseAnalysisResult.ShowMulliganEval fail-safe-OFF gate + the paste-artifact AppendMulliganEvaluationBlock prose this card mirrors"
provides:
  - "ManabaseViewModel.ShowMulliganEval view-model flag, mapped from result.ShowMulliganEval beside ShowTapAnalyzer"
  - "ManabaseDisplay.KeepableMarker(band) — keepable-band css/marker helper mirroring TapMarker"
  - "Flag-guarded manabase-mulliganlens on-page lens card on /manabase mirroring the paste-artifact content (keepable band, keep-size process, color/curve, tracked-spell-attributed representative openers)"
  - "manabase-mulliganlens / manabase-mulliganlens-split / manabase-mulliganlens-openers layout CSS in site-common.css (no new :root tokens, no theme file touched)"
  - "IRazorViewEngine excision byte-identity test proving the flag-OFF page is identical to ON minus the contiguous mulligan-lens block"
  - "manabase-mulligan.spec.ts desktop 1280 + mobile 390 live-UX smoke toggling the flag via /Admin/Flags"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TAP-02 on-page lens recipe reused exactly: ViewModel flag -> controller mapping -> flag-guarded one-contiguous-block Razor card styled only in site-common.css -> IRazorViewEngine excision byte-identity test -> Playwright admin-flag-toggle live-UX smoke"
    - "BuildPopulatedModel(showTapAnalyzer, showMulliganEval = false) extended with an independent second flag parameter over one shared fixed report, so tap and mulligan cards can each be toggled in isolation against the same byte-identical baseline"

key-files:
  created:
    - DeckFlow.Web/e2e/manabase-mulligan.spec.ts
  modified:
    - DeckFlow.Web/Models/ManabaseViewModel.cs
    - DeckFlow.Web/Models/ManabaseDisplay.cs
    - DeckFlow.Web/Controllers/ManabaseController.cs
    - DeckFlow.Web/Views/Deck/Manabase.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs

key-decisions:
  - "KeepableMarker keys off the aggregator's own coarse KeepableBand string (\"high\"/\"medium\"/\"low\") rather than re-deriving a threshold from KeepableHandPercent in the view layer — the band is already the single source of truth from ComputeMulliganEvaluation (Plan 81-01), so the marker can never disagree with the printed band."
  - "The excision test holds ShowTapAnalyzer constant at false for both the OFF and ON mulligan renders so the tap card never appears in either page, keeping the differing region isolated to exactly the mulligan-lens block (mirrors the existing tap excision test's isolation discipline in reverse)."
  - "manabase-mulligan.spec.ts reuses the exact admin-lock + synthetic CF-Connecting-IP + /Admin/Flags toggle convention from deck-analysis-render.spec.ts (shared SQLite flag store + brute-force throttle require serialization across all admin e2e specs) rather than inventing a second locking scheme."

patterns-established: []

requirements-completed: [MULLIGAN-01, MULLIGAN-02, MULLIGAN-06]

# Metrics
duration: 22min
completed: 2026-07-03
---

# Phase 81 Plan 03: Opening-Hand / Mulligan On-Page Readout Summary

**The `/manabase` page now renders a flag-guarded opening-hand lens card (keepable band, keep-size process, tracked-spell-attributed representative openers) behind `ShowMulliganEval`, proven byte-identical to baseline when OFF by an `IRazorViewEngine` excision test.**

## Performance

- **Duration:** ~22 min
- **Started:** 2026-07-03T23:19:00Z
- **Completed:** 2026-07-03T23:41:30Z
- **Tasks:** 3
- **Files modified:** 7 (1 new e2e spec, 6 modified)

## Accomplishments

- `ManabaseViewModel.ShowMulliganEval` flows from `ManabaseAnalysisResult.ShowMulliganEval` through `ManabaseController`'s analyze action, mirroring `ShowTapAnalyzer` line-for-line.
- `ManabaseDisplay.KeepableMarker(band)` maps the aggregator's own `KeepableBand` ("high"/"medium"/"low") to the existing `manabase-lens-met`/`manabase-lens-short` marker classes — no new css tokens, no re-derivation of the threshold in the view layer.
- The `/manabase` page renders a `manabase-mulliganlens` card immediately after the tap-analyzer card, as one contiguous flag-guarded block: a keepable-band headline with marker + narrow keep criterion pill, a color/curve line, a keep-size process line (kept-7/to-6/to-5 percentages), and a representative-openers list whose on-curve read names the tracked spell (`TrackedSpellName` + `TrackedOnCurveTurn`) rather than a generic claim, plus a has-a-plan read — closed with a hedge that it is a first-pass consistency signal drawn from the same simulation as the cast rate, never a keep/mulligan recommendation.
- New layout CSS (`.manabase-mulliganlens`, `-split`, `-openers`) lives only in `site-common.css`, reusing existing `.manabase-lens-*` tokens/classes; `:root` count in `site-common.css` is unchanged and no theme `site-*.css` file was touched.
- `ManabaseViewRenderTests` gained an OFF-state no-markup test, an ON-state card-presence test (asserting the tracked-spell on-curve wording renders, not a generic claim), and an excision byte-identity test proving the OFF page equals the ON page with the `manabase-mulliganlens` `<div>` cut out — zero bytes leak when the flag is off.
- `manabase-mulligan.spec.ts` is a desktop-1280 + mobile-390 Playwright live-UX smoke (runs under both `chromium-desktop` and `chromium-mobile` projects) that toggles `analysis.mulligan-eval` via `/Admin/Flags` and asserts the card's visibility follows the flag.

## Task Commits

1. **Task 1: ViewModel ShowMulliganEval + controller analyze mapping + ManabaseDisplay keepable-band helper** - `996870ba` (feat)
2. **Task 2: On-page opening-hand lens card + site-common.css** - `d1b72048` (feat)
3. **Task 3: Page-level flag-OFF byte-identity render test + desktop/mobile Playwright smoke** - `c55b6e3b` (test)

_No plan-metadata commit yet — this SUMMARY.md + STATE.md/ROADMAP.md update is the final commit for this plan._

## Files Created/Modified

- `DeckFlow.Web/Models/ManabaseViewModel.cs` - `ShowMulliganEval { get; init; }` added beside `ShowTapAnalyzer`
- `DeckFlow.Web/Models/ManabaseDisplay.cs` - `KeepableMarker(string keepableBand)` helper mirroring `TapMarker`
- `DeckFlow.Web/Controllers/ManabaseController.cs` - analyze action maps `ShowMulliganEval = result.ShowMulliganEval`
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` - flag-guarded `manabase-mulliganlens` card immediately after the tap-analyzer card
- `DeckFlow.Web/wwwroot/css/site-common.css` - `.manabase-mulliganlens` / `-split` / `-openers` layout classes + a matching `max-width: 640px` collapse, reusing existing tokens
- `DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs` - OFF no-markup test, ON card-presence test, excision byte-identity test; `BuildPopulatedModel` gained an independent `showMulliganEval` parameter; the shared fixture report now also carries a populated `ManabaseMulliganEvaluation`
- `DeckFlow.Web/e2e/manabase-mulligan.spec.ts` (new) - desktop/mobile live-UX smoke toggling the flag via `/Admin/Flags`

## Decisions Made

- `KeepableMarker` reads the aggregator's own `KeepableBand` string rather than re-checking `KeepableHandPercent` against a threshold in the view layer, so the marker glyph and the printed band text can never disagree.
- The excision test holds `ShowTapAnalyzer` at `false` for both renders so the tap card never appears in either page, isolating the differing region to exactly the mulligan-lens block.
- The e2e spec reuses the existing admin-lock + synthetic `CF-Connecting-IP` + `/Admin/Flags` toggle convention from `deck-analysis-render.spec.ts` rather than inventing a second flag-toggle mechanism, since all `/Admin/*` e2e specs share the same SQLite feature-flag store and brute-force throttle and must serialize.

## Deviations from Plan

None - plan executed exactly as written. One incidental fix during Task 2: the first draft of the new CSS comment block used the literal substring `:root` in its prose ("no new :root additions"), which would have broken the plan's own `grep -c ":root"` unchanged-count acceptance check by coincidentally matching as a second occurrence. Reworded to "no new root-scope additions" before committing — a self-correction during drafting, not a deviation from the plan's actual code requirements (verified `grep -c ":root" DeckFlow.Web/wwwroot/css/site-common.css` returns `1`, unchanged from baseline, in the committed version).

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required. `analysis.mulligan-eval` was already seeded OFF in both dialects by Plan 81-02; this plan only adds a consumer of the existing flag.

## Next Phase Readiness

- Phase 81 (Opening-Hand / Mulligan Evaluator) is now fully executed across all 3 plans (81-01 Core instrumentation, 81-02 flag + paste artifact, 81-03 on-page readout).
- Build 0/0 (full solution: Core, CLI, Core.Tests, Studio, Studio.Tests, Web, Web.Tests). Core.Tests 1052/1052 pass. Web.Tests 1150/1162 pass (12 pre-existing Postgres-integration skips, same baseline as 81-01/81-02). Format-gate (`scripts/format-check-changed.sh staged`) clean on all changed lines.
- `manabase-mulligan.spec.ts` was validated with `npx --no-install playwright test --list` (4 tests across chromium-desktop/chromium-mobile, no parse errors) but was NOT executed against a live server in this session (Scryfall-dependent live-UX smoke, per project convention it self-skips when Scryfall is unreachable) — CI is the authoritative gate for this spec, per project convention.
- No blockers. Owed to the operator (same as 79/80): push the branch, verify CI green, and do a live visual smoke (desktop 1280 + mobile 390, flag ON/OFF, across themes) before merge + prod deploy + flipping `analysis.mulligan-eval` ON in prod.

---
*Phase: 81-opening-hand-mulligan-evaluator*
*Completed: 2026-07-03*

## Self-Check: PASSED

All 8 files created/modified confirmed present on disk; all 4 commits (`996870ba`, `d1b72048`, `c55b6e3b`, `4f30d036`) confirmed present in git log.
