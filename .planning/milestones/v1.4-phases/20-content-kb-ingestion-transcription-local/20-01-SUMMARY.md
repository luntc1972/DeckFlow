# Plan 20-01 Execution Summary

**Phase:** 20-content-kb-ingestion-transcription-local  
**Plan:** 20-01  
**Executed:** 2026-05-27

## What Was Built

- Added `IContentVideoStore.UpdateTranscriptStatusAsync(long, string, CancellationToken)` with a C# allowlist guard for `pending`, `captions`, `whisper`, `failed`, and `skipped_over_cap`.
- Added `IContentVideoStore.GetVideoByYoutubeIdAsync(long, string, CancellationToken)` for rerun/resume lookup by `(sourceId, youtube_video_id)`.
- Added `SlugifySourceName.Slugify(string)` as a pure Core helper for lowercase ASCII source slugs with deterministic `"source"` fallback for empty or non-ASCII-only names.
- Added the flat `content-source-add` CLI command with `--url`, `--name`, `--type`, and `--db`.
- Implemented duplicate handling for `content-source-add`:
  - duplicate URL: idempotent success, exit 0
  - duplicate slug with different URL: error, exit 3
  - invalid type: clear error, exit 2 before writing a row
- Preserved Core ownership for content stores; no packages were added.

## Key Files

- `DeckFlow.Core/Content/IContentVideoStore.cs`
- `DeckFlow.Core/Content/ContentVideoStore.cs`
- `DeckFlow.Core/Content/SlugifySourceName.cs`
- `DeckFlow.CLI/Program.cs`
- `DeckFlow.CLI/CommandRunners.cs`
- `DeckFlow.Core.Tests/ContentVideoStoreTests.cs`
- `DeckFlow.Core.Tests/SlugifySourceNameTests.cs`

## Tests Added

- `ContentVideoStoreTests`
  - updates pending video to `captions`
  - updates pending video to `skipped_over_cap`
  - rejects unknown status before opening/creating the database
  - treats missing video id update as a no-op
  - returns an existing YouTube video with current transcript status
  - returns null for a missing YouTube video lookup
- `SlugifySourceNameTests`
  - ASCII lowercase slug
  - punctuation collapse and dash trimming
  - empty-name fallback
  - non-ASCII-only fallback
  - seeded source is returned by `ListEnabledSourcesAsync`

## Verification Results

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj -c Debug`
  - Passed: 0 warnings, 0 errors
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~ContentVideoStoreTests"`
  - Passed: 12 passed, 0 failed
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~SlugifySourceNameTests"`
  - Passed: 5 passed, 0 failed
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.CLI/DeckFlow.CLI.csproj -c Debug`
  - Passed: 0 warnings, 0 errors
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln`
  - Passed: 0 warnings, 0 errors
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test`
  - Core tests passed: 143 passed, 0 failed
  - Web tests failed: 13 failed, 465 passed, 5 skipped
  - Failures are in existing `DeckFlow.Web.Tests.AdminCssPhase1Tests` expecting missing Phase 1 CSS markers; no Web CSS files were touched by this plan.

## CLI Verification

- First add:
  - `content-source-add --url https://www.youtube.com/@MTGGoldfish --name "MTGGoldfish" --type youtube_channel --db artifacts/uat-content-kb.db`
  - Exit 0; printed `Computed slug: mtggoldfish` and added row id 1.
- Duplicate URL:
  - Same command rerun
  - Exit 0; printed `source already exists (same url)`.
- Duplicate slug, different URL:
  - `--url https://www.youtube.com/@DifferentGoldfish --name "MTGGoldfish"`
  - Exit 3; printed `slug 'mtggoldfish' already used by a different url - pass a distinct --name`.
- Invalid type:
  - `--type bogus`
  - Exit 2; printed `Unsupported content source type 'bogus'. Use youtube_channel or podcast_rss.`

## Deviations

- `ContentVideoStoreTests.cs` already existed, so the new behavior coverage was added to that fixture instead of creating a duplicate test class.
- `GetVideoByYoutubeIdAsync` selects the full `ContentVideo` row, not only `id` and `transcript_status`, because the existing `ContentVideo` read model has required properties for the full row.
- `Program.cs` now returns `Environment.ExitCode` when `InvokeAsync` itself returns 0. This keeps the existing handler convention while making CLI command failures visible to the shell, which was required for duplicate-slug and invalid-type acceptance.
