---
phase: 20-content-kb-ingestion-transcription-local
plan: 03
subsystem: integration
tags: [whisper, openai, polly, ffmpeg, spend-cap, transcription]

requires:
  - phase: 20-02
    provides: AudioDownloadResult, ITranscriptSource contracts, authoritative knownDuration/monthKey threading
provides:
  - Pure Whisper transcription service with cap-read gate, billed seconds/cost, status, and monthKey result
  - Concrete 12 minute Polly pipeline wrapping all transcription delegates
  - ffmpeg shell-out chunker for audio files larger than 24 MB
  - Focused xUnit coverage for cap skips, duration authority, Polly retry, chunk concatenation, and ffmpeg availability
affects: [20-04, harvest, content-kb, whisper-spend-ledger]

tech-stack:
  added: [OpenAI 2.10.0]
  patterns: [pure ingestion service, Func transcribe seam, Polly-wrapped OpenAI delegate, ffmpeg shell-out chunker]

key-files:
  created:
    - DeckFlow.Core/Integration/IFfmpegAudioChunker.cs
    - DeckFlow.Core/Integration/FfmpegAudioChunker.cs
    - DeckFlow.Core/Integration/IWhisperTranscriptionService.cs
    - DeckFlow.Core/Integration/WhisperTranscriptionResult.cs
    - DeckFlow.Core/Integration/WhisperResiliencePipeline.cs
    - DeckFlow.Core/Integration/WhisperTranscriptionService.cs
    - DeckFlow.Core.Tests/FfmpegAudioChunkerTests.cs
    - DeckFlow.Core.Tests/WhisperTranscriptionServiceTests.cs
  modified:
    - DeckFlow.Core/DeckFlow.Core.csproj

key-decisions:
  - "Kept WhisperTranscriptionResult in its own file to honor the one-public-type-per-file project rule."
  - "Opened a fresh file stream inside each Polly attempt so transient retries do not reuse a consumed stream."
  - "Aligned the existing Microsoft.Extensions.Logging.Abstractions reference to 10.0.3 because OpenAI 2.10.0 transitively requires it through System.ClientModel 1.10.0."

patterns-established:
  - "Spend-cap checks use max(knownDuration, audio.DurationSeconds), fail when both are unknown, and happen before any transcription delegate call."
  - "Whisper services return status, billing, and monthKey only; the harvest verb remains the sole persistence owner."
  - "Large-audio chunking degrades to Failed when ffmpeg is unavailable or chunking fails."

requirements-completed: [KB-04, KB-05]

duration: 35min
completed: 2026-05-27
---

# Phase 20 Plan 03: Whisper Runtime Summary

**Pure Whisper fallback runtime with authoritative spend-cap gating, Polly-wrapped OpenAI transcription, and ffmpeg chunking for large local audio files**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-05-27T01:10:00Z
- **Completed:** 2026-05-27T01:44:17Z
- **Tasks:** 2
- **Files modified:** 9 implementation/test files plus this summary

## What Was Built

- Added `FfmpegAudioChunker` with exception-safe `IsAvailableAsync`, deterministic `BuildSegmentArguments`, and ordered chunk file output from `ffmpeg -f segment -segment_time 300 -c copy`.
- Added `WhisperResiliencePipeline` with a concrete Polly timeout/retry pipeline and tests proving the transcription delegate runs through it.
- Added `WhisperTranscriptionService` as a pure service: it writes no stores or ledger rows, checks `IWhisperSpendLedger.WouldExceedCapAsync` before any transcription call, returns `SkippedOverCap` without invoking OpenAI, and echoes the verb-supplied `MonthKey`.
- Implemented KB-05 duration authority: projection uses `Math.Max(knownDuration, audio.DurationSeconds)` and returns `Failed` when both are unknown so real videos are never transcribed with a $0 projection.
- Implemented chunk transcription for audio over 24 MB with ordered concatenation and cleanup of the chunk temp directory.

## Task Commits

1. **Task 1: FfmpegAudioChunker** - `7707f59` (`feat(20-03): add ffmpeg audio chunker`)
2. **Task 2: WhisperResiliencePipeline + WhisperTranscriptionService** - `2247a0a` (`feat(20-03): add whisper transcription service`)
3. **Summary** - committed separately as `docs(20-03): execution summary`

## Tests Added

- `FfmpegAudioChunkerTests`
  - argument builder includes `-segment_time 300 -c copy`
  - availability check returns false gracefully when ffmpeg is absent, while staying tolerant if ffmpeg exists
- `WhisperTranscriptionServiceTests`
  - over-cap result skips the delegate and never writes the ledger
  - knownDuration drives positive cap projection when `audio.DurationSeconds == 0`
  - both duration sources unknown returns `Failed` before cap check or transcription
  - under-cap single file returns body, billed seconds, cost, and monthKey
  - transient `HttpRequestException` is retried through Polly
  - large audio chunks are transcribed twice and concatenated in order
  - missing ffmpeg on large audio returns `Failed` with no delegate call

## Verification Results

- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~FfmpegAudioChunkerTests"`
  - Passed: 2 passed, 0 failed
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~WhisperTranscriptionServiceTests"`
  - Passed: 7 passed, 0 failed
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~WhisperTranscriptionServiceTests|FullyQualifiedName~FfmpegAudioChunkerTests"`
  - Passed: 9 passed, 0 failed
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`
  - Passed: 164 passed, 0 failed
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj -c Debug`
  - Passed: 0 warnings, 0 errors
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln`
  - Passed: 0 warnings, 0 errors

## Acceptance Checks

- `OpenAI` 2.10.0 is referenced from `DeckFlow.Core.csproj`.
- No `packages.lock.json` exists.
- `WhisperTranscriptionService` contains `HttpClientPipelineTransport`, `ClientRetryPolicy(0)`, and `NetworkTimeout = Timeout.InfiniteTimeSpan`.
- `WhisperTranscriptionService` contains no `DateTime.UtcNow`, `new HttpClient()`, `ContentVideoStore`, transcript writes, or ledger writes.
- `WhisperTranscriptionService` reads `OPENAI_API_KEY` from `Environment` only.
- Core gained no `Microsoft.AspNetCore.*` or `Microsoft.Extensions.Http` reference.
- ffmpeg absence is a Failed/degrade path, not a crash path.

## Deviations From Plan

### Auto-Fixed Issues

**1. [Rule 3 - Blocking] Existing logging package version had to align with OpenAI transitive dependencies**
- **Found during:** Task 2 restore after adding `OpenAI` 2.10.0
- **Issue:** NuGet failed with `NU1605` because `OpenAI -> System.ClientModel 1.10.0` requires `Microsoft.Extensions.Logging.Abstractions >= 10.0.3`, while Core directly pinned `10.0.0`.
- **Fix:** Updated the existing `Microsoft.Extensions.Logging.Abstractions` reference in `DeckFlow.Core.csproj` from `10.0.0` to `10.0.3`.
- **Files modified:** `DeckFlow.Core/DeckFlow.Core.csproj`
- **Verification:** Restore succeeded; Core build, Core tests, and solution build all passed with 0 warnings/errors.
- **Committed in:** `2247a0a`

**Total deviations:** 1 auto-fixed blocking dependency alignment.  
**Impact on plan:** No new package was added and no lockfile was generated; the existing direct package version was aligned to satisfy OpenAI 2.10.0.

## Issues Encountered

- `AudioTranscription.Usage` and `AudioTranscriptionDurationUsage` emit `OPENAI001`. The plan requires preferring `Usage.Duration`, so the implementation uses a narrow pragma around only the billed-duration helper. Build remains clean.
- A post-summary full `DeckFlow.Core.Tests` rerun briefly hit an unrelated `ObjectDisposedException` in `SlugifySourceNameTests.ListEnabledSourcesAsync_ReturnsSeededContentSource`; the specific test rerun passed, the 20-03 focused tests passed, and a subsequent full Core test rerun passed 164/164.

## User Setup Required

No new setup document was generated. Runtime Whisper calls still require `OPENAI_API_KEY` in the local environment, and local ffmpeg is still required only for audio files larger than 24 MB.

## Next Phase Readiness

Plan 20-04 can inject the ledger, chunker, and HTTP client; pass the lister's authoritative `knownDuration` and one verb-created `monthKey`; and persist transcripts plus spend rows from the returned pure result. The service already returns `Whisper`, `Failed`, and `SkippedOverCap` outcomes with the month key needed for consistent ledger writes.

---
*Phase: 20-content-kb-ingestion-transcription-local*
*Completed: 2026-05-27*
