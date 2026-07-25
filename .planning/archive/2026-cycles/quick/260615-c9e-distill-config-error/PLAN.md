---
quick_id: 260615-c9e
slug: distill-config-error
type: quick
date: 2026-06-15
follow_up_to: phase 45-04 (HARV-05)
description: Surface a clear "distiller CLI not configured" abort instead of N silent per-video "distill failed" lines when DECKFLOW_LLM_CLI_COMMAND is missing/invalid
files_modified:
  - DeckFlow.Core/Integration/LlmCliConfigurationException.cs (new)
  - DeckFlow.Core/Integration/CliLlmDistillationService.cs
  - DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs
  - DeckFlow.Core.Tests/Integration/CliLlmDistillationServiceTests.cs (existing or new — config-error coverage)
  - DeckFlow.Core.Tests/Orchestration/DistillConfigAbortTests.cs (new)
---

# Quick Task: clear distill CLI-config error

## Problem

When the claude CLI distiller is misconfigured (on Windows `DECKFLOW_LLM_CLI_COMMAND` unset, or set to
invalid JSON / missing the `{instruction}` placeholder), every video fails with a bare
`distill failed <id>` line and the result shows "Failed: N, LLM calls: 0" with no reason. The operator
cannot tell a config problem from a content problem. The underlying exception carries a precise,
actionable message but it is swallowed by the per-video failure path.

## Decision

Throw a typed `LlmCliConfigurationException` for CLI-config problems and, in the orchestrator, convert
it into a single run ABORT with a clear `AbortedReason` (Studio already renders AbortedReason in a red
alert) — instead of marking every video Failed.

## Key facts (verified)

- `CliLlmDistillationService.BuildCommandSpec(instruction)` runs ONCE before the retry loop
  (`ExtractWithRetryAsync` line ~200), so a config exception propagates UNWRAPPED to the caller — it is
  not retried or wrapped in "CLI extraction failed after N attempts".
- `ContentKbOrchestrator` distill loop already aborts the whole run when a `DistillVideoOutcome` carries
  a non-null `AbortedReason` (sets `stopRun`); the cap-skip path uses this.

## Tasks

### Task 1 — typed exception + distiller throws

- New `DeckFlow.Core/Integration/LlmCliConfigurationException.cs`:
  `public sealed class LlmCliConfigurationException : Exception` with the standard ctors
  (`(string message)` and `(string message, Exception inner)`). Xmldoc the type.
- `CliLlmDistillationService`: change the CLI-COMMAND **configuration** throws from
  `InvalidOperationException` to `LlmCliConfigurationException`:
  - the Windows "must be set as a JSON array …" throw (~line 143),
  - the `BuildOverrideCommandSpec` validation throws: invalid-JSON (~159), empty-array (~166),
    null-element (~171), empty-executable (~176), placeholder-count/position (~186).
  Keep the help text (the `wsl.exe`/`cmd.exe` examples). Do NOT change the `NotSupportedException`
  for an unsupported provider (~126) or runtime process/exit-code errors in `RunProcessAsync` — those
  are not config errors.

### Task 2 — orchestrator: abort once with a clear reason

- `ContentKbOrchestrator.DistillVideoAsync`: add a catch for `LlmCliConfigurationException` BEFORE the
  existing general `catch (Exception … when (… not OperationCanceledException))`:
  - Do NOT set the video's distill status to Failed (it is not the video's fault).
  - Log it once (LogError), `progress?.Report($"distill aborted — distiller CLI not configured: {ex.Message}")`.
  - Return a new `DistillVideoOutcome.AbortedConfig(llmCalls, llmSpend, reason)` where
    `reason = $"Distiller CLI not configured: {ex.Message}"`.
- Add factory `DistillVideoOutcome.AbortedConfig(int llmCalls, decimal llmSpendUsd, string reason)`
  = `new(false, false, llmCalls, llmSpendUsd, FailedVideoId: null, AbortedReason: reason)`.
- The existing loop logic (`if (outcome.AbortedReason is not null) { abortedReason = …; stopRun = true; }`)
  then aborts the run on the first video, and the returned `DistillResult.AbortedReason` carries the
  clear message. No change to the loop needed.

### Task 3 — tests

- `CliLlmDistillationServiceTests` (extend existing if present, else new): assert
  `BuildOverrideCommandSpec`-class config errors throw `LlmCliConfigurationException` — e.g.
  `DECKFLOW_LLM_CLI_COMMAND` set to invalid JSON, and set to a valid array WITHOUT a `{instruction}`
  placeholder. (Drive via the public distill entrypoint with the env var set + restored in a finally,
  or via the existing test seam — match how current tests exercise this service.)
- New `DistillConfigAbortTests` (orchestrator): inject a distiller stub/override that throws
  `LlmCliConfigurationException` on the extraction call; run `DistillAsync(dryRun:false)` over ≥2 pending
  videos; assert the result `AbortedReason` is non-null and contains "not configured", `DistillFailed == 0`
  (NOT inflated to the video count), and `VideosDistilled == 0`. Use the existing orchestrator test
  harness / fakes (FakeOrchestratorStores etc.).

## Acceptance

- `dotnet build DeckFlow.sln` — 0 errors, 0 new warnings; both test projects build.
- New tests pass.
- Manual: with `DECKFLOW_LLM_CLI_COMMAND` unset on Windows, Run Distill shows ONE red
  "Distill aborted: Distiller CLI not configured: …" message (with the wsl.exe/cmd.exe example), not N
  "distill failed" lines; Failed count is 0.
- With the env var correctly set, distill works unchanged (already verified live: 1 distilled, 3 LLM calls).

## Out of scope

- Any change to the metered OpenAI HTTP distiller.
- A Studio settings UI for the CLI command (env-var configured; documented separately).
