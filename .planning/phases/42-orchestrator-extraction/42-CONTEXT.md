# Phase 42: Orchestrator Extraction - Context

**Gathered:** 2026-06-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Extract the Content KB orchestration logic (harvest, distill, block/unblock, corpus-reset, source add/set-enabled, seed-index export) out of `DeckFlow.CLI/ContentKbCommandRunners.cs` (~1480-line god class) into `DeckFlow.Core` behind an `IContentKbOrchestrator` facade. CLI command runners become thin adapters: build stores from paths, delegate to the orchestrator, convert the returned result to an exit code. Closes the v1.6 `ContentKbCommandRunners` god-class backlog item (ORCH-01) and establishes route/behavior parity (ORCH-02).

**Net effect:** identical CLI behavior; domain logic reusable by both CLI and Studio; Studio can call the orchestrator with no `DeckFlow.CLI` project reference.

</domain>

<decisions>
## Implementation Decisions

### Orchestrator Surface
- **D-01:** `IContentKbOrchestrator` is a **facade composed of focused sub-interfaces** (ISP-aligned per CLAUDE.md SOLID), e.g. `IHarvestOrchestrator` / `IDistillOrchestrator` / `IContentMaintenance` (block/unblock/reset/list-blocked) / `IContentSourceManager` (source add/set-enabled) / index export. The facade `IContentKbOrchestrator` aggregates them so a single type satisfies ROADMAP's named contract, while Studio can depend on just the slice it needs. Exact sub-interface split is planner/Codex discretion, but it MUST NOT be one fat interface.

### Output Contract (Console-free Core)
- **D-02:** Each operation returns a **structured result record** (operation-specific: counts, success flag, human-readable messages — fold the existing `HarvestCounts` / `DistillCounts` aggregators into these results). Core contains **no `Console.WriteLine`/`Console.Error`**.
- **D-03:** Live progress is emitted via an **injected progress sink** — `IProgress<string>` (or equivalent small callback) passed per-operation. CLI supplies a console-writing sink (preserving today's real-time per-video harvest/distill progress lines = parity); Studio supplies its own. The CLI adapter renders the result and **maps result → exit code** (exit-code policy lives in CLI, not Core).

### Dependency / Store Wiring
- **D-04:** Orchestrator(s) take their stores/services via **constructor injection** (DI-style). Operation methods take only operation arguments (+ the progress sink + `CancellationToken`). This makes Studio DI registration clean and keeps method signatures small.
- **D-05:** **Provider selection stays in the host composition root, not Core.** The Postgres-vs-Sqlite branch in `RunCorpusReset` (building `RelationalDatabaseConnection` + `PostgresConnectionStringNormalizer`) remains CLI-side: CLI resolves the provider/connection, constructs the right store instances, and injects ready store interfaces into the orchestrator. Orchestrator is storage-agnostic (depends only on `IContentVideoStore`, `IContentSiteIndexStore`, etc.). Matches SC2 "construct stores from paths."
- **D-06:** `ContentKbCliPaths` path resolution **stays in CLI**. `FileInfo`/path arguments do not cross into Core — CLI resolves paths, Core receives constructed stores.

### Validation / Helper Consolidation
- **D-07:** Moved validators (`ValidateClips`, `ValidateSummary`, `ValidateTranscriptLength`, word/token counters, projected-cost math) and distill constants (`DistillStatus*`, `SummaryMaxOutputTokens`, `ShortVideoMaxDuration`, etc.) are **consolidated into the existing Core validation home** (`DeckFlow.Core/Knowledge/DistillationValidation.cs` + `DistillationSchemas.cs`) where they overlap, rather than duplicated. **Semantics must stay byte-identical** — this is a behavior-preserving move. Cover with the existing `CommandRunnerValidateClipsTests` anchor (re-pointed) plus any new unit tests for the consolidated validators.

### Studio Integration Scope
- **D-08:** Prove SC4 end-to-end: add an `AddContentKbOrchestrator()` `IServiceCollection` extension in Core and a **minimal Studio service that resolves and calls the orchestrator** (smoke-level), confirming Studio→Core works with **no `DeckFlow.CLI` reference**. No Studio UI feature in this phase — that's Phase 43 publish work.

### Test Seam
- **D-09:** Ctor injection changes the `RunDistillAsync` seam. The `CommandRunnerValidateClipsTests` anchor (in `DeckFlow.Core.Tests`) is **rewritten at the call site** (construct orchestrator with ctor stores → call `DistillAsync(args)`) while **assertions stay identical** (validation-fail short-circuits before DB writes; no rows written). "Behavior unchanged" = same verified behavior via the new seam. **No static back-compat shim** — a leftover static entry point would contradict the thin-adapter goal.

### Claude's Discretion
- Exact sub-interface decomposition and result-record field shapes.
- Whether the orchestrator implementation is one class implementing the facade or several classes (one per sub-interface) aggregated behind it.
- Namespace placement (recommended `DeckFlow.Core/Orchestration/` per scout) and file-per-type layout.
- Naming of result records and the progress-sink abstraction.
- New unit-test coverage added for the extracted orchestrator paths beyond the re-pointed anchor.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Source to extract (the god class)
- `DeckFlow.CLI/ContentKbCommandRunners.cs` — 8 public `Run*Async` entry points, 28 private/internal domain helpers, 5 domain model types (`HarvestCounts`, `DistillCounts`, `DistillVideoOutcome`, `HarvestVideoResolution`, `ContentIndexExportRow`), 22 store-construction sites. The full extraction target.
- `DeckFlow.CLI/ContentKbCliPaths.cs` — path/db resolution; STAYS in CLI (D-06).

### Core stores/services the orchestrator depends on (inject these)
- `DeckFlow.Core/Content/IContentSourceStore.cs`, `IContentVideoStore.cs`, `IContentSiteIndexStore.cs`, `IBlockedVideoStore.cs`, `ILlmSpendLedger.cs`, `IWhisperSpendLedger.cs`, `IContentHarvestRunStore.cs`
- `DeckFlow.Core/Integration/ILlmDistillationService.cs`, `IYouTubeChannelVideoLister.cs`, `ITranscriptSource.cs`, `IFfmpegAudioChunker.cs`
- `DeckFlow.Core/Storage/` — `RelationalDatabaseConnection`, `RelationalDatabaseProvider`, dialects (provider selection stays host-side per D-05)

### Validation consolidation target
- `DeckFlow.Core/Knowledge/DistillationValidation.cs`, `DistillationSchemas.cs`, `ContentTagVocabulary.cs`, `ContentArtifactWriter.cs`

### Behavior-parity anchor (test)
- `DeckFlow.Core.Tests/CommandRunnerValidateClipsTests.cs:56` — only test that directly exercises `RunDistillAsync`; re-point to new seam, keep assertions (D-09).

### Studio target
- `DeckFlow.Studio/DeckFlow.Studio.csproj` (references Core only — must NOT add CLI ref), `DeckFlow.Studio/Program.cs`, `DeckFlow.Studio/StudioConfig.cs`

### Project files
- `DeckFlow.CLI/DeckFlow.CLI.csproj`, `DeckFlow.Core/DeckFlow.Core.csproj`, `DeckFlow.sln`

### Planning docs
- `.planning/ROADMAP.md` (Phase 42 success criteria), `.planning/REQUIREMENTS.md` (ORCH-01, ORCH-02)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Existing **internal-static store-injecting overloads** of `RunBlockVideoAsync`/`RunUnblockVideoAsync`/`RunCorpusResetAsync`/`RunListBlockedAsync`/`RunDistillAsync`/`RunHarvestAsync` already separate domain logic from store construction — these become the orchestrator method bodies (lift, swap `Console` for progress sink + result record, swap static for ctor-injected instance).
- `DeckFlow.Core/Knowledge/DistillationValidation.cs` already exists as the validation home — consolidate moved validators here (D-07).
- `DeckFlow.Core/Integration/CliLlmDistillationService.cs` and harvest/transcript services already live in Core — orchestrator just composes existing Core interfaces; **0 new packages** required.

### Established Patterns
- Phase 38/39 SRP splits (extract `IDeckEntryLoader` / `IScryfallCardResolver`) are the precedent: pure behavior-preserving refactor, Codex-impl/Claude-review per wave, build 0E/0W gate, route/behavior parity.
- DeckFlow convention: `I`-prefixed interfaces, `sealed` leaf classes, one public type per file matching filename, `Async` suffix, optional `ILogger` defaulting to `NullLogger<T>.Instance`, file-scoped namespaces, Allman braces, LF endings.
- DI registration extension pattern exists (`AddDeckFlowResiliencePipelines()`, `UseDeckFlowSecurityHeaders()`) — mirror for `AddContentKbOrchestrator()`.

### Integration Points
- CLI `Program.cs` System.CommandLine wiring stays; runners delegate to orchestrator.
- Studio composition root (`Program.cs`) gains `AddContentKbOrchestrator()` + a smoke service (D-08).
- `DeckFlow.Core.Tests` is the test home (xUnit); CLI has no separate test project — the one anchor lives in Core.Tests.

</code_context>

<specifics>
## Specific Ideas

- Recommended namespace: `DeckFlow.Core/Orchestration/` (scout recommendation).
- This is a **pure refactor** — zero user-visible change. Same parity bar as Phase 38 (route/behavior parity + live smoke).
- Build gate via Windows `dotnet.exe` from WSL; VSTest unreliable in WSL (per project constraints) — rely on `dotnet build DeckFlow.sln` clean + targeted Core.Tests run + push-and-watch CI.

</specifics>

<deferred>
## Deferred Ideas

- Studio UI feature consuming the orchestrator (harvest/distill/publish from Blazor) — Phase 43+ (PUB-01/02, REVQ-01).
- Routing the orchestrator into the Web app's hosted `ArchidektCacheJobService`-style background job — out of scope; not requested.

None of the low-score todo matches (combo-data spike, expert-context pin, validate-KB-value) relate to orchestrator extraction — not folded.

</deferred>

---

*Phase: 42-orchestrator-extraction*
*Context gathered: 2026-06-13*
