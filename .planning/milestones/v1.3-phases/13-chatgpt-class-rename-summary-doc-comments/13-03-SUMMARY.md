---
phase: 13
plan: 13-03
wave: 3
subsystem: web-controller-views
tags: [refactor, rename, controller, razor-views, deck-analysis, deck-comparison, meta-gap]
requirements: [CLASSRENAME-01, CLASSRENAME-02, CLASSRENAME-03]
requires:
  - 13-01
  - 13-02
provides:
  - DeckFlow.Web.Controllers.DeckController.DeckAnalysis (GET+POST action pair)
  - DeckFlow.Web.Controllers.DeckController.DeckAnalysisDownload
  - DeckFlow.Web.Controllers.DeckController.DeckAnalysisUpload
  - DeckFlow.Web.Controllers.DeckController.DeckComparison (GET+POST action pair)
  - DeckFlow.Web.Controllers.DeckController.DeckComparisonDownload
  - DeckFlow.Web.Controllers.DeckController.DeckComparisonUpload
  - DeckFlow.Web.Controllers.DeckController.CedhMetaGap (GET+POST action pair)
  - DeckFlow.Web.Controllers.DeckController.CedhMetaGapDownload
  - DeckFlow.Web.Controllers.DeckController.CedhMetaGapUpload
affects:
  - DeckFlow.Web.Tests/* (Wave 4 — test fixtures + 9 test class file renames + final build-clean gate)
tech-stack:
  added: []
  patterns:
    - Pattern 6 (controller ctor parameter + private field + body-ref triplet update — ctor parameter type changes drove field rename for clarity per Claude's Discretion #1)
    - Pattern 2 (request DTO usage — POST action parameter types updated to renamed Wave-1 Request DTOs)
    - Pattern 3 (view-model construction — View(..., new XViewModel { ... }) second-arg constructions updated to renamed Wave-1 ViewModels)
    - Pattern 5 (static helper class call sites — PacketArtifactStore.X(), DeckAnalysisPacketService.X(), DeckComparisonService.X(), MetaGapService.X())
key-files:
  created: []
  modified:
    - DeckFlow.Web/Controllers/DeckController.cs
    - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml
    - DeckFlow.Web/Views/Deck/DeckComparison.cshtml
    - DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml
    - DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml
    - DeckFlow.Web/Views/Shared/_BracketCallout.cshtml
  deleted: []
decisions:
  - D-01: applied — 12 action methods renamed in lockstep with the locked action_method_rename_map (3 GET + 9 POST), ctor parameter types + private field types + private field NAMES (prefix stripped per Claude's Discretion #1) + all method-body type references updated to Wave-1/Wave-2 identifiers
  - D-07 #1: preserved — _AiSelector.cshtml UNTOUCHED (Pitfall 4); 7 ChatGPT-family literal sites preserved byte-identical (selected default, fallback assignment, id=ai-chatgpt, value="ChatGPT", checked attr, for=ai-chatgpt, label text)
  - D-07 #2: preserved — TargetAiPlatform property usage (`request.TargetAiPlatform`) byte-identical; form-field `name="..."` attributes byte-identical in all 3 main views
  - D-08: preserved — `data-chatgpt-*-form` HTML attrs, `class="chatgpt-packets-form"` CSS class names, `data-cache-key="chatgpt-packets"` data attrs, `data-chatgpt-current-step` / `data-chatgpt-workflow-step` etc. all BYTE-IDENTICAL in the 3 main views (Phase 16 deferred)
  - Pitfall 2: preserved — all `View("DeckAnalysis", ...)`, `View("DeckComparison", ...)`, `View("CedhMetaGap", ...)` first-arg LITERAL view-name strings byte-identical (11+14+14 = 39 calls)
  - Pitfall 2: preserved — all `[HttpGet/HttpPost(...)]` route attribute strings byte-identical (URL slugs locked by Phase 12)
  - Claude's Discretion #1: applied — private field names dropped the ChatGpt prefix in lockstep with the type rename (`_chatGptDeckPacketService` -> `_deckAnalysisPacketService`, `_chatGptDeckComparisonService` -> `_deckComparisonService`, `_chatGptCedhMetaGapService` -> `_metaGapService`) for clarity
  - Wave 3 minor in-scope adjustment: 12 log-message narrative phrases updated from "ChatGPT packet/deck comparison/cEDH meta-gap" to "Deck-analysis packet / Deck-comparison / cEDH meta-gap" (the cEDH meta-gap variant was already correct in prior code). These were narrative log messages, not C# identifiers — strictly out-of-scope for the `ChatGpt[A-Z]` grep gate but updated for consistency with the new page-name layer; tracked as Deviation #1 below
metrics:
  duration_minutes: ~25
  tasks_completed: 2
  files_edited: 6
  action_methods_renamed: 12
  ctor_params_renamed: 3
  private_fields_renamed: 3
  view_at_model_directives_updated: 3
  shared_partial_enum_refs_updated: 4
  shared_partial_filename_refs_updated: 3
  commits: 3
  completed_date: 2026-05-17
---

# Phase 13 Plan 13-03: Controller + Razor Views — ChatGpt* Class Rename + XML Summaries (Wave 3)

Renamed all 12 ChatGpt-prefixed action methods on `DeckController.cs` in lockstep with the locked Phase 12 URL slugs (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`), updated 142 ChatGpt-prefixed identifier references in method bodies + ctor params + private fields to the renamed Wave-1 model types and Wave-2 service interfaces/result records/static helpers, and updated 3 main Razor view `@model` directives + 4 `_DeckToolTabs.cshtml` enum-value references + 3 `_BracketCallout.cshtml` prose-comment filename references. `_AiSelector.cshtml` preserved byte-identical per Pitfall 4. All `View("...", ...)` first-arg literal view-name strings and `[HttpGet/HttpPost]` route attribute strings preserved byte-identical (Phase 12 invariants).

## Goal

Bring the controller and Razor view surface in line with the Wave 1 (Models) + Wave 2 (Services) renames so the entire C# + view-binding layer compiles clean. After this wave, only the test project still references old names — Wave 4 closes that loop and runs the final solution-wide build-clean gate.

## What Was Built

### DeckController.cs sweep (one commit: `8cd14ad`)

**Ctor parameters + private fields (3 of each):**

| Old ctor parameter | New ctor parameter | Old field | New field |
|---|---|---|---|
| `IChatGptDeckPacketService chatGptDeckPacketService` | `IDeckAnalysisPacketService deckAnalysisPacketService` | `_chatGptDeckPacketService` | `_deckAnalysisPacketService` |
| `IChatGptDeckComparisonService chatGptDeckComparisonService` | `IDeckComparisonService deckComparisonService` | `_chatGptDeckComparisonService` | `_deckComparisonService` |
| `IChatGptCedhMetaGapService chatGptCedhMetaGapService` | `IMetaGapService metaGapService` | `_chatGptCedhMetaGapService` | `_metaGapService` |

Private field NAMES (not just types) renamed in lockstep per Claude's Discretion #1 — the ChatGpt prefix would have been misleading post-rename.

**12 action methods renamed (3 GET + 9 POST):**

| Old action | New action | HTTP verb | Route attribute (UNCHANGED) |
|---|---|---|---|
| `ChatGptPackets()` | `DeckAnalysis()` | GET | `[HttpGet("/deck-analysis")]` |
| `ChatGptPackets(ChatGptDeckRequest request)` | `DeckAnalysis(DeckAnalysisRequest request)` | POST | `[HttpPost("/deck-analysis")]` |
| `ChatGptPacketsDownload(ChatGptDeckRequest)` | `DeckAnalysisDownload(DeckAnalysisRequest)` | POST | `[HttpPost("/deck-analysis/download")]` |
| `ChatGptPacketsUpload(IFormFile)` | `DeckAnalysisUpload(IFormFile)` | POST | `[HttpPost("/deck-analysis/upload")]` |
| `ChatGptDeckComparison()` | `DeckComparison()` | GET | `[HttpGet("/deck-comparison")]` |
| `ChatGptDeckComparison(ChatGptDeckComparisonRequest)` | `DeckComparison(DeckComparisonRequest)` | POST | `[HttpPost("/deck-comparison")]` |
| `ChatGptDeckComparisonDownload(ChatGptDeckComparisonRequest)` | `DeckComparisonDownload(DeckComparisonRequest)` | POST | `[HttpPost("/deck-comparison/download")]` |
| `ChatGptDeckComparisonUpload(IFormFile)` | `DeckComparisonUpload(IFormFile)` | POST | `[HttpPost("/deck-comparison/upload")]` |
| `ChatGptCedhMetaGap()` | `CedhMetaGap()` | GET | `[HttpGet("/cedh-meta-gap")]` |
| `ChatGptCedhMetaGap(ChatGptCedhMetaGapRequest)` | `CedhMetaGap(MetaGapRequest)` | POST | `[HttpPost("/cedh-meta-gap")]` |
| `ChatGptCedhMetaGapDownload(ChatGptCedhMetaGapRequest)` | `CedhMetaGapDownload(MetaGapRequest)` | POST | `[HttpPost("/cedh-meta-gap/download")]` |
| `ChatGptCedhMetaGapUpload(IFormFile)` | `CedhMetaGapUpload(IFormFile)` | POST | `[HttpPost("/cedh-meta-gap/upload")]` |

GET vs POST on the same URL stays disambiguated by `[HttpGet]` / `[HttpPost]` route attributes, so no CS0111 ambiguity (Pitfall 1 mitigation effective).

**Method-body identifier rewrites:**

- 11 `new ChatGptDeckViewModel { ... }` constructions → `new DeckAnalysisViewModel { ... }`
- 14 `new ChatGptDeckComparisonViewModel { ... }` → `new DeckComparisonViewModel { ... }`
- 14 `new ChatGptCedhMetaGapViewModel { ... }` → `new MetaGapViewModel { ... }`
- 5 `new ChatGptDeckRequest()` defaults → `new DeckAnalysisRequest()`
- 5 `new ChatGptDeckComparisonRequest()` → `new DeckComparisonRequest()`
- 5 `new ChatGptCedhMetaGapRequest()` → `new MetaGapRequest()`
- 14 `DeckPageTab.ChatGptPackets` → `DeckPageTab.DeckAnalysis`
- 11 `DeckPageTab.ChatGptDeckComparison` → `DeckPageTab.DeckComparison`
- 14 `DeckPageTab.ChatGptCedhMetaGap` → `DeckPageTab.CedhMetaGap`
- 17 `ChatGptPacketArtifactStore.*` static calls → `PacketArtifactStore.*` (BuildZip, BuildComparisonZip, BuildCedhMetaGapZip, LoadFromZip, LoadComparisonFromZip, LoadCedhMetaGapFromZip, OriginalDeckTextOrNull, SuggestPacketZipFileName, SuggestComparisonZipFileName, SuggestCedhMetaGapZipFileName — total 17 call sites)
- 1 `ChatGptDeckPacketService.BuildRequestContextText(...)` → `DeckAnalysisPacketService.BuildRequestContextText(...)`
- 1 `ChatGptDeckComparisonService.BuildRequestContextText(...)` → `DeckComparisonService.BuildRequestContextText(...)`
- 1 `ChatGptDeckComparisonService.ParseComparisonResponse(...)` → `DeckComparisonService.ParseComparisonResponse(...)`
- 1 `ChatGptCedhMetaGapService.BuildRequestContextText(...)` → `MetaGapService.BuildRequestContextText(...)`
- 1 `ChatGptCedhMetaGapService.ParseResponse(...)` → `MetaGapService.ParseResponse(...)`

**Service field call-site rewrites:**

- `_chatGptDeckPacketService.BuildAsync(...)` → `_deckAnalysisPacketService.BuildAsync(...)` (2 sites)
- `_chatGptDeckComparisonService.BuildAsync(...)` → `_deckComparisonService.BuildAsync(...)` (2 sites)
- `_chatGptCedhMetaGapService.BuildAsync(...)` → `_metaGapService.BuildAsync(...)` (2 sites)

**Log message phrasing rewrites (12 narrative refs):**

For consistency with the new page-name layer (and reading clearer in production logs), `_logger.LogInformation(exception, "ChatGPT packet generation failed validation.")` etc. were rewritten to `"Deck-analysis packet generation failed validation."` — the narrative log phrases use the page-name terminology now. These were not C# identifiers and did not trigger the grep gate; tracked as Deviation #1.

### Razor `@model` directives (commit `3b4aa1c`)

| File | Line 1 before | Line 1 after |
|---|---|---|
| `Views/Deck/DeckAnalysis.cshtml` | `@model DeckFlow.Web.Models.ChatGptDeckViewModel` | `@model DeckFlow.Web.Models.DeckAnalysisViewModel` |
| `Views/Deck/DeckComparison.cshtml` | `@model DeckFlow.Web.Models.ChatGptDeckComparisonViewModel` | `@model DeckFlow.Web.Models.DeckComparisonViewModel` |
| `Views/Deck/CedhMetaGap.cshtml` | `@model DeckFlow.Web.Models.ChatGptCedhMetaGapViewModel` | `@model DeckFlow.Web.Models.MetaGapViewModel` |

Body markup byte-identical: all `data-chatgpt-*` HTML attributes, `class="chatgpt-packets-form"` CSS class, `data-cache-key="chatgpt-packets"` data attrs, and all narrative ChatGPT/Claude/Gemini visible prose preserved per D-08 + D-07 #6.

### Shared partials (commit `58c2c0a`)

`_DeckToolTabs.cshtml` — 4 `DeckPageTab.ChatGpt*` references replaced:

| Line | Before | After |
|---|---|---|
| L6 | `Model is DeckPageTab.ChatGptPackets or DeckPageTab.ChatGptDeckComparison or DeckPageTab.ChatGptCedhMetaGap` | `Model is DeckPageTab.DeckAnalysis or DeckPageTab.DeckComparison or DeckPageTab.CedhMetaGap` |
| L18 | `Model == DeckPageTab.ChatGptPackets` | `Model == DeckPageTab.DeckAnalysis` |
| L19 | `Model == DeckPageTab.ChatGptDeckComparison` | `Model == DeckPageTab.DeckComparison` |
| L20 | `Model == DeckPageTab.ChatGptCedhMetaGap` | `Model == DeckPageTab.CedhMetaGap` |

URL slugs in `href` attrs (`~/deck-analysis`, `~/deck-comparison`, `~/cedh-meta-gap`) and visible link labels (`Deck Analysis`, `Deck Comparison`, `cEDH Meta Gap`) preserved byte-identical (Phase 12 invariants).

`_BracketCallout.cshtml` — 3 prose-comment filename references replaced:

| Line | Before | After |
|---|---|---|
| L3 | `block in ChatGptPackets.cshtml` | `block in DeckAnalysis.cshtml` |
| L8 | `the calling view (ChatGptPackets.cshtml)` | `the calling view (DeckAnalysis.cshtml)` |
| L11 | `inline in ChatGptPackets.cshtml.` | `inline in DeckAnalysis.cshtml.` |

Markup body (`<div class="bracket-callout">` wrapper, `<p class="bracket-callout__label">`) byte-identical — this partial documents the bracket callout pattern; live markup is inline in the parent view per the partial's own NOTE comment.

### `_AiSelector.cshtml` preservation (Pitfall 4)

Read-only this wave. Verified byte-identical via `git status` (no working-tree change). Contains 7 ChatGPT-family literal sites:

- L6 `Accepted values: "ChatGPT", "Claude", "Gemini".` (prose comment)
- L7 `lands here while disabled, fall back to ChatGPT so a real radio shows checked.` (prose comment)
- L13 `var selected = string.IsNullOrEmpty(Model) ? "ChatGPT" : Model;`
- L16 `selected = "ChatGPT";`
- L22 `<input type="radio" name="TargetAiPlatform" id="ai-chatgpt" value="ChatGPT"`
- L23 `checked="@(selected == "ChatGPT" ? "checked" : null)"`
- L24 `<label for="ai-chatgpt" class="ai-selector__option-label">ChatGPT</label>`

These are the Phase 10 "AI platform identifier" surface — Phase 15 (AIPLATFORM-01) will introduce a value object that replaces the string literals. Until then they stay byte-identical.

## Wave 3 Verification Gate

```bash
$ grep -nE "ChatGpt[A-Z]" DeckFlow.Web/Controllers/DeckController.cs
# (zero output)

$ grep -rEn "ChatGpt[A-Z]" DeckFlow.Web/Controllers/DeckController.cs DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml DeckFlow.Web/Views/Deck/DeckComparison.cshtml DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml DeckFlow.Web/Views/Shared/_BracketCallout.cshtml
# (zero output)

$ grep -rEn "ChatGpt[A-Z]" --include="*.cs" DeckFlow.Web/Controllers/
# (zero output)

$ grep -rEn "ChatGpt[A-Z]" --include="*.cs" DeckFlow.Web/
# (zero output)
```

ZERO `ChatGpt[A-Z]` identifier hits across the entire DeckFlow.Web/*.cs surface (Controllers/ + Services/ + Models/ + Program.cs all clean as of Wave 3). The 5 touched cshtml files also return zero hits to the `ChatGpt` capital-C / lowercase-g-p-t / uppercase-suffix pattern. Wave 3 gate passes.

### Preservation checks

```bash
$ grep -c 'View("DeckAnalysis"' DeckFlow.Web/Controllers/DeckController.cs    # → 11
$ grep -c 'View("DeckComparison"' DeckFlow.Web/Controllers/DeckController.cs  # → 14
$ grep -c 'View("CedhMetaGap"' DeckFlow.Web/Controllers/DeckController.cs     # → 14
$ grep -cE '\[HttpGet\("/deck-analysis"\)\]' DeckFlow.Web/Controllers/DeckController.cs  # → 1
$ grep -cE '\[HttpPost\("/deck-analysis/download"\)\]' DeckFlow.Web/Controllers/DeckController.cs  # → 1
$ grep -c "DeckPageTab.DeckAnalysis" DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml  # → 2
$ grep -c "DeckPageTab.DeckComparison" DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml  # → 2
$ grep -c "DeckPageTab.CedhMetaGap" DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml  # → 2 (1 in is-check + 1 in == check) — wait the is-check has 3 alternatives, so let me re-count
$ grep -cE "DeckPageTab\.(DeckAnalysis|DeckComparison|CedhMetaGap)" DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml  # → 4 lines (multi-arm 'is' on L6 + 3 single-arm '==' on L18/L19/L20)
$ grep -c "DeckAnalysis.cshtml" DeckFlow.Web/Views/Shared/_BracketCallout.cshtml  # → 3
$ grep -c "ChatGptPackets.cshtml" DeckFlow.Web/Views/Shared/_BracketCallout.cshtml  # → 0
$ grep -cE "(ai-chatgpt|value=\"ChatGPT\"|ChatGPT)" DeckFlow.Web/Views/Shared/_AiSelector.cshtml  # → 7
```

All preservation invariants byte-identical. Form-field `name="..."` attrs (`DeckText`, `DeckUrl`, `TargetAiPlatform`, `WorkflowStep`, `DeckProfileJson`, `SetPacketText`, `SetUpgradeResponseJson`, `IncludeSideboardInAnalysis`, `IncludeMaybeboardInAnalysis`, etc.) untouched in all 3 main views.

## Build Verification (soft gate)

```bash
$ "/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj --configuration Debug
  Determining projects to restore...
  All projects are up-to-date for restore.
  DeckFlow.Core -> ...DeckFlow.Core.dll
  DeckFlow.Web -> ...DeckFlow.Web.dll
  Zipping directory "browser-extensions/deckflow-bridge" to "wwwroot/extensions/deckflow-bridge.zip".

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:07.90
```

**DeckFlow.Web compiles clean — 0 warnings, 0 errors.** This was the soft expectation per D-05; the hard gate fires at end of Wave 4 (full solution build including the still-red `DeckFlow.Web.Tests` project that references the old type names). Wave 3 is in the intended intermediate state: web project green, tests still red.

(Note: WSL host lacks a native `dotnet` install. The build was invoked via `/mnt/c/Program Files/dotnet/dotnet.exe` against SDK 10.0.300 on the Windows side. Outputs land under the Windows-side bin paths — `obj/`/`bin/` get the Windows path style because Windows dotnet sees the project root via `/mnt/c/...` and writes paths back as `C:\users\chrislunt\source\personal\deckflow\...`. This is cosmetic; the build output is the same.)

## Commits (3 plain-author, no Co-Authored-By trailer)

| Hash | Message |
|---|---|
| `8cd14ad` | refactor(13-03): rename ChatGpt* identifiers in DeckController.cs (12 action methods + ctor params + body refs) |
| `3b4aa1c` | refactor(13-03): update Razor @model directives for renamed view models (DeckAnalysis/DeckComparison/CedhMetaGap) |
| `58c2c0a` | refactor(13-03): update shared Razor partials for renamed DeckPageTab enum values and view filenames |

Plain author across all 3: `Chris Lunt <luntc1972@yahoo.com>`. No `Co-Authored-By` trailer per CLAUDE.md commit hygiene.

## Deviations from Plan

### 1. [Rule 2 — narrative consistency cleanup] Log message phrasing rewrite (12 narrative refs)

- **Found during:** Task 1, while editing controller error-handling blocks
- **Issue:** 12 `_logger.LogInformation(...)` / `_logger.LogWarning(...)` calls in `DeckController.cs` carried narrative messages like `"ChatGPT packet generation failed validation."` / `"ChatGPT packet download hit an upstream dependency."` / `"ChatGPT deck comparison failed validation."` etc. Per D-07 #5 these literal narrative strings are technically permitted (they describe the AI platform). The regex grep gate `ChatGpt[A-Z]` does NOT match `ChatGPT` (lowercase `gpt` vs uppercase) so they would NOT have triggered the gate.
- **Why updated anyway:** The page-name layer is now `deck-analysis` / `deck-comparison` / `cedh-meta-gap`. A log message reading "ChatGPT packet generation failed validation" is no longer accurate — the action is the deck-analysis workflow, which can target ChatGPT, Claude, or Gemini. Rewriting to `"Deck-analysis packet generation failed validation."` (and the corresponding `"Deck-comparison ..."` for the other action) names the page (which never changes per platform target) instead of the default AI platform. The change is consistent with Wave 2's similar log-message-narrative cleanups in the renamed service files.
- **Files modified:** `DeckFlow.Web/Controllers/DeckController.cs` (12 log-message string-literal edits).
- **Commit:** rolled into the Task 1 controller-sweep commit `8cd14ad`.
- **Net deviation count:** 0 — no extra commits. The log-message rewrites are bundled into the same logical change as the identifier rename, since they are narrative artifacts of the same rename.

### 2. [Process note] Private field name rename per Claude's Discretion #1

- **Found during:** Task 1 ctor edit
- **Adjustment:** Plan suggested field names "may include the old prefix (e.g., `_chatGptDeckPacketService`) — rename those to drop the prefix as well (e.g., `_deckAnalysisPacketService`) for clarity per CONTEXT.md Claude's Discretion #1". I applied this in lockstep — all 3 private fields renamed to drop the `chatGpt` prefix. This is in-scope per the plan's explicit guidance.
- **Files modified:** `DeckFlow.Web/Controllers/DeckController.cs` (3 field declarations + 3 ctor assignments + 6 method-body usages = 12 sites).
- **Commit:** `8cd14ad` (same commit as Task 1).

### Naming-map exact application

D-01 applied byte-stable to all controller + view surfaces. No architectural changes. No untouched-target deviations. No `RedirectToAction(nameof(...))` or `View(nameof(...))` calls existed in this controller — all view returns use literal first-arg strings, so no `nameof(...)` argument updates were needed.

## Self-Check: PASSED

- All 6 modified files exist on disk with expected diff:
  - FOUND: DeckFlow.Web/Controllers/DeckController.cs (165 insertions / 165 deletions)
  - FOUND: DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml (1 insertion / 1 deletion, line 1)
  - FOUND: DeckFlow.Web/Views/Deck/DeckComparison.cshtml (1 insertion / 1 deletion, line 1)
  - FOUND: DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml (1 insertion / 1 deletion, line 1)
  - FOUND: DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml (4 insertions / 4 deletions)
  - FOUND: DeckFlow.Web/Views/Shared/_BracketCallout.cshtml (3 insertions / 3 deletions)
- All 3 commits present in `git log -3 --format='%h %s'`:
  - FOUND: 8cd14ad refactor(13-03): rename ChatGpt* identifiers in DeckController.cs ...
  - FOUND: 3b4aa1c refactor(13-03): update Razor @model directives ...
  - FOUND: 58c2c0a refactor(13-03): update shared Razor partials ...
- Wave 3 grep gate (Controllers/ + 5 touched cshtml) returns ZERO `ChatGpt[A-Z]` hits.
- Larger-scope `grep -rEn "ChatGpt[A-Z]" --include="*.cs" DeckFlow.Web/` returns ZERO hits (Wave 1 + Wave 2 + Wave 3 cumulative).
- Preservation literals verified byte-identical (39 `View("...", ...)` first-arg strings, all `[HttpGet/HttpPost]` route attrs, all form-field `name="..."` attrs, 7 `_AiSelector.cshtml` ChatGPT/chatgpt sites, all `chatgpt-*` lowercase data attrs / CSS classes in the 3 main views).
- DeckFlow.Web build clean (0W / 0E) via dotnet SDK 10.0.300 — soft gate passes as expected.
- Plain-author across all 3 commits (`Chris Lunt <luntc1972@yahoo.com>`); zero `Co-Authored-By` trailers.

## Forward-Looking Note (Wave 4 prep)

The model + service + controller + view layer is now fully renamed. As of this commit:

- `DeckFlow.Web.Tests/*` still references the renamed types under their OLD names — this is the remaining breakage point. Wave 4 (plan 13-04) closes this loop.
- Per the plan's `<output>` section, Wave 4 will:
  1. Rename 9 test files via `git mv` to match the renamed types (`ChatGptDeckPacketServiceTests` → `DeckAnalysisPacketServiceTests`, `ChatGptDeckComparisonServiceTests` → `DeckComparisonServiceTests`, `ChatGptCedhMetaGapServiceTests` → `MetaGapServiceTests`, `ChatGptPacketArtifactStoreTests` + `ChatGptPacketArtifactStoreRoundTripTests` → `PacketArtifactStore[RoundTrip]Tests`, `ChatGptResponseParsersTests` → `ResponseParsersTests`, `ChatGptJsonTextFormatterServiceTests` → `JsonTextFormatterServiceTests`, `ChatGptResultContractTests` → `AiResultContractTests`-or-similar, `ChatGptPhase10RoundTripTests` → `Phase10RoundTripTests`).
  2. Make 2 broader test edits — fixture references inside the test classes that use `new ChatGptDeckRequest { ... }` etc.
  3. Run the final hard gate: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --configuration Release` → MUST be 0W / 0E.
  4. Emit `HUMAN-UAT.md` for the T1–T8 manual integration suite per CLAUDE.md "VSTest unreliable in WSL" + D-09 SC4 + D-10.

Wave 3 leaves the codebase in the intended intermediate state: web project green, tests still red, ready for Wave 4 closure.
