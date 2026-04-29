# External Integrations

**Analysis Date:** 2026-04-29

## APIs & External Services

**Card data (Scryfall):**
- `https://api.scryfall.com/` - Card lookup, search, set listings.
  - Named HttpClient: `"scryfall-rest"` registered in `DeckFlow.Web/Program.cs:75-80`.
  - Wrapper: `DeckFlow.Web/Services/ScryfallRestClientFactory.cs` (`IScryfallRestClientFactory`) issues `RestClient` over the named `HttpClient`.
  - Throttle: `DeckFlow.Web/Services/ScryfallThrottle.cs` - Process-wide `SemaphoreSlim` enforcing 200ms pacing and 429 retry; wraps every Scryfall call.
  - Resilience pipeline: `"scryfall"` in `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` (30s total timeout outermost, retry x2 on 5xx only - 429 deferred to throttle).
  - Consumers: `ScryfallCardLookupService.cs`, `ScryfallCardSearchService.cs`, `ScryfallCommanderSearchService.cs`, `ScryfallSetService.cs`, `ScryfallTaggerService.cs` (set/number resolution), and `ChatGptDeckPacketService.cs` (batched in `ScryfallBatchSize = 75` chunks).
  - Auth: None. User-Agent `DeckFlow/1.0 (+https://github.com/luntc1972/DeckFlow)`.

- `https://tagger.scryfall.com/` - Community oracle/functional tag GraphQL endpoint (CSRF-protected).
  - Typed HttpClient: `ScryfallTaggerHttpClient` registered in `DeckFlow.Web/Program.cs:85-98`.
  - Primary handler: `SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false, PooledConnectionLifetime = 5min }`. CSRF tokens are explicitly replayed via headers (no `CookieContainer`).
  - Session cache: `DeckFlow.Web/Services/TaggerSessionCache.cs` (`ITaggerSessionCache`) with 270s TTL (30s buffer below the 5-min handler lifetime).
  - Pipelines: `"tagger"` (GET path - retry x3 exponential+jitter, 8s timeout, CB 50%/30s) and `"tagger-post"` (POST path, no retry because GraphQL POST is non-idempotent).
  - Consumer: `DeckFlow.Web/Services/ScryfallTaggerService.cs` (`IScryfallTaggerService`).

**Commander metadata:**
- `https://mtgcommander.net/index.php/banned-list/` - HTML scrape of the official Commander banned list.
  - Named HttpClient: `"commander-banlist"` (`DeckFlow.Web/Program.cs:63-67`).
  - Pipeline: `"banlist"` - retry x2 (200ms constant), 5s attempt timeout, no circuit breaker.
  - Consumer: `DeckFlow.Web/Services/CommanderBanListService.cs` (6 hr in-memory cache).

- `https://backend.commanderspellbook.com/` - Combo lookup + Moxfield deck fallback.
  - Named HttpClient: `"commander-spellbook"` (`DeckFlow.Web/Program.cs:69-73`).
  - Pipeline: `"spellbook"` - retry x3 exponential+jitter, 10s timeout, CB 50%/30s.
  - Endpoints used: `find-my-combos` (combo search) and `card-list-from-url` (Moxfield fallback when Moxfield blocks the cloud egress IP).
  - Consumer: `DeckFlow.Web/Services/CommanderSpellbookService.cs`. Fallback callsite: `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs:FetchViaCommanderSpellbookAsync`.

- `https://edhtop16.com/api/graphql` - cEDH tournament results GraphQL.
  - Direct `RestClient` (no named factory) in `DeckFlow.Web/Services/EdhTop16Client.cs:21`. Endpoint hardcoded; injectable `executeAsync` for tests.
  - Consumer: `DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs`.

- `https://json.edhrec.com/pages/cards/` - EDHREC card lookup JSON.
  - Consumer: `DeckFlow.Core/Integration/EdhrecCardLookup.cs` (used by `CategorySuggestionService` for the EdhrecCategories source).

- `https://magic.wizards.com/en/rules` - Wizards of the Coast Comprehensive Rules HTML page.
  - Consumer: `DeckFlow.Web/Services/MechanicLookupService.cs` (`WotcMechanicLookupService`). Resolves the link to the Comprehensive Rules text file and caches the document for 6 hr.

**Deck import sources:**
- `https://api.moxfield.com/v2/decks/all/` - Moxfield deck JSON.
  - Consumer: `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` (`IMoxfieldDeckImporter`).
  - Falls back to Commander Spellbook `card-list-from-url` when Moxfield returns cloud-edge block (e.g., 403/429 on cloud IPs).
  - Browser-extension fallback: `browser-extensions/deckflow-bridge/` (Manifest V3) reads the user's logged-in Moxfield session client-side.

- `https://archidekt.com/api/decks/` - Archidekt deck JSON.
  - Consumer: `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` (legacy Polly `AsyncRetryPolicy` - not migrated to v8 named pipelines yet; retry x6, exponential backoff with jitter).
  - Recent-decks importer: `DeckFlow.Core/Integration/ArchidektRecentDecksImporter.cs` used by `CategoryKnowledgeStore` to harvest 20 decks per sweep.
  - URL helpers: `ArchidektApiUrl.cs`, `MoxfieldApiUrl.cs`.

## Data Storage

**Databases:**
- Primary: SQLite (default).
  - Files: `${MTG_DATA_DIR}/feedback.db` (admin feedback) and `${MTG_DATA_DIR}/category-knowledge.db` (Archidekt-harvested category labels).
  - Client: `Microsoft.Data.Sqlite` 10.0.0.
  - Connection factory: `DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs`.
- Optional: Postgres.
  - Selected via `DECKFLOW_DATABASE_PROVIDER=Postgres` + `DECKFLOW_DATABASE_CONNECTION_STRING`.
  - Client: `Npgsql` 10.0.0.
  - Dialect abstraction: `DeckFlow.Core/Storage/{IRelationalDialect.cs,SqliteRelationalDialect.cs,PostgresRelationalDialect.cs}` and `RelationalDatabaseConnection.cs`.
  - Repositories: `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs`, `DeckFlow.Web/Services/FeedbackStore.cs`, `DeckFlow.Web/Services/CategoryKnowledgeStore.cs`.
- Startup validation: `Program.cs:ValidateDatabaseConnectionsAsync` runs `CountAsync` and `GetProcessedDeckCountAsync` on Production startup so misconfigured DBs fail fast.

**File Storage:**
- Local filesystem under `MTG_DATA_DIR` (production: `/data` mount on Render disk or Fly volume).
  - ChatGPT artifacts: `DeckFlow.Web/Services/ChatGptArtifactsDirectory.cs` and `ChatGptPacketArtifactStore.cs`.
  - Held-content includes generated prompts, deck-comparison packets, set-upgrade results.
- Markdown help content shipped from `DeckFlow.Web/Help/**/*.md` (`PreserveNewest` copy-to-output).
- Browser extension zip: `DeckFlow.Web/wwwroot/extensions/deckflow-bridge.zip` produced by `ZipDeckFlowBridge` MSBuild target.
- Logs: `DeckFlow.Web/logs/web-YYYYMMDD.log` (Serilog file sink, daily rolling, 14 retained).

**Caching:**
- In-process `IMemoryCache` (`AddMemoryCache()` in `Program.cs:56`).
  - Used by `CommanderBanListService` (6 hr), `WotcMechanicLookupService` (6 hr), `ScryfallCommanderSearchService`, etc.
- `TaggerSessionCache` (singleton) - Custom 270s TTL session/CSRF cache for the Tagger flow.
- Background harvest worker: `ArchidektCacheJobService` (`AddHostedService`) periodically populates `category-knowledge.db`.

## Authentication & Identity

**Auth Provider:**
- No third-party identity provider. The application is unauthenticated for end users.
- Admin endpoints (`/Admin/**`): HTTP Basic auth via `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs`.
  - Credentials sourced from `FEEDBACK_ADMIN_USER` / `FEEDBACK_ADMIN_PASSWORD` environment variables.
  - Returns 503 when env vars are missing (so admin is "off" by default).
  - Constant-time compare via `CryptographicOperations.FixedTimeEquals`.
- CSRF / origin enforcement: `DeckFlow.Web/Security/SameOriginRequestValidator.cs` validates `Origin`/`Referer` header matches request scheme+host+port for browser callers. Non-browser callers (no Origin/Referer) are allowed.
- Forwarded headers: `app.UseForwardedHeaders()` runs before HTTPS redirection so the validator sees the public HTTPS scheme behind Render/Fly proxies (`KnownIPNetworks` and `KnownProxies` are cleared because Render assigns dynamic proxy IPs).
- Security headers: `DeckFlow.Web/Infrastructure/SecurityHeadersApplicationBuilderExtensions.cs` sets CSP `default-src 'self'`, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy: camera=(), microphone=(), geolocation=()` on every response (CSP suppressed for `/swagger`).

## Monitoring & Observability

**Error Tracking:**
- None (no Sentry, App Insights, or equivalent integration).

**Logs:**
- Serilog (`Program.cs:34-47`):
  - Console sink (active in all environments because Render captures stdout/stderr).
  - File sink at `ContentRoot/logs/web-.log`, daily rolling, 14 file retention.
  - `UseSerilogRequestLogging()` adds per-request log entries.
  - `Log.Fatal` on host-startup failure; `Log.CloseAndFlush()` in `finally`.
- `DeckFlow.CLI` writes its own Serilog file sink (`Serilog.Sinks.File` 7.0.0).

## CI/CD & Deployment

**Hosting:**
- Render (primary): `render.yaml` declares `type: web`, `runtime: docker`, plan `starter`, `healthCheckPath: /`, autoDeploy true. Disk `mtg-data` (1 GB) at `/data`.
- Fly.io (alternate): `fly.toml`, region `sea`, shared-cpu-1x / 512 MB, volume `mtg_data` mounted at `/data`, `force_https=true`, `auto_stop_machines=stop`, `min_machines_running=0`. Health check `GET /` every 15s with 30s grace.
- Both honor `${PORT}` from the platform; Dockerfile entrypoint: `ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet DeckFlow.Web.dll`.

**CI Pipeline:**
- None checked into the repo (no `.github/workflows`, `azure-pipelines.yml`, `Jenkinsfile`, or similar). Render's auto-deploy triggers on `git push` per `render.yaml: autoDeploy: true`.

## Environment Configuration

**Required env vars:**
- `ASPNETCORE_ENVIRONMENT` - `Production` for Render/Fly; `Development` enables Swagger UI and auto browser launch.
- `MTG_DATA_DIR` - Filesystem path for SQLite DBs and ChatGPT artifacts. Defaults to `<contentRoot>/../artifacts` when unset (dev only). Production uses `/data`.
- `PORT` - Injected by Render/Fly; consumed in the Dockerfile entrypoint.

**Optional env vars:**
- `DECKFLOW_DATABASE_PROVIDER` - `Sqlite` (default) or `Postgres`.
- `DECKFLOW_DATABASE_CONNECTION_STRING` - Required when provider is Postgres; for SQLite either a `Data Source=...` string or a bare path that gets wrapped in `SqliteConnectionStringBuilder`.
- `FEEDBACK_ADMIN_USER`, `FEEDBACK_ADMIN_PASSWORD` - Enables `/Admin/**` Basic auth. Both must be non-empty or middleware returns 503.
- `FEEDBACK_IP_SALT` - Salt for hashing IPs in `FeedbackStore` (privacy).
- `MTGDECKSTUDIO_DISABLE_AUTO_BROWSER` - Set to `true` to skip the Development browser auto-launch.

**Secrets location:**
- Provided by hosting platform's secret store (Render Environment Variables, Fly secrets). No `.env` files are committed; none exist in the repo at the time of this analysis.

## Webhooks & Callbacks

**Incoming:**
- None. The app exposes only browser-driven UI endpoints (Razor controllers) and JSON APIs under `DeckFlow.Web/Controllers/Api/` consumed by the same UI / browser extension.
- Health probes: `GET /` (Render `healthCheckPath`, Fly `[[http_service.checks]]`).

**Outgoing:**
- None. All upstream traffic is request/response (no published webhooks or push subscriptions).

**Browser extension surface:**
- `browser-extensions/deckflow-bridge/manifest.json` (Manifest V3) declares host permissions for `https://moxfield.com/*`, `https://api.moxfield.com/*`, `https://api2.moxfield.com/*`. The extension fetches Moxfield deck data from the user's logged-in browser session and posts back into the DeckFlow page via content script `deckflow-bridge.js` and service worker `background.js`.

---

*Integration audit: 2026-04-29*
