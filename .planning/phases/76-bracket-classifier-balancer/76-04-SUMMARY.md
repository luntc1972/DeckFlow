---
phase: 76-bracket-classifier-balancer
plan: "04"
subsystem: DeckFlow.Web.Services.Bracket + DeckFlow.Web.Tests.Bracket
tags: [bracket, orchestration, spellbook, combo-null-disclosure, tdd]
dependency_graph:
  requires:
    - 76-01 (BracketClassifier, TwoCardCombo, BracketClassification, GameChangerCatalog)
    - 76-02 (IGameChangerCatalogService, GameChangerCatalogService)
    - 76-03 (BracketPromptVariantRegistry, IBracketPromptVariant, three decoupled variants)
  provides:
    - IBracketClassificationService + BracketClassificationResult
    - BracketClassificationService (orchestration: load -> combos -> classify -> artifact)
  affects:
    - Phase 76 plan 05 (BracketController calls IBracketClassificationService.ClassifyAsync)
tech_stack:
  added: []
  patterns:
    - Internal-ctor + factory-lambda DI registration (analog: DeckPrimerPacketService)
    - Null-spellbook preserved as null TwoCardCombos (BRACKET-03 / Pitfall 1)
    - Two-card-only combo mapping (CardNames.Count==2 filter at the Web orchestrator boundary)
    - Private nested fake doubles in test class (analog: ManabaseAnalysisServiceTests)
key_files:
  created:
    - DeckFlow.Web/Services/Bracket/IBracketClassificationService.cs
    - DeckFlow.Web/Services/Bracket/BracketClassificationService.cs
    - DeckFlow.Web.Tests/Bracket/BracketClassificationServiceTests.cs
  modified:
    - DeckFlow.Web/Program.cs (AddScoped<IBracketClassificationService> factory registration)
decisions:
  - "BracketClassificationService constructor is internal (not public) because BracketPromptVariantRegistry is internal; DI uses a factory lambda — mirrors DeckPrimerPacketService pattern"
  - "SpellbookCombo->TwoCardCombo mapping lives exclusively in the Web orchestrator (BracketClassificationService); DeckFlow.Core stays free of DeckFlow.Web reference per 76-01 decision"
  - "null spellbook result preserved as null TwoCardCombos (not empty list); BracketClassifier interprets null as ComboDetectionAvailable=false without any B4 gate"
  - "Three-card combos filtered out at the mapping stage (CardNames.Count==2 predicate); BracketClassifier only sees two-card combos"
  - "MaxDeckSourceChars=100_000 matches ManabaseAnalysisService abuse cap; mirrors same rationale"
metrics:
  duration_minutes: 20
  completed_date: "2026-06-28"
  tasks_completed: 2
  files_changed: 4
---

# Phase 76 Plan 04: BracketClassificationService Orchestration Summary

Orchestrating service wiring the full classification pipeline: deck load via IDeckEntryLoader, two-card combo detection via ICommanderSpellbookService (null = unavailable, never zero combos), classification via BracketClassifier, and paste artifact build via BracketPromptVariantRegistry. Five service tests cover null-combo disclosure, two-card gating, three-card exclusion, empty-source guard, and platform artifact build.

## What Was Built

### Task 1: IBracketClassificationService + BracketClassificationService + DI

**IBracketClassificationService** (`DeckFlow.Web/Services/Bracket/IBracketClassificationService.cs`):
- Single method `ClassifyAsync(deckSource, targetBracketNumber, platform, deckName, ct)`
- `BracketClassificationResult` sealed record: Classification, Tiers, PromptArtifact, TargetBracketNumber, ImportWarning

**BracketClassificationService** (`BracketClassificationService.cs`):
- `internal` constructor (not `public`) because `BracketPromptVariantRegistry` is `internal sealed class` — same accessibility constraint as `DeckPrimerPacketService`
- Guards: `string.IsNullOrWhiteSpace` throws `InvalidOperationException("Enter a deck URL or paste a deck list.")`, length > 100_000 throws, empty entries throws `"That deck looks empty."`
- `DeckParseException` is caught and re-thrown as `InvalidOperationException` (user-facing copy, no 500)
- Critical null-combo path: `comboResult is null ? null : comboResult.IncludedCombos.Where(c => c.CardNames.Count == 2).Select(c => new TwoCardCombo(...)).ToList()` — null preserved as null (BRACKET-03)
- Artifact built via `_registry.Build(AiPlatform.Normalize(platform), ...)`
- Structured logging (no string interpolation)

**DI registration** (`Program.cs`):
- Factory lambda: `AddScoped<IBracketClassificationService>(sp => new BracketClassificationService(...))` resolving all five dependencies via `sp.GetRequiredService<T>()` / `sp.GetService<T>()` for optional logger
- `using DeckFlow.Web.Services.PromptBuilders.Bracket;` added for `BracketPromptVariantRegistry` resolution

### Task 2: BracketClassificationService tests (TDD)

**BracketClassificationServiceTests** (`DeckFlow.Web.Tests/Bracket/BracketClassificationServiceTests.cs`):

| Test | What it asserts |
|------|-----------------|
| `ClassifyAsync_NullSpellbook_SetsComboDetectionAvailableFalse` | null API result → ComboDetectionAvailable=false, TwoCardCombos=null, artifact contains "combo detection" disclosure, NOT "0 two-card combos" |
| `ClassifyAsync_TwoCardCombo_GatesB4` | 2-card combo → BracketNumber>=4, ComboDetectionAvailable=true |
| `ClassifyAsync_ThreeCardCombo_NotCountedAsTwoCardGate` | 3-card combo → BracketNumber<4 (filtered out), TwoCardCombos empty |
| `ClassifyAsync_EmptySource_Throws` | whitespace deckSource → InvalidOperationException |
| `ClassifyAsync_BuildsArtifactForPlatform` | Claude platform → non-empty artifact, contains "WHY THIS BRACKET" |

Test doubles (all private nested classes in the test file):
- `FakeDeckEntryLoader` — returns fixed 5-card fixture deck
- `FakeSpellbookService` — parameterized: returns null, a 2-card combo result, or a 3-card combo result
- `FakeGameChangerCatalogService` — returns hand-built catalog with zero GC cards (so combo is the only B4 signal in combo tests)

```
Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5
```

Full suite after both tasks: 975 passed, 12 skipped (Postgres integration), 0 failed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] BracketPromptVariantRegistry internal accessibility conflict**
- **Found during:** Task 1 build verification
- **Issue:** `BracketClassificationService` was `public sealed class` with a `public` constructor taking `BracketPromptVariantRegistry` (which is `internal sealed class`). CS0051 "Inconsistent accessibility" build error.
- **Fix:** Changed `BracketClassificationService` constructor from `public` to `internal`; changed DI registration from `AddScoped<IBracketClassificationService, BracketClassificationService>()` to a factory lambda `AddScoped<IBracketClassificationService>(sp => new BracketClassificationService(...))`. This exactly mirrors the `DeckPrimerPacketService` pattern (also internal-ctor + factory-lambda).
- **Files modified:** `BracketClassificationService.cs`, `Program.cs`
- **Commit:** `cd1d103f` (fixed before committing)

**2. [Rule 3 - Blocking] Missing namespace import for BracketPromptVariantRegistry in Program.cs**
- **Found during:** Task 1 build verification (second build after fix 1)
- **Issue:** CS0246 — `BracketPromptVariantRegistry` in the factory lambda requires `using DeckFlow.Web.Services.PromptBuilders.Bracket;` which was not present in Program.cs
- **Fix:** Added the missing `using` directive
- **Files modified:** `Program.cs`
- **Commit:** `cd1d103f` (fixed before committing)

## Known Stubs

None — this plan builds an orchestration service with real data plumbing (load + classify + artifact). No placeholder data sources, no TODO content, no UI rendering with empty data.

## Threat Flags

None — no new network endpoints or auth paths introduced. Security surface for this plan:

- **T-76-08 (SSRF):** Mitigated — URL imports flow through existing `IDeckEntryLoader` / `DeckSourceHost` allow-list; `BracketClassificationService` never fetches URLs itself.
- **T-76-09 (DoS / oversized paste):** Mitigated — `MaxDeckSourceChars=100_000` guard before `LoadFromSourceAsync`.
- **T-76-10 (malicious Spellbook JSON):** Mitigated — `CommanderSpellbookService` already handles parse errors and returns null; service treats null as unavailable (not zero combos).

## Self-Check: PASSED

Files created/exist:
- DeckFlow.Web/Services/Bracket/IBracketClassificationService.cs — FOUND
- DeckFlow.Web/Services/Bracket/BracketClassificationService.cs — FOUND
- DeckFlow.Web.Tests/Bracket/BracketClassificationServiceTests.cs — FOUND

Commits confirmed:
- `cd1d103f` — feat(76-04): IBracketClassificationService + BracketClassificationService + DI
- `563158e6` — test(76-04): BracketClassificationService tests — null-combo disclosure + 2-card gating

Source assertions:
- `grep -c "Count == 2" BracketClassificationService.cs` → 1
- `grep -c "AddScoped<IBracketClassificationService" Program.cs` → 1

Test run: 5/5 PASS
Full Web suite: 975 passed, 0 failed
