# Phase 21 Plan 01 Summary

## What Was Built

- `DeckFlow.Core/Knowledge/DistillationResults.cs`
  - Added `TokenUsage`, `SummaryResult`, `ClipItem`, `ClipsResult`, and `TagsResult`.
  - Each top-level call result carries per-call `TokenUsage` from the OpenAI completion.
- `DeckFlow.Core/Knowledge/DistillationSchemas.cs`
  - Added strict raw-string JSON schemas for summary, clips, and tags.
  - Schemas include `additionalProperties:false`, required properties, and nullable required `timestamp_seconds`.
- `DeckFlow.Core/Integration/ILlmDistillationService.cs`
  - Added pure async contract for `SummarizeAsync`, `ExtractClipsAsync`, and `InferTagsAsync`.
- `DeckFlow.Core/Integration/LlmDistillationService.cs`
  - Added `gpt-4o-mini` `ChatClient` adapter using the same OpenAI client construction seam as `WhisperTranscriptionService`.
  - Uses three isolated strict-`json_schema` calls, one per method.
  - Classifies refusal, truncation, unexpected finish reasons, missing content, null deserialization, and malformed JSON as failures.
  - Reads exact `completion.Usage.InputTokenCount` and `completion.Usage.OutputTokenCount` into `TokenUsage`.
  - Holds no persistence dependency and no USD price constants.
- `DeckFlow.Core.Tests/LlmDistillationServiceTests.cs`
  - Added SDK model-factory tests for happy-path parsing and token usage, refusal, truncation, garbage JSON, null JSON, and pure constructor surface.

## Verification

- Confirmed existing package reference:
  - `OpenAI 2.10.0`
- Required build:
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj`
  - Result: succeeded, 0 warnings, 0 errors.
- Required targeted test:
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~LlmDistillationService"`
  - Result: passed, 6 passed, 0 failed, 0 skipped.
- Plan solution build:
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug`
  - Result: succeeded, 0 warnings, 0 errors.
- Acceptance checks:
  - `sealed record` count in `DistillationResults.cs`: 5.
  - `TokenUsage Usage` count in `DistillationResults.cs`: 3.
  - `additionalProperties` count in `DistillationSchemas.cs`: 4.
  - `timestamp_seconds` count in `DistillationSchemas.cs`: 2.
  - `LlmDistillationService.cs` contains `new ChatClient`, `using OpenAI.Chat`, refusal/finish guards, `.Usage`, `TokenUsage`, and `maxRetries: 0`.
  - No `DeckFlow.Core.csproj` package diff.
  - No persistence dependencies or USD price constants in `LlmDistillationService.cs`.

## Deviations

- No deviations from the plan.
- The test helper narrowly suppresses `OPENAI001` around `OpenAIChatModelFactory.ChatCompletion` because the SDK marks model factories as evaluation/test-only.

## Follow-Ups

- Plan 04 still owns persistence, LLM spend ledger recording, tag allowlist filtering, and artifact/index orchestration.
