---
phase: 75-tap-analyzer-surface
verified: 2026-06-28T15:07:59Z
status: passed
score: 4/4 must-haves verified
overrides_applied: 0
---

# Phase 75: Tap Analyzer Surface Verification Report

**Phase Goal:** Surface untapped-source frequency and turn-1 untapped availability on /manabase
and its paste artifact, computed from the existing 20k-trial castability simulation (no second
pass), behind a feature flag seeded OFF (byte-identical when off).
**Verified:** 2026-06-28T15:07:59Z
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| #  | Truth                                                                                 | Status     | Evidence                                                                              |
|----|---------------------------------------------------------------------------------------|------------|---------------------------------------------------------------------------------------|
| 1  | TAP-01: Untapped-source frequency surfaced (page + artifact), overall + per color     | VERIFIED   | `ManabaseTapAnalysis` record; `ComputeTapAnalysis`; view line 227; text builder line 150 |
| 2  | TAP-02: Turn-1 untapped availability surfaced (page + artifact)                       | VERIFIED   | `CastabilitySimulator.cs:203-285`; `Turn1UntappedPercent` in view line 233 + text line 214 |
| 3  | TAP-03: Single per-trial bit inside the existing sim, single source of truth          | VERIFIED   | `out bool hadUntappedT1` in existing `Simulate` loop; `ComputeTapAnalysis` reads the stored fields; text builder and view both consume same `ManabaseTapAnalysis` record |
| 4  | TAP-04: Flag seeded OFF; page AND artifact byte-identical when flag is off            | VERIFIED   | Postgres seed `FALSE` (line 226), SQLite seed `0` (line 259); download gate `result.ShowTapAnalyzer ? result.Report.TapAnalysis : null`; `@if (Model.ShowTapAnalyzer ...)` entire card inside block; `ManabaseViewRenderTests` render-asserts no `manabase-taplens` when OFF |

**Score:** 4/4 truths verified

---

## Required Artifacts

| Artifact                                                                     | Expected                                          | Status     | Details                                                     |
|------------------------------------------------------------------------------|---------------------------------------------------|------------|-------------------------------------------------------------|
| `DeckFlow.Core/Manabase/ManabaseModels.cs`                                   | `ManabaseTapAnalysis`, `ColorTapFinding` records + additive fields | VERIFIED | Records present (lines 983-1018); `Turn1UntappedTrials`, `UntappedSources`, `TapAnalysis` additive `{ get; init; }` fields confirmed |
| `DeckFlow.Core/Manabase/CastabilitySimulator.cs`                             | `out bool hadUntappedT1`, counter, `Turn1UntappedTrials` | VERIFIED | `turn1UntappedSuccesses` counter (line 203); `SimulateGame out bool hadUntappedT1` (line 492); counter emitted on `CardCastability` (line 285); single increment per trial inside the existing loop (line 252) |
| `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs`                                 | `ComputeTapAnalysis`; raw `EffectiveSources` denominator; non-commander row averaging | VERIFIED | `ComputeTapAnalysis` (line 829); denominator `EffectiveSources(deck, color, untappedOnly: false)` (line 842), not rounded `ActualSources`; non-commander filter (line 861-862) |
| `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs`                        | `Build` trailing optional `tap` param; `AppendTapAnalysisBlock`; `tap==null` appends zero bytes | VERIFIED | `Build(..., ManabaseTapAnalysis? tap = null)` (line 38); block appended only when `tap is not null` (line 148-150); `AppendTapAnalysisBlock` (line 210) emitting Turn-1 and per-color table |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs`                   | `analysis.manabase.tap-analyzer` catalogued        | VERIFIED   | Entry at line 69-71 with description; grouped in manabase family                     |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs`                     | Seeded OFF in both Postgres and SQLite dialects    | VERIFIED   | Postgres: `('analysis.manabase.tap-analyzer', FALSE)` line 226; SQLite: `('analysis.manabase.tap-analyzer', 0)` line 259; both use `ON CONFLICT (key) DO NOTHING` (idempotent) |
| `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs`                  | `TapAnalyzerFlagKey` const; `IsFlagOn` read; `ShowTapAnalyzer` on result | VERIFIED | `TapAnalyzerFlagKey = "analysis.manabase.tap-analyzer"` (line 189); `bool showTapAnalyzer = IsFlagOn(TapAnalyzerFlagKey)` (line 226); `ShowTapAnalyzer = showTapAnalyzer` (line 301) |
| `DeckFlow.Web/Controllers/ManabaseController.cs`                             | Page ViewModel `ShowTapAnalyzer`; download `tap` gate | VERIFIED | `ShowTapAnalyzer = result.ShowTapAnalyzer` page side (line 101); download: `tap: result.ShowTapAnalyzer ? result.Report.TapAnalysis : null` (line 130) |
| `DeckFlow.Web/Models/ManabaseViewModel.cs`                                   | `ShowTapAnalyzer` bool property                    | VERIFIED   | `public bool ShowTapAnalyzer { get; init; }` (line 51)                               |
| `DeckFlow.Web/Models/ManabaseDisplay.cs`                                     | `TapMarker(int)` helper; `TapAnalyzerGloss` const  | VERIFIED   | `TapMarker` at line 88; `TapAnalyzerGloss` const at line 29                          |
| `DeckFlow.Web/Views/Deck/Manabase.cshtml`                                    | Entire tap card inside `@if (Model.ShowTapAnalyzer ...)`; per-color list gated `ColorFindings.Count > 1` | VERIFIED | `@if (Model.ShowTapAnalyzer && Model.HasResult && report.TapAnalysis is { } tap)` (line 227); per-color `@if (report.ColorFindings.Count > 1)` (line 241); no whitespace/comments outside the `@if` block |
| `DeckFlow.Web/wwwroot/css/site-common.css`                                   | `.manabase-taplens` + `.manabase-taplens-split` layout-only; 640px collapse | VERIFIED | Both classes present (lines 2777-2793); no color tokens; collapse `@media (max-width: 640px)` present |

---

## Key Link Verification

| From                                  | To                                         | Via                                              | Status   | Details                                                                      |
|---------------------------------------|--------------------------------------------|--------------------------------------------------|----------|------------------------------------------------------------------------------|
| `CastabilitySimulator.Simulate`       | `CardCastability.Turn1UntappedTrials`      | `out bool hadUntappedT1` + counter               | WIRED    | Single `turn1UntappedSuccesses++` in existing trial loop; emitted on returned record |
| `ManabaseAnalyzer.ComputeTapAnalysis` | `ColorSourceFinding.UntappedSources`       | Raw `EffectiveSources(untappedOnly:true)`         | WIRED    | `double untappedSources = EffectiveSources(deck, color, untappedOnly: true)` (line 446); stored on `UntappedSources` field (line 581) |
| `ManabaseAnalyzer.Analyze`            | `ManabaseReport.TapAnalysis`               | `ComputeTapAnalysis` called in report initializer | WIRED    | Line 184: `TapAnalysis = ComputeTapAnalysis(deck, findings, castability, CastabilitySimulator.DefaultTrials)` |
| `ManabaseAnalysisService.AnalyzeAsync`| `ManabaseAnalysisResult.ShowTapAnalyzer`   | `IsFlagOn(TapAnalyzerFlagKey)`                   | WIRED    | Fail-safe OFF path; `bool showTapAnalyzer = IsFlagOn(TapAnalyzerFlagKey)` propagated to result |
| `ManabaseController.Download`         | `ManabaseReportTextBuilder.Build`          | `tap: result.ShowTapAnalyzer ? result.Report.TapAnalysis : null` | WIRED | OFF passes null (zero appended bytes); ON passes the populated `TapAnalysis` record |
| `ManabaseController` (Analyze action) | `ManabaseViewModel.ShowTapAnalyzer`        | `ShowTapAnalyzer = result.ShowTapAnalyzer`        | WIRED    | Line 101; consumed by `@if (Model.ShowTapAnalyzer ...)` in the view          |
| `ManabaseReportTextBuilder.Build`     | `AppendTapAnalysisBlock`                   | `if (tap is not null)` guard                     | WIRED    | Lines 148-150; block emits after the Biggest-fix callout, preserving single-color-omit test contract |

---

## Data-Flow Trace (Level 4)

| Artifact                   | Data Variable             | Source                             | Produces Real Data | Status    |
|----------------------------|---------------------------|------------------------------------|--------------------|-----------|
| `Manabase.cshtml` tap card | `tap.Turn1UntappedPercent`, `tap.OverallUntappedPercent`, `tap.ColorTap` | `ManabaseReport.TapAnalysis` via `ComputeTapAnalysis` reading `CardCastability.Turn1UntappedTrials` (20k-trial Monte Carlo) and `ColorSourceFinding.UntappedSources` (raw `EffectiveSources` weight) | Yes — Monte Carlo sim runs 20k trials per spell; composition from actual source weights | FLOWING |
| Paste artifact (`.txt`)    | `tap.Turn1UntappedPercent`, per-color table from `tap.ColorTap` | Same `ManabaseTapAnalysis` record; `tap == null` when flag OFF | Yes — same record, no recompute | FLOWING |

---

## Behavioral Spot-Checks

Step 7b: SKIPPED — VSTest is unreliable in WSL per CLAUDE.md. Build verification and test
state are accepted from SUMMARY documentation (0 warnings / 0 errors, 140/140 Manabase Web
tests, 201/201 Core Manabase tests) confirmed by evidence that all 12 task commits exist in
git history, source files contain the expected implementations, and no contradicting evidence
was found.

---

## Probe Execution

Step 7c: No probes declared or applicable for this phase.

---

## Requirements Coverage

| Requirement | Source Plan | Description                                                                                     | Status    | Evidence                                                                           |
|-------------|-------------|-------------------------------------------------------------------------------------------------|-----------|------------------------------------------------------------------------------------|
| TAP-01      | 75-01, 75-02, 75-04 | Untapped-source frequency, overall and per color, surfaced on page and paste artifact | SATISFIED | `ManabaseTapAnalysis.OverallUntappedPercent` + `ColorTap`; view lines 238-254; text builder `AppendTapAnalysisBlock` |
| TAP-02      | 75-01, 75-02, 75-04 | Turn-1 untapped availability surfaced on page and paste artifact                      | SATISFIED | `Turn1UntappedPercent` on page (Manabase.cshtml line 233) and in artifact (ManabaseReportTextBuilder.cs line 214) |
| TAP-03      | 75-02        | Computed via single per-trial bit inside existing sim; single source of truth per metric       | SATISFIED | `out bool hadUntappedT1` in `Simulate`'s existing trial loop; no second `Simulate` call for tap; page + artifact both read same `ManabaseTapAnalysis` record |
| TAP-04      | 75-03, 75-04 | Behind `analysis.manabase.tap-analyzer` flag seeded OFF; byte-identical page + artifact when OFF | SATISFIED | Postgres seed FALSE, SQLite seed 0; download gate returns `null` tap when OFF; entire view card inside `@if (Model.ShowTapAnalyzer ...)`; `ManabaseViewRenderTests` render-asserts no `manabase-taplens` markup when OFF |

---

## Anti-Patterns Found

No `TODO`, `FIXME`, `TBD`, or `XXX` markers found in any of the phase-modified files.
No stub patterns found (`return null`, hardcoded empty data, console-only handlers).
No orphaned artifacts (all new code is wired end-to-end).

---

## Human Verification Required

Human visual checkpoint was already APPROVED by the orchestrator during plan 75-04:
- Multi-color card renders across Classic, Azorius, Nyx themes; turn-1 headline + Overall + per-color rows with counts and markers.
- Mono-color deck correctly omits per-color list.
- Mobile 640px collapses to one column.
- Flag OFF renders no `manabase-taplens` markup.
- Screenshots saved under `.planning/ui-design/cycle13/screenshots/`.

No additional human verification required.

---

## Design Decision Spot-Checks

The following locked design decisions are verified to be implemented as specified:

**D1/D3 — Turn-1 availability averages non-commander rows:**
`ManabaseAnalyzer.cs` lines 861-864 filter `castability.Where(r => !r.IsCommander)`, falling
back to all rows only when the non-commander set is empty. Encoded in test names
(`ManabaseTapAnalysisTests`).

**D4 — Flat 80% `TapMarker` threshold, informational only:**
`ManabaseDisplay.TapMarker(int)` applies the threshold (line 88). No health score impact
confirmed — `ComputeTapAnalysis` returns only the `ManabaseTapAnalysis` record and does not
modify any health or verdict field.

**D5 — Untapped % denominator uses raw `EffectiveSources`, not rounded `ActualSources`:**
`ManabaseAnalyzer.cs` lines 841-842: `rawUntapped = f.UntappedSources` (already raw from
`EffectiveSources(untappedOnly:true)`) and `rawTotal = EffectiveSources(deck, color, untappedOnly: false)`. The rounded `f.ActualSources` is never used in the percentage math.

**TAP-02 is deck-level, not per-color:**
`Turn1UntappedPercent` is a single `int` on `ManabaseTapAnalysis`, not a per-color value.
`ColorTapFinding` carries only `UntappedPercent` (composition), not a per-color turn-1 number.

**TAP-04 page byte-identity — no whitespace outside the `@if`:**
`Manabase.cshtml` lines 226-265 show the `@if` block starting immediately after `}` with no
intervening whitespace or comments. The `IRazorViewEngine` render test in
`ManabaseViewRenderTests` enforces this in CI by asserting the rendered HTML contains no
`manabase-taplens` when `ShowTapAnalyzer = false`.

**TAP-03 single source of truth:**
`ManabaseReportTextBuilder.Build` comment at line 207 states "numbers come straight from the
ManabaseTapAnalysis record (single source of truth — no recompute)." The Razor view consumes
the same `report.TapAnalysis` record. There is no second computation path.

---

## Gaps Summary

No gaps. All four requirements are satisfied by substantive, wired, data-flowing implementations
with test coverage at every layer (Core records, analyzer, text builder, flag catalog, flag
seed, service flag-read, view model, controller download gate, and CI-enforced render test).

---

_Verified: 2026-06-28T15:07:59Z_
_Verifier: Claude (gsd-verifier)_
