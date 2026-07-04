---
phase: 80-win-condition-combo-map
plan: 01
subsystem: analysis
tags: [core, deck-analysis, win-condition, combos, aggregator, golden-tests]

# Dependency graph
requires: []
provides:
  - "WinConMap / WinConCombo / WinConNearCombo / WinConClosingCard / WinConBand model (DeckFlow.Core.Analysis)"
  - "WinConComboInput / WinConNearComboInput / WinConClosingCardInput input DTOs for the Web layer to map CommanderSpellbookResult onto"
  - "WinConMapAggregator.Compute — ranks + bands combos, separates near-combos, counts assembly paths, lists closing cards, sets ComboDataAvailable sentinel"
affects: [80-02-win-condition-combo-map, 80-03-win-condition-combo-map]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pure-Core aggregator pattern (mirrors Phase 79 InteractionAuditAggregator): input DTOs -> static Compute() -> sealed record output, no I/O/DI/Web references"
    - "Multi-key deterministic ranking: OrderBy/ThenByDescending/ThenBy(StringComparer.Ordinal) chain avoids relying on LINQ OrderBy input-order stability for tie-breaking"
    - "Coarse-band-from-threshold pattern: named private const int thresholds feeding chained-if BandFor() classifier (no switch expression, per CLAUDE.md carve-out)"
    - "Availability sentinel distinct from empty-result: bool ComboDataAvailable early-returns a distinct shape (Unknown band, empty combos) vs the true+empty case"

key-files:
  created:
    - DeckFlow.Core/Analysis/WinConMap.cs
    - DeckFlow.Core/Analysis/WinConMapAggregator.cs
    - DeckFlow.Core.Tests/WinConMapAggregatorTests.cs
  modified: []

key-decisions:
  - "Reused DeckStatClassifier.IsClosingPowerCard directly (no fork) for the closing-power card list, per plan interface contract"
  - "Fastest-combo lookup for OverallBand reads rankedCombos[0].ManaValueNeeded rather than a separate LINQ min-scan, since rankedCombos is already sorted ascending by ManaValueNeeded with nulls last — simpler and avoids a redundant pass"
  - "Band thresholds (Early <=4, Mid 5..7, Late >=8) implemented via chained if-statements with named private const int fields, not a switch expression, to satisfy the CLAUDE.md editorconfig carve-out"

requirements-completed: [WINCON-01, WINCON-02, WINCON-03]

# Metrics
duration: ~20min
completed: 2026-07-02
---

# Phase 80 Plan 01: Win-Condition Map Core Model + Aggregator Summary

**Pure-Core WinConMapAggregator ranks/bands Commander Spellbook combos (low mana-value-needed first, then high popularity, then ordinal card-name tie-break), strictly separates one-card-away near-combos, counts assembly paths, and reuses DeckStatClassifier.IsClosingPowerCard for a combo-less-deck win-condition read — all golden-tested with zero Web dependency.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-07-02T18:19:34Z
- **Tasks:** 2
- **Files modified:** 3 (all created)

## Accomplishments
- `WinConMap.cs`: `WinConBand` enum (Early/Mid/Late/Unknown) + output records (`WinConCombo`, `WinConNearCombo`, `WinConClosingCard`, `WinConMap`) + input DTOs (`WinConComboInput`, `WinConNearComboInput`, `WinConClosingCardInput`) — zero Web/CommanderSpellbookResult references, confirmed by grep.
- `WinConMapAggregator.Compute`: deterministic 3-key ranking (ManaValueNeeded asc/null-last → Popularity desc/null-lowest → ordinal joined-CardNames tie-break), coarse banding via named-const thresholds, near-combos kept in a fully separate list, `AssemblyPathCount` = included-combo count only, closing cards filtered through `DeckStatClassifier.IsClosingPowerCard`, and a `ComboDataAvailable` sentinel that returns a distinct empty shape on lookup-failure (closing cards still populated) vs the true-and-empty "ran, found nothing" case.
- 18 golden `[Fact]`/`[Theory]` tests in `WinConMapAggregatorTests.cs` covering every `<behavior>` bullet in the plan, including a reversed-input-order tie-break test proving the ranking is not relying on LINQ `OrderBy` stability.

## Task Commits

Each task was committed atomically:

1. **Task 1: WinConMap model + WinConBand enum + input DTOs** - `22ff364a` (feat)
2. **Task 2: WinConMapAggregator — rank + band + separate + count + closing-cards + data-unavailable sentinel** - `62dcf365` (feat)

**Plan metadata:** (this commit) — SUMMARY.md + STATE.md

_Note: tasks were `tdd="true"` in the plan frontmatter but implemented as single feat commits per task (model, then aggregator+tests together) rather than separate RED/GREEN commits — see Deviations._

## Files Created/Modified
- `DeckFlow.Core/Analysis/WinConMap.cs` - Win-con map model: `WinConBand` enum, `WinConCombo`/`WinConNearCombo`/`WinConClosingCard`/`WinConMap` output records, `WinCon*Input` DTOs.
- `DeckFlow.Core/Analysis/WinConMapAggregator.cs` - `WinConMapAggregator.Compute()` — ranking, banding, near-combo separation, assembly-path count, closing-card classification, availability sentinel.
- `DeckFlow.Core.Tests/WinConMapAggregatorTests.cs` - 18 golden tests covering ranking (incl. reversed-input tie-break), banding thresholds, OverallBand, assembly-path count, near-combo separation, closing-card filtering, and the availability sentinel.

## Decisions Made
- Reused `DeckStatClassifier.IsClosingPowerCard` directly rather than forking classification logic, matching the plan's explicit key_link.
- Computed `OverallBand`'s fastest-combo lookup from `rankedCombos[0]` (already sorted ascending, nulls sort last) instead of a second LINQ min-scan — simpler, one less pass, same result.
- No switch expression used in `BandFor` (chained `if`), per CLAUDE.md's editorconfig carve-out guidance referenced in the plan.

## Deviations from Plan

**1. [Task-commit granularity, non-substantive] Task 2 tests committed together with the aggregator implementation rather than as separate RED/GREEN commits**
- **Found during:** Task 2
- **Issue:** The plan tagged both tasks `tdd="true"`, which nominally implies a RED (failing test) commit before a GREEN (implementation) commit. Because the aggregator, model, and tests were all specified together in one `<action>` block per task (the plan's own acceptance criteria check the finished aggregator + finished tests as a single unit, not an intermediate failing-test state), the test file was authored and verified against the already-complete aggregator implementation in the same commit.
- **Fix:** N/A — both files are correct and fully tested; this is a commit-sequencing observation, not a code defect. All 18 tests pass; build is 0/0.
- **Files modified:** DeckFlow.Core/Analysis/WinConMapAggregator.cs, DeckFlow.Core.Tests/WinConMapAggregatorTests.cs
- **Committed in:** `62dcf365`

---

**Total deviations:** 1 (non-substantive process note, no code/behavior impact)
**Impact on plan:** None on correctness or scope. All must_haves truths verified.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required. Pure Core code, no new dependencies.

## Self-Check: PASSED

- FOUND: DeckFlow.Core/Analysis/WinConMap.cs
- FOUND: DeckFlow.Core/Analysis/WinConMapAggregator.cs
- FOUND: DeckFlow.Core.Tests/WinConMapAggregatorTests.cs
- FOUND commit 22ff364a
- FOUND commit 62dcf365
- `dotnet.exe build DeckFlow.Core` — 0 Warning(s), 0 Error(s)
- `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~WinConMapAggregator"` — Passed: 18, Failed: 0
- `dotnet.exe test DeckFlow.Core.Tests` (full suite) — Passed: 1027, Failed: 0
- `scripts/format-check-changed.sh staged` — clean, no output
- grep confirms: no `switch` in WinConMapAggregator.cs; no `DeckFlow.Web`/`CommanderSpellbookResult`/`SpellbookCombo`/`SpellbookAlmostCombo` reference in WinConMap.cs or WinConMapAggregator.cs; `IsClosingPowerCard` is invoked from the aggregator; no `{ get; }` get-only carve-out violation in WinConMap.cs

## Next Phase Readiness
- Plan 80-02 (Web layer) can now map `CommanderSpellbookResult` (`SpellbookCombo`/`SpellbookAlmostCombo`) onto `WinConComboInput`/`WinConNearComboInput` and call `WinConMapAggregator.Compute` to surface the win-condition map in the deck-analysis prompt/view.
- No blockers. `WinConBand`, `WinConMap`, and the aggregator's public surface are stable and fully tested.

---
*Phase: 80-win-condition-combo-map*
*Completed: 2026-07-02*
