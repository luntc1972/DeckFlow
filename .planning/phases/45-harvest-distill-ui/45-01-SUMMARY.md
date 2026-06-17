---
phase: 45-harvest-distill-ui
plan: "01"
subsystem: Core
tags: [spend-ledger, orchestrator, redistill, core-enabler]
dependency_graph:
  requires: []
  provides: [ILlmSpendLedger.GetMonthlyCapUsd, IDistillOrchestrator.redistill]
  affects: [DeckFlow.Core, DeckFlow.CLI, DeckFlow.Core.Tests]
tech_stack:
  added: []
  patterns: [interface-extension, template-method-promotion, defaulted-parameter, in-memory-fake-store]
key_files:
  created:
    - DeckFlow.Core.Tests/Orchestration/ContentKbOrchestratorDistillTests.cs
  modified:
    - DeckFlow.Core/Content/ILlmSpendLedger.cs
    - DeckFlow.Core/Content/SpendLedgerBase.cs
    - DeckFlow.Core/Orchestration/IDistillOrchestrator.cs
    - DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs
    - DeckFlow.CLI/ContentKbCommandRunners.cs
    - DeckFlow.Core.Tests/LlmSpendLedgerTests.cs
    - DeckFlow.Core.Tests/Orchestration/FakeOrchestratorStores.cs
    - DeckFlow.Core.Tests/Orchestration/ThrowingOrchestratorDependencies.cs
    - DeckFlow.Core.Tests/RunDistillAsyncTests.cs
decisions:
  - "redistill param inserted between isSubscriptionProvider and videoIds; CLI caller updated to named args to preserve correct binding"
  - "redistill=true clears child rows via ClearDistillOutputAsync before re-distilling; status reset is implicit (DistillVideoAsync re-sets to distilled on success)"
  - "Reset status constant: null (ClearDistillOutputAsync removes child rows; distill_status is reset by ClearDistillOutputAsync removing old status via child-row delete only; DistillVideoAsync re-writes it to distilled on success)"
  - "FakeLlmSpendLedger in FakeOrchestratorStores and RunDistillAsyncTests updated to implement GetMonthlyCapUsd; ThrowingLlmSpendLedger updated to throw"
  - "DistillTestVideoStore created as standalone IContentVideoStore (FakeContentVideoStore is sealed, cannot inherit)"
metrics:
  duration: "~20 minutes"
  completed: "2026-06-15"
  tasks_completed: 3
  files_changed: 9
---

# Phase 45 Plan 01: Core Enabler — Spend Ledger Cap Getter + Redistill Flag

**One-liner:** Exposed `ILlmSpendLedger.GetMonthlyCapUsd()` for page display and wired a defaulted `redistill` force flag into `IDistillOrchestrator.DistillAsync` that clears prior output and re-processes targeted already-distilled videos.

## What Was Built

This Core-only enabler wave adds two new surfaces without breaking any existing callers:

1. **`ILlmSpendLedger.GetMonthlyCapUsd()`** — synchronous cap read so the Harvest page can display "Cap: $X.XX / Remaining: $Y.YY" at render time without an async call. Implemented by promoting `SpendLedgerBase.ReadMonthlyCapUsd()` from `private` to `protected`, then adding `public decimal GetMonthlyCapUsd() => ReadMonthlyCapUsd()` on the base class. `LlmSpendLedger` and `WhisperSpendLedger` inherit the implementation without any file changes.

2. **`IDistillOrchestrator.DistillAsync(redistill = false)`** — defaulted boolean inserted between `isSubscriptionProvider` and `videoIds`. When `redistill=true` and the video's natural key is in `requestedKeys`, the already-distilled `continue` is bypassed: on a live run, `ClearDistillOutputAsync` is called to remove old child rows before re-distilling; on a dry run, the video is counted in `WouldRun`. Distilled videos NOT in the targeted `videoIds` set are still skipped (T-45-15 / targeted guard).

3. **xUnit test coverage** — three new `[Fact]` tests in `LlmSpendLedgerTests` (cap default, resolver override, D-03 mechanism proof) and two new tests in `ContentKbOrchestratorDistillTests` (default-skip regression, force-redistill WouldRun=1).

## Decisions Made

### redistill parameter placement
Inserted between `isSubscriptionProvider` and `videoIds` as planned. All existing callers already used named arguments (`videoIds:`, `progress:`, `cancellationToken:`), so none required a source change except the CLI caller (which used positional args). The CLI call was updated to named form: `videoIds: videoIds, progress: new ConsoleOrchestratorProgress(), cancellationToken: ct`. No positional binding was broken.

### Reset status on redistill
`ClearDistillOutputAsync` removes summary/clip/tag child rows but does NOT clear `distill_status`. After clearing child rows, the code falls through to the existing `DistillVideoAsync` path, which re-sets the status to `"distilled"` on success. No explicit status-reset call is needed because `SetDistillStatusAsync` only accepts the four valid terminal statuses (distilled/skipped_over_cap/failed/filtered) — null is not settable through the interface. The pre-clear of child rows is sufficient.

### DistillTestVideoStore (standalone, not inheriting FakeContentVideoStore)
`FakeContentVideoStore` is `sealed`, so a separate `DistillTestVideoStore` implementing `IContentVideoStore` was created in the test file. It has controllable `GetDistillStatusAsync` (returns from a dictionary), tracks `ClearDistillOutputCalled`, and implements all required members with `throw new NotImplementedException()` for paths not exercised by the redistill tests.

## Deviations from Plan

### Auto-fixed: FakeLlmSpendLedger implementations (Rule 2 — missing critical functionality)
Adding `GetMonthlyCapUsd()` to `ILlmSpendLedger` required updating three fake/throwing implementations to satisfy the interface contract:
- `FakeLlmSpendLedger` in `FakeOrchestratorStores.cs` — returns `15.00m`
- `FakeLlmSpendLedger` in `RunDistillAsyncTests.cs` — returns `15.00m`
- `ThrowingLlmSpendLedger` in `ThrowingOrchestratorDependencies.cs` — throws `InvalidOperationException` (consistent with the Throwing pattern)

These are in-test files and touch no production logic. Committed in the Task 1 commit.

## Test Results

| Test suite | Filter | Result |
|---|---|---|
| `LlmSpendLedgerTests` | `FullyQualifiedName~LlmSpendLedgerTests` | 9/9 Passed |
| `ContentKbOrchestratorDistillTests` | `FullyQualifiedName~ContentKbOrchestratorDistillTests` | 2/2 Passed |
| Full solution build | `dotnet build DeckFlow.sln` | Build succeeded — 0 errors, 0 warnings |

WSL VSTest was functional for this run; no fallback to build-only was needed.

## Known Stubs

None — this plan adds no UI, no placeholder data, and no wired-but-empty paths.

## Threat Flags

No new network endpoints, auth paths, file access patterns, or schema changes introduced.
The `GetMonthlyCapUsd()` read surface is read-only and non-persistent (T-45-01 mitigated: getter reads env/resolver only, no write path).
The redistill bypass is guarded by `redistill && requestedKeys is not null && requestedKeys.Contains(naturalKey)` (T-45-15 mitigated: targeted, never blanket).

## Self-Check: PASSED

- `DeckFlow.Core/Content/ILlmSpendLedger.cs` — FOUND
- `DeckFlow.Core/Content/SpendLedgerBase.cs` — FOUND
- `DeckFlow.Core/Orchestration/IDistillOrchestrator.cs` — FOUND
- `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` — FOUND
- `DeckFlow.Core.Tests/LlmSpendLedgerTests.cs` — FOUND
- `DeckFlow.Core.Tests/Orchestration/ContentKbOrchestratorDistillTests.cs` — FOUND
- Commit 5c20d21 (Task 1) — FOUND
- Commit 2668225 (Task 2) — FOUND
- Commit 96fd9c5 (Task 3) — FOUND
