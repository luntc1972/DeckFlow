---
phase: 107-cut-lab-tech-debt-cleanup
plan: 01
subsystem: api
tags: [cut-lab, dependency-injection, tests, structural-analysis]
requires: []
provides:
  - CutLabPageService constructor trimmed to live dependencies only
  - structural-analysis DI guard re-scoped to manabase baseline, cEDH baseline, and simulation
  - preserved combo/category test coverage through explicit analysisContextBuilder wiring
affects: [cut-lab, dependency-injection, structural-analysis, tests]
tech-stack:
  added: []
  patterns: [explicit analysis builder wiring in tests, three-dependency DI shape guard]
key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/CutLab/CutLabPageService.cs
    - DeckFlow.Web.Tests/CutLabPageServiceTests.cs
    - DeckFlow.Web.Tests/CutLabOriginalEntriesTests.cs
    - .planning/workstreams/cut-lab/phases/107-cut-lab-tech-debt-cleanup/107-01-SUMMARY.md
key-decisions:
  - "Removed only the dead CutLabPageService spellbook/categoryKnowledge fields and left CutLabAnalysisContextBuilder untouched because it still uses both dependencies legitimately."
  - "Converted the fallback-reliant tests to explicit CutLabAnalysisContextBuilder instances instead of deleting arguments, so combo/category coverage stayed live."
  - "Kept HasStructuralAnalysisDependencies and re-scoped it to the three remaining constructor-shape dependencies the plan called out."
patterns-established:
  - "When CutLab tests need combo/category behavior after constructor slimming, pass a named analysisContextBuilder instead of relying on service-side fallback construction."
  - "The CutLabPageService DI guard tracks only the dependencies it directly uses for structural-analysis availability."
requirements-completed: [CLEANUP-1]
duration: 18 min
completed: 2026-07-22
---

# Phase 107 Plan 01 Summary

**CutLabPageService now drops dead spellbook/category DI state while the CutLab tests preserve structural-analysis coverage through explicit analysis-builder wiring.**

## Performance

- **Duration:** 18 min
- **Started:** 2026-07-22T20:10:00Z
- **Completed:** 2026-07-22T20:28:26Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- Removed the unused `_spellbook` and `_categoryKnowledge` fields, constructor parameters, and assignments from `CutLabPageService`.
- Re-scoped `HasStructuralAnalysisDependencies` to the three remaining real dependencies: manabase baseline, cEDH baseline, and simulation service.
- Updated all in-scope test call sites for the slimmer constructor, including explicit `analysisContextBuilder:` wiring in the fallback-reliant cases so combo/category assertions still execute real analysis behavior.

## Task Commits

Implemented as one logical commit:

1. **Task 1 + Task 2 + summary** - single conventional commit covering the service refactor, test rewiring, and this summary.

## Files Created/Modified

- `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` - Removed the dead DI fields/parameters, trimmed fallback builder construction, and re-scoped the DI guard.
- `DeckFlow.Web.Tests/CutLabPageServiceTests.cs` - Updated constructor call sites, preserved fallback-dependent combo/category coverage with explicit builders, and reworked DI-guard coverage to the new three-dependency shape.
- `DeckFlow.Web.Tests/CutLabOriginalEntriesTests.cs` - Updated the shared test service factory to the slimmer constructor signature.
- `.planning/workstreams/cut-lab/phases/107-cut-lab-tech-debt-cleanup/107-01-SUMMARY.md` - Recorded implementation, verification, and self-check results.

## Decisions Made

- Reused the existing explicit `CutLabAnalysisContextBuilder` pattern already present in the test suite for the fallback-reliant cases, rather than introducing a new helper.
- Treated the “analysis registration drops” DI-guard branch as an omitted remaining structural-analysis dependency (`IManabaseBaselineProvider`), because the guard no longer keys off spellbook/category registrations after the refactor.
- Left `CutLabAnalysisContextBuilder.cs` untouched per the scope fence and verified it stayed out of the diff.

## Deviations from Plan

None - plan executed exactly as written within the file fence.

## Issues Encountered

- `dotnet` was not on the WSL shell `PATH`; verification used `"/mnt/c/Program Files/dotnet/dotnet.exe"`.
- An already running local `DeckFlow.Web.exe` from this repo locked `DeckFlow.Core.dll` and blocked the first `dotnet build DeckFlow.sln` attempt. I stopped that local process and reran the required build/test commands successfully.
- A temporary isolated-output build attempt created `artifacts/{build,obj}` directories under several SDK projects, which caused duplicate-attribute compile noise. Those temporary directories were removed before the final successful verification run.

## User Setup Required

None - no external service configuration required.

## Test Results

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` : PASS, 0 warnings, 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln --filter "FullyQualifiedName~CutLab"` : PASS, `363/363` tests passed (`DeckFlow.Core.Tests`: 9, `DeckFlow.Web.Tests`: 354).
- `grep -n "_spellbook\\|_categoryKnowledge" DeckFlow.Web/Services/CutLab/CutLabPageService.cs` : PASS, 0 matches.
- `grep -c "spellbook\\|categoryKnowledge" DeckFlow.Web/Services/CutLab/CutLabPageService.cs` : PASS, 0 matches.
- `grep -c "new CutLabPageService(" DeckFlow.Web.Tests/CutLabPageServiceTests.cs` : PASS, 42 call sites remain.
- `grep -c "new CutLabPageService(" DeckFlow.Web.Tests/CutLabOriginalEntriesTests.cs` : PASS, 1 call site remains.
- `git diff --stat -- DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs` : PASS, no diff.
- `git diff --ignore-all-space --stat` matched `git diff --stat`, confirming no whitespace/EOL churn beyond the intended line edits.

## Next Phase Readiness

- CutLabPageService now exposes only the dependencies it actually owns, so the remaining Phase 107 cleanup plans can build on a smaller service surface.
- Structural-analysis coverage remains intact in tests, with the fallback-sensitive cases now explicit about the analysis builder they require.

## Self-Check: PASSED

---
*Phase: 107-cut-lab-tech-debt-cleanup*
*Completed: 2026-07-22*
