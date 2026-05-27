# Plan 21-04 Summary

## What Built

- Added the `distill` CLI verb with `--db`, `--limit`, and `--dry-run`.
- Added `RunDistillAsync` as a sibling of harvest, composing enabled sources, source-scoped pending videos, durable `content_distill_status`, three separate LLM distillation calls, per-call LLM spend ledgering, artifact file emission, slim-index upsert, and the overloaded `content_harvest_runs` run record.
- Added `content-source-set-enabled` and `RunContentSourceSetEnabledAsync` for enable/disable-only source management.
- Added `RunDistillAsyncTests` with fake `ILlmDistillationService` and stores. No test makes a live OpenAI call.

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~RunDistillAsync"`: Passed, 9 tests, 0 failed.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug`: Passed, 0 warnings, 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" run --project DeckFlow.CLI -- distill --help`: Passed; help includes `--dry-run`.
- Confirmed the full `DeckFlow.Core.Tests` project compiles as part of the solution build.
- Confirmed no new `PackageReference` diff and no edits inside the existing harvest method bodies.

## Deviations

- No scope deviations were made. Only the allowed implementation/test files and this summary were changed.
- Live distillation was not run. `OPENAI_API_KEY` is not set and live spend is gated by the separate human-verify UAT checkpoint.

## Follow-Ups

- Human UAT remains pending: run dry-run first, then a real capped distill over the UAT database, verify artifacts/index/run record/per-call ledger rows/status behavior, sample E5/E6 quality, and confirm source-disable behavior.
