# Technology Stack

**Analysis Date:** 2026-04-29

## Languages

**Primary:**
- C# 12 / .NET 10 - All server-side projects (`DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.CLI`, plus tests). `<TargetFramework>net10.0</TargetFramework>` and `<Nullable>enable</Nullable>` set in every csproj.
- TypeScript 6.x (compiles to ES2017) - Browser-side scripts in `DeckFlow.Web/wwwroot/ts/**/*.ts`, output to `wwwroot/js/`. Configured by `DeckFlow.Web/tsconfig.json` (`strict: true`, `module: "none"`).

**Secondary:**
- JavaScript (ES module) - Browser extension `browser-extensions/deckflow-bridge/{background.js,deckflow-bridge.js,options.js}` (Manifest V3, no build step).
- Razor (`.cshtml`) - MVC views under `DeckFlow.Web/Views/{About,Admin,Commander,Deck,Feedback,Help,Shared}/`.
- HTML / CSS - `DeckFlow.Web/wwwroot/css/`, `wwwroot/extension-install.html`.
- Markdown - In-app help (`DeckFlow.Web/Help/**/*.md` copied to output via `<Content>` item) and prompt templates (`prompt-templates/deck-comparison/`).
- PowerShell + Bash - Run scripts in `scripts/run-web.ps1` and `scripts/run-web.sh`.

## Runtime

**Environment:**
- .NET 10 ASP.NET Core (Kestrel) - Web host bootstrapped in `DeckFlow.Web/Program.cs`.
- Container base images (production): `mcr.microsoft.com/dotnet/sdk:10.0` (build) and `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime), see `Dockerfile`.
- Node.js 20 (build-time only) - Installed in Docker build stage to compile TypeScript via the `CompileTypeScriptAssets` MSBuild target in `DeckFlow.Web/DeckFlow.Web.csproj`.

**Package Manager:**
- NuGet for .NET dependencies - Restore portable settings in `Directory.Build.props` (clears `RestoreFallbackFolders` to avoid Visual Studio shared cache leakage in WSL).
- npm for TypeScript build tooling - `DeckFlow.Web/package.json`, `package-lock.json` (root + `DeckFlow.Web/`).

## Frameworks

**Core:**
- ASP.NET Core MVC 10.0 - Controllers + Razor views (`Microsoft.NET.Sdk.Web` SDK in `DeckFlow.Web.csproj`).
- Swashbuckle.AspNetCore 7.0.0 - Swagger UI exposed at `/swagger` in Development (registered in `DeckFlow.Web/Program.cs:148-163`).
- Microsoft.AspNetCore.RateLimiting (built-in) - Fixed window rate limiter on feedback submit (5/hr per IP), `DeckFlow.Web/Program.cs:130-146`.
- System.CommandLine 2.0.0-beta4.22272.1 - CLI parsing in `DeckFlow.CLI` (`Program.cs`, `CommandRunners.cs`).

**Testing:**
- xUnit 2.9.3 - Both test projects (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`).
- xunit.runner.visualstudio 3.1.4 - VS test discovery.
- Microsoft.NET.Test.Sdk 17.14.1 - Test SDK.
- RichardSzalay.MockHttp 7.0.0 - HTTP mocking, used in `DeckFlow.Web.Tests` (e.g., `CommanderSpellbookServiceTests`).
- coverlet.collector 6.0.4 - Code coverage in `DeckFlow.Core.Tests`.

**Build/Dev:**
- TypeScript 6.0.2 (npm) plus `Microsoft.TypeScript.MSBuild` 5.2.2 - TS compiles in MSBuild `BeforeTargets="Build"` target in `DeckFlow.Web.csproj`.
- ESLint 10.2.0 (devDependency in `DeckFlow.Web/package.json`) - Not wired into MSBuild.
- MSBuild custom target `ZipDeckFlowBridge` - Zips `browser-extensions/deckflow-bridge/` to `wwwroot/extensions/deckflow-bridge.zip` on every `Build`/`Publish`.

## Key Dependencies

**Critical:**
- RestSharp 114.0.0 - Single HTTP client abstraction for all upstream calls, used by every `DeckFlow.Web/Services/*Service.cs` and `DeckFlow.Core/Integration/*ApiDeckImporter.cs`.
- Polly 8.x - Resilience pipelines registered as named `ResiliencePipeline<RestResponse>` (banlist, spellbook, tagger, tagger-post, scryfall) in `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs`. Services resolve via `ResiliencePipelineProvider<string>`. `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` still uses legacy Polly `AsyncRetryPolicy` directly.
- Markdig 0.38.0 - Help-content Markdown rendering (`HelpContentService.cs`).
- Microsoft.Extensions.Caching.Memory (built-in via `AddMemoryCache()`) - Cache layer for ban list, search results, session cache, etc.

**Infrastructure:**
- Microsoft.Data.Sqlite 10.0.0 - Default storage for `feedback.db` and `category-knowledge.db` under `MTG_DATA_DIR`.
- Npgsql 10.0.0 - Optional Postgres provider (toggled via `DECKFLOW_DATABASE_PROVIDER=Postgres`).
- Serilog.AspNetCore 9.0.0 + Serilog.Sinks.Console 6.0.0 + Serilog.Sinks.File 6.0.0 - Structured logging, configured in `DeckFlow.Web/Program.cs:34-47`. Logs roll daily to `logs/web-.log` (14-file retention).
- Serilog 4.2.0 + Serilog.Sinks.File 7.0.0 - Used directly by `DeckFlow.CLI`.
- Microsoft.Extensions.Logging.Abstractions 10.0.0 - Used in `DeckFlow.Core` (no Serilog dependency in core).

## Configuration

**Environment:**
- Configured via environment variables; no `.env` file present in repo.
- Required for production: `ASPNETCORE_ENVIRONMENT=Production`, `MTG_DATA_DIR=/data`, `PORT` (Render/Fly inject).
- Optional: `DECKFLOW_DATABASE_PROVIDER` (`Sqlite`|`Postgres`), `DECKFLOW_DATABASE_CONNECTION_STRING`, `FEEDBACK_ADMIN_USER`, `FEEDBACK_ADMIN_PASSWORD`, `FEEDBACK_IP_SALT`, `MTGDECKSTUDIO_DISABLE_AUTO_BROWSER`.
- App-level: `DeckFlow.Web/appsettings.json` (logging defaults, allowed hosts) and `appsettings.Development.json` (logging override).
- `DeckFlow.Web/Properties/launchSettings.json` - Local dev URLs `http://localhost:5173` / `https://localhost:7173`.

**Build:**
- `Directory.Build.props` - Clears NuGet fallback folders.
- `DeckFlow.sln` - Solution file referencing all 5 projects.
- `DeckFlow.Web/tsconfig.json` - Strict TS config.
- `Dockerfile` - Multi-stage build (sdk:10.0 -> aspnet:10.0).
- `render.yaml` - Render Blueprint (Docker, starter plan, `/data` disk, `mtg-deck-studio` service name).
- `fly.toml` - Fly.io app `mtg-deck-studio`, Seattle region, shared-cpu-1x/512MB, `/data` mount.

## Platform Requirements

**Development:**
- .NET 10 SDK.
- Node.js (any recent version) + npm install once in `DeckFlow.Web/` to populate `node_modules/typescript` for the MSBuild TypeScript target.
- Cross-platform: WSL2, Linux, and Windows are all first-class targets. `Directory.Build.props` exists specifically because Windows VS shared NuGet cache breaks WSL restores.
- IIS Express + IIS profiles defined for Windows-only Visual Studio runs (`launchSettings.json`).

**Production:**
- Containerized .NET 10 on Linux. Listens on `${PORT:-8080}` over HTTP behind a TLS-terminating reverse proxy (Render or Fly). `UseForwardedHeaders` honors `X-Forwarded-{For,Proto,Host}` so HTTPS redirection and `SameOriginRequestValidator` see the browser's scheme.
- Persistent disk mounted at `/data` (Render `mtg-data` 1 GB; Fly `mtg_data` volume) holds SQLite DBs and ChatGPT artifacts.

---

*Stack analysis: 2026-04-29*
