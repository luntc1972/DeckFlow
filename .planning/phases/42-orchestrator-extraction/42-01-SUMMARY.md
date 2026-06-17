---
phase: 42-orchestrator-extraction
plan: 01
subsystem: api
tags: [orchestration, contracts, records, dependency-injection, content-kb]

requires:
  - phase: 41-studio-scaffold-secrets-wiring
    provides: standalone DeckFlow.Studio host that will consume Core orchestration
provides:
  - IContentKbOrchestrator facade + 5 focused sub-interfaces under DeckFlow.Core/Orchestration/
  - 7 null-safe structured result/row records (DistillResult, HarvestResult, ContentSourceResult, ContentMaintenanceResult, BlockedVideoListResult, ContentIndexExportResult, ContentIndexExportRow)
  - synchronous IOrchestratorProgress sink + NullOrchestratorProgress
  - ContentKbOrchestratorOptions options record (host-resolved ArtifactRoot, DI-clean ctor binding)
affects: [42-02, 42-03, 42-04, 42-05]

tech-stack:
  added: []
  patterns:
    - "Facade-by-inheritance: IContentKbOrchestrator aggregates 5 slice interfaces so Studio can depend on one slice"
    - "Synchronous progress sink (not IProgress<T>) to preserve live CLI line interleaving"
    - "Options record transports host-resolved artifactRoot instead of bare-string ctor param"

key-files:
  created:
    - DeckFlow.Core/Orchestration/IContentKbOrchestrator.cs
    - DeckFlow.Core/Orchestration/DistillResult.cs
    - DeckFlow.Core/Orchestration/ContentIndexExportRow.cs
    - DeckFlow.Core/Orchestration/ContentKbOrchestratorOptions.cs
    - DeckFlow.Core/Orchestration/OrchestratorProgress.cs
  modified: []

key-decisions:
  - "Companion types (NullOrchestratorProgress, ContentSourceOutcome enum, BlockedVideoListItem) co-located in their parent files to stay inside the 15-file fence — consistent with project's co-location convention"

patterns-established:
  - "Core orchestration contracts are Console-free and Serilog-free; live output flows through IOrchestratorProgress"
  - "Result records: required bool Success, IReadOnlyList<T> init Array.Empty<T>(), nullable messages string?"

requirements-completed: [ORCH-01]

duration: 8min
completed: 2026-06-13
---

# Phase 42-01: Orchestration Contract Skeleton Summary

**Interface-first DeckFlow.Core/Orchestration/ contract layer — IContentKbOrchestrator facade-by-inheritance over 5 slice interfaces, 7 null-safe result/row records, synchronous progress sink, and a DI-clean options record — no domain logic lifted.**

## Performance
- **Duration:** ~8 min (Codex gpt-5.4)
- **Tasks:** 2
- **Files created:** 15 (all under DeckFlow.Core/Orchestration/)

## Accomplishments
- 6 interfaces: facade + IHarvestOrchestrator, IDistillOrchestrator, IContentMaintenanceOrchestrator, IContentSourceManager, IContentIndexExporter (facade aggregates by inheritance, not one fat interface).
- 7 records: DistillResult/HarvestResult/ContentSourceResult/ContentMaintenanceResult/BlockedVideoListResult/ContentIndexExportResult + standalone ContentIndexExportRow (byte-identical property set + declaration order to the CLI private copy, From(ContentSiteIndexRow) factory preserved).
- Synchronous IOrchestratorProgress { void Report(string); } + NullOrchestratorProgress — deliberately NOT System.IProgress<T> (would reorder live per-video lines).
- ContentKbOrchestratorOptions { required string ArtifactRoot } so Wave 2 ctor is DI-resolvable without a global bare-string registration.

## Task Commits
1. **Task 1: result records + options + progress sink** — `249be82` (feat)
2. **Task 2: facade + sub-interfaces** — `70d2663` (feat)

## Decisions Made
- Companion types nested in parent files (NullOrchestratorProgress in OrchestratorProgress.cs; ContentSourceOutcome enum in ContentSourceResult.cs; BlockedVideoListItem in BlockedVideoListResult.cs) to honor the 15-file scope fence. Matches project co-location convention.

## Deviations from Plan
None functional. Codex added a defensive `ArgumentNullException.ThrowIfNull(row)` to ContentIndexExportRow.From — no behavior change for valid input; matches project guard convention.

## Verification
- `dotnet build DeckFlow.sln -warnaserror` → 0 errors / 0 warnings.
- Console-free / Serilog-free / IProgress-free greps over Orchestration/ all clean.
- ContentIndexExportRow property names + order confirmed identical to CLI original (reviewer diff).

## Next Phase Readiness
- Wave 2 (42-02) can implement ContentKbOrchestrator against these fixed signatures + the ContentKbOrchestratorOptions ctor shape.

---
*Phase: 42-orchestrator-extraction*
*Completed: 2026-06-13*
