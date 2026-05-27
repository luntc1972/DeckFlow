---
phase: 20-content-kb-ingestion-transcription-local
reviewed: 2026-05-26
reviewers: [codex]
verdict: HIGH
high_concerns: 13
---

# Phase 20 — Cross-AI Plan Review

## Codex Review

Using `gstack-openclaw-ceo-review` + `plan-eng-review` in hold-scope mode.

**Summary**

The plan set is strong on phase decomposition, local-only scope, known pitfalls, and package discipline. It is not yet execution-safe. The main issue is ownership confusion between “services return transcript results” and “services persist transcript/status themselves,” especially across 20-03 and 20-04. As written, a Codex executor could produce duplicate Whisper transcript rows, lose `skipped_over_cap` status, or never implement the audio download/chunk path required for Whisper fallback. Overall risk: **HIGH until the contracts are tightened.**

Sources checked: local [ContentVideoStore.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Core/Content/ContentVideoStore.cs), [WhisperSpendLedger.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Core/Content/WhisperSpendLedger.cs), [Program.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.CLI/Program.cs), plus official OpenAI .NET and YoutubeExplode docs: [OpenAI .NET README](https://github.com/openai/openai-dotnet), [OpenAI 2.10.0 changelog](https://github.com/openai/openai-dotnet/blob/main/CHANGELOG.md), [YoutubeExplode README](https://github.com/Tyrrrz/YoutubeExplode), [YoutubeExplode NuGet](https://www.nuget.org/packages/YoutubeExplode/).

**20-01 Review**

Strengths:
- Correctly identifies the real store gap: `IContentVideoStore` currently has insert/count/delete but no status update.
- Good narrow wave-0 slice; no new packages.
- Tests cover status transition, invalid guard, and missing row behavior.

Concerns:
- **MEDIUM:** `content-source-add` treats all unique violations as idempotent. Duplicate URL is fine; duplicate slug with a different URL should not return success.
- **MEDIUM:** Slug generation can produce an empty slug for punctuation/non-ASCII names and does not handle collisions.
- **LOW:** No direct test is planned for source add/list behavior, even though it is a UAT prerequisite.

Suggestions:
- Add `SlugifySourceName` as a small tested helper: reject empty slug and distinguish duplicate URL from duplicate slug.
- Add a CLI/store test or runner-level test proving add then `ListEnabledSourcesAsync`.

Risk Assessment: **MEDIUM.** The store addition is straightforward, but the source seeding idempotency could mask bad UAT setup.

**20-02 Review**

Strengths:
- Correctly avoids Google captions API and uses YoutubeExplode.
- Accepts auto-generated captions, which is essential for the fallback ratio.
- Good use of a delegate seam for no-live-HTTP unit tests.

Concerns:
- **HIGH:** The provider toggle is mostly symbolic. “Read `DECKFLOW_YOUTUBE_TRANSCRIPT_PROVIDER` but only wire default” does not satisfy “proxy-pluggable from day 1.”
- **HIGH:** `.Take(limit).ToListAsync(ct)` appears later in 20-04, but no approved async LINQ package exists. Use YoutubeExplode’s `CollectAsync(limit)` or manual `await foreach`.
- **MEDIUM:** The Polly plan is vague for YoutubeExplode. It says “HandleResult on transient failures,” but YoutubeExplode calls throw exceptions, not `RestResponse` results.
- **MEDIUM:** Empty/whitespace caption tracks are not classified. `HasCaptions=true` with empty body will fail later at `InsertTranscriptAsync`.

Suggestions:
- Define a real `IYouTubeCaptionProvider` or provider factory now, with `direct` implemented and unsupported provider values failing clearly.
- Name retryable exceptions explicitly: `HttpRequestException`, timeout/cancellation cases, and YoutubeExplode-specific exceptions if available.
- Treat empty joined caption text as `NoCaptions` or `FailedEmptyCaptions`, but do not let it drift into persistence.

Risk Assessment: **MEDIUM-HIGH.** Caption fetch is plausible, but SC2’s proxy requirement and retry semantics are under-specified.

**20-03 Review**

Strengths:
- Correctly gates Whisper before API calls using `WouldExceedCapAsync`.
- Correctly disables SDK retries/timeouts in principle; OpenAI docs confirm automatic retries exist by default, so this matters.
- Good no-live-OpenAI testing direction.

Concerns:
- **HIGH:** `WhisperTranscriptionService` persists transcript/status, but 20-04’s orchestrator also persists returned transcript/status. This will duplicate Whisper transcript rows or create inconsistent ownership.
- **HIGH:** The interface accepts `Stream audioStream`; the ffmpeg chunker accepts file paths. No plan explains who downloads audio to a temp file, checks >24MB, chunks, loops chunks, concatenates transcript text, or deletes files.
- **HIGH:** “Polly timeout = 12min” is a must-have, but the task action does not actually specify a Polly timeout/retry wrapper around `TranscribeAudioAsync`.
- **MEDIUM:** Cap projection depends on `estimatedDurationSeconds`, but no upstream source for that value is defined. Underestimation can exceed the monthly cap.
- **MEDIUM:** Month key is captured before a long call; a call crossing UTC month boundary records spend in the prior month.

Suggestions:
- Make `IWhisperTranscriptionService` pure: return body/status/cost only, and let one orchestrator persist transcript, status, and ledger in one place. Or invert it and make `ITranscriptSource` own persistence, but pick one.
- Add an `IYouTubeAudioSource`/`AudioDownloadResult` contract before Whisper: path, filename, size bytes, duration seconds, cleanup handle.
- Explicitly wrap the OpenAI delegate in a Polly timeout/retry policy and test that the delegate is invoked through that wrapper.

Risk Assessment: **HIGH.** The plan claims KB-04/KB-05, but the audio/chunking/resilience path is not actually composable as written.

**20-04 Review**

Strengths:
- Correct phase boundary: local harvest stops at transcript persistence, leaving distillation to Phase 21.
- Bounded channel listing and ffmpeg warn-not-abort are the right operational defaults.
- Human UAT checkpoint is appropriate because third-party caption coverage cannot be proven by unit tests.

Concerns:
- **HIGH:** Whisper fallback cannot work: `FetchTranscriptAsync(..., audioUrl: null)` has no audio stream, filename, duration, or chunked files to pass into `IWhisperTranscriptionService`.
- **HIGH:** `TranscriptFetchResult?` loses failure status. The plan says return `null` when skipped/failed, then `result?.FinalStatus ?? Failed`, which will mark `skipped_over_cap` as `failed`.
- **HIGH:** Persistence ownership conflicts with 20-03; Whisper success can be inserted twice.
- **MEDIUM:** Re-run behavior is weak. Duplicate `content_videos.youtube_video_id` is skipped as “already harvested,” but there is no lookup to resume pending/failed videos.
- **MEDIUM:** `transcript_source` semantics conflict: sometimes `captions|whisper`, sometimes `auto-generated|manual`. UAT will be ambiguous.
- **MEDIUM:** UAT using only the latest video per channel is fragile; shorts/live/premieres can distort the fallback ratio.

Suggestions:
- Replace nullable result with a discriminated result: `Status`, `Body?`, `Source?`, `FailureReason?`, `IsAutoGenerated?`.
- Add `GetVideoByYoutubeIdAsync` or an upsert so reruns can resume existing pending/failed videos.
- Split logs into `transcript_source=captions|whisper` and `caption_track_kind=manual|auto_generated`.
- For UAT, use known stable video IDs or harvest 2 per channel and report both per-channel and aggregate ratios.

Risk Assessment: **HIGH.** This plan is the integration point, and the current contracts do not support the advertised end-to-end Whisper fallback.

**Overall Risk**

**HIGH.** The plans are directionally good but not yet implementable without executor guesswork. The blocking fixes are: single persistence owner, explicit audio download/chunk contract, non-null status-carrying result type, real provider toggle semantics, and a concrete Polly wrapper for OpenAI calls. Once those are resolved, the phase becomes a reasonable local-ingestion slice.

## Consensus Summary

Single reviewer (Codex; Claude self-skipped as host runtime). Verdict HIGH — NOT execution-safe.
Blocking HIGH fixes required before /gsd-execute-phase 20:
1. Single persistence owner (services pure → harvest verb persists; resolves 20-03/20-04 duplicate-row conflict).
2. Explicit audio download + chunk contract (IYouTubeAudioSource/AudioDownloadResult: stream→temp file, size, duration, cleanup) so Whisper fallback is composable.
3. Status-carrying result type (no nullable; carry Status/Body/Source/FailureReason) so skipped_over_cap isn't mislabeled failed.
4. Concrete Polly 12min timeout + retry wrapper around the OpenAI delegate, with a test asserting invocation through it.
5. Real provider-toggle semantics (factory; default impl + unsupported value fails clearly) per SC2.
6. Replace .Take().ToListAsync() with YoutubeExplode CollectAsync(limit)/await foreach (no async-LINQ package).
MEDIUMs: slug edge cases + dup-slug-vs-dup-url, empty caption track classification, cap estimate source + UTC month-boundary, re-run resume of pending/failed videos, transcript_source vs caption_track_kind split, UAT video selection robustness.
