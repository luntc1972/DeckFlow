---
phase: 15-aiplatform-value-object-refactor
plan: "01"
subsystem: ai-platform-value-object
tags:
  - value-object
  - refactor
  - models
  - razor
  - tests
dependency_graph:
  requires: []
  provides:
    - AiPlatform sealed record value object (DeckFlow.Web.Models)
    - AiPlatform.All single source of truth for AI platform set
    - AiPlatform.Normalize defensive guard for string→platform resolution
  affects:
    - DeckFlow.Web/Models/DeckAnalysisRequest.cs
    - DeckFlow.Web/Models/DeckComparisonRequest.cs
    - DeckFlow.Web/Models/MetaGapRequest.cs
    - DeckFlow.Web/Views/Shared/_AiSelector.cshtml
    - DeckFlow.Web/Services/PacketArtifactStore.cs
    - DeckFlow.Web.Tests/AiPlatformPhase10RoundTripTests.cs
tech_stack:
  added:
    - DeckFlow.Web/Models/AiPlatform.cs (new file — sealed record value object)
  patterns:
    - Sealed record with static registry (All, Default, Normalize)
    - StringComparison.Ordinal for case-sensitive key matching
    - MemberData(AllPlatforms) for SC5-forward-compatible theory data source
key_files:
  created:
    - DeckFlow.Web/Models/AiPlatform.cs
  modified:
    - DeckFlow.Web/Models/DeckAnalysisRequest.cs
    - DeckFlow.Web/Models/DeckComparisonRequest.cs
    - DeckFlow.Web/Models/MetaGapRequest.cs
    - DeckFlow.Web/Views/Shared/_AiSelector.cshtml
    - DeckFlow.Web/Services/PacketArtifactStore.cs
    - DeckFlow.Web.Tests/AiPlatformPhase10RoundTripTests.cs
decisions:
  - "D-01 (from CONTEXT.md): AiPlatform record is data-only (Key/DisplayName/Description) — no Enabled, no ResponseExtractor, no Strategy"
  - "D-06 preservation: ChatGPT/Claude/Gemini Key literals unchanged; TargetAiPlatform property name unchanged; name=TargetAiPlatform form field unchanged; chatgpt zip filename fallback unchanged"
  - "AllForTesting seam deliberately omitted from AiPlatform.cs — Plan 15-03 territory"
  - "Razor continue-guard approach chosen for Gemini conditional: if (key == Gemini && !enabled) { continue; } inside foreach"
metrics:
  duration: "~5 minutes"
  completed: "2026-05-18"
  tasks_completed: 5
  tasks_total: 5
  files_modified: 6
  files_created: 1
  commits: 5
---

# Phase 15 Plan 01: AiPlatform Value Object + String-Touchpoint Migration Summary

AiPlatform sealed record value object landed with All/Default/Normalize; all 6 string-touchpoint sites (3 DTO setters, Razor partial, 3 zip-load assigns) migrated to use it; test suite migrated to MemberData(AllPlatforms) driven from AiPlatform.All.

## Tasks Completed

| Task | Description | Commit | Files |
|------|-------------|--------|-------|
| 1 | Create AiPlatform sealed record value object | c24e66a | DeckFlow.Web/Models/AiPlatform.cs (+66 lines) |
| 2 | Migrate 3 request DTO setters to AiPlatform.Normalize | 216b114 | DeckAnalysisRequest.cs, DeckComparisonRequest.cs, MetaGapRequest.cs |
| 3 | Migrate _AiSelector.cshtml to iterate AiPlatform.All | a01fba4 | DeckFlow.Web/Views/Shared/_AiSelector.cshtml |
| 4 | Defensive AiPlatform.Normalize at 3 zip-load sites | 57400bf | DeckFlow.Web/Services/PacketArtifactStore.cs |
| 5 | Migrate round-trip tests to MemberData(AllPlatforms) | 0756bc8 | DeckFlow.Web.Tests/AiPlatformPhase10RoundTripTests.cs |

## Build Status

`dotnet build DeckFlow.sln --configuration Release` — PASS, 0 warnings, 0 errors.

Build verification note: The worktree does not have `DeckFlow.Web/node_modules/` checked in. A Windows directory junction to the main repo's `DeckFlow.Web/node_modules/` was created (not committed) to allow the TypeScript MSBuild target to run. This is a worktree-local setup step. CI will use the containerized build with `npm install` in the Docker layer and is unaffected.

## Preservation Invariants Verified

- `"ChatGPT" or "Claude" or "Gemini"` switch arm pattern: **gone** from all 3 DTOs (grep returns 0 matches)
- `AiPlatform.Normalize` active usages: 6 (3 setter sites + 3 zip-load sites)
- `"chatgpt"` zip filename fallback: preserved in 3 `SuggestZipFileName` helpers (D-06)
- `name="TargetAiPlatform"` form field in Razor partial: preserved
- `class="sr-only ai-selector__option"` and `class="ai-selector__option-label"`: preserved
- `aria-label="AI analysis target"`: preserved
- `DECKFLOW_GEMINI_ENABLED` gate still wraps Gemini option render only (not the loop)
- `AllForTesting` seam: NOT present (Plan 15-03 responsibility — D-01 clean surface)
- 5 prompt-builder switch arms in DeckAnalysisPacketService/DeckComparisonService/MetaGapService: UNCHANGED (Plan 15-02 territory)

## Test-Count Preservation (W9 Binding)

Pre-migration `AiPlatformPhase10` test count: **64**
Post-migration `AiPlatformPhase10` test count: **64**

No test cases lost. The 3 triple-platform `[InlineData("ChatGPT"/"Claude"/"Gemini")]` Theory groups were migrated to `[MemberData(nameof(AllPlatforms))]`. The `AllPlatforms()` source iterates `AiPlatform.All` — adding a 4th platform automatically extends all 3 Theory matrices without test edits (SC5 forward-compat invariant).

Boundary normalization tests (`_normalizes_invalid_*`) preserved as `[Fact]` — they test a specific out-of-set input, not the round-trip matrix.

## Deviations from Plan

None — plan executed exactly as written. The worktree node_modules setup is an executor-environment concern, not a code deviation.

## Plan 15-02 Unblocked

Plan 15-02 (variant extraction + registries + DI) is unblocked. `AiPlatform` is now the single source of truth for the platform set; the 5 prompt-builder switches in DeckAnalysisPacketService/DeckComparisonService/MetaGapService are intact for extraction.

## Threat Surface Scan

No new network endpoints, auth paths, file access patterns, or schema changes. The `AiPlatform.Normalize` guard strengthens the existing Phase 10 T-15-01 (Tampering) defense — crafted zip with arbitrary `target_ai_platform` value is now defensively normalized at both the zip-load layer AND the setter layer (depth-in-defense). No new attack surface introduced.

## Self-Check: PASSED

| Check | Result |
|-------|--------|
| DeckFlow.Web/Models/AiPlatform.cs exists | FOUND |
| .planning/phases/.../15-01-SUMMARY.md exists | FOUND |
| Commit c24e66a (Task 1) | FOUND |
| Commit 216b114 (Task 2) | FOUND |
| Commit a01fba4 (Task 3) | FOUND |
| Commit 57400bf (Task 4) | FOUND |
| Commit 0756bc8 (Task 5) | FOUND |
