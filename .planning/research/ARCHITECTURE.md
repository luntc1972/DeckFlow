# v1.4 Integration Architecture

> Mode: Architecture for v1.4 (Content Knowledge Base + Admin Mobile + v1.3 Backlog Cleanup) — integration with existing DeckFlow ASP.NET 10 + Razor + Postgres on Render. Confidence: HIGH on existing-pattern reuse; MEDIUM on Whisper/LLM choice and Gemini-strategy fork.

## Summary

v1.4 = 5 feature clusters. **3 are tiny + independent** (WDG-04 modal, doc-comment backlog, Gemini unblock if split-message route) and **2 are heavy** (admin mobile sweep, Content KB Phase 1). Build order sequences small-low-risk first to keep test gate green while KB infrastructure lands.

Content KB lives in a **new `DeckFlow.Web/Services/Content/` subtree** mirroring `Services/Harvest/` and `Services/FeatureFlags/`. NO new `DeckFlow.Content` project — adds csproj plumbing, breaks Web DI composition root, contradicts CLAUDE.md "no framework migration this milestone."

**Reuse HarvestRunStore *pattern* but fork** into `ContentHarvestRunStore` — `harvest_runs` table is deck-harvest-shaped (`kind IN ('bulk','url')`, `decks_processed`). Multiplexing decks + content into one table forces nullable columns + CHECK-constraint widening. New table; same code shape.

All KB tables (`content_sources`, `content_videos`, `content_transcripts`, `content_summaries`, `content_clips`, `content_tags`, `whisper_spend_ledger`, `content_harvest_runs`) land in existing single Postgres instance via `IRelationalDialect` + per-store `EnsureSchemaAsync`. Schema namespacing via `content_*` prefix.

LLM summarization = **single dedicated client** (one provider, config-selected), NOT the 5-platform `AiPlatform` registry. Registry serves user-pasted multi-AI prompts; admin-side ingestion is server-to-server. Keep AiPlatform untouched.

Admin mobile sweep follows the v1.0 `site.css → site-common.css → site-mobile.css` factoring precedent: **add `admin-common.css` + `admin-mobile.css`**, not a `## Responsive` section in `admin.css`.

Gemini unblock — recommendation: **split-message strategy first** (Phase 999.2 D-08 invariant preserved, no new outbound HTTP, no new API key). Direct Gemini API only if split-message UX fails.

## Integration Map (Existing → New Wire-Ups)

| Existing component | Role for v1.4 | Touch |
|---|---|---|
| `DeckFlow.Web/Program.cs` (~50-189) | Register ~12 new services + 3-4 named HTTP clients + 4-5 Polly pipelines via `AddDeckFlowResiliencePipelines()`. New routes inherit `/Admin/*` BasicAuth branch automatically | Modified |
| `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` | Add named pipelines: `youtube`, `podcast-rss`, `whisper-api`, `llm-summarizer` | Modified |
| `DeckFlow.Core/Storage/IRelationalDialect.cs` + Sqlite/Postgres impls | NO interface changes; new stores carry own DDL constants + call `RelationalDatabaseConnection.OpenAsync()` (mirrors `HarvestRunStore.cs:69-103`) | Untouched |
| `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` | **Pattern source** — `ContentHarvestRunStore` is parallel impl, not subclass. Schema-gate + reaper + stats-invalidation seam mirrored | Reference template |
| `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagStore` + `IFeatureFlagCache` | Reuse to gate Content KB behind `content_kb_enabled` flag. DO NOT use for Whisper monthly cap | Reused |
| `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` | New `/Admin/Content*` routes inherit `/Admin/*` branch gate at `Program.cs:225-227`. Zero middleware changes | Reused |
| `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml` | 3 new sidebar nav entries: "Content Sources", "Content Harvest", "Content Spend" | Modified |
| `DeckFlow.Web/wwwroot/css/admin.css` | Factored: extract layout into `admin-common.css`; new `admin-mobile.css` for `@media` rules; `admin.css` becomes entry shim | Factored |
| `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs` + `Views/Admin/AdminFeedback/Detail.cshtml` | WDG-04 modal lands here — replace deferred `onsubmit` confirm with focus-trapped modal (v1.3 audit line 79 carry-over) | Modified |
| `DeckFlow.Web/Services/PromptBuilders/{Analysis,Comparison,MetaGap,FollowUp,SetUpgrade}/Gemini*PromptVariant.cs` | Gemini unblock split-message path — `Build()` emits N chunks with cursor markers | Modified (Path 1) OR untouched (Path 2) |
| `DeckFlow.Web/DeckFlow.Web.csproj` | Strip `<NoWarn>$(NoWarn);1591;1573;1587</NoWarn>` LAST, after ~88-type backfill | Modified (last) |
| `DeckFlow.Web/wwwroot/ts/` | New `admin-feedback-modal.ts` — native `<dialog>` per FEATURES.md, hand-rolled focus-trap. Compiles via existing MSBuild target | New TS module |

## New Components (per cluster)

### Cluster A — WDG-04 Focus-Trapped Modal

| Path | Responsibility |
|---|---|
| `wwwroot/ts/admin-feedback-modal.ts` | Native `<dialog>` + focus capture (first/last focusable), ARIA `role="dialog"` + `aria-modal="true"`, ESC → close, restore-focus. Hand-rolled (no npm dep) |
| `wwwroot/css/admin.css` (additions) | `.admin-modal`, `.admin-modal__backdrop`, `.admin-modal__panel`, focus-visible styles |
| `Views/Admin/AdminFeedback/Detail.cshtml` (or `_AdminConfirmModal.cshtml` partial) | Replaces deferred `onsubmit` confirm at line 41 |

### Cluster B — Doc-Comment Backlog

No new components. Per-file `<summary>` backfill across `Controllers/`, `Controllers/Admin/`, `Controllers/Api/`, `Services/`, `Models/`, `Models/Api/`, `Infrastructure/`, `Security/`, `ViewModels/`. ~88 types. Last step: strip `<NoWarn>`. PITFALLS.md flags Razor partial CS1591 quirk — may need scoped 1591 suppression even after backfill.

### Cluster C — Admin Mobile-Responsive Sweep

| Path | Responsibility |
|---|---|
| `wwwroot/css/admin-common.css` | NEW. Extracted layout primitives from `admin.css`: sidebar grid, table base, form base, focus primitives. Mirrors `site-common.css` role. Includes WDG-08 a11y rules extended to admin |
| `wwwroot/css/admin-mobile.css` | NEW. `@media (max-width: 768px)` rules: sidebar → top-of-page disclosure; tables → `overflow-x: auto` OR card-stack; forms → single-column; touch-target floor |
| `wwwroot/css/admin.css` | Reduced to import shim: `@import url('admin-common.css'); @import url('admin-mobile.css'); /* admin-specific overrides */` |
| `Views/Shared/_AdminLayout.cshtml` | Sidebar markup gains disclosure wrapper (`<details>`/`<summary>` no-JS fallback) |

### Cluster D — Gemini Unblock

**Path 1: Split-message (PREFERRED — preserves Phase 999.2 D-08)**

| Path | Responsibility |
|---|---|
| `Services/PromptBuilders/{Analysis,Comparison,MetaGap,FollowUp,SetUpgrade}/Gemini*PromptVariant.cs` | `Build()` emits N concatenated chunks with cursor markers + standing `GeminiJsonMandate` |
| `Views/Deck/{DeckAnalysis,DeckComparison,CedhMetaGap}.cshtml` | Gemini-only paste-stepper UI hint when `AiPlatform.Key == "Gemini"` |
| `DeckFlow.Web.Tests/PromptBuilders/Gemini*PromptVariantTests.cs` | New facts: chunk boundary correctness, mandate-on-final-chunk, byte-budget per chunk |

**Path 2: Direct Gemini API (FALLBACK only if Path 1 fails UAT)**

| Path | Responsibility |
|---|---|
| `Services/Gemini/GeminiApiClient.cs` + interface | IHttpClientFactory named `gemini-api`, RestSharp + Polly pipeline `gemini-api`. Hand-roll per STACK.md (not vendor SDK) |
| `Services/Gemini/GeminiApiOptions.cs` | API key + base URL + model name from env (`DECKFLOW_GEMINI_API_KEY`) |
| `Tests/Services/Gemini/GeminiApiClientTests.cs` | RichardSzalay.MockHttp fixtures |
| `AiPlatform.cs` | NEW variant: `AiPlatform.GeminiApi` alongside `AiPlatform.Gemini` (paste); `AiPlatform.All` extended |

### Cluster E — Content Knowledge Base Phase 1 (LARGEST)

**Postgres tables** (all `content_*` prefix, via per-store `EnsureSchemaAsync` mirroring `HarvestRunStore.cs:436-471`):

| Table | Purpose | Key columns |
|---|---|---|
| `content_sources` | Admin-curated YouTube channels + podcast RSS | `id UUID PK`, `kind TEXT CHECK ('youtube_channel','podcast_rss')`, `external_id TEXT`, `display_name TEXT`, `enabled BOOL`, `archetype_tags TEXT[]` (PG) / TEXT-JSON (SQLite), `created_utc TIMESTAMPTZ`, `last_harvested_utc TIMESTAMPTZ NULL` |
| `content_videos` | Per-episode metadata | `id UUID PK`, `source_id UUID FK`, `external_id TEXT`, `title TEXT`, `published_utc TIMESTAMPTZ`, `duration_seconds INT`, `url TEXT`, `transcript_status TEXT CHECK ('pending','captions','whisper','failed','skipped_over_cap')`, `ingested_utc TIMESTAMPTZ NULL`, UNIQUE(`source_id`,`external_id`) |
| `content_transcripts` | Raw transcript + source-of-record | `id UUID PK`, `video_id UUID FK UNIQUE`, `source TEXT CHECK ('youtube_captions','whisper')`, `language TEXT`, `text TEXT`, `whisper_seconds_billed NUMERIC(10,2) NULL`, `created_utc TIMESTAMPTZ` |
| `content_summaries` | Per-video LLM summary (1:1) | `id UUID PK`, `video_id UUID FK UNIQUE`, `model TEXT`, `prompt_version INT`, `summary_text TEXT`, `tokens_in INT`, `tokens_out INT`, `cost_usd NUMERIC(10,4)`, `created_utc TIMESTAMPTZ` |
| `content_clips` | Per-video timestamped excerpts | `id UUID PK`, `video_id UUID FK`, `start_seconds INT`, `end_seconds INT`, `excerpt_text TEXT`, `topic_tag TEXT` |
| `content_tags` | Many-to-many video ↔ tags | `video_id UUID FK`, `tag_kind TEXT CHECK ('archetype','strategy','bracket','format','card_category')`, `tag_value TEXT`, PRIMARY KEY (`video_id`,`tag_kind`,`tag_value`) |
| `whisper_spend_ledger` | Per-call spend record | `id UUID PK`, `video_id UUID FK NULL`, `seconds_billed NUMERIC(10,2)`, `cost_usd NUMERIC(10,4)`, `called_utc TIMESTAMPTZ DEFAULT now()`, `month_key TEXT GENERATED ALWAYS AS (to_char(called_utc,'YYYY-MM')) STORED` (PG) / app-computed (SQLite) |
| `content_harvest_runs` | Mirror `harvest_runs` shape, content-specific cols | `id UUID PK`, `state TEXT CHECK ('Queued','Running','Stopping','Succeeded','Failed','Cancelled')`, run timestamps, `sources_processed INT`, `videos_processed INT`, `transcripts_fetched INT`, `whisper_calls INT`, `whisper_spend_usd NUMERIC(10,4)`, `error_message TEXT NULL`, `aborted_reason TEXT NULL` |

**Monthly aggregate:** App-side over `whisper_spend_ledger.month_key` (single SELECT GROUP BY in `WhisperSpendLedger.GetMonthlyTotal(yearMonth)`). Postgres VIEW adds deployment friction; plain query is sub-ms.

**Monthly cap:** Env var `DECKFLOW_WHISPER_MONTHLY_CAP_USD` (default `5.00`). Typed-decimal; survives admin-UI accidents; not user-toggleable. `IFeatureFlagStore` is wrong tool for $-cap. NO `ContentBudgetStore`.

**Service layer (`DeckFlow.Web/Services/Content/`):**

| Path | Responsibility |
|---|---|
| `IContentSourceStore` + impl | CRUD over `content_sources` |
| `IContentVideoStore` + impl | CRUD + UNIQUE upsert |
| `IContentTranscriptStore` + impl | Read/write transcripts |
| `IContentSummaryStore` + impl | Read/write summaries with cost telemetry |
| `IContentClipStore` + impl | Per-video clip rows |
| `IContentTagStore` + impl | Many-to-many tag CRUD |
| `IWhisperSpendLedger` + impl | Append-only spend + `GetMonthlyTotalAsync` + `WouldExceedCapAsync` gate |
| `IYouTubeIngestionService` + impl | **YoutubeExplode 6.6.0** per STACK.md (NOT Google.Apis.YouTube.v3 — 403 on third-party). Named HTTP client `youtube`, Polly pipeline `youtube` |
| `IPodcastIngestionService` + impl | **System.ServiceModel.Syndication 10**. Named HTTP client `podcast-rss`, Polly pipeline `podcast-rss`. Parses RSS, extracts enclosure URLs |
| `IWhisperTranscriptionService` + impl | **OpenAI 2.10.0 SDK** + `HttpClientPipelineTransport(httpClient)` seam. Named HTTP client `whisper-api`, Polly pipeline `whisper-api`. **Gates every call through `WouldExceedCapAsync` BEFORE invoking Whisper.** Records ledger on success |
| `ILlmSummarizationService` + impl | OpenAI 2.10.0 chat-completion + Structured Outputs per PITFALLS.md (<0.1% parse failure). Single provider. Named HTTP client `llm-summarizer` |
| `Content/Prompts/SummaryPrompt.cs` | **C# static helper** (matches PromptBuilder convention). Build summary + clip-extraction directives |
| `IContentHarvestRunStore` + impl | Mirror `HarvestRunStore` pattern; own table |
| `IContentHarvestOrchestrator` + impl | Coordinates: for each enabled source → list videos → for each new video → captions → fallback Whisper (gated) → summarize → clips → tags |

**Controllers (`DeckFlow.Web/Controllers/Admin/`):**

| Path | Routes |
|---|---|
| `AdminContentSourcesController` | `GET/POST /Admin/ContentSources`, `GET/POST /Admin/ContentSources/Edit/{id}`, `POST /Admin/ContentSources/Delete/{id}` |
| `AdminContentHarvestController` | `GET /Admin/ContentHarvest`, `POST /Admin/ContentHarvest/Trigger` (202 + run id), `GET /Admin/ContentHarvest/{id}` |
| `AdminContentSpendController` | `GET /Admin/ContentSpend` (current month + last 6 months + cap warning >80%) |

**Views (`DeckFlow.Web/Views/Admin/`):**

| Path | Purpose |
|---|---|
| `AdminContentSources/{Index,Create,Edit}.cshtml` | Source CRUD UI |
| `AdminContentHarvest/{Index,Detail}.cshtml` | Run table + per-run drill-down |
| `AdminContentSpend/Index.cshtml` | Spend dashboard |

**Tests:**

- `ContentSourceStoreTests`, `ContentVideoStoreTests`, `WhisperSpendLedgerTests` — SQLite-backed; **F-PROD-CONTRACT-style fixture isolation per Phase 999.6 lesson** (separate temp DB per fact)
- `YouTubeIngestionServiceTests`, `PodcastIngestionServiceTests`, `WhisperTranscriptionServiceTests`, `LlmSummarizationServiceTests` — RichardSzalay.MockHttp fixtures + Polly pipeline test seam (`Func<RestRequest, CancellationToken, Task<RestResponse<T>>>` per `CardLookupService.cs:106-121`)
- `ContentHarvestOrchestratorTests` — end-to-end with deps faked; cap-abort behavior, idempotent re-runs

## Data Flow Changes

### Preserved (unchanged):
- Deck workflows: browser → controller → service → IHttpClientFactory + RestSharp + Polly → upstream
- `harvest_runs`, `feedback`, `feature_flags`, `category_knowledge` tables

### New — Admin-triggered content harvest:

```
Admin clicks "Trigger Harvest" on /Admin/ContentHarvest
  → AdminContentHarvestController.Trigger (POST, SameOriginRequestValidator)
  → IContentHarvestOrchestrator.RunAsync()
    → ContentHarvestRunStore.Insert(state='Queued')
    → For each enabled IContentSourceStore.GetAll()
      → if kind=youtube_channel:
          → IYouTubeIngestionService.ListChannelVideos(channelId)
          → IYouTubeIngestionService.GetCaptions(videoId) [captions first]
        if kind=podcast_rss:
          → IPodcastIngestionService.ListEpisodes(rssUrl)
      → For each new video (not in content_videos):
          → ContentVideoStore.Insert(...)
          → if captions: ContentTranscriptStore.Insert(source='youtube_captions')
          → else:
              → IWhisperSpendLedger.WouldExceedCapAsync(estimate)
              → if exceeds: video.transcript_status='skipped_over_cap'; ContentHarvestRunStore.Update(aborted_reason='whisper_cap_hit'); BREAK
              → else: IWhisperTranscriptionService.Transcribe(audioUrl)
                      → records whisper_spend_ledger on success
                      → ContentTranscriptStore.Insert(source='whisper')
          → ILlmSummarizationService.SummarizeAsync(transcript, SummaryPrompt.Build())
          → ContentSummaryStore.Insert(...)
          → ContentClipStore.InsertMany(...)
          → ContentTagStore.UpsertMany(...)
    → ContentHarvestRunStore.Update(state='Succeeded', ...)
```

### New — Whisper spend gate (critical correctness contract):

```
Every IWhisperTranscriptionService.Transcribe(audioUrl) call:
  1. Estimate cost = duration_seconds × Whisper_per_minute_rate
  2. await IWhisperSpendLedger.WouldExceedCapAsync(estimateUsd, ct)
     → SELECT COALESCE(SUM(cost_usd),0) FROM whisper_spend_ledger WHERE month_key = current
     → return (current_total + estimate) > Env.WhisperMonthlyCapUsd
  3. if true → throw WhisperCapExceededException (orchestrator → run aborted)
  4. else → invoke Whisper API → on success, INSERT ledger row
```

Test fact required: "cap-gate prevents Whisper call when projected to exceed cap, ledger row NOT written, no upstream HTTP attempted."

### Postgres schema additions

Per-store `EnsureSchemaAsync` (matches existing pattern). 8 new tables, all `content_*` prefixed except `whisper_spend_ledger`. Each store self-bootstraps schema lazy inside `SemaphoreSlim` gate. No FK enforcement across stores (matches existing pattern; explicit `REFERENCES` declared in DDL for documentation, not enforced via cascading deletes).

## Suggested Build Order

| # | Cluster / Phase | Why this order | Deps | Risk |
|---|---|---|---|---|
| 1 | **WDG-04 Modal (Cluster A)** | Closes v1.3 carry-over. Tiny: 1 TS + 1 view + small CSS. Zero coupling. Lands first as "ship gate working" proof | None | LOW |
| 2 | **Doc-Comment Backlog Part 1 — Controllers + Services (B subset)** | ~50 of 88 types. Mechanical. NoWarn stays until Part 2 | None | LOW |
| 3 | **Gemini Unblock — split-message (D Path 1)** | Closes v1.2 deferred Gemini flag. 5 PromptBuilder files + 3 views. Preserves Phase 999.2 D-08. Lands BEFORE admin mobile so Gemini-paste flows regression-tested across existing CSS, re-verified after sweep | None | MED — UAT-gated |
| 4 | **Admin Mobile Sweep (C)** | AFTER WDG-04 so modal CSS doesn't need re-architecting mid-factoring. Touches `_AdminLayout.cshtml`, splits `admin.css` → `admin-common.css` + `admin-mobile.css` + import shim | Cluster A | MED — full admin regression |
| 5 | **Content KB Stores + Schema (E foundation)** | First half. 8 new stores + 8 new tables. Zero UI; zero outbound HTTP. Validates schema before HTTP services depend on it | None within v1.4 | MED — F-PROD-CONTRACT test isolation (999.6 lesson) |
| 6 | **Content KB Outbound HTTP Services (E ingestion)** | YouTube + Podcast + Whisper + LLM. IHttpClientFactory + RestSharp + Polly pattern. WhisperSpendLedger cap-gate integrated. MockHttp tests | #5 | MED-HIGH — 4 new upstream surfaces |
| 7 | **Content KB Orchestrator + Harvest Runs (E coordination)** | `ContentHarvestOrchestrator` + `ContentHarvestRunStore`. Wires #5 + #6 end-to-end. Tests assert cap-abort, idempotent re-run, partial-success | #5, #6 | MED |
| 8 | **Content KB Admin UI (E UI)** | 3 admin controllers + 7 Razor views + sidebar additions. Inherits Cluster C's CSS. SameOriginRequestValidator on every POST | #4, #5-7 | LOW |
| 9 | **Doc-Comment Backlog Part 2 + strip `NoWarn` (B finish)** | Remaining ~38 types. LAST is csproj edit. Triggers warnings-as-future-gate. Lands last so v1.4 new types (D + E) are documented before gate flips | All prior | LOW |

**Sequencing rationale:**
- A before C — modal CSS lands in new `admin-common.css` factoring (#4); doing modal after split forces touching two files
- D before C — Gemini changes isolated to deck workflow views (not admin); mixing inside admin mobile sweep mixes concerns in PRs
- E stores (#5) before HTTP services (#6) before orchestrator (#7) before UI (#8) — each layer's tests need prior layer's seam
- B split into #2 + #9 — deferring entire backlog to end risks NoWarn shipping with cluster work; #2 picks easy half early; #9 finishes after all v1.4 surface exists

## Cross-Cutting Constraints (preserve these invariants)

1. **IHttpClientFactory + RestSharp + Polly named pipeline** for ALL outbound HTTP. NEVER migrate to `Microsoft.Extensions.Http.Resilience` standard handler (CLAUDE.md anti-pattern).
2. **AiPlatform value object UNTOUCHED for admin-side LLM summarization.** Registry serves user-facing multi-AI prompt dispatch; admin ingestion uses single provider. Path 2 (direct Gemini API) is ONLY scenario that adds AiPlatform variant — user-facing Gemini, not admin-side.
3. **`ScryfallThrottle` static gate UNTOUCHED.** New upstream services have own Polly pipelines + own throttle.
4. **HarvestRunStore PATTERN, not extension.** Fork to `ContentHarvestRunStore`; do NOT widen `harvest_runs.kind` CHECK.
5. **`IFeatureFlagStore` for binary/string toggles only.** Use `content_kb_enabled` to gate feature. DO NOT use for `whisper_monthly_cap_usd` — env var instead.
6. **SameOriginRequestValidator on every Admin/Content POST.** Feature is admin-only via BasicAuthMiddleware, CSRF guard still mandatory per PITFALLS.md.
7. **CSS layout discipline:** new `admin-common.css` mirrors `site-common.css` role. Do NOT pile mobile rules into `admin.css`.
8. **C# raw string literals preserved byte-for-byte** in `SummaryPrompt.cs` and DDL constants (CLAUDE.md).
9. **`{ get; init; }` preservation** on every new record type. System.Text.Json silently skips get-only properties — broke EdhTop16Client before.
10. **No new npm dependency** for focus-trap (Cluster A) — hand-rolled native `<dialog>` per FEATURES.md.
11. **Whisper cap-gate is correctness-critical.** `Transcribe` MUST call `WouldExceedCapAsync` BEFORE invoking Whisper API.
12. **Test isolation per F-PROD-CONTRACT 999.6 lesson:** every store test gets own SQLite file or `:memory:` with per-fact scope.
13. **Plain default-author commits, no Co-Authored-By trailer.** README updated when behavior changes.
14. **VSTest unreliable in WSL** — rely on `dotnet build` clean + push-and-watch CI on `v1.4` branch + targeted manual UAT.
15. **Memory budget:** Render Basic-256mb Postgres + 512MB web tier. Avoid in-memory caching of transcripts/summaries — read-on-demand.

## Confidence

| Area | Level | Reason |
|---|---|---|
| Reuse of existing patterns | HIGH | Patterns verified at HEAD 65f2fe4 (12/12 wired flows) |
| Service placement | HIGH | Existing `Services/Harvest/`, `Services/FeatureFlags/` convention |
| Postgres table design + `EnsureSchemaAsync` | HIGH | Direct mirror of `HarvestRunStore.cs:436-471` |
| Admin CSS factoring | HIGH | Direct precedent from `site-common.css` + `site-mobile.css` |
| Gemini split-message | MED | Architecture sound; UAT determines whether paste-cap accommodates N chunks |
| LLM provider (OpenAI per STACK.md) | HIGH | Single-vendor OpenAI 2.10.0 for Whisper + chat + Structured Outputs |
| YouTube transcript (YoutubeExplode per STACK.md) | HIGH | Avoids Data API 403 on third-party (verified Issue Tracker 241669016) |

## Open Questions

1. **Gemini paste-cap empirical limit** — needs UAT. If Path 1 fails, fall back to Path 2.
2. **Content KB feature flag default** — `content_kb_enabled=false` until first admin UAT pass.
3. **Tag inference vocabulary** — exact enum values per FEATURES.md (Wizards Feb 2025 bracket + ~15 community-standard archetypes).
4. **Render Dockerfile ffmpeg** — confirm via `docker run mcr.microsoft.com/dotnet/aspnet:10.0 which ffmpeg`. If missing AND podcasts > 25MB need chunking, Dockerfile change required.
