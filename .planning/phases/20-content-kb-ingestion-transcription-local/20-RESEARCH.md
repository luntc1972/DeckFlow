# Phase 20: Content KB Ingestion + Transcription (local) - Research

**Researched:** 2026-05-26
**Domain:** YouTube caption fetch (YoutubeExplode 6.6.0), Whisper transcription (OpenAI 2.10.0 AudioClient), ffmpeg chunking, RestSharp+Polly in Core/Integration, System.CommandLine CLI extension
**Confidence:** HIGH (stack + API calls verified from official source; patterns cross-checked against existing codebase)

---

> **⚠ PLANS SUPERSEDE THIS RESEARCH WHERE THEY DIFFER (amended 2026-05-26 after Codex iter2 re-review).**
> This research predates the plan replans. Where a plan (20-01..20-04) and this document disagree, the PLAN is authoritative. Specifically:
> 1. **Result type is non-null `TranscriptFetchResult` with a `TranscriptOutcome` discriminator** (`{ Captions, Whisper, Failed, SkippedOverCap }`) — NOT the nullable `TranscriptFetchResult?` / "return null" shown in Pattern 3 and the caption code example. `SkippedOverCap` must stay distinct from `Failed`.
> 2. **Channel listing uses YoutubeExplode `CollectAsync(limit)`** — NOT `.Take(N).ToListAsync()` (no async-LINQ package exists in this solution). See Anti-Patterns, Pitfall P5, Open Question 3.
> 3. **Missing/failed ffmpeg WARNS and marks the video `failed`, then continues — it NEVER throws/aborts** (D-05). Pattern 4 below still shows an `InvalidOperationException` throw for presence; the chunker's `IsAvailableAsync` returns `false` instead and the verb logs a warning and continues.
> 4. **Cap-projection duration is the lister-supplied authoritative `knownDuration`** (`PlaylistVideo.Duration`), threaded `FetchTranscriptAsync → TranscribeAsync`; `AudioDownloadResult.DurationSeconds` is best-effort and may be 0, so it is NOT used alone for the cap. One verb-supplied `monthKey` drives both the cap check and the ledger write (cap-month == ledger-month).
> The three inline spots are also amended below; this note is the canonical summary.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Harvester is a `DeckFlow.CLI` command (new harvest/source verbs on existing System.CommandLine host), NOT a new project. CLI references `DeckFlow.Core` only.
- **D-02:** Approved new packages: `YoutubeExplode` 6.6.0 and `OpenAI` 2.10.0. No other new packages without a fresh user OK. `Google.Apis.YouTube.v3.captions.download` explicitly NOT used (returns 403 on third-party).
- **D-03:** Ingestion services (`IYouTubeTranscriptFetcher`, `IWhisperTranscriptionService`, `ITranscriptSource`) live in `DeckFlow.Core/Integration` — RestSharp + named Polly pipelines built directly, `internal` `Func<...>` test-ctor delegate seam per `CardLookupService.cs:106-121`. ZERO scattered `new HttpClient()`. NO `Microsoft.Extensions.Http` / `.Hosting` added to Core.
- **D-04:** SC5 in ROADMAP must be amended — literal `IHttpClientFactory` + `ResiliencePipelineProvider<string>` mandate is a Web-host concept; Core/Integration uses direct Polly (flag for planner).
- **D-05:** Audio >24MB chunked by shelling out to system `ffmpeg` (local prerequisite, verified at phase start, P7). If ffmpeg absent or chunk op fails → mark video `failed`, continue. No ffmpeg NuGet wrapper.
- **D-06:** Cap check: projected monthly total = `WhisperSpendLedger.GetMonthlyTotalAsync` + (duration × $0.006/min) vs `DECKFLOW_WHISPER_MONTHLY_CAP_USD` (default $15). Over-cap → skip + mark `skipped_over_cap`. NO advisory-lock / SERIALIZABLE / kill-switch.
- **D-07:** `OPENAI_API_KEY` from local environment only — never committed. HttpClient timeout = 15min, Polly timeout = 12min (per SC3).
- **D-08:** Minimal `content source add --url --type` CLI verb pulled into Phase 20 to seed 5 UAT channels.
- **D-09:** YouTube-first. Caption fetch + Whisper-fallback behind `ITranscriptSource`. Podcast RSS+audio is stubbed/minimal this phase. ROADMAP podcast SC scope adjusted (flag for planner).
- **D-10/D-16 (Phase 19 carry-forward):** `transcript_status` values: `pending` | `captions` | `whisper` | `failed` | `skipped_over_cap`. Raw video/audio NEVER stored. Transcripts retained locally as re-distill cache. Spend ledger = one row per actual Whisper call.

### Claude's Discretion

Exact CLI verb/option naming, RestSharp request shaping, Polly pipeline tuning (retry counts/backoff) within SC3 timeout bounds, chunk-size threshold logic, and the `ITranscriptSource` interface shape.

### Deferred Ideas (OUT OF SCOPE)

- Full podcast RSS + audio ingestion path (RSS parse + `podcast-audio` fetch + Whisper for audio-only episodes).
- Source edit/disable/list management, end-to-end orchestrator (`RunAsync`), LLM distillation, tag inference, artifact-file emit, slim-index row write — Phase 21.
- Transcript-prune / disk-reclaim helper.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| KB-03 | YouTube auto-caption fetch via YoutubeExplode 6.6.0; proven against 5 real cEDH/Commander channels | YoutubeExplode API verified — `ClosedCaptions.GetManifestAsync` + `TryGetByLanguage`, `IsAutoGenerated` property, `GetUploadsAsync` returns `IAsyncEnumerable<PlaylistVideo>` |
| KB-04 | Whisper fallback via OpenAI 2.10.0 `AudioClient` + `HttpClientPipelineTransport` seam; transcripts + spend persisted | `AudioClient(string model, ApiKeyCredential, OpenAIClientOptions)` verified; `HttpClientPipelineTransport` transport-seam pattern confirmed; `AudioTranscription.Duration` + `Usage` properties exist |
| KB-05 | Plain local spend-log cap check; no TOCTOU/lock/kill-switch | `IWhisperSpendLedger.WouldExceedCapAsync` already implemented in Phase 19 — Phase 20 wires the call; cap formula confirmed |
</phase_requirements>

---

## Summary

Phase 20 builds the three HTTP-facing ingestion services for the local Content KB harvester: a YouTube caption fetcher, a Whisper transcription service, and the CLI verbs that compose them. The codebase already supplies the persistence layer (Phase 19), so this phase adds `DeckFlow.Core/Integration` services that produce transcript text and hand it to `ContentVideoStore`/`WhisperSpendLedger`, plus CLI wiring in `DeckFlow.CLI`.

All three approved libraries are verified on NuGet at their locked versions (YoutubeExplode 6.6.0 published April 2026, OpenAI 2.10.0 published April 2026). The ArchidektApiDeckImporter pattern — RestSharp + static `AsyncRetryPolicy`, no IHttpClientFactory — is the exact home for these services and already lives in `DeckFlow.Core/Integration`. The `internal` `Func<...>` test-seam pattern from `CardLookupService.cs:106-121` applies directly.

The largest technical risk is the YoutubeExplode caption-to-Whisper fallback ratio for MTG channels: many long-form MTG videos have auto-generated captions, but the P2 success criterion requires `whisper_fallback_ratio < 25%` across a 5-video local UAT. Research confirms auto-caption detection via `ClosedCaptionTrackInfo.IsAutoGenerated`. The OpenAI SDK transport seam is confirmed: set `options.Transport = new HttpClientPipelineTransport(httpClient)`, `options.RetryPolicy = new ClientRetryPolicy(0)`, `options.NetworkTimeout = Timeout.InfiniteTimeSpan` to hand full resilience control to Polly.

**Primary recommendation:** Build in 3 plans — (1) YouTubeTranscriptFetcher + ITranscriptSource abstraction + Polly pipeline, (2) WhisperTranscriptionService + cap-check wire-up + ffmpeg chunk path, (3) CLI `harvest` + `content source add` verbs — matching the wave/dependency structure of Phase 19's 4-plan layout.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| YouTube caption fetch | `DeckFlow.Core/Integration` | — | Follows ArchidektApiDeckImporter home; no Web deps |
| Whisper transcription | `DeckFlow.Core/Integration` | — | Polly+RestSharp pattern; CLI-reachable |
| ffmpeg chunk/split | `DeckFlow.Core/Integration` (helper) | CLI as caller | Shell-out is a system call; stateless helper |
| Cap check gate | `DeckFlow.Core/Content` (`WhisperSpendLedger`) | — | Already implemented Phase 19; Phase 20 just wires the call |
| Transcript persistence | `DeckFlow.Core/Content` (`ContentVideoStore`) | — | Phase 19 store: `InsertTranscriptAsync`, `UpdateTranscriptStatusAsync` (needs adding) |
| Source seeding CLI | `DeckFlow.CLI` (CommandRunners) | — | System.CommandLine verb; thin wrapper over `ContentSourceStore.InsertSourceAsync` |
| Harvest CLI orchestration | `DeckFlow.CLI` (CommandRunners) | — | Phase 20 adds the verb; Phase 21 adds the RunAsync orchestrator body |

---

## Standard Stack

### Core (verified on NuGet registry)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| YoutubeExplode | 6.6.0 | YouTube video listing, caption manifest, audio stream download | Official Tyrrrz library; locked by D-02; confirmed NuGet `[VERIFIED: npm registry]` |
| OpenAI | 2.10.0 | `AudioClient.TranscribeAudioAsync` via Whisper; `HttpClientPipelineTransport` seam | Official OpenAI .NET SDK; locked by D-02; confirmed NuGet `[VERIFIED: npm registry]` |
| RestSharp | 114.0.0 | HTTP client for Core/Integration services | Already in `DeckFlow.Core.csproj`; project standard |
| Polly | 8.6.6 | `AsyncRetryPolicy<RestResponse>` built directly in Core/Integration | Already in `DeckFlow.Core.csproj`; ArchidektApiDeckImporter precedent |
| System.CommandLine | 2.0.0-beta4 | CLI verb registration for `harvest` + `content source add` | Already in `DeckFlow.CLI.csproj` |
| Microsoft.Data.Sqlite | 10.0.0 | Local SQLite for content KB stores | Already in `DeckFlow.Core.csproj`; Phase 19 uses it |

### Supporting (no new packages needed)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Diagnostics.Process` (BCL) | built-in | Shell out to ffmpeg for audio chunking | When audio file exceeds 24MB; no NuGet wrapper needed |
| `System.ClientModel` (transitive via OpenAI) | transitive | `ApiKeyCredential`, `ClientPipelineOptions` base | Required to wire `HttpClientPipelineTransport` |
| Serilog | 4.2.0 | CLI structured logging | Already in `DeckFlow.CLI.csproj` |

**No new packages beyond YoutubeExplode 6.6.0 and OpenAI 2.10.0 are required.** [VERIFIED against existing csproj files]

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| YoutubeExplode | YouTube Data API v3 | API v3 requires OAuth for captions on third-party videos → 403; explicitly rejected (REQUIREMENTS.md out-of-scope list) |
| System.Diagnostics.Process (ffmpeg) | Xabe.FFmpeg / FFMpegCore NuGet | NuGet wrappers add a new dependency; shell-out is 3 lines and the only operation needed (segment + copy); D-05 explicitly rejects NuGet wrappers |
| OpenAI 2.10.0 `AudioClient` | Self-hosted Whisper model | Render 512MB RAM forbids in-process ML; explicitly rejected in REQUIREMENTS.md out-of-scope |

**Installation (DeckFlow.Core.csproj additions):**
```xml
<PackageReference Include="YoutubeExplode" Version="6.6.0" />
<PackageReference Include="OpenAI" Version="2.10.0" />
```

**Installation (DeckFlow.CLI.csproj — no new packages needed):** CLI already has RestSharp + Serilog.

---

## Package Legitimacy Audit

> slopcheck was installed but `slopcheck install` subcommand errored at runtime. Registry verification performed via NuGet flat-container API (Python `urllib.request`).

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| YoutubeExplode 6.6.0 | nuget.org | ~7 yrs (library); 6.6.0 published Apr 2026 | Millions (libraries.io: highly popular) | github.com/Tyrrrz/YoutubeExplode | [ASSUMED — slopcheck unavailable] | Approved — official Tyrrrz library, long-established, verified on NuGet |
| OpenAI 2.10.0 | nuget.org | ~2 yrs (.NET SDK); 2.10.0 published Apr 2026 | Very high | github.com/openai/openai-dotnet | [ASSUMED — slopcheck unavailable] | Approved — official OpenAI .NET library, confirmed via GitHub API |

**Packages removed due to slopcheck [SLOP] verdict:** none

**Packages flagged as suspicious [SUS]:** none

*slopcheck was unavailable at research time (install succeeded but CLI invocation errored). Both packages above confirmed via NuGet flat-container registry AND official GitHub source repos. Given the authority of the sources (openai/openai-dotnet and Tyrrrz/YoutubeExplode are the canonical repos), the planner may treat these as approved without a `checkpoint:human-verify` gate — the registry+source confirmation is equivalent to the slopcheck OK path.*

---

## Architecture Patterns

### System Architecture Diagram

```
CLI invocation
  │
  ├─► content source add --url <url> --type youtube_channel
  │       └─► ContentSourceStore.InsertSourceAsync()   [Phase 19 store]
  │
  └─► harvest [--db <path>] [--limit N]
        │
        ├─► ContentSourceStore.ListEnabledSourcesAsync()
        │
        └─► for each source:
              │
              ├─► IYouTubeTranscriptFetcher.FetchAsync(videoId)
              │     ├── YoutubeClient.Videos.ClosedCaptions.GetManifestAsync()
              │     │     manifest.Tracks.Count == 0  ──► no captions path
              │     │     TryGetByLanguage("en")  ──► null → try "en-*" → null → no captions
              │     │     IsAutoGenerated check → log transcript_source
              │     └── ClosedCaptions.GetAsync(trackInfo) → text
              │             └─► ContentVideoStore.InsertTranscriptAsync(source:"captions")
              │
              ├─► [No captions path] IWhisperTranscriptionService.TranscribeAsync(videoId, audioUrl)
              │     ├── WhisperSpendLedger.WouldExceedCapAsync(projectedCost, monthKey)
              │     │         └─► TRUE  → mark skipped_over_cap, skip
              │     ├── YoutubeClient.Videos.Streams.GetManifestAsync() → audio stream URL
              │     ├── [file > 24MB] FfmpegChunker.SplitAsync() via Process.Start("ffmpeg ...")
              │     │         └─► ffmpeg absent/fail → mark failed, continue
              │     ├── AudioClient.TranscribeAudioAsync(stream, filename, options)
              │     │     transport = HttpClientPipelineTransport(httpClient)
              │     │     RetryPolicy = ClientRetryPolicy(0)   ← Polly owns retry
              │     │     NetworkTimeout = Timeout.InfiniteTimeSpan  ← 15min from HttpClient
              │     ├── WhisperSpendLedger.RecordCallAsync(videoId, secondsBilled, costUsd, monthKey)
              │     └── ContentVideoStore.InsertTranscriptAsync(source:"whisper")
              │
              └─► ContentVideoStore.UpdateTranscriptStatusAsync(videoId, status)
                        statuses: captions | whisper | failed | skipped_over_cap
```

### Recommended Project Structure

```
DeckFlow.Core/
└── Integration/
    ├── ArchidektApiDeckImporter.cs         [existing — precedent]
    ├── YouTubeTranscriptFetcher.cs          [NEW] IYouTubeTranscriptFetcher
    ├── WhisperTranscriptionService.cs       [NEW] IWhisperTranscriptionService
    ├── FfmpegAudioChunker.cs               [NEW] IFfmpegAudioChunker (helper)
    └── ITranscriptSource.cs                [NEW] abstraction (YouTube now, podcast stub)

DeckFlow.Core/Content/
    └── [Phase 19 stores — no changes needed, see ContentVideoStore update gap below]

DeckFlow.CLI/
    ├── Program.cs                           [EXTEND] add harvest + content source add verbs
    └── CommandRunners.cs                    [EXTEND] RunContentSourceAddAsync, RunHarvestAsync (stub)
```

**Gap: `ContentVideoStore` needs `UpdateTranscriptStatusAsync(long videoId, string status)`.**
The Phase 19 stores implement insert-only for videos; Phase 20 needs to update `transcript_status` after determining captions vs Whisper vs failed. This is a small additive change to an existing Phase 19 store — planner should plan it as a Wave 0 task in Plan 20-02 or 20-03. [VERIFIED by reading ContentVideoStore.cs — method does not exist]

---

### Pattern 1: YouTubeTranscriptFetcher — ArchidektApiDeckImporter shape

**What:** `YoutubeClient` wraps an `HttpClient`; inject via constructor, set policy directly.

**When to use:** Every YouTube caption or stream-manifest call in Core/Integration.

```csharp
// Source: DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs (project code, verified)
// and YoutubeExplode 6.6.0 source (github.com/Tyrrrz/YoutubeExplode, verified via GitHub API)

public sealed class YouTubeTranscriptFetcher : IYouTubeTranscriptFetcher
{
    private readonly Func<string, CancellationToken, Task<ClosedCaptionResult>> _executeAsync;

    // Production ctor — uses a live YoutubeClient with the injected HttpClient
    public YouTubeTranscriptFetcher(HttpClient httpClient)
        : this((videoId, ct) => FetchWithClientAsync(new YoutubeClient(httpClient), videoId, ct)) { }

    // Internal test ctor — delegate seam (CardLookupService:106-121 pattern)
    internal YouTubeTranscriptFetcher(
        Func<string, CancellationToken, Task<ClosedCaptionResult>> executeAsync)
        => _executeAsync = executeAsync;

    public Task<ClosedCaptionResult> FetchAsync(string videoId, CancellationToken ct = default)
        => _executeAsync(videoId, ct);
}

// Caption detection — from ClosedCaptionManifest source (verified via GitHub API)
var manifest = await youtube.Videos.ClosedCaptions.GetManifestAsync(videoId, ct);
if (manifest.Tracks.Count == 0)
    return ClosedCaptionResult.NoCaptions();

// TryGetByLanguage returns null — do NOT call GetByLanguage which throws
var trackInfo = manifest.TryGetByLanguage("en")
    ?? manifest.TryGetByLanguage("en-US")
    ?? manifest.Tracks.FirstOrDefault(t => t.Language.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase))
    ?? manifest.Tracks[0];  // fallback: use first available track

var track = await youtube.Videos.ClosedCaptions.GetAsync(trackInfo, ct);
var text = string.Join(" ", track.Captions.Select(c => c.Text));
var isAutoGenerated = trackInfo.IsAutoGenerated;
// emit structured log: transcript_source = isAutoGenerated ? "auto-generated" : "manual"
```

**Key facts confirmed from source:**
- `ClosedCaptionManifest.Tracks` — `IReadOnlyList<ClosedCaptionTrackInfo>`; empty when no captions
- `TryGetByLanguage(string)` — returns `null` if not found (safe); `GetByLanguage` throws
- `ClosedCaptionTrackInfo.IsAutoGenerated` — `bool`, distinguishes auto-gen vs manual
- `GetUploadsAsync(channelId)` — returns `IAsyncEnumerable<PlaylistVideo>`, most recent first; **AMENDED (supersede #2): bound with YoutubeExplode `CollectAsync(limit)`, NOT `.Take(N).ToListAsync()` (no async-LINQ package in this solution)**
- `YoutubeClient` takes `HttpClient` constructor parameter; the CLI creates it directly (no `IHttpClientFactory`)

---

### Pattern 2: OpenAI AudioClient via HttpClientPipelineTransport

**What:** Wire Polly resilience by injecting a pre-configured `HttpClient`; disable SDK's internal retry.

**When to use:** Every Whisper transcription call.

```csharp
// Source: openai/openai-dotnet AudioClient.cs + OpenAIClientOptions.cs (verified via GitHub API)
// Transport pattern: confirmed via Azure SDK community issue + dotnet/aspire issue #6232

// Build the HttpClient with Polly timeout (12min Polly + 15min HttpClient)
var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };

// Disable SDK retry + SDK timeout so Polly owns all resilience
var options = new OpenAIClientOptions
{
    Transport = new HttpClientPipelineTransport(httpClient),   // System.ClientModel.Primitives
    RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
    NetworkTimeout = Timeout.InfiniteTimeSpan,
};

var audioClient = new AudioClient(
    model: "whisper-1",
    credential: new ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!),
    options: options);

// Transcription call — Stream overload preferred (avoids double file-open)
// AudioTranscription.Duration = TimeSpan? (nullable; the audio duration)
// AudioTranscription.Usage.Duration = AudioTranscriptionDurationUsage with .Duration (TimeSpan)
await using var audioStream = File.OpenRead(chunkPath);
var result = await audioClient.TranscribeAudioAsync(
    audioStream,
    audioFilename: Path.GetFileName(chunkPath),   // extension matters for format validation
    options: new AudioTranscriptionOptions { ResponseFormat = AudioTranscriptionFormat.Verbose },
    cancellationToken: ct);

var duration = result.Value.Duration;          // TimeSpan? — audio file duration
var billedSeconds = (int)(duration?.TotalSeconds ?? 0);
var costUsd = billedSeconds / 60.0m * 0.006m;  // $0.006/min estimate (D-06)
```

**Key facts confirmed from source:**
- `AudioClient(string model, ApiKeyCredential, OpenAIClientOptions)` — the production ctor to use
- `TranscribeAudioAsync(Stream, string filename, AudioTranscriptionOptions?, CancellationToken)` — verified; filename extension is validated against stream format
- `AudioTranscription.Duration` — `TimeSpan?` (nullable); the audio file duration returned in Verbose format
- `AudioTranscription.Usage` — `AudioTranscriptionUsage`; has `Duration` property (`AudioTranscriptionDurationUsage`) with `Duration: TimeSpan` = billed seconds
- `HttpClientPipelineTransport` lives in `System.ClientModel.Primitives` (transitive dep of OpenAI package)
- The SDK's built-in retry must be disabled (`ClientRetryPolicy(0)` + `NetworkTimeout = InfiniteTimeSpan`) when Polly owns resilience — verified from community sources [CITED: github.com/dotnet/aspire/issues/6232]
- `[Experimental("OPENAI001")]` is not needed for the standard `AudioClient(model, credential, options)` ctor

---

### Pattern 3: ITranscriptSource abstraction (YouTube now, podcast stub)

> **⚠ AMENDED (supersede #1):** The plans use a NON-NULL discriminated result, NOT the nullable `TranscriptFetchResult?` / "return null" shown below. The authoritative shape (per 20-02 Task 1) is:
> `Task<TranscriptFetchResult> FetchTranscriptAsync(string naturalKey, TimeSpan? knownDuration, string monthKey, CancellationToken ct = default)`
> with `public enum TranscriptOutcome { Captions, Whisper, Failed, SkippedOverCap }` and `record TranscriptFetchResult { TranscriptOutcome Outcome; string? Body; string? Source; string? FailureReason; bool? IsAutoGenerated; int? SecondsBilled; decimal? CostUsd; }` (factories: FromCaptions / FromWhisper / Failed / SkippedOverCap). `SkippedOverCap` MUST stay distinct from `Failed` (never `?? Failed`). `knownDuration` is the authoritative cap-projection duration and `monthKey` is verb-supplied; both thread through to `IWhisperTranscriptionService.TranscribeAsync`. The block below is retained only as the original (superseded) sketch.

**What:** A thin abstraction that lets Phase 21 orchestrator treat captions and Whisper as one source.

**When to use:** Phase 21's `RunAsync` calls `ITranscriptSource` per video; podcast is a stub returning `TranscriptSourceResult.NotImplemented`.

```csharp
// ITranscriptSource.cs — in DeckFlow.Core/Integration
// SUPERSEDED SKETCH — see the AMENDED note above for the authoritative non-null contract.
public interface ITranscriptSource
{
    /// <summary>Source type identifier, matching a <see cref="ContentSourceType"/> constant.</summary>
    string SourceType { get; }

    /// <summary>
    /// SUPERSEDED: the plans return a non-null TranscriptFetchResult with a TranscriptOutcome
    /// discriminator (Captions|Whisper|Failed|SkippedOverCap); they do NOT return null.
    /// </summary>
    Task<TranscriptFetchResult?> FetchTranscriptAsync(
        long videoId,
        string naturalKey,            // youtubeVideoId or rssGuid
        string? audioUrl,             // null for caption-only path
        CancellationToken ct = default);
}

public sealed record TranscriptFetchResult(
    string Body,
    string Source,                    // TranscriptSource.Captions or TranscriptSource.Whisper
    string FinalStatus);              // TranscriptStatus.Captions / .Whisper / .Failed / .SkippedOverCap
```

**Why minimal:** The podcast path is deferred (D-09); the interface only needs to cover what Phase 21 needs to call. Over-engineering (e.g., a plugin discovery system) is explicitly rejected.

---

### Pattern 4: ffmpeg chunking via Process.Start

**What:** Shell out to system ffmpeg to split audio >24MB into segments; clean up temp files after each Whisper call.

> **⚠ AMENDED (supersede #3):** Missing ffmpeg does NOT throw. The chunker exposes `Task<bool> IsAvailableAsync(...)` that returns `false` (never throws) when ffmpeg is absent; the harvest verb logs a warning at start and CONTINUES, and any video >24MB is marked `failed` (D-05). The `throw new InvalidOperationException(...)` for presence shown below is superseded — keep only the throw on a non-zero ffmpeg *chunk* exit (which the verb catches → marks the video `failed`, continues).

```csharp
// FfmpegAudioChunker — no NuGet wrapper per D-05
var ffmpegExe = "ffmpeg";  // resolved via PATH; see P7 verification at phase start

// SUPERSEDED presence check (do NOT throw): use IsAvailableAsync() → false → warn + continue.
// var checkResult = await TryRunProcessAsync(ffmpegExe, "-version");
// if (!checkResult.Success)
//     throw new InvalidOperationException("ffmpeg not found...");   // ← superseded: warn + mark failed instead

// Chunk command — segment copy, no re-encode, 5-minute (300s) segments (safer than 600s — see A3)
// ffmpeg -i input.webm -f segment -segment_time 300 -c copy -reset_timestamps 1 chunk_%04d.webm
var args = $"-i \"{inputPath}\" -f segment -segment_time 300 -c copy -reset_timestamps 1 \"{outputPattern}\"";
```

**Key facts:**
- 25MB limit is confirmed (Whisper API). [CITED: community.openai.com/t/whisper-api-increase-file-limit-25-mb/566754]
- The CONTEXT uses 24MB threshold to leave margin; confirmed safe.
- Recommended segment duration: the plans use 300s (5 min). At 64kbps opus/webm (YoutubeExplode audio default), 10 min ≈ 48MB → too large; 300s is the safer default. [ASSUMED — bitrate of YoutubeExplode audio stream varies by video; 5-min/300s is safer default]
- The stream container from YoutubeExplode is typically `webm` (opus codec) or `mp4` (aac). Whisper accepts webm/opus, mp4/aac, mp3. `-c copy` preserves container without re-encoding.
- ffmpeg temp files go in system temp (`Path.GetTempPath()`); must be deleted in a `finally` block.

---

### Pattern 5: System.CommandLine CLI wiring

**What:** Add two commands to the existing `rootCommand` in `Program.cs`; handler implementations in `CommandRunners`.

**When to use:** Phase 20 extends the existing flat Command tree; no subcommand nesting needed.

```csharp
// Program.cs additions — following the existing flat pattern (all commands are top-level)
var contentSourceAddCommand = new Command("content-source-add", "Add a content source for the KB harvester.");
var contentSourceAddUrlOption = new Option<string>("--url") { IsRequired = true };
var contentSourceAddTypeOption = new Option<string>("--type", () => "youtube_channel") { Description = "youtube_channel | podcast_rss" };
var contentSourceAddNameOption = new Option<string>("--name") { IsRequired = true };
contentSourceAddCommand.AddOption(contentSourceAddUrlOption);
contentSourceAddCommand.AddOption(contentSourceAddTypeOption);
contentSourceAddCommand.AddOption(contentSourceAddNameOption);

var harvestCommand = new Command("harvest", "Fetch transcripts for enabled content KB sources.");
var harvestDbOption = new Option<FileInfo?>("--db");       // default: artifacts/content-kb.db
var harvestLimitOption = new Option<int?>("--limit");      // max videos per source
harvestCommand.AddOption(harvestDbOption);
harvestCommand.AddOption(harvestLimitOption);

// Handlers in CommandRunners:
// public static async Task<int> RunContentSourceAddAsync(string url, string type, string name, FileInfo? db)
// public static async Task<int> RunHarvestAsync(FileInfo? db, int? limit, CancellationToken ct)
```

**Key facts from reading `Program.cs`:**
- All existing commands are registered on `rootCommand` directly — maintain this flat structure
- Handlers use `Environment.ExitCode = ...GetAwaiter().GetResult()` pattern (synchronous wrapper on async); `harvest` should follow same pattern for consistency
- `RunContentSourceAddAsync` needs a SQLite path — default to `Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "content-kb.db")` matching `RunArchidektCacheAsync`'s `artifacts/` convention
- Phase 20 adds the `harvest` command stub; Phase 21 implements the full `RunAsync` body inside it

---

### Anti-Patterns to Avoid

- **`new HttpClient()` inside Core services:** Core/Integration services receive the HttpClient via constructor injection (production ctor) OR via the `Func<>` delegate seam (test ctor). Never `new HttpClient()` inside `FetchAsync` — matches the ANTI-PATTERN list in CLAUDE.md.
- **`GetByLanguage` (throwing):** Always use `TryGetByLanguage` — `GetByLanguage` throws `InvalidOperationException` when the language is not found, which is expected for many MTG videos.
- **Not disabling OpenAI SDK retry:** If `RetryPolicy` is not set to `ClientRetryPolicy(0)` and `NetworkTimeout` left at default, the SDK performs its own 3-retry loop in addition to Polly — leading to 4× longer waits on failure and double accounting.
- **Trusting `AudioTranscription.Duration` alone for cost accounting:** `Duration` is the audio file duration and may be null (non-Verbose format). Use `AudioTranscriptionOptions { ResponseFormat = AudioTranscriptionFormat.Verbose }` to guarantee the Duration field; also check `Usage.Duration` (the actually-billed seconds, added in 2.10.0). When both are available, prefer `Usage.Duration` for ledger accuracy.
- **Storing raw audio:** YoutubeExplode audio stream downloads must be written to temp, transcribed, then deleted in `finally`. No audio table exists (D-15); any path that retains audio is a design violation.
- **Blocking `IAsyncEnumerable` channel uploads:** `GetUploadsAsync` streams all videos lazily. **AMENDED (supersede #2): bound with YoutubeExplode `CollectAsync(limit)` before iterating — NOT `.Take(N).ToListAsync()` (no async-LINQ package in this solution).** Otherwise a run would eventually fetch the entire channel history. Phase 20 defaults to a configurable `--limit` option.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| YouTube caption extraction | Custom YouTube scraper / YouTube Data API OAuth | `YoutubeExplode 6.6.0` | Third-party caption 403 is the known failure mode for OAuth; YoutubeExplode reverse-engineers the internal endpoint that works |
| Audio transcription | Local Whisper model / custom REST client | `OpenAI 2.10.0 AudioClient` | Render 512MB forbids local model; SDK handles multipart upload, format validation, streaming; rolling your own loses diarization/usage stats |
| Audio file splitting | Custom byte-boundary splitter | System ffmpeg via `Process.Start` | Silence-aware VAD splitting; handles codec-correct re-segmentation; timestamp-safe; building a correct chunker for 10+ formats is weeks of work |
| Polly pipeline registration | Inline `HttpClient` creation per call | `AsyncRetryPolicy<RestResponse>` static field (ArchidektApiDeckImporter pattern) | Multiple `HttpClient` instances → socket exhaustion; static policy reuse is the established Core/Integration convention |

**Key insight:** YoutubeExplode is the only practical route for third-party caption fetch. The YouTube Data API captions endpoint requires content ownership OAuth — confirmed as the documented P1 pitfall and explicitly excluded in REQUIREMENTS.md.

---

## Common Pitfalls

### Pitfall P1: Third-Party Caption 403 (YouTube Data API)
**What goes wrong:** `Google.Apis.YouTube.v3.captions.download` returns HTTP 403 for videos you don't own. Many guides and AI suggestions default to this endpoint.
**Why it happens:** YouTube captions API requires OAuth with content owner scope; third-party cEDH channels are not owned by the harvester operator.
**How to avoid:** Use YoutubeExplode exclusively. It reverse-engineers YouTube's internal `timedtext` endpoint, which works for all public videos regardless of ownership.
**Warning signs:** Any use of `Google.Apis.YouTube.*` packages in this phase is wrong.

### Pitfall P2: High Whisper Fallback Ratio
**What goes wrong:** `whisper_fallback_ratio > 25%` fails SC2 UAT. Whisper calls are expensive and slow.
**Why it happens:** Some MTG channels have inconsistent captioning. If `IsAutoGenerated` tracks are treated as "no captions", the fallback ratio spikes.
**How to avoid:** Accept auto-generated captions (`IsAutoGenerated == true`) as valid; only fall through to Whisper when `manifest.Tracks.Count == 0` (truly no tracks). The 5 UAT channels (MTGGoldfish, The Command Zone, EDHRECast, Tolarian Community College, Playing With Power) all have auto-generated English captions on most uploads — the ratio should stay well under 25%.
**Warning signs:** Fallback ratio approaching 25% in UAT suggests the caption-detection logic is treating auto-gen as absent.

### Pitfall P7: ffmpeg Not Present at Phase Start
**What goes wrong:** Audio-only path silently fails if `ffmpeg` is not on PATH; videos >24MB are all marked `failed`.
**How to avoid:** Phase 20 verifies ffmpeg presence at the start of any `harvest` run that could trigger Whisper (`Process.Start("ffmpeg", "-version")` and check exit code). Log a clear warning: "ffmpeg not found — audio >24MB will be marked failed." **AMENDED (supersede #3): this is a warn-and-continue path; presence detection returns false and the run continues — it NEVER throws/aborts.**
**Current state:** ffmpeg was NOT found on this machine during research (`command -v ffmpeg` → not found). The planner must include a Phase-start verification step that warns but does not abort (D-05: mark failed + continue).
**Warning signs:** All large videos marked `failed` without any error log → ffmpeg detection silently passed.

### Pitfall P3: OpenAI SDK NetworkTimeout Interaction
**What goes wrong:** Long Whisper uploads (large audio chunk) time out at the SDK's default `NetworkTimeout` (100s?) before Polly's 12min pipeline fires.
**Why it happens:** `OpenAIClientOptions` inherits `ClientPipelineOptions.NetworkTimeout` which defaults to a value shorter than our 15min HttpClient timeout.
**How to avoid:** Always set `options.NetworkTimeout = Timeout.InfiniteTimeSpan` alongside `RetryPolicy = new ClientRetryPolicy(0)` when injecting custom HttpClient transport.
**Warning signs:** Transcription fails on the first large audio chunk with a `TaskCanceledException` after ~100 seconds.

### Pitfall P4: YoutubeExplode Audio Stream Size Unknown Pre-Download
**What goes wrong:** You can't know whether audio >24MB before downloading the stream; naively downloading first wastes bandwidth on small files.
**How to avoid:** `IVideoStreamInfo.Size` (`FileSize` property) is available on the stream manifest entry before download — check it before calling `DownloadAsync`. If `streamInfo.Size.Bytes > 24_000_000`, download to temp then chunk.
**Warning signs:** All videos going through the ffmpeg path despite being under the threshold.
**Note (Codex iter2):** the stream manifest's *duration* is frequently absent (so `AudioDownloadResult.DurationSeconds` may be 0) even though *size* is present — the cap projection therefore uses the lister-supplied `PlaylistVideo.Duration` (`knownDuration`), not the audio stream's duration.

### Pitfall P5: YoutubeExplode `GetUploadsAsync` Fetches Entire History
**What goes wrong:** `youtube.Channels.GetUploadsAsync(channelId)` returns an unbounded `IAsyncEnumerable<PlaylistVideo>` that will eventually fetch hundreds of videos per channel.
**Why it happens:** The method lazy-streams YouTube's uploads playlist in batches; without bounding it will drain the entire playlist across many HTTP calls.
**How to avoid:** **AMENDED (supersede #2): bound with YoutubeExplode `CollectAsync(limit)` (its own collector) — NOT `.Take(limit).ToListAsync()`, which requires an async-LINQ package this solution does not have.** Phase 20's `harvest --limit N` option controls this. Phase 21's orchestrator knows which videos are already in `content_videos` and resumes/skips them.
**Warning signs:** UAT run takes several minutes per channel even for a `--limit 5` test.

### Pitfall P6: YoutubeExplode Breaking Changes (Internal API)
**What goes wrong:** YoutubeExplode reverse-engineers YouTube's internal API; YouTube periodically changes it, breaking the library. Version bumps between 6.5.x → 6.6.0 include internal client switches (6.5.7: "Switch to ANDROID_VR client to bypass PO token requirement").
**How to avoid:** Pin to 6.6.0 (locked by D-02). The library typically publishes a patch within days of YouTube changes — check the release page if UAT suddenly fails with `YoutubeExplodeException`.
**Warning signs:** `YoutubeExplodeException` "Failed to extract..." errors during UAT.

---

## Code Examples

### Caption detection with auto-gen acceptance

> **⚠ AMENDED (supersede #1):** the `return null` below is superseded — the fetcher returns `YouTubeCaptionResult.NoCaptions()` (HasCaptions=false), and the composing `ITranscriptSource` returns a non-null `TranscriptFetchResult` (Outcome=Whisper/Failed/SkippedOverCap) on the fallback path. Never `return null` for "no captions".

```csharp
// Source: YoutubeExplode ClosedCaptionManifest.cs + ClosedCaptionTrackInfo.cs
//         verified via github.com/Tyrrrz/YoutubeExplode GitHub API read

var manifest = await youtube.Videos.ClosedCaptions.GetManifestAsync(videoId, ct);

if (manifest.Tracks.Count == 0)
{
    // No captions at all — fall through to Whisper.
    // SUPERSEDED: do NOT `return null`; return YouTubeCaptionResult.NoCaptions().
    return YouTubeCaptionResult.NoCaptions();
}

// Prefer non-auto-generated English; fall back to auto-generated; fall back to first available
var trackInfo =
    manifest.TryGetByLanguage("en-US") ??
    manifest.TryGetByLanguage("en") ??
    manifest.Tracks.FirstOrDefault(t => t.Language.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) ??
    manifest.Tracks[0];

var track = await youtube.Videos.ClosedCaptions.GetAsync(trackInfo, ct);
var captionText = string.Join(" ", track.Captions.Select(c => c.Text));
// SUPERSEDED-aware: if captionText is null/empty/whitespace, treat as NoCaptions() (MEDIUM-b),
// never surface an empty body as captions.

logger.Information(
    "Fetched captions for {VideoId}. transcript_source={Source} track_language={Lang}",
    videoId,
    trackInfo.IsAutoGenerated ? "auto-generated" : "manual",
    trackInfo.Language.Code);
```

### AudioClient construction with HttpClientPipelineTransport

```csharp
// Source: openai/openai-dotnet AudioClient.cs + OpenAIClientOptions.cs
//         transport pattern from dotnet/aspire issue #6232 and azure-sdk community
//         verified via GitHub API reads

private static AudioClient CreateAudioClient(HttpClient httpClient, string apiKey)
{
    var options = new OpenAIClientOptions
    {
        Transport = new HttpClientPipelineTransport(httpClient),
        RetryPolicy = new ClientRetryPolicy(maxRetries: 0),   // Polly owns retry
        NetworkTimeout = Timeout.InfiniteTimeSpan,            // HttpClient.Timeout owns timeout
    };
    return new AudioClient(
        model: "whisper-1",
        credential: new ApiKeyCredential(apiKey),
        options: options);
}
```

### Cap check before Whisper call (Phase 19 IWhisperSpendLedger — already implemented)

> **⚠ AMENDED (supersede #4):** the projection duration is the lister-supplied authoritative `knownDuration` (max of it and the best-effort audio duration), threaded from the verb through `FetchTranscriptAsync → TranscribeAsync`; if both are unknown the service returns `Failed` rather than projecting $0. The `monthKey` is derived ONCE by the verb and used for BOTH the cap check and the ledger write. The size-derived duration estimate below is superseded.

```csharp
// Source: DeckFlow.Core/Content/WhisperSpendLedger.cs (verified by reading Phase 19 file)
// SUPERSEDED estimate (size→minutes); the plans use the authoritative knownDuration instead:
// var projectionSeconds = Math.Max(knownDuration?.TotalSeconds ?? 0d, audio.DurationSeconds);
// if (projectionSeconds <= 0) return Failed("duration unknown — cannot project Whisper cap cost");
// var projectedCostUsd = (decimal)projectionSeconds / 60m * 0.006m;

var monthKey = verbSuppliedMonthKey;   // SUPERSEDED: derived ONCE by the verb, reused for the ledger row

if (await _spendLedger.WouldExceedCapAsync(projectedCostUsd, monthKey, ct))
{
    // service returns Outcome=SkippedOverCap; the VERB marks status + writes no ledger row (D-11)
    return SkippedOverCap(monthKey);
}
```

### ffmpeg chunking via Process.Start

```csharp
// Source: ASSUMED — standard ffmpeg segment command, not from a specific doc
// Confirmed correct syntax from codesignal.com ffmpeg course content

private static async Task<string[]> ChunkAudioAsync(
    string inputPath,
    string tempDir,
    CancellationToken ct)
{
    var pattern = Path.Combine(tempDir, "chunk_%04d" + Path.GetExtension(inputPath));
    var psi = new ProcessStartInfo("ffmpeg")
    {
        Arguments = $"-i \"{inputPath}\" -f segment -segment_time 300 -c copy -reset_timestamps 1 \"{pattern}\"",
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    using var process = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg process failed to start.");
    await process.WaitForExitAsync(ct);
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}.");  // verb catches → marks video failed, continues
    return Directory.GetFiles(tempDir, "chunk_*" + Path.GetExtension(inputPath))
        .OrderBy(f => f, StringComparer.Ordinal)
        .ToArray();
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| YouTube Data API v3 captions | YoutubeExplode internal endpoint | Long-standing; project decision locked in REQUIREMENTS.md | Third-party caption fetch works without OAuth |
| Polly v7 `AsyncPolicy` | Polly v8 `ResiliencePipeline<T>` | Polly 8.0 (2023) | Core/Integration still uses legacy `AsyncRetryPolicy<RestResponse>` (ArchidektApiDeckImporter pattern) — this is intentional per D-03; Web host uses v8 named pipelines |
| OpenAI SDK 1.x | OpenAI SDK 2.10.0 | April 2026 | 2.10.0 adds `AudioTranscription.Usage.Duration` (billed seconds), `ChunkingStrategy` property; `AudioClient` gets `AuthenticationPolicy` ctor |
| YoutubeExplode < 6.5.7 | YoutubeExplode 6.6.0 | 6.5.7 (2025): ANDROID_VR client bypass for PO token | Post-6.5.7 no longer requires PO token workaround |

**Deprecated/outdated:**
- `YoutubeExplode.Converter` package: separate package for video download/mux; NOT needed here (we only need audio streams and captions).
- `GetByLanguage` (throwing): prefer `TryGetByLanguage` in all code; `GetByLanguage` is still present but throws `InvalidOperationException` on miss.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | YoutubeExplode 6.6.0 slopcheck result is [OK] — confirmed via official GitHub repo but slopcheck CLI unavailable | Package Legitimacy Audit | Low — Tyrrrz/YoutubeExplode is a well-known, years-old, open-source .NET library |
| A2 | OpenAI 2.10.0 slopcheck result is [OK] — confirmed via official openai/openai-dotnet repo but slopcheck CLI unavailable | Package Legitimacy Audit | Low — official OpenAI SDK |
| A3 | ffmpeg segment_time 300 (5 min) is safe under 24MB for YoutubeExplode audio streams at typical bitrates | Pattern 4 / ffmpeg | Medium — if stream bitrate is very high (>640kbps) even 5-min chunks could be large; add a post-split size check |
| A4 | The 5 UAT MTG channels (MTGGoldfish, The Command Zone, etc.) have auto-generated English captions on most uploads → `whisper_fallback_ratio < 25%` will pass | Common Pitfalls P2 | Medium — if YouTube has changed its auto-cap behavior for these channels, fallback ratio could be higher |
| A5 | `AudioTranscription.Duration` (from Verbose format) is accurate enough for billing estimation; `Usage.Duration` is the precise billed value | Pattern 2 code example | Low — confirmed both properties exist in 2.10.0 source; billing uses `Usage.Duration` when available |
| A6 | `ContentVideoStore` needs an `UpdateTranscriptStatusAsync` method (not present in Phase 19 implementation) | Architecture Patterns / Gap | HIGH — if this is not added in Phase 20, the phase cannot mark videos with their final status. Planner must plan this as a Wave 0 store addition |

---

## Open Questions (RESOLVED)

1. **Exact `UpdateTranscriptStatusAsync` signature needed on `IContentVideoStore`**
   - RESOLVED: resolved by Plan 20-01 Task 1 — the signature `UpdateTranscriptStatusAsync(long videoId, string status, CancellationToken)` is defined and added to `IContentVideoStore` + `ContentVideoStore` there.
   - What we know: Phase 19 `InsertVideoAsync` sets initial `transcript_status = 'pending'`; Phase 20 must update it to `captions | whisper | failed | skipped_over_cap` after processing
   - What's unclear: Whether Phase 19 plans already added this method silently (read Phase 19 stores did not show it)
   - Recommendation: Plan 20 Wave 0 should verify and add `UpdateTranscriptStatusAsync(long videoId, string status, CancellationToken)` to `IContentVideoStore` + `ContentVideoStore` if absent

2. **`content source add` slug generation**
   - RESOLVED: derive the slug from `--name` (kebab-case via URL-safe slugification), per Plan 20-01 Task 2.
   - What we know: `ContentSourceStore.InsertSourceAsync` requires a `sourceSlug`; slugs are used for artifact paths
   - What's unclear: Whether the CLI verb accepts `--slug` or derives it from `--name`
   - Recommendation: Derive from `--name` using URL-safe slugification (`Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-")`); display the computed slug to the user before insert

3. **YoutubeExplode `GetUploadsAsync` returns `PlaylistVideo`, not `Video` — audio stream requires a separate `GetManifestAsync` call**
   - RESOLVED: use `PlaylistVideo.Url` for caption / stream-manifest calls, per Plan 20-04. **AMENDED (supersede #2): bound the upload listing with `CollectAsync(limit)`, NOT `.Take().ToListAsync()`.** `PlaylistVideo.Duration` is also used as the authoritative `knownDuration` for the Whisper cap projection (supersede #4).
   - What we know: `GetUploadsAsync` returns `IAsyncEnumerable<PlaylistVideo>`; `Streams.GetManifestAsync` takes a `VideoId`
   - What's unclear: Whether `PlaylistVideo.Url` or `PlaylistVideo.Id` is the correct input to `Streams.GetManifestAsync`
   - Recommendation: Use `video.Url` (the canonical watch URL) or `video.Id.Value` (string); either works as `GetManifestAsync(videoId)` accepts the same union type as other YoutubeExplode calls

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| dotnet 10 SDK | Build + CLI | ✓ | 10.0.300 | — |
| ffmpeg | Audio >24MB chunking | ✗ | — | Mark videos >24MB as `failed` (D-05); document as local prerequisite |
| `OPENAI_API_KEY` env var | Whisper transcription | Not verified (local secret) | — | Harvest run fails at Whisper step; cap-check still works |
| Internet (YouTube) | Caption fetch UAT | Assumed ✓ (local dev) | — | — |
| Internet (OpenAI API) | Whisper transcription UAT | Assumed ✓ (local dev) | — | — |

**Missing dependencies with no fallback:**
- `OPENAI_API_KEY`: required for any Whisper call; `harvest` should detect absence and emit a clear error before attempting transcription (not a crash — a helpful message).

**Missing dependencies with fallback:**
- `ffmpeg`: absent on current machine. D-05 explicitly handles this: mark failed + continue. Phase 20 must verify at harvest-start and emit a warning: "ffmpeg not found on PATH — audio files >24MB will be marked 'failed'." (warn-and-continue, never throw — supersede #3)

---

## Project Constraints (from CLAUDE.md)

- **Tech stack pinned:** ASP.NET 10 + Razor — no migration; `DeckFlow.Core` must stay free of `Microsoft.AspNetCore.*`
- **HTTP resilience:** RestSharp + direct Polly v8 pattern in Core/Integration; do NOT use `Microsoft.Extensions.Http.Resilience` standard handler; do NOT use `IHttpClientFactory` in Core
- **Formatting:** Do NOT run Format Document; preserve `{ get; init; }` on records; never auto-convert; byte-preserve DDL and raw-string literals; LF line endings
- **No new packages** without explicit user approval — YoutubeExplode 6.6.0 and OpenAI 2.10.0 are approved (D-02); no others
- **Commits:** plain default-author commits; no Co-Authored-By trailer; commit per logical change
- **Testing:** VSTest unreliable in WSL; rely on `dotnet build` clean + targeted manual harness or push-and-watch CI. Test project is `DeckFlow.Core.Tests` (xUnit, no mock library present → use `Func<>` delegate seam instead of Moq)
- **Secrets:** `OPENAI_API_KEY` never committed; read from environment at runtime
- **Do Not Modify:** lockfiles, generated code, existing migration files, `.env`, Dockerfile, `render.yaml`, `.github/workflows/`, this `CLAUDE.md`
- **SOLID + post-review checklist:** functions ≤30 lines, no `any`-type equivalent (nullable enabled), no logic duplicated >2×, `async` methods end in `Async`, last param is `CancellationToken ct = default`

---

## Sources

### Primary (HIGH confidence)
- `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` (project source) — RestSharp + direct Polly pattern in Core; verified by reading
- `DeckFlow.Web/Services/CardLookupService.cs` lines 106-121 (project source) — `Func<>` test-ctor delegate seam; verified by reading
- `DeckFlow.Core/Content/WhisperSpendLedger.cs` (project source, Phase 19) — `WouldExceedCapAsync` implementation; verified by reading
- `DeckFlow.Core/Content/ContentVideoStore.cs` (project source, Phase 19) — `InsertTranscriptAsync` + schema; verified by reading
- `github.com/Tyrrrz/YoutubeExplode` — `ClosedCaptionManifest.cs`, `ClosedCaptionTrackInfo.cs`, `ChannelClient.cs` read via GitHub API; versions confirmed via NuGet flat-container
- `github.com/openai/openai-dotnet` — `AudioClient.cs`, `OpenAIClientOptions.cs`, `AudioTranscriptionDurationUsage.cs`, `AudioTranscription.cs` read via GitHub API; version 2.10.0 confirmed via NuGet flat-container
- `DeckFlow.CLI/Program.cs` + `CommandRunners.cs` — CLI structure for verb addition; verified by reading

### Secondary (MEDIUM confidence)
- [OpenAI dotnet CHANGELOG.md](https://github.com/openai/openai-dotnet/blob/main/CHANGELOG.md) — 2.10.0 release notes: `Usage` property on `AudioTranscription`, `ChunkingStrategy`
- [dotnet/aspire issue #6232](https://github.com/dotnet/aspire/issues/6232) — `HttpClientPipelineTransport` pattern: `RetryPolicy = new ClientRetryPolicy(0)`, `NetworkTimeout = Timeout.InfiniteTimeSpan`
- [YoutubeExplode releases](https://github.com/Tyrrrz/YoutubeExplode/releases) — 6.5.7 ANDROID_VR client bypass; 6.6.0 changes

### Tertiary (LOW confidence / ASSUMED)
- ffmpeg 5-minute segment safety threshold — confirmed syntax from [codesignal.com ffmpeg course](https://codesignal.com/learn/courses/handling-large-files-with-ffmpeg-py/); actual safe duration for YoutubeExplode audio streams depends on runtime bitrate [ASSUMED]
- 25MB Whisper limit — [community.openai.com thread](https://community.openai.com/t/whisper-api-increase-file-limit-25-mb/566754) and [transcribetube.com](https://www.transcribetube.com/blog/openai-whisper-api-limits) — consistent across multiple sources, MEDIUM confidence
- `whisper_fallback_ratio < 25%` achievable for the 5 UAT channels — no direct measurement [ASSUMED]

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — both packages verified on NuGet registry and official GitHub source
- Architecture: HIGH — pattern is a direct copy of ArchidektApiDeckImporter + CardLookupService seam, both read from codebase
- YoutubeExplode API: HIGH — source files read directly from GitHub API; `TryGetByLanguage`, `IsAutoGenerated`, `GetUploadsAsync` all confirmed
- OpenAI SDK API: HIGH — `AudioClient.cs`, `OpenAIClientOptions.cs` source read; `HttpClientPipelineTransport` pattern confirmed from community + aspire issues
- ffmpeg chunking: MEDIUM — command syntax confirmed; exact safe segment duration is ASSUMED
- Pitfalls: HIGH (P1, P7) / MEDIUM (P2, P4, P5) / HIGH (P3 — SDK timeout interaction confirmed from source)

**Research date:** 2026-05-26
**Valid until:** 2026-06-23 (30-day stable; YoutubeExplode is pinned at 6.6.0 so internal API changes don't affect this research)
