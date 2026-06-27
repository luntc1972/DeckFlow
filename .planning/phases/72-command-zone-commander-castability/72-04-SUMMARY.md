---
phase: 72-command-zone-commander-castability
plan: 04
subsystem: testing
tags: [csharp, manabase, commander, companion, castability, prompts]

requires:
  - phase: 72-command-zone-commander-castability
    provides: plan 72-04 requirements and command-zone core contracts
provides:
  - deterministic worst-of commander headline selection for Central importance
  - SimulateCompanion helper using library size minus commanders and caller-preapplied plus-three tax
  - opt-in command-zone castability block in swap prompts with default-off byte identity
  - regression locks for partner/background commander counts, companion exclusion, and max-commander threshold
affects: [phase-72, phase-73, manabase-analysis, swap-prompts]

tech-stack:
  added: []
  patterns:
    - test-first updates for analyzer, classifier, simulator, and prompt-builder surfaces
    - default-off prompt extension guarded by byte-identity regression coverage

key-files:
  created:
    - DeckFlow.Core.Tests/Manabase/CastabilitySimulatorTests.cs
  modified:
    - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs
    - DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerTests.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseClassifierTests.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseSwapPromptBuilderTests.cs

key-decisions:
  - "Used MinBy over commander rows for Central headline selection so multi-commander decks surface the worst cast rate deterministically."
  - "Modeled companion simulation as a separate helper that reuses the deck library minus commanders and trusts the caller to pre-apply the plus-three heuristic."
  - "Kept swap-prompt command-zone output fully opt-in so existing prompt bytes remain unchanged unless the caller enables it."

patterns-established:
  - "Command-zone helpers stay additive: do not inject companion cards into deck.Spells or deck.Sources."
  - "Prompt-surface expansions default off and require byte-identity regression tests."

requirements-completed: [D-01, C-01, C-02, A/B-01, A/B-02, B-03, F-pin]

duration: 31min
completed: 2026-06-27
---

# Phase 72: Command-Zone Commander Castability Summary

**Deterministic worst-of commander headlines, companion castability simulation outside the 99, and an opt-in command-zone prompt block with default-off byte identity**

## Performance

- **Duration:** 31 min
- **Started:** 2026-06-27T08:38:00-06:00
- **Completed:** 2026-06-27T09:09:16-06:00
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments
- Central-importance headline selection now picks the lowest-percent commander row instead of the first commander row.
- Added `ManabaseAnalyzer.SimulateCompanion(...)` and locked the companion, partner/background, and threshold behaviors with Core tests.
- Extended `ManabaseSwapPromptBuilder.Build(...)` with default-off command-zone output and byte-identity coverage for the unchanged path.

## Task Commits

Each task was committed atomically:

1. **Tasks 1-3: worst-of headline, SimulateCompanion, and command-zone prompt opt-in** - `57c438b` (feat)
2. **Plan 72-04 summary** - recorded in the follow-up docs commit for this file

## Files Created/Modified
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` - Added `SimulateCompanion` and changed Central headline selection to worst-of commander castability.
- `DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs` - Added optional command-zone output parameters and rendering block.
- `DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerTests.cs` - Added deterministic Central headline regression coverage.
- `DeckFlow.Core.Tests/Manabase/CastabilitySimulatorTests.cs` - Added companion simulation regression coverage.
- `DeckFlow.Core.Tests/Manabase/ManabaseClassifierTests.cs` - Added commander-count and threshold regression locks.
- `DeckFlow.Core.Tests/Manabase/ManabaseSwapPromptBuilderTests.cs` - Added byte-identity and command-zone prompt coverage.
- `.planning/phases/72-command-zone-commander-castability/72-04-SUMMARY.md` - Execution summary for plan 72-04.

## Decisions Made
- Used reflection in the analyzer test to pin the private headline-selection behavior directly, keeping the regression narrowly focused.
- Kept companion prompt text concise and ASCII-only while explicitly disclosing the plus-three heuristic.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- The plan referenced `DeckFlow.Core.Tests/Manabase/CastabilitySimulatorTests.cs`, but that file did not exist in the worktree. It was created inside the allowed fence and matched the required test filter.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Core command-zone math and prompt wiring points are in place for the downstream Phase 72/73 web-service integration.
- Verification is clean on the required targeted test slices and on a fresh `DeckFlow.Core` build.

---
*Phase: 72-command-zone-commander-castability*
*Completed: 2026-06-27*
