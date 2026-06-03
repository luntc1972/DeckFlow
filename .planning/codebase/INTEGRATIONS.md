# External Integrations

**Analysis Date:** 2026-05-29

## APIs & External Services

**MTG Community APIs:**
- **Scryfall REST API** - Card database + search
  - SDK/Client: RestSharp 114.0.0 via `IScryfallRestClientFactory` and `RestClient` in `DeckFlow.Web/Services/ScryfallRestClientFactory.cs`
  - Resilience: Named pipeline `scryfall` (3x exponential+jitter retry, 20s timeout, 50% circuit breaker)
  - Rate limiting: Static `ScryfallThrottle.ExecuteAsync` enforces ~5 req/s global limit via `SemaphoreSlim` across entire app (`DeckFlow.Web/Services/ScryfallThrottle.cs`)
  - Services: `ScryfallCardLookupService`, `ScryfallCardSearchService`, `ScryfallCommanderSearchService`, `ScryfallSetService`, `ScryfallTaggerLookupService`

- **Scryfall Tagger** - Card mechanic/tag suggestions via GraphQL
  - SDK/Client: `ScryfallTaggerHttpClient` typed client in `DeckFlow.Web/Services/ScryfallTaggerHttpClient.cs` with cookie-disabled `SocketsHttpHandler` for automatic session management
  - Auth: CSRF token cookie handling; 270s session cache TTL in `TaggerSessionCache.cs` (must stay 30s below 5-min handler lifetime per HIGH-2 invariant in Program.cs:111)
  - Resilience: Separate pipelines for GET (`tagger`) and POST (`tagger-post`); POST has no retry (GraphQL idempotency hazard)
  - Headers: Browser-mimicking (Accept-Language, Sec-Fetch-*, gzip/deflate/brotli) to bypass Cloudflare Browser Integrity Check

- **Commander Spellbook** - Combo lookup
  - Endpoint: https://backend.commanderspellbook.com/
  - SDK/Client: RestSharp via `ICommanderSpellbookService` in `DeckFlow.Web/Services/CommanderSpellbookService.cs`
  - Resilience: Named pipeline `spellbook` (3x exponential+jitter retry, 50% circuit breaker, 10s timeout)
  - Used by: Deck analysis and comparison workflows

- **MTG Commander Banlist** - Format legality
  - Endpoint: https://mtgcommander.net/
  - SDK/Client: RestSharp via `ICommanderBanListService` in `DeckFlow.Web/Services/CommanderBanListService.cs`
  - Resilience: Named pipeline `banlist` (2x constant-backoff retry 200ms, 5s timeout)
  - Cached in `IMemoryCache`

- **EDHTop16** - EDH metagame data
  - SDK/Client: `IEdhTop16Client` in `DeckFlow.Web/Services/EdhTop16Client.cs`
  - Used by: Meta-gap analysis in `MetaGapService`

**Deck Import APIs:**
- **Moxfield REST API** - Deck import and export
  - SDK/Client: `IMoxfieldDeckImporter` / `MoxfieldApiDeckImporter` in `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs`
  - URL builder: `MoxfieldApiUrl.cs`
  - Resilience: Polly v8 with retry + timeout

- **Archidekt REST API** - Deck import, category aggregation, recent-deck harvesting
  - SDK/Client: `IArchidektDeckImporter` / `ArchidektApiDeckImporter` in `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs`
  - Also: `ArchidektRecentDecksImporter.cs` for category knowledge harvesting
  - URL builder: `ArchidektApiUrl.cs`
  - Resilience: Polly v8 with retry + timeout

**Content Knowledge Base (v1.4):**
- **OpenAI API** (API Key-based)
  - Version: OpenAI .NET SDK 2.10.0
  - Endpoints:
    - Whisper audio transcription: `POST /v1/audio/transcriptions`
    - Chat completions (GPT-4o-mini): `POST /v1/chat/completions`
  - SDK/Client: `OpenAI` package via `WhisperTranscriptionService` and `LlmDistillationService` in `DeckFlow.Core/Integration/`
  - Auth: `OPENAI_API_KEY` env var (Render dashboard, sync: false)
  - Spend tracking: `IWhisperSpendLedger` (per-call recording of seconds billed at $0.006/min) and `ILlmSpendLedger` (per-call recording of input/output tokens) with monthly cap checks (D-08, D-05)
  - Models: `whisper-1` (Whisper), `gpt-4o-mini` (distillation)
  - Distillation tasks:
    - Summary: 400 max output tokens (max 200 words)
    - Clips extraction: 1200 max output tokens (3-8 clips)
    - Tag generation: 200 max output tokens

- **YouTube** - Channel video listing and captions/audio
  - SDK/Client: YoutubeExplode 6.6.0 in `DeckFlow.Core/Integration/`
    - `YouTubeChannelVideoLister.cs` - Lists recent uploads via `YoutubeClient`
    - `YouTubeTranscriptFetcher.cs` - Fetches captions (auto-generated or manual)
    - `YouTubeAudioSource.cs` - Downloads audio for Whisper fallback (when captions unavailable)
  - Resilience: No explicit Polly pipelines; relies on YoutubeExplode's HTTP handling
  - Auth: Public API, no token required
  - WR-02 known issue: Per-video metadata lookup unbounded on large channel listings; revisit if --limit grows

- **Podcast RSS** - Feed parsing (planned v1.4 expansion)
  - SDK/Client: System.ServiceModel.Syndication (built-in .NET)
  - Auth: Public, URL-based
  - Usage: Planned in `TranscriptProviderFactory.cs` for podcast source ingestion

## Data Storage

**Databases:**

| Database | Default Provider | Production | Purpose | Connection Factory |
|----------|------------------|------------|---------|-------------------|
| feedback.db | SQLite | Postgres | Feedback submissions, admin brute-force tracking, feature flags, harvest state | `CreateFeedbackConnection()` |
| category-knowledge.db | SQLite | Postgres | Archidekt category aggregation, card→category relationships | `CreateCategoryKnowledgeConnection()` |
| content-kb.db | **SQLite only** | **SQLite only** | Video metadata, transcripts, distillation artifacts, Whisper/LLM spend ledgers | `CreateLocalContentKbConnection()` (D-14: local-only, never uploaded) |
| content-site-index.db | SQLite | Postgres | Slim content index (metadata only, no transcripts) for browser serving | `CreateContentSiteIndexConnection()` |

- Location: Default path `../artifacts/` from content root, overridable via `MTG_DATA_DIR` env var
- Connection factory: `DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs`
  - Provider selection via `DECKFLOW_DATABASE_PROVIDER` (defaults SQLite)
  - Postgres URI parsing: Converts `postgresql://user:pass@host:port/db` to `NpgsqlConnectionStringBuilder` with sslmode query-param support
  - Foreign-key enforcement: SQLite `PRAGMA foreign_keys=ON` set per-connection in `RelationalDatabaseConnection.OpenConnectionAsync()`

**File Storage:**
- Local filesystem only (no S3 / cloud blob storage)
- `/data` persistent volume in containers:
  - SQLite database files (artifacts/*.db)
  - ChatGPT prompt packet artifacts (stored by `ChatGptDeckPacketService`)
  - Browser extension zip (`wwwroot/extensions/deckflow-bridge.zip`)
  - Application logs (logs/web-.log, logs/cli-.log, 14-day rolling)

**Caching:**
- In-memory: `IMemoryCache` (built-in ASP.NET Core)
  - Ban list cache: 24h TTL
  - Search results cache: Variable TTL per service
  - Card lookup cache: `CardLookupCache` singleton
  - Tagger session cache: `TaggerSessionCache` (270s TTL)
  - Packet session cache: `PacketSessionCache` (per-request packets)

## Authentication & Identity

**HTTP Basic Auth (Admin):**
- Gate: `/Admin/*` endpoints via `BasicAuthMiddleware` in `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs`
- Credentials: `FEEDBACK_ADMIN_USER` and `FEEDBACK_ADMIN_PASSWORD` env vars
- Brute-force protection: `IAdminBruteForceTrackerStore` with 5-attempt throttle over sliding window (Phase 5 BUG-02)
- IP partitioning: `DeriveCloudflareClientIp()` reads `CF-Connecting-IP` header (Cloudflare-set, immune to proxy fan-out)

**Session Cookies:**
- Scryfall Tagger: Automatic cookie replay via `CookieContainer` in `SocketsHttpHandler` (Phase 5 BUG-01 fix)
- CSRF Token: Extracted from Tagger session HTML, cached 270s in `TaggerSessionCache`

**Same-Origin Requests:**
- Middleware: `SameOriginRequestValidator` in `DeckFlow.Web/Security/SameOriginRequestValidator.cs`
- Checks: Origin header must match request scheme/host or missing (fail-closed)
- Applied to: API endpoints (`POST /api/deck/diff`, suggestions endpoints)
- Forwarded headers: Requires `app.UseForwardedHeaders()` before `SameOriginRequestValidator` to see browser's original scheme/host

## Monitoring & Observability

**Error Tracking:**
- None detected (no Sentry, Application Insights, or rollbar integration)

**Logs:**
- Serilog to console (stdout, captured by Render service logs) and rolling daily file
- Web: `DeckFlow.Web/Program.cs:47-60` with structured templates (named placeholders, no string interpolation)
- CLI: `DeckFlow.CLI/Program.cs:12-16` with File sink rolling daily, 14-file retention
- Request logging: `app.UseSerilogRequestLogging()` in middleware pipeline (line 369, Program.cs)
- Decision markers: References to plan/CONTEXT docs (e.g., `D-01`, `D-06`, `HIGH-2`, `B2`) logged as comments

**Metrics:**
- Analytics middleware `D-12` in `DeckFlow.Web/Services/Analytics/` (Phase 25+)
- Stores request metrics in `IRequestMetricsStore` (feedback DB)
- IP hashing: Salt resolved at startup via `AnalyticsSaltAccessor` and `IpHasher` (Program.cs:436-450)

## CI/CD & Deployment

**Hosting:**
- Render.com (docker runtime, free plan, Oregon region)
- Container entry point: `sh -c "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet DeckFlow.Web.dll"`
- Health check: `GET /` (Render default, line 40 render.yaml)
- Disk: 1GB `/data` volume mounted at `/data`

**CI Pipeline:**
- None detected (public repo; no GitHub Actions, GitLab CI, or Azure Pipelines configuration committed)
- Build via Docker on Render: `docker build --file ./Dockerfile` + `docker push` + `docker pull` + `docker run`

**Deployment:**
- Container image: Multi-stage Dockerfile
  - Build stage: `mcr.microsoft.com/dotnet/sdk:10.0` with Node.js 20, TypeScript install, dotnet publish Release
  - Runtime stage: `mcr.microsoft.com/dotnet/aspnet:10.0` with `/data` volume, env vars, Kestrel listener
- Secrets: Render dashboard env vars with `sync: false`: `OPENAI_API_KEY`, `FEEDBACK_ADMIN_USER`, `FEEDBACK_ADMIN_PASSWORD`, `DECKFLOW_DATABASE_CONNECTION_STRING`
- Database: Postgres provisioned separately; connection string pasted into dashboard (render.yaml comment line 8-10)
- Auto-deploy: Enabled on render.yaml:22 (redeploy on repo push)

## Environment Configuration

**Required env vars:**
- `ASPNETCORE_ENVIRONMENT=Production` (production only)
- `MTG_DATA_DIR=/data` (production container path)
- `DECKFLOW_DATABASE_PROVIDER=Postgres` (production)
- `DECKFLOW_DATABASE_CONNECTION_STRING=postgresql://...` (production, set in dashboard sync: false)
- `OPENAI_API_KEY=sk-...` (required for Content KB v1.4, set in dashboard sync: false)

**Optional env vars:**
- `FEEDBACK_ADMIN_USER` / `FEEDBACK_ADMIN_PASSWORD` - Defaults to empty string (no admin auth enforced)
- `FEEDBACK_IP_SALT` - Auto-generated per session if not set
- `DECKFLOW_GEMINI_ENABLED=true` - Expose Google Gemini UI option (defaults false; paste limit hazard)
- `DECKFLOW_DISABLE_AUTO_BROWSER=true` - Skip auto-launching browser in dev

**Secrets location:**
- Render dashboard: `sync: false` env vars (never committed to git, only set in dashboard)
- Local dev: Untracked `launchSettings.json` profiles or environment shell sourcing
- CI/CD: Would go in GitHub Secrets / GitLab CI variables (not yet integrated)

## Webhooks & Callbacks

**Incoming:**
- None detected

**Outgoing:**
- None detected (app is entirely request-response, no event publishing or background job webhooks)

---

*Integration audit: 2026-05-29*
