# Plan 21-03 Summary

## Built

- Added `ContentArtifactWriter` with `ToText`, `WriteFile`, and `ComputeRelativeArtifactPath`.
- Extended `ContentVideoStore` with source-scoped pending distill listing, latest transcript read, clean distill-output clearing, and durable per-video distill status.
- Added `content_distill_status` as a new `CREATE TABLE IF NOT EXISTS` table in both SQLite and Postgres DDL.
- Extended `ContentSourceStore` with `SetEnabledAsync`.
- Added focused xUnit coverage for artifact rendering/path writing, source enable toggles, source-scoped pending distill queries, distill-status upsert transitions, latest transcript reads, and clean reruns.
- Added the approved `content-kb/` `.gitignore` entry.

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj`: succeeded with 0 warnings and 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~ContentArtifactWriter|FullyQualifiedName~ContentSourceStoreSetEnabled|FullyQualifiedName~ContentVideoStoreDistill"`: Passed, Failed: 0, Passed: 12, Total: 12.
- `git check-ignore -q content-kb/anything.md && echo ignored`: `ignored`.
- Static checks passed:
  - writer references `ContentArtifactSpec` and does not reference `ContentVideoStore`, `content_clips`, or `SELECT`;
  - `IContentVideoStore` exposes all 5 new distill methods;
  - pending query is source-scoped;
  - `content_distill_status` appears in both DDL and read/write SQL;
  - no `ALTER TABLE` or `ADD COLUMN` was introduced;
  - `IContentSourceStore` exposes `SetEnabledAsync`.

## Deviations

- The plan asked to update existing test fake implementations, but the allowed file list did not include `DeckFlow.Core.Tests/CommandRunnerHarvestTests.cs`, where the existing fakes live. To preserve the absolute scope boundary, the new interface members have default throwing implementations while the real stores implement them. This keeps existing fakes compiling without touching an out-of-scope file.

## Follow-ups

- Plan 04 can consume the new store primitives and override any new fake behavior in its own in-scope tests.
