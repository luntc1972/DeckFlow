# Codebase Structure

**Analysis Date:** 2026-04-29

## Directory Layout

```
decksyncworkbench/
├── DeckFlow.sln                    # Five-project solution
├── Directory.Build.props           # Shared MSBuild props (TFM, nullable, implicit usings)
├── Dockerfile                      # Production container build
├── fly.toml                        # Fly.io deploy config
├── render.yaml                     # Render.com deploy config
├── README.md                       # Project overview / usage doc (kept current with commits)
├── AGENTS.md                       # Auto-memory pointer file
│
├── DeckFlow.Core/                  # Pure-domain class library (net10.0)
│   ├── Models/                     # Records: DeckEntry, DeckDiff, LoadedDecks, MatchMode, etc.
│   ├── Parsing/                    # IParser + MoxfieldParser, ArchidektParser, DeckParseException
│   ├── Diffing/                    # DiffEngine
│   ├── Loading/                    # DeckEntryLoader, DeckLoadRequest, DeckPlatform, DeckInputKind
│   ├── Integration/                # Moxfield/Archidekt API importers, EDHREC lookup
│   ├── Exporting/                  # MoxfieldTextExporter, DeltaExporter, FullImportExporter
│   ├── Filtering/                  # DeckEntryFilter
│   ├── Knowledge/                  # CategoryKnowledgeRepository, ArchidektDeckCacheSession, comparers
│   ├── Normalization/              # CardNormalizer
│   ├── Reporting/                  # Category/reconciliation/inference reporters
│   ├── Storage/                    # IRelationalDialect + Sqlite/Postgres dialects, RelationalDatabaseConnection
│   └── DeckFlow.Core.csproj
│
├── DeckFlow.Core.Tests/            # xUnit tests for DeckFlow.Core
│
├── DeckFlow.Web/                   # ASP.NET Core MVC + Web API host (net10.0)
│   ├── Program.cs                  # Composition root, middleware, DI, hosted services
│   ├── Controllers/
│   │   ├── DeckController.cs       # All deck-tool MVC pages
│   │   ├── CommanderController.cs
│   │   ├── HelpController.cs
│   │   ├── FeedbackController.cs
│   │   ├── AboutController.cs
│   │   ├── Api/                    # JSON API controllers (always [ApiController])
│   │   │   ├── DeckSyncApiController.cs
│   │   │   ├── SuggestionsApiController.cs
│   │   │   └── ArchidektCacheJobsController.cs
│   │   └── Admin/                  # BasicAuth-gated admin
│   │       └── AdminFeedbackController.cs
│   ├── Services/                   # Application + adapter services (~30 files)
│   │   ├── *.cs                    # Concrete services + their interfaces (often co-located)
│   │   ├── I*Service.cs            # Where extracted to separate file
│   │   └── Http/                   # HTTP infrastructure
│   │       ├── ResiliencePipelineFactory.cs
│   │       ├── NullHttpClientFactory.cs
│   │       └── NullScryfallRestClientFactory.cs
│   ├── Models/                     # View models + request DTOs
│   │   └── Api/                    # JSON request/response DTOs
│   │       ├── DeckSyncApiRequest.cs
│   │       ├── DeckSyncApiResponse.cs
│   │       └── SuggestionResponses.cs
│   ├── Views/                      # Razor views, one folder per controller
│   │   ├── Deck/                   # CardLookup, DeckSync, DeckConvert, ChatGpt*, MechanicLookup,
│   │   │                           # JudgeQuestions, SuggestCategories, Home, ChatGptCedhMetaGap
│   │   ├── Commander/              # CommanderCategories.cshtml
│   │   ├── Help/                   # Index.cshtml, Topic.cshtml
│   │   ├── About/
│   │   ├── Feedback/
│   │   ├── Admin/Feedback/
│   │   ├── Shared/                 # _Layout, _WorkflowStepTabs, _DeckToolTabs, _BusyIndicator,
│   │   │                           # _FormError, _MoxfieldBulkEditHint, _DeckFlowBridgeHint
│   │   ├── _ViewImports.cshtml
│   │   └── _ViewStart.cshtml
│   ├── Infrastructure/             # Cross-cutting middleware
│   │   ├── BasicAuthMiddleware.cs
│   │   ├── DevelopmentBrowserLauncher.cs
│   │   └── SecurityHeadersApplicationBuilderExtensions.cs
│   ├── Security/                   # SameOriginRequestValidator.cs
│   ├── Help/                       # Markdown help topics, copied to output dir
│   │   └── *.md                    # Rendered by HelpController via Markdig
│   ├── Properties/
│   │   ├── launchSettings.json
│   │   └── PublishProfiles/
│   └── wwwroot/                    # Static assets
│       ├── css/
│       │   ├── site.css            # Base theme (planeswalker-dark default)
│       │   ├── site-common.css     # Shared cross-theme layout (extends every theme)
│       │   ├── site-mobile.css
│       │   ├── site-commander-table.css
│       │   ├── site-<guild>.css    # 22 guild themes (abzan, azorius, …, simic, sultai, temur)
│       │   └── site-planeswalker-dark.css, site-nyx.css
│       ├── ts/                     # TypeScript source (compiled at build)
│       │   ├── site.ts             # Shared bootstrap
│       │   ├── deck-sync.ts, card-lookup.ts, card-search.ts, commander-search.ts,
│       │   ├── category-suggestions.ts, judge-questions.ts, mechanic-lookup.ts
│       │   └── df-select.ts, df-typeahead.ts        # Reusable widgets
│       ├── js/                     # tsc output (one .js per .ts) — checked in
│       ├── lib/                    # Vendored client libs
│       └── extensions/
│           └── deckflow-bridge.zip # Auto-generated by ZipDeckFlowBridge target
│
├── DeckFlow.Web.Tests/             # xUnit + integration tests for the web project
│   ├── *Tests.cs                   # Per-target tests (Controller / Service / Middleware)
│   ├── Services/                   # Sub-grouped service tests (Spellbook, Tagger)
│   └── TestDoubles/
│       ├── FakeHttpClientFactory.cs
│       ├── FakeResiliencePipelineProvider.cs
│       ├── FakeScryfallRestClientFactory.cs
│       ├── FakeCategoryKnowledgeStore.cs
│       └── StubHttpMessageHandler.cs
│
├── DeckFlow.CLI/                   # System.CommandLine console host
│   ├── Program.cs
│   └── CommandRunners.cs
│
├── browser-extensions/
│   └── deckflow-bridge/            # Source for the companion browser extension
│
├── docs/
│   └── superpowers/
│       ├── plans/
│       └── specs/
│
├── prompt-templates/
│   └── deck-comparison/            # Stored ChatGPT prompt scaffolds
│
├── scripts/
│   ├── run-web.ps1
│   └── run-web.sh
│
├── tasks/                          # Per-session todo / lessons logs
├── logs/                           # Runtime Serilog output (gitignored data)
├── artifacts/                      # Generated artifacts staging
├── .planning/                      # GSD command working dir (this file lives under codebase/)
└── .github/                        # Workflows + community files
```

## Directory Purposes

**`DeckFlow.Core/`:**
- Purpose: Domain library — deck data model, parsing, diff, normalization, reporting, knowledge cache, storage dialect.
- Contains: `.cs` files grouped by responsibility folder; no controllers/views.
- Key files: `Diffing/DiffEngine.cs`, `Parsing/MoxfieldParser.cs`, `Parsing/ArchidektParser.cs`, `Loading/DeckEntryLoader.cs`, `Storage/RelationalDatabaseConnection.cs`, `Knowledge/CategoryKnowledgeRepository.cs`.

**`DeckFlow.Web/Controllers/`:**
- Purpose: HTTP entry points.
- Contains: One controller per Razor feature area at the root, JSON APIs in `Api/`, BasicAuth-gated controllers in `Admin/`.
- Key files: `DeckController.cs` (largest, all deck tools), `Api/DeckSyncApiController.cs`, `Admin/AdminFeedbackController.cs`.

**`DeckFlow.Web/Services/`:**
- Purpose: Application logic, external HTTP adapters, persistence stores, ChatGPT packet builders.
- Contains: Concrete services (often with their interface declared in the same file) and a sub-folder `Http/` for HTTP infrastructure.
- Key files: `DeckSyncService.cs`, `CategorySuggestionService.cs`, `ScryfallTaggerService.cs`, `TaggerSessionCache.cs`, `ScryfallThrottle.cs`, `Http/ResiliencePipelineFactory.cs`, `FeedbackStore.cs`, `CategoryKnowledgeStore.cs`, `ArchidektCacheJobService.cs`.

**`DeckFlow.Web/Models/`:**
- Purpose: View models and request/response DTOs.
- Contains: `*ViewModel`, `*Request`, enum types like `DeckPageTab`, `CedhMetaSortBy`. JSON DTOs live under `Api/`.

**`DeckFlow.Web/Views/`:**
- Purpose: Razor templates.
- Contains: Folder per controller, plus `Shared/` partials and `_Layout.cshtml`.

**`DeckFlow.Web/Infrastructure/`:**
- Purpose: Middleware and dev-only helpers.
- Key files: `BasicAuthMiddleware.cs`, `SecurityHeadersApplicationBuilderExtensions.cs`, `DevelopmentBrowserLauncher.cs`.

**`DeckFlow.Web/Security/`:**
- Purpose: Security primitives consumed by controllers.
- Key files: `SameOriginRequestValidator.cs`.

**`DeckFlow.Web/Help/`:**
- Purpose: Markdown source for the in-app `/help` topics.
- Behavior: Each `*.md` is rendered by `HelpContentService` via Markdig; copied to output dir on build.

**`DeckFlow.Web/wwwroot/`:**
- Purpose: Static web assets served directly.
- Notable: `css/` contains 22 fully-forked guild themes plus a base theme; `ts/` holds source, `js/` holds checked-in compiled output; `extensions/deckflow-bridge.zip` is generated by an MSBuild target.

**`DeckFlow.Web.Tests/` and `DeckFlow.Core.Tests/`:**
- Purpose: xUnit test projects (one per production project).
- `DeckFlow.Web.Tests/TestDoubles/` is the canonical place for fakes/stubs shared across tests.

**`DeckFlow.CLI/`:**
- Purpose: Headless command runner using `System.CommandLine`.
- Files: `Program.cs` wires commands; `CommandRunners.cs` holds the per-command handlers.

**`browser-extensions/deckflow-bridge/`:**
- Purpose: Companion browser extension source.
- Behavior: Zipped automatically into `DeckFlow.Web/wwwroot/extensions/deckflow-bridge.zip` during web build.

**`docs/superpowers/`:**
- Purpose: Spec/plan documents authored during planning sessions.

**`prompt-templates/deck-comparison/`:**
- Purpose: Static prompt scaffolds used by ChatGPT packet/comparison services.

**`tasks/`:**
- Purpose: Session todo lists (`todo.md`) and accumulated `lessons.md` per workflow rules.

**`scripts/`:**
- Purpose: Wrappers for `dotnet run --project DeckFlow.Web` (PowerShell + bash).

## Key File Locations

**Entry Points:**
- `DeckFlow.Web/Program.cs` — Web host, DI, middleware.
- `DeckFlow.CLI/Program.cs` — CLI commands.

**Configuration:**
- `Directory.Build.props` — solution-wide MSBuild props.
- `DeckFlow.Web/appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json` — runtime config.
- `DeckFlow.Web/Properties/launchSettings.json` — Dev launch profiles.
- `DeckFlow.Web/tsconfig.json` — TypeScript build.
- `Dockerfile`, `fly.toml`, `render.yaml`, `.dockerignore` — deployment.

**Core Logic:**
- `DeckFlow.Core/Diffing/DiffEngine.cs` — deck comparison.
- `DeckFlow.Core/Parsing/MoxfieldParser.cs`, `ArchidektParser.cs` — text → entries.
- `DeckFlow.Core/Loading/DeckEntryLoader.cs` — orchestrates parse/import + Commander size validation.
- `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs`, `ArchidektApiDeckImporter.cs` — remote fetch.
- `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` — DB connection abstraction.

**HTTP Infrastructure:**
- `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` — registers all five Polly pipelines.
- `DeckFlow.Web/Services/ScryfallRestClientFactory.cs` — RestSharp adapter over named `IHttpClientFactory` clients.
- `DeckFlow.Web/Services/ScryfallTaggerHttpClient.cs` — typed cookie-disabled client.
- `DeckFlow.Web/Services/ScryfallThrottle.cs` — static Scryfall concurrency gate.

**Security:**
- `DeckFlow.Web/Security/SameOriginRequestValidator.cs` — CSRF guard.
- `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` — admin gate.
- `DeckFlow.Web/Infrastructure/SecurityHeadersApplicationBuilderExtensions.cs` — CSP + headers.

**Persistence:**
- `DeckFlow.Web/Services/FeedbackStore.cs`
- `DeckFlow.Web/Services/CategoryKnowledgeStore.cs`
- `DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs`

**Testing Doubles:**
- `DeckFlow.Web.Tests/TestDoubles/FakeHttpClientFactory.cs`
- `DeckFlow.Web.Tests/TestDoubles/FakeResiliencePipelineProvider.cs`
- `DeckFlow.Web.Tests/TestDoubles/StubHttpMessageHandler.cs`

## Naming Conventions

**Files:**
- C# files: `PascalCase.cs`, one public type per file, file name matches type (e.g., `DeckSyncService.cs`).
- Interface + implementation often co-located (`I<Name>` interface declared in the same file as `<Name>` class), e.g., `IDeckSyncService` lives at the top of `DeckSyncService.cs`. Standalone interface files exist where the interface predates implementations or has multiple impls (`ICategoryKnowledgeStore.cs`, `IFeedbackStore.cs`, `IHelpContentService.cs`, `IVersionService.cs`).
- Razor views: `PascalCase.cshtml`; partials/layouts prefixed with underscore (`_Layout.cshtml`, `_WorkflowStepTabs.cshtml`).
- TypeScript: `kebab-case.ts` (`deck-sync.ts`, `df-select.ts`); compiled `.js` mirrors the source name.
- CSS themes: `site-<theme>.css` (kebab-case theme name, all under `wwwroot/css/`).
- Markdown help topics: `kebab-case.md` under `DeckFlow.Web/Help/`.
- Test files: `<TargetType>Tests.cs` (e.g., `DeckSyncServiceTests.cs`, `DeckSyncApiControllerTests.cs`).
- Test doubles: `Fake<X>` for stand-in services, `Stub<X>` for low-level handlers (`FakeHttpClientFactory`, `StubHttpMessageHandler`).

**Directories:**
- Project folders: `DeckFlow.<Concern>` (`DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.CLI`, `DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`).
- Feature subfolders inside a project: PascalCase (`Controllers/Api/`, `Services/Http/`, `Models/Api/`).
- View subfolders: PascalCase matching controller name (`Views/Deck/`, `Views/Admin/Feedback/`).
- Static assets: lowercase (`wwwroot/css/`, `wwwroot/ts/`, `wwwroot/js/`, `wwwroot/lib/`).

**Types and members:**
- Public types/methods: `PascalCase`.
- Local variables, parameters: `camelCase`.
- Private fields: `_camelCase` underscore-prefixed (visible throughout `DeckController`, `DeckSyncService`, etc.).
- Interfaces: `IName` prefix.
- Async methods: `*Async` suffix (e.g., `CompareDecksAsync`, `LoadAsync`).
- Cancellation token: always trailing parameter named `cancellationToken`.

**Service registration names (for `IHttpClientFactory` and Polly):**
- Lowercase, hyphenated: `commander-banlist`, `commander-spellbook`, `scryfall-rest`, plus pipeline names `banlist`, `spellbook`, `tagger`, `tagger-post`, `scryfall`. Tagger uses a typed client (`ScryfallTaggerHttpClient`) instead of a named client.

## Where to Add New Code

**New Razor MVC tool / page:**
- Controller action: extend `DeckFlow.Web/Controllers/DeckController.cs` (or add a new `*Controller.cs` if conceptually separate, e.g., new top-level domain).
- View: `DeckFlow.Web/Views/Deck/<NewView>.cshtml` (or matching controller folder).
- View model: `DeckFlow.Web/Models/<NewView>ViewModel.cs`.
- Help topic: `DeckFlow.Web/Help/<slug>.md` and link from `HelpController` mapping.
- Workflow tabs entry: extend `DeckPageTab.cs` and `WorkflowStepTabsModel.cs`.
- Theming: any new layout/structural CSS goes into `wwwroot/css/site-common.css`, theme accent overrides into `site-<guild>.css`. Add a `--panel`/`--line` panel wrapper and `--accent-strong` link color for new pages so themes look right.

**New JSON API endpoint:**
- Controller: `DeckFlow.Web/Controllers/Api/<Feature>ApiController.cs`, `[ApiController]`, `[Route("api/<feature>")]`.
- Request/response DTO: `DeckFlow.Web/Models/Api/<Feature>ApiRequest.cs` / `Response.cs`.
- First line of every action: `if (!SameOriginRequestValidator.IsValid(Request)) return StatusCode(403, ...)`.

**New service:**
- Implementation + interface: `DeckFlow.Web/Services/<Name>Service.cs` (interface at top of same file, unless multiple implementations are expected).
- Registration: add to `Program.cs` (`builder.Services.AddSingleton/Scoped<I<Name>Service, <Name>Service>()`) — keep alphabetical-ish grouping near similar services.

**New external HTTP adapter:**
- Add `builder.Services.AddHttpClient("<service-name>", c => { c.BaseAddress = …; c.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow/1.0"); });` in `Program.cs`.
- Add a Polly pipeline registration in `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` (`AddDeckFlowResiliencePipelines`).
- Service constructor receives `IHttpClientFactory` + `ResiliencePipelineProvider<string>` (and `ILogger<T>`); wraps the named client with RestSharp `RestClient` and resolves pipeline by name. Mirror `CommanderBanListService.cs` or `CommanderSpellbookService.cs` shape.
- For Scryfall calls, route through `IScryfallRestClientFactory` and gate on `ScryfallThrottle`.

**New domain logic (parsers, exporters, diff helpers):**
- Goes in `DeckFlow.Core/<Concern>/`. Do not depend on ASP.NET or `HttpClient` from `DeckFlow.Core` (importers in `Core/Integration/` are the explicit exception that uses RestSharp + Polly via the package references in `DeckFlow.Core.csproj`).

**New persistence:**
- Add a method to `IFeedbackStore`/`ICategoryKnowledgeStore` (or a new store) and implement against `RelationalDatabaseConnection`. Provide both Sqlite + Postgres SQL where dialects differ via `IRelationalDialect`.

**New TypeScript module:**
- Source: `DeckFlow.Web/wwwroot/ts/<module>.ts`. The MSBuild `CompileTypeScriptAssets` target compiles all of `wwwroot/ts/**/*.ts` to `wwwroot/js/`.
- Reference compiled `.js` via `<script src="~/js/<module>.js" asp-append-version="true"></script>` in the relevant Razor view.

**New tests:**
- Unit/service tests: `DeckFlow.Core.Tests/` (for core) or `DeckFlow.Web.Tests/` (for web).
- Reuse `TestDoubles/StubHttpMessageHandler`, `FakeHttpClientFactory`, `FakeResiliencePipelineProvider` for HTTP-level fakes.
- Service-grouped tests can sit under `DeckFlow.Web.Tests/Services/<Group>/`.

**New help topic:**
- Markdown: `DeckFlow.Web/Help/<slug>.md` (auto-copied to output via `<Content>` glob).
- Mapping: register in `IHelpContentService`/`HelpContentService.cs`.

**Utilities:**
- Cross-cutting middleware → `DeckFlow.Web/Infrastructure/`.
- Security primitives → `DeckFlow.Web/Security/`.
- Static helpers used by multiple services → keep in `DeckFlow.Web/Services/` next to a related service (no separate `Utils/` folder convention exists yet).

## Special Directories

**`DeckFlow.Web/wwwroot/js/`:**
- Purpose: Compiled TypeScript output.
- Generated: Yes — by `CompileTypeScriptAssets` MSBuild target.
- Committed: Yes (avoids requiring node on every build host).

**`DeckFlow.Web/wwwroot/extensions/`:**
- Purpose: Bundled DeckFlow Bridge extension zip.
- Generated: Yes — by `ZipDeckFlowBridge` MSBuild target on every build/publish.
- Committed: `.zip` file is build output; check `.gitignore` before committing.

**`DeckFlow.Web/Help/`:**
- Purpose: Source markdown for in-app help.
- Generated: No.
- Committed: Yes; copied to output dir at build (`PreserveNewest`).

**`logs/`:**
- Purpose: Serilog rolling daily log files (web + CLI).
- Generated: Yes (runtime).
- Committed: No — runtime data only.

**`artifacts/`:**
- Purpose: Build / ChatGPT artifact staging.
- Generated: Yes.
- Committed: No.

**`.planning/codebase/`:**
- Purpose: GSD codebase mapping documents (this directory).
- Generated: By `/gsd-map-codebase`.
- Committed: Yes — read by other GSD commands.

**`tasks/`:**
- Purpose: Session-scoped todo lists and accumulated lessons.
- Generated: By Claude during workflow.
- Committed: Yes per project rules.

**`bin/`, `obj/`:**
- Purpose: .NET build output.
- Generated: Yes.
- Committed: No.

---

*Structure analysis: 2026-04-29*
