---
status: complete
plan: 19-03
phase: 19-content-kb-foundation-local-schema-contracts
---

# GSD Summary

## key-files

- `DeckFlow.Web/Services/Content/IContentSourceStore.cs` - created source-store contract.
- `DeckFlow.Web/Services/Content/ContentSourceStore.cs` - created local Content KB source store with dual SQLite/Postgres DDL.
- `DeckFlow.Web.Tests/ContentSourceStoreTests.cs` - created source-store idempotency, round-trip, and constraint tests.
- `DeckFlow.Web/Services/Content/IContentVideoStore.cs` - created video aggregate store contract.
- `DeckFlow.Web/Services/Content/ContentVideoStore.cs` - created local Content KB videos/transcripts/summaries/clips/tags aggregate store.
- `DeckFlow.Web.Tests/ContentVideoStoreTests.cs` - created idempotency, parent-first bootstrap, natural-key, duplicate-tag, and cascade-all-four-child-table tests.

## what-was-built

- `ContentSourceStore` creates `content_sources` idempotently with integer surrogate IDs, `source_type` CHECK values matching `ContentSourceType`, soft-disable `is_enabled` default true, `UNIQUE (source_url)`, and `UNIQUE (source_slug)`.
- `ContentSourceStore` exposes public `EnsureSchemaAsync`, `InsertSourceAsync`, `GetSourceAsync`, and `ListEnabledSourcesAsync`, using `DeckFlowDatabaseConnectionFactory.CreateLocalContentKbConnection` for DI and `_connectionInfo.OpenConnectionAsync` for connection opens.
- `ContentVideoStore` creates `content_videos`, `content_transcripts`, `content_summaries`, `content_clips`, and `content_tags` idempotently with dual DDL and `content_` table prefixes.
- `ContentVideoStore.EnsureSchemaAsync` constructs `ContentSourceStore` over the same connection and awaits its schema first before issuing child DDL, with an explicit REVIEW #1 / D-04 comment and parent-first test coverage.
- `content_videos` enforces at least one natural key through `CHECK (youtube_video_id IS NOT NULL OR rss_guid IS NOT NULL)`.
- All four child tables declare `REFERENCES content_videos(id) ON DELETE CASCADE`; `content_videos.source_id` declares `REFERENCES content_sources(id) ON DELETE CASCADE`.
- The D-04 SQLite proof test inserts transcript, summary, clip, and tag rows, deletes the video, and asserts all four child-table counts are zero.

## deviations

- Used `RETURNING id` for SQLite inserts rather than `last_insert_rowid()`; the plan allowed either and the current SQLite provider supports `RETURNING`.
- Added extra negative tests for invalid source type, duplicate source slug/url, missing video natural key, and duplicate video tag rows beyond the minimum named tests.
- Did not run the full `DeckFlow.Web.Tests` suite because the plan notes a pre-existing unrelated `AdminCssPhase1` failure; ran the requested content-test filter instead.

## Self-Check

- TDD red for Task 1: filtered Web test run failed on missing `DeckFlow.Web.Services.Content.ContentSourceStore`.
- Task 1 green: `ContentSourceStoreTests` passed 5/5; `set -o pipefail; "/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj 2>&1 | grep -E "Build succeeded|error"` returned `Build succeeded.`
- TDD red for Task 2: filtered Web test run failed on missing `ContentVideoStore`.
- Task 2 green: `ContentVideoStoreTests` passed 5/5; the same Web.Tests build grep returned `Build succeeded.`
- Final build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web` passed with 0 warnings and 0 errors.
- Final filtered tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~ContentSourceStoreTests|FullyQualifiedName~ContentVideoStoreTests" --no-restore` passed 10/10.
- Acceptance greps confirmed `_connectionInfo.OpenConnectionAsync`, `CreateLocalContentKbConnection`, `UNIQUE` constraints, CHECK literals, 8 total `REFERENCES content_videos(id) ON DELETE CASCADE` occurrences, `UNIQUE (video_id, dimension, tag_value)`, `skipped_over_cap`, and zero `CREATE TABLE IF NOT EXISTS harvest_runs` matches in `ContentVideoStore.cs`.

## relocation note

- Stores relocated to DeckFlow.Core (separate-app packaging).
