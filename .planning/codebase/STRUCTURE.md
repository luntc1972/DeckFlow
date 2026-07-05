# Codebase Structure

**Analysis Date:** 2026-05-29

## Directory Layout

```
deckflow/
├── DeckFlow.sln                       # Solution file (5 projects)
├── Directory.Build.props              # Shared .NET build props (clears NuGet fallback)
├── Dockerfile                         # Multi-stage ASP.NET 10 container
├── render.yaml                        # Render Blueprint (Docker, starter plan)
├── CLAUDE.md                          # Project conventions & constraints
├── README.md                          # Public documentation
│
├── DeckFlow.Web/                      # Main ASP.NET 10 MVC app (entry point for web)
│   ├── DeckFlow.Web.csproj            # Runs TypeScript compiler + extension zip on build
│   ├── Program.cs                     # DI wiring, middleware pipeline, startup validation
│   ├── appsettings.json               # Logging defaults, allowed hosts
│   ├── appsettings.Development.json   # Dev logging override
│   ├── tsconfig.json                  # Strict TypeScript config (ES2017 target)
│   │
│   ├── Controllers/                   # HTTP entry points (MVC + JSON API)
│   │   ├── DeckController.cs          # Deck workflows (sync, convert, lookup, analysis)
│   │   ├── CommanderController.cs     # Commander categories page
│   │   ├── FeedbackController.cs      # Feedback form submission
│   │   ├── HelpController.cs          # Markdown help topics
│   │   ├── AboutController.cs         # Credits/version page
│   │   ├── Api/                       # JSON endpoints
│   │   │   ├── DeckSyncApiController.cs     # Deck diff JSON
│   │   │   ├── SuggestionsApiController.cs  # Category suggestions JSON
│   │   │   └── ArchidektCacheJobsController.cs  # Job control
│   │   └── Admin/                     # BasicAuth-gated admin shell
│   │       ├── AdminLandingController.cs    # /Admin dashboard
│   │       ├── AdminFeedbackController.cs   # Feedback console
│   │       ├── AdminAnalyticsController.cs  # Analytics dashboard
│   │       ├── AdminHarvestController.cs    # Harvest job monitoring
│   │       └── AdminFlagsController.cs      # Feature flag toggles
│   │
│   ├── Services/                      # Application logic (30+ services)
│   │   ├── CardLookupService.cs       # Scryfall card lookup
│   │   ├── CardSearchService.cs       # Scryfall fuzzy search
│   │   ├── CommanderSearchService.cs  # Commander-legal search
│   │   ├── ScryfallSetService.cs      # Set data + mechanics
│   │   ├── CommanderSpellbookService.cs   # Combo data
│   │   ├── CommanderBanListService.cs    # Ban list fetch
│   │   ├── ScryfallTaggerService.cs   # Tagger.scryfall.com + CSRF cache
│   │   ├── ScryfallTaggerLookupService.cs # Tagger async wrapper
│   │   ├── ScryfallTaggerHttpClient.cs    # Typed HTTP client
│   │   ├── EdhTop16Client.cs          # Meta tier lists
│   │   ├── DeckAnalysisPacketService.cs   # Analysis prompt build
│   │   ├── DeckComparisonService.cs   # Comparison prompt build
│   │   ├── MetaGapService.cs          # Meta gap analysis
│   │   ├── DeckSyncService.cs         # Deck sync orchestration
│   │   ├── DeckConvertService.cs      # Format conversion
│   │   ├── CategorySuggestionService.cs   # Mode-routed suggestions
│   │   ├── CommanderCategoryService.cs    # Commander categories
│   │   ├── CategoryKnowledgeStore.cs  # Category knowledge persistence
│   │   ├── FeedbackStore.cs           # Feedback persistence
│   │   ├── AdBruteForceTrackerStore.cs   # Admin login throttle
│   │   ├── HelpContentService.cs      # Markdown rendering
│   │   ├── VersionService.cs          # Version info
│   │   ├── ArchidektCacheJobService.cs   # Hosted background service
│   │   │
│   │   ├── PromptBuilders/            # Prompt variant strategies (AiPlatform)
│   │   │   ├── Analysis/
│   │   │   │   ├── IAnalysisPromptVariant.cs
│   │   │   │   ├── ChatGptAnalysisPromptVariant.cs
│   │   │   │   ├── ClaudeAnalysisPromptVariant.cs
│   │   │   │   ├── GeminiAnalysisPromptVariant.cs
│   │   │   │   └── AnalysisPromptVariantRegistry.cs
│   │   │   ├── Comparison/
│   │   │   │   ├── IComparisonPromptVariant.cs
│   │   │   │   ├── ChatGptComparisonPromptVariant.cs
│   │   │   │   ├── ClaudeComparisonPromptVariant.cs
│   │   │   │   ├── GeminiComparisonPromptVariant.cs
│   │   │   │   └── ComparisonPromptVariantRegistry.cs
│   │   │   ├── FollowUp/
│   │   │   │   ├── IFollowUpPromptVariant.cs
│   │   │   │   ├── ChatGptFollowUpPromptVariant.cs
│   │   │   │   ├── ClaudeFollowUpPromptVariant.cs
│   │   │   │   ├── GeminiFollowUpPromptVariant.cs
│   │   │   │   └── FollowUpPromptVariantRegistry.cs
│   │   │   ├── SetUpgrade/
│   │   │   │   ├── ISetUpgradePromptVariant.cs
│   │   │   │   ├── ChatGptSetUpgradePromptVariant.cs
│   │   │   │   ├── ClaudeSetUpgradePromptVariant.cs
│   │   │   │   ├── GeminiSetUpgradePromptVariant.cs
│   │   │   │   └── SetUpgradePromptVariantRegistry.cs
│   │   │   └── MetaGap/
│   │   │       ├── IMetaGapPromptVariant.cs
│   │   │       ├── ChatGptMetaGapPromptVariant.cs
│   │   │       ├── ClaudeMetaGapPromptVariant.cs
│   │   │       ├── GeminiMetaGapPromptVariant.cs
│   │   │       └── MetaGapPromptVariantRegistry.cs
│   │   │
│   │   ├── Http/                      # HTTP infrastructure
│   │   │   ├── ResiliencePipelineFactory.cs  # Polly v8 pipelines
│   │   │   └── ScryfallRestClientFactory.cs  # RestSharp client builder
│   │   │
│   │   ├── Harvest/                   # Content KB harvest scheduling (v1.4)
│   │   │   ├── HarvestScheduleService.cs
│   │   │   ├── HarvestRunStore.cs
│   │   │   ├── HarvestScheduleStore.cs
│   │   │   ├── HarvestScheduleCache.cs
│   │   │   ├── HarvestStatsAggregator.cs
│   │   │   └── IHarvestRunStore.cs, IHarvestScheduleStore.cs, etc.
│   │   │
│   │   ├── Content/                   # Content KB stores (v1.4)
│   │   │   └── (forwarding to DeckFlow.Core/Content/)
│   │   │
│   │   ├── Analytics/                 # Request metrics (v1.4)
│   │   │   ├── RequestMetricsStore.cs
│   │   │   ├── RequestMetricsFlusher.cs
│   │   │   ├── RequestMetricsBuffer.cs
│   │   │   ├── AnalyticsSaltAccessor.cs
│   │   │   └── IRequestMetricsStore.cs
│   │   │
│   │   └── FeatureFlags/              # Feature flag services
│   │       └── DeckFlowFeatureFlagExtensions.cs
│   │
│   ├── Infrastructure/                # Middleware & cross-cutting concerns
│   │   ├── BasicAuthMiddleware.cs     # HTTP Basic auth for /Admin
│   │   ├── AnalyticsMiddleware.cs     # Request metrics collection
│   │   ├── AnalyticsApplicationBuilderExtensions.cs
│   │   ├── SecurityHeadersApplicationBuilderExtensions.cs  # CSP, X-Frame, etc.
│   │   ├── DevelopmentBrowserLauncher.cs
│   │   └── FeatureFlagGateAttribute.cs
│   │
│   ├── Security/                      # CSRF & authentication
│   │   ├── SameOriginRequestValidator.cs
│   │   └── IpHasher.cs
│   │
│   ├── Configuration/                 # Options & setup
│   │   ├── AiPlatformOptions.cs       # Gemini enable/disable toggle
│   │   └── DeckFlowDatabaseConnectionFactory.cs
│   │
│   ├── Extensions/                    # DI & service extensions
│   │   ├── DeckFlowHarvestExtensions.cs
│   │   ├── DeckFlowAnalyticsExtensions.cs
│   │   └── (other helper extensions)
│   │
│   ├── Models/                        # View models & DTOs
│   │   ├── AiPlatform.cs              # AI platform discriminator
│   │   ├── DeckAnalysisRequest.cs
│   │   ├── DeckSyncRequest.cs
│   │   ├── DeckSyncResponse.cs
│   │   ├── Api/                       # JSON response DTOs
│   │   │   ├── DeckSyncResponse.cs
│   │   │   ├── SuggestionResponse.cs
│   │   │   └── (other API models)
│   │   └── (20+ view models)
│   │
│   ├── Views/                         # Razor templates (one folder per controller)
│   │   ├── Shared/
│   │   │   ├── _Layout.cshtml         # Master layout
│   │   │   ├── _ViewImports.cshtml
│   │   │   ├── _ViewStart.cshtml
│   │   │   ├── _WorkflowStepTabs.cshtml  # Shared navigation bar
│   │   │   └── (error pages, admin common, etc.)
│   │   ├── Deck/
│   │   │   ├── Index.cshtml           # Deck sync page
│   │   │   ├── Convert.cshtml
│   │   │   ├── Lookup.cshtml
│   │   │   ├── DeckAnalysis.cshtml
│   │   │   ├── DeckComparison.cshtml
│   │   │   ├── MechanicLookup.cshtml
│   │   │   ├── JudgeQuestions.cshtml
│   │   │   ├── SetUpgrade.cshtml
│   │   │   └── (more deck views)
│   │   ├── Commander/
│   │   │   └── CommanderCategories.cshtml
│   │   ├── Feedback/
│   │   │   └── Index.cshtml
│   │   ├── Help/
│   │   │   └── Index.cshtml
│   │   ├── About/
│   │   │   └── Index.cshtml
│   │   ├── Admin/
│   │   │   └── (Landing.cshtml — legacy placeholder)
│   │   ├── AdminFeedback/
│   │   │   ├── Index.cshtml
│   │   │   ├── _FeedbackRow.cshtml
│   │   │   └── (admin feedback partials)
│   │   ├── AdminAnalytics/
│   │   │   └── Index.cshtml
│   │   ├── AdminHarvest/
│   │   │   └── Index.cshtml
│   │   ├── AdminFlags/
│   │   │   └── Index.cshtml
│   │   └── AdminLanding/
│   │       └── Index.cshtml
│   │
│   ├── wwwroot/                       # Static assets & compiled TypeScript
│   │   ├── css/
│   │   │   ├── site-common.css        # Shared layout CSS (MUST stay here)
│   │   │   ├── site.css               # Default theme tokens
│   │   │   ├── site-guild-azorius.css # Guild themes (token overrides)
│   │   │   ├── site-guild-dimir.css
│   │   │   ├── site-guild-rakdos.css
│   │   │   ├── site-guild-gruul.css
│   │   │   ├── site-guild-selesnya.css
│   │   │   ├── site-guild-orzhov.css
│   │   │   ├── site-guild-izzet.css
│   │   │   ├── site-guild-golgari.css
│   │   │   ├── site-guild-simic.css
│   │   │   ├── site-guild-boros.css
│   │   │   ├── admin.css              # Admin theme base
│   │   │   ├── admin-common.css       # Shared admin layout
│   │   │   ├── admin-mobile.css       # Admin responsive
│   │   │   └── (legacy guild files)
│   │   ├── js/                        # Compiled TypeScript output (git-tracked)
│   │   │   ├── site.js
│   │   │   ├── deck-sync.js
│   │   │   ├── card-lookup.js
│   │   │   ├── category-suggestions.js
│   │   │   ├── admin-feedback.js
│   │   │   ├── admin-analytics.js
│   │   │   ├── admin-harvest.js
│   │   │   └── (more compiled bundles)
│   │   ├── ts/                        # TypeScript source (compiles to js/)
│   │   │   ├── site.ts
│   │   │   ├── deck-sync.ts
│   │   │   ├── card-lookup.ts
│   │   │   ├── card-search.ts
│   │   │   ├── category-suggestions.ts
│   │   │   ├── commander-search.ts
│   │   │   ├── judge-questions.ts
│   │   │   ├── feedback.ts
│   │   │   ├── admin-feedback.ts
│   │   │   ├── admin-analytics.ts
│   │   │   ├── admin-harvest.ts
│   │   │   ├── admin-modal.ts
│   │   │   ├── df-select.ts           # Shared typeahead component
│   │   │   └── df-typeahead.ts
│   │   ├── lib/                       # Third-party libs (if any)
│   │   ├── extensions/
│   │   │   └── deckflow-bridge.zip    # Browser extension (auto-zipped from root)
│   │   └── (images, fonts, etc.)
│   │
│   ├── Help/                          # Markdown help content (copied to output)
│   │   ├── deck-sync.md
│   │   ├── deck-analysis.md
│   │   ├── deck-conversion.md
│   │   └── (more help topics)
│   │
│   ├── Properties/
│   │   ├── launchSettings.json        # Local dev URLs (5173 HTTP, 7173 HTTPS)
│   │   └── AssemblyInfo.cs            # InternalsVisibleTo for tests
│   │
│   ├── logs/                          # Daily rolling log files (gitignored)
│   │   └── web-YYYY-MM-DD.log
│   │
│   └── bin/, obj/                     # Build outputs (gitignored)
│
├── DeckFlow.Core/                     # Domain logic (zero I/O framework deps)
│   ├── DeckFlow.Core.csproj
│   │
│   ├── Models/                        # Deck primitives & records
│   │   ├── DeckEntry.cs               # Card + quantity + properties
│   │   ├── DeckDiff.cs                # Two-way diff result
│   │   ├── LoadedDecks.cs             # Parsed + validated decks
│   │   ├── MatchMode.cs               # Enum: Strict, Loose
│   │   ├── SyncDirection.cs           # Enum: MoxToArch, ArchToMox, BiDir
│   │   ├── PrintingChoice.cs          # Enum: FirstNonBasic, Latest, Oldest
│   │   ├── PrintingConflict.cs        # Printing resolution result
│   │   └── (other models)
│   │
│   ├── Parsing/                       # Deck text → DeckEntry
│   │   ├── IParser.cs                 # Interface
│   │   ├── MoxfieldParser.cs
│   │   ├── ArchidektParser.cs
│   │   └── DeckParseException.cs
│   │
│   ├── Loading/                       # Fetch decks (APIs + parsers)
│   │   ├── IDeckEntryLoader.cs
│   │   ├── DeckEntryLoader.cs
│   │   ├── IMoxfieldDeckImporter.cs
│   │   ├── IArchidektDeckImporter.cs
│   │   └── (implementation classes)
│   │
│   ├── Integration/                   # HTTP-based importers (RestSharp + Polly)
│   │   ├── MoxfieldApiDeckImporter.cs
│   │   ├── ArchidektApiDeckImporter.cs
│   │   ├── MoxfieldApiUrl.cs          # URL builder
│   │   ├── ArchidektApiUrl.cs
│   │   └── (API adapters)
│   │
│   ├── Diffing/                       # Deck reconciliation
│   │   ├── DiffEngine.cs              # Card-by-card compare
│   │   ├── ConflictResolution.cs      # Conflict strategies
│   │   └── (diff helpers)
│   │
│   ├── Exporting/                     # DeckEntry → Moxfield/Archidekt text
│   │   ├── MoxfieldExporter.cs
│   │   ├── ArchidektExporter.cs
│   │   └── IExporter.cs
│   │
│   ├── Filtering/                     # Card filtering & search
│   │   └── (filter implementations)
│   │
│   ├── Normalization/                 # Card name canonicalization
│   │   ├── CardNormalizer.cs          # Scryfall-based normalization
│   │   └── (name helpers)
│   │
│   ├── Reporting/                     # Prompt artifact generation
│   │   ├── DeckReporter.cs            # Multi-platform reporting
│   │   ├── PromptTemplateLoader.cs    # Loads prompt-templates/
│   │   └── (reporting helpers)
│   │
│   ├── Knowledge/                     # Category knowledge & artifacts (v1.4)
│   │   ├── CategoryKnowledgeRepository.cs
│   │   ├── DeckCategoryCacheWriter.cs # Writes deck→categories
│   │   ├── ArchidektDeckCacheSession.cs
│   │   ├── ContentArtifactSpec.cs     # Front-matter contract
│   │   ├── ContentArtifactWriter.cs   # Writes markdown artifacts
│   │   ├── ContentArtifactMetadata.cs
│   │   ├── ContentSiteIndexRow.cs
│   │   ├── ContentModels.cs           # Domain models
│   │   ├── ContentSpendModels.cs      # LLM/Whisper spend
│   │   ├── ContentTagVocabulary.cs    # Tag allowlists
│   │   ├── DistillationResults.cs     # Distill output contract
│   │   ├── DistillationSchemas.cs     # JSON schemas for AI
│   │   ├── CardBoardComparer.cs
│   │   ├── BoardCategoryComparer.cs
│   │   └── (knowledge helpers)
│   │
│   ├── Content/                       # Content KB stores (v1.4)
│   │   ├── IContentSourceStore.cs     # Interface
│   │   ├── ContentSourceStore.cs      # YouTube/podcast sources
│   │   ├── IContentVideoStore.cs
│   │   ├── ContentVideoStore.cs       # Videos + transcripts
│   │   ├── IContentHarvestRunStore.cs
│   │   ├── ContentHarvestRunStore.cs  # Harvest run records
│   │   ├── IContentSiteIndexStore.cs
│   │   ├── ContentSiteIndexStore.cs   # Slim browse/filter index
│   │   ├── ILlmSpendLedger.cs
│   │   ├── LlmSpendLedger.cs          # OpenAI token tracking
│   │   ├── IWhisperSpendLedger.cs
│   │   ├── WhisperSpendLedger.cs      # Fallback transcription tracking
│   │   ├── ContentStoreGeneratedId.cs
│   │   └── SlugifySourceName.cs
│   │
│   ├── Storage/                       # Pluggable SQL dialect
│   │   ├── IRelationalDialect.cs      # Interface
│   │   ├── SqliteRelationalDialect.cs
│   │   ├── PostgresRelationalDialect.cs
│   │   └── RelationalDatabaseConnection.cs
│   │
│   └── bin/, obj/                     # Build outputs
│
├── DeckFlow.CLI/                      # System.CommandLine host
│   ├── DeckFlow.CLI.csproj
│   ├── Program.cs                     # Command tree + logging setup
│   └── CommandRunners.cs              # Verb handlers (compare, harvest, distill, etc.)
│
├── DeckFlow.Web.Tests/                # Web layer tests (xUnit)
│   ├── DeckFlow.Web.Tests.csproj
│   ├── Extension/
│   │   └── (extension tests)
│   ├── Infrastructure/
│   │   ├── BasicAuthMiddlewareTests.cs
│   │   └── (middleware tests)
│   ├── Security/
│   │   └── SameOriginRequestValidatorTests.cs
│   ├── Integration/
│   │   └── (end-to-end tests)
│   ├── Services/
│   │   ├── CardLookupServiceTests.cs
│   │   ├── CommanderSpellbookServiceTests.cs
│   │   ├── ScryfallTaggerServiceTests.cs
│   │   ├── AnalysisPromptVariantTests.cs (ChatGpt, Claude, Gemini)
│   │   └── (30+ service tests)
│   ├── TestDoubles/
│   │   ├── FakeHttpClientFactory.cs
│   │   ├── FakeCategoryKnowledgeStore.cs
│   │   ├── FakeResiliencePipelineProvider.cs
│   │   ├── FakeScryfallRestClientFactory.cs
│   │   ├── StubHttpMessageHandler.cs
│   │   ├── ThrowingCardSearchService.cs
│   │   └── (more test doubles)
│   └── bin/, obj/
│
├── DeckFlow.Core.Tests/               # Core layer tests (xUnit)
│   ├── DeckFlow.Core.Tests.csproj
│   ├── Parsing/
│   │   ├── MoxfieldParserTests.cs
│   │   ├── ArchidektParserTests.cs
│   │   └── (parser tests)
│   ├── Diffing/
│   │   ├── DiffEngineTests.cs
│   │   └── (diff tests)
│   ├── Integration/
│   │   └── (integration tests)
│   └── bin/, obj/
│
├── browser-extensions/                # Browser extension companion
│   └── deckflow-bridge/               # Manifest V3 extension
│       ├── manifest.json
│       ├── background.js              # Service worker
│       ├── deckflow-bridge.js         # Content script
│       ├── options.js
│       └── (extension assets)
│
├── prompt-templates/                  # ChatGPT prompt templates
│   └── deck-comparison/
│       ├── system-prompt.txt
│       ├── user-prompt.txt
│       └── (template partials)
│
├── artifacts/                         # Runtime artifacts (gitignored)
│   ├── feedback.db                    # SQLite feedback store
│   ├── category-knowledge.db          # SQLite category knowledge
│   ├── content-kb.db                  # SQLite content KB (v1.4)
│   └── deck-cache.json                # (example)
│
├── logs/                              # Runtime logs (gitignored)
│   ├── web-2026-05-29.log
│   └── cli-2026-05-29.log
│
├── docs/                              # Documentation
│   ├── ops/
│   │   ├── admin-operations.md        # Admin console guide
│   │   ├── db-schema.md               # Schema reference
│   │   └── (ops docs)
│   ├── decisions/                     # Architecture Decision Records (ADRs)
│   │   ├── 001-mvc-razor-framework.md
│   │   ├── 002-service-layer-pattern.md
│   │   ├── (more ADRs)
│   │   └── 999-phase-notes.md
│   └── (research docs)
│
├── scripts/                           # Helper scripts
│   ├── run-web.ps1                    # Windows dev runner
│   ├── run-web.sh                     # Linux dev runner
│   └── (build helpers)
│
├── .planning/                         # GSD planning artifacts (gitignored)
│   ├── codebase/
│   │   ├── ARCHITECTURE.md            # (this file's sibling)
│   │   ├── STRUCTURE.md               # (this file)
│   │   ├── CONVENTIONS.md             # (future)
│   │   ├── TESTING.md                 # (future)
│   │   ├── STACK.md                   # (future)
│   │   ├── INTEGRATIONS.md            # (future)
│   │   └── CONCERNS.md                # (future)
│   ├── phases/
│   │   ├── 25-*/                      # Phase 25 planning (shipped)
│   │   ├── 26-*/                      # Phase 26 planning (shipped)
│   │   └── (more phases)
│   ├── specs/
│   │   ├── 01-CONTEXT.md              # Decision matrix
│   │   ├── 02-ARCHIVE.md              # Archived decisions
│   │   └── (spec docs)
│   └── (other planning)
│
├── .codex/                            # Codex (companion AI) config
│   └── (workspace config)
│
├── .agents/                           # Claude agent config (skills registry)
│   └── (agent setup)
│
└── .gitignore                         # Exclude artifacts, logs, build outputs
```

## Directory Purposes

**Solution Root:**
- `DeckFlow.sln`: Solution file referencing 5 projects (Web, Core, CLI, Web.Tests, Core.Tests).
- `Directory.Build.props`: Shared .NET build properties (clears NuGet fallback folders for WSL compatibility).
- `Dockerfile`: Multi-stage build (sdk:10.0 → aspnet:10.0); copies code, restores, builds, publishes.
- `render.yaml`: Deployment config (Render — the live production host).

**DeckFlow.Web:**
- Main ASP.NET Core MVC application (web entry point).
- Controllers drive HTTP routing; Services handle business logic; Views render Razor + TypeScript.
- DI setup in `Program.cs` registers 30+ services; middleware pipeline configured for security, logging, analytics.
- `wwwroot/` contains static assets (CSS themes, compiled TypeScript, packaged extension).

**DeckFlow.Core:**
- Pure domain logic: parsing, diffing, normalization, exporting, knowledge, content KB stores, storage dialect.
- Zero HTTP framework deps (Integration/* uses RestSharp/Polly for importers only).
- Used by Web and CLI; consumed by tests.

**DeckFlow.CLI:**
- System.CommandLine host for power-user commands: compare, harvest, distill, archidekt-*, probe, card-lookup, scryfall-probe, content-source-*.
- Depends on DeckFlow.Core; minimal web dependencies.

**Test Projects:**
- `DeckFlow.Web.Tests`: 30+ service/controller/infrastructure unit tests; test doubles in TestDoubles/ folder.
- `DeckFlow.Core.Tests`: Parsing, diffing, integration tests.
- Both use xUnit; coverage via coverlet.

## Key File Locations

**Entry Points:**
- `DeckFlow.Web/Program.cs`: Web app bootstrap (DI, middleware, startup validation).
- `DeckFlow.CLI/Program.cs`: CLI app bootstrap (System.CommandLine tree, command handlers).
- `DeckFlow.Web/Controllers/DeckController.cs`: Primary user workflows.

**Configuration:**
- `DeckFlow.Web/appsettings.json`: Logging defaults, allowed hosts.
- `DeckFlow.Web/Configuration/AiPlatformOptions.cs`: Feature flags (Gemini enable/disable).
- `DeckFlow.Core/Storage/IRelationalDialect.cs`: Pluggable SQL dialect interface.

**Core Logic:**
- `DeckFlow.Core/Parsing/{MoxfieldParser,ArchidektParser}.cs`: Deck text parsing.
- `DeckFlow.Core/Integration/{MoxfieldApiDeckImporter,ArchidektApiDeckImporter}.cs`: API deck fetch.
- `DeckFlow.Core/Diffing/DiffEngine.cs`: Card-by-card reconciliation.
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs`: Category lookup.
- `DeckFlow.Core/Knowledge/ContentArtifactWriter.cs`: KB artifact emission.
- `DeckFlow.Core/Content/ContentVideoStore.cs`: Harvested video persistence.

**HTTP/Services:**
- `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs`: Polly v8 pipeline registration.
- `DeckFlow.Web/Services/CardLookupService.cs`: Scryfall card lookup.
- `DeckFlow.Web/Services/PromptBuilders/*/`: Prompt variant strategies.
- `DeckFlow.Web/Services/ArchidektCacheJobService.cs`: Background category knowledge job.

**Security & Infrastructure:**
- `DeckFlow.Web/Security/SameOriginRequestValidator.cs`: CSRF guard.
- `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs`: Admin authentication.
- `DeckFlow.Web/Infrastructure/AnalyticsMiddleware.cs`: Request metrics.

**Views & Styling:**
- `DeckFlow.Web/Views/Shared/_Layout.cshtml`: Master page.
- `DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml`: Shared navigation.
- `DeckFlow.Web/wwwroot/css/site-common.css`: Shared layout CSS (CRITICAL: layout only, no tokens).
- `DeckFlow.Web/wwwroot/css/site.css`: Default theme token overrides.
- `DeckFlow.Web/wwwroot/css/site-guild-*.css`: Guild-specific themes.
- `DeckFlow.Web/wwwroot/css/admin*.css`: Admin shell styling.

**TypeScript Source:**
- `DeckFlow.Web/wwwroot/ts/site.ts`: Global initialization.
- `DeckFlow.Web/wwwroot/ts/deck-sync.ts`: Deck sync page logic.
- `DeckFlow.Web/wwwroot/ts/*.ts`: Feature-specific bundles (all compile to `wwwroot/js/`).

**Testing:**
- `DeckFlow.Web.Tests/Services/`: Service unit tests (CardLookupServiceTests.cs, etc.).
- `DeckFlow.Web.Tests/TestDoubles/`: Fake/stub/throwing test doubles.
- `DeckFlow.Core.Tests/Parsing/`: Parser unit tests.

## Naming Conventions

**Files:**
- One public type per file; file name matches type name exactly (CardLookupService.cs contains ScryfallCardLookupService).
- Test files mirror source with Tests suffix (CardLookupService.cs → CardLookupServiceTests.cs).
- Razor views: PascalCase `.cshtml` (CommanderCategories.cshtml); shared partials prefixed `_` (_Layout.cshtml, _WorkflowStepTabs.cshtml).
- TypeScript files: kebab-case to match compiled JS bundles (deck-sync.ts → deck-sync.js).
- CSS files: hyphenated (site-common.css, site-guild-azorius.css, admin-common.css).

**Directories:**
- One folder per controller (Deck/, Commander/, Feedback/, Admin*/).
- Feature-grouped services (Harvest/, Content/, Analytics/, PromptBuilders/).
- Logical layer grouping (Infrastructure/, Security/, Http/).

**Types:**
- Interfaces: I prefix (ICardLookupService, IScryfallRestClientFactory).
- Classes: PascalCase, prefer sealed on leaf types (public sealed class ScryfallCardLookupService).
- Records: Sealed when immutable (sealed record DeckEntry).
- Test classes: Public sealed class XxxTests (CardLookupServiceTests).
- Test doubles: Fake* (FakeCategoryKnowledgeStore), Stub* (StubHttpMessageHandler), Throwing* (ThrowingCardSearchService).

**Methods:**
- Async methods always end in Async (LookupAsync, FindCombosAsync, GetCategoriesAsync).
- Private helpers: PascalCase (FormatCard, NormalizeName, ExtractMechanicNames).
- Static readonly fields: PascalCase (MinInterval, RetryAfterCap, QuantityPrefixRegex).
- Constants: PascalCase (CollectionBatchSize, MaxCardsPerSubmission, ApiUrl).
- Private instance fields: _camelCase (\_executeAsync, \_logger, \_httpClientFactory).
- Parameters & locals: camelCase.

**Namespaces:**
- File-scoped: namespace DeckFlow.Web.Services; namespace DeckFlow.Core.Models; namespace DeckFlow.Web.Tests.
- Tests in single namespace per project (DeckFlow.Web.Tests, DeckFlow.Core.Tests) regardless of subfolder.

## Where to Add New Code

**New Feature (e.g., new Deck workflow):**
- Implementation: `DeckFlow.Web/Controllers/DeckController.cs` (new action) + `DeckFlow.Web/Services/DecNewFeatureService.cs` (new service).
- View: `DeckFlow.Web/Views/Deck/NewFeature.cshtml` (new Razor).
- Tests: `DeckFlow.Web.Tests/Services/DecNewFeatureServiceTests.cs`.
- Prompt variant (if AI-driven): `DeckFlow.Web/Services/PromptBuilders/NewType/{ChatGptNewTypePromptVariant,ClaudeNewTypePromptVariant,GeminiNewTypePromptVariant}.cs` + Registry.

**New Component/Module:**
- Implementation: Create folder in DeckFlow.Core/ or DeckFlow.Web/Services/, follow naming conventions.
- Interface: Declare INewModule in same folder; implement concrete class.
- DI registration: Add to Program.cs (Web) or inline (CLI).
- Tests: Mirror path in test project with Tests suffix.

**New API Endpoint:**
- Controller: `DeckFlow.Web/Controllers/Api/NewApiController.cs` (new controller in Api/ folder).
- Validator: Call `SameOriginRequestValidator.IsValid(request)` for POST/PUT/DELETE.
- DTO: Define request/response models in `DeckFlow.Web/Models/Api/NewApiRequest.cs`, `NewApiResponse.cs`.
- Tests: `DeckFlow.Web.Tests/Services/NewApiControllerTests.cs` (integration test) or mocked unit tests.

**Utilities & Helpers:**
- Shared helpers: `DeckFlow.Core/Normalization/`, `DeckFlow.Core/Filtering/`, or `DeckFlow.Web/Extensions/`.
- Static classes OK for stateless utilities (CardNormalizer, ScryfallThrottle, MoxfieldApiUrl).
- Test doubles: Add to `DeckFlow.Web.Tests/TestDoubles/` (Fake*, Stub*, Throwing* naming).

**Database Schema Changes:**
- Dialect SQL: Update `DeckFlow.Core/Storage/{SqliteRelationalDialect,PostgresRelationalDialect}.cs`.
- Store implementation: New store class in `DeckFlow.Core/Content/` or `DeckFlow.Web/Services/`.
- Migration: None (project uses schema-on-startup via EnsureSchemaAsync); simply deploy new code + restart.

**New Prompt Variant (new AI platform):**
- Strategy: Create `DeckFlow.Web/Services/PromptBuilders/Analysis/NewPlatformAnalysisPromptVariant.cs` (implement IAnalysisPromptVariant).
- Registry update: Register in `AnalysisPromptVariantRegistry` (or create new registry for new prompt type).
- AiPlatform value: Add static singleton to `DeckFlow.Web/Models/AiPlatform.cs` + All list.
- UI toggle: Optional — hide via AiPlatformOptions feature flag if not ready.

**New Theme:**
- CSS file: `DeckFlow.Web/wwwroot/css/site-guild-{name}.css` (add tokens only, never layout).
- Registration: Add to Razor theme-picker select element in Views.
- Layout CSS stays in `site-common.css` (CRITICAL constraint per CLAUDE.md).

## Special Directories

**`/artifacts/`:**
- Purpose: Runtime database & artifact files.
- Generated: Yes (created at startup if missing).
- Committed: No (gitignored). Structure:
  - `feedback.db`: User submissions, feedback, brute-force tracking.
  - `category-knowledge.db`: Deck→category mappings from Archidekt harvester.
  - `content-kb.db`: Content KB sources, videos, transcripts, artifacts (v1.4).
  - `deck-cache.json`: (example, if any intermediate files).

**`/logs/`:**
- Purpose: Daily rolling log files.
- Generated: Yes (created by Serilog at startup).
- Committed: No (gitignored). Retention: 14 days (Program.cs:59, CLI:15).

**`/wwwroot/js/`:**
- Purpose: Compiled TypeScript output.
- Generated: Yes (MSBuild TypeScript target, BeforeTargets="Build").
- Committed: Yes (CRITICAL: both source .ts and output .js git-tracked per build coupling in DeckFlow.Web.csproj).

**`/wwwroot/extensions/`:**
- Purpose: Packaged browser extension.
- Generated: Yes (ZipDeckFlowBridge MSBuild target, compresses `browser-extensions/deckflow-bridge/`).
- Committed: No (deckflow-bridge.zip recreated on every build).

**`/.planning/codebase/`:**
- Purpose: GSD mapper output (ARCHITECTURE.md, STRUCTURE.md, etc.).
- Generated: Yes (by `/gsd:map-codebase` command).
- Committed: Yes (part of repo state for navigation).

**`/.planning/phases/`:**
- Purpose: Phase execution plans and reviews.
- Generated: Yes (by `/gsd:plan-phase` command).
- Committed: Yes (milestone records).

**`/prompt-templates/`:**
- Purpose: ChatGPT prompt templates (system + user).
- Generated: No (hand-authored).
- Committed: Yes. Structure: `prompt-templates/deck-comparison/{system-prompt.txt,user-prompt.txt}` (loaded by DeckReporter).

**`/docs/decisions/`:**
- Purpose: Architecture Decision Records (ADRs) in MADR format.
- Generated: No (hand-authored for significant design choices).
- Committed: Yes (design rationale history).

---

*Structure analysis: 2026-05-29*
