---
phase: 77-multi-axis-deck-score
plan: 03
subsystem: analysis
tags: [prompt-variants, score, adr-0001, parity-test, deck-analysis]

# Dependency graph
requires:
  - phase: (existing Web)
    provides: IAnalysisPromptVariant contract + AnalysisPromptVariantRegistry + three concrete variants (ChatGpt/Claude/Gemini) + companionName trailing-optional-param pattern
provides:
  - scoreBlockText trailing optional param on IAnalysisPromptVariant.Build and AnalysisPromptVariantRegistry.Build
  - per-variant hand-edited score-block insertion (ADR-0001, no shared helper)
  - AnalysisScorePromptParityTests (present / OFF-path byte-identity / four-axis figures-match)
affects: [77-04 (supplies the pre-built scoreBlockText via BuildScoreBlockText + wires it into BuildAnalysisPrompt)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Trailing optional string? param threaded through contract -> registry -> each variant (mirrors companionName)"
    - "Per-variant hand-edited guard block (ADR-0001): each variant owns its own insertion position; no shared prose helper"
    - "OFF-path byte-identity proven by excision-equality (not marker-absence) parity test"

key-files:
  created:
    - DeckFlow.Web.Tests/AnalysisScorePromptParityTests.cs
  modified:
    - DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs
    - DeckFlow.Web.Tests/AiPlatformExtensionTests.cs

key-decisions:
  - "scoreBlockText is added as the LAST trailing optional param (string? scoreBlockText = null), mirroring the existing companionName analog, so all call sites stay source-compatible"
  - "Each variant inserts its own guard `if (!string.IsNullOrWhiteSpace(scoreBlockText)) { AppendLine(); AppendLine(scoreBlockText); }` at its own chosen position (ChatGPT/Gemini: after `## DECK CONTEXT`, before `## EVIDENCE RULES`; Claude: after the `<commander>` block) — ADR-0001 forbids a shared helper"
  - "The variant does NOT build the block text — the caller (plan 77-04) supplies the pre-built string; this plan only threads + inserts it"
  - "OFF-path byte-identity is proven by an excision-equality test (the inserted contiguous block = Environment.NewLine + scoreBlockText + Environment.NewLine appears exactly once and, removed, yields the null-path output) rather than mere DECK-SCORE marker absence (Codex HIGH)"

patterns-established:
  - "Pattern: thread a new optional artifact-section string through the analysis prompt contract + registry + all three variants, hand-edited per ADR-0001"
  - "Pattern: parity test that asserts present + figures-survive + OFF-path byte-identity across all three platforms"

metrics:
  duration: ~12 min
  completed: 2026-06-29
---

# Phase 77 Plan 03: Score Block Through Analysis Prompt Variants Summary

Threaded a pre-built `string? scoreBlockText` argument through the analysis prompt contract, the registry, and all three platform variants (ChatGpt/Claude/Gemini), each hand-editing its own insertion point per ADR-0001 (no shared helper). Added a 3-platform parity test proving the score block is present when supplied, all four axis figures survive into each variant, and the null path is byte-identical to the with-score output minus the contiguous block.

## What Was Built

### Task 1 — Thread scoreBlockText through contract + registry + three variants (`13da3df3`)
- `IAnalysisPromptVariant.Build`: added `string? scoreBlockText = null` as the last optional param, with a `<param name="scoreBlockText">` XML doc mirroring the `companionName` doc.
- `AnalysisPromptVariantRegistry.Build`: added the same trailing param and passed it through on the variant dispatch line.
- `ChatGptAnalysisPromptVariant` / `GeminiAnalysisPromptVariant`: added the param and a guard block inserting `scoreBlockText` after `## DECK CONTEXT`, before `## EVIDENCE RULES`.
- `ClaudeAnalysisPromptVariant`: added the param and a guard block inserting `scoreBlockText` immediately after the `<commander>` block.
- Each guard is exactly `if (!string.IsNullOrWhiteSpace(scoreBlockText)) { builder.AppendLine(); builder.AppendLine(scoreBlockText); }` — hand-written per file, no shared helper (ADR-0001).
- `dotnet.exe build DeckFlow.Web`: 0 warnings, 0 errors.

### Task 2 — AnalysisScorePromptParityTests (`7418a255`)
- `Score_Block_AppearsInAllThreeVariants(platform)`: builds with a "DECK SCORE …" block → `Assert.Contains("DECK SCORE", …, Ordinal)` across all three platforms.
- `Score_NullPath_ByteIdenticalToExcisedScorePath(platform)`: builds once with a unique sentinel and once with `scoreBlockText: null`; asserts the inserted contiguous block (`Environment.NewLine + sentinel + Environment.NewLine`) appears exactly once and, excised, the remainder is byte-identical to the null-path output. This proves OFF-path byte identity per variant, not mere marker absence.
- `Score_AllFourAxisFigures_MatchAcrossAllThreeVariants(platform)`: builds with a block carrying all four axes and asserts every one of `Power: 4/5`, `Speed: 3/5`, `Control: 2/5`, `Consistency: 5/5` is present (Ordinal) in each variant.
- 9 tests (3 × 3 platforms) GREEN.

## Verification

- `dotnet.exe build DeckFlow.Web`: 0 warnings, 0 errors.
- `dotnet.exe build DeckFlow.Web.Tests`: 0 warnings, 0 errors.
- `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~AnalysisScorePromptParity"`: 9 passed, 0 failed.
- Full `dotnet.exe test DeckFlow.Web.Tests`: 995 passed, 12 skipped (Postgres integration), 0 failed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated StubTestAnalysisVariant for the new contract param**
- **Found during:** Task 2 (DeckFlow.Web.Tests build)
- **Issue:** `AiPlatformExtensionTests.StubTestAnalysisVariant` implements `IAnalysisPromptVariant` and no longer satisfied the interface after `scoreBlockText` was added (CS0535).
- **Fix:** Added `string? scoreBlockText = null` to the stub's `Build` signature.
- **Files modified:** DeckFlow.Web.Tests/AiPlatformExtensionTests.cs
- **Commit:** `7418a255`

## Known Stubs

None — this plan only threads + inserts a caller-supplied string. The pre-built `scoreBlockText` source (`BuildScoreBlockText`) and its wiring into `BuildAnalysisPrompt` land in plan 77-04; until then the param defaults to `null` and the OFF path is byte-identical (guarded by the parity test).

## Threat Flags

None — variants emit only a pre-built server-side plaintext string; no new external input crosses a trust boundary (per the plan's threat register, T-77-03-01 disposition = accept, byte-identity guarded by the parity test).

## Self-Check: PASSED
- FOUND: DeckFlow.Web.Tests/AnalysisScorePromptParityTests.cs
- FOUND commit 13da3df3
- FOUND commit 7418a255
