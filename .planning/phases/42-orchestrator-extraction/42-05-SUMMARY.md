---
phase: 42-orchestrator-extraction
plan: 05
subsystem: testing
tags: [parity-tests, golden-fixture, json, content-kb, orchestration]

requires:
  - phase: 42-orchestrator-extraction
    provides: ContentKbOrchestrator + shared Fake*/Throwing* doubles + exposed CLI SerializeContentIndexExportRows
provides:
  - exit-code/output parity tests (AddSource outcomes, ListBlocked tab format, corpus-reset dry-run, metered-distill refusal) — HIGH-3 closed
  - byte-identical content-index-export JSON golden-fixture test through the real CLI serializer — HIGH-4 closed
affects: []

tech-stack:
  added: []
  patterns:
    - "Parity tests assert orchestrator RESULT records the CLI maps to exit 0/1/2/3 (mapping stays in CLI, not re-implemented in Core)"
    - "Golden test serializes through the real CLI SerializeContentIndexExportRows (no fork) and normalizes newlines so it pins JSON shape, not platform Environment.NewLine"

key-files:
  created:
    - DeckFlow.Core.Tests/Orchestration/ContentSourceOrchestratorParityTests.cs
    - DeckFlow.Core.Tests/Orchestration/ContentMaintenanceOrchestratorParityTests.cs
    - DeckFlow.Core.Tests/Orchestration/DistillOrchestratorParityTests.cs
    - DeckFlow.Core.Tests/Orchestration/ContentIndexExportJsonGoldenTests.cs
    - DeckFlow.Core.Tests/Orchestration/Fixtures/index-seed.golden.json
  modified:
    - DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj

key-decisions:
  - "Metered-refusal test passes isSubscriptionProvider:FALSE (the !dryRun && !isSubscriptionProvider case) — fixes the round-2 HIGH-3 flag where the prior plan used the wrong flag value"
  - "Golden test is newline-agnostic (normalizes CRLF->LF on both sides). The production serializer (System.Text.Json WriteIndented) emits Environment.NewLine = CRLF on Windows / LF on Linux; that platform artifact is irrelevant to JSON shape and would otherwise break the test under .gitattributes LF normalization. Serializer NOT modified (it preserves today's behavior)."

patterns-established:
  - "Local named test doubles (RecordingContentSourceStore, StubBlockedVideoStore, RecordingDeleteAll*Store) live in the parity test file when the shared 42-03 fakes lack the needed recording/enumeration surface"

requirements-completed: [ORCH-02]

duration: 18min
completed: 2026-06-13
---

# Phase 42-05: Orchestrator Parity + JSON Golden Tests Summary

**8 new tests make the behavior-preserving extraction enforceable: AddSource invalid-type/same-url/slug-conflict outcomes, ListBlocked tab format, corpus-reset dry-run, and the metered-provider distill refusal are pinned to the orchestrator RESULT records the CLI maps to exit codes (HIGH-3); the content-index-export JSON is pinned byte-for-byte (newline-normalized) to a committed golden fixture through the real CLI serializer (HIGH-4). Core.Tests 330/330.**

## Performance
- **Duration:** ~18 min (Codex gpt-5.4, incl. 1 corrective newline pass)
- **Tasks:** 2 + newline-fragility corrective
- **Files:** 5 created, 1 modified (test csproj)

## Accomplishments
- ContentSourceOrchestratorParityTests: InvalidType (exact message + no store insert), AlreadyExistsSameUrl (exit-0), SlugConflict (exit-3), Added happy-path.
- ContentMaintenanceOrchestratorParityTests: ListBlocked `id\t{BlockedUtc:O}\t{reason}` projection; corpus-reset dry-run (DryRun=true, 0 deletions).
- DistillOrchestratorParityTests: metered refusal (isSubscriptionProvider:false, dryRun:false) → Success=false + exact line-412 AbortedReason, no distill writes.
- ContentIndexExportJsonGoldenTests + index-seed.golden.json: real CLI SerializeContentIndexExportRows path, null-handling + distinct natural-key rows, newline-normalized ordinal compare.

## Task Commits
1. **Task 1: parity tests** — `09d9cae` (test)
2. **Task 2: golden fixture test** — `26fb7c2` (test)
3. **Corrective: newline-agnostic golden + LF fixture** — `829054d` (test)

## Decisions Made
- Local named fakes added where shared fakes lacked surface: RecordingContentSourceStore (insert/unique-violation shaping), StubBlockedVideoStore + RecordingDeleteAll*Store (enumeration + delete-all recording).
- Golden test normalizes newlines; production serializer untouched (preserves today's platform behavior). Fixture stored LF per .gitattributes.

## Reviewer Verification (Claude)
- Caught the CRLF golden fixture as a cross-platform fragility (would break on Windows-dotnet after .gitattributes LF normalization / on CI). Dispatched corrective: newline-normalized compare + LF fixture. Re-verified.
- Golden test uses the real CLI serializer (not a fork) — drift in property order/camelCase/indentation/null/row-order/trailing-newline still fails the test.

## Verification
- `dotnet build DeckFlow.sln -warnaserror` → 0 errors / 0 warnings.
- `dotnet test DeckFlow.Core.Tests` → Passed 330 / Failed 0 / Total 330 (322 prior + 8 new).

## Next Phase Readiness
- Phase 42 complete: orchestrator extracted to Core, CLI thin, Studio consumes a slice (no CLI ref), parity + golden enforced.

---
*Phase: 42-orchestrator-extraction*
*Completed: 2026-06-13*
