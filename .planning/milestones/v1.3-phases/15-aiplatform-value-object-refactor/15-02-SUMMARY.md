---
phase: 15
plan: "02"
subsystem: PromptBuilders
tags: [refactor, strategy-pattern, di, prompt-builders, ai-platform]
dependency_graph:
  requires: [15-01]
  provides: [AiPlatformPromptBuilderFamilies]
  affects: [DeckAnalysisPacketService, DeckComparisonService, MetaGapService, Program.cs]
tech_stack:
  added: []
  patterns: [Strategy Pattern per-AI-platform dispatch, IEnumerable<IXxx> DI auto-resolution]
key_files:
  created:
    - DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs
    - DeckFlow.Web/Services/PromptBuilders/SetUpgrade/ISetUpgradePromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/SetUpgrade/ChatGptSetUpgradePromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/SetUpgrade/ClaudeSetUpgradePromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/SetUpgrade/GeminiSetUpgradePromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/SetUpgrade/SetUpgradePromptVariantRegistry.cs
    - DeckFlow.Web/Services/PromptBuilders/Comparison/IComparisonPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Comparison/ChatGptComparisonPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Comparison/ClaudeComparisonPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Comparison/GeminiComparisonPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Comparison/ComparisonPromptVariantRegistry.cs
    - DeckFlow.Web/Services/PromptBuilders/FollowUp/IFollowUpPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/FollowUp/ChatGptFollowUpPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/FollowUp/ClaudeFollowUpPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/FollowUp/GeminiFollowUpPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/FollowUp/FollowUpPromptVariantRegistry.cs
    - DeckFlow.Web/Services/PromptBuilders/MetaGap/IMetaGapPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/MetaGap/ChatGptMetaGapPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/MetaGap/ClaudeMetaGapPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/MetaGap/GeminiMetaGapPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/MetaGap/MetaGapPromptVariantRegistry.cs
  modified:
    - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
    - DeckFlow.Web/Services/DeckComparisonService.cs
    - DeckFlow.Web/Services/MetaGapService.cs
    - DeckFlow.Web/Program.cs
    - DeckFlow.Web.Tests/ResultContractTests.cs
decisions:
  - "Helper classification: NormalizeSingleLine, ParseCardNameList, BuildComboReferenceText, FormatBannedCardsLine promoted to internal static on DeckAnalysisPacketService so variant classes can reference them without duplication"
  - "AppendPromptDeckSection and AppendComparisonPromptDeckXml promoted to internal static on DeckComparisonService; IndentJson and FallbackText also promoted for cross-variant access"
  - "BuildCompactDecklist, BuildCompactRefDecklist, BuildComboReferenceText promoted to internal static on MetaGapService"
  - "ResultContractTests updated to construct minimal inline registries instead of calling static methods; no live HTTP needed since registries are pure prompt builders"
  - "TypeScript build failure in worktree is expected (node_modules not installed); C# DLL produced successfully"
metrics:
  duration: "~3h"
  completed: "2026-05-18"
  tasks: 5
  files: 30
---

# Phase 15 Plan 02: Prompt-Builder Strategy Registry Extraction Summary

Extracted 5 AI-platform prompt-builder switch dispatchers into per-platform strategy registries, converting 1,600+ lines of switch-arm spaghetti into 25 isolated variant classes. Adding a 4th AI platform now requires only 3 new variant classes + 4 DI lines — zero edits to host services.

## What Was Built

Five prompt-builder families extracted from 3 host services:

| Family | Interface | Registry | Host Service |
|--------|-----------|----------|-------------|
| Analysis | IAnalysisPromptVariant | AnalysisPromptVariantRegistry | DeckAnalysisPacketService |
| SetUpgrade | ISetUpgradePromptVariant | SetUpgradePromptVariantRegistry | DeckAnalysisPacketService |
| Comparison | IComparisonPromptVariant | ComparisonPromptVariantRegistry | DeckComparisonService |
| FollowUp | IFollowUpPromptVariant | FollowUpPromptVariantRegistry | DeckComparisonService |
| MetaGap | IMetaGapPromptVariant | MetaGapPromptVariantRegistry | MetaGapService |

Each family: 1 interface + 3 sealed variant classes (ChatGpt/Claude/Gemini) + 1 registry = 5 files per family × 5 families = 25 files created.

## Pattern Applied

All 5 registries follow the same shape:
- Constructor: `IEnumerable<IXxxPromptVariant> variants` → `variants.ToDictionary(v => v.Platform)`
- Dispatch: `TryGetValue(platform, out var found) ? found : _variants[AiPlatform.Default]`
- DI: `AddSingleton<IXxxPromptVariant, PlatformXxxPromptVariant>()` × 3 + `AddSingleton<XxxPromptVariantRegistry>()`

No static `XxxPromptVariantRegistry.Default` shim anywhere (D-02 LOCKED honored). All 5 dispatcher methods are instance methods (BLOCKER 2 honored).

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 | 2cc10f4 | Extract Analysis prompt-builder family |
| 2 | ee4f0dd | Extract SetUpgrade prompt-builder family |
| 3a | 88d80af | Extract Comparison prompt-builder family |
| 3b | ca18fd2 | Extract FollowUp prompt-builder family |
| 4 | 3192667 | Extract MetaGap prompt-builder family |
| 5 | f5ba5f3 | Register 15 variants + 5 registries in DI |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Missing `using DeckFlow.Web.Services;` in Analysis family**
- **Found during:** Task 1 pre-commit review
- **Issue:** `IAnalysisPromptVariant.cs` and `AnalysisPromptVariantRegistry.cs` referenced `CommanderSpellbookResult` (namespace `DeckFlow.Web.Services`) but only had `using DeckFlow.Web.Models;`
- **Fix:** Added `using DeckFlow.Web.Services;` to both files
- **Files modified:** IAnalysisPromptVariant.cs, AnalysisPromptVariantRegistry.cs
- **Commit:** 2cc10f4

**2. [Rule 1 - Bug] ResultContractTests called static methods that became instance methods**
- **Found during:** Task 5
- **Issue:** All 5 service dispatch methods changed from `internal static` to `internal` instance methods, so the tests' direct static calls (`DeckAnalysisPacketService.BuildAnalysisPrompt(...)` etc.) would not compile
- **Fix:** Rewrote tests to construct minimal inline registries from variant arrays and call `registry.Build(AiPlatform.Normalize(platform), ...)` — no live HTTP/DI required since registries are pure prompt builders
- **Files modified:** DeckFlow.Web.Tests/ResultContractTests.cs
- **Commit:** f5ba5f3

## Build Verification

C# compilation: **CLEAN** — both `DeckFlow.Core.dll` and `DeckFlow.Web.dll` produced with 0 C# errors.

TypeScript error (`Cannot find module typescript/bin/tsc`) is a known worktree environment issue: `npm install` has not been run in the worktree's `DeckFlow.Web/node_modules`. This is not a code error — the worktree is isolated from the main checkout's `node_modules`. CI/CD uses Docker with explicit `npm install` step.

## Known Stubs

None — all variant Build bodies are byte-for-byte copies of the pre-refactor switch arms. No placeholder or stub content introduced.

## Threat Flags

None — pure refactor. No new network endpoints, auth paths, file access patterns, or schema changes. All new types are `internal sealed` classes inaccessible outside the assembly.

## Self-Check: PASSED

Verified:
- 25 new files exist in `DeckFlow.Web/Services/PromptBuilders/{Analysis,SetUpgrade,Comparison,FollowUp,MetaGap}/`
- 6 commits present: 2cc10f4, ee4f0dd, 88d80af, ca18fd2, 3192667, f5ba5f3
- DeckAnalysisPacketService.cs: `BuildAnalysisPrompt` and `BuildSetUpgradePrompt` are instance methods
- DeckComparisonService.cs: `BuildComparisonPrompt` and `BuildFollowUpPrompt` are instance methods
- MetaGapService.cs: `BuildPrompt` is instance method
- Program.cs: 20 DI registrations added (15 variants + 5 registries)
- ResultContractTests.cs: all test helpers use inline registry construction
