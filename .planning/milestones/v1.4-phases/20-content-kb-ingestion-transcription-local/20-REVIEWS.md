---
phase: 20-content-kb-ingestion-transcription-local
reviewed: 2026-05-26
reviewers: [codex]
verdict: GREEN
high_concerns: 0
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

---

## Codex RE-REVIEW (iteration 2, after replan 305c205)

All 6 prior HIGH RESOLVED. 1 NEW HIGH + 2 MEDIUM. VERDICT: HIGH.

**Prior HIGHs**

- HIGH-1 persistence ownership: RESOLVED. D-11 is explicit, 20-02/20-03 services persist nothing, and 20-04 makes `RunHarvestAsync` the only writer. Evidence: [20-02-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-02-PLAN.md:21), [20-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-03-PLAN.md:20), [20-04-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-04-PLAN.md:181).
- HIGH-2 audio download/chunk contract: RESOLVED. `IYouTubeAudioSource`/`AudioDownloadResult` now carries temp path, filename, size, duration, and cleanup; 20-03 specifies size gate, ffmpeg chunking, ordered per-chunk transcription, concat, and cleanup. Evidence: [20-02-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-02-PLAN.md:183), [20-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-03-PLAN.md:155).
- HIGH-3 nullable/status-losing result: RESOLVED. `TranscriptOutcome` is non-null and `SkippedOverCap` remains distinct from `Failed`; harvest maps outcomes by explicit switch. Evidence: [20-02-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-02-PLAN.md:121), [20-04-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-04-PLAN.md:181).
- HIGH-4 Polly 12min wrapper not wired: RESOLVED. Plan creates `WhisperResiliencePipeline`, requires every transcribe delegate call through `ExecuteAsync`, and includes a retry/wrapper test. Evidence: [20-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-03-PLAN.md:147), [20-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-03-PLAN.md:160).
- HIGH-5 symbolic provider toggle: RESOLVED. `TranscriptProviderFactory` resolves `direct` and throws clear `NotSupportedException` for unsupported values. Evidence: [20-02-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-02-PLAN.md:151), [20-04-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-04-PLAN.md:174).
- HIGH-6 `.Take().ToListAsync()` async-LINQ: RESOLVED in the plans. 20-04 requires YoutubeExplode `CollectAsync(limit)` and a grep gate for zero `ToListAsync`. Evidence: [20-04-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-04-PLAN.md:135), [20-04-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-04-PLAN.md:152).

**New Concerns**

- HIGH: cap projection duration is still not actually wired from the video lister into Whisper. `FetchTranscriptAsync` receives `knownDuration`, but `YouTubeTranscriptSource` downloads audio and calls `whisper.TranscribeAsync(audio)` without using it. `AudioDownloadResult.DurationSeconds` can be `0` when stream/video duration is unavailable, and 20-03 uses that value for `WouldExceedCapAsync`. That can make projected cost `$0` and bypass KB-05 for captionless videos. Fix by passing `knownDuration` into `TranscribeAsync`, or by ensuring `AudioDownloadResult.DurationSeconds` is always populated from `knownDuration` before the cap check. Evidence: [20-02-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-02-PLAN.md:185), [20-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-03-PLAN.md:149), [20-04-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-04-PLAN.md:139).

- MEDIUM: month-key consistency is claimed but not carried through the contracts. 20-03 says the service captures `monthKey` and returns it, but `WhisperTranscriptionResult` has no `MonthKey`, and 20-04 records the ledger with a fresh `DateTime.UtcNow`. Fix by adding `MonthKey` to the result path or by having the verb create and pass the month key into the service. Evidence: [20-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-03-PLAN.md:149), [20-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-03-PLAN.md:153), [20-04-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-04-PLAN.md:183).

- MEDIUM: `20-RESEARCH.md` still contradicts the revised plans on key points, including nullable `TranscriptFetchResult?`, `.Take().ToListAsync()`, and throwing on missing ffmpeg. Since every plan tells executors to read research, this is an execution hazard. Either amend research or add a clear “plans supersede stale research” note. Evidence: [20-RESEARCH.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-RESEARCH.md:321), [20-RESEARCH.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-RESEARCH.md:407), [20-RESEARCH.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/20-content-kb-ingestion-transcription-local/20-RESEARCH.md:458).

**Checks**

Single persistence owner: holds structurally. Audio pipeline: composable and mostly complete, except the cap-duration hole above. `SkippedOverCap` collapse: resolved. Polly wrapper: specified and tested. Provider factory: real fail-fast toggle. Async-LINQ: fixed in plans. Waves: acyclic; no same-wave file overlap. KB-03/04/05: covered, but KB-05 is blocked by the duration wiring issue. Scope stays Phase 20.

VERDICT: HIGH

---

## Codex RE-REVIEW (iteration 3, after replan e37a729) — VERDICT: GREEN

Checked the plan files on disk, not just the pasted text.

**Iter-2 Findings**
- NEW HIGH cap duration: RESOLVED. `knownDuration` is threaded `FetchTranscriptAsync → TranscribeAsync`; cap projection uses `Math.Max(knownDuration?.TotalSeconds ?? 0d, audio.DurationSeconds)`, and `<= 0` fails with no API call.
- MED month-key consistency: RESOLVED. The verb derives one `monthKey` per video, passes it through fetch/transcribe, and reuses it for `RecordCallAsync`. The service is explicitly barred from minting `DateTime.UtcNow`.
- MED stale research: RESOLVED. `20-RESEARCH.md` now has top-level and inline supersede notes for `TranscriptOutcome`, `CollectAsync(limit)`, ffmpeg warn/continue, authoritative duration, and month-key reuse.

**Original HIGH Regression Check**
- HIGH-1 single persistence owner: still resolved. Services remain pure; verb writes transcript/status/ledger.
- HIGH-2 audio path: still resolved via `IYouTubeAudioSource` + `AudioDownloadResult`.
- HIGH-3 non-null distinct outcomes: still resolved; `SkippedOverCap` is not collapsed into `Failed`.
- HIGH-4 Polly wrapper: still resolved; transcribe delegate must run through `WhisperResiliencePipeline`.
- HIGH-5 provider toggle: still resolved via real `TranscriptProviderFactory`.
- HIGH-6 bounded listing: still resolved via `CollectAsync(limit)`, no async-LINQ `ToListAsync`.

**Plan Invariants**
- Signature consistency: clean across 20-02/20-03/20-04.
- Waves: acyclic. `20-01`, `20-02` wave 1; `20-03` depends on `20-02`; `20-04` depends on all prior.
- Same-wave file overlap: none between `20-01` and `20-02`.
- KB coverage: KB-03 in 20-02/20-04; KB-04 and KB-05 in 20-03/20-04.
- Scope: still Phase 20. Phase 21 distillation/artifacts/tags/orchestrator remain deferred; no Render path.

Remaining concern: none HIGH. Minor implementation watch: keep the Whisper-success ledger/status writes ordered or handled so a failed `RecordCallAsync` cannot leave a false successful status. The current per-video catch should prevent that if implemented literally.

VERDICT: GREEN
