# Phase 20: Content KB Ingestion + Transcription (local) - Context

**Gathered:** 2026-05-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Build and prove the **local harvester's upstream ingestion services** — YouTube caption fetch (KB-03), Whisper transcription runtime (KB-04), and the plain local spend-cap check (KB-05) — wired through a Core-resident RestSharp + named-Polly resilience pattern, runnable from a `DeckFlow.CLI` harvest command. Phase 20 ends at: captions/transcripts fetched and persisted to the local SQLite `content_transcripts`, per-call spend rows written to `whisper_spend_ledger`, and the cap-check gating Whisper calls.

**In scope:** caption fetcher, Whisper transcription service, cap-check runtime, the `ITranscriptSource` abstraction, the YouTube audio-download contract for Whisper, a minimal `content source add` CLI verb to seed sources for UAT, structured `transcript_source` logging, the 5-channel local caption-coverage UAT.

**Out of scope (Phase 21):** the end-to-end orchestrator (`RunAsync`), LLM distillation (summary/clips), tag inference, artifact-file emit, slim-index row write, source edit/disable/list management, full podcast RSS+audio path. Nothing runs on Render this phase.
</domain>

<decisions>
## Implementation Decisions

### Packaging (locked pre-discussion)
- **D-01:** Harvester is a **`DeckFlow.CLI` command** (new harvest/source verbs on the existing System.CommandLine host alongside `compare`/`probe-moxfield`/`export-moxfield`/`archidekt-*`), NOT a new project. CLI references `DeckFlow.Core` only — this is why Phase-19 stores were relocated to `DeckFlow.Core/Content` (commit `ae9f2f5`).
- **D-02:** **Approved new packages** (CLAUDE.md dependency gate satisfied): `YoutubeExplode` 6.6.0 (YouTube auto-captions; `Google.Apis.YouTube.v3.captions.download` is NOT used — 403 on third-party) and `OpenAI` 2.10.0 (Whisper `AudioClient` via `HttpClientPipelineTransport(httpClient)` seam). No other new packages without a fresh OK.

### HTTP service placement + resilience (GA1)
- **D-03:** Ingestion services (`IYouTubeTranscriptFetcher`, `IWhisperTranscriptionService`, the `ITranscriptSource` abstraction) live in **`DeckFlow.Core/Integration`**, following the existing `ArchidektApiDeckImporter.cs` precedent — **RestSharp + named Polly pipelines built directly**, `internal` `Func<...>` test-ctor delegate seam per `CardLookupService.cs:106-121`, ZERO scattered `new HttpClient()`.
- **D-04:** This keeps the **SPIRIT** of ROADMAP SC5 (named resilience pipelines, no raw clients, test seam) but drops the **literal** `IHttpClientFactory` + `ResiliencePipelineProvider<string>` mandate, which is a `DeckFlow.Web`-host concept the CLI/Core home cannot use. **SC5 needs amending** to the Core/Integration shape — flag for planner. No `Microsoft.Extensions.Http`/`.Hosting` packages added to Core (rejected the Generic-Host alternative to avoid extra framework deps + protect Core's lean surface).

### Whisper transcription + cap check (GA2, KB-04/KB-05)
- **D-05:** Audio >24MB is chunked client-side by **shelling out to a system `ffmpeg`** treated as a documented **local harvester prerequisite** (verified at phase start per Pitfall 7). No ffmpeg NuGet wrapper. If `ffmpeg` is absent or a chunk op fails → mark the video `failed` and continue the run (no abort).
- **D-06:** Carry forward Phase-19 cap mechanics: before any Whisper call, projected monthly total = `WhisperSpendLedger.GetMonthlyTotalAsync(monthKey)` + (duration × **$0.006/min** estimate); compare to env `DECKFLOW_WHISPER_MONTHLY_CAP_USD` (default **$15.00**) via the stubbed `WouldExceedCapAsync`. Over-cap → skip call, mark video `skipped_over_cap`. NO advisory-lock / SERIALIZABLE / kill-switch (single-user local).
- **D-07:** `OPENAI_API_KEY` read from local environment/secrets only — never committed. HttpClient timeout = 15min, Polly timeout = 12min (per SC3).

### Source bootstrap for UAT (GA3)
- **D-08:** Pull a **minimal `content source add --url --type` CLI verb forward** into Phase 20 (thin wrapper over the already-built `ContentSourceStore`) to seed the 5 UAT channels. Real + reusable; Phase 21 layers edit/disable/list on top. Rejected the throwaway-fixture approach.

### Podcast scope (GA4)
- **D-09:** **YouTube-first.** Build YouTube caption fetch + Whisper-fallback end-to-end behind a clean `ITranscriptSource` abstraction. Whisper is fully exercised via caption-less YouTube videos. **Podcast RSS+audio fetch is stubbed/minimal this phase**; the full podcast path is a later slice. (ROADMAP goal's podcast mention is intentionally deferred — flag the SC scope adjustment for planner.)

### Carry-forward from Phase-19 CONTEXT (still binding)
- **D-10:** `transcript_status` / `source` discriminator values: `captions` | `whisper` | `failed` | `skipped_over_cap` (align with KB-04 `source` discriminator). Raw video/audio NEVER stored (D-15): YouTube fetches caption text only; podcast audio is transient-for-Whisper then discarded — no audio table. Transcripts retained locally as a re-distill cache (D-16) — never re-pay Whisper on re-run. Spend ledger = one row per actual Whisper call.

### Persistence ownership (D-11 — added 2026-05-26 from Codex review)
- **D-11:** **PURE services + harvest verb is the SINGLE persistence owner.** The ingestion services (`IYouTubeTranscriptFetcher`, `IWhisperTranscriptionService`, `ITranscriptSource`/`YouTubeTranscriptSource`, `IYouTubeAudioSource`, `IFfmpegAudioChunker`) return **status-carrying RESULTS and persist NOTHING** — they touch no `ContentVideoStore`, no `WhisperSpendLedger`, no DB at all. The Phase-20 `harvest` verb (`RunHarvestAsync`) is the ONE place that writes: it calls `InsertVideoAsync`, `InsertTranscriptAsync`, `UpdateTranscriptStatusAsync`, and `WhisperSpendLedger.RecordCallAsync` (the ledger row is written **on actual Whisper success only**, never on cap-skip/failure). This removes the original 20-03/20-04 duplicate-persistence conflict (Codex HIGH-1) and keeps every service unit-testable with no DB. Phase-21's real orchestrator later supersedes the thin verb. **Corollary:** because Whisper success returns billed seconds + cost in its result record, the harvest verb writes the ledger row from that result; the Whisper service computes cost but does not persist it.

### Claude's Discretion
- Exact CLI verb/option naming, RestSharp request shaping, Polly pipeline tuning (retry counts/backoff) within SC3 timeout bounds, chunk-size threshold logic, and the `ITranscriptSource` interface shape — planner/researcher decide.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + requirements
- `.planning/ROADMAP.md` §"Phase 20: Content KB Ingestion + Transcription (local)" — Goal + 6 Success Criteria (SC5 IHttpClientFactory mandate to be AMENDED per D-03/D-04; podcast SC scope adjusted per D-09).
- `.planning/REQUIREMENTS.md` KB-03 (YouTube/YoutubeExplode), KB-04 (Whisper runtime + transcript/ledger), KB-05 (plain local cap-check) + KB-section pivot note.
- `.planning/phases/19-content-kb-foundation-local-schema-contracts/19-CONTEXT.md` §decisions D-05..D-16 — spend-log shape, cap-check stub, transcript_status, raw-media policy, re-distill cache.

### Resilience + test-seam precedent (the pattern to copy)
- `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` — RestSharp + direct Polly in Core (the home + pattern for the new ingestion services).
- `DeckFlow.Web/Services/CardLookupService.cs:106-121` — `internal` `Func<...>` delegate test-ctor seam to mirror.
- `DeckFlow.Core/Content/WhisperSpendLedger.cs` — cap-check (`WouldExceedCapAsync`, `GetMonthlyTotalAsync`) to wire; `ContentVideoStore.cs` — `transcript_status` persistence.

### Pitfalls referenced by SCs (Phase-20 RESEARCH.md will expand)
- P1 (third-party caption 403), P2 (proxy-pluggable transcript provider + whisper_fallback_ratio<25%), P7 (local ffmpeg verified at phase start) — researcher to produce/confirm in `20-RESEARCH.md`.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `DeckFlow.Core/Content/*` stores (relocated this session) — `ContentVideoStore` (transcript persistence), `WhisperSpendLedger` (cap-check + ledger), `ContentSourceStore` (source seed). CLI-reachable now.
- `DeckFlow.CLI/Program.cs` + `CommandRunners.cs` — System.CommandLine host to extend with `harvest` + `content source add` verbs.
- `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` — RestSharp+Polly importer template.

### Established Patterns
- Core/Integration importers use RestSharp + direct Polly (no IHttpClientFactory) — the CLI/Core-friendly resilience home (D-03).
- Internal `Func<RestRequest,CancellationToken,Task<RestResponse<T>>>` test seam (canonical per CLAUDE.md).
- `{ get; init; }` on all records; byte-preserved DDL/raw strings; LF endings.

### Integration Points
- New ingestion services are PURE (D-11). The `harvest` verb (single persistence owner) wires their results into `ContentVideoStore`/`WhisperSpendLedger`. Phase 21 orchestrator supersedes the thin verb.
</code_context>

<specifics>
## Specific Ideas

- Proven UAT target channels (SC1): MTGGoldfish, The Command Zone, EDHRECast, Tolarian Community College, Playing With Power — captions fetched from the LOCAL harvester environment via YoutubeExplode.
- `DECKFLOW_YOUTUBE_TRANSCRIPT_PROVIDER` toggle for the proxy-pluggable abstraction (SC2, P2). A real factory/selector: `direct` is wired; unsupported values fail clearly.
- Structured log emits `transcript_source` (captions|whisper) AND `caption_track_kind` (manual|auto_generated) on every fetch; UAT asserts `whisper_fallback_ratio < 25%`.
</specifics>

<deferred>
## Deferred Ideas

- Full **podcast RSS + audio** ingestion path (RSS parse + `podcast-audio` fetch + Whisper for audio-only episodes) — later slice; only the `ITranscriptSource` abstraction is built now (D-09).
- Source **edit/disable/list** management + the end-to-end orchestrator + distillation/artifact emit — Phase 21.
- Transcript-prune / disk-reclaim helper — only if disk pressure ever appears (per Phase-19 D-16).

</deferred>

---

*Phase: 20-content-kb-ingestion-transcription-local*
*Context gathered: 2026-05-26*
