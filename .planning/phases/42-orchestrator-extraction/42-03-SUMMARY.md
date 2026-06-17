---
phase: 42-orchestrator-extraction
plan: 03
subsystem: api
tags: [orchestration, cli, dependency-injection, refactor, tests, content-kb]

requires:
  - phase: 42-orchestrator-extraction
    provides: ContentKbOrchestrator (Wave 2) + Wave-1 contracts
provides:
  - thin ContentKbCommandRunners CLI adapters over IContentKbOrchestrator (zero domain logic in CLI)
  - AddContentKbOrchestrator() Core DI extension (facade + 5 sub-interfaces forwarded to one scoped instance)
  - shared internal test doubles (FakeOrchestratorStores, ThrowingOrchestratorDependencies, RecordingOrchestratorDoubles)
  - 5 Core.Tests files re-pointed to the orchestrator seam
affects: [42-04, 42-05]

tech-stack:
  added: []
  patterns:
    - "Host owns provider selection (D-05) + path resolution (D-06); Core gets resolved values via ContentKbOrchestratorOptions"
    - "Synchronous ConsoleOrchestratorProgress sink in CLI (no Progress<T>) preserves live per-video line interleaving"
    - "DI: one scoped concrete + 6 GetRequiredService forwards so any slice injection resolves the same instance"

key-files:
  created:
    - DeckFlow.Core/Orchestration/ServiceCollectionExtensions.cs
    - DeckFlow.Core.Tests/Orchestration/FakeOrchestratorStores.cs
    - DeckFlow.Core.Tests/Orchestration/ThrowingOrchestratorDependencies.cs
    - DeckFlow.Core.Tests/Orchestration/RecordingOrchestratorDoubles.cs
  modified:
    - DeckFlow.CLI/ContentKbCommandRunners.cs
    - DeckFlow.Core.Tests/CommandRunnerValidateClipsTests.cs
    - DeckFlow.Core.Tests/RunDistillAsyncTests.cs
    - DeckFlow.Core.Tests/CommandRunnerHarvestTests.cs
    - DeckFlow.Core.Tests/CommandRunnerCorpusResetTests.cs
    - DeckFlow.Core.Tests/BlockedVideoStoreTests.cs

key-decisions:
  - "PLAN GAP CLOSED: plan scoped re-pointing only 1 anchor test, but 4 more Core.Tests files (~50 tests) called the old CLI store-injecting seams. User chose the full fix: re-point all 4 + delete the back-compat shims (true D-09), not a deferred shim bridge."
  - "blank-id BlockVideo: orchestrator try/catch returns Success=false instead of throwing; CLI adapter maps Success=false to Console.Error + exit 1 — IDENTICAL user-facing behavior to the old throw->caught->exit-1 path. Only the direct-seam unit assertion changed."

patterns-established:
  - "Seam tests construct ContentKbOrchestrator directly with RecordingOrchestratorProgress + recording ILogger<T>, asserting on recorders/result records instead of Console/Serilog capture"

requirements-completed: [ORCH-02]

duration: 30min
completed: 2026-06-13
---

# Phase 42-03: Thin CLI Adapters + DI + Seam Re-point Summary

**ContentKbCommandRunners reduced to thin adapters over IContentKbOrchestrator (provider/path resolution stays host-side), AddContentKbOrchestrator() forwards the facade + 5 slices to one scoped instance, and all 5 Core.Tests seam files re-pointed to the orchestrator with the back-compat shims fully removed (D-09) — Core.Tests 322/322 green.**

## Performance
- **Duration:** ~30 min (Codex gpt-5.4, incl. 1 corrective pass)
- **Tasks:** 3 + gap-closure corrective pass
- **Files:** 4 created, 6 modified

## Accomplishments
- AddContentKbOrchestrator(): 1 scoped concrete + 6 GetRequiredService<ContentKbOrchestrator> forwards (facade + 5 sub-interfaces). No bare-string/options registration (host's job). No new package (IServiceCollection resolves via existing transitive Logging.Abstractions).
- CLI adapters: each public Run*Async resolves paths (ContentKbCliPaths), constructs stores, wraps artifactRoot in ContentKbOrchestratorOptions, builds the orchestrator + a synchronous ConsoleOrchestratorProgress sink, calls the op, maps result→exit code. Postgres-vs-Sqlite branch + PostgresConnectionStringNormalizer stay in corpus-reset (D-05). All lifted domain methods/records deleted from CLI.
- Test seam: shared FakeOrchestratorStores + ThrowingOrchestratorDependencies + RecordingOrchestratorDoubles; 5 test files re-pointed.

## Task Commits
1. **Task 1: AddContentKbOrchestrator DI** — `fdb6d3f` (feat)
2. **Task 2: thin CLI adapters** — `b7516b9` (refactor)
3. **Task 3: re-point anchor test** — `7463c23` (test)
4. **Gap closure: re-point 4 seam files + drop shims** — `c65b8a9` (test)

## Decisions Made
- **Planning gap (surfaced by Codex, decided by user):** the plan's 5-file scope missed 4 test files (RunDistillAsyncTests, CommandRunnerHarvestTests, CommandRunnerCorpusResetTests, BlockedVideoStoreTests) that called the old internal store-injecting seams. Codex initially kept thin delegating shims to compile; full Core.Tests run then exposed 6 failures (shims passed progress:null + no logger → dry-run "WOULD…" lines and "dropped out-of-vocab tag" warnings lost). User chose to re-point all 4 + delete the shims (true D-09 closure) rather than bridge.

## Reviewer Parity Verification (Claude)
- DI forwards = 6 (facade + 5 slices), all GetRequiredService<ContentKbOrchestrator>.
- No `internal static Run*Async(stores…)` shim remains in CLI (grep empty).
- No lifted domain methods/records remain in CLI (grep empty); ParseVideoIds kept.
- blank-id BlockVideo: CLI boundary behavior identical (Console.Error + exit 1) — verified against orchestrator try/catch + adapter WriteErrorAndReturn mapping.
- Full Core.Tests suite 322/322 (not filtered) on Windows dotnet.

## Verification
- `dotnet build DeckFlow.sln -warnaserror` → 0 errors / 0 warnings.
- `dotnet test DeckFlow.Core.Tests` → Passed 322 / Failed 0 / Total 322.

## Next Phase Readiness
- Wave 4: 42-04 (Studio smoke service proving slice injection resolves) + 42-05 (parity/golden tests) can both proceed — AddContentKbOrchestrator + shared test doubles are in place.

---
*Phase: 42-orchestrator-extraction*
*Completed: 2026-06-13*
