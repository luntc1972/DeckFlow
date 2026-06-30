---
phase: 77-multi-axis-deck-score
plan: 05
subsystem: web-ui
tags: [deck-score, multi-axis, razor-view, theme-css, byte-identity, render-test, a11y]

# Dependency graph
requires:
  - phase: 77-02
    provides: DeckMultiAxisScore record + MultiAxisScorer.BandLabel for the pill text + aria-label
  - phase: 77-04
    provides: DeckAnalysisViewModel.Score populated + DeckAnalysisRequest.ScoreJson round-trip field + analysis.multi-axis-score flag (seeded OFF)
provides:
  - Step-3 score block markup in DeckAnalysis.cshtml (four-axis grid + bracket cross-check), gated on Model.Score
  - Hidden ScoreJson form field that carries the serialized score across the Step-3 re-post (rendered only when present)
  - Score layout CSS in site-common.css (responsive 4->2->1 grid, baked-hex band pills, agree/diverge cross-check callout)
  - DeckAnalysisScoreViewTests render guard (OFF byte-identity via excision-equality + ON grid presence)
affects: [77-06 (README + theme/mobile visual sign-off)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Flag-gated view block gated on the server-computed Model.Score (never form-bound) so OFF stays byte-identical"
    - "Round-trip hidden field gated on Request.ScoreJson non-empty so the OFF page emits no extra field"
    - "Baked-hex band pills (own bg+ink) mirror .manabase-health--* so they read on every theme with no per-theme fork"
    - "Render-level Razor test through IRazorViewEngine; antiforgery token neutralized before byte-equality compare"

key-files:
  created:
    - DeckFlow.Web.Tests/DeckAnalysisScoreViewTests.cs
  modified:
    - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css

key-decisions:
  - "Score block gated on Model.Score is not null (server-computed, init-only) so a crafted POST cannot force-render it and the flag-OFF/absent path is byte-identical to baseline"
  - "Hidden ScoreJson textarea gated on !string.IsNullOrEmpty(Model.Request.ScoreJson) and placed beside DeckProfileJson (outside the AnalysisResponse guard) so it is present at Step-2 render — when Score is computed but AnalysisResponse is still null — and survives the Step-3 submit; when absent (OFF path) no field renders, preserving byte-identity"
  - "All score CSS confined to site-common.css; band fills are baked hex with their own legible ink (mirrors .manabase-health--*), so no per-theme fork edit was needed (UI-SPEC §7, Phase-75 tap-analyzer precedent); grep confirms 0 occurrences in site.css (Pitfall 7)"
  - "Cross-check callout borrows var(--success) (agree) / var(--gold-warning) (diverge) left-border and carries a leading glyph + class + text, never color alone (UI-SPEC §9)"
  - "OFF byte-identity proven by prefix+suffix exact-equality around the excised score block after neutralizing the per-render antiforgery token, not merely asserting class-string absence (Codex MED)"

patterns-established:
  - "Pattern: a flag-gated Razor result block whose OFF byte-identity is enforced by an IRazorViewEngine render test that excises the contiguous block and asserts the surrounding markup is unchanged"

metrics:
  duration: ~25 min
  completed: 2026-06-29
---

# Phase 77 Plan 05: Multi-Axis Score Render Summary

Rendered the four-axis deck score (Power/Speed/Control/Consistency) in the `/deck-analysis` Step-3 results panel and added the supporting theme-safe layout CSS, completing the on-page surface for the score that 77-04 already computes and folds into the three prompt artifacts. The score grid sits above the existing per-category Overview/Strengths breakdown, gated on `Model.Score is not null` so the flag-OFF/absent path is byte-identical to baseline. Each axis card encodes its band four redundant ways (numeral, pip meter, word pill, color) and carries a full `aria-label`, so the level is never color-only. A hidden `ScoreJson` field round-trips the serialized score across the Step-3 re-post, rendered only when present. A render-level test proves both the OFF byte-identity (via excision-equality) and the ON grid presence.

## What Was Built

### Task 1 — Score block + hidden ScoreJson field in DeckAnalysis.cshtml (`3ffcbd61`)
- Added `@using DeckFlow.Core.Analysis` for `MultiAxisScorer.BandLabel`.
- Inserted `@if (Model.Score is not null)` between `<h3>Analysis Summary</h3>` and the per-category `<div class="stack">`: a `.chatgpt-score` region with an eyebrow, a `.chatgpt-score-grid` `@foreach` over the four axes emitting `.chatgpt-score-card .chatgpt-score-band--@band` (`role="group"`, `aria-label="@axisLabel score: @band of 5, @MultiAxisScorer.BandLabel(band)"`), each with the axis label, the big numeral, an `aria-hidden` 5-pip meter (`--filled` for `pip <= band`), the band-label pill, and the inline rationale line.
- Cross-check note `.chatgpt-score-crosscheck--@(ScoreAlignsBracket ? "agree" : "diverge")` with `role="note"`, a leading `✓`/`⚠` glyph in the label, and `Model.Score.BracketCrossCheckText`.
- Hidden `<textarea name="ScoreJson" hidden aria-hidden="true" tabindex="-1">@Model.Request.ScoreJson</textarea>` beside `DeckProfileJson`, gated on `!string.IsNullOrEmpty(Model.Request.ScoreJson)`.
- All score-derived values render through Razor `@`-expressions (auto HTML-encode) — no `@Html.Raw` (threat T-77-05-01 mitigated).

### Task 2 — Score CSS in site-common.css (`34b0fc4e`)
- `.chatgpt-score` / `__eyebrow` (mirrors `.manabase-lens-label`), `.chatgpt-score-grid` with the §8 4->2->1 media queries (860px -> 2 cols, 520px -> 1 col), `.chatgpt-score-card` (mirrors `.manabase-lens` soft card, centered), `.chatgpt-score-label`, `.chatgpt-score-value` (mirrors `.manabase-lens-big`), `.chatgpt-score-meter` + `.chatgpt-score-pip`/`--filled`, `.chatgpt-score-band` pill, `.chatgpt-score-band--0..--5` baked-hex bg+ink, `.chatgpt-score-rationale` (`--muted`, `--fs-xs`), and `.chatgpt-score-crosscheck` (mirrors `.bracket-callout`) with `--agree` (`var(--success)`) / `--diverge` (`var(--gold-warning)`) left-borders + a `__label`.
- Confined to site-common.css; `grep -c "chatgpt-score" site.css` == 0 (Pitfall 7). No per-theme fork required — baked-hex pills carry their own contrast (UI-SPEC §7).

### Task 3 — Render guard test (`c7a77778`)
- `DeckAnalysisScoreViewTests` renders the real `DeckAnalysis` view through `IRazorViewEngine` (registers the `_DeckToolTabs` services + `IOptions<AiPlatformOptions>` for `_AiSelector`).
- `ScoreNull_RendersNoScoreMarkup`: Score null + empty ScoreJson -> `DoesNotContain "chatgpt-score"`.
- `ScoreNull_MarkupEqualsScoredMinusScoreBlock`: renders null-Score and non-null-Score with an identical Request, neutralizes the per-render antiforgery token, then asserts the prefix (up to `</h3>`) and suffix (from `<div class="stack">`) are byte-identical and the excised middle is whitespace-only when OFF / contains `chatgpt-score-grid` when scored — proves no surrounding-markup drift (Codex MED).
- `ScorePresent_RendersGridAllAxesAndCrossCheck`: asserts the grid, all four axis labels, and the cross-check note render.

## Verification

- `dotnet.exe build DeckFlow.Web`: 0 warnings, 0 errors.
- `dotnet.exe build DeckFlow.Web.Tests`: 0 warnings, 0 errors.
- `dotnet.exe test --filter DeckAnalysisScoreView`: 3 passed.
- Full `dotnet.exe test DeckFlow.Web.Tests`: 1011 passed, 12 skipped (Postgres integration), 0 failed — the entire existing byte-identity suite green proves the OFF page is unchanged.
- `grep -c "chatgpt-score" site.css` == 0 (Pitfall 7); `grep -c "chatgpt-score-grid|chatgpt-score-band--5|chatgpt-score-crosscheck--diverge" site-common.css` matches all three; both responsive media queries present.
- No compiled `wwwroot/js/*.js` staged or committed.
- Pre-commit format-gate hooks ran on each commit (no `--no-verify`); all green.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Render harness uses DeckPacketController + IOptions<AiPlatformOptions>**
- **Found during:** Task 3 (test build/run)
- **Issue:** The plan's analog (`BracketViewRenderTests`) references `BracketController`, but the `/deck-analysis` view is served by `DeckPacketController` (there is no `DeckController`); the build failed on the missing type. The `DeckAnalysis` view also pulls in `_AiSelector`, which `@inject`s `IOptions<AiPlatformOptions>` (the Bracket view does not), so rendering threw without that registration.
- **Fix:** Used `typeof(DeckPacketController)` for the application part + `ApplicationName`, and registered `Options.Create(new AiPlatformOptions())` in the harness alongside the existing `IToolRegistry` / `IFeatureFlagCache` (mirrors the Bracket harness's partial-service registrations).
- **Files modified:** `DeckFlow.Web.Tests/DeckAnalysisScoreViewTests.cs`
- **Commit:** `c7a77778`

**2. [Rule 1 - Bug] Neutralized the antiforgery token before byte-equality compare**
- **Found during:** Task 3 (excision-equality test initially failed at the CSRF nonce)
- **Issue:** The page form emits a `__RequestVerificationToken` hidden input whose value is randomized per render; a raw prefix byte-equality between two renders failed on the token, not on any score drift.
- **Fix:** Added a `NeutralizeAntiforgery` regex that replaces the token value with a stable placeholder before splitting/comparing, so the test measures only score-block drift.
- **Files modified:** `DeckFlow.Web.Tests/DeckAnalysisScoreViewTests.cs`
- **Commit:** `c7a77778`

## Known Stubs

None. The score is now fully rendered on-page (this plan) and in all three prompt artifacts (77-04). 77-06 covers the README update and the theme/mobile visual sign-off (a verification step, not a stub).

## Threat Flags

None beyond the plan's registered threats. T-77-05-01 (XSS) is mitigated — all score-derived strings and the hidden ScoreJson textarea render through auto-encoding Razor `@`-expressions, no `@Html.Raw`. T-77-05-02 (byte-identity tampering) is mitigated — the block is inside `@if (Model.Score is not null)` and the render test asserts the OFF page omits `chatgpt-score` and carries no surrounding drift.

## Self-Check: PASSED
- FOUND: DeckFlow.Web.Tests/DeckAnalysisScoreViewTests.cs
- FOUND: DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml (chatgpt-score markup)
- FOUND: DeckFlow.Web/wwwroot/css/site-common.css (chatgpt-score-grid)
- FOUND commit 3ffcbd61 (Task 1)
- FOUND commit 34b0fc4e (Task 2)
- FOUND commit c7a77778 (Task 3)
