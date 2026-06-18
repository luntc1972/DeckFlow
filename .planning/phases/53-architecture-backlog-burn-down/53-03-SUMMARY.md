---
phase: 53-architecture-backlog-burn-down
plan: 03
subsystem: Core / Web Services
tags: [refactor, arch, classifier, core, deck-comparison, testing]
dependency_graph:
  requires: []
  provides: [DeckFlow.Core.Analysis.DeckStatClassifier]
  affects: [DeckFlow.Web/Services/DeckComparisonService.cs]
tech_stack:
  added: [DeckFlow.Core.Analysis namespace]
  patterns: [pure-static classifier class, verbatim expression move]
key_files:
  created:
    - DeckFlow.Core/Analysis/DeckStatClassifier.cs
    - DeckFlow.Core.Tests/DeckStatClassifierTests.cs
  modified:
    - DeckFlow.Web/Services/DeckComparisonService.cs
decisions:
  - Expressions moved verbatim (no re-authoring) to guarantee packet stat tallies are unchanged
  - using directive placed in DeckFlow.* group, before DeckFlow.Core.Integration (alpha order)
  - Only 9 lines changed in DeckComparisonService (1 using add, 6 call-site prefixes, 62 lines deleted)
metrics:
  duration_seconds: 230
  completed_date: "2026-06-18"
  tasks_completed: 2
  files_changed: 3
---

# Phase 53 Plan 03: Deck-Stat Classifiers to Core Summary

**One-liner:** Relocated six pure deck-stat classifiers and ParseManaToken from DeckComparisonService into a new `DeckFlow.Core.Analysis.DeckStatClassifier` public static class, with 64 Core unit tests locking the verbatim behavior.

## What Was Built

### Task 1 — DeckStatClassifier + Core tests (commit 80b318b)

Created `DeckFlow.Core/Analysis/DeckStatClassifier.cs`: a new `public static` class in namespace `DeckFlow.Core.Analysis` exposing seven members:

- `IsRampCard(typeLine, oracleText)` — lands, mana-add phrases, land-search, Treasure producers
- `IsDrawCard(oracleText)` — draw-a-card, investigate, connive
- `IsInteractionCard(typeLine, oracleText)` — Instant type, destroy/exile/counter/return/fight target
- `IsBoardWipeCard(oracleText)` — destroy all, each creature gets -, exile all
- `IsRecursionCard(oracleText)` — graveyard-return phrases, reanimate
- `IsClosingPowerCard(typeLine, oracleText)` — win conditions, extra turns, Craterhoof, combat-draw engines
- `ParseManaToken(token)` — numeric → int value; X → 0; hybrid → 1; colored → 1

All boolean expressions copied verbatim from DeckComparisonService (no re-authoring).

Created `DeckFlow.Core.Tests/DeckStatClassifierTests.cs`: 64 xUnit `[Theory]`/`[InlineData]` assertions covering at least one true-case and one false-case per classifier, plus all ParseManaToken variants (numeric, X, hybrid, colored). All 64 passed on first run.

### Task 2 — Repoint DeckComparisonService (commit 2c33104)

Modified `DeckFlow.Web/Services/DeckComparisonService.cs` with surgical changes only:

- Added `using DeckFlow.Core.Analysis;` (1 line, sorted before `DeckFlow.Core.Integration`)
- Prefixed 6 classifier call sites in the curve/stat tally loop with `DeckStatClassifier.`
- Prefixed `ParseManaToken` call in `EstimateManaValue` with `DeckStatClassifier.`
- Deleted all 7 private static method definitions (62 lines removed)

Net diff: 8 insertions, 69 deletions. No other lines touched.

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build DeckFlow.sln` | 0 errors, 2 pre-existing CS1574 warnings (unchanged) |
| `DeckStatClassifierTests` (filter) | 64/64 passed |
| Web `--filter FullyQualifiedName~Comparison` | 44/44 passed |
| Full Core suite | 447/447 passed (383 prior + 64 new) |

## Threat Model Coverage

T-53-03 (Tampering — classifier phrase lists preserved): **CLOSED**. Expressions moved character-for-character. New Core unit tests lock exact true/false behavior. Web comparison tests confirm tally loop is unchanged.

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None.

## Threat Flags

None. Pure-function relocation between assemblies; no new input surfaces, I/O, endpoints, or schema changes.

## Self-Check: PASSED

- `DeckFlow.Core/Analysis/DeckStatClassifier.cs` exists: FOUND
- `DeckFlow.Core.Tests/DeckStatClassifierTests.cs` exists: FOUND
- Commit 80b318b exists: FOUND
- Commit 2c33104 exists: FOUND
- Private copies removed (`grep -c` = 0): CONFIRMED
- DeckStatClassifier call sites (`grep -c` = 7): CONFIRMED
