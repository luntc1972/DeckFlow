---
phase: 57-admin-surface-distill-quality
plan: 02
subsystem: testing
tags: [csharp, dotnet10, prompt-engineering, xunit]

# Dependency graph
requires: []
provides:
  - "Reworked distill system prompts for paste-ready summaries, stronger KEEP gating, on-topic clips, and dominant-topic tag parsimony"
  - "Refreshed prompt regression fixture with ClassificationSystemPrompt coverage"
affects: [phase-58, distillation, content-kb]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Prompt-only distillation tuning under unchanged JSON schema and validation contracts"]

key-files:
  created: [.planning/phases/57-admin-surface-distill-quality/57-02-SUMMARY.md]
  modified: [DeckFlow.Core/Knowledge/DistillationSchemas.cs, DeckFlow.Core.Tests/DistillationPromptRegressionTests.cs]

key-decisions:
  - "Kept all schema constants, FormatAllowlist, BuildInstruction, and DistillationValidation byte-identical while reworking prompt prose only."
  - "Added a ClassificationSystemPrompt regression fixture and assertion in the existing prompt test to close the uncovered classification surface."

patterns-established:
  - "Prompt quality changes for distillation stay in system-prompt prose while schema and validator contracts remain fixed."
  - "Prompt regression fixtures should pin every shipped system prompt, not just a subset."

requirements-completed: [DIST-01]

# Metrics
duration: not tracked
completed: 2026-06-18
---

# Phase 57 Summary

**Distill prompt prose now targets paste-ready Commander deckbuilding knowledge while preserving the shipped JSON and validation contract**

## Performance

- **Duration:** not tracked
- **Started:** not tracked
- **Completed:** 2026-06-18T18:32:26-06:00
- **Tasks:** 1
- **Files modified:** 2

## Accomplishments

- Reworked `SummarySystemPrompt` to target paste-ready AI-chatbot deckbuilding summaries, emphasize card names and decision heuristics, and exclude plot/host/sponsor recap while keeping the 200-word cap instruction.
- Strengthened `ClassificationSystemPrompt` KEEP guidance for any transcript with at least one substantial deckbuilding lesson while preserving DROP criteria and the "when in doubt, keep" fallback.
- Tightened `ClipsSystemPrompt` and `TagsSystemPrompt` around on-topic clip selection, dominant-topic tag parsimony, advisory tag caps, and the per-dimension fallback floor without touching schemas or validators.
- Refreshed `SystemPrompts_MatchShippedPhase21Fixtures` and added classification prompt coverage while leaving `ResponseFormatSchemas_MatchShippedPhase21Fixtures` unchanged.

## Task Commits

Each task was committed atomically:

1. **Task 1: Rework the four distill system prompts and realign the regression fixture** - `00c3bc7` (refactor)

## Files Created/Modified

- `.planning/phases/57-admin-surface-distill-quality/57-02-SUMMARY.md` - Execution summary for this plan.
- `DeckFlow.Core/Knowledge/DistillationSchemas.cs` - Prompt prose rework for summary, classification, clips, and tags.
- `DeckFlow.Core.Tests/DistillationPromptRegressionTests.cs` - Refreshed prompt fixtures and added classification prompt assertion.

## Decisions Made

- Kept raw-string delimiters, schema constants, `FormatAllowlist`, `DistillationValidation`, and `CliLlmDistillationService.BuildInstruction()` unchanged to preserve the shipped contract and satisfy `CarveOutGuard`.
- Kept clip-count guidance at `3 to 8` and expressed tag count limits only in prompt prose, not in `TagsSchema`.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- `dotnet build` reported 2 existing XML-doc warnings outside the task scope, but 0 errors. No action taken per plan scope.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 58 dogfood can evaluate the before/after quality of the shipped prompts on real harvested content.
- The prompt contract guards remain in place: schema fixtures unchanged, `CarveOutGuard` passing, and filtered distillation tests green.

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` -> succeeded with 0 errors, 2 warnings.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-build --filter "Distillation|CarveOutGuard"` -> Passed: 51, Failed: 0, Skipped: 0.
- `grep -ic 'paste' DeckFlow.Core/Knowledge/DistillationSchemas.cs` -> `1`
- `grep -in 'heuristic\|specific card\|named\|reason' DeckFlow.Core/Knowledge/DistillationSchemas.cs` -> hits in classification and clips prompt text
- `grep -ic 'dominant' DeckFlow.Core/Knowledge/DistillationSchemas.cs` -> `2`
- `grep -c 'FormatAllowlist(ContentTagVocabulary' DeckFlow.Core/Knowledge/DistillationSchemas.cs` -> `3`
- `grep -c '3 to 8' DeckFlow.Core/Knowledge/DistillationSchemas.cs` -> `1`

---
*Phase: 57-admin-surface-distill-quality*
*Completed: 2026-06-18*
