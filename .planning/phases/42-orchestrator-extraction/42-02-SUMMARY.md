---
phase: 42-orchestrator-extraction
plan: 02
subsystem: api
tags: [orchestration, content-kb, distillation, refactor, behavior-preserving]

requires:
  - phase: 42-orchestrator-extraction
    provides: Wave-1 orchestration contracts (interfaces, result records, progress sink, options)
provides:
  - ContentKbOrchestrator concrete impl (full IContentKbOrchestrator facade, CLI domain logic lifted into Core)
  - consolidated DistillationValidation (all-zero-timestamp clip rule added to Core + cost helpers + distill constants)
affects: [42-03, 42-04, 42-05]

tech-stack:
  added: []
  patterns:
    - "Behavior-preserving lift: CLI internal-static Run*Async bodies copied verbatim into Core, Console->progress?.Report, return-1->result-record"
    - "Single facade-implementing class is a deliberate temporary SRP compromise (impl split deferred); ISP honored at consumer boundary"

key-files:
  created:
    - DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs
  modified:
    - DeckFlow.Core/Knowledge/DistillationValidation.cs

key-decisions:
  - "Single ContentKbOrchestrator class implements the whole facade (not one class per slice) — relocation now, structural split later, to avoid mixing refactor into a pure lift"
  - "SetSourceEnabled success uses Outcome=Added (no dedicated enum member); Success drives the exit code so CLI parity holds"

patterns-established:
  - "Core orchestrator is Console-free + Serilog-free; 24 live lines flow through synchronous IOrchestratorProgress"
  - "Spend-record-before-next-call ordering (HIGH-1/FIX-1) preserved verbatim incl. Why comment"

requirements-completed: [ORCH-01]

duration: 14min
completed: 2026-06-13
---

# Phase 42-02: ContentKbOrchestrator Domain Lift Summary

**ContentKbOrchestrator (1305 LOC) implements the full facade by lifting the CLI Run*Async domain bodies into Core verbatim — Console swapped for the synchronous progress sink, exit codes swapped for null-safe result records — plus the all-zero-timestamp clip rule + cost math + distill constants consolidated into DistillationValidation (D-07).**

## Performance
- **Duration:** ~14 min (Codex gpt-5.4)
- **Tasks:** 2
- **Files:** 1 created, 1 modified

## Accomplishments
- DistillationValidation.cs: added ValidateClips all-zero-timestamp rule (was MISSING in Core, present in CLI), ValidateTranscriptLength, EstimateTokenCount, ComputeProjectedVideoCostUsd/CallCostUsd, and distill constants (ShortVideoMaxDuration, *MaxOutputTokens, MaxTranscriptInputTokens=120_000, DistillationCallCount=3, 4 distill-status strings). Additions-only diff — no existing message/threshold touched.
- ContentKbOrchestrator.cs: Distill/Harvest/Block/Unblock/CorpusReset/ListBlocked/AddSource/SetSourceEnabled/ExportIndex all lifted. Ctor-injected interface stores/services + Func<DateTimeOffset> + ContentKbOrchestratorOptions (no bare-string artifactRoot) + optional ILogger→NullLogger.

## Task Commits
1. **Task 1: consolidate validators + constants** — `72aa62a` (refactor)
2. **Task 2: ContentKbOrchestrator domain lift** — `48182a0` (refactor)

## Decisions Made
- Single class implements whole facade — deliberate temporary impl-SRP compromise, recorded for a later cleanup phase.
- HandleContentSourceUniqueViolationAsync now takes IContentSourceStore (interface) instead of the CLI concrete store — same body.

## Reviewer Parity Verification (Claude)
- ValidateClips condition ORDER identical to CLI (count → negative → all-zero) — same exception thrown first on overlap.
- DistillVideoAsync spend ordering identical: WouldExceedCap(summary)→Summarize→RecordCall→WouldExceedCap(clips)→ExtractClips→RecordCall→WouldExceedCap(tags)→InferTags. "record cost BEFORE next call" Why comment (HIGH-1/FIX-1) intact.
- AddSourceAsync invalid-type short-circuit is the first returnable branch (line 103), touches no store on that path.
- Console-free / Serilog-free / IProgress-free greps clean; 24 progress?.Report calls.
- Full behavioral parity (exit codes, byte-identical export) is pinned by the Wave 3 anchor + Wave 4 parity/golden tests.

## Deviations from Plan
None functional — all adaptations dictated by the Wave-1 contracts (interface-store injection, Console→progress, ListBlocked returns items).

## Verification
- `dotnet build DeckFlow.sln -warnaserror` → 0 errors / 0 warnings (no CS0535 — facade fully implemented).

## Next Phase Readiness
- Wave 3 (42-03) can rewire ContentKbCommandRunners into thin adapters over IContentKbOrchestrator and add AddContentKbOrchestrator() DI.

---
*Phase: 42-orchestrator-extraction*
*Completed: 2026-06-13*
