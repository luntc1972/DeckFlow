<!-- GSD:project-start source:PROJECT.md -->
## Project

**DeckFlow**

DeckFlow is a Magic: The Gathering deck analysis tool for cEDH and Commander players, deployed live at https://www.deckflow.gg. It pulls deck data from Archidekt and Moxfield, generates ChatGPT-ready prompt artifacts for deck analysis, and provides synergy/category knowledge derived from the user's own crawled deck history. Audience: serious deck-builders who want a structured "compare, analyze, decide" workflow rather than a one-click recommender.

**Core Value:** **Every supported workflow must produce output the user can paste into ChatGPT and get back a useful answer in one round-trip — without the user reformatting anything.** Visual polish, theme variety, and admin tooling all serve that core. If the prompt artifacts are wrong or missing, nothing else matters.

### Constraints

- **Tech stack**: ASP.NET 10 + Razor — pinned by deployed app; no framework migration in this milestone
- **Hosting**: Render Starter web + Basic-256mb Postgres — 512MB RAM cap on web tier, mind allocations
- **Theme system**: Guild themes are full standalone CSS forks; layout CSS must go in `site-common.css`, not `site.css` — token additions go in `:root` of each theme file
- **HTTP resilience**: Use existing RestSharp + direct Polly v8 pattern — do NOT migrate to standard handler
- **Public repo**: `luntc1972/DeckFlow` is public — no secrets in commits ever; secrets live in Render dashboard with `sync: false`
- **Testing**: VSTest unreliable in WSL; rely on `dotnet build` clean + targeted manual harness or push-and-watch CI
- **Commits**: Plain default-author commits, no Co-Authored-By trailer; README updated when behavior changes; commit per logical change
- **Formatting**: `.editorconfig` is the enforced, tool-agnostic source of truth and `.gitattributes` still enforces LF line endings. New and changed C# lines must satisfy the changed-lines gate locally (`git config core.hooksPath .githooks` opt-in, then the versioned pre-commit hook runs `scripts/format-check-changed.sh staged`) and in CI (`format-gate`, which is the authoritative enforcer). Existing files are not mass-reflowed; the gate is changed-lines-only, so when editing a file, touch only the lines that need touching. The five bug-driven carve-outs override any conflicting formatter preference: never auto-convert `{ get; init; }` to `{ get; }` (System.Text.Json silently skips get-only properties in .NET 9+ — has broken `EdhTop16Client` deserialization before), never inline `[Attribute]` onto the property line, never re-indent C# raw-string literals (changes the literal value shipped to the AI), preserve switch expressions, preserve xmldoc single-space indent, preserve LF line endings (`.gitattributes` enforces). The carve-outs live authoritatively in `.editorconfig` and are guarded by the `CarveOutGuard` test.
<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->
## Technology Stack

## Languages
- C# 12 / .NET 10 - All server-side projects (`DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.CLI`, plus tests). `<TargetFramework>net10.0</TargetFramework>` and `<Nullable>enable</Nullable>` set in every csproj.
- TypeScript 6.x (compiles to ES2017) - Browser-side scripts in `DeckFlow.Web/wwwroot/ts/**/*.ts`, output to `wwwroot/js/`. Configured by `DeckFlow.Web/tsconfig.json` (`strict: true`, `module: "none"`).
- JavaScript (ES module) - Browser extension `browser-extensions/deckflow-bridge/{background.js,deckflow-bridge.js,options.js}` (Manifest V3, no build step).
- Razor (`.cshtml`) - MVC views under `DeckFlow.Web/Views/{About,Admin,Commander,Deck,Feedback,Help,Shared}/`.
- HTML / CSS - `DeckFlow.Web/wwwroot/css/`, `wwwroot/extension-install.html`.
- Markdown - In-app help (`DeckFlow.Web/Help/**/*.md` copied to output via `<Content>` item) and prompt templates (`prompt-templates/deck-comparison/`).
- PowerShell + Bash - Run scripts in `scripts/run-web.ps1` and `scripts/run-web.sh`.
## Runtime
- .NET 10 ASP.NET Core (Kestrel) - Web host bootstrapped in `DeckFlow.Web/Program.cs`.
- Container base images (production): `mcr.microsoft.com/dotnet/sdk:10.0` (build) and `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime), see `Dockerfile`.
- Node.js 20 (build-time only) - Installed in Docker build stage to compile TypeScript via the `CompileTypeScriptAssets` MSBuild target in `DeckFlow.Web/DeckFlow.Web.csproj`.
- NuGet for .NET dependencies - Restore portable settings in `Directory.Build.props` (clears `RestoreFallbackFolders` to avoid Visual Studio shared cache leakage in WSL).
- npm for TypeScript build tooling - `DeckFlow.Web/package.json`, `package-lock.json` (root + `DeckFlow.Web/`).
## Frameworks
- ASP.NET Core MVC 10.0 - Controllers + Razor views (`Microsoft.NET.Sdk.Web` SDK in `DeckFlow.Web.csproj`).
- Swashbuckle.AspNetCore 7.0.0 - Swagger UI exposed at `/swagger` in Development (registered in `DeckFlow.Web/Program.cs:148-163`).
- Microsoft.AspNetCore.RateLimiting (built-in) - Fixed window rate limiter on feedback submit (5/hr per IP), `DeckFlow.Web/Program.cs:130-146`.
- System.CommandLine 2.0.0-beta4.22272.1 - CLI parsing in `DeckFlow.CLI` (`Program.cs`, `CommandRunners.cs`).
- xUnit 2.9.3 - Both test projects (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`).
- xunit.runner.visualstudio 3.1.4 - VS test discovery.
- Microsoft.NET.Test.Sdk 17.14.1 - Test SDK.
- RichardSzalay.MockHttp 7.0.0 - HTTP mocking, used in `DeckFlow.Web.Tests` (e.g., `CommanderSpellbookServiceTests`).
- coverlet.collector 6.0.4 - Code coverage in `DeckFlow.Core.Tests`.
- TypeScript 6.0.2 (npm) plus `Microsoft.TypeScript.MSBuild` 5.2.2 - TS compiles in MSBuild `BeforeTargets="Build"` target in `DeckFlow.Web.csproj`.
- ESLint 10.2.0 (devDependency in `DeckFlow.Web/package.json`) - Not wired into MSBuild.
- MSBuild custom target `ZipDeckFlowBridge` - Zips `browser-extensions/deckflow-bridge/` to `wwwroot/extensions/deckflow-bridge.zip` on every `Build`/`Publish`.
## Key Dependencies
- RestSharp 114.0.0 - Single HTTP client abstraction for all upstream calls, used by every `DeckFlow.Web/Services/*Service.cs` and `DeckFlow.Core/Integration/*ApiDeckImporter.cs`.
- Polly 8.x - Resilience pipelines registered as named `ResiliencePipeline<RestResponse>` (banlist, spellbook, tagger, tagger-post, scryfall) in `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs`. Services resolve via `ResiliencePipelineProvider<string>`. `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` still uses legacy Polly `AsyncRetryPolicy` directly.
- Markdig 0.38.0 - Help-content Markdown rendering (`HelpContentService.cs`).
- Microsoft.Extensions.Caching.Memory (built-in via `AddMemoryCache()`) - Cache layer for ban list, search results, session cache, etc.
- Microsoft.Data.Sqlite 10.0.0 - Default storage for `feedback.db` and `category-knowledge.db` under `MTG_DATA_DIR`.
- Npgsql 10.0.0 - Optional Postgres provider (toggled via `DECKFLOW_DATABASE_PROVIDER=Postgres`).
- Serilog.AspNetCore 9.0.0 + Serilog.Sinks.Console 6.0.0 + Serilog.Sinks.File 6.0.0 - Structured logging, configured in `DeckFlow.Web/Program.cs:34-47`. Logs roll daily to `logs/web-.log` (14-file retention).
- Serilog 4.2.0 + Serilog.Sinks.File 7.0.0 - Used directly by `DeckFlow.CLI`.
- Microsoft.Extensions.Logging.Abstractions 10.0.0 - Used in `DeckFlow.Core` (no Serilog dependency in core).
## Configuration
- Configured via environment variables; no `.env` file present in repo.
- Required for production: `ASPNETCORE_ENVIRONMENT=Production`, `MTG_DATA_DIR=/data`, `PORT` (Render/Fly inject).
- Optional: `DECKFLOW_DATABASE_PROVIDER` (`Sqlite`|`Postgres`), `DECKFLOW_DATABASE_CONNECTION_STRING`, `FEEDBACK_ADMIN_USER`, `FEEDBACK_ADMIN_PASSWORD`, `FEEDBACK_IP_SALT`, `DECKFLOW_DISABLE_AUTO_BROWSER`.
- App-level: `DeckFlow.Web/appsettings.json` (logging defaults, allowed hosts) and `appsettings.Development.json` (logging override).
- `DeckFlow.Web/Properties/launchSettings.json` - Local dev URLs `http://localhost:5173` / `https://localhost:7173`.
- `Directory.Build.props` - Clears NuGet fallback folders.
- `DeckFlow.sln` - Solution file referencing all 5 projects.
- `DeckFlow.Web/tsconfig.json` - Strict TS config.
- `Dockerfile` - Multi-stage build (sdk:10.0 -> aspnet:10.0).
- `render.yaml` - Render Blueprint (Docker, starter plan, `/data` disk, `mtg-deck-studio` service name).
- `fly.toml` - Fly.io app `mtg-deck-studio`, Seattle region, shared-cpu-1x/512MB, `/data` mount.
## Platform Requirements
- .NET 10 SDK.
- Node.js (any recent version) + npm install once in `DeckFlow.Web/` to populate `node_modules/typescript` for the MSBuild TypeScript target.
- Cross-platform: WSL2, Linux, and Windows are all first-class targets. `Directory.Build.props` exists specifically because Windows VS shared NuGet cache breaks WSL restores.
- IIS Express + IIS profiles defined for Windows-only Visual Studio runs (`launchSettings.json`).
- Containerized .NET 10 on Linux. Listens on `${PORT:-8080}` over HTTP behind a TLS-terminating reverse proxy (Render or Fly). `UseForwardedHeaders` honors `X-Forwarded-{For,Proto,Host}` so HTTPS redirection and `SameOriginRequestValidator` see the browser's scheme.
- Persistent disk mounted at `/data` (Render `mtg-data` 1 GB; Fly `mtg_data` volume) holds SQLite DBs and ChatGPT artifacts.
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

## Naming Patterns
- One public type per `.cs` file; file name matches the type name exactly (`CardLookupService.cs` contains `ScryfallCardLookupService`).
- Interface and implementation often co-located in the same file (e.g., `ICardLookupService` + `ScryfallCardLookupService` + result records all live in `DeckFlow.Web/Services/CardLookupService.cs`).
- Test files mirror the source type with a `Tests` suffix: `CardLookupService.cs` → `CardLookupServiceTests.cs`.
- Razor views use PascalCase `.cshtml` (`CommanderCategories.cshtml`); shared partials prefixed `_` (`_ViewImports.cshtml`).
- TypeScript files in `DeckFlow.Web/wwwroot/ts/` use kebab/dot lowercase to match emitted JS bundles.
- Interfaces: `I` prefix, PascalCase (`ICardLookupService`, `ICommanderSpellbookService`, `IScryfallRestClientFactory`).
- Classes: PascalCase, prefer `sealed` on leaf types — see `public sealed class ScryfallCardLookupService` (`DeckFlow.Web/Services/CardLookupService.cs:42`) and `public sealed record DeckEntry` (`DeckFlow.Core/Models/DeckEntry.cs:3`).
- Records used for immutable DTOs / results: `CardLookupResult`, `SingleCardLookupResult`, `SpellbookCombo`, `DeckEntry`. Prefer `sealed record` with `init`/`required` properties.
- Test classes: `public sealed class XxxTests`.
- Test doubles: `Fake*` for stateful behavior fakes (`FakeCategoryKnowledgeStore`, `FakeHttpClientFactory`), `Stub*` for queue-driven stubs (`StubHttpMessageHandler`), `Throwing*` for exception injection (`ThrowingCardSearchService`).
- PascalCase, async methods always end in `Async` (`LookupAsync`, `FindCombosAsync`, `GetCategoriesAsync`).
- Internal/private helpers PascalCase too (`FormatCard`, `NormalizeName`, `ExtractMechanicNames`).
- Private instance fields: `_camelCase` with leading underscore (`_executeAsync`, `_logger`, `_httpClientFactory`).
- Static readonly fields: `PascalCase` (`MinInterval`, `RetryAfterCap`, `Gate`, `QuantityPrefixRegex`).
- Constants: `PascalCase` (`CollectionBatchSize`, `MaxCardsPerSubmission`, `ApiUrl`, `MaxIncluded`).
- Locals and parameters: `camelCase`.
- File-scoped, mirror folder layout: `namespace DeckFlow.Web.Services;`, `namespace DeckFlow.Core.Models;`, `namespace DeckFlow.Web.Tests;`.
- Tests live in a single namespace per project (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`) regardless of subfolder.
## Code Style
- `.editorconfig` is checked in and is the enforced source of truth for formatting; new and changed C# lines must pass the local hook/CI changed-lines gate.
- 4-space indentation in C#; 2-space in `.json` config.
- Allman braces (open brace on new line) throughout C#.
- File-scoped namespaces (`namespace X;`) — never block-scoped.
- One `using` directive per line, sorted with `System.*` first then third-party then `DeckFlow.*`. No global `Using Include` in `DeckFlow.Web` (uses `ImplicitUsings=enable` instead); `DeckFlow.Core.Tests.csproj` adds `<Using Include="Xunit" />`.
- `<TargetFramework>net10.0</TargetFramework>`
- `<Nullable>enable</Nullable>` — nullable reference types are enforced everywhere.
- `<ImplicitUsings>enable</ImplicitUsings>` — `System`, `System.Linq`, `System.Threading.Tasks` etc. are implicit.
- `DeckFlow.Web.csproj` adds `<GenerateDocumentationFile>true</GenerateDocumentationFile>` with `<NoWarn>$(NoWarn);1591;1573;1587</NoWarn>` to suppress missing-doc warnings on partials.
- No external linter (no Roslyn analyzers, no StyleCop config). Compiler warnings + nullable diagnostics are the gate.
## Import Organization
- Not applicable (C#). Project references via `<ProjectReference>` in `.csproj`:
## Error Handling
- **Argument validation at the top of constructors:** `ArgumentNullException.ThrowIfNull(...)` — see `CommanderSpellbookService` ctor (`DeckFlow.Web/Services/CommanderSpellbookService.cs:77-78`) and `FakeHttpClientFactory:11`.
- **HTTP error translation:** non-2xx upstream responses throw `HttpRequestException` with the upstream status code preserved:
- **Centralized upstream-error messaging:** `UpstreamErrorMessageBuilder.BuildScryfallMessage(exception)` produces user-facing strings; controllers return 503 with this body (`CommanderController.cs:103-110`).
- **Controllers catch broad `Exception`** at the action boundary, log it, and re-render the view with an `ErrorMessage` populated on the view model (`CommanderController.cs:80-88`). They distinguish `OperationCanceledException` (timeout copy) from generic failures.
- **Graceful degradation in services:** `CommanderSpellbookService.FindCombosAsync` returns `null` on API failure rather than throwing, and the prompt builder continues without combo data (see service comments and `CommanderSpellbookServiceTests.FindCombosAsync_ApiFailure_ReturnsNull`).
- **Cancellation-token timeouts** wrap the request token: `CancellationTokenSource.CreateLinkedTokenSource(HttpContext?.RequestAborted ...).CancelAfter(LookupTimeout)` (`CommanderController.cs:55-57`).
- **Throw guards for upstream HTTP families** centralized in helper: `ScryfallThrottle.ThrowIfUpstreamUnavailable(HttpStatusCode)` raises `HttpRequestException` for 429 and 5xx (`ScryfallThrottle.cs:111-121`).
## Logging
- Inject `ILogger<TController>` / `ILogger<TService>` via constructor.
- Use **structured templates** with named placeholders, never string interpolation:
- Default `ILogger<T>` parameter to optional/nullable in services and fall back to `NullLogger<T>.Instance` so tests don't have to wire one (`CommanderSpellbookService.cs:75, 82`; tests use `NullLogger<DeckController>.Instance`).
- File sink rolls daily, `retainedFileCountLimit: 14`, output under `<ContentRoot>/logs/web-.log`.
- Console sink stays enabled in production so platforms like Render/Fly capture stdout.
- Request logging via `app.UseSerilogRequestLogging();` in the middleware pipeline (`Program.cs:210`).
## Comments
- XML doc comments (`/// <summary>`) on every public type, interface, public method, and public record. `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is on, so missing doc warnings are explicitly suppressed for known noise (`NoWarn` 1591/1573/1587).
- Use `<param>`/`<returns>` tags on non-trivial methods.
- Inline comments explain **why**, not what. Examples:
- Decision/risk markers like `D-01`, `D-06`, `HIGH-2`, `B2` reference plan/CONTEXT documents (see `Program.cs:58-62, 82-104`).
## Function Design
- Service classes are larger (200-500+ LOC) but methods stay focused. Long methods are usually a single async pipeline (e.g., `LookupAsync` at ~90 LOC orchestrates parse → batched fetch → fallback → format).
- Helpers are extracted to private `static` methods when pure (no `this` access): `NormalizeName`, `FormatCard`, `Chunk`, `ExtractMechanicNames`, `ParseLines` (`CardLookupService.cs`).
- All async methods take an optional `CancellationToken cancellationToken = default` as the **last** parameter.
- Use `IReadOnlyList<T>` / `IReadOnlyDictionary<TK,TV>` on result records and method parameters; never expose mutable `List<T>` on public surface.
- Test seam pattern: optional `Func<...> executeAsyncOverride` delegates injected via internal constructor so tests bypass live HTTP without mocking `IHttpClientFactory` (`CardLookupService.cs:106-121`). Production constructor takes the DI-resolved dependencies; internal test ctor is exposed via `[InternalsVisibleTo("DeckFlow.Web.Tests")]` in `DeckFlow.Web/AssemblyInfo.cs:3`.
- Prefer `record`/`sealed record` for multi-value results (`CardLookupResult`, `CommanderSpellbookResult`).
- Use nullable return (`Task<T?>`) to indicate "operation succeeded but no match"; throw for upstream/system errors.
- For collection returns use `IReadOnlyList<T>`; for "nothing found" return `Array.Empty<T>()` not `null`.
## Module Design
- `public` for surface that crosses project boundaries (controllers, services consumed by DI, view models, core models).
- `internal` for test doubles (`StubHttpMessageHandler`, `FakeHttpClientFactory`, `FakeResiliencePipelineProvider`, `FakeScryfallRestClientFactory`) so they stay scoped to the test assembly.
- `internal` constructor used for test seams + `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]` to grant the test project access without leaking to consumers.
- `static` classes for stateless helpers (`ScryfallThrottle`, `MoxfieldApiUrl`, `ArchidektApiUrl`, `CardNormalizer`).
## Dependency Injection Conventions
- All registrations live in `DeckFlow.Web/Program.cs:50-189`. No DI extension methods except `AddDeckFlowResiliencePipelines()` and `UseDeckFlowSecurityHeaders()`.
- Lifetime guidelines applied:
- Hosted background work uses `AddHostedService` plus a singleton facade so controllers can call into it (`ArchidektCacheJobService` registered both as `Singleton` and `HostedService`, `Program.cs:178-180`).
## HTTP / Resilience Conventions
- `IHttpClientFactory` named clients configured in one place (`Program.cs:63-89`) — `commander-banlist`, `commander-spellbook`, `scryfall-rest`, plus a typed client `ScryfallTaggerHttpClient`.
- All external HTTP calls flow through **RestSharp** (`RestClient` wrapping the factory's `HttpClient`) plus **Polly v8** `ResiliencePipeline<RestResponse>` resolved via `ResiliencePipelineProvider<string>` keyed by name (`scryfall`, `spellbook`, ...).
- Static throttle gate `ScryfallThrottle.ExecuteAsync` is wrapped around every Scryfall call to enforce ~5 req/s pacing across the whole process.
- Each HTTP-touching service exposes a public DI ctor and an `internal` test ctor that injects a delegate (`Func<RestRequest, CancellationToken, Task<RestResponse<T>>>`) — this is the canonical test seam (see `CardLookupService.cs:91-121`).
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

## System Overview
```text
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
- Controller-per-feature MVC, with API controllers split into `Controllers/Api/` and admin controllers in `Controllers/Admin/`.
- Service-oriented internals: every controller dependency is an interface (`I*Service`), registered in `Program.cs` and consumed via constructor injection.
- HTTP egress is centralized in `DeckFlow.Web/Services/Http/` — services do not `new HttpClient()`; they receive an `IHttpClientFactory` (or typed client) plus a `ResiliencePipelineProvider<string>` and resolve a named pipeline.
- Domain logic that is pure CPU work (parsing, diffing, exporting, normalization, reporting) lives in `DeckFlow.Core` and has no `HttpClient`/`AspNet` references.
- Persistence is dialect-pluggable: `IRelationalDialect` with `SqliteRelationalDialect` and `PostgresRelationalDialect` implementations behind `RelationalDatabaseConnection`.
- Razor Views drive the UI; client-side TypeScript in `wwwroot/ts/*` compiles to `wwwroot/js/*` during MSBuild.
- A browser-extension companion (`browser-extensions/deckflow-bridge`) is zipped into `wwwroot/extensions/` at build time.
## Layers
- Purpose: Headless command runner (`compare`, `probe-moxfield`, `export-moxfield`, `archidekt-categories`, etc.)
- Location: `DeckFlow.CLI/`
- Contains: `System.CommandLine` setup + invocation handlers
- Depends on: `DeckFlow.Core`
- Used by: Local power users, scripts in `scripts/`
- Purpose: Deck domain logic, with no I/O frameworks beyond `Microsoft.Data.Sqlite`/`Npgsql` for the storage dialect and `RestSharp`/`Polly` for `Integration/*` HTTP importers
- Location: `DeckFlow.Core/`
- Contains: `Models/`, `Parsing/`, `Diffing/`, `Exporting/`, `Filtering/`, `Loading/`, `Normalization/`, `Reporting/`, `Knowledge/`, `Integration/` (Moxfield/Archidekt importers), `Storage/` (relational dialect)
- Depends on: `Microsoft.Data.Sqlite`, `Npgsql`, `Polly`, `RestSharp`, `Microsoft.Extensions.Logging.Abstractions`
- Used by: `DeckFlow.Web`, `DeckFlow.CLI`, both test projects
- Purpose: HTTP entry points (Razor MVC + JSON API + admin pages)
- Location: `DeckFlow.Web/Controllers/`, `Controllers/Api/`, `Controllers/Admin/`
- Contains: Thin orchestrators that bind models, invoke services, return `IActionResult`
- Depends on: Web service interfaces
- Used by: Browser, DeckFlow Bridge extension, external API consumers
- Purpose: Application logic, external adapters, persistence stores
- Location: `DeckFlow.Web/Services/`
- Contains: ~30 services. Sub-folder `Services/Http/` holds HTTP infrastructure (resilience pipeline factory, null-impl factories used in tests/CLI).
- Depends on: `DeckFlow.Core`, `IHttpClientFactory`, `ResiliencePipelineProvider<string>`, `IMemoryCache`, RestSharp, Markdig, Serilog
- Used by: Controllers, hosted services
- Purpose: Cross-cutting middleware and security primitives
- Location: `DeckFlow.Web/Infrastructure/`, `DeckFlow.Web/Security/`
- Contains: `BasicAuthMiddleware`, `SecurityHeadersApplicationBuilderExtensions`, `DevelopmentBrowserLauncher`, `SameOriginRequestValidator`
- Used by: `Program.Main` middleware pipeline, every API controller
- Purpose: Server-side HTML rendering
- Location: `DeckFlow.Web/Views/`
- Contains: One folder per controller (`Deck/`, `Commander/`, `Admin/`, `Help/`, `About/`, `Feedback/`) plus `Shared/` partials and `_Layout.cshtml`
- Used by: `Controller.View(...)` calls
- Purpose: Themed CSS, compiled TypeScript modules, packaged browser extension
- Location: `DeckFlow.Web/wwwroot/`
- Contains: `css/site*.css` (one per guild theme + `site-common.css` + `site.css`), `ts/*.ts` (source), `js/*.js` (compiled output), `extensions/deckflow-bridge.zip`, `lib/`
## Data Flow
### Primary Request Path — Deck Sync (browser)
### Outgoing HTTP Adapter Flow (e.g., banlist)
### Tagger Session Flow
### CategorySuggestion Mode Routing
- Server state: singletons for read-mostly caches (`TaggerSessionCache`, `IMemoryCache`, hosted `ArchidektCacheJobService`).
- Per-request state: scoped services (`IDeckSyncService`, `ICategorySuggestionService`, ChatGPT services) — `Program.cs:174-184`.
- Persistent state: SQLite (default, file in content root) or Postgres via connection string env var; chosen at startup by `DeckFlowDatabaseConnectionFactory`.
- Client state: page-local TS modules; no SPA framework.
## Key Abstractions
- Purpose: Immutable deck primitives (`DeckEntry`, `DeckDiff`, `LoadedDecks`, `MatchMode`, `SyncDirection`, `PrintingChoice`, `PrintingConflict`).
- Pattern: C# `record` types where appropriate; nullable reference types enabled.
- Purpose: Convert raw deck text into `DeckEntry` lists.
- Pattern: `IParser` interface with `MoxfieldParser` and `ArchidektParser` implementations; throws `DeckParseException` on bad input.
- Purpose: Fetch decks from external sites.
- Pattern: `IMoxfieldDeckImporter` / `IArchidektDeckImporter` (`DeckImporterInterfaces.cs`) with `*ApiDeckImporter` and URL-builder helpers.
- Purpose: Pluggable SQL backend.
- Pattern: `IRelationalDialect` with `SqliteRelationalDialect` and `PostgresRelationalDialect`; `RelationalDatabaseConnection` is the consumer-facing handle.
- Purpose: Single composition-time registration of all named Polly pipelines.
- Pattern: `services.AddDeckFlowResiliencePipelines()` extension; consumers resolve by string name via `ResiliencePipelineProvider<string>` (NOT keyed services).
- Purpose: Strongly-typed payloads bound to Razor views and JSON APIs.
- Convention: View-specific models named `*ViewModel`, request DTOs named `*Request`, response DTOs in `Models/Api/*`.
- Purpose: Shared navigation chrome rendered by `Views/Shared/_WorkflowStepTabs.cshtml` so every Deck tool shows the same step strip.
## Entry Points
- Location: `DeckFlow.Web/Program.cs`
- Triggers: `dotnet run --project DeckFlow.Web` or container startup (`Dockerfile`, `fly.toml`, `render.yaml`)
- Responsibilities: Configure Serilog, register all DI services, build Polly pipelines, configure middleware (forwarded headers → security headers → HTTPS redirect → static files → routing → request logging → Swagger (Dev) → auth → rate limit → BasicAuth on `/Admin` → `MapControllers` + default route), validate DB connections in non-Dev, run.
- Location: `DeckFlow.CLI/Program.cs`
- Triggers: `dotnet run --project DeckFlow.CLI -- <command> ...`
- Responsibilities: Configure Serilog file sink, build `System.CommandLine` root with `compare`, `probe-moxfield`, `export-moxfield`, `archidekt-categories`, `archidekt-category-cards` commands; dispatch to `CommandRunners`.
- `GET /` → `DeckController.Home`
- `GET /sync` → `DeckController.Index`
- Plus `/lookup`, `/mechanic-lookup`, `/convert`, `/suggest-categories`, `/judge-questions`, `/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`, `/commander-categories`, `/help`, `/about`, `/feedback`.
- Default conventional route registered at the end (`Program.cs:230`).
- `POST /api/deck/diff` → `DeckSyncApiController`
- Suggestion endpoints under `SuggestionsApiController`
- Internal cache control under `ArchidektCacheJobsController`
- `/Admin/*` — guarded by `BasicAuthMiddleware` branch (`Program.cs:225-227`)
- `/swagger` — Development only.
## Architectural Constraints
- **Threading:** Standard ASP.NET Core async request pipeline. Hosted background service `ArchidektCacheJobService` runs on the host scheduler. `ScryfallThrottle` is a static `SemaphoreSlim` enforcing global Scryfall rate limit; do not bypass it for Scryfall callers.
- **Global state:** Static `ScryfallThrottle` (`DeckFlow.Web/Services/ScryfallThrottle.cs`) is shared across all Scryfall services. Static `ScryfallRestClientFactory` shim retained for back-compat (Phase 1 note in `Program.cs:108`).
- **Cookie/session lifetime invariant:** `TaggerSessionCache` TTL (270s) MUST stay strictly below `ScryfallTaggerHttpClient` `SetHandlerLifetime` (5 min) — see comment at `Program.cs:83-95`.
- **Forwarded headers:** `app.UseForwardedHeaders()` MUST run before HTTPS redirect / security headers / `SameOriginRequestValidator`, otherwise scheme mismatch breaks CSRF check (`Program.cs:194-196`).
- **Build coupling:** `DeckFlow.Web.csproj` runs `tsc -p tsconfig.json` and zips `browser-extensions/deckflow-bridge` on every build. TS sources live in `wwwroot/ts/` (git-tracked); compiled output goes to `wwwroot/js/` and is **gitignored** (`.gitignore` ignores `DeckFlow.Web/wwwroot/js/*.js`) — never stage or commit compiled `.js`. The Docker build rebuilds all TS at deploy (Node 20 + `CompileTypeScriptAssets` on `dotnet publish`), so committed `.js` is unnecessary and creates stale-artifact drift.
- **Shared package path bug (env):** Building from VS-shared NuGet path on Windows can leave a stale `project.assets.json`; build from WSL or clean obj/.
## Anti-Patterns
### Direct `new HttpClient()` in services
### Building Polly pipelines per call
### Using `Microsoft.Extensions.Http.Resilience` standard handler
### Calling Scryfall without `ScryfallThrottle`
### Skipping `SameOriginRequestValidator` on API endpoints
### Putting layout CSS into `site.css`
## Error Handling
- Controllers catch domain exceptions (`DeckParseException`, validation errors) and return 400 with a structured `{ Message }` body or model-state errors for Razor.
- Polly handles transient HTTP failures (retry + timeout + circuit breaker); persistent failures bubble up and are converted to user-facing messages via `UpstreamErrorMessageBuilder`.
- Top-level `try/catch/finally` in `Program.Main` logs fatal startup/run exceptions through Serilog and flushes the sink before rethrowing.
- Non-development environments use `app.UseExceptionHandler("/Deck")` to render a friendly error view.
- Same-origin and rate-limit failures return 403 / 429 with `{ Message }`.
- Upstream API failures funnel through `UpstreamErrorMessageBuilder` so users see service-specific copy ("Scryfall is unreachable…", etc.).
- Tagger 404s and CSRF expiry are treated as soft errors and surfaced as empty suggestion sets, not exceptions.
## Cross-Cutting Concerns
<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->
## Project Skills

No project skills found. Add skills to any of: `.claude/skills/`, `.agents/skills/`, `.cursor/skills/`, `.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->



<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
