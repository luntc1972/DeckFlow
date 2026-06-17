---
quick_id: 260615-c9e
slug: distill-config-error
date: 2026-06-15
status: complete
commits:
  - 094c5a8
  - cc16da0
files_modified:
  - DeckFlow.Core/Integration/LlmCliConfigurationException.cs (new)
  - DeckFlow.Core/Integration/CliLlmDistillationService.cs
  - DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs
  - DeckFlow.Core.Tests/CliLlmDistillationServiceTests.cs
  - DeckFlow.Core.Tests/Orchestration/DistillConfigAbortTests.cs (new)
---

# Quick Task 260615-c9e: clear distill CLI-config error — Summary

**One-liner:** Typed `LlmCliConfigurationException` replaces bare `InvalidOperationException` for CLI config errors; the orchestrator converts the first throw into a single run-abort instead of N silent "distill failed" lines.

## What Was Done

### Task 1+2 (commit 094c5a8)

- Added `LlmCliConfigurationException` (sealed, two ctors, xmldoc).
- `CliLlmDistillationService`: 6 config throws converted to `LlmCliConfigurationException`: Windows "must be set" + 5 `BuildOverrideCommandSpec` validation throws (invalid JSON, empty array, null element, blank executable, wrong placeholder count/position). `NotSupportedException` and runtime `RunProcessAsync` errors unchanged.
- `ContentKbOrchestrator.DistillVideoAsync`: new `catch (LlmCliConfigurationException ex)` placed BEFORE the general `catch (Exception …)`. Does NOT set video status to Failed; logs once; reports to progress; returns `DistillVideoOutcome.AbortedConfig(llmCalls, llmSpend, reason)`.
- Added `DistillVideoOutcome.AbortedConfig` factory (`FailedVideoId: null` — DistillFailed not incremented).

### Task 3 (commit cc16da0)

- `CliLlmDistillationServiceTests`: 4 existing config-error facts now assert `LlmCliConfigurationException` (not `InvalidOperationException`).
- New `DistillConfigAbortTests` (3 facts): `ConfigErrorLlmDistillationService` stub + `TrackingDistillTestVideoStore`; covers AbortedReason non-null, DistillFailed==0, VideosDistilled==0, video not marked Failed, exception message propagated.

## Build Results

- `DeckFlow.Core.Tests`: 0 errors, 0 new warnings
- `DeckFlow.Web.Tests`: 0 errors, 0 new warnings
- Full solution: 0 compiler errors; 2 MSB file-lock warnings from running Studio (pre-existing)
- WSL VSTest unreliable; tests verified by structural review (per CLAUDE.md)

## Deviations

None — executed exactly as planned.

## Self-Check: PASSED

- LlmCliConfigurationException.cs: EXISTS (commit 094c5a8)
- CliLlmDistillationService.cs: MODIFIED (6 throws changed)
- ContentKbOrchestrator.cs: MODIFIED (AbortedConfig factory + catch block)
- CliLlmDistillationServiceTests.cs: MODIFIED (4 facts use typed exception)
- DistillConfigAbortTests.cs: EXISTS (commit cc16da0)
