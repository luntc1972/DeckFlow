<!-- refreshed: 2026-05-29 -->
# Architecture

**Analysis Date:** 2026-05-29

## System Overview

```text
┌──────────────────────────────────────────────────────────────────────────┐
│                         DeckFlow.Web (ASP.NET 10 MVC)                    │
├──────────────────────────────────────────────────────────────┬───────────┤
│  Controllers Layer (MVC + JSON API)                          │ Admin     │
│  ├─ DeckController (Razor workflows)                         │ Shell     │
│  ├─ CommanderController (Commander categories)              │ (BasicAuth)
│  ├─ FeedbackController (Rate-limited submissions)            │           │
│  ├─ Api/{DeckSync,Suggestions,ArchidektCacheJobs}           │           │
│  └─ Admin/{Feedback,Harvest,Analytics,Flags,Landing}        │ Controllers
├──────────────────────────────────────────────────────────────┴───────────┤
│  Services Layer (Singleton/Scoped)                                       │
│  ├─ DeckAnalysisPacketService, DeckComparisonService                    │
│  ├─ CategorySuggestionService, CommanderCategoryService                 │
│  ├─ CardLookup, CardSearch, CommanderSearch, ScryfallSet,               │
│  │   CommanderSpellbook, CommanderBanList, EdhTop16Client               │
│  ├─ ScryfallTaggerService (w/ CookieContainer + SocketsHttpHandler)     │
│  ├─ Prompt Variant Registries (Analysis, Comparison, FollowUp,          │
│  │   SetUpgrade, MetaGap × ChatGPT/Claude/Gemini)                      │
│  ├─ Harvest: HarvestScheduleService, HarvestRunStore,                   │
│  │   HarvestScheduleStore, HarvestStatsAggregator                       │
│  ├─ Content: ContentSourceStore, ContentVideoStore,                     │
│  │   ContentSiteIndexStore (CLI-harvester local artifact model)         │
│  ├─ Analytics: RequestMetricsStore, RequestMetricsFlusher,              │
│  │   AnalyticsSaltAccessor (IP hash salt resolution)                   │
│  ├─ Storage Dialects: SqliteRelationalDialect,                          │
│  │   PostgresRelationalDialect (feedback, category knowledge)           │
│  └─ Http: ScryfallRestClientFactory, ScryfallTaggerHttpClient,          │
│      ResiliencePipelineProvider<string> (Polly v8 named pipelines)     │
├──────────────────────────────────────────────────────────────────────────┤
│  Infrastructure & Security                                              │
│  ├─ BasicAuthMiddleware (guards /Admin/*)                               │
│  ├─ AnalyticsMiddleware (D-12: after routing, before logging)           │
│  ├─ SameOriginRequestValidator (CSRF gate for API endpoints)            │
│  ├─ SecurityHeadersApplicationBuilderExtensions (CSP, X-Frame, etc.)    │
│  └─ ForwardedHeadersMiddleware (X-Forwarded-Proto/Host/For)             │
└──────────────────────────────────────────────────────────────────────────┘
         │                      │                      │
         ▼                      ▼                      ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                  DeckFlow.Core (Pure Domain Logic)                        │
├──────────────────────────────────────────────────────────────────────────┤
│  Models: DeckEntry, DeckDiff, LoadedDecks, MatchMode, SyncDirection     │
│  Parsing: MoxfieldParser, ArchidektParser (→ IParser interface)         │
│  Loading: DeckEntryLoader, IMoxfieldDeckImporter, IArchidektDeckImporter│
│  Diffing: DiffEngine (card-by-card reconciliation)                       │
│  Exporting: MoxfieldExporter, ArchidektExporter                          │
│  Normalization: CardNormalizer (Scryfall → canonical form)              │
│  Knowledge: CategoryKnowledgeRepository, DeckCategoryCacheWriter,       │
│    ContentArtifactWriter, ContentSpendModels, ContentTagVocabulary     │
│  Content: ContentVideoStore, ContentSourceStore,                        │
│    ContentHarvestRunStore, ContentSiteIndexStore, IRelationalDialect   │
│  Integration: ArchidektApiDeckImporter, MoxfieldApiDeckImporter        │
│  Storage: RelationalDatabaseConnection (SQLite | Postgres)              │
│  Reporting: DeckReporter (prompt artifact generation)                    │
└──────────────────────────────────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                    DeckFlow.CLI (System.CommandLine)                      │
├──────────────────────────────────────────────────────────────────────────┤
│  Verbs: compare, probe-moxfield, export-moxfield, archidekt-*,          │
│    card-lookup, scryfall-probe, content-source-add, harvest, distill    │
│  Depends: DeckFlow.Core (parsing, loading, exporting, knowledge)        │
│  Harvest/Distill: YouTube transcript ingestion + AI artifact emission   │
│  Database: MTG_DATA_DIR/artifacts/{feedback,category-knowledge,         │
│    content-kb}.db (or Postgres via DECKFLOW_DATABASE_CONNECTION_STRING) │
└──────────────────────────────────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────────────────────┐
│              Data Stores (SQLite / Postgres)                              │
│  └─ feedback.db: feedback, admin_bruteforce_attempts, request_metrics   │
│  └─ category-knowledge.db: deck_categories, category_cards, sources     │
│  └─ content-kb.db: content_sources, content_videos, content_runs,       │
│                    content_llm_spend, content_whisper_spend              │
│  └─ Harvest: harvest_runs, harvest_schedules (analytics state)          │
└──────────────────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| Program.Main | DI wiring, middleware pipeline, Serilog, startup validation | `DeckFlow.Web/Program.cs` |
| DeckController | Deck workflows (sync, convert, lookup, categories, analysis) | `DeckFlow.Web/Controllers/DeckController.cs` |
| CommanderController | Commander category page | `DeckFlow.Web/Controllers/CommanderController.cs` |
| FeedbackController | Feedback submission (rate-limited 5/hr) | `DeckFlow.Web/Controllers/FeedbackController.cs` |
| DeckSyncApiController | Deck diff JSON endpoint | `DeckFlow.Web/Controllers/Api/DeckSyncApiController.cs` |
| SuggestionsApiController | Category suggestion JSON endpoints | `DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs` |
| ArchidektCacheJobsController | Internal job control endpoint | `DeckFlow.Web/Controllers/Api/ArchidektCacheJobsController.cs` |
| Admin* Controllers | Admin-only (BasicAuth gated) dashboards | `DeckFlow.Web/Controllers/Admin/` |
| DeckAnalysisPacketService | Orchestrates analysis prompt + artifacts | `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` |
| DeckComparisonService | Two-deck comparison prompt generation | `DeckFlow.Web/Services/DeckComparisonService.cs` |
| MetaGapService | Meta gap analysis (cEDH meta comparison) | `DeckFlow.Web/Services/MetaGapService.cs` |
| CategorySuggestionService | Mode-routed category suggestions (cached/tagger/reference) | `DeckFlow.Web/Services/CategorySuggestionService.cs` |
| CardLookupService | Scryfall REST adapter (RestSharp + Polly) | `DeckFlow.Web/Services/CardLookupService.cs` |
| CardSearchService | Scryfall fuzzy search | `DeckFlow.Web/Services/CardSearchService.cs` |
| CommanderSearchService | Commander-legal search via Scryfall | `DeckFlow.Web/Services/CommanderSearchService.cs` |
| ScryfallSetService | MTG set data + mechanics lookup | `DeckFlow.Web/Services/ScryfallSetService.cs` |
| CommanderSpellbookService | Combo data from commanderspellbook.com | `DeckFlow.Web/Services/CommanderSpellbookService.cs` |
| CommanderBanListService | EDH banlist from mtgcommander.net | `DeckFlow.Web/Services/CommanderBanListService.cs` |
| ScryfallTaggerService | Tagger.scryfall.com integration (CSRF + session) | `DeckFlow.Web/Services/ScryfallTaggerService.cs` |
| EdhTop16Client | EDH metagame tier lists | `DeckFlow.Web/Services/EdhTop16Client.cs` |
| ScryfallTaggerHttpClient | Typed HTTP client w/ cookie-disabled handler | `DeckFlow.Web/Services/ScryfallTaggerHttpClient.cs` |
| ResiliencePipelineFactory | Polly v8 pipelines (banlist, spellbook, tagger, scryfall) | `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` |
| IAnalysisPromptVariant (ChatGpt/Claude/Gemini) | Platform-specific prompt formatting (strategy) | `DeckFlow.Web/Services/PromptBuilders/Analysis/*.cs` |
| AnalysisPromptVariantRegistry | Routes AiPlatform → variant implementation | `DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs` |
| AiPlatformOptions | Feature flags (DECKFLOW_GEMINI_ENABLED) | `DeckFlow.Web/Configuration/AiPlatformOptions.cs` |
| BasicAuthMiddleware | HTTP Basic auth gate for /Admin/* | `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` |
| SameOriginRequestValidator | CSRF check (Origin/Referer match) | `DeckFlow.Web/Security/SameOriginRequestValidator.cs` |
| AnalyticsMiddleware | Request metrics collection (before logging) | `DeckFlow.Web/Infrastructure/AnalyticsMiddleware.cs` |
| HarvestScheduleService | Manages Archidekt cache job scheduling | `DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs` |
| HarvestRunStore | Persists job execution records | `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` |
| RequestMetricsStore | Analytics event persistence (SQLite/Postgres) | `DeckFlow.Web/Services/Analytics/RequestMetricsStore.cs` |
| ArchidektCacheJobService | Hosted background service for category knowledge | `DeckFlow.Web/Services/ArchidektCacheJobService.cs` |
| CategoryKnowledgeStore | Deck→card category persistence | `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` |
| FeedbackStore | User feedback persistence (rate-limit state) | `DeckFlow.Web/Services/FeedbackStore.cs` |
| ContentSourceStore | Content KB source registry (YouTube, podcast) | `DeckFlow.Core/Content/ContentSourceStore.cs` |
| ContentVideoStore | Harvested video/episode metadata + transcripts | `DeckFlow.Core/Content/ContentVideoStore.cs` |
| ContentSiteIndexStore | Slim index for browse/filter UI | `DeckFlow.Core/Content/ContentSiteIndexStore.cs` |
| ContentArtifactWriter | Emits front-matter markdown artifacts | `DeckFlow.Core/Knowledge/ContentArtifactWriter.cs` |
| LlmSpendLedger | Tracks distillation spend (OpenAI tokens) | `DeckFlow.Core/Content/LlmSpendLedger.cs` |
| WhisperSpendLedger | Tracks fallback transcription spend | `DeckFlow.Core/Content/WhisperSpendLedger.cs` |
| RelationalDatabaseConnection | Pluggable SQL dialect wrapper | `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` |
| IRelationalDialect | SQLite vs Postgres column types | `DeckFlow.Core/Storage/IRelationalDialect.cs` |
| DeckFlow.Core | Pure domain: parsing, diffing, normalization, reporting | `DeckFlow.Core/*` |
| DeckFlow.CLI | System.CommandLine host for harvest/distill/compare | `DeckFlow.CLI/Program.cs` |

## Pattern Overview

**Overall:** Service-oriented ASP.NET Core MVC with a pluggable storage dialect and strategy-pattern prompt builders.

**Key Characteristics:**

- **DI-driven composition:** All services registered in `Program.cs:50-348`; no `new` outside factories.
- **HTTP resilience:** RestSharp + Polly v8 named pipelines (banlist, spellbook, tagger, scryfall) — never direct `HttpClient()`.
- **Prompt variants:** IXxxPromptVariant interface per prompt type × 3 platforms (ChatGPT/Claude/Gemini) + registry pattern.
- **Pluggable storage:** IRelationalDialect abstracts SQLite vs Postgres; runtime choice via env var.
- **Content KB hybrid model:** CLI-driven harvest (transcripts) → distillation (AI) → artifact files + slim site-index in DB.
- **Session-based theming:** Guild theme CSS + site-common.css layout + admin theme shell.
- **CSRF protection:** SameOriginRequestValidator on all API POST endpoints; stateless via Origin/Referer header.
- **Rate limiting:** Fixed-window 5 req/hr per IP on /Feedback (feedback:xx partition key).
- **Analytics:** RequestMetrics table (one row per req) + IpHasher salt isolation + request logging via Serilog.

## Layers

**CLI Layer:**

- Purpose: Headless command runner for power users and batch jobs (deck compare, content harvest/distill, archidekt cache).
- Location: `DeckFlow.CLI/`
- Contains: System.CommandLine command builders, CommandRunners.cs verb handlers, logging setup.
- Depends on: DeckFlow.Core, System.CommandLine 2.0.0-beta4, Serilog.
- Used by: Local scripts, scheduled jobs, operator CLI.
- Entry point: `DeckFlow.CLI/Program.cs` — builds command tree, dispatches to handlers.

**Core Layer (Domain Logic):**

- Purpose: Deck domain logic with zero I/O frameworks (except Polly/RestSharp for Integration/* importers, Sqlite/Npgsql for Storage dialects).
- Location: `DeckFlow.Core/`
- Contains: Models, Parsing, Diffing, Exporting, Filtering, Normalization, Knowledge, Content (KB stores), Integration (importers), Storage (dialect), Reporting.
- Depends on: Microsoft.Data.Sqlite, Npgsql, Polly, RestSharp, Microsoft.Extensions.Logging.Abstractions.
- Used by: DeckFlow.Web controllers, DeckFlow.CLI, both test projects.
- Key subsystems:
  - **Parsing:** `IMoxfieldDeckParser`, `IArchidektDeckParser` → `DeckEntry` lists.
  - **Loading:** `IMoxfieldDeckImporter`, `IArchidektDeckImporter` (fetch from API).
  - **Diffing:** `DiffEngine` (card-by-card reconciliation, conflict resolution).
  - **Knowledge:** `CategoryKnowledgeRepository`, `DeckCategoryCacheWriter` (Archidekt cache ingestion).
  - **Content KB:** `ContentSourceStore`, `ContentVideoStore`, `ContentHarvestRunStore`, `ContentSiteIndexStore` (local artifact model).
  - **Storage:** `RelationalDatabaseConnection` + `IRelationalDialect` (feedback, category-knowledge, content-kb DBs).
  - **Spending:** `LlmSpendLedger`, `WhisperSpendLedger` (AI cost tracking).

**Web Services Layer:**

- Purpose: Application logic, external adapters, HTTP resilience, persistence.
- Location: `DeckFlow.Web/Services/` (30+ services in themed subfolders).
- Contains: Lookup/search services, prompt builders, tagger session cache, harvest/content/analytics stores, HTTP infrastructure.
- Depends on: DeckFlow.Core, IHttpClientFactory, ResiliencePipelineProvider, IMemoryCache, RestSharp, Polly, Markdig, Serilog, Microsoft.AspNetCore.*.
- Used by: Controllers, hosted services.
- Key subfolders:
  - **PromptBuilders/:** Analysis, Comparison, FollowUp, MetaGap, SetUpgrade — each with ChatGptVariant/ClaudeVariant/GeminiVariant + Registry.
  - **Http/:** ResiliencePipelineFactory, ScryfallRestClientFactory, ScryfallTaggerHttpClient (SocketsHttpHandler w/ CookieContainer).
  - **Harvest/:** HarvestScheduleService, HarvestRunStore, HarvestScheduleStore (Archidekt cache scheduling).
  - **Content/:** ContentSourceStore, ContentVideoStore, etc. (v1.4 KB local model).
  - **Analytics/:** RequestMetricsStore, AnalyticsSaltAccessor (IP hash + metrics persistence).

**Web Controllers Layer:**

- Purpose: HTTP entry points (MVC + JSON API + admin pages).
- Location: `DeckFlow.Web/Controllers/`, `Controllers/Api/`, `Controllers/Admin/`.
- Contains: Thin orchestrators that bind models, invoke services, return IActionResult.
- Depends on: Web service interfaces, DeckFlow.Core models, SecurityValidator.
- Used by: Browser, DeckFlow Bridge extension, external API consumers.

**Infrastructure & Security:**

- Purpose: Cross-cutting middleware, CSRF, auth, security headers.
- Location: `DeckFlow.Web/Infrastructure/`, `DeckFlow.Web/Security/`.
- Contains: BasicAuthMiddleware, AnalyticsMiddleware, SameOriginRequestValidator, SecurityHeadersApplicationBuilderExtensions, DevelopmentBrowserLauncher.
- Used by: Program.Main middleware pipeline, every controller.

**Views & Static Assets:**

- Purpose: Server-side HTML rendering, themed CSS, compiled TypeScript, packaged browser extension.
- Location: `DeckFlow.Web/Views/`, `DeckFlow.Web/wwwroot/`.
- Contains: Razor views (one folder per controller), CSS (site-common.css layout + guild themes + admin shell), TypeScript source + compiled JS, packaged browser extension.
- Used by: Controller.View(…) calls; browser clients.

## Data Flow

### Primary Request Path — Deck Sync (Browser)

1. Browser POST `/api/deck/diff` with two deck URLs → `DeckSyncApiController.Diff()` (`DeckFlow.Web/Controllers/Api/DeckSyncApiController.cs:30-60`).
2. Controller validates same-origin via `SameOriginRequestValidator.IsValid(request)` (`DeckFlow.Web/Security/SameOriginRequestValidator.cs:17-32`).
3. Controller invokes `IDeckSyncService.SyncAsync(request)` (`DeckFlow.Web/Services/DeckSyncService.cs`).
4. Service resolves deck loaders (`IDeckEntryLoader`) → fetches via `IMoxfieldDeckImporter` / `IArchidektDeckImporter` (RestSharp + Polly).
5. Service parses deck text via `MoxfieldParser` / `ArchidektParser` (DeckFlow.Core).
6. Service runs `DiffEngine.DiffDecks(loaded, mode, direction)` to produce `DeckDiff` (Core).
7. Service serializes diff to JSON, returns 200 with payload.
8. Browser receives JSON, client-side TypeScript (`deck-sync.ts`) renders the UI.

### Prompt Building Flow — Analysis Packet

1. Browser POST `/deck/analysis` → `DeckController.DeckAnalysis()` (`DeckFlow.Web/Controllers/DeckController.cs:180-220`).
2. Controller invokes `IDeckAnalysisPacketService.BuildAsync(request)` (scoped, reconstructed per request).
3. Service fetches and normalizes deck data, commanderspellbook combos, banlist, set mechanics.
4. Service resolves prompt variant via `AnalysisPromptVariantRegistry.GetVariant(request.AiPlatform)` → concrete ChatGptAnalysisPromptVariant / ClaudeAnalysisPromptVariant / GeminiAnalysisPromptVariant.
5. Variant assembles prompt text → platform-specific formatting (markdown headers for ChatGPT, XML tags for Claude, persona scaffold for Gemini).
6. Service writes artifact to session cache (`PacketSessionCache`) or persistent store (`ChatGptDeckPacketService` writes to `/data/artifacts/`).
7. Controller returns view with packet text, user copies/pastes into ChatGPT.

### Tagger Session Flow

1. Service needs card tags → invokes `IScryfallTaggerService.LookupAsync(cardName)` (`DeckFlow.Web/Services/ScryfallTaggerService.cs`).
2. Service checks `ITaggerSessionCache` for cached CSRF token (270s TTL, per HIGH-2 invariant).
3. On miss, service uses `ScryfallTaggerHttpClient` (SocketsHttpHandler with CookieContainer) to GET `/card/{set}/{num}`, extracts CSRF token from response HTML.
4. Service caches CSRF token + session (CookieContainer reused across calls via singleton registration).
5. Service POST `/graphql` with cached CSRF token + mutation → Polly "tagger-post" pipeline (no retry — GraphQL POST not idempotent).
6. Service parses response, returns tags.

### Analytics Collection Path

1. Request enters middleware pipeline.
2. `AnalyticsMiddleware` (registered after `UseRouting`) logs endpoint info to `RequestMetricEvent` (in-memory buffer).
3. `RequestMetricsFlusher` flushes buffer every N seconds to `RequestMetricsStore.InsertAsync()`.
4. Store writes to `request_metrics` table (feedback.db or Postgres).
5. Admin views aggregate metrics via `RequestMetricsStore.QueryAsync()`.

### Content Harvest Flow (CLI)

1. User runs `dotnet DeckFlow.CLI.dll harvest --limit 5 --db artifacts/content-kb.db`.
2. CLI resolves `IContentSourceStore`, enumerates enabled sources (YouTube channels, podcast RSS feeds).
3. For each source, fetches recent videos (YouTube Data API) or episodes (RSS parse).
4. For each video/episode, fetches captions (YouTube) or transcription (Whisper fallback, opt-in via --enable-whisper).
5. Stores metadata + transcript in `content_videos` table.
6. Stores run record in `content_harvest_runs` table.

### Content Distill Flow (CLI)

1. User runs `dotnet DeckFlow.CLI.dll distill --limit 5 --db artifacts/content-kb.db [--dry-run]`.
2. CLI resolves `IContentVideoStore`, queries videos with `transcript_status = "transcribed"` and `distillation_status = "pending"`.
3. For each video (up to limit), sends transcript to OpenAI API (via OpenAI .NET SDK).
4. OpenAI returns summary, tags, clips (custom instructions in prompt).
5. `ContentArtifactWriter` writes front-matter markdown artifact to `content-kb/{source-slug}/{video_id}.md`.
6. `ContentSiteIndexStore` inserts site-index row for browse/filter UI.
7. `LlmSpendLedger` records token spend; `WhisperSpendLedger` records any Whisper fallback spend.
8. `distillation_status` → "complete" on success.

**State Management:**

- **Per-request scoped services:** IDeckSyncService, IDeckAnalysisPacketService, ICategorySuggestionService (Program.cs:290-330).
- **Singleton caches:** CardLookupCache, PacketSessionCache, TaggerSessionCache, IMemoryCache (search results, set data).
- **Persistent state:** SQLite/Postgres — feedback.db (submissions, brute-force), category-knowledge.db (deck categories), content-kb.db (sources, videos, runs).
- **Hosted background service:** ArchidektCacheJobService (singleton facade, runs on host scheduler).

## Key Abstractions

**Deck Primitives:**

- Purpose: Immutable deck container types (DeckEntry, DeckDiff, LoadedDecks, MatchMode, SyncDirection, PrintingChoice, PrintingConflict).
- Pattern: C# `sealed record` types where immutable multi-value returns needed; nullable reference types enabled.
- Examples: `DeckFlow.Core/Models/DeckEntry.cs`, `DeckFlow.Core/Models/DeckDiff.cs`.

**Parsers & Importers:**

- Purpose: Convert raw deck text / API responses into DeckEntry lists.
- Pattern: `IParser` interface with MoxfieldParser / ArchidektParser; throws `DeckParseException` on bad input. `IMoxfieldDeckImporter` / `IArchidektDeckImporter` for API fetching.
- Files: `DeckFlow.Core/Parsing/*`, `DeckFlow.Core/Integration/*`.

**Prompt Variants:**

- Purpose: Platform-specific prompt formatting (ChatGPT vs Claude vs Gemini).
- Pattern: `IAnalysisPromptVariant` (+ Comparison, FollowUp, SetUpgrade, MetaGap variants) interface with ChatGpt/Claude/Gemini implementations. Registry pattern (`AnalysisPromptVariantRegistry`) routes AiPlatform → variant.
- Files: `DeckFlow.Web/Services/PromptBuilders/*/IXxxPromptVariant.cs`, `DeckFlow.Web/Services/PromptBuilders/*/ChatGptXxxPromptVariant.cs` (and Claude, Gemini).

**AI Platform Value Object:**

- Purpose: Strongly-typed AI platform discriminator (ChatGpt, Claude, Gemini).
- Pattern: Sealed record with static singletons + All list + Normalize(string).
- File: `DeckFlow.Web/Models/AiPlatform.cs`.

**Storage Dialect:**

- Purpose: Pluggable SQL backend (SQLite vs Postgres).
- Pattern: `IRelationalDialect` interface with `SqliteRelationalDialect` / `PostgresRelationalDialect`; `RelationalDatabaseConnection` is consumer-facing handle.
- Files: `DeckFlow.Core/Storage/IRelationalDialect.cs`, `DeckFlow.Core/Storage/SqliteRelationalDialect.cs`, `DeckFlow.Core/Storage/PostgresRelationalDialect.cs`.

**Polly Resilience Pipelines:**

- Purpose: Single composition-time registration of all named HTTP pipelines.
- Pattern: `services.AddDeckFlowResiliencePipelines()` extension; consumers resolve via `ResiliencePipelineProvider<string>.GetPipeline<RestResponse>(name)`.
- File: `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs`.

**Content Artifact Spec:**

- Purpose: Canonical front-matter markdown format for KB artifacts.
- Pattern: Static format string + metadata record + tag serialization helpers.
- File: `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs`.

**View Models & DTOs:**

- Purpose: Strongly-typed payloads bound to Razor views and JSON APIs.
- Convention: View-specific models named `*ViewModel`, request DTOs in Controllers, response DTOs in `Models/Api/*`.
- Examples: `DeckAnalysisRequest`, `DeckSyncRequest`, `DeckSyncResponse`.

**Feature Flags:**

- Purpose: Runtime toggles for optional features (e.g., Gemini UI visibility).
- Pattern: Options classes bound from env vars in Program.cs (AiPlatformOptions).
- File: `DeckFlow.Web/Configuration/AiPlatformOptions.cs`.

## Entry Points

**Web Host:**

- Location: `DeckFlow.Web/Program.cs`
- Triggers: `dotnet run --project DeckFlow.Web` (local dev) or container startup (Dockerfile, render.yaml).
- Responsibilities: Configure Serilog (console + daily file logs), register 30+ services in DI, build Polly pipelines, configure middleware (forwarded headers → security headers → HTTPS redirect → static files → routing → analytics → request logging → Swagger [Dev] → rate limit → BasicAuth on /Admin → MapControllers), validate DB connections at startup, launch browser (dev only).
- Key bootstrap: Lines 40-463 (Main method), lines 47-60 (Serilog setup), lines 63-348 (DI registration), lines 354-389 (middleware), lines 423-450 (startup validation).

**CLI Host:**

- Location: `DeckFlow.CLI/Program.cs`
- Triggers: `dotnet run --project DeckFlow.CLI -- <command> [options]`
- Responsibilities: Configure Serilog file sink only, build System.CommandLine root with compare, probe-moxfield, export-moxfield, archidekt-*, card-lookup, scryfall-probe, content-source-add, harvest, distill commands; dispatch to CommandRunners.cs handlers.
- Key commands: compare, harvest, distill (content KB focused).

**HTTP Endpoints:**

- `GET /` → `DeckController.Home`
- `GET /sync` → `DeckController.Index` (deck sync UI)
- `POST /api/deck/diff` → `DeckSyncApiController.Diff` (JSON endpoint)
- `POST /deck/analysis` → `DeckController.DeckAnalysis` (analysis packet UI)
- `POST /api/suggestions/categories` → `SuggestionsApiController.Categories` (JSON endpoint)
- `/Admin/*` → guarded by BasicAuthMiddleware
- `/swagger` → Development only (Swashbuckle).

## Architectural Constraints

**Threading & Concurrency:**

- Standard ASP.NET Core async request pipeline; no explicit multithreading.
- Hosted background service `ArchidektCacheJobService` runs on the host scheduler (single instance, queued tasks).
- `ScryfallThrottle` (static SemaphoreSlim in `DeckFlow.Web/Services/ScryfallThrottle.cs`) enforces global Scryfall 5 req/s rate limit across all concurrent requests — DO NOT bypass for Scryfall callers.
- `IMemoryCache` is thread-safe (built-in).

**Global State & Singletons:**

- **Static throttle:** `ScryfallThrottle` gates all Scryfall calls (HIGH-1 invariant).
- **CookieContainer:** Singleton scoped to `ScryfallTaggerHttpClient` so session cookies persist across requests; SocketsHttpHandler automatically replays them.
- **Polly registries:** Singleton `ResiliencePipelineProvider<string>` + `ResiliencePipelineRegistry<string>` — pipelines built once at composition time, never rebuilt per call.
- **Tagger session cache:** Singleton `TaggerSessionCache` with 270s TTL (must stay strictly below SocketsHttpHandler 5 min HandlerLifetime — 30s margin enforced by HIGH-2 comment in Program.cs:111).
- **Static shim:** `ScryfallRestClientFactory` static instance retained for Phase 1 back-compat (Program.cs:108 D-01 note).

**Circular Imports & Dependencies:**

- No known circular import chains; layers flow: Controllers → Services → Core (unidirectional).
- Tests use `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]` (AssemblyInfo.cs:3) to access test seams without leaking to consumers.

**Forwarded Headers Invariant:**

- `app.UseForwardedHeaders()` MUST run BEFORE `app.UseHttpsRedirection()`, security headers, and `SameOriginRequestValidator` (Program.cs:354-365), otherwise scheme mismatch breaks CSRF check and HTTPS redirect.
- Render's reverse proxy sets `X-Forwarded-Proto: https`; without this ordering, request.Scheme stays "http" and CSRF validation fails (CAT-FIX-01 in SameOriginRequestValidator.cs:70).

**Build Coupling:**

- `DeckFlow.Web.csproj` runs TypeScript compiler (`tsc -p tsconfig.json`) and zips browser-extensions on every build (BeforeTargets="Build").
- TS sources in `wwwroot/ts/`, output in `wwwroot/js/`, both git-tracked.
- Guild themes: CSS must split layout (site-common.css) from token overrides (site.css, site-guild-*.css); adding new theme requires CSS file + token entries.

**Database Dialect at Startup:**

- Runtime choice via `DECKFLOW_DATABASE_PROVIDER` env var (default: SQLite).
- Connection string via `DECKFLOW_DATABASE_CONNECTION_STRING` (optional; defaults to MTG_DATA_DIR for SQLite).
- Dialect chosen by `DeckFlowDatabaseConnectionFactory` at composition time (Program.cs instantiation).

**Content KB Storage Namespace:**

- All content-related tables prefixed `content_*` (content_sources, content_videos, content_harvest_runs, content_llm_spend, content_whisper_spend, content_site_index) in content-kb.db.
- Separate ledger tables for LLM + Whisper spend isolation (per Phase 21 design).

**Analytics IP Salt:**

- IP hash salt resolved at startup via `IpHasher.ResolveSaltAsync()` and cached in `AnalyticsSaltAccessor` (Program.cs:437-450).
- If resolution fails, `ip_hash` stays null until next restart (non-blocking failure).
- Partition key for admin brute-force + feedback rate limit derived via `DeriveCloudflareClientIp()` (CF-Connecting-IP header) — fail-closed if missing.

## Anti-Patterns

### Direct `new HttpClient()` in Services

**What happens:** Code instantiates HttpClient directly (e.g., `new HttpClient().GetAsync(…)`).

**Why it's wrong:** Bypasses IHttpClientFactory pooling, violates HTTP_RESILIENCE convention (D-01), loses named pipeline routing, creates port exhaustion risk at scale, breaks Polly integration.

**Do this instead:** Inject `IHttpClientFactory`, call `factory.CreateClient("named-client")` (program.cs:85-102 defines named clients). Wrap with Polly pipeline resolution (`ResiliencePipelineProvider<string>.GetPipeline<RestResponse>(name)`). See `CardLookupService.cs:91-121` test seam pattern.

### Building Polly Pipelines Per Call

**What happens:** Code calls `ResiliencePipelineBuilder<RestResponse>().AddRetry(…).AddTimeout(…).Build()` inside a request handler.

**Why it's wrong:** Rebuilds the entire pipeline on every request, incurring reflection + allocation overhead, violates D-04 (composition-time registration).

**Do this instead:** Register pipelines once in Program.cs via `AddDeckFlowResiliencePipelines()` (Program.cs:165). Resolve at call time via `ResiliencePipelineProvider<string>.GetPipeline<RestResponse>(name)`. See ResiliencePipelineFactory.cs:24-31.

### Using `Microsoft.Extensions.Http.Resilience` Standard Handler

**What happens:** Code replaces Polly with the new standard `AddResilienceHandler` in Program.cs.

**Why it's wrong:** Project is locked to Polly v8 per CLAUDE.md constraint; migration would require retesting all upstream resilience behaviors (5 pipelines × 3 strategies = 15 scenarios), risk breaking live service.

**Do this instead:** Continue using current Polly v8 patterns (ResiliencePipelineFactory, named pipelines, ScryfallThrottle overlay). Migration to standard handler is future work (out of scope for v1.4).

### Calling Scryfall Without `ScryfallThrottle`

**What happens:** Service calls Scryfall API directly without going through `ScryfallThrottle.ExecuteAsync(…)`.

**Why it's wrong:** Violates global rate-limit gate (5 req/s across all services), risks 429 throttle responses from Scryfall, degraded UX for concurrent users.

**Do this instead:** Every Scryfall service wrapper (`CardLookupService`, `CardSearchService`, etc.) calls `ScryfallThrottle.ExecuteAsync(…)` before invoking the Polly "scryfall" pipeline. See CardLookupService.cs:150-165.

### Skipping `SameOriginRequestValidator` on API Endpoints

**What happens:** API endpoint (POST /api/…) is implemented without calling `SameOriginRequestValidator.IsValid(request)`.

**Why it's wrong:** Opens CSRF vulnerability; attacker can craft cross-origin form POST targeting the endpoint.

**Do this instead:** Every API POST in SuggestionsApiController, DeckSyncApiController, etc. validates same-origin in the handler (e.g., DeckSyncApiController.cs:50-56: `if (!SameOriginRequestValidator.IsValid(request)) return Forbid(…)`).

### Putting Layout CSS into `site.css`

**What happens:** Shared layout CSS (grid, flexbox, spacing patterns) is added to the guild-specific `site.css`.

**Why it's wrong:** Breaks other guild themes; layout CSS must be shared in `site-common.css` per CLAUDE.md constraint.

**Do this instead:** Layout CSS goes in `DeckFlow.Web/wwwroot/css/site-common.css`. Token overrides (colors, fonts, borders) go in `site-guild-{name}.css` or `site.css` (default theme). See DeckFlow.Web/wwwroot/css/ structure.

## Error Handling

**Strategy:** Layered error handling with domain exceptions at the boundary, HTTP translation in controllers, and graceful degradation in services.

**Patterns:**

- **Domain exceptions** (Core layer): `DeckParseException` (bad deck text), thrown by parsers. Controllers catch and return 400 with structured error message.
- **HTTP error translation** (Services layer): Non-2xx upstream responses (e.g., 404 from Scryfall) throw `HttpRequestException` with upstream status code preserved. Controllers catch, invoke `UpstreamErrorMessageBuilder.BuildScryfallMessage(exception)`, return 503 with user-facing copy.
- **Graceful degradation:** Services like `CommanderSpellbookService.FindCombosAsync()` return `null` on API failure rather than throwing; prompt builders continue without combo data (nullable pattern — see CommanderSpellbookServiceTests.FindCombosAsync_ApiFailure_ReturnsNull).
- **Cancellation timeouts:** Controllers link user cancellation token (HttpContext?.RequestAborted) to operation timeout via `CancellationTokenSource.CreateLinkedTokenSource(…).CancelAfter(LookupTimeout)` (DeckController.cs:55-57).
- **Polly integration:** Polly handles transient HTTP failures (retry, circuit breaker, timeout); persistent failures bubble up to services and are converted to user messages.
- **Non-dev exception handler:** `app.UseExceptionHandler("/Deck/Error")` renders friendly error page in Production (Program.cs:358-360).

## Cross-Cutting Concerns

**Logging:**

- Injected via `ILogger<T>` constructor parameter (nullable/optional in services, defaults to `NullLogger<T>.Instance` so tests don't wire one).
- Structured templates with named placeholders, never string interpolation (e.g., `logger.LogInformation("Lookup for {CardName} returned {Count} results.", name, results.Count)`).
- File sink rolls daily, retains 14 days (Program.cs:59, CLI:15).
- Console sink always on (even in Production) for platform capture (Render).
- Request logging via `app.UseSerilogRequestLogging()` (Program.cs:369).

**Validation:**

- Argument validation at constructor entry: `ArgumentNullException.ThrowIfNull(…)` (e.g., CommanderSpellbookService.cs:77-78).
- Model validation on Razor binding (built-in ModelState).
- API request DTO validation via `[Required]`, `[MinLength]` attributes (xUnit tests confirm).
- CSRF validation via `SameOriginRequestValidator.IsValid(request)` (every API POST).

**Authentication & Authorization:**

- No user identity/claims system (unauthenticated public app).
- `/Admin/*` endpoints guarded by `BasicAuthMiddleware` (HTTP Basic Auth via `FEEDBACK_ADMIN_USER`/`FEEDBACK_ADMIN_PASSWORD` env vars).
- Brute-force throttle on admin login attempts (5 fails → 1 min lockout per IP).

**Rate Limiting:**

- Feedback submission: 5 req/hr per IP (fixed-window, partitioned by `feedback:{ip}` derived from CF-Connecting-IP header).
- Registered in Program.cs:200-213 via `app.UseRateLimiter()`.

**Analytics & Observability:**

- RequestMetrics logged per request (endpoint, status, duration, ip_hash, user_agent, referrer).
- Stored in request_metrics table for admin dashboards (AdminAnalyticsController).
- IP hash salt resolved at startup (AnalyticsSaltAccessor).
- No external APM (all metrics local to SQLite/Postgres).

---

*Architecture analysis: 2026-05-29*
