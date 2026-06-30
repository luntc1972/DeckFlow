---
phase: 75-tap-analyzer-surface
plan: 03
subsystem: manabase
tags: [manabase, tap-analyzer, feature-flag, csharp, xunit, tdd-green]

# Dependency graph
requires:
  - phase: 75-01
    provides: ShowTapAnalyzer on ManabaseAnalysisResult + ManabaseViewModel, Build tap parameter, RED flag/download suite
  - phase: 75-02
    provides: ManabaseReport.TapAnalysis populated + "Untapped Sources:" paste-artifact block (tap non-null)
provides:
  - analysis.manabase.tap-analyzer flag registered in FeatureFlagCatalog + seeded OFF in both dialects (idempotent)
  - TapAnalyzerFlagKey const + fail-safe-OFF flag read + ShowTapAnalyzer propagation in ManabaseAnalysisService
  - Page ViewModel ShowTapAnalyzer wiring + download tap-gate (tap == null when OFF) in ManabaseController
  - Flag-read fail-safe + viewmodel-default test coverage
affects: [75-04 (page UI card + CSS + e2e)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "New (non-renamed) flag: catalog description + both-dialect seed OFF, NO RenamedFlagKeys entry"
    - "Capability shipped dark: bool gate (ShowTapAnalyzer ? Report.TapAnalysis : null) keeps OFF byte-identical"
    - "Wave 2 GREEN: turn the Wave 0 flag/download RED tests green by wiring the Web layer"

key-files:
  created:
    - .planning/phases/75-tap-analyzer-surface/75-03-SUMMARY.md
  modified:
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
    - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs
    - DeckFlow.Web/Controllers/ManabaseController.cs
    - DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs
    - DeckFlow.Web.Tests/Manabase/ManabaseViewModelTests.cs
    - README.md

key-decisions:
  - "TAP-04 left Pending: the artifact byte-identity half is done here; the page byte-identity half lands with the guarded card in 75-04, so the requirement is not yet fully satisfied"
  - "Flag read via IsFlagOn (Snapshot().TryGetValue → false on missing key), never IFeatureFlagCache.IsEnabled (defaults missing keys ON)"
  - "Download gate uses the bool (ShowTapAnalyzer ? ... : null), not a bare Report.TapAnalysis, so OFF sends null and the .txt stays byte-identical"

patterns-established:
  - "Brand-new flags get a catalog entry + both-dialect seed but NO RenamedFlagKeys mapping"

requirements-completed: []

# Metrics
duration: ~20min
completed: 2026-06-28
---

# Phase 75 Plan 03: Tap Analyzer Surface (Wave 2 — Web Flag Wiring) Summary

**Registered and seeded the `analysis.manabase.tap-analyzer` flag OFF in both dialects, then threaded it fail-safe-OFF through the service → result → page ViewModel → download path so the tap metric surfaces only when an operator enables it — turning the Wave 0 flag-catalog/seed and `Download_FlagOn` RED tests GREEN while keeping the OFF artifact byte-identical (TAP-04 artifact half).**

## Performance

- **Duration:** ~20 min
- **Tasks:** 3
- **Files modified:** 7 (1 created, 6 modified)

## Accomplishments
- **Flag registered + seeded OFF (idempotent, both dialects):** catalog description grouped with the manabase family (after `commander-castability`, before the different-family `command-zone-awareness`); `('analysis.manabase.tap-analyzer', FALSE)`/`(..., 0)` inserted into Postgres/SQLite seed SQL ahead of `ON CONFLICT (key) DO NOTHING`; no `RenamedFlagKeys` entry (brand-new key). README documents it OFF-by-default / byte-identical-when-off.
- **Service reads fail-safe OFF + propagates:** `TapAnalyzerFlagKey` const + `bool showTapAnalyzer = IsFlagOn(TapAnalyzerFlagKey)` in `AnalyzeAsync`, assigned as `ShowTapAnalyzer = showTapAnalyzer` on the `ManabaseAnalysisResult` initializer.
- **Controller gate:** page ViewModel gets `ShowTapAnalyzer = result.ShowTapAnalyzer`; `Download` passes `tap: result.ShowTapAnalyzer ? result.Report.TapAnalysis : null` so OFF passes null → zero appended bytes.
- **Test coverage (not source-only):** 3 service facts (absent/no-cache → false, explicit false → false, true → true) via the `FakeFeatureFlagCache` pattern + a `GetResultShowTapAnalyzer` reflection helper; 2 viewmodel facts (default-false + round-trip-true).
- `dotnet build DeckFlow.sln` 0 warnings / 0 errors; FeatureFlag (47), Service+ViewModel (33), and ControllerDownload (7) suites all green.

## Task Commits

Each task committed atomically:

1. **Task 1: register + seed flag OFF (both dialects) + README** - `cea746d6` (feat)
2. **Task 2: service reads flag fail-safe OFF + carries ShowTapAnalyzer (+ tests)** - `89959258` (feat)
3. **Task 3: controller page ViewModel + download tap-gate** - `272fdc86` (feat)

## Files Created/Modified
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` - manabase-family tap-analyzer description.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` - seed OFF row in PostgresSeedSql + SqliteSeedSql.
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` - `TapAnalyzerFlagKey` const, `IsFlagOn` read, `ShowTapAnalyzer` on the result initializer.
- `DeckFlow.Web/Controllers/ManabaseController.cs` - page ViewModel `ShowTapAnalyzer` + download tap-gate.
- `DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs` - `GetResultShowTapAnalyzer` helper + 3 flag-read facts.
- `DeckFlow.Web.Tests/Manabase/ManabaseViewModelTests.cs` - ShowTapAnalyzer default-false + round-trip facts.
- `README.md` - tap-analyzer flag note (OFF by default, byte-identical when off).

## Decisions Made
- **TAP-04 not marked complete.** The plan's own success criteria scope TAP-04 to "byte-identical artifact (and page, once 75-04 lands the guarded card)." This plan delivers the artifact half (download tap-gate) and the flag/service/result/ViewModel plumbing; the page card guard lands in 75-04. REQUIREMENTS.md TAP-04 therefore stays Pending to remain accurate.
- **Flag read via `IsFlagOn`** (fail-safe OFF) per the threat register (T-75-03-E) — never the default-ON `IsEnabled` path.

## Deviations from Plan

None - plan executed exactly as written. No auto-fixes were needed; all three task verifications passed on first run, and the full solution built 0/0.

## Threat Surface

No new threat surface beyond the plan's `<threat_model>`. The flag toggles ride the existing admin-gated /Admin flag surface; no new endpoint, auth path, or input crosses to end users. All three registered threats (T-75-03-I artifact byte-identity, T-75-03-T re-bootstrap overwrite, T-75-03-E default-ON confusion) are mitigated and test-covered (Download_FlagOff byte-clean; `ON CONFLICT DO NOTHING` seed; `IsFlagOn` fail-safe-OFF facts).

## Out of Scope (per plan)
- 75-04 page UI card (`Manabase.cshtml` `@if (Model.ShowTapAnalyzer ...)` block), `site-common.css` `.manabase-taplens` rules, and e2e/visual verification. Not implemented here by design.

## Test States (via Windows dotnet test)
- `FeatureFlagCatalogTests` / `FeatureFlagStoreSeedTests` — tap-analyzer cases **GREEN** (registered + seeds OFF both dialects). 47/47 FeatureFlag.
- `ManabaseAnalysisServiceTests` flag-read (absent/false/true) + `ManabaseViewModelTests` (default/round-trip) — **GREEN**. 33/33.
- `ManabaseControllerDownloadTests` — flag-OFF byte-clean + flag-ON has "Untapped Sources:" / "Turn-1 untapped availability:" — **GREEN**. 7/7.
- `dotnet build DeckFlow.sln` — 0 warnings / 0 errors.

## Next Phase Readiness
- 75-04 adds the guarded view card + `.manabase-taplens` CSS + e2e/visual verification, completing the page byte-identity half and closing TAP-04.
- No blockers. `node_modules` remains restored in the worktree for full-solution builds.

---
*Phase: 75-tap-analyzer-surface*
*Completed: 2026-06-28*

## Self-Check: PASSED

- Created file present: `.planning/phases/75-tap-analyzer-surface/75-03-SUMMARY.md`.
- All three task commits exist in history: `cea746d6`, `89959258`, `272fdc86`.
- Modified source files present; full solution builds 0/0; flag/service/viewmodel/download suites green.
