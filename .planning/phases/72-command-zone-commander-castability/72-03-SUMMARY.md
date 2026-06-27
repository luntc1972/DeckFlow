---
phase: 72-command-zone-commander-castability
plan: 03
subsystem: testing
tags: [moxfield, archidekt, importer, companion, command-zone]

requires:
  - phase: 72-02
    provides: verified fixture captures and fixture notes for Moxfield and Archidekt importer behavior
provides:
  - Moxfield direct-import companion name extraction as inert metadata
  - DeckSourceLoadResult propagation for detected companion name
  - Archidekt regression lock for real Background commander-board behavior
affects: [72-05, manabase, deck-loading]

tech-stack:
  added: []
  patterns: [metadata-only companion detection, fixture-locked importer regression tests]

key-files:
  created: [DeckFlow.Core.Tests/ArchidektApiDeckImporterTests.cs]
  modified:
    [
      DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs,
      DeckFlow.Core/Integration/DeckImporterInterfaces.cs,
      DeckFlow.Core/Loading/DeckEntryLoader.cs,
      DeckFlow.Core.Tests/MoxfieldApiDeckImporterTests.cs,
      DeckFlow.Core.Tests/DeckEntryLoaderTests.cs
    ]

key-decisions:
  - "Moxfield companions stay out of Entries and are surfaced only as DetectedCompanionName."
  - "Commander Spellbook fallback leaves DetectedCompanionName null because the fallback payload exposes no companion data."
  - "Archidekt regression coverage follows fixture ground truth: Passionate Archaeologist arrives on the commander board and no Companion category assertion is retained."

patterns-established:
  - "Importers may expose side metadata through result records without mutating deck-entry output."
  - "Fixture notes override stale plan prose when real importer behavior diverges."

requirements-completed: [B-04, B-05]

duration: 45min
completed: 2026-06-27
---

# Phase 72: Command Zone Commander Castability Summary

**Moxfield direct imports now surface companion names as inert metadata, DeckEntryLoader forwards that metadata, and Archidekt tests lock the real commander-board Background behavior from the captured fixture**

## Performance

- **Duration:** 45 min
- **Started:** 2026-06-27T14:45:00Z
- **Completed:** 2026-06-27T15:29:38Z
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments
- Added `DetectedCompanionName` to `MoxfieldImportResult` and populated it only from the first direct-API Moxfield companion entry.
- Threaded `DetectedCompanionName` through `DeckSourceLoadResult` on the Moxfield URL path while leaving Archidekt and pasted-text paths null.
- Added fixture-backed regression tests proving Archidekt surfaces `Passionate Archaeologist` on the commander board and does not expose a reliable Companion category contract.

## Task Commits

Each task was committed atomically:

1. **Tasks 1-3: Companion metadata threading and Archidekt regression lock** - `1f06bdba` (feat)

**Plan metadata:** pending summary commit

## Files Created/Modified
- `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` - Extracts the first companion name from `root.companions` without adding a `DeckEntry`.
- `DeckFlow.Core/Integration/DeckImporterInterfaces.cs` - Extends `MoxfieldImportResult` with `DetectedCompanionName`.
- `DeckFlow.Core/Loading/DeckEntryLoader.cs` - Extends `DeckSourceLoadResult` and threads Moxfield companion metadata through source loading.
- `DeckFlow.Core.Tests/MoxfieldApiDeckImporterTests.cs` - Covers direct companion detection, absent-companion null behavior, and fallback null behavior.
- `DeckFlow.Core.Tests/DeckEntryLoaderTests.cs` - Covers propagation to `DeckSourceLoadResult` and null behavior on non-Moxfield paths.
- `DeckFlow.Core.Tests/ArchidektApiDeckImporterTests.cs` - Locks the real Archidekt fixture behavior for `Passionate Archaeologist`.
- `.planning/phases/72-command-zone-commander-castability/72-03-SUMMARY.md` - Records implementation, verification, and the fixture-driven Task 3 deviation.

## Decisions Made
- Used metadata-only companion extraction to preserve importer entry output byte identity.
- Capped detected companion names at 200 characters after trimming.
- Kept Archidekt production code unchanged and asserted the captured fixture’s current output instead.

## Deviations from Plan

### Auto-fixed Issues

**1. [Fixture override] Task 3 used real Archidekt output instead of stale plan prose**
- **Found during:** Task 3 (Archidekt regression lock)
- **Issue:** The plan assumed Background and Companion semantics would be preserved on mainboard entries, but the verified fixture shows `Passionate Archaeologist` has `categories = ["Commander"]`.
- **Fix:** Wrote regression tests asserting the Background card arrives with `Board == "commander"` and dropped the stale Companion-category assertion.
- **Files modified:** `DeckFlow.Core.Tests/ArchidektApiDeckImporterTests.cs`, `.planning/phases/72-command-zone-commander-castability/72-03-SUMMARY.md`
- **Verification:** Targeted `ArchidektApiDeckImporterTests` pass against the captured fixture.
- **Committed in:** summary commit plus implementation commit

---

**Total deviations:** 1 auto-fixed (fixture-ground-truth override)
**Impact on plan:** Necessary for correctness. No production scope creep and no Archidekt importer behavior change.

## Issues Encountered
- The initial Archidekt test seam omitted the importer’s base URL, which caused RestSharp request validation to fail. The test harness was corrected without changing production code.
- The real Archidekt fixture emitted `79` importer entries rather than a nominal deck-card count of `100`, so the regression lock was updated to the importer’s actual current output.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `72-05` can consume `DeckSourceLoadResult.DetectedCompanionName` without changing importer entry semantics.
- Archidekt companion auto-detection should remain out of scope unless a new reliable source signal is found.

---
*Phase: 72-command-zone-commander-castability*
*Completed: 2026-06-27*
