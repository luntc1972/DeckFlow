<!-- refreshed: 2026-04-29 -->
# Architecture

**Analysis Date:** 2026-04-29

## System Overview

```text
┌──────────────────────────────────────────────────────────────────────┐
│                      Browser / External Caller                       │
│  Razor Views (cshtml) + TS bundles + DeckFlow Bridge extension       │
└──────────────────────────────────┬───────────────────────────────────┘
                                   │ HTTPS (same-origin guarded)
                                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│                      DeckFlow.Web (ASP.NET Core MVC)                 │
├──────────────────┬────────────────────────┬──────────────────────────┤
│  MVC Controllers │   API Controllers      │  Admin (BasicAuth)       │
│ `Controllers/*`  │ `Controllers/Api/*`    │ `Controllers/Admin/*`    │
│ Razor Views      │ JSON, [ApiController]  │ Feedback console         │
│ `Views/*`        │ Same-origin guard      │                          │
└────────┬─────────┴──────────┬─────────────┴──────────────┬───────────┘
         │                    │                            │
         ▼                    ▼                            ▼
┌──────────────────────────────────────────────────────────────────────┐
│                       DeckFlow.Web Services Layer                    │
│ `DeckFlow.Web/Services/*` (~30 service classes, mostly singletons)   │
│  - Deck workflow (DeckSyncService, DeckConvertService)               │
│  - Lookup/search (CardLookup, CardSearch, MechanicLookup, SetService)│
│  - ChatGPT packet builders (ChatGptDeckPacketService, etc.)          │
│  - External adapters (CommanderBanList, CommanderSpellbook,          │
│    ScryfallTagger, EdhTop16, Archidekt cache job, Moxfield/Archidekt)│
│  - Persistence (FeedbackStore, CategoryKnowledgeStore)               │
└────────┬─────────────────────────────┬────────────────────────────┬──┘
         │                             │                            │
         ▼                             ▼                            ▼
┌────────────────────────┐  ┌──────────────────────┐  ┌────────────────────┐
│ DeckFlow.Web HTTP      │  │  DeckFlow.Core       │  │ Persistence        │
│ infrastructure         │  │  (domain library)    │  │                    │
│ `Services/Http/*`      │  │ `DeckFlow.Core/*`    │  │ SQLite / Postgres  │
│  - IHttpClientFactory  │  │  Diffing, Parsing,   │  │ via                 │
│    named clients       │  │  Loading, Models,    │  │ `Core/Storage/*`    │
│  - Polly v8 resilience │  │  Reporting, Knowledge│  │ pluggable dialect   │
│    pipeline registry   │  │  Integration, Export │  │ (Sqlite/Postgres)   │
│  - RestSharp wrappers  │  │  Filtering, Normalize│  │                     │
└──────────┬─────────────┘  └──────────┬───────────┘  └─────────┬──────────┘
           │                           │                         │
           ▼                           ▼                         ▼
   External APIs:             In-process domain          Local SQLite file
   Scryfall (REST + Tagger),  logic (no I/O)             or Render/Fly
   Moxfield, Archidekt,                                  Postgres
   mtgcommander.net banlist,
   CommanderSpellbook,
   EDHTop16
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| `Program.Main` | Composition root, DI wiring, middleware pipeline, Serilog config, startup DB validation | `DeckFlow.Web/Program.cs` |
| `DeckController` | Razor views for deck sync, convert, lookup, mechanic lookup, ChatGPT packet/comparison/CEDH gap, judge questions, suggest categories | `DeckFlow.Web/Controllers/DeckController.cs` |
| `CommanderController` | Commander category page | `DeckFlow.Web/Controllers/CommanderController.cs` |
| `FeedbackController` | Feedback submission (rate-limited) | `DeckFlow.Web/Controllers/FeedbackController.cs` |
| `HelpController` | Markdown-rendered help topics | `DeckFlow.Web/Controllers/HelpController.cs` |
| `AboutController` | Credits/version page | `DeckFlow.Web/Controllers/AboutController.cs` |
| `DeckSyncApiController` | JSON deck diff endpoint | `DeckFlow.Web/Controllers/Api/DeckSyncApiController.cs` |
| `SuggestionsApiController` | JSON category suggestion endpoint | `DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs` |
| `ArchidektCacheJobsController` | Internal job control endpoint | `DeckFlow.Web/Controllers/Api/ArchidektCacheJobsController.cs` |
| `AdminFeedbackController` | Admin-only feedback console (BasicAuth) | `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs` |
| `IDeckSyncService` | Loads two decks via `IDeckEntryLoader`, validates Commander size, runs `DiffEngine` | `DeckFlow.Web/Services/DeckSyncService.cs` |
| `IDeckConvertService` | Converts deck text between Moxfield/Archidekt formats | `DeckFlow.Web/Services/DeckConvertService.cs` |
| `ICategorySuggestionService` | Mode-routed category suggestion (cached, reference deck, tagger, all) | `DeckFlow.Web/Services/CategorySuggestionService.cs` |
| `IChatGptDeckPacketService` | Builds ChatGPT prompt packets and stores artifacts | `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` |
| `IScryfallCardLookupService` / `Search` / `Set` / `CommanderSearch` | Scryfall REST adapters (RestSharp + Polly) | `DeckFlow.Web/Services/Scryfall*.cs`, `CardLookupService.cs`, `CardSearchService.cs` |
| `IScryfallTaggerService` | Scrapes tagger.scryfall.com via cookie-disabled `SocketsHttpHandler` + CSRF session cache | `DeckFlow.Web/Services/ScryfallTaggerService.cs`, `TaggerSessionCache.cs` |
| `ICommanderBanListService` | Fetches banlist HTML from mtgcommander.net | `DeckFlow.Web/Services/CommanderBanListService.cs` |
| `ICommanderSpellbookService` | Combo lookup via backend.commanderspellbook.com | `DeckFlow.Web/Services/CommanderSpellbookService.cs` |
| `IEdhTop16Client` | EDH metagame data | `DeckFlow.Web/Services/EdhTop16Client.cs` |
| `ArchidektCacheJobService` | Hosted background service refreshing knowledge cache from Archidekt | `DeckFlow.Web/Services/ArchidektCacheJobService.cs` |
| `IFeedbackStore` / `ICategoryKnowledgeStore` | Persistence over `RelationalDatabaseConnection` (SQLite or Postgres) | `DeckFlow.Web/Services/FeedbackStore.cs`, `CategoryKnowledgeStore.cs` |
| `ResiliencePipelineFactory` | Registers five named Polly v8 `ResiliencePipeline<RestResponse>` (banlist, spellbook, tagger, tagger-post, scryfall) | `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` |
| `ScryfallTaggerHttpClient` | Typed `HttpClient` wrapper with cookie-disabled `SocketsHttpHandler` | `DeckFlow.Web/Services/ScryfallTaggerHttpClient.cs` |
| `IScryfallRestClientFactory` | Builds RestSharp `RestClient` from named `IHttpClientFactory` HTTP clients | `DeckFlow.Web/Services/ScryfallRestClientFactory.cs` |
| `SameOriginRequestValidator` | CSRF guard for API endpoints (Origin/Referer match) | `DeckFlow.Web/Security/SameOriginRequestValidator.cs` |
| `BasicAuthMiddleware` | HTTP Basic Auth gate for `/Admin/*` | `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` |
| `SecurityHeadersApplicationBuilderExtensions` | CSP, X-Frame-Options, etc. | `DeckFlow.Web/Infrastructure/SecurityHeadersApplicationBuilderExtensions.cs` |
| `DeckFlow.Core` (lib) | Pure-domain deck logic: parsers, diff, exporters, models, knowledge, normalization, reporting, storage dialect | `DeckFlow.Core/*` |
| `DeckFlow.CLI` | `System.CommandLine` host for compare/probe/export commands | `DeckFlow.CLI/Program.cs`, `CommandRunners.cs` |

## Pattern Overview

**Overall:** Layered ASP.NET Core MVC + Web API monolith with a separately compiled domain library (`DeckFlow.Core`) and a thin CLI front-end. Web layer talks to upstream services through a uniform "named `IHttpClientFactory` client + RestSharp + Polly v8 pipeline (resolved by name)" pattern.

**Key Characteristics:**
- Controller-per-feature MVC, with API controllers split into `Controllers/Api/` and admin controllers in `Controllers/Admin/`.
- Service-oriented internals: every controller dependency is an interface (`I*Service`), registered in `Program.cs` and consumed via constructor injection.
- HTTP egress is centralized in `DeckFlow.Web/Services/Http/` — services do not `new HttpClient()`; they receive an `IHttpClientFactory` (or typed client) plus a `ResiliencePipelineProvider<string>` and resolve a named pipeline.
- Domain logic that is pure CPU work (parsing, diffing, exporting, normalization, reporting) lives in `DeckFlow.Core` and has no `HttpClient`/`AspNet` references.
- Persistence is dialect-pluggable: `IRelationalDialect` with `SqliteRelationalDialect` and `PostgresRelationalDialect` implementations behind `RelationalDatabaseConnection`.
- Razor Views drive the UI; client-side TypeScript in `wwwroot/ts/*` compiles to `wwwroot/js/*` during MSBuild.
- A browser-extension companion (`browser-extensions/deckflow-bridge`) is zipped into `wwwroot/extensions/` at build time.

## Layers

**DeckFlow.CLI:**
- Purpose: Headless command runner (`compare`, `probe-moxfield`, `export-moxfield`, `archidekt-categories`, etc.)
- Location: `DeckFlow.CLI/`
- Contains: `System.CommandLine` setup + invocation handlers
- Depends on: `DeckFlow.Core`
- Used by: Local power users, scripts in `scripts/`

**DeckFlow.Core (domain):**
- Purpose: Deck domain logic, with no I/O frameworks beyond `Microsoft.Data.Sqlite`/`Npgsql` for the storage dialect and `RestSharp`/`Polly` for `Integration/*` HTTP importers
- Location: `DeckFlow.Core/`
- Contains: `Models/`, `Parsing/`, `Diffing/`, `Exporting/`, `Filtering/`, `Loading/`, `Normalization/`, `Reporting/`, `Knowledge/`, `Integration/` (Moxfield/Archidekt importers), `Storage/` (relational dialect)
- Depends on: `Microsoft.Data.Sqlite`, `Npgsql`, `Polly`, `RestSharp`, `Microsoft.Extensions.Logging.Abstractions`
- Used by: `DeckFlow.Web`, `DeckFlow.CLI`, both test projects

**DeckFlow.Web Controllers:**
- Purpose: HTTP entry points (Razor MVC + JSON API + admin pages)
- Location: `DeckFlow.Web/Controllers/`, `Controllers/Api/`, `Controllers/Admin/`
- Contains: Thin orchestrators that bind models, invoke services, return `IActionResult`
- Depends on: Web service interfaces
- Used by: Browser, DeckFlow Bridge extension, external API consumers

**DeckFlow.Web Services:**
- Purpose: Application logic, external adapters, persistence stores
- Location: `DeckFlow.Web/Services/`
- Contains: ~30 services. Sub-folder `Services/Http/` holds HTTP infrastructure (resilience pipeline factory, null-impl factories used in tests/CLI).
- Depends on: `DeckFlow.Core`, `IHttpClientFactory`, `ResiliencePipelineProvider<string>`, `IMemoryCache`, RestSharp, Markdig, Serilog
- Used by: Controllers, hosted services

**DeckFlow.Web Infrastructure / Security:**
- Purpose: Cross-cutting middleware and security primitives
- Location: `DeckFlow.Web/Infrastructure/`, `DeckFlow.Web/Security/`
- Contains: `BasicAuthMiddleware`, `SecurityHeadersApplicationBuilderExtensions`, `DevelopmentBrowserLauncher`, `SameOriginRequestValidator`
- Used by: `Program.Main` middleware pipeline, every API controller

**Razor View Layer:**
- Purpose: Server-side HTML rendering
- Location: `DeckFlow.Web/Views/`
- Contains: One folder per controller (`Deck/`, `Commander/`, `Admin/`, `Help/`, `About/`, `Feedback/`) plus `Shared/` partials and `_Layout.cshtml`
- Used by: `Controller.View(...)` calls

**Static Assets / Client TS:**
- Purpose: Themed CSS, compiled TypeScript modules, packaged browser extension
- Location: `DeckFlow.Web/wwwroot/`
- Contains: `css/site*.css` (one per guild theme + `site-common.css` + `site.css`), `ts/*.ts` (source), `js/*.js` (compiled output), `extensions/deckflow-bridge.zip`, `lib/`

## Data Flow

### Primary Request Path — Deck Sync (browser)

1. User submits deck-sync form on `/sync` (`DeckFlow.Web/Views/Deck/DeckSync.cshtml`).
2. Browser TS (`wwwroot/ts/deck-sync.ts`) POSTs JSON to `/api/deck/diff`.
3. `DeckSyncApiController.PostDiffAsync` validates same-origin via `SameOriginRequestValidator.IsValid(Request)` (`DeckFlow.Web/Controllers/Api/DeckSyncApiController.cs:48`).
4. Controller delegates to `IDeckSyncService.CompareDecksAsync` (`DeckFlow.Web/Services/DeckSyncService.cs:48`).
5. Service uses `IDeckEntryLoader` to either parse pasted text via `MoxfieldParser`/`ArchidektParser` (`DeckFlow.Core/Parsing/*`) or fetch via `IMoxfieldDeckImporter`/`IArchidektDeckImporter` (`DeckFlow.Core/Integration/*`).
6. `DiffEngine.Compare` (`DeckFlow.Core/Diffing/DiffEngine.cs`) produces `DeckDiff`.
7. Controller serializes `DeckSyncApiResponse` (`DeckFlow.Web/Models/Api/DeckSyncApiResponse.cs`) and returns 200.

### Outgoing HTTP Adapter Flow (e.g., banlist)

1. Service receives `IHttpClientFactory` and `ResiliencePipelineProvider<string>` via constructor.
2. Service requests `client = factory.CreateClient("commander-banlist")` (named in `Program.cs:63`).
3. Service wraps it in a RestSharp `RestClient` (via `ScryfallRestClientFactory` for Scryfall, or directly).
4. Service resolves `pipeline = provider.GetPipeline<RestResponse>("banlist")`.
5. Each call goes `await pipeline.ExecuteAsync(ct => client.ExecuteAsync(req, ct))` — Polly handles retry/timeout/circuit-break.
6. `ScryfallThrottle` (static) gates concurrency for Scryfall calls (`DeckFlow.Web/Services/ScryfallThrottle.cs`).

### Tagger Session Flow

1. `ScryfallTaggerService` requests `IScryfallTaggerHttpClient` (typed client with cookies disabled, `Program.cs:85`).
2. Service consults `ITaggerSessionCache` (singleton, 270s TTL — 30s under handler 5min lifetime) for a CSRF token + cookie set.
3. Cache miss: GET tagger landing, scrape CSRF, store in cache.
4. POST card lookup with stored CSRF/cookies, deserialize via `ScryfallTaggerParsers`.

### CategorySuggestion Mode Routing

1. UI POSTs `CategorySuggestionRequest` with `Mode` enum (`CachedData=0`, `ReferenceDeck=1`, `ScryfallTagger=2`, `All=3`).
2. `CategorySuggestionService` switches: cache (`ICategoryKnowledgeStore`), reference (`ArchidektDeckCacheSession`), tagger (`IScryfallTaggerService`), or merges all three.
3. Result formatted by `CategorySuggestionMessageBuilder`.

**State Management:**
- Server state: singletons for read-mostly caches (`TaggerSessionCache`, `IMemoryCache`, hosted `ArchidektCacheJobService`).
- Per-request state: scoped services (`IDeckSyncService`, `ICategorySuggestionService`, ChatGPT services) — `Program.cs:174-184`.
- Persistent state: SQLite (default, file in content root) or Postgres via connection string env var; chosen at startup by `DeckFlowDatabaseConnectionFactory`.
- Client state: page-local TS modules; no SPA framework.

## Key Abstractions

**Domain models (`DeckFlow.Core/Models/`):**
- Purpose: Immutable deck primitives (`DeckEntry`, `DeckDiff`, `LoadedDecks`, `MatchMode`, `SyncDirection`, `PrintingChoice`, `PrintingConflict`).
- Pattern: C# `record` types where appropriate; nullable reference types enabled.

**Parsers (`DeckFlow.Core/Parsing/`):**
- Purpose: Convert raw deck text into `DeckEntry` lists.
- Pattern: `IParser` interface with `MoxfieldParser` and `ArchidektParser` implementations; throws `DeckParseException` on bad input.

**Importers (`DeckFlow.Core/Integration/`):**
- Purpose: Fetch decks from external sites.
- Pattern: `IMoxfieldDeckImporter` / `IArchidektDeckImporter` (`DeckImporterInterfaces.cs`) with `*ApiDeckImporter` and URL-builder helpers.

**Storage dialect (`DeckFlow.Core/Storage/`):**
- Purpose: Pluggable SQL backend.
- Pattern: `IRelationalDialect` with `SqliteRelationalDialect` and `PostgresRelationalDialect`; `RelationalDatabaseConnection` is the consumer-facing handle.

**Resilience pipeline registry (`DeckFlow.Web/Services/Http/`):**
- Purpose: Single composition-time registration of all named Polly pipelines.
- Pattern: `services.AddDeckFlowResiliencePipelines()` extension; consumers resolve by string name via `ResiliencePipelineProvider<string>` (NOT keyed services).

**View models (`DeckFlow.Web/Models/`):**
- Purpose: Strongly-typed payloads bound to Razor views and JSON APIs.
- Convention: View-specific models named `*ViewModel`, request DTOs named `*Request`, response DTOs in `Models/Api/*`.

**Workflow tabs (`DeckFlow.Web/Models/WorkflowStepTabsModel.cs`, `DeckPageTab.cs`):**
- Purpose: Shared navigation chrome rendered by `Views/Shared/_WorkflowStepTabs.cshtml` so every Deck tool shows the same step strip.

## Entry Points

**ASP.NET Core web host:**
- Location: `DeckFlow.Web/Program.cs`
- Triggers: `dotnet run --project DeckFlow.Web` or container startup (`Dockerfile`, `fly.toml`, `render.yaml`)
- Responsibilities: Configure Serilog, register all DI services, build Polly pipelines, configure middleware (forwarded headers → security headers → HTTPS redirect → static files → routing → request logging → Swagger (Dev) → auth → rate limit → BasicAuth on `/Admin` → `MapControllers` + default route), validate DB connections in non-Dev, run.

**CLI host:**
- Location: `DeckFlow.CLI/Program.cs`
- Triggers: `dotnet run --project DeckFlow.CLI -- <command> ...`
- Responsibilities: Configure Serilog file sink, build `System.CommandLine` root with `compare`, `probe-moxfield`, `export-moxfield`, `archidekt-categories`, `archidekt-category-cards` commands; dispatch to `CommandRunners`.

**MVC routes:**
- `GET /` → `DeckController.Home`
- `GET /sync` → `DeckController.Index`
- Plus `/lookup`, `/mechanic-lookup`, `/convert`, `/suggest-categories`, `/judge-questions`, `/chatgpt-packets`, `/chatgpt-comparison`, `/chatgpt-cedh-meta-gap`, `/commander-categories`, `/help`, `/about`, `/feedback`.
- Default conventional route registered at the end (`Program.cs:230`).

**API routes:**
- `POST /api/deck/diff` → `DeckSyncApiController`
- Suggestion endpoints under `SuggestionsApiController`
- Internal cache control under `ArchidektCacheJobsController`

**Admin route:**
- `/Admin/*` — guarded by `BasicAuthMiddleware` branch (`Program.cs:225-227`)

**Swagger UI:**
- `/swagger` — Development only.

## Architectural Constraints

- **Threading:** Standard ASP.NET Core async request pipeline. Hosted background service `ArchidektCacheJobService` runs on the host scheduler. `ScryfallThrottle` is a static `SemaphoreSlim` enforcing global Scryfall rate limit; do not bypass it for Scryfall callers.
- **Global state:** Static `ScryfallThrottle` (`DeckFlow.Web/Services/ScryfallThrottle.cs`) is shared across all Scryfall services. Static `ScryfallRestClientFactory` shim retained for back-compat (Phase 1 note in `Program.cs:108`).
- **Cookie/session lifetime invariant:** `TaggerSessionCache` TTL (270s) MUST stay strictly below `ScryfallTaggerHttpClient` `SetHandlerLifetime` (5 min) — see comment at `Program.cs:83-95`.
- **Forwarded headers:** `app.UseForwardedHeaders()` MUST run before HTTPS redirect / security headers / `SameOriginRequestValidator`, otherwise scheme mismatch breaks CSRF check (`Program.cs:194-196`).
- **Build coupling:** `DeckFlow.Web.csproj` runs `tsc -p tsconfig.json` and zips `browser-extensions/deckflow-bridge` on every build. TS sources live in `wwwroot/ts/`, output goes to `wwwroot/js/` and is also git-tracked.
- **Shared package path bug (env):** Building from VS-shared NuGet path on Windows can leave a stale `project.assets.json`; build from WSL or clean obj/.

## Anti-Patterns

### Direct `new HttpClient()` in services

**What happens:** A service constructs `HttpClient` instead of receiving `IHttpClientFactory`.
**Why it's wrong:** Skips the named-client config in `Program.cs`, bypasses `SocketHttpHandler` lifetime rotation, and breaks Polly pipeline resolution.
**Do this instead:** Inject `IHttpClientFactory` and call `factory.CreateClient("<name>")` matching the registration in `Program.cs:63-89`. For Tagger, inject `IScryfallTaggerHttpClient`.

### Building Polly pipelines per call

**What happens:** Service rebuilds `ResiliencePipelineBuilder<RestResponse>` inside the request method.
**Why it's wrong:** Defeats Polly's circuit-breaker state (it must persist across calls) and adds allocation cost.
**Do this instead:** Resolve the named pipeline once via `ResiliencePipelineProvider<string>.GetPipeline<RestResponse>("name")`. Add new pipelines in `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs`.

### Using `Microsoft.Extensions.Http.Resilience` standard handler

**What happens:** Wiring `AddStandardResilienceHandler()` on the named client.
**Why it's wrong:** Project deliberately uses **direct Polly v8 pipelines on `RestResponse`** (RestSharp), not the MS standard handler — the response shape is `RestResponse`, not `HttpResponseMessage`.
**Do this instead:** Register pipelines in `ResiliencePipelineFactory.cs` keyed by string name and resolve by `RestResponse`.

### Calling Scryfall without `ScryfallThrottle`

**What happens:** A new Scryfall client bypasses the static throttle.
**Why it's wrong:** Risks tripping Scryfall rate limits and invalidates the global concurrency invariant.
**Do this instead:** Wrap Scryfall calls with the existing `ScryfallThrottle` API used by all 7+ Scryfall services.

### Skipping `SameOriginRequestValidator` on API endpoints

**What happens:** New API action returns data without origin check.
**Why it's wrong:** Project relies on origin/referer matching (no auth) to keep browser-only endpoints from being abused cross-site.
**Do this instead:** Start every state-changing or data-returning API action with `if (!SameOriginRequestValidator.IsValid(Request)) return StatusCode(403, ...)` (see `DeckSyncApiController.cs:48`).

### Putting layout CSS into `site.css`

**What happens:** New site-wide CSS rule added to `wwwroot/css/site.css`.
**Why it's wrong:** Themes are full standalone forks of `site.css` per guild — adding shared layout there means each theme silently drifts.
**Do this instead:** Put cross-theme layout/structural rules in `wwwroot/css/site-common.css`. Theme-specific colors stay in `site-<guild>.css`.

## Error Handling

**Strategy:**
- Controllers catch domain exceptions (`DeckParseException`, validation errors) and return 400 with a structured `{ Message }` body or model-state errors for Razor.
- Polly handles transient HTTP failures (retry + timeout + circuit breaker); persistent failures bubble up and are converted to user-facing messages via `UpstreamErrorMessageBuilder`.
- Top-level `try/catch/finally` in `Program.Main` logs fatal startup/run exceptions through Serilog and flushes the sink before rethrowing.
- Non-development environments use `app.UseExceptionHandler("/Deck")` to render a friendly error view.

**Patterns:**
- Same-origin and rate-limit failures return 403 / 429 with `{ Message }`.
- Upstream API failures funnel through `UpstreamErrorMessageBuilder` so users see service-specific copy ("Scryfall is unreachable…", etc.).
- Tagger 404s and CSRF expiry are treated as soft errors and surfaced as empty suggestion sets, not exceptions.

## Cross-Cutting Concerns

**Logging:** Serilog configured in `Program.cs:34-47` — console sink (always on, Render/Fly only capture stdout) plus rolling daily file sink under `logs/`. Request logging via `app.UseSerilogRequestLogging()`.

**Validation:** Request DTOs use data-annotations (`[Required]`, etc.) plus explicit guard methods in services (`ArgumentNullException.ThrowIfNull`, `ValidateCommanderDeckSize`).

**Authentication:** No user auth for the public site. `/Admin/*` is gated by `BasicAuthMiddleware`. API CSRF protection is `SameOriginRequestValidator` (Origin/Referer match on browser requests; non-browser callers permitted).

**Rate limiting:** ASP.NET Core `AddRateLimiter` with a `feedback-submit` policy (5/hour per IP) at `Program.cs:130-146`.

**Security headers:** `app.UseDeckFlowSecurityHeaders()` at `Program.cs:205` (CSP, X-Frame-Options, etc.).

**Forwarded headers:** Required first in pipeline so Render/Fly proxy hops don't break HTTPS scheme detection.

**Configuration / environment:** `appsettings.json` + `appsettings.{Environment}.json`; runtime env vars override (e.g., DB connection strings, basic-auth creds, browser auto-launch toggle `MTGDECKSTUDIO_DISABLE_AUTO_BROWSER`).

---

*Architecture analysis: 2026-04-29*
