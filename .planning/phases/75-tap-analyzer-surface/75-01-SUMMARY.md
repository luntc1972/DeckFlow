---
phase: 75-tap-analyzer-surface
plan: 01
subsystem: testing
tags: [manabase, tap-analyzer, feature-flag, csharp, xunit, record-types]

# Dependency graph
requires:
  - phase: 70-72 (manabase accuracy / command-zone)
    provides: CastabilitySimulator, ManabaseAnalyzer, ManabaseReport, EffectiveSources, feature-flag plumbing
provides:
  - ManabaseTapAnalysis + ColorTapFinding Core records
  - Additive fields CardCastability.Turn1UntappedTrials, ColorSourceFinding.UntappedSources, ManabaseReport.TapAnalysis
  - ManabaseReportTextBuilder.Build trailing optional tap parameter (signature only)
  - ManabaseDisplay.TapMarker(int) helper + TapAnalyzerGloss const (pure, implemented)
  - ShowTapAnalyzer on ManabaseViewModel and ManabaseAnalysisResult
  - Full Wave 0 RED xUnit suite specifying TAP-01..TAP-04 behavior
affects: [75-02 (computation wiring), 75-03 (Web flag + view), 75-04 (CSS + e2e)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Additive { get; init; } record fields with safe defaults (CarveOutGuard-safe)"
    - "Trailing optional Build parameter (tap=null) for byte-identical flag-off artifact"
    - "Wave 0 RED baseline: type surface + full test suite before any computation"

key-files:
  created:
    - DeckFlow.Core.Tests/Manabase/ManabaseTapAnalysisTests.cs
    - .planning/phases/75-tap-analyzer-surface/75-01-SUMMARY.md
  modified:
    - DeckFlow.Core/Manabase/ManabaseModels.cs
    - DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs
    - DeckFlow.Web/Models/ManabaseDisplay.cs
    - DeckFlow.Web/Models/ManabaseViewModel.cs
    - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs
    - DeckFlow.Web.Tests/Manabase/ManabaseDisplayTests.cs
    - DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs
    - DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs
    - DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs

key-decisions:
  - "D1/D3: Turn-1 availability averages across non-commander castability rows (recorded in test names/comments)"
  - "D4: flat 80% TapMarker threshold, informational only, never affects health"
  - "D5: untapped % denominator is all colored sources via EffectiveSources weighting"
  - "TAP-02 is deck-level, not per-color"

patterns-established:
  - "Wave 0 RED type-surface plan: a compiled-language project needs the referenced types/signatures before tests can fail honestly"
  - "TapMarker implemented now (pure helper) so its test is GREEN at the RED baseline"

requirements-completed: []

# Metrics
duration: ~30min
completed: 2026-06-28
---

# Phase 75 Plan 01: Tap Analyzer Surface (Wave 0 RED Baseline) Summary

**Landed the complete tap-analysis type surface (two new Core records, three additive `{ get; init; }` fields, the `Build` tap parameter, the pure `TapMarker`/`TapAnalyzerGloss` display helper, and `ShowTapAnalyzer` on the result + viewmodel) plus the full xUnit suite specifying TAP-01..TAP-04 — behavior tests RED, pure-helper tests GREEN.**

## Performance

- **Duration:** ~30 min
- **Started:** 2026-06-28T13:50:00Z
- **Completed:** 2026-06-28T14:04:00Z
- **Tasks:** 3
- **Files modified:** 11 (1 created, 10 modified)

## Accomplishments
- Two new sealed records (`ManabaseTapAnalysis`, `ColorTapFinding`) and three additive fields, all `{ get; init; }` with safe defaults — `dotnet build DeckFlow.Core` 0/0, CarveOutGuard unaffected.
- Full solution compiles 0 warnings / 0 errors with the entire tap-analysis contract surface in place.
- `ManabaseDisplay.TapMarker` (flat 80% threshold, D4) + `TapAnalyzerGloss` implemented now; their tests are GREEN at the RED baseline.
- Six test files (1 new + 5 extended) establish the Wave 0 RED contract: behavior tests fail honestly, the four locked design decisions (D1, D3, D4, D5, TAP-02 definition) are encoded in test names/comments for later waves.

## Task Commits

Each task was committed atomically:

1. **Task 1: tap-analysis Core records + additive fields** - `00980cb1` (feat)
2. **Task 2: compile-time contracts (Build param, display helper, ShowTapAnalyzer props)** - `807fe221` (feat)
3. **Task 3: RED test suite (1 new file + 5 extensions)** - `10d52755` (test)

_Note: this is a `type: tdd` plan whose RED gate spans the whole plan; the GREEN/REFACTOR gates land in plans 75-02/75-03. See TDD Gate Compliance below._

## Files Created/Modified
- `DeckFlow.Core/Manabase/ManabaseModels.cs` - Added `ManabaseTapAnalysis` + `ColorTapFinding` records; additive `CardCastability.Turn1UntappedTrials`, `ColorSourceFinding.UntappedSources`, `ManabaseReport.TapAnalysis`.
- `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs` - `Build` gains trailing optional `ManabaseTapAnalysis? tap = null` (body unchanged; block append deferred to 75-02).
- `DeckFlow.Web/Models/ManabaseDisplay.cs` - `TapMarker(int)` helper + `TapAnalyzerGloss` const (both implemented).
- `DeckFlow.Web/Models/ManabaseViewModel.cs` - `ShowTapAnalyzer` bool.
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` - `ManabaseAnalysisResult.ShowTapAnalyzer` additive init property (flag-read wiring deferred to 75-03).
- `DeckFlow.Core.Tests/Manabase/ManabaseTapAnalysisTests.cs` (new) - 7 TAP-01/TAP-02 cases via `Analyze` on synthetic decks (RED).
- `DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs` - byte-identity (GREEN) + content/single-color-omit facts (RED).
- `DeckFlow.Web.Tests/Manabase/ManabaseDisplayTests.cs` - `TapMarker` theory + gloss fact (GREEN).
- `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` - `analysis.manabase.tap-analyzer` InlineData (RED until flag registered).
- `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` - `analysis.manabase.tap-analyzer` seed-OFF InlineData (RED until flag seeded).
- `DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs` - `StubService` `showTapAnalyzer` param + `ReportWithTapAnalysis()` helper + flag on/off download facts.

## Decisions Made
- None beyond the plan's locked decisions (D1/D3/D4/D5, TAP-02 definition), which were encoded into the new Core test names and comments so later waves implement them as specified.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Restored DeckFlow.Web node dependencies (`npm ci`)**
- **Found during:** Task 2 (full-solution build)
- **Issue:** The worktree had no `DeckFlow.Web/node_modules`, so the MSBuild `CompileTypeScriptAssets` target failed (`Cannot find module .../typescript/bin/tsc`), blocking `dotnet build DeckFlow.sln`.
- **Fix:** Ran `npm ci` in `DeckFlow.Web/` (lockfile-respecting restore of an already-pinned in-solution dependency — not a new package; `package-lock.json` was verified untouched). This is restoration of existing deps per CLAUDE.md ("npm install once in DeckFlow.Web/"), not a package addition.
- **Files modified:** none committed (`node_modules` is gitignored; lockfile unchanged).
- **Verification:** `dotnet build DeckFlow.sln` 0 warnings / 0 errors afterward.
- **Committed in:** n/a (no tracked file changed).

**2. [Rule 1 - Bug] Disambiguated `Analyze` cref to silence CS0419**
- **Found during:** Task 3 (test-suite build)
- **Issue:** `<see cref="ManabaseAnalyzer.Analyze"/>` produced a new CS0419 ambiguous-cref warning (two overloads), violating the no-new-warnings gate.
- **Fix:** Changed the cref to `ManabaseAnalyzer.Analyze(ManabaseDeck)`.
- **Files modified:** `DeckFlow.Core.Tests/Manabase/ManabaseTapAnalysisTests.cs`.
- **Verification:** `dotnet build DeckFlow.Core.Tests` 0 warnings.
- **Committed in:** `10d52755` (Task 3 commit).

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug/warning).
**Impact on plan:** Both necessary to satisfy the build/no-new-warnings Definition of Done. No scope creep; no production behavior added.

## Issues Encountered
- WSL has no `dotnet`; builds and tests ran via the Windows `dotnet.exe` per CLAUDE.md. `npm.cmd` could not be invoked directly from WSL bash; used `cmd.exe /c "npm ci"`.

## TDD Gate Compliance

This is a `type: tdd` plan deliberately structured as the Wave 0 RED baseline. The RED gate is satisfied; the GREEN/REFACTOR gates are owned by later plans (75-02 computation, 75-03 Web wiring), per the plan's explicit RED contract.

Verified test states (via Windows `dotnet test`, `--no-build`):
- `ManabaseTapAnalysisTests` — 7/7 **RED** (TapAnalysis null until 75-02). Expected.
- `ManabaseReportTextBuilderTests` — 20 pass incl. byte-identity (GREEN); 2 **RED** (content + single-color-omit, need 75-02 block). Expected.
- `ManabaseDisplayTests` — TapMarker theory + gloss **GREEN** (pure helper implemented here).
- `FeatureFlagCatalogTests` / `FeatureFlagStoreSeedTests` — tap-analyzer cases **RED** (flag registered/seeded in 75-03). Expected.
- `ManabaseControllerDownloadTests` — flag-OFF **GREEN** (byte-identity); flag-ON **RED** (needs 75-02 block + 75-03 controller wiring). Expected.
- `CarveOutGuardTests` — **GREEN** (no get-only properties introduced).

No `feat`/`refactor` GREEN-gate commit is expected in this plan; the RED `test(...)` commit (`10d52755`) plus the two `feat(...)` contract-surface commits establish the baseline. This is intentional, not a gate violation.

## Next Phase Readiness
- 75-02 can now implement `ComputeTapAnalysis` in `ManabaseAnalyzer` + the `Turn1UntappedTrials` counter in `CastabilitySimulator` + the `AppendTapAnalysisBlock` in the text builder to turn the Core/text RED tests GREEN.
- 75-03 registers/seeds the `analysis.manabase.tap-analyzer` flag, reads it in `ManabaseAnalysisService`, wires `ShowTapAnalyzer` through the controller, and passes `tap` to `Download` — turning the flag/download RED tests GREEN.
- No blockers. `node_modules` must remain restored in the worktree for full-solution builds.

---
*Phase: 75-tap-analyzer-surface*
*Completed: 2026-06-28*

## Self-Check: PASSED

All created files present; all three task commits (00980cb1, 807fe221, 10d52755) exist in history.
