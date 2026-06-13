# Phase 39 Architecture Audit - Codex

Independent read-only audit of branch `v1.6`. I did not run `dotnet build` or local tests per the prompt; evidence comes from source inspection, `wc -l`, `rg`, and existing test names/coverage. Only this file was written.

## Executive Recommendation

**#1 execute now: Extract shared Web packet infrastructure for deck loading/canonicalization and Scryfall card-reference lookup from the packet services.**

This has the best value x leverage / risk ratio. The controllers are slim now, but their main callees still carry repeated infrastructure and domain orchestration in large classes:

- `DeckAnalysisPacketService.cs`: 1,625 LOC
- `DeckComparisonService.cs`: 1,305 LOC
- `MetaGapService.cs`: 1,018 LOC
- `DeckPrimerPacketService.cs`: 791 LOC

The extraction should be narrow and behavior-preserving:

1. Introduce an injected deck-source loader that owns "URL or pasted Moxfield/Archidekt text" loading and returns a loaded deck envelope.
2. Introduce an injected Scryfall card-reference resolver that owns collection batching, fallback search, oracle-name mapping, and throttle/pipeline execution.
3. Leave prompt variants, prompt text, schemas, request validation, workflow-step branching, and tool-specific artifacts in their current services.

Safety net: `DeckAnalysisPacketServiceTests`, `DeckComparisonServiceTests`, `MetaGapServiceTests`, `DeckPrimerPacketServiceTests`, `AiPlatformPhase10RoundTripTests`, `ResultContractTests`, and `dotnet build` in CI. Because VSTest is unreliable in WSL, local proof should be source-level plus build/CI rather than depending on local full-suite execution.

## Ranked Refactor Backlog

### 1. Shared Packet Infrastructure Behind Slim Controllers

**Files + LOC**

- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` - 1,625 LOC; constructor and RestSharp/Pipeline setup at lines 61-144; cache/load path at 288-324 and 399-430; deck loading at 669-703; Scryfall lookup/fallback at 1170-1278.
- `DeckFlow.Web/Services/DeckComparisonService.cs` - 1,305 LOC; constructor and RestSharp/Pipeline setup at lines 62-127; workflow at 227-307; deck loading at 416-443; Scryfall lookup/fallback at 446-508.
- `DeckFlow.Web/Services/MetaGapService.cs` - 1,018 LOC; constructor and RestSharp/Pipeline setup at lines 53-121; workflow at 227-305; Scryfall fallback/name resolution at 631-694.
- `DeckFlow.Web/Services/DeckPrimerPacketService.cs` - 791 LOC; constructor deps at lines 64-159; workflow at 218-330; deck loading at 399-430; decklist/summary formatting at 638-704.

**Problem**

The Phase 38 controller split moved HTTP actions out, but the packet services now combine too many SRP axes: deck-source loading, commander inference, canonical cache inputs, Scryfall transport and fallback behavior, oracle-name mapping, prompt artifact formatting, workflow validation, timing/logging, and cache writes. The same private deck-loading branch appears across analysis/comparison/meta-gap/primer. Scryfall collection lookup plus fallback search appears separately in analysis, comparison, and meta-gap with different local records and subtly different query behavior. This makes future packet tools likely to copy more private infrastructure and makes cache-key parity fixes expensive, as shown by the large comments around `ResolvePreScryfallCommanderState`.

This is not a recommendation to centralize per-platform prompt prose. `docs/decisions/0001-prompt-variants-decoupled.md` explicitly records that ChatGPT/Claude/Gemini prompt variants are intentionally decoupled; keep them separate.

**Severity**

High.

**Refactor**

Extract two narrow services:

- `IDeckSourceLoader` / `DeckSourceLoader`: wraps Moxfield/Archidekt URL import, pasted export parsing, fallback notice, playable/optional split, commander inference primitives, and canonical deck-source text where semantics are shared.
- `IScryfallCardReferenceResolver` / `ScryfallCardReferenceResolver`: wraps RestSharp collection batching, throttling, fallback search, oracle-name mapping, and unresolved-card handling. Return a simple result with resolved cards, oracle map, and unresolved names; let each packet service shape domain-specific text.

Then migrate one caller at a time: comparison first or meta-gap first, then analysis, then primer deck loading. Do not move prompt builders, prompt variants, schema JSON builders, response parsers, or tool-specific request validation in the first pass.

**Effort**

M.

**Risk**

M. The behavior is sensitive because prompt bytes and cache keys are product behavior, but the refactor can be incremental and private to Web DI.

**Behavior Preservation**

Use existing coverage as the guard:

- `DeckAnalysisPacketServiceTests`: deck source parsing, possible includes, commander inference, fallback commander eligibility, alternate printed names, set packet generation, validation errors.
- `DeckComparisonServiceTests`: prompt generation, missing input validation, parse failures, per-card fallback, commander-section recovery, round-trip exported deck text.
- `MetaGapServiceTests`: saved response path, sorted reference entries, alternate-face/base-card normalization, alternate print resolution before Commander Spellbook lookup, fetched-entry overrides.
- `DeckPrimerPacketServiceTests`: combo/category/meta-query branches and platform enablement.
- `AiPlatformPhase10RoundTripTests` and `ResultContractTests`: packet zip/request context and prompt-contract guardrails.
- `dotnet build` clean + CI. Do not rely on local VSTest in WSL.

### 2. Extract Content KB CLI Harvest/Distill Application Services

**Files + LOC**

- `DeckFlow.CLI/ContentKbCommandRunners.cs` - 1,508 LOC; public CLI construction at lines 68-106 and 594-637; distill orchestration at 384-470 and 970-1065; harvest orchestration at 647-708 and 770-860; validation/costing at 1194-1284; counters at 1391-1508.

**Problem**

The Phase 38 CLI top split deleted the old god file, but `ContentKbCommandRunners` is still both CLI adapter and application layer. It constructs stores/providers/HTTP clients, interprets exit codes, orchestrates source iteration, enforces subscription-provider policy, handles run records, performs transcript harvest, performs LLM distillation, validates LLM outputs, computes projected spend, emits artifacts/site-index rows, and owns process logging. The internal overloads are testable, but the class remains a low-cohesion module where a change to validation, billing, harvest retry, or command wiring all lands in the same file.

**Severity**

High.

**Refactor**

Extract behavior-preserving application services:

- `ContentKbHarvestRunner` for source/video harvest and transcript persistence.
- `ContentKbDistillRunner` for distillation status transitions, LLM calls, spend guardrails, artifact/index updates, and run summaries.
- Optional small value objects for `DistillOptions`, `HarvestOptions`, `DistillCounts`, and `HarvestCounts`.

Keep `ContentKbCommandRunners` as a thin CLI adapter that resolves paths, builds dependencies, calls the application services, writes console output, and maps exceptions to exit codes.

**Effort**

M/L.

**Risk**

M. The code is well-covered but operationally important because it mutates content DB state and spend ledgers.

**Behavior Preservation**

Guard with `RunDistillAsyncTests`, `CommandRunnerHarvestTests`, `CommandRunnerValidateClipsTests`, `CommandRunnerCorpusResetTests`, `BlockedVideoStoreTests`, `ContentVideoStoreDistillTests`, and `dotnet build`/CI. Important named tests include `RunDistillAsync_DryRunProjectsSpendWithoutBusinessMutations`, `RunDistillAsync_MeteredProvider_FailsClosedWithoutClassifying`, `RunDistillAsync_ClassifierDropsPreviouslyIndexedVideo_RemovesStaleIndexRow`, `RunHarvestAsync_WhisperInsertFailureAfterLedgerWriteKeepsLedgerRecord`, and `RunHarvestAsync_ExistingSkippedOverCapVideoIsNotDowngradedToFailedOnRetryException`.

### 3. Split Core Persistence Schema/SQL From Store Behavior

**Files + LOC**

- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` - 1,276 LOC; schema creation and indexes at lines 59-137; read/query methods around 209-260; batch writes around 521-650; queue/content-hash methods around 900-1040; upsert helpers around 1150-1208; dialect column inspection at 1236-1275.
- `DeckFlow.Core/Content/ContentVideoStore.cs` - 837 LOC; schema creation at lines 43-68; CRUD at 71-260; SQL constants at 580-837.
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` - 678 LOC; schema/migrations at 44-81; row upserts at 84-150; reads at 153-253; SQL constants at 558-678.
- `DeckFlow.Core/Storage/IRelationalDialect.cs`, `SqliteRelationalDialect.cs`, `PostgresRelationalDialect.cs`, `RelationalDatabaseConnection.cs`.

**Problem**

The Core persistence stores mix schema creation/migration, SQL text, dialect branching, row mapping, validation, and repository behavior in single classes. The SQLite/Postgres layer itself is small, but dialect-specific SQL and migration details are scattered inside each store. This increases the cost of future schema work and makes parity harder to reason about, especially for content tables where SQL constants dominate the lower half of the classes.

**Severity**

Medium/High.

**Refactor**

Extract schema/SQL modules without changing public repository APIs:

- `CategoryKnowledgeSchema` and `CategoryKnowledgeSql`.
- `ContentVideoSchema` / `ContentVideoSql`.
- `ContentSiteIndexSchema` / `ContentSiteIndexSql`.

Keep row mapping and validation initially in the existing stores unless it naturally falls out. Avoid a framework migration or ORM introduction; stay with the current ADO.NET style and `RelationalDatabaseConnection`.

**Effort**

M.

**Risk**

M. SQL movement is mechanically simple but parity-sensitive across SQLite/Postgres.

**Behavior Preservation**

Guard with `CategoryKnowledgeRepositoryTests`, `CategoryCacheSchemaParityTests`, `ContentHashDedupTests`, `ContentVideoStoreTests`, `ContentVideoStoreDistillTests`, `ContentSiteIndexStoreTests`, and `DeckFlow.Web.Tests/Integration/PostgresStorageTests`. Use CI for Postgres integration coverage where available; local WSL VSTest should not be the proof point.

### 4. Modularize Web Composition Root

**Files + LOC**

- `DeckFlow.Web/Program.cs` - 557 LOC; HTTP clients at lines 88-164 and 186-189; feature/harvest/analytics extension calls at 191-193; service graph at 245-378; middleware at 385-420; startup schema/salt work at 454-486; partition-key helpers at 510-537.
- Existing examples: `DeckFlow.Web/Extensions/FeatureFlagsServiceCollectionExtensions.cs`, `HarvestServiceCollectionExtensions.cs`, `AnalyticsServiceCollectionExtensions.cs`.

**Problem**

`Program.cs` still mixes hosting, logging, HTTP-client configuration, DI registration for most application services, prompt variant registration, middleware, startup database validation/schema creation, analytics salt initialization, and partition-key helper behavior. Some subsystems have extension methods already, but the packet services, prompt variants, external HTTP clients, and startup tasks remain inline. This makes composition changes noisy and harder to test in isolation.

**Severity**

Medium.

**Refactor**

Follow the existing extension pattern:

- `AddDeckFlowExternalHttpClients()`
- `AddDeckFlowDeckTools()`
- `AddDeckFlowPromptVariants()`
- `AddDeckFlowContentKb()`
- `UseDeckFlowPipeline()`
- `InitializeDeckFlowStoresAsync()` or a startup initializer service

Keep `DeriveCloudflareClientIp`, `DeriveFeedbackPartitionKey`, and `DeriveAdminPartitionKey` either in a small `RequestPartitionKeys` helper or leave them until a later pass if test churn is not worth it.

**Effort**

S/M.

**Risk**

L/M.

**Behavior Preservation**

Guard with `Extensions/HarvestServiceCollectionExtensionsTests.cs`, `BasicAuthMiddlewareTests`, `FeatureFlagGateAttributeTests`, `FeedbackControllerTests`, `DeckFlowDatabaseConnectionFactoryTests`, and compile-time DI smoke from `dotnet build`/CI. This refactor needs extra caution because DI lifetime changes can be invisible at compile time.

### 5. Split PacketArtifactStore By Artifact Family

**Files + LOC**

- `DeckFlow.Web/Services/PacketArtifactStore.cs` - 867 LOC; allowed-name sets at lines 27-74; build/load methods for analysis/comparison/meta-gap/primer at lines 107-262, 382-649; filename helpers at 666-681; archive helpers at 684-817.

**Problem**

The class is a static utility for four artifact families: analysis packet, deck comparison, cEDH meta-gap, and primer. It owns whitelist policy, zip building, zip loading, path-safe filenames, JSON extraction, and restore DTOs. The individual methods are not huge, but the class is an axis of change for every packet family. Adding a new artifact file or restore field risks touching a shared static file with unrelated families.

**Severity**

Medium.

**Refactor**

Extract per-family artifact stores or builders:

- `AnalysisPacketArtifacts`
- `ComparisonPacketArtifacts`
- `MetaGapPacketArtifacts`
- `PrimerPacketArtifacts`

Keep a facade named `PacketArtifactStore` temporarily to preserve call sites, then migrate controllers/tests family by family. Share only low-level archive helpers.

**Effort**

M.

**Risk**

M.

**Behavior Preservation**

Guard with `AiPlatformPhase10RoundTripTests`, `PacketArtifactStorePrimerTests`, `DeckPrimerResultRoundTripTests`, controller packet tests, and zip round-trip assertions already present in the Web tests.

### 6. Test Architecture: Reduce Large Fixture Coupling Without Requiring Local Full-Suite Runs

**Files + LOC**

- `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs` - 1,542 LOC.
- `DeckFlow.Web.Tests/AiPlatformPhase10RoundTripTests.cs` - 1,053 LOC.
- `DeckFlow.Core.Tests/RunDistillAsyncTests.cs` - 960 LOC.
- `DeckFlow.Core.Tests/CommandRunnerHarvestTests.cs` - 764 LOC.
- Shared construction lives in `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs`.

**Problem**

The test suite has valuable behavioral coverage, but the largest fixtures mirror the production god-service shape. That makes refactors more likely to require broad fixture edits and can hide missing focused tests for extracted collaborators. Because VSTest is unreliable in WSL, the project needs targeted guard tests that are easy to reason about in CI/build review, not only massive end-to-end service fixtures.

**Severity**

Medium.

**Refactor**

After extracting collaborators, add focused tests for the new seams:

- deck-source loader tests for URL/text/fallback notice/commander inference.
- Scryfall resolver tests for batching, fallback search, oracle map, and unresolved cards.
- CLI application service tests that reuse current fakes but move away from static command-runner entry points.

Do not delete existing broad tests until the new seam tests are in place.

**Effort**

S/M.

**Risk**

L.

**Behavior Preservation**

This is a safety-net refactor. Proof is additive: existing tests remain, new seam tests lock the extracted behavior, and CI build stays clean.

### 7. Defer: Per-Platform Prompt Variant Duplication

**Files + LOC**

- `DeckFlow.Web/Services/PromptBuilders/**`
- `docs/decisions/0001-prompt-variants-decoupled.md`

**Problem**

None for this phase. The repeated ChatGPT/Claude/Gemini prose is intentional product design, not accidental architecture debt.

**Severity**

Low / intentional.

**Refactor**

Do not merge, centralize, or extract shared prompt prose/constants across platform variants. Only fix semantic divergence when a rule differs unintentionally.

**Effort**

N/A.

**Risk**

High if violated, because prompt bytes are product behavior.

**Behavior Preservation**

Guard with `ResultContractTests`, `AiPlatformExtensionTests`, `AnalysisPromptVariantNoExpertContextTests`, and the decision record. Reviewers should treat semantic drift as a bug and textual duplication as intentional.

## Why #1 Beats The Other High Findings

The CLI runner is also high-value, but it is operationally riskier: it touches spend ledgers, distillation status, transcript persistence, and blocking/index deletion behavior. Core persistence extraction is useful, but it mostly improves internal maintainability without reducing the Web feature-change pressure created by the new slim controllers.

The Web packet-services refactor has broader leverage because nearly every deck tool now routes through these services. It reduces duplication across four user-facing flows, creates reusable seams for future tools, and can be done without changing prompt bytes or public behavior. The existing tests are unusually strong around exactly the risky behavior: commander inference, alternate-name fallback, cache/zip round trips, prompt contracts, and per-platform output.
