---
phase: 76-bracket-classifier-balancer
plan: "01"
subsystem: DeckFlow.Core.Bracket + DeckFlow.Web/Data
tags: [bracket, classifier, game-changers, core-models, json-seed, tdd]
dependency_graph:
  requires: []
  provides:
    - DeckFlow.Core.Bracket namespace (GameChangerCatalog, BracketTier, TwoCardCombo, BracketClassification, BracketRubricThresholds, BracketClassifier)
    - DeckFlow.Web/Data/bracket-data.json (53 Game Changers, 5 tiers, MLD, extra-turn lists)
  affects:
    - Phase 76 plans 02-06 (all consume GameChangerCatalog / BracketClassifier)
    - Phase 77 Power axis (SCORE-02 reuses GameChangerCatalog.GameChangers count signal)
tech_stack:
  added: []
  patterns:
    - Pure static classifier (analog: ManabaseClassifier.cs)
    - Positional sealed records with IReadOnlyList<T> (analog: ManabaseReport.cs)
    - TDD RED/GREEN cycle (test commit precedes implementation commit)
    - Versioned JSON seed in DeckFlow.Web/Data/ with Content Update csproj item
key_files:
  created:
    - DeckFlow.Core/Bracket/GameChangerCatalog.cs
    - DeckFlow.Core/Bracket/BracketTier.cs (co-located in GameChangerCatalog.cs)
    - DeckFlow.Core/Bracket/TwoCardCombo.cs
    - DeckFlow.Core/Bracket/BracketClassification.cs
    - DeckFlow.Core/Bracket/BracketRubricThresholds.cs
    - DeckFlow.Core/Bracket/BracketClassifier.cs
    - DeckFlow.Web/Data/bracket-data.json
    - DeckFlow.Core.Tests/Bracket/BracketClassifierTests.cs
  modified:
    - DeckFlow.Web/DeckFlow.Web.csproj (Content Update for bracket-data.json)
decisions:
  - "TwoCardCombo is Core-local (not SpellbookCombo from Web) — keeps DeckFlow.Core free of DeckFlow.Web reference; Web orchestrator (76-04) maps at the boundary"
  - "No ExtraCardDraw field by design (RESEARCH A2): informational-only, covered by Game Changers list; view shows static line only"
  - "CedhGameChangerCount=10 documented as product heuristic with Why: comment; downstream artifact must instruct AI to re-confirm B5/cEDH at meta level"
  - "ZeroSignalBracket=2 (Core): B1/Exhibition requires self-declaration per WotC rubric, never auto-assigned"
  - "Extra-turn cards: informational only, never feed B4 hard-floor gating (confirmed per scrollvault.net rubric)"
  - "Content Update (not Include) in csproj avoids duplicate-item error; SDK already includes Data/*.json by default"
metrics:
  duration_minutes: 35
  completed_date: "2026-06-28"
  tasks_completed: 3
  files_changed: 9
---

# Phase 76 Plan 01: Bracket Core Models + Classifier + JSON Seed Summary

Pure Core bracket foundation: GameChangerCatalog/BracketClassification positional records, 53-card versioned bracket-data.json seed (effective 2026-02-09), BracketRubricThresholds constants, and a pure static BracketClassifier with 17 TDD-authored unit tests encoding the official Oct-2025/Feb-2026 WotC rubric.

## What Was Built

### Task 1: Core bracket record models + rubric thresholds

Four files created in `DeckFlow.Core/Bracket/`:

- **GameChangerCatalog.cs** — Two positional sealed records: `GameChangerCatalog` (catalog root with EffectiveDate, GameChangers, MassLandDenialCards, ExtraTurnCards, Tiers) and `BracketTier` (tier metadata with MaxGameChangers; -1 = unlimited). All properties IReadOnlyList<T>.
- **TwoCardCombo.cs** — Core-local combo record (CardNames + Results). Keeps DeckFlow.Core free of DeckFlow.Web (Web orchestrator maps SpellbookCombo → TwoCardCombo in 76-04).
- **BracketClassification.cs** — Result record with BracketNumber, three detected-card lists, nullable TwoCardCombos, ComboDetectionAvailable flag, EffectiveDate string.
- **BracketRubricThresholds.cs** — Constants: HardFloorGameChangerCount=4, MinGameChangersForB3=1, ZeroSignalBracket=2, CedhGameChangerCount=10. The cEDH threshold carries a `/// <summary>` explicitly documenting it as a product heuristic, not the official WotC rubric.

No ExtraCardDraw field exists by design (RESEARCH A2: covered by Game Changers; view renders static line only).

### Task 2: Versioned bracket-data.json + csproj

- **DeckFlow.Web/Data/bracket-data.json** — effectiveDate 2026-02-09; 53 Game Changers sorted OrdinalIgnoreCase; 12 MLD cards; 9 extra-turn cards; 5 bracket tiers with array key `tiers` (not `bracketTiers`) binding to `GameChangerCatalog.Tiers` under JsonSerializerDefaults.Web case-insensitive matching. Tier label/summary/turnsExpectation copied verbatim from CommanderBracketCatalog.cs for byte-identical migration in 76-02.
- **DeckFlow.Web.csproj** — `<Content Update="Data\bracket-data.json">` sets CopyToOutputDirectory=Always. Used `Update` (not `Include`) because the SDK already auto-includes Data/ directory files; `Include` would create a duplicate item error (NETSDK1022).

### Task 3: BracketClassifier (TDD RED → GREEN)

- **RED commit** (`eca24eb0`): BracketClassifierTests.cs with 17 tests covering all rubric cases. Tests failed to compile (BracketClassifier didn't exist yet).
- **GREEN commit** (`89ccf0b4`): BracketClassifier.cs. Pure static Classify() method:
  - Builds deckNames from mainboard/commander entries only (sideboard excluded)
  - Intersects catalog.GameChangers, MassLandDenialCards, ExtraTurnCards against deckNames
  - Gating: B5 at >=10 GC (product heuristic, `// Why:` comment), B4 at MLD/combo/>=4GC, B3 at 1-3 GC, B2 (ZeroSignalBracket) at zero signals
  - null twoCardCombos → ComboDetectionAvailable=false, TwoCardCombos=null (Pitfall 1: null ≠ zero combos)
  - Extra-turn detection populated but never feeds gating
  - EffectiveDate: "yyyy-MM-dd" InvariantCulture

## Test Results

```
Passed!  - Failed: 0, Passed: 17, Skipped: 0, Total: 17
```

Test cases:
- 9 InlineData theory: zero-signal B2, B3 at 1 GC, B3 at 3 GC, B4 at 4 GC, B4 at 9 GC, B5 at 10 GC, combo→B4, MLD→B4, 3GC+combo→B4
- DetectedGameChangers matches deck intersection
- DetectedMassLandDenial populated when MLD in deck
- Extra-turn informational only (B2, DetectedExtraTurnCards populated)
- Null combo → ComboDetectionAvailable=false, TwoCardCombos=null (Pitfall 1)
- Empty combo list → ComboDetectionAvailable=true, TwoCardCombos=empty
- EffectiveDate formatted "2026-02-09"
- Sideboard entries excluded from all signals

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Used `Content Update` instead of `Content Include` for bracket-data.json**
- **Found during:** Task 2 verification (DeckFlow.Web build)
- **Issue:** `<Content Include="Data\bracket-data.json">` caused NETSDK1022 duplicate-item error because the SDK auto-includes all files under the project directory as Content items
- **Fix:** Changed to `<Content Update="Data\bracket-data.json">` which updates the existing auto-included item's metadata rather than adding a new entry
- **Files modified:** DeckFlow.Web/DeckFlow.Web.csproj
- **Commit:** 9ef21288 (same Task 2 commit; fixed before committing)

**2. [Rule 1 - Bug] Dangling cref in TwoCardCombo.cs XML doc**
- **Found during:** Task 1 DeckFlow.Core build
- **Issue:** `<see cref="BracketClassifier"/>` in TwoCardCombo.cs caused CS1574 warning because BracketClassifier didn't exist yet (Task 3)
- **Fix:** Changed to `<c>BracketClassifier</c>` (plain text code element, not a cref)
- **Files modified:** DeckFlow.Core/Bracket/TwoCardCombo.cs
- **Commit:** ecdcdcb4 (same Task 1 commit; fixed before committing)

## Known Stubs

None — this plan is pure Core logic with no data sources or UI rendering. No stub patterns were introduced.

## Threat Flags

None — all output is public deck classification data. No new network endpoints, auth paths, file access patterns (beyond the already-planned JSON seed), or schema changes at trust boundaries introduced in this plan.

## TDD Gate Compliance

- RED gate: `test(76-01): add failing BracketClassifier unit tests (RED)` — commit `eca24eb0`
- GREEN gate: `feat(76-01): BracketClassifier pure static classifier (GREEN)` — commit `89ccf0b4`
- No REFACTOR pass needed (implementation was clean on first pass)

## Self-Check: PASSED
