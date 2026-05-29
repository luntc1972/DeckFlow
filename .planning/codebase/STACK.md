# Technology Stack

**Analysis Date:** 2026-05-29

## Languages

**Primary:**
- C# 12 / .NET 10 - All server-side projects (`DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.CLI`) with `<TargetFramework>net10.0</TargetFramework>` and nullable reference types enabled
- TypeScript 6.0.2 (compiles to ES2017) - Browser scripts in `DeckFlow.Web/wwwroot/ts/**/*.ts`, output to `wwwroot/js/` during MSBuild `CompileTypeScriptAssets` target
- JavaScript (ES module) - Browser extension `browser-extensions/deckflow-bridge/{background.js, deckflow-bridge.js, options.js}` (Manifest V3, no build step)

**Secondary:**
- Razor (`.cshtml`) - MVC views under `DeckFlow.Web/Views/{About,Admin,Commander,Deck,Feedback,Help,Shared}/`
- HTML / CSS - Theme system with `site-common.css` + per-guild `site.css` variants in `DeckFlow.Web/wwwroot/css/`
- Markdown - Help content in `DeckFlow.Web/Help/**/*.md`, prompt templates in `prompt-templates/`
- PowerShell + Bash - Local dev scripts in `scripts/`

## Runtime

**ASP.NET Core:**
- .NET 10 Kestrel on HTTP via reverse proxy (TLS at Cloudflare/Render edge)
- Bootstrapped in `DeckFlow.Web/Program.cs` with Serilog configuration, DI wiring, and middleware pipeline
- Listens on port specified by `PORT` env var or `8080` (see `Dockerfile` line 60)
- `UseForwardedHeaders()` honoring `X-Forwarded-{For,Proto,Host}` for proxy transparency

**TypeScript Build:**
- TypeScript 6.0.2 (npm) + `Microsoft.TypeScript.MSBuild` 5.2.2 MSBuild target
- Compiles `DeckFlow.Web/wwwroot/ts/*.ts` to `wwwroot/js/*.js` in `CompileTypeScriptAssets` target before each Build/Publish
- Node.js 20 installed in Docker build stage (Dockerfile line 16) for npm TypeScript installation

**Package Managers:**
- NuGet for .NET dependencies (configured in `Directory.Build.props` with fallback-folder clearing for WSL/Windows cross-compilation)
- npm for TypeScript build tooling (`DeckFlow.Web/package.json`)

## Frameworks

**Core Web:**
- ASP.NET Core MVC 10.0 - Controllers + Razor views via `Microsoft.NET.Sdk.Web` in `DeckFlow.Web.csproj`
- System.CommandLine 2.0.0-beta4.22272.1 - CLI parsing in `DeckFlow.CLI/Program.cs` and `CommandRunners.cs` with commands: `compare`, `probe-moxfield`, `export-moxfield`, `archidekt-*`, `card-lookup`, `scryfall-probe`, `content-*`, `harvest`, `distill`

**HTTP & Resilience:**
- RestSharp 114.0.0 - Single HTTP client abstraction for all upstream calls (Scryfall, Commander Spellbook, MTG Commander banlist)
- Polly 8.6.6 - Five named `ResiliencePipeline<RestResponse>` registered in `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs`: `banlist` (2x retry, 5s timeout), `spellbook` (3x exponential+jitter, 50% circuit breaker), `scryfall` (3x exponential+jitter, 50% circuit breaker, 20s timeout), `tagger` / `tagger-post` (separate GET/POST pipelines with decompression support for Cloudflare BIC bypass)
- Resolved via `ResiliencePipelineProvider<string>` keyed by name

**API Documentation:**
- Swashbuckle.AspNetCore 7.0.0 - Swagger UI at `/swagger` in Development mode

**Templating & Markdown:**
- Markdig 0.38.0 - Renders help-content Markdown to HTML in `HelpContentService.cs`

**Caching:**
- Microsoft.Extensions.Caching.Memory (built-in) - Used by: `CardLookupCache`, `PacketSessionCache`, `TaggerSessionCache` (270s TTL), ban list service, search services

**Content Knowledge Base (v1.4):**
- OpenAI 2.10.0 - Dual integration: (1) Whisper audio transcription via `WhisperTranscriptionService` with spend ledger gating, (2) GPT-4o-mini LLM distillation via `LlmDistillationService` (summary, clip extraction, tag generation) in `DeckFlow.Core/Integration/`
- YoutubeExplode 6.6.0 - YouTube channel video listing and transcript fetching in `YouTubeChannelVideoLister.cs` and `YouTubeTranscriptFetcher.cs`
- System.ServiceModel.Syndication - Podcast RSS feed parsing (planned, referenced in phase notes)
- FFmpeg - Audio chunking for large files via `IFfmpegAudioChunker` interface in `DeckFlow.Core/Integration/FfmpegAudioChunker.cs` (24MB chunk threshold)

**Testing:**
- xUnit 2.9.3 - Both test projects (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`) with runner xunit.runner.visualstudio 3.1.4
- Microsoft.NET.Test.Sdk 17.14.1 - Test SDK
- RichardSzalay.MockHttp 7.0.0 - HTTP mocking in `DeckFlow.Web.Tests`
- coverlet.collector 6.0.4 - Code coverage (enabled in `DeckFlow.Core.Tests`)

**Data Persistence:**
- Microsoft.Data.Sqlite 10.0.0 - SQLite provider for local dev/artifact stores
- Npgsql 10.0.0 - PostgreSQL provider (production connection via `RelationalDatabaseConnection` abstraction)
- Dialect abstraction: `IRelationalDialect` with `SqliteRelationalDialect` and `PostgresRelationalDialect` implementations

**Logging:**
- Serilog.AspNetCore 9.0.0 + Serilog.Sinks.Console 6.0.0 + Serilog.Sinks.File 6.0.0 (web)
- Serilog 4.2.0 + Serilog.Sinks.File 7.0.0 (CLI)
- Structured logging configured in `DeckFlow.Web/Program.cs:47-60` with Console + File (14-day rolling)
- Request logging via `app.UseSerilogRequestLogging()` in middleware pipeline

## Key Dependencies

**Critical:**
- RestSharp 114.0.0 - Required by every HTTP-touching service (`*Service.cs` in `DeckFlow.Web/Services/` and `DeckFlow.Core/Integration/`)
- Polly 8.x - Resilience infrastructure; services resolve pipelines by name via `ResiliencePipelineProvider<string>`
- OpenAI 2.10.0 v1.4 - Whisper transcription ($0.006/min) and GPT-4o-mini distillation ($0.00015 input / $0.0006 output per 1K tokens) with monthly spend caps per `IWhisperSpendLedger` and `ILlmSpendLedger`

**Infrastructure:**
- Microsoft.Data.Sqlite 10.0.0 - Default artifact storage (feedback.db, category-knowledge.db, content-kb.db)
- Npgsql 10.0.0 - Production Postgres connection (Render managed database)
- Markdig 0.38.0 - Help content rendering
- YoutubeExplode 6.6.0 - YouTube integration for Content KB harvesting
- System.Text.Json (built-in) - JSON serialization with custom `JsonStringEnumConverter` in `Program.cs:67`

## Configuration

**Environment Variables:**
- `ASPNETCORE_ENVIRONMENT` - Set to `Production` in Render (line 24, render.yaml)
- `MTG_DATA_DIR` - Artifact path; defaults to `../artifacts` from content root. Set to `/data` in containers
- `DECKFLOW_DATABASE_PROVIDER` - `Sqlite` (default) or `Postgres` (set to `Postgres` in render.yaml:29)
- `DECKFLOW_DATABASE_CONNECTION_STRING` - Postgres connection URI; required when provider is Postgres. Parsed as `postgresql://` or `postgres://` URI with user:pass@host:port/database
- `OPENAI_API_KEY` - Required for Whisper and LLM distillation; must be in Render dashboard (sync: false per line 31, render.yaml)
- `FEEDBACK_ADMIN_USER` / `FEEDBACK_ADMIN_PASSWORD` - HTTP Basic Auth credentials for `/Admin/*` endpoints (sync: false per render.yaml:33-34)
- `FEEDBACK_IP_SALT` - Optional salt for IP hashing in analytics (defaults generated per session if not set)
- `DECKFLOW_GEMINI_ENABLED` - Optional toggle (defaults false) to enable Google Gemini in AI platform selection UI
- `DECKFLOW_DISABLE_AUTO_BROWSER` - Optional flag (set to "true" in Render/CI to skip auto-launching browser in dev)

**Build Configuration:**
- `DeckFlow.Web/tsconfig.json` - Strict TypeScript (strict: true, module: "none", target: "ES2017")
- `DeckFlow.Web.csproj` - GenerateDocumentationFile enabled with NoWarn suppressing 1591/1573/1587 (missing XML docs on partials)
- `Dockerfile` - Multi-stage: build stage installs Node.js 20, runs `npm install typescript`, publishes Release build; runtime stage exposes port 8080, mounts `/data` volume, sets `MTG_DATA_DIR=/data`
- `render.yaml` - Render Blueprint (docker runtime, free plan, Oregon region, 1GB `/data` disk)
- `.editorconfig` - Formatting conventions (4-space indents C#, 2-space JSON, Allman braces, LF line endings)
- `Directory.Build.props` - Clears NuGet fallback folders for cross-platform WSL/Windows builds

## Platform Requirements

**Development:**
- .NET 10 SDK
- Node.js 20+ (or any recent version) to install TypeScript for the MSBuild target
- Cross-platform: WSL2, Linux, and Windows as first-class targets. `Directory.Build.props` prevents Windows VS shared NuGet cache from breaking WSL restores
- Visual Studio 2022 recommended (IIS Express + Kestrel profiles in `launchSettings.json`, Windows-only)

**Production:**
- Containerized .NET 10 (`mcr.microsoft.com/dotnet/aspnet:10.0` runtime image)
- Listens on `${PORT:-8080}` over HTTP behind TLS-terminating reverse proxy (Cloudflare + Render)
- `UseForwardedHeaders` honors `X-Forwarded-{For,Proto,Host}` so HTTPS scheme and same-origin CSRF checks see browser's original values
- Persistent `/data` volume (1GB on Render) for SQLite artifacts and ChatGPT prompt packets. Relational data (feedback, harvest state, analytics, feature flags) is Postgres-backed in production
- Health check: `GET /` (default Render health check at render.yaml:40)

---

*Stack analysis: 2026-05-29*
