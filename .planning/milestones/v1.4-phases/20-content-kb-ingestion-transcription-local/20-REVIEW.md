---
phase: 20-content-kb-ingestion-transcription-local
reviewed: 2026-05-27T02:10:00Z
depth: standard
files_reviewed: 35
files_reviewed_list:
  - DeckFlow.CLI/CommandRunners.cs
  - DeckFlow.CLI/Program.cs
  - DeckFlow.Core/Content/ContentVideoStore.cs
  - DeckFlow.Core/Content/IContentVideoStore.cs
  - DeckFlow.Core/Content/SlugifySourceName.cs
  - DeckFlow.Core/DeckFlow.Core.csproj
  - DeckFlow.Core/Integration/FfmpegAudioChunker.cs
  - DeckFlow.Core/Integration/IFfmpegAudioChunker.cs
  - DeckFlow.Core/Integration/ITranscriptSource.cs
  - DeckFlow.Core/Integration/IWhisperTranscriptionService.cs
  - DeckFlow.Core/Integration/IYouTubeAudioSource.cs
  - DeckFlow.Core/Integration/IYouTubeChannelVideoLister.cs
  - DeckFlow.Core/Integration/IYouTubeTranscriptFetcher.cs
  - DeckFlow.Core/Integration/TranscriptProviderFactory.cs
  - DeckFlow.Core/Integration/WhisperResiliencePipeline.cs
  - DeckFlow.Core/Integration/WhisperTranscriptionResult.cs
  - DeckFlow.Core/Integration/WhisperTranscriptionService.cs
  - DeckFlow.Core/Integration/YouTubeAudioSource.cs
  - DeckFlow.Core/Integration/YouTubeChannelVideo.cs
  - DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs
  - DeckFlow.Core/Integration/YouTubeTranscriptFetcher.cs
  - DeckFlow.Core/Integration/YouTubeTranscriptSource.cs
  - DeckFlow.Core.Tests/CommandRunnerHarvestTests.cs
  - DeckFlow.Core.Tests/ContentVideoStoreTests.cs
  - DeckFlow.Core.Tests/FfmpegAudioChunkerTests.cs
  - DeckFlow.Core.Tests/SlugifySourceNameTests.cs
  - DeckFlow.Core.Tests/TranscriptFetchResultTests.cs
  - DeckFlow.Core.Tests/TranscriptProviderFactoryTests.cs
  - DeckFlow.Core.Tests/WhisperTranscriptionServiceTests.cs
  - DeckFlow.Core.Tests/YouTubeAudioSourceTests.cs
  - DeckFlow.Core.Tests/YouTubeChannelVideoListerTests.cs
  - DeckFlow.Core.Tests/YouTubeTranscriptFetcherTests.cs
  - DeckFlow.Core.Tests/YouTubeTranscriptSourceTests.cs
findings:
  critical: 2
  warning: 5
  info: 4
  total: 11
status: issues_found
---

# Phase 20: Code Review Report

**Reviewed:** 2026-05-27T02:10:00Z
**Depth:** standard
**Files Reviewed:** 35
**Status:** issues_found

## Summary

Reviewed the Content KB ingestion / transcription / local cap-check slice: the
`harvest` CLI verb (`CommandRunners.RunHarvestAsync`), the pure Core/Integration
services (YouTube listing, caption fetch, audio download, Whisper transcription,
ffmpeg chunking), the `ContentVideoStore` persistence, and supporting tests.

The phase-critical contracts mostly hold up well: D-11 purity is respected (only
`RunHarvestAsync` writes `content_videos`/`content_transcripts`/`transcript_status`
and the `whisper_spend_ledger` row), the month-key flows as one value through
fetch → transcribe → ledger, `TranscriptOutcome` maps 1:1 to `transcript_status`
without collapsing `skipped_over_cap`/`failed`, `OPENAI_API_KEY` is env-only, and
the `OPENAI001` suppression is narrowly scoped to `ReadBilledSeconds`. The
`Microsoft.Extensions.Logging.Abstractions` 10.0.3 bump appears only in
`DeckFlow.Core.csproj` (transitive of OpenAI 2.10.0) — no unexpected placements.

However, two correctness defects undermine the cap-accounting guarantee that is
the entire point of KB-05, and several robustness gaps reduce the value of the
harvested data. Details below.

## Critical Issues

### CR-01: Whisper spend can be incurred without a ledger row (non-atomic persistence) — cap under-counts and risks cost overrun

**File:** `DeckFlow.CLI/CommandRunners.cs:618-644` (`PersistTranscriptResultAsync`), with the catch at `DeckFlow.CLI/CommandRunners.cs:574-578`
**Issue:** For the `Whisper` outcome the verb performs three independent writes with
no enclosing transaction:

```csharp
case TranscriptOutcome.Whisper:
    await videoStore.InsertTranscriptAsync(videoId, TranscriptSource.Whisper, result.Body!, ct);
    await videoStore.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.Whisper, ct);
    await ledger.RecordCallAsync(videoId, result.SecondsBilled!.Value, result.CostUsd!.Value, monthKey, ct);
    break;
```

The Whisper API call has already completed and money has already been spent by the
time this method runs. If `RecordCallAsync` throws (DB lock, I/O error,
cancellation-adjacent failure) — or the process dies — after `InsertTranscriptAsync`
succeeds, the spend is real but **no ledger row exists**. Worse, the catch in
`HarvestVideoAsync` then calls `MarkFailedIfPossibleAsync`, overwriting the status to
`failed` while the whisper transcript row remains. Net result: real OpenAI spend that
the ledger never records, so every subsequent `WouldExceedCapAsync` check
under-counts actual spend and the monthly cap can be silently blown — defeating the
KB-05 cost-control guarantee. The ledger write should happen first (or all three
should run in one transaction), so that recorded spend is never less than incurred
spend.
**Fix:** Record spend before (or atomically with) persisting the transcript, and do
not downgrade a video to `failed` once spend has been incurred. For example, write the
ledger row first so the cap is conservative even if the transcript insert later fails:

```csharp
case TranscriptOutcome.Whisper:
    // Record spend FIRST: a ledger row without a transcript is safe (cap stays
    // conservative); a transcript without a ledger row is not (cap under-counts).
    await ledger.RecordCallAsync(videoId, result.SecondsBilled!.Value, result.CostUsd!.Value, monthKey, ct);
    await videoStore.InsertTranscriptAsync(videoId, TranscriptSource.Whisper, result.Body!, ct);
    await videoStore.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.Whisper, ct);
    break;
```

Additionally, scope `MarkFailedIfPossibleAsync` so it does not overwrite a status once
a Whisper spend/transcript has been committed for this video.

### CR-02: Audio-duration arm of the cap projection is dead code — videos with unknown lister duration are always marked Failed

**File:** `DeckFlow.Core/Integration/YouTubeAudioSource.cs:93-94` (`GetBestEffortDurationSeconds`), consumed at `DeckFlow.Core/Integration/WhisperTranscriptionService.cs:63`
**Issue:** The cap-duration authority is documented (interface XML doc, `ITranscriptSource.cs:140`,
and `AudioDownloadResult.DurationSeconds` doc at `IYouTubeAudioSource.cs:38`) as
"max of authoritative `knownDuration` and best-effort audio duration." But
`GetBestEffortDurationSeconds` is hardcoded to return `0`:

```csharp
private static double GetBestEffortDurationSeconds(IStreamInfo streamInfo)
    => 0;
```

So `AudioDownloadResult.DurationSeconds` is always 0 in production, which makes the
`Math.Max(knownDuration?.TotalSeconds ?? 0d, audio.DurationSeconds)` at
`WhisperTranscriptionService.cs:63` reduce to `knownDuration ?? 0`. The audio-side
fallback that the contract promises is non-functional. Concretely: any channel video
whose `PlaylistVideo.Duration` is null (which YoutubeExplode can return) will hit the
`projectionSeconds <= 0` branch and be marked **Failed** even though its audio was
successfully downloaded and could have been measured/transcribed. The "both-unknown =>
Failed" guard was meant to be a last resort, but here it fires whenever the *single*
remaining source is unavailable. This both loses content and contradicts the documented
behavior the tests claim to exercise (`TranscribeAsync_UsesKnownDurationForCapWhenAudioDurationIsZero`
only ever feeds 0 because that is all production produces).
**Fix:** Either implement a real best-effort duration (e.g., derive from the stream
manifest's duration if YoutubeExplode exposes it, or probe with ffmpeg/ffprobe before
transcription), or — if a best-effort source genuinely cannot be obtained — remove the
dead `Math.Max`/`DurationSeconds` plumbing and the "max with audio duration" wording
from the contracts so the implementation and the documented cap authority agree. Do not
leave a documented safety arm silently stubbed to 0.

## Warnings

### WR-01: Whisper retry pipeline does not match the exceptions the OpenAI SDK actually throws

**File:** `DeckFlow.Core/Integration/WhisperResiliencePipeline.cs:24`
**Issue:** The retry `ShouldHandle` predicate only handles `HttpRequestException` or
`TimeoutRejectedException`:

```csharp
ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception is HttpRequestException or TimeoutRejectedException),
```

The OpenAI SDK (`System.ClientModel`) surfaces transient HTTP failures (429 rate-limit,
5xx) as `ClientResultException`, not `HttpRequestException`. Because `RetryPolicy =
new ClientRetryPolicy(maxRetries: 0)` disables the SDK's own retries
(`WhisperTranscriptionService.cs:184`), a real 429/503 from OpenAI will propagate as
`ClientResultException`, fail the `ShouldHandle` test, and be caught by
`TranscribeAfterCapCheckAsync` as a permanent `Failed` with no retry. The only thing the
retry pipeline currently retries is the synthetic `HttpRequestException` thrown in the
unit test (`WhisperTranscriptionServiceTests.cs:110`), giving false confidence. Result:
transient OpenAI failures permanently mark videos `failed` instead of retrying.
**Fix:** Add `ClientResultException` (and inspect its `Status`/`GetRawResponse()?.Status`
for 408/429/5xx) to the `ShouldHandle` predicate, or re-enable a bounded SDK retry. For
example:

```csharp
ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception switch
{
    HttpRequestException or TimeoutRejectedException => true,
    ClientResultException cre => cre.Status is 0 or 408 or 429 or >= 500,
    _ => false,
}),
```

### WR-02: `published_utc` is never populated for YouTube videos

**File:** `DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs:91-99` (`MapVideo`)
**Issue:** `MapVideo` hardcodes `PublishedUtc = null` for every listed video. That null
flows through `ResolveHarvestVideoIdAsync` → `InsertVideoAsync` → the `published_utc`
column, so every harvested YouTube row stores NULL despite the schema column existing and
the whole insert path faithfully carrying the value. Downstream Phase 21 distillation /
slim-index work that wants recency ordering will have no publication date to sort on. The
`YouTubeChannelVideo.Duration` field is correctly mapped from `video.Duration`, so the
omission of `PublishedUtc` looks like an oversight rather than a deliberate limitation.
**Fix:** Populate `PublishedUtc` from the YoutubeExplode video metadata where available
(e.g., the `PlaylistVideo` upload date, or a follow-up `Videos.GetAsync` if the playlist
projection lacks it). If YoutubeExplode genuinely cannot supply it from the uploads
playlist, document that explicitly at the mapping site so it is not mistaken for a bug.

### WR-03: ffmpeg argument string is brittle against paths containing quotes

**File:** `DeckFlow.Core/Integration/FfmpegAudioChunker.cs:84-85` (`BuildSegmentArguments`)
**Issue:** Arguments are built by wrapping paths in `"..."` and concatenating into a single
`Arguments` string:

```csharp
=> $"-i \"{inputPath}\" -f segment -segment_time {segmentSeconds} -c copy -reset_timestamps 1 \"{outputPattern}\"";
```

`UseShellExecute = false` means there is no shell to inject into, so this is not a command
-injection vulnerability in the current call path (inputs are GUID-based temp paths under
`Path.GetTempPath()`). However, a path containing a double-quote or trailing backslash
would break argument parsing and cause a confusing ffmpeg failure. Since the chunker is a
public API surface (`IFfmpegAudioChunker.ChunkAsync` accepts arbitrary `inputPath`/
`outputDirectory`), a future caller passing a less-controlled path would hit this.
**Fix:** Use `ProcessStartInfo.ArgumentList` (which handles quoting/escaping per the
platform) instead of building the `Arguments` string by hand. Note that switching to
`ArgumentList` will require updating the `BuildSegmentArguments_UsesSegmentCopyCommand`
test, which asserts on the concatenated string.

### WR-04: Empty-channel / empty-video edge produces no diagnostic and silently no-ops

**File:** `DeckFlow.CLI/CommandRunners.cs:526-546` (`HarvestSourceAsync`) and `491-524` (`RunHarvestAsync`)
**Issue:** If `sources` is empty (no enabled sources) or `ListRecentAsync` returns an empty
list, the harvest loop completes with exit code 0 and only the aggregate
`whisper_fallback_ratio` line (which divides to 0). There is no warning that the harvester
did nothing. Operationally this is easy to misread as a successful harvest when in fact no
source was configured or the channel URL failed to resolve any uploads. Combined with the
broad `catch` in the public `RunHarvestAsync` (line 483) that returns 1 only on
non-`OperationCanceledException`, a misconfigured run is hard to distinguish from a healthy
empty run.
**Fix:** Log a `Warning` when `sources` contains no enabled YouTube sources, and an
`Information`/`Warning` per source when `ListRecentAsync` yields zero videos, so empty runs
are observable in the CLI logs.

### WR-05: On exception after status was already advanced, `MarkFailedIfPossibleAsync` can clobber a more accurate status

**File:** `DeckFlow.CLI/CommandRunners.cs:574-578` and `657-666` (`MarkFailedIfPossibleAsync`)
**Issue:** Any exception thrown anywhere inside `HarvestVideoAsync` after
`contentVideoId` is resolved results in the video being force-set to `failed`. For the
non-Whisper paths this is mostly fine, but it interacts badly with CR-01 (a Whisper success
that fails only on the ledger write gets downgraded to `failed` while its transcript row
persists) and it can also overwrite a `captions`/`whisper` status that was just written if
a later `await` (e.g., the ledger write) throws. The "mark failed" recovery is too broad: it
assumes the failure happened before any terminal write.
**Fix:** Track whether a terminal-success status was already persisted for this video and
skip the `MarkFailedIfPossibleAsync` downgrade in that case; only mark `failed` when the
video is still `pending`. This is closely tied to the CR-01 fix and should be addressed
together.

## Info

### IN-01: `YouTubeCaptionResult.CaptionTrackKind` is computed but never consumed

**File:** `DeckFlow.Core/Integration/IYouTubeTranscriptFetcher.cs:40,66` and `DeckFlow.CLI/CommandRunners.cs:687-695`
**Issue:** `FromCaptions` derives `CaptionTrackKind` ("auto_generated"/"manual"), but the
only consumer that logs caption kind (`GetCaptionTrackKind` in `CommandRunners.cs`)
recomputes it from `TranscriptFetchResult.IsAutoGenerated` instead of carrying the already
-computed value through. The `CaptionTrackKind`/`LanguageCode` fields on
`YouTubeCaptionResult` are dropped at the `YouTubeTranscriptSource.MapWhisperResult`/
`FromCaptions` boundary (only `IsAutoGenerated` is forwarded). Minor duplicated logic plus
a quietly discarded `LanguageCode`.
**Fix:** Either forward `CaptionTrackKind`/`LanguageCode` through `TranscriptFetchResult`
and have the logger read them, or drop the unused fields from `YouTubeCaptionResult` to
avoid the impression they are persisted.

### IN-02: `WhisperTranscriptionResult.MonthKey` echo is never read by the verb

**File:** `DeckFlow.Core/Integration/WhisperTranscriptionResult.cs:26` and `DeckFlow.CLI/CommandRunners.cs:568,635`
**Issue:** The service echoes back `MonthKey` for "ledger consistency," but the verb uses its
own locally computed `monthKey` (line 568) for the ledger write and never reads
`result.MonthKey`. The two are equal by construction today, so this is correct but the echo
field is dead with respect to the actual ledger write. It is harmless but could mislead a
future maintainer into thinking the echoed value is the source of truth.
**Fix:** Either assert `result.MonthKey == monthKey` at the persistence boundary as a guard,
or document that the echo is informational only.

### IN-03: Duplicated `RetryPolicy` definition across two fetchers

**File:** `DeckFlow.Core/Integration/YouTubeAudioSource.cs:14-21` and `DeckFlow.Core/Integration/YouTubeTranscriptFetcher.cs:14-21`
**Issue:** The identical Polly `AsyncRetryPolicy` (6 retries, exponential + jitter, same
exception set, empty `onRetry`) is copy-pasted into both `YouTubeAudioSource` and
`YouTubeTranscriptFetcher`. Per the project's "logic duplicated more than twice" guideline
this is borderline; extracting a shared factory would keep the two in sync.
**Fix:** Extract a single `YouTubeRetryPolicy.Build()` (or a shared static field) in
`DeckFlow.Core/Integration` and reference it from both fetchers.

### IN-04: `onRetry` callbacks are empty — silent retries on every upstream blip

**File:** `DeckFlow.Core/Integration/YouTubeAudioSource.cs:21` and `DeckFlow.Core/Integration/YouTubeTranscriptFetcher.cs:21`
**Issue:** Both YouTube retry policies pass `onRetry: (...) => { }`. With 6 retries and up to
~64s backoff per attempt, a flaky channel can stall the harvest for minutes with no log line
explaining why. Given this is a long-running CLI harvest, retry visibility is valuable for
diagnosis.
**Fix:** Log a `Debug`/`Information` line in `onRetry` (attempt number, delay, exception
type). Requires threading a logger into the fetchers, or using the static `Serilog.Log`.

---

_Reviewed: 2026-05-27T02:10:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
