---
phase: 72-command-zone-commander-castability
plan: 07
subsystem: testing
tags: [deck-analysis, companion, moxfield, feature-flags, docs]
requires:
  - phase: 72-command-zone-commander-castability
    provides: command-zone companion metadata ground truth and packet-service seams from plans 72-01 through 72-05
provides:
  - companion-inert byte-identity regression coverage for DeckAnalysisPacketService
  - Phase 72 manabase docs covering the command-zone castability callout and companion heuristic
  - explicit Moxfield-only companion auto-detect documentation
affects: [phase-73, manabase, deck-analysis, docs]
tech-stack:
  added: []
  patterns:
    - byte-stable packet comparison for deck-analysis regression tests
    - Moxfield direct API as the only companion auto-detect source in user docs
key-files:
  created:
    - .planning/phases/72-command-zone-commander-castability/72-07-SUMMARY.md
  modified:
    - DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs
    - README.md
    - DeckFlow.Web/Help/manabase.md
key-decisions:
  - "Kept DeckAnalysisPacketService production code unchanged and proved inertness through importer metadata and flag-toggle tests only."
  - "Documented companion auto-detection as Moxfield direct API only; Archidekt companion category detection was removed from the docs per fixture ground truth."
patterns-established:
  - "Packet inertness guard: compare UTF-8 bytes of the built packet text across metadata-only variants."
  - "Doc corrections must prefer fixture notes over stale plan wording when they conflict."
requirements-completed: [F-02, F-03]
duration: 16min
completed: 2026-06-27
---

# Phase 72: Command Zone Commander Castability Summary

**Deck-analysis packet regressions now prove companion side-metadata and the commander-castability flag are inert, while the manabase docs describe the new command-zone callout and the Moxfield-only companion detection path**

## Performance

- **Duration:** 16 min
- **Started:** 2026-06-27T15:35:00Z
- **Completed:** 2026-06-27T15:50:50Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Added a no-leak regression test proving a detected Moxfield companion does not enter deck-analysis packet content and matches the non-companion baseline byte-for-byte.
- Added a flag-invariance regression test proving `manabase.commander-castability` OFF and ON yield identical deck-analysis packet bytes for the same companion plus Background deck.
- Updated README and mana base help text to describe the command-zone callout, manual companion designator fallback, and the companion +3 generic heuristic.

## Task Commits

Each task was committed atomically:

1. **Task 1-2: packet regression tests and Phase 72 docs** - `4e6bfa47` (test)

**Plan metadata:** pending summary commit

## Files Created/Modified
- `.planning/phases/72-command-zone-commander-castability/72-07-SUMMARY.md` - plan execution summary with the doc deviation recorded
- `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs` - companion inertness and flag invariance regression coverage plus test seams
- `README.md` - Phase 72 mana base "What's new" entry
- `DeckFlow.Web/Help/manabase.md` - command-zone callout and companion heuristic help section

## Decisions Made
- Used test-only importer metadata and fake flag cache injection rather than any production change because the plan expected DeckAnalysisPacketService to remain unchanged.
- Compared flattened packet text as UTF-8 bytes so the tests guard true output identity rather than partial field equality.
- Treated the fixture notes as ground truth for companion docs when they conflicted with the stale plan wording.

## Deviations from Plan

### Auto-fixed Issues

**1. Documentation correction for companion detection source**
- **Found during:** Task 2 (README + Help/manabase.md documentation)
- **Issue:** The plan text still claimed Archidekt companion-category detection, but `72-FIXTURE-NOTES.md` states Archidekt has no reliable Companion category.
- **Fix:** Updated both docs to state that companion auto-detection is Moxfield direct API only, with the manual designator used for Archidekt decks, pasted lists, and the Moxfield Commander Spellbook fallback path.
- **Files modified:** README.md, DeckFlow.Web/Help/manabase.md
- **Verification:** requested grep check passed and doc text matches fixture notes
- **Committed in:** 4e6bfa47 (task commit)

---

**Total deviations:** 1 auto-fixed (documentation correction)
**Impact on plan:** Corrected stale wording without expanding scope. The shipped docs now match the verified behavior.

## Issues Encountered
- The focused test run initially failed to compile because the existing helper always passed a null feature-flag cache and the new test seam needed explicit `DeckEntry` construction. This was resolved inside the fenced test file only.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- DeckAnalysisPacketService now has regression coverage for companion inertness and flag invariance.
- Phase 73 can build on the documented command-zone and companion behavior without re-litigating Archidekt companion auto-detection.

---
*Phase: 72-command-zone-commander-castability*
*Completed: 2026-06-27*
