---
phase: 13
plan: 13-01
wave: 1
subsystem: web-models
tags: [refactor, rename, xml-docs, deck-analysis, deck-comparison, meta-gap]
requirements: [CLASSRENAME-01, CLASSRENAME-02, CLASSRENAME-03]
requires: []
provides:
  - DeckFlow.Web.Models.DeckAnalysisRequest
  - DeckFlow.Web.Models.DeckAnalysisViewModel
  - DeckFlow.Web.Models.DeckAnalysisResponse
  - DeckFlow.Web.Models.WeakSlot
  - DeckFlow.Web.Models.QuestionAnswer
  - DeckFlow.Web.Models.DeckVersion
  - DeckFlow.Web.Models.SetUpgradeResponse
  - DeckFlow.Web.Models.SetUpgradeSet
  - DeckFlow.Web.Models.SetUpgradeTopAdd
  - DeckFlow.Web.Models.SetUpgradeCardNote
  - DeckFlow.Web.Models.SetUpgradeShortlist
  - DeckFlow.Web.Models.DeckComparisonRequest
  - DeckFlow.Web.Models.DeckComparisonViewModel
  - DeckFlow.Web.Models.DeckComparisonResponse
  - DeckFlow.Web.Models.DeckComparisonRecommendation
  - DeckFlow.Web.Models.MetaGapRequest
  - DeckFlow.Web.Models.MetaGapViewModel
  - DeckFlow.Web.Models.MetaGapResponse
  - DeckFlow.Web.Models.MetaGapData
  - DeckFlow.Web.Models.WinLineSet
  - DeckFlow.Web.Models.WinLines
  - DeckFlow.Web.Models.Interaction
  - DeckFlow.Web.Models.Speed
  - DeckFlow.Web.Models.ManaEfficiency
  - DeckFlow.Web.Models.CoreConvergenceCard
  - DeckFlow.Web.Models.MissingStaple
  - DeckFlow.Web.Models.PotentialCut
  - DeckFlow.Web.Models.TopAdd
  - DeckFlow.Web.Models.TopCut
  - DeckPageTab.DeckAnalysis (=5)
  - DeckPageTab.DeckComparison (=8)
  - DeckPageTab.CedhMetaGap (=9)
affects:
  - DeckFlow.Web/Controllers/DeckController.cs (Wave 3 — references all renamed types)
  - DeckFlow.Web/Services/ChatGptDeckPacketService.cs (Wave 2 — produces DeckAnalysisResponse/SetUpgradeResponse)
  - DeckFlow.Web/Services/ChatGptDeckComparisonService.cs (Wave 2 — produces DeckComparisonResponse)
  - DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs (Wave 2 — produces MetaGapResponse)
  - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml (Wave 3 — @model directive)
  - DeckFlow.Web/Views/Deck/DeckComparison.cshtml (Wave 3 — @model directive)
  - DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml (Wave 3 — @model directive)
  - DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml (Wave 3 — DeckPageTab enum branches)
  - DeckFlow.Web.Tests/* (Wave 4 — fixture types)
tech-stack:
  added: []
  patterns:
    - Pattern 2: sealed-class request DTO with property-level XML summaries
    - Pattern 3: sealed-class Razor view model with init-only properties and XML summaries
    - Pattern 4: sealed class with nested response shapes, one file per response triplet
    - Pattern 7: flat enum, PascalCase values, explicit integer values (no doc comments)
key-files:
  created:
    - DeckFlow.Web/Models/DeckAnalysisRequest.cs
    - DeckFlow.Web/Models/DeckAnalysisViewModel.cs
    - DeckFlow.Web/Models/DeckAnalysisResponse.cs
    - DeckFlow.Web/Models/SetUpgradeResponse.cs
    - DeckFlow.Web/Models/DeckComparisonRequest.cs
    - DeckFlow.Web/Models/DeckComparisonViewModel.cs
    - DeckFlow.Web/Models/DeckComparisonResponse.cs
    - DeckFlow.Web/Models/MetaGapRequest.cs
    - DeckFlow.Web/Models/MetaGapViewModel.cs
    - DeckFlow.Web/Models/MetaGapResponse.cs
  modified:
    - DeckFlow.Web/Models/DeckPageTab.cs
  deleted:
    - DeckFlow.Web/Models/ChatGptDeckRequest.cs
    - DeckFlow.Web/Models/ChatGptDeckViewModel.cs
    - DeckFlow.Web/Models/ChatGptDeckAnalysisResponse.cs
    - DeckFlow.Web/Models/ChatGptSetUpgradeResponse.cs
    - DeckFlow.Web/Models/ChatGptDeckComparisonRequest.cs
    - DeckFlow.Web/Models/ChatGptDeckComparisonViewModel.cs
    - DeckFlow.Web/Models/ChatGptDeckComparisonResponse.cs
    - DeckFlow.Web/Models/ChatGptCedhMetaGapRequest.cs
    - DeckFlow.Web/Models/ChatGptCedhMetaGapViewModel.cs
    - DeckFlow.Web/Models/ChatGptCedhMetaGapResponse.cs
decisions:
  - D-01: applied — all 10 files renamed per locked naming map
  - D-03: applied — XML <summary> backfilled on every renamed class and on every public property that lacked one
  - D-05: applied — each file rename + the enum edit committed as separate plain-author commits; build NOT run (intermediate red expected, Wave 4 closes loop)
  - D-07: preserved — `"ChatGPT"` literals byte-identical in all 3 request DTO defaults + switch arms; `TargetAiPlatform` property names unchanged; `[JsonPropertyName]` attrs byte-identical
  - D-08: respected — EdhTop16Entry references preserved in MetaGapViewModel (out of CLASSRENAME scope)
  - Pattern 7 / enum doc-comments: deferred per 13-PATTERNS.md — existing project enums (CategorySuggestionMode, CedhMetaSortBy) ship without summaries, so DeckPageTab follows suit; AUDIT-02 will sweep
  - Minor in-scope adjustment: one doc-comment narrative reference to `ChatGptCedhMetaGapService.BuildAsync` inside `MetaGapRequest.cs` was updated to `MetaGapService.BuildAsync` so the Wave 1 grep gate (`grep -rE "ChatGpt[A-Z]" DeckFlow.Web/Models/`) returns ZERO. Tracked as a single-line deviation below — see Deviations.
metrics:
  duration_minutes: ~25
  tasks_completed: 4
  files_renamed: 10
  files_edited: 1
  types_renamed: 29
  enum_values_renamed: 3
  commits: 12
  completed_date: 2026-05-17
---

# Phase 13 Plan 13-01: Models — ChatGpt* Class Rename + XML Summaries (Wave 1)

Renamed all 10 `ChatGpt*` model files in `DeckFlow.Web/Models/` to AI-agnostic filenames per D-01, renamed all 29 public types declared inside them, renamed 3 `DeckPageTab` enum values while preserving their byte-stable integer values (5/8/9), and backfilled `/// <summary>` XML doc comments on every renamed class plus on every public property that lacked one — using `CategorySuggestionRequest.cs` / `CardLookupViewModel.cs` / `UpstreamErrorMessageBuilder.cs` as canonical tone analogs per 13-PATTERNS.md.

## Goal

Bring the C# model-layer naming in line with Phase 12's user-facing AI-agnostic page slugs (`deck-analysis`, `deck-comparison`, `cedh-meta-gap`) so the request → view-model → response triplet is symmetric per page. This is Wave 1 of the four-wave sequential rename plan locked in 13-CONTEXT.md D-05; Wave 2 (services + DI), Wave 3 (controller + Razor), and Wave 4 (tests + final build gate) follow.

## What Was Built

### File renames (10 × `git mv`)

| Old path | New path | Types renamed inside |
|---|---|---|
| `Models/ChatGptDeckRequest.cs` | `Models/DeckAnalysisRequest.cs` | `ChatGptDeckRequest` → `DeckAnalysisRequest` |
| `Models/ChatGptDeckViewModel.cs` | `Models/DeckAnalysisViewModel.cs` | `ChatGptDeckViewModel` → `DeckAnalysisViewModel` |
| `Models/ChatGptDeckAnalysisResponse.cs` | `Models/DeckAnalysisResponse.cs` | `ChatGptDeckAnalysisResponse` → `DeckAnalysisResponse`; `ChatGptWeakSlot` → `WeakSlot`; `ChatGptQuestionAnswer` → `QuestionAnswer`; `ChatGptDeckVersion` → `DeckVersion` |
| `Models/ChatGptSetUpgradeResponse.cs` | `Models/SetUpgradeResponse.cs` | `ChatGptSetUpgradeResponse` → `SetUpgradeResponse`; `ChatGptSetUpgradeSet` → `SetUpgradeSet`; `ChatGptSetUpgradeTopAdd` → `SetUpgradeTopAdd`; `ChatGptSetUpgradeCardNote` → `SetUpgradeCardNote`; `ChatGptSetUpgradeShortlist` → `SetUpgradeShortlist` |
| `Models/ChatGptDeckComparisonRequest.cs` | `Models/DeckComparisonRequest.cs` | `ChatGptDeckComparisonRequest` → `DeckComparisonRequest` |
| `Models/ChatGptDeckComparisonViewModel.cs` | `Models/DeckComparisonViewModel.cs` | `ChatGptDeckComparisonViewModel` → `DeckComparisonViewModel` |
| `Models/ChatGptDeckComparisonResponse.cs` | `Models/DeckComparisonResponse.cs` | `ChatGptDeckComparisonResponse` → `DeckComparisonResponse`; `ChatGptDeckComparisonRecommendation` → `DeckComparisonRecommendation` |
| `Models/ChatGptCedhMetaGapRequest.cs` | `Models/MetaGapRequest.cs` | `ChatGptCedhMetaGapRequest` → `MetaGapRequest` |
| `Models/ChatGptCedhMetaGapViewModel.cs` | `Models/MetaGapViewModel.cs` | `ChatGptCedhMetaGapViewModel` → `MetaGapViewModel` |
| `Models/ChatGptCedhMetaGapResponse.cs` | `Models/MetaGapResponse.cs` | 12 classes: `ChatGptCedhMetaGapResponse` → `MetaGapResponse`; `ChatGptCedhMetaGapData` → `MetaGapData`; `ChatGptCedhWinLineSet` → `WinLineSet`; `ChatGptCedhWinLines` → `WinLines`; `ChatGptCedhInteraction` → `Interaction`; `ChatGptCedhSpeed` → `Speed`; `ChatGptCedhManaEfficiency` → `ManaEfficiency`; `ChatGptCedhCoreConvergenceCard` → `CoreConvergenceCard`; `ChatGptCedhMissingStaple` → `MissingStaple`; `ChatGptCedhPotentialCut` → `PotentialCut`; `ChatGptCedhTopAdd` → `TopAdd`; `ChatGptCedhTopCut` → `TopCut` |

**Total: 10 files renamed, 29 public types renamed in lockstep.**

### Enum value renames (in-place edit of `DeckFlow.Web/Models/DeckPageTab.cs`)

| Old member | New member | Integer value |
|---|---|---|
| `ChatGptPackets` | `DeckAnalysis` | 5 (preserved) |
| `ChatGptDeckComparison` | `DeckComparison` | 8 (preserved) |
| `ChatGptCedhMetaGap` | `CedhMetaGap` | 9 (preserved) |

Other eight members (`Sync=0`, `SuggestCategories=1`, `CommanderCategories=2`, `CardLookup=3`, `MechanicLookup=4`, `Convert=7`, `Home=10`, `JudgeQuestions=11`) byte-identical including declaration order.

### XML `<summary>` doc-comment backfill (D-03)

Summary count per renamed file:

| File | `<summary>` count |
|---|---|
| `DeckAnalysisRequest.cs` | 27 (class + 26 properties) |
| `DeckAnalysisViewModel.cs` | 14 (class + 13 init-only properties) |
| `DeckAnalysisResponse.cs` | 4 (one per sealed class) |
| `SetUpgradeResponse.cs` | 5 (one per sealed class) |
| `DeckComparisonRequest.cs` | 10 (class + 9 properties) |
| `DeckComparisonViewModel.cs` | 15 (class + 14 init-only properties) |
| `DeckComparisonResponse.cs` | 2 (one per sealed class) |
| `MetaGapRequest.cs` | 12 (class + 11 properties) |
| `MetaGapViewModel.cs` | 10 (class + 9 init-only properties) |
| `MetaGapResponse.cs` | 12 (one per nested sealed class) |

Tone matches the analogs (`CategorySuggestionRequest.cs`, `CardLookupViewModel.cs`) — ONE sentence per `<summary>`, active voice, verb-first or noun-phrase first, view-model properties start with "Gets", request DTOs describe the field's effect. Nested response shapes anchored to their JSON sub-tree role.

## Wave 1 Verification Gate

```bash
$ grep -rE "ChatGpt[A-Z]" --include="*.cs" DeckFlow.Web/Models/
# (zero output)
$ grep -rE "ChatGpt[A-Z]" --include="*.cs" DeckFlow.Web/Models/ | wc -l
0
```

ZERO `ChatGpt[A-Z]` identifier hits remain in `DeckFlow.Web/Models/` — Wave 1 gate passes. Services / controllers / tests still reference old names — that is acceptable per D-05 and is closed by Waves 2/3/4.

### Preservation checks (D-07)

- `_targetAiPlatform = "ChatGPT"` default present in each request DTO (1 occurrence each: `DeckAnalysisRequest.cs`, `DeckComparisonRequest.cs`, `MetaGapRequest.cs`).
- Each request DTO has 5 total `"ChatGPT"` literal occurrences (default + switch arm `case "ChatGPT":` + 3 narrative XML-doc usages).
- All `[JsonPropertyName("snake_case")]` attributes byte-identical: 22 in `DeckAnalysisResponse.cs`, 16 in `SetUpgradeResponse.cs`, 28 in `DeckComparisonResponse.cs`, 58 in `MetaGapResponse.cs` (counts match the pre-rename files).
- `EdhTop16Entry` references preserved in `MetaGapViewModel.cs` (out of CLASSRENAME scope per D-08).
- All public property names unchanged: `TargetAiPlatform`, `DeckText`, `DeckUrl`, `StrategyNotes`, `MetaNotes`, `Format`, `DeckName`, `IncludeSideboardInAnalysis`, `IncludeMaybeboardInAnalysis`, `SelectedAnalysisQuestions`, `BudgetUpgradeAmount`, `IncludeCardVersions`, `PreferredCategories`, `ProtectedCards`, `FreeformQuestion`, `DeckProfileJson`, `SetPacketText`, `SetUpgradeResponseJson`, `TargetCommanderBracket`, `WorkflowStep`.

## Commits (12 plain-author, no Co-Authored-By trailer)

| Hash | Message |
|---|---|
| `b50ea32` | refactor(13-01): rename ChatGptDeckRequest to DeckAnalysisRequest with XML summaries |
| `5e52191` | refactor(13-01): rename ChatGptDeckAnalysisResponse and nested shapes to AI-agnostic names with XML summaries |
| `17178d0` | refactor(13-01): rename ChatGptSetUpgradeResponse and nested shapes to AI-agnostic names with XML summaries |
| `90ef943` | refactor(13-01): rename ChatGptDeckViewModel to DeckAnalysisViewModel with XML summaries |
| `c53443a` | refactor(13-01): rename ChatGptDeckComparisonRequest to DeckComparisonRequest with XML summaries |
| `e28c78d` | refactor(13-01): rename ChatGptDeckComparisonResponse and nested shape with XML summaries |
| `ec7feb0` | refactor(13-01): rename ChatGptDeckComparisonViewModel to DeckComparisonViewModel with XML summaries |
| `b621934` | refactor(13-01): rename ChatGptCedhMetaGapRequest to MetaGapRequest with XML summaries |
| `d571dfa` | refactor(13-01): rename ChatGptCedhMetaGapResponse and 11 nested shapes with XML summaries |
| `b4261a7` | refactor(13-01): rename ChatGptCedhMetaGapViewModel to MetaGapViewModel with XML summaries |
| `eb0a5ce` | refactor(13-01): update MetaGapRequest doc-comment narrative service reference to MetaGapService (Wave 1 grep gate) |
| `2df9d4d` | refactor(13-01): rename DeckPageTab enum values to AI-agnostic names (preserve integer values 5/8/9) |

Plain author across all 12: `Chris Lunt <luntc1972@yahoo.com>`. No Co-Authored-By trailer.

## Deviations from Plan

### 1. [Rule 3 — blocking-issue fix] Doc-comment narrative service-name reference

- **Found during:** Task 4 (Wave 1 grep verification step)
- **Issue:** `MetaGapRequest.cs` line 91 had a narrative XML doc-comment reference to `ChatGptCedhMetaGapService.BuildAsync` (the Wave-2-renamed service). That single identifier matched the Wave 1 gate `grep -rE "ChatGpt[A-Z]" DeckFlow.Web/Models/`, which the plan's verification block requires to return ZERO output.
- **Why this is in-scope for Wave 1:** D-07 #5 permits the literal word "ChatGPT" inside `<summary>` narratives describing the AI; it does NOT permit a renamed C# **identifier** to remain. The Wave 1 grep gate is intentionally narrow (`ChatGpt[A-Z]` — matches identifier-like prefixes, not the bare word "ChatGPT"). Leaving this identifier in the doc-comment would have failed the gate and been wrong post-Wave-2 anyway.
- **Fix:** Updated narrative reference to `MetaGapService.BuildAsync` (the new service name Wave 2 will land).
- **Files modified:** `DeckFlow.Web/Models/MetaGapRequest.cs` (one line in an XML doc-comment).
- **Commit:** `eb0a5ce`.
- **Net deviation count:** +1 commit beyond the plan's "11 plain-author commits" target → 12 commits total. Each commit remains a single logical change per CLAUDE.md.

No other deviations. Naming map applied byte-stable to D-01; no architectural changes; no service/controller/view/test touches.

## Self-Check: PASSED

- All 10 new files exist on disk; all 10 old files removed:
  - FOUND: DeckFlow.Web/Models/DeckAnalysisRequest.cs
  - FOUND: DeckFlow.Web/Models/DeckAnalysisViewModel.cs
  - FOUND: DeckFlow.Web/Models/DeckAnalysisResponse.cs
  - FOUND: DeckFlow.Web/Models/SetUpgradeResponse.cs
  - FOUND: DeckFlow.Web/Models/DeckComparisonRequest.cs
  - FOUND: DeckFlow.Web/Models/DeckComparisonViewModel.cs
  - FOUND: DeckFlow.Web/Models/DeckComparisonResponse.cs
  - FOUND: DeckFlow.Web/Models/MetaGapRequest.cs
  - FOUND: DeckFlow.Web/Models/MetaGapViewModel.cs
  - FOUND: DeckFlow.Web/Models/MetaGapResponse.cs
  - FOUND: DeckFlow.Web/Models/DeckPageTab.cs (modified in place)
- All 12 commits present in `git log`:
  - FOUND: b50ea32, 5e52191, 17178d0, 90ef943, c53443a, e28c78d, ec7feb0, b621934, d571dfa, b4261a7, eb0a5ce, 2df9d4d
- Wave 1 grep gate returns ZERO hits in `DeckFlow.Web/Models/`.
- Preservation literals verified byte-identical (`"ChatGPT"` defaults, `[JsonPropertyName]` attrs).

## Forward-Looking Note (Wave 2 prep)

The model rename is "ahead" of the rest of the codebase. As of this commit:
- `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` still references the renamed model types under old names → BREAKS BUILD until Wave 2 closes.
- `DeckFlow.Web/Controllers/DeckController.cs` likewise → Wave 3.
- `DeckFlow.Web.Tests/*` fixtures likewise → Wave 4.

`dotnet build` was NOT run as a pass criterion in this plan per D-05. **Build-clean gate fires only at end of Wave 4 (plan 13-04).** This is the intended intermediate state.

Wave 2 (plan 13-02) will:
1. Rename 7 service files + their interfaces + DI registrations in `Program.cs:263-295`.
2. Sweep service-internal references to the renamed model types.
3. Update README mentions of the old type names.
4. Update any service-class XML doc-comments that mention `ChatGpt*` identifiers (Wave 2 has full freedom; Wave 1 only touched the one MetaGapRequest narrative ref needed to clear the Wave 1 gate).
