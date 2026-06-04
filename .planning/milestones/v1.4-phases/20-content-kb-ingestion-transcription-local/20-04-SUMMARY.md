---
phase: 20-content-kb-ingestion-transcription-local
plan: 04
subsystem: ingestion
tags: [youtube, youtubeexplode, whisper, cli, content-kb, transcription]

requires:
  - phase: 20-01
    provides: content source/video stores, resume lookup, content-source-add CLI
  - phase: 20-02
    provides: transcript source contract, caption fetcher, audio source
  - phase: 20-03
    provides: Whisper transcription service, ffmpeg chunker, spend-cap gate
provides:
  - Pure YouTubeTranscriptSource composing captions first and Whisper fallback
  - CollectAsync-bounded YouTubeChannelVideoLister
  - harvest CLI verb with single-owner transcript/status/ledger persistence
  - Non-network unit tests for source and harvest orchestration contracts
affects: [phase-21, content-kb, local-harvest, whisper-spend-ledger]

tech-stack:
  added: []
  patterns: [pure integration composition, CLI composition root, Func seam harvest tests]

key-files:
  created:
    - DeckFlow.Core/Integration/IYouTubeChannelVideoLister.cs
    - DeckFlow.Core/Integration/YouTubeChannelVideo.cs
    - DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs
    - DeckFlow.Core/Integration/YouTubeTranscriptSource.cs
    - DeckFlow.Core.Tests/YouTubeChannelVideoListerTests.cs
    - DeckFlow.Core.Tests/YouTubeTranscriptSourceTests.cs
    - DeckFlow.CLI/AssemblyInfo.cs
    - DeckFlow.Core.Tests/CommandRunnerHarvestTests.cs
  modified:
    - DeckFlow.CLI/Program.cs
    - DeckFlow.CLI/CommandRunners.cs
    - DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj

key-decisions:
  - "Added a CLI internals test seam so harvest orchestration can be unit-tested without YouTube/OpenAI network calls."
  - "Kept RunHarvestAsync as the only writer of transcript rows, transcript status, and Whisper ledger rows."

patterns-established:
  - "The verb derives one yyyy-MM monthKey per video and reuses it for FetchTranscriptAsync and RecordCallAsync."
  - "Existing captions/whisper rows are terminal successes; pending/failed/skipped_over_cap rows are resumed."
  - "Harvest logs transcript_source, caption_track_kind, and per-source plus aggregate whisper_fallback_ratio."

requirements-completed: [KB-03, KB-04, KB-05]

duration: 90min
completed: 2026-05-27
---

# Phase 20 Plan 04: Harvest Composition Summary

**Local Content KB harvest now lists bounded YouTube uploads, fetches captions first, falls back to Whisper under the cap, and persists transcripts/status/ledger rows from one CLI owner**

## What Was Built

- Added `YouTubeChannelVideoLister` over YoutubeExplode uploads with `CollectAsync(limit)` and no `ToListAsync`.
- Added pure `YouTubeTranscriptSource : ITranscriptSource` that fetches captions first, downloads transient audio only when needed, forwards authoritative `knownDuration` plus verb-supplied `monthKey` to Whisper, and maps `SkippedOverCap` distinctly from `Failed`.
- Added flat `harvest` CLI command with `--db` and `--limit`.
- Implemented `RunHarvestAsync` as the D-11 single persistence owner for `content_videos`, `content_transcripts`, transcript status, and `whisper_spend_ledger` rows.
- Added non-network xUnit coverage for source composition and harvest orchestration seams.

## Task Commits

1. **Task 1: bounded lister + pure source** - `7ac52d8` (`feat(20-04): compose youtube transcript source`)
2. **Task 2: harvest verb + RunHarvestAsync** - `a5d31a5` (`feat(20-04): add content harvest verb`)
3. **Summary** - `docs(20-04): execution summary`

## Verification Results

- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~YouTubeTranscriptSourceTests|FullyQualifiedName~YouTubeChannelVideoListerTests|FullyQualifiedName~CommandRunnerHarvestTests"`
  - Passed: 9 passed, 0 failed
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`
  - Passed: 173 passed, 0 failed
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.CLI/DeckFlow.CLI.csproj -c Debug`
  - Passed: 0 warnings, 0 errors
- `"/mnt/c/Program Files/dotnet/dotnet.exe" run --project DeckFlow.CLI -- harvest --help`
  - Passed; help lists `--db` and `--limit`
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln`
  - Passed: 0 warnings, 0 errors

## UAT: PENDING-HUMAN-RUN

The live 5-channel UAT was not run in this sandbox by instruction. It requires internet access, `OPENAI_API_KEY`, and `ffmpeg` on `PATH`.

Required environment:

```bash
export OPENAI_API_KEY="..."
ffmpeg -version
```

Seed the UAT database from the repo root:

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project DeckFlow.CLI -- content-source-add --name "MTGGoldfish" --url https://www.youtube.com/@MTGGoldfish --type youtube_channel --db artifacts/uat-content-kb.db
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project DeckFlow.CLI -- content-source-add --name "The Command Zone" --url https://www.youtube.com/@TheCommandZone --type youtube_channel --db artifacts/uat-content-kb.db
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project DeckFlow.CLI -- content-source-add --name "EDHRECast" --url https://www.youtube.com/@EDHRECast --type youtube_channel --db artifacts/uat-content-kb.db
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project DeckFlow.CLI -- content-source-add --name "Tolarian Community College" --url https://www.youtube.com/@TolarianCommunityCollege --type youtube_channel --db artifacts/uat-content-kb.db
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project DeckFlow.CLI -- content-source-add --name "Playing With Power" --url https://www.youtube.com/@PlayingwithPowerMTG --type youtube_channel --db artifacts/uat-content-kb.db
```

Run the bounded harvest:

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project DeckFlow.CLI -- harvest --db artifacts/uat-content-kb.db --limit 2
```

Expected result:

- Each fetch log includes `transcript_source` and `caption_track_kind`.
- Each of the 5 channels logs `whisper_fallback_ratio`.
- The aggregate log line has `whisper_fallback_ratio < 0.25`.
- Missing `ffmpeg` logs one warning and does not abort; large audio needing chunking may be marked `failed`.
- `whisper_spend_ledger` has rows only for successful Whisper fallbacks, and each row's `month_key` matches the run month.

## Deviations From Plan

- Added `DeckFlow.CLI/AssemblyInfo.cs` plus a `DeckFlow.CLI` project reference from `DeckFlow.Core.Tests` so the harvest command runner can be unit-tested with fakes and no live network/API calls.
- Added `YouTubeChannelVideo.cs` as a separate file to honor the one-public-type-per-file project rule.

## Next Phase Readiness

Phase 21 can build distillation, artifact emission, slim-index writes, and run records on top of the local transcript/status/ledger persistence now owned by `RunHarvestAsync`.

## Review Fixes

- CR-01: `RunHarvestAsync` now records the Whisper ledger row before transcript/status persistence, so a later transcript insert failure keeps the cap conservative. Added `RunHarvestAsync_WhisperInsertFailureAfterLedgerWriteKeepsLedgerRecord`.
- WR-05: harvest failure recovery now marks `failed` only for videos that were pending and have not had a status persisted. Added `RunHarvestAsync_ExistingSkippedOverCapVideoIsNotDowngradedToFailedOnRetryException`.
- CR-02: `YouTubeAudioSource` now derives best-effort duration from YoutubeExplode video metadata instead of hardcoded `0`, and the duration XML contracts describe the `Math.Max(knownDuration, audioDuration)` cap authority. Added `GetBestEffortDurationSeconds_ReturnsMetadataDurationSeconds` and `TranscribeAsync_UsesAudioDurationForCapWhenKnownDurationIsUnknown`.
- WR-01: `WhisperResiliencePipeline` now retries transient OpenAI SDK `ClientResultException` statuses `0`, `408`, `429`, and `>=500`. Added `TranscribeAsync_RetriesTransientClientResultExceptionThroughPollyPipeline`.
- WR-02: `YouTubeChannelVideoLister` now performs a bounded video-metadata lookup to populate `PublishedUtc` when available; the mapping site documents that the playlist projection lacks upload date. Added `MapVideo_CarriesPublishedUtcFromMetadataLookup`.

---
*Phase: 20-content-kb-ingestion-transcription-local*
*Completed: 2026-05-27*
