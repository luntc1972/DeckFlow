---
phase: 96-stated-rules-distiller
plan: 07
subsystem: testing
tags: [dotnet, core, cli, stated-rules, golden-tests]
requires:
  - phase: 96-stated-rules-distiller
    provides: four ILlmDistillationService stated-rules stages from 96-04
  - phase: 96-stated-rules-distiller
    provides: ICardNameGrounder grounding seam from 96-02 and Web implementation from 96-06
provides:
  - StatedRulesExtractor multi-pass coordinator over chunking, staged distillation, deterministic dedupe, optional grounding, and validation
  - Unit coverage for dedupe, ambiguity drop, grounding rewrite, grounding miss, null grounder, and null card-reference pass-through
  - Golden CLI seam regression over a representative Salubrious Snail transcript fixture with canonical card-name rewrite
affects: [phase-96, stated-rules, cli-service, grounding, golden-regression]
tech-stack:
  added: []
  patterns: [pure Core coordinator over injected seams, canned CLI queue golden harness, per-call card-name grounding cache]
key-files:
  created: [DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRulesExtractor.cs, DeckFlow.Core.Tests/StatedRulesExtraction/StatedRulesExtractorTests.cs, DeckFlow.Core.Tests/StatedRulesExtraction/CliLlmDistillationStatedRulesGoldenTests.cs, DeckFlow.Core.Tests/StatedRulesExtraction/Fixtures/salubrious-snail-transcript.txt, .planning/phases/96-stated-rules-distiller/96-07-SUMMARY.md]
  modified: [DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj]
key-decisions:
  - "Kept StatedRulesExtractor pure Core and structural: grounding only looks at non-null CardReference values and never rescans SourceClip or Condition."
  - "Copied the Snail transcript fixture to test output via a minimal additive csproj include because Core.Tests had no existing glob that would place the new .txt beside the test binaries."
patterns-established:
  - "Extractor sequencing is fixed: chunk, per-chunk Select/Disambiguate/Decompose, LLM reduce, deterministic dedupe, optional grounding, validate."
  - "Golden stated-rules regressions can reuse the existing CliLlmDistillationService process-runner seam with queued Claude envelopes and a fake grounder."
requirements-completed: [CS-11, CS-12, CS-15]
duration: 6min
completed: 2026-07-12
---

# Phase 96: Stated-Rules Distiller Summary

**Stated-rules extraction now runs end-to-end through a pure Core coordinator with deterministic grounding and a Snail golden regression over the CLI seam**

## Performance

- **Duration:** 6min
- **Started:** 2026-07-12T17:17:00Z
- **Completed:** 2026-07-12T17:23:09Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- Added `StatedRulesExtractor` with the locked six-step sequence and per-call card-grounding cache.
- Added focused unit tests covering cross-chunk dedupe, ambiguity drop, null grounder pass-through, canonical rewrite, unresolved keep+flag, and null card-reference pass-through.
- Added a deterministic CLI golden test and representative Snail transcript fixture proving validated range, cap, and grounded card-reference outputs without a live subprocess or network call.

## Task Commits

No git commits were created. The plan hard rule forbade git operations, and no git commands were run.

## Files Created/Modified
- `DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRulesExtractor.cs` - Pure coordinator for chunking, staged distillation, deterministic dedupe, optional grounding, and final validation.
- `DeckFlow.Core.Tests/StatedRulesExtraction/StatedRulesExtractorTests.cs` - Fake-seam unit coverage for the coordinator behaviors locked in the plan.
- `DeckFlow.Core.Tests/StatedRulesExtraction/CliLlmDistillationStatedRulesGoldenTests.cs` - Deterministic queued-envelope golden regression over the real CLI seam.
- `DeckFlow.Core.Tests/StatedRulesExtraction/Fixtures/salubrious-snail-transcript.txt` - LF-only representative Snail transcript fixture with timestamp markers, numeric prototype language, typoed card name, and an ambiguous sentence to drop.
- `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` - Minimal additive fixture copy rule so the transcript file is available at test runtime.
- `.planning/phases/96-stated-rules-distiller/96-07-SUMMARY.md` - Execution summary, verification evidence, and the csproj deviation note.

## Decisions Made

- Kept the extractor strictly pure-Core with no HTTP, RestSharp, or AspNet dependencies.
- Used a structural grounding rule only: non-null `CardReference` values are deduped, cached, grounded once per distinct name, then rewritten or flagged with record `with`.
- Added the minimal csproj fixture include because the project did not already copy `.txt` fixtures into the test output directory.

## Deviations from Plan

### Auto-fixed Issues

**1. [Execution - Fixture loading] Added a minimal Core.Tests csproj include for the new transcript file**
- **Found during:** Task 2 (golden regression fixture wiring)
- **Issue:** The new `.txt` fixture would not be present in `AppContext.BaseDirectory` at test runtime because the test project only explicitly copied one existing JSON fixture.
- **Fix:** Added one additive `<None Include=...>` entry with `CopyToOutputDirectory=PreserveNewest` and a `TargetPath` under `Fixtures/`.
- **Files modified:** `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`
- **Verification:** The targeted golden test loaded the transcript from `AppContext.BaseDirectory/Fixtures/salubrious-snail-transcript.txt` and passed.
- **Committed in:** None

---

**Total deviations:** 1 auto-fixed (fixture runtime availability only)
**Impact on plan:** No scope creep. The csproj edit was the smallest change needed to satisfy deterministic fixture loading.

## Issues Encountered

None beyond the fixture-copy requirement above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The orchestrator can now consume `StatedRulesExtractor` as the pure coordination unit for full stated-rules extraction.
- The phase gate now has a deterministic Snail golden regression covering ambiguity drop, grounding rewrite, and representative validated rules.

## Verification

- `dotnet.exe build DeckFlow.Core/DeckFlow.Core.csproj`
  PASS - `Build succeeded. 0 Warning(s) 0 Error(s).`
- `dotnet.exe test DeckFlow.Core.Tests --filter FullyQualifiedName~"StatedRulesExtractorTests|CliLlmDistillationStatedRulesGoldenTests"`
  PASS - `Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 78 ms.`

---
*Phase: 96-stated-rules-distiller*
*Completed: 2026-07-12*
