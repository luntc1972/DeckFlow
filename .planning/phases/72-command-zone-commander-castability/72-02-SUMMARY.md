---
phase: 72-command-zone-commander-castability
plan: 02
subsystem: testing
tags: [moxfield, archidekt, fixtures, command-zone, companion, background]
requires:
  - phase: 72-command-zone-commander-castability
    provides: plan 72-01 groundwork and importer context
provides:
  - synthetic Moxfield direct fixture with a top-level companions board
  - real Archidekt deck 3674983 capture proving Background is surfaced as Commander
  - fixture notes documenting ground-truth corrections to the original assumptions
affects: [72-03, 72-05, importer tests, command-zone analysis]
tech-stack:
  added: []
  patterns: [synthetic fixture for Cloudflare-blocked Moxfield direct API, real Archidekt API capture for importer truthing]
key-files:
  created:
    - DeckFlow.Core.Tests/Fixtures/moxfield-companion-direct.json
    - DeckFlow.Core.Tests/Fixtures/archidekt-background-companion.json
    - .planning/phases/72-command-zone-commander-castability/72-FIXTURE-NOTES.md
    - .planning/phases/72-command-zone-commander-castability/72-02-SUMMARY.md
  modified: []
key-decisions:
  - "Use a synthetic Moxfield fixture because live direct API capture is Cloudflare-blocked from this environment."
  - "Treat Archidekt Background ground truth as commander-board routing via categories=['Commander'], not as a preserved user category."
  - "Drop Archidekt companion-category detection; rely on manual designator plus Moxfield DetectedCompanionName."
patterns-established:
  - "Moxfield fixture shape must mirror top-level root board objects consumed by AddBoardEntries."
  - "Archidekt board categories are importer routing signals and are stripped from DeckEntry.Category."
requirements-completed: [B-04, B-05]
duration: 20min
completed: 2026-06-27
---

# Phase 72-02 Summary

**Wave-0 fixtures now capture the real Archidekt Background routing behavior and a synthetic Moxfield companions board in the exact importer shape.**

## Performance

- **Duration:** 20 min
- **Started:** 2026-06-27T09:02:00-06:00
- **Completed:** 2026-06-27T09:22:16-06:00
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- Added `DeckFlow.Core.Tests/Fixtures/moxfield-companion-direct.json` as a synthetic direct-API fixture with top-level `commanders`, `companions`, `mainboard`, `sideboard`, and `maybeboard` objects.
- Captured the real Archidekt API payload for deck `3674983` and verified `Passionate Archaeologist` appears with `categories=["Commander"]` and `oracleCard.subTypes=["Background"]`.
- Documented the corrected ground truth in `72-FIXTURE-NOTES.md`, including the dropped Archidekt companion-category path.

## Task Commits

1. **Fixture capture and notes** - `a4f92a0` (`test`)
2. **Plan summary** - `[pending in this summary file commit]` (`docs`)

## Files Created/Modified

- `DeckFlow.Core.Tests/Fixtures/moxfield-companion-direct.json` - Synthetic Moxfield direct payload matching `AddBoardEntries` top-level board/object shape.
- `DeckFlow.Core.Tests/Fixtures/archidekt-background-companion.json` - Real Archidekt deck `3674983` API capture used to verify Background routing truth.
- `.planning/phases/72-command-zone-commander-castability/72-FIXTURE-NOTES.md` - Ground-truth notes and plan corrections for Moxfield companion and Archidekt Background/Companion assumptions.
- `.planning/phases/72-command-zone-commander-castability/72-02-SUMMARY.md` - Phase execution summary and deviation record.

## Decisions Made

- Used the user-directed synthetic Moxfield fixture instead of attempting further live direct-API capture retries.
- Preserved the full real Archidekt payload rather than trimming it, because the user explicitly allowed a large capture and wanted proof it was not fabricated.
- Recorded the exact importer consequence that `Commander` is a board category, so Archidekt Background cards do not preserve `DeckEntry.Category`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Ground Truth] Moxfield live capture replaced with synthetic fixture**
- **Found during:** Task 1 (Moxfield fixture creation)
- **Issue:** The original plan assumed a live Moxfield direct-API capture, but this environment is Cloudflare-blocked.
- **Fix:** Built `moxfield-companion-direct.json` by hand to match `MoxfieldApiDeckImporter.AddBoardEntries`, including the exact top-level `companions` board shape and path `root.companions.<slot>.card.name`.
- **Files modified:** `DeckFlow.Core.Tests/Fixtures/moxfield-companion-direct.json`, `.planning/phases/72-command-zone-commander-castability/72-FIXTURE-NOTES.md`
- **Verification:** JSON parse succeeded and `companions` was found in the saved fixture.
- **Committed in:** `a4f92a0`

**2. [Ground Truth] Archidekt Background assumption was wrong**
- **Found during:** Task 2 (Archidekt fixture capture)
- **Issue:** The original plan assumed a Background would arrive as a mainboard entry with category preserved.
- **Fix:** Captured real deck `3674983` and documented that `Passionate Archaeologist` arrives with `categories=["Commander"]` and `oracleCard.subTypes=["Background"]`, which routes it to the commander board and strips the board category from `DeckEntry.Category`.
- **Files modified:** `DeckFlow.Core.Tests/Fixtures/archidekt-background-companion.json`, `.planning/phases/72-command-zone-commander-castability/72-FIXTURE-NOTES.md`
- **Verification:** JSON parse succeeded; the saved payload contains `Passionate Archaeologist` and `Background`.
- **Committed in:** `a4f92a0`

**3. [Scope Correction] Archidekt companion-category detection dropped**
- **Found during:** Notes finalization
- **Issue:** The original plan assumed Archidekt exposes a reliable `Companion` category for detection.
- **Fix:** Recorded the corrected rule that companion detection relies on manual designator input plus Moxfield `DetectedCompanionName` only.
- **Files modified:** `.planning/phases/72-command-zone-commander-castability/72-FIXTURE-NOTES.md`
- **Verification:** Summary and notes align with the user-directed ground truth for phase 72.
- **Committed in:** `a4f92a0`

---

**Total deviations:** 3 auto-fixed
**Impact on plan:** The deviations narrowed the phase to validated fixture truth and removed incorrect downstream assumptions before production-code plans consume them.

## Issues Encountered

- The requested `DeckFlow.Core.Tests/Fixtures` directory did not exist in this worktree and had to be created before saving the fixtures.
- The live Moxfield direct API remained unavailable from this environment, so only the synthetic path was viable.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan `72-03` can consume the new Moxfield companions fixture using the confirmed top-level key `companions`.
- Plan `72-05` should treat Archidekt Background as commander-board routing and should not implement Archidekt companion-category detection.

---
*Phase: 72-command-zone-commander-castability*
*Completed: 2026-06-27*
