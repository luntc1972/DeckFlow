---
phase: 77-multi-axis-deck-score
plan: 04
subsystem: analysis
tags: [deck-score, multi-axis, feature-flag, packet-service, score-roundtrip, byte-identity]

# Dependency graph
requires:
  - phase: 77-01
    provides: DeckStatSummary.Tutors/FastMana/RampDrawUnderThreeMv/Counters signals
  - phase: 77-02
    provides: DeckMultiAxisScore record + MultiAxisScorer.Score + BandLabel + bracket cross-check
  - phase: 77-03
    provides: scoreBlockText trailing param threaded through IAnalysisPromptVariant + registry + 3 variants
provides:
  - analysis.multi-axis-score flag (catalog + seeded OFF in both dialects)
  - DeckAnalysisPacketService score computation behind the flag (stats + bracket classify + scorer)
  - BuildScoreBlockText paste-safe ASCII artifact (UI-SPEC §10) folded into all three variants
  - ScoreJson hidden-field round-trip across the Step-3 early-return (untrusted-input hardened)
  - DeckAnalysisViewModel.Score surfaced for the view (rendering lands in 77-05)
affects: [77-05 (renders Model.Score in DeckAnalysis.cshtml + CSS), 77-06 (README + theme/mobile verify)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Default-OFF flag read via explicit _flagCache.Snapshot().TryGetValue (never IsEnabled default-on)"
    - "Widen a SINGLE existing async gate to serve a new consumer — reuse the one fetch, never double-call"
    - "Untrusted hidden-field round-trip: length-cap + typed deserialize in try/catch -> null, no eval/reflect"
    - "OFF-path byte-identity preserved by gating the prompt-side combo arg separately from the widened fetch"

key-files:
  created:
    - DeckFlow.Web.Tests/DeckAnalysisScoreBlockTextTests.cs
    - DeckFlow.Web.Tests/DeckAnalysisScorePersistenceTests.cs
  modified:
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
    - DeckFlow.Web/Models/DeckAnalysisRequest.cs
    - DeckFlow.Web/Models/DeckAnalysisViewModel.cs
    - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
    - DeckFlow.Web/Controllers/DeckPacketController.cs
    - DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs
    - DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs
    - DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs
    - DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs
    - DeckFlow.Web.Tests/Extensions/DiCompositionExtensionsTests.cs
    - DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs

key-decisions:
  - "Flag seeded OFF in BOTH Postgres (FALSE) and SQLite (0) via the existing idempotent ON CONFLICT DO NOTHING block; lockstep seed + catalog test guards updated with the new key"
  - "The ONE combo gate was widened to (scoreEnabled || RequiresComboLookup) so Commander Spellbook is fetched exactly once and reused for BOTH prompt text and the score's combo-density signal — no second comboForScoreTask (Codex HIGH avoided)"
  - "Prompt output stays byte-identical when OFF: the prompt receives promptComboResult = RequiresComboLookup ? comboResult : null, so widening the fetch for the score never injects combo text the OFF path would not emit; the Commander Spellbook timing row also stays gated on the prompt requirement"
  - "comboDetectionAvailable = comboResult is not null is threaded to the scorer so a null combo result (API down) is never mistaken for zero combos (Pitfall 1)"
  - "ScoreJson is untrusted client input: deserialized only into the typed DeckMultiAxisScore inside a length-capped (8192) try/catch that returns null on malformed/oversized input (threat T-77-04-01)"
  - "IGameChangerCatalogService injected as a new ctor param (DI-registered since Phase 76); DeckStatAggregator.Compute is called independently of the deck-stats flag (Pitfall 5)"

patterns-established:
  - "Pattern: end-to-end flag-gated feature with byte-identical OFF output proven by the full existing byte-identity suite passing unchanged"

metrics:
  duration: ~40 min
  completed: 2026-06-29
---

# Phase 77 Plan 04: Multi-Axis Score Packet Wiring Summary

Wired the multi-axis deck score end-to-end behind a default-OFF `analysis.multi-axis-score` flag: registered + seeded the flag in both dialects, computed the score inside `DeckAnalysisPacketService.BuildAsync` (deck stats + bracket classification + `MultiAxisScorer.Score`), built the paste-safe ASCII `BuildScoreBlockText` artifact and threaded it into all three analysis prompt variants, round-tripped the score across the Step-3 early-return via a hardened `ScoreJson` hidden field, and surfaced `Score` on the view model for the upcoming view work. When the flag is OFF the analysis packet and all three artifacts are byte-identical to baseline (proven by the entire existing byte-identity suite passing unchanged).

## What Was Built

### Task 1 — Register the flag (catalog + seed both dialects) (`6203ee9e`)
- `FeatureFlagCatalog`: added the `["analysis.multi-axis-score"]` operator description ("four-axis Power/Speed/Control/Consistency score block ... Off = byte-identical").
- `FeatureFlagStore`: appended `('analysis.multi-axis-score', FALSE)` (Postgres) and `('analysis.multi-axis-score', 0)` (SQLite) before each block's `ON CONFLICT (key) DO NOTHING` line, fixing the trailing comma so the prior-last row gains a comma. Seeded OFF, idempotent.
- Lockstep guards kept green: `[InlineData("analysis.multi-axis-score", false)]` added to `FeatureFlagStoreSeedTests`; `[InlineData("analysis.multi-axis-score")]` added to `FeatureFlagCatalogTests`.
- `FeatureFlag`-filter tests: 51 passed.

### Task 2 — ScoreJson round-trip field + Score view-model property (`6429771d`)
- `DeckAnalysisRequest.ScoreJson` (backing field + null-guard setter), XML-documented as the Step-3 round-trip carrier, placed immediately after `DeckProfileJson`.
- `DeckAnalysisViewModel.Score` (`DeckMultiAxisScore? { get; init; }`, server-computed, never form-bound), with `using DeckFlow.Core.Analysis;`.

### Task 3 — Compute score, build block text, round-trip, wire controller (`59cc42c3`)
- Injected `IGameChangerCatalogService _catalogService` (DI registration + `TestServiceFactory` updated).
- `internal const string MultiAxisScoreFlag`; `scoreEnabled` read via the explicit `_flagCache.Snapshot().TryGetValue` default-OFF pattern.
- **Single combo fetch, reused:** widened the existing `comboTask` gate to `(scoreEnabled || requiresComboLookup)`. The one `comboResult` feeds both the score and (only when `requiresComboLookup`) the prompt; `grep -c FindCombosAsync` on the service is unchanged from baseline.
- Score computation: `DeckStatAggregator.Compute` over current-deck non-commander references (mirrors `BuildDeckStatsText`), combos mapped to `TwoCardCombo`, `BracketClassifier.Classify`, then `MultiAxisScorer.Score(stats, DetectedGameChangers.Count, twoCardComboCount, comboDetectionAvailable, BracketNumber)`.
- `internal static BuildScoreBlockText(DeckMultiAxisScore)`: header + four aligned `Axis: N/5 Label (rationale)` lines (via `MultiAxisScorer.BandLabel`) + cross-check line + heuristic disclaimer, ASCII only.
- `scoreBlockText` passed as the trailing arg through `BuildAnalysisPrompt` -> registry -> variants (77-03 contract).
- `DeckAnalysisPacketResult.Score` added (trailing optional); set on the Step-2/score path and restored from `ScoreJson` (via `TryDeserializeScore`) on the Step-3 early-return.
- `TryDeserializeScore`: length-cap (8192) + typed deserialize in try/catch -> null; never throws/reflects.
- `DeckPacketController`: both `DeckAnalysisViewModel`-from-packet paths set `Score = result.Score` and write `request.ScoreJson = JsonSerializer.Serialize(result.Score)` when non-null so the hidden field carries the score into Step-3.
- Tests: `DeckAnalysisScoreBlockTextTests` (header/axes/cross-check/disclaimer, band figures + labels, per-axis rationale carry, ASCII-safe no em/en dash), `DeckAnalysisScorePersistenceTests` (Step-3 valid round-trip, malformed theory -> null, oversized -> null), and a single-fetch guard proving Commander Spellbook is called exactly once when both the score flag and a combo question are active.

## Verification

- `dotnet.exe build DeckFlow.Web`: 0 warnings, 0 errors.
- `dotnet.exe build DeckFlow.Web.Tests`: 0 warnings, 0 errors.
- New score tests: `DeckAnalysisScoreBlockText` 4 pass; `Step3EarlyReturn` round-trip 6 pass; single-fetch guard 1 pass.
- `FeatureFlag`-filter tests: 51 pass; `DiCompositionExtensions` ValidateOnBuild: 1 pass.
- Full `dotnet.exe test DeckFlow.Web.Tests`: 1008 passed, 12 skipped (Postgres integration), 0 failed — the entire existing byte-identity suite green proves the OFF path is unchanged.
- Changed-lines format gate (`scripts/format-check-changed.sh staged`): clean.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Registered IGameChangerCatalogService in two hand-rolled test compositions**
- **Found during:** Task 3 (DeckFlow.Web.Tests build/run)
- **Issue:** `DeckAnalysisPacketService` now requires `IGameChangerCatalogService`. `TestServiceFactory.CreateDeckAnalysisPacketService` (compile error) and `DiCompositionExtensionsTests` ValidateOnBuild (runtime resolution failure) hand-build the graph and did not supply it.
- **Fix:** Added an `EmptyGameChangerCatalogService` to `TestServiceFactory`, and registered the real `GameChangerCatalogService` in the DI composition test (mirroring the existing inline-registered `IDeckEntryLoader`/`ICategoryKnowledgeStore` stubs — the catalog is registered inline in `Program.cs`, not in any extension group).
- **Files modified:** `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs`, `DeckFlow.Web.Tests/Extensions/DiCompositionExtensionsTests.cs`
- **Commit:** `59cc42c3`

**2. [Rule 3 - Blocking] Placed the Step-3 persistence tests as a partial of the existing test class**
- **Found during:** Task 3 (test authoring)
- **Issue:** The plan names a standalone `DeckAnalysisScorePersistenceTests.cs`, but exercising `BuildAsync`'s Step-3 round-trip requires the full `CreateService` fake graph (~10 collaborators) which lives as private nested fakes inside `DeckAnalysisPacketServiceTests`. Duplicating that graph would be a large, drift-prone copy.
- **Fix:** Created `DeckAnalysisScorePersistenceTests.cs` as `public sealed partial class DeckAnalysisPacketServiceTests` (one-word `partial` added to the existing declaration) so the new file reuses `CreateService`. The file exists as planned; the tests share the established harness (DRY).
- **Files modified:** `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs` (added `partial`), `DeckFlow.Web.Tests/DeckAnalysisScorePersistenceTests.cs` (new)
- **Commit:** `59cc42c3`

## Known Stubs

- `DeckAnalysisViewModel.Score` is populated by the controller but is **not yet rendered** in `DeckAnalysis.cshtml`. This is intentional and scoped to plan **77-05** (view block + CSS, UI-SPEC §1/§4). The core value of this plan — the score inside the paste artifact for all three AI variants — is fully delivered via `BuildScoreBlockText`; only the on-page visual render is deferred. Not a blocking stub.

## Threat Flags

None beyond the plan's registered threats. The single new untrusted surface (`request.ScoreJson` deserialized at the Step-3 early-return, T-77-04-01) is mitigated as planned: typed-only deserialize, length-capped, try/catch -> null, no eval/reflect — covered by the malformed/oversized persistence tests.

## Self-Check: PASSED
- FOUND: DeckFlow.Web.Tests/DeckAnalysisScoreBlockTextTests.cs
- FOUND: DeckFlow.Web.Tests/DeckAnalysisScorePersistenceTests.cs
- FOUND commit 6203ee9e (Task 1)
- FOUND commit 6429771d (Task 2)
- FOUND commit 59cc42c3 (Task 3)
