---
phase: 77-multi-axis-deck-score
plan: 01
subsystem: analysis
tags: [deck-stats, classifier, oracle-text, tutors, fast-mana, counterspell, score]

# Dependency graph
requires:
  - phase: (existing Core)
    provides: DeckStatClassifier predicate pattern + DeckStatAggregator.Compute tally loop + EstimateManaValue
provides:
  - DeckStatClassifier.IsTutorCard / IsFastManaCard / IsRampOrDrawUnderThreeMv / IsCounterspellCard predicates
  - DeckStatSummary.Tutors / FastMana / RampDrawUnderThreeMv / Counters additive { get; init; } fields
  - quantity-weighted tallies for the four signals in DeckStatAggregator.Compute
affects: [77-02, 77-03, multi-axis scorer Speed/Consistency/Power/Control axes]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Additive { get; init; } summary fields (not new positional params) for backward-compatible record extension"
    - "Single-expression Contains(..., OrdinalIgnoreCase) chaining predicates with land-fetch exclusions"

key-files:
  created: []
  modified:
    - DeckFlow.Core/Analysis/DeckStatClassifier.cs
    - DeckFlow.Core/Analysis/DeckStatAggregator.cs
    - DeckFlow.Core.Tests/DeckStatClassifierTests.cs
    - DeckFlow.Core.Tests/DeckStatAggregatorTests.cs

key-decisions:
  - "New DeckStatSummary fields added as trailing { get; init; } members, never positional params, to preserve backward compatibility (CLAUDE.md carve-out: init not get-only, no required)"
  - "IsTutorCard excludes land-fetch ramp via three literal substrings (basic land / land card / land onto the battlefield) per Pitfall 6; dual-land fetches that match none of these are intentionally classified as tutors"
  - "IsFastManaCard keys off EstimateManaValue(manaCost)==0 so MV-1 rocks like Sol Ring are excluded"

patterns-established:
  - "Pattern: quantity-weighted classifier-call block appended after the land-skip continue in Compute's foreach"
  - "Pattern: RED test commit (test) then GREEN impl commit (feat) per TDD task"

metrics:
  duration: ~5 min
  completed: 2026-06-29
---

# Phase 77 Plan 01: Multi-Axis Deck Score Signals Summary

Added the four missing deck signals the multi-axis scorer consumes — tutor count, fast-mana count, ramp/draw-under-MV-2 count, and counterspell count — as pure Core oracle-text/type-line predicates plus additive `{ get; init; }` fields and quantity-weighted tallies on `DeckStatSummary`/`DeckStatAggregator`.

## What Was Built

### Task 1 — Four DeckStatClassifier predicates (RED `6e6f3aef`, GREEN `be3ae88f`)
- `IsTutorCard(string oracleText)`: matches "search your library for", excludes land-fetch ramp via `!"basic land" && !"land card" && !"land onto the battlefield"`.
- `IsFastManaCard(string typeLine, string oracleText, string manaCost)`: `EstimateManaValue(manaCost)==0 && Artifact && ("{T}: Add" || "Add {")` — Mana Crypt true, Sol Ring (MV 1) false.
- `IsRampOrDrawUnderThreeMv(string typeLine, string oracleText, string manaCost)`: `EstimateManaValue(manaCost) <= 2 && (IsRampCard || IsDrawCard)`.
- `IsCounterspellCard(string oracleText)`: matches "counter target spell" (ability counters excluded).
- Tests: `Is*_TrueCases`/`Is*_FalseCases` `[Theory]/[InlineData]` sections per predicate.

### Task 2 — Four DeckStatSummary fields + aggregator tallies (RED `8ac72b47`, GREEN `98693538`)
- Added `Tutors`, `FastMana`, `RampDrawUnderThreeMv`, `Counters` as trailing `public int … { get; init; }` members on the `DeckStatSummary` record (object-initializer body on the existing positional record — backward compatible).
- Added four `var …= 0;` tally accumulators and four classifier-call blocks (quantity-weighted, `card.ManaCost` passed as third arg where needed) inside `Compute`'s foreach, set via object-initializer on the returned summary.
- Tests: `Compute_TalliesNewSignalFields` (quantity-weighted: Tutors 1, FastMana 2, Counters 1, RampDrawUnderThreeMv 1) and `Compute_NoMatchingCards_LeavesNewSignalFieldsZero`.

## Verification
- `dotnet.exe build DeckFlow.Core`: Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~DeckStatClassifier"`: 83 passed, exit 0.
- `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~DeckStatAggregator"`: 14 passed, exit 0.
- Full `DeckFlow.Core.Tests`: 926 passed, 0 failed.
- `grep -c "required " DeckStatAggregator.cs` → 0 (carve-out honored: init, not required, not get-only).
- Changed-lines format gate (`scripts/format-check-changed.sh staged`): clean.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Test correction] Over-strict IsTutorCard false case**
- **Found during:** Task 1 GREEN (test run)
- **Issue:** My RED test asserted "Search your library for a Forest or Plains card onto the battlefield." was NOT a tutor, but that text matches none of the three documented exclusion substrings, so the predicate (correctly, per spec) returns true. The wrong expectation was in my test, not the implementation.
- **Fix:** Replaced the InlineData with "Search your library for a Mountain, then put that land onto the battlefield." which genuinely exercises the `land onto the battlefield` exclusion.
- **Files modified:** DeckFlow.Core.Tests/DeckStatClassifierTests.cs
- **Commit:** be3ae88f (folded into the GREEN commit)

## Known Stubs
None.

## Threat Flags
None — pure Core CPU transform over already-loaded card data; no new I/O, DI, network, or NuGet packages (matches plan threat_model).

## Self-Check: PASSED
- Files: all 4 modified files present.
- Commits: 6e6f3aef, be3ae88f, 8ac72b47, 98693538 all present in git log.
