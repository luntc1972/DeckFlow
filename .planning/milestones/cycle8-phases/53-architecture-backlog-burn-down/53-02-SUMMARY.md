---
phase: 53-architecture-backlog-burn-down
plan: "02"
subsystem: di-composition
tags: [refactor, arch-d, program-cs, di-extensions, services-foldering]
dependency_graph:
  requires: [53-04]
  provides: [AddDeckFlowHttpClients, AddDeckFlowScryfallServices, AddDeckFlowPromptVariants, AddDeckFlowPacketServices, Services/Persistence, Services/Content-filled]
  affects: [Program.cs, DeckFlow.Web/Extensions/, DeckFlow.Web/Services/]
tech_stack:
  added: []
  patterns: [IServiceCollection-extension-method, concern-foldering]
key_files:
  created:
    - DeckFlow.Web/Extensions/HttpClientServiceCollectionExtensions.cs
    - DeckFlow.Web/Extensions/ScryfallServiceCollectionExtensions.cs
    - DeckFlow.Web/Extensions/PromptVariantServiceCollectionExtensions.cs
    - DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs
    - DeckFlow.Web.Tests/Extensions/DiCompositionExtensionsTests.cs
  modified:
    - DeckFlow.Web/Program.cs (553 → 354 LOC)
  moved:
    - 12 flat Services/ Scryfall files → Services/Scryfall/
    - 8 flat Services/ persistence files → Services/Persistence/ (new folder; includes FeedbackDialect from 53-04)
    - 4 flat Services/ content files → Services/Content/ (was empty)
decisions:
  - "DeckComparisonService left at flat Services/ root — moves to Services/PromptBuilders/ in a follow-up to avoid cross-plan churn collision with 53-03"
  - "AddDeckFlowHttpClients called BEFORE AddDeckFlowResiliencePipelines so named clients exist when Polly factory wires up — original ordering preserved"
  - "ICategoryKnowledgeStore registered inline in Program.cs (not part of packet group) to avoid disrupting the hosted-service + schema-validation startup sequence"
  - "DiCompositionExtensionsTests uses FakeCategoryKnowledgeStore (existing TestDoubles) + StubDeckEntryLoader (inline) + StubWebHostEnvironment (inline, mirrors HarvestServiceCollectionExtensionsTests pattern)"
metrics:
  duration: "~30 min"
  completed: "2026-06-17"
  tasks_completed: 3
  files_modified: 31
---

# Phase 53 Plan 02: Program.cs DI Extraction + Services Foldering Summary

**One-liner:** Extracted ~200 LOC of inline DI wiring from Program.cs into four AddDeckFlowXxx() extension methods and completed Services/ concern-foldering (Scryfall, Persistence, Content filled), with a ValidateOnBuild smoke test as the correctness guard.

## What Was Built

### Task 1 — Extract AddDeckFlowHttpClients() + AddDeckFlowScryfallServices() (commit 6fdd10d)

**HttpClientServiceCollectionExtensions.cs (new)**
- `AddDeckFlowHttpClients()`: 4 named HTTP clients (commander-banlist, commander-spellbook, scryfall-rest, scryfall-tagger), `System.Net.CookieContainer` singleton, `ScryfallTaggerHttpClient` typed client + `IScryfallTaggerHttpClient`, youtube-metadata client + `IYouTubeChannelVideoLister`
- HIGH-2 TaggerSessionCache TTL / HandlerLifetime invariant comment preserved verbatim

**ScryfallServiceCollectionExtensions.cs (new)**
- `AddDeckFlowScryfallServices()`: `IScryfallRestClientFactory`, `ITaggerSessionCache`, `ICommanderSearchService`, `ICardSearchService`, `CardLookupCache`, `ICardLookupService`, `IScryfallCardResolver`, `IMechanicLookupService`, `ICommanderBanListService`, `ICommanderSpellbookService`, `IScryfallSetService`, `IEdhTop16Client`, `IScryfallTaggerLookupService`

**Program.cs changes**: replaced ~100 LOC inline registration with `builder.Services.AddDeckFlowHttpClients()` + `builder.Services.AddDeckFlowScryfallServices()` at original positions.

### Task 2 — Extract AddDeckFlowPromptVariants() + AddDeckFlowPacketServices(); add DI smoke test (commit b4f7bb0)

**PromptVariantServiceCollectionExtensions.cs (new)**
- `AddDeckFlowPromptVariants()`: 6 prompt-variant families (Analysis, SetUpgrade, Comparison, FollowUp, MetaGap, Primer), each with ChatGpt/Claude/Gemini implementations + per-family registry singleton

**PacketServiceCollectionExtensions.cs (new)**
- `AddDeckFlowPacketServices()`: `PacketSessionCache` + 4 scoped packet-service factories (`IDeckAnalysisPacketService`, `IDeckComparisonService`, `IMetaGapService`, `IDeckPrimerPacketService`)

**Program.cs**: 553 → 354 LOC (36% reduction); 4 `AddDeckFlowXxx()` call sites preserved at original positions.

**DiCompositionExtensionsTests.cs (new)**
- `ValidateOnBuild = true, ValidateScopes = true` proves the extracted graph composes correctly
- Resolves all 4 packet services + 6 prompt-variant registries in a scope
- No new NuGet package (ServiceCollection + BuildServiceProvider already in Web.Tests)
- Result: 1 new test, 633 total passing, 11 skipped (pre-existing PG integration)

### Task 3 — Finish Services/ concern-foldering (commit 79bac41)

**Services/Scryfall/** (12 moves + existing ScryfallCardResolver = 13 files):
`CardLookupCache`, `CardLookupService`, `CardSearchService`, `ScryfallCommanderSearchService`, `ScryfallDtos`, `ScryfallRestClientFactory`, `ScryfallSetService`, `ScryfallTaggerHttpClient`, `ScryfallTaggerLookupService`, `ScryfallTaggerParsers`, `ScryfallThrottle`, `TaggerSessionCache`

**Services/Persistence/** (new folder, 8 files):
`AdminBruteForceTrackerStore`, `CategoryKnowledgeStore`, `ICategoryKnowledgeStore`, `DeckFlowDatabaseConnectionFactory`, `FeedbackDialect` (from 53-04 — no orphan at flat root), `FeedbackStore`, `IFeedbackStore`, `PacketArtifactStore`

**Services/Content/** (was empty, 4 files):
`ContentArtifactParser`, `ContentKbArtifactPathResolver`, `ContentKbSeedLoader`, `IContentKbSeedLoader`

All moves via `git mv` (git status shows R, not D+A). All namespaces remain `DeckFlow.Web.Services;` — zero using-directive churn in any consuming file.

## Verification

- `dotnet build DeckFlow.sln` — 0 errors, 2 pre-existing CS1574 warnings (Core + Core.Tests, out of scope)
- `dotnet test DeckFlow.Web.Tests` — 633 passed, 11 skipped (PG integration), 0 failed
- `DiCompositionExtensionsTests` — 1/1 (new; ValidateOnBuild proves extracted DI graph)
- Program.cs: 354 LOC (target <400 ✓), 8 `AddDeckFlowXxx()` call sites
- FeedbackDialect.cs in Services/Persistence/ — no Feedback persistence file at flat Services/ root
- `git status` shows R (rename) for all 24 moved files

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Missing `DeckFlow.Core.Loading` using in PacketServiceCollectionExtensions.cs**
- **Found during:** Task 2 build
- **Issue:** `IDeckEntryLoader` from `DeckFlow.Core.Loading` was not imported
- **Fix:** Added `using DeckFlow.Core.Loading;`
- **Files modified:** `PacketServiceCollectionExtensions.cs`
- **Commit:** b4f7bb0

**2. [Rule 1 - Bug] `ScryfallTaggerLookupService` requires `IFeatureFlagCache` (discovered via ValidateOnBuild)**
- **Found during:** Task 2 test run (DiCompositionExtensionsTests)
- **Issue:** `ScryfallTaggerLookupService` takes an `IFeatureFlagCache` constructor dep — not registered in test setup
- **Fix:** Added `services.AddDeckFlowFeatureFlags()` + `IWebHostEnvironment` stub (`FeatureFlagStore` needs it) to test setup
- **Files modified:** `DiCompositionExtensionsTests.cs`
- **Commit:** b4f7bb0

### Deliberate Carry-Forwards

**DeckComparisonService not moved to Services/PromptBuilders/**
- The plan noted this as out-of-scope to avoid cross-plan churn collision with 53-03 (deck-stat classifiers). File stays at `Services/DeckComparisonService.cs` for a follow-up move.

## Known Stubs

None.

## Threat Flags

None — pure refactor: DI registrations moved declaration site, files moved on disk with unchanged namespaces. T-53-02 (DI graph + startup sequence integrity) is CLOSED: extension calls placed at original positions; hosted-service + schema-validation startup left inline; ValidateOnBuild smoke test + full Web suite (633/633) are the guards.

## Self-Check: PASSED

- [x] `DeckFlow.Web/Extensions/HttpClientServiceCollectionExtensions.cs` exists
- [x] `DeckFlow.Web/Extensions/ScryfallServiceCollectionExtensions.cs` exists
- [x] `DeckFlow.Web/Extensions/PromptVariantServiceCollectionExtensions.cs` exists
- [x] `DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs` exists
- [x] `DeckFlow.Web.Tests/Extensions/DiCompositionExtensionsTests.cs` exists
- [x] `DeckFlow.Web/Services/Persistence/FeedbackDialect.cs` exists (folded from 53-04)
- [x] `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` exists (Content filled)
- [x] Commit 6fdd10d exists
- [x] Commit b4f7bb0 exists
- [x] Commit 79bac41 exists
- [x] `grep -c "AddHttpClient(\"scryfall-tagger\"" DeckFlow.Web/Program.cs` = 0
- [x] `grep -c "AddSingleton<ICardLookupService>" DeckFlow.Web/Program.cs` = 0
- [x] `grep -c "AddSingleton<IAnalysisPromptVariant" DeckFlow.Web/Program.cs` = 0
- [x] `grep -c "AddScoped<IDeckComparisonService" DeckFlow.Web/Program.cs` = 0
- [x] Program.cs line count = 354 (< 400 target)
- [x] `dotnet build DeckFlow.sln` exits 0
- [x] Web tests 633/0/11 (passed/failed/skipped)
