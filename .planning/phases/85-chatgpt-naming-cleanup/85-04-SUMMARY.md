---
phase: 85-chatgpt-naming-cleanup
plan: 04
subsystem: ui
tags: [razor, csharp, naming-cleanup, manabase, deck-primer, content-kb, playwright, xunit]

# Dependency graph
requires:
  - phase: 85-01
    provides: D3 keep-list decision (genuine ChatGPT model/platform references + user-visible copy) and the naming-cleanup inventory this plan executes against
provides:
  - ChatGptSwapPrompt C# symbol renamed to PromptSwapPrompt (ManabaseAnalysisService record param + doc tag, ManabaseViewModel property, ManabaseController assignment, 9 test refs, Manabase.cshtml binding)
  - Cosmetic chatgpt-* identifiers renamed to prompt-* in DeckPrimer.cshtml (sticky-download/resume/step-*), Manabase.cshtml (print-button class/attr, swap-prompt textarea id + copy-target), ContentKb/Detail.cshtml (sticky-download), _FormError.cshtml (doc example)
  - 2 internal (non-visible) doc comments genericized in DeckPacketController.cs and DeckAnalysisPacketService.cs
  - print-manabase-results.spec.ts + HelpContentServiceTests.cs + HelpControllerTests.cs updated to prompt-* selectors/slugs
affects: [85-05]

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs
    - DeckFlow.Web/Models/ManabaseViewModel.cs
    - DeckFlow.Web/Controllers/ManabaseController.cs
    - DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs
    - DeckFlow.Web/Views/Deck/Manabase.cshtml
    - DeckFlow.Web/Views/Deck/DeckPrimer.cshtml
    - DeckFlow.Web/Views/ContentKb/Detail.cshtml
    - DeckFlow.Web/Views/Shared/_FormError.cshtml
    - DeckFlow.Web/Controllers/DeckPacketController.cs
    - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
    - DeckFlow.Web/e2e/print-manabase-results.spec.ts
    - DeckFlow.Web.Tests/HelpContentServiceTests.cs
    - DeckFlow.Web.Tests/HelpControllerTests.cs

key-decisions:
  - "Renamed the generic ChatGptSwapPrompt C# symbol to PromptSwapPrompt in one commit; property rename does not change the string VALUE, so the rendered swap-prompt textarea content stays byte-identical"
  - "Left 3 pre-existing internal doc-comment mentions of 'ChatGPT' out of scope (ManabaseAnalysisService.cs:25 deckName param doc, ManabaseViewModel.cs:7 class summary, ManabaseController.cs:13 class summary) — not enumerated in the plan's interfaces/doc-comment rename list and not required by any acceptance criterion; flagged for a future cleanup pass rather than silently expanding scope"
  - "DeckAnalysisPacketService.cs:1815 doc comment reworded to 'that the prompt should follow' (added 'the' for grammatical correctness) rather than a literal drop-in of the bare word 'prompt'"

patterns-established: []

requirements-completed: [AICLEAN-02, AICLEAN-03]

# Metrics
duration: 25min
completed: 2026-07-05
---

# Phase 85 Plan 04: ChatGPT Naming Cleanup — Manabase/DeckPrimer/ContentKb Summary

**Renamed the generic ChatGptSwapPrompt C# property to PromptSwapPrompt and the remaining cosmetic chatgpt-* CSS/data/test identifiers to prompt-*, while keeping every user-visible "ChatGPT" copy string and the TargetAiPlatform model-key default verbatim.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-07-05
- **Tasks:** 3/3 completed
- **Files modified:** 13

## Accomplishments
- Renamed the generic `ChatGptSwapPrompt` record param / view-model property / controller assignment / 9 test references / Razor binding to `PromptSwapPrompt` in one commit — value and rendered copy unchanged (confirmed by passing tests).
- Renamed cosmetic `chatgpt-*` classes/attributes/ids to `prompt-*` in DeckPrimer.cshtml (sticky-download, resume, step-heading/eyebrow/badge/actions), Manabase.cshtml (print-button class + `data-prompt-print` attr, `manabase-prompt-output` textarea id + matching `data-copy-target`), and ContentKb/Detail.cshtml (sticky-download); updated the `_FormError.cshtml` doc-comment example.
- Genericized 2 internal (non-visible) doc comments in DeckPacketController.cs and DeckAnalysisPacketService.cs; kept the DeckAnalysisPacketService.cs:2131 `"ChatGPT"` TargetAiPlatform default untouched.
- Updated `print-manabase-results.spec.ts` to assert `data-prompt-print`, and renamed the synthetic `chatgpt-analysis` test-fixture slug to `prompt-analysis` in `HelpContentServiceTests.cs` / `HelpControllerTests.cs`.

## Task Commits

Each task was committed atomically:

1. **Task 1: Rename the ChatGptSwapPrompt C# symbol -> PromptSwapPrompt** - `984faffc` (refactor)
2. **Task 2: Rename cosmetic chatgpt-* identifiers + 2 doc comments** - `54da2447` (refactor)
3. **Task 3: Update print-manabase e2e + rename chatgpt-analysis test slug** - `0df9a582` (test)

_No TDD tasks in this plan; each task is a single commit._

## Files Created/Modified
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` - `ChatGptSwapPrompt` record param + doc `<param>` -> `PromptSwapPrompt`
- `DeckFlow.Web/Models/ManabaseViewModel.cs` - `ChatGptSwapPrompt` property -> `PromptSwapPrompt`
- `DeckFlow.Web/Controllers/ManabaseController.cs` - assignment `PromptSwapPrompt = result.PromptSwapPrompt`
- `DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs` - 9 refs renamed
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` - `@Model.PromptSwapPrompt` binding, `prompt-print-button`/`data-prompt-print`, `manabase-prompt-output` id + `data-copy-target`
- `DeckFlow.Web/Views/Deck/DeckPrimer.cshtml` - `chatgpt-sticky-download*`, `chatgpt-resume`, `chatgpt-step-*` -> `prompt-*`
- `DeckFlow.Web/Views/ContentKb/Detail.cshtml` - `chatgpt-sticky-download` -> `prompt-sticky-download`
- `DeckFlow.Web/Views/Shared/_FormError.cshtml` - doc-comment example slug renamed
- `DeckFlow.Web/Controllers/DeckPacketController.cs` - "Processes a ChatGPT workflow postback" -> "Processes a prompt workflow postback"
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` - 2 doc comments genericized; `:2131` model-key default kept
- `DeckFlow.Web/e2e/print-manabase-results.spec.ts` - `data-chatgpt-print` -> `data-prompt-print` selector
- `DeckFlow.Web.Tests/HelpContentServiceTests.cs` - `chatgpt-analysis` slug/title -> `prompt-analysis`/`Prompt Analysis`
- `DeckFlow.Web.Tests/HelpControllerTests.cs` - same slug/title rename

## Decisions Made
- `ChatGptSwapPrompt` -> `PromptSwapPrompt`: pure identifier rename, no value/copy change (D4) — verified by the Manabase xUnit suite passing unchanged and by grepping the rendered "Copy this prompt for ChatGPT / Claude" copy stayed intact.
- Left 3 additional pre-existing "ChatGPT" mentions in doc-comment prose (`ManabaseAnalysisService.cs:25`, `ManabaseViewModel.cs:7`, `ManabaseController.cs:13`) untouched — they were not named in the plan's `<interfaces>` rename list or acceptance criteria, so renaming them would have silently widened scope beyond the dual-reviewed plan. Flagging here for a future pass (see Known Stubs / Issues below) rather than fixing unilaterally.
- Reworded DeckAnalysisPacketService.cs:1815 to "that the prompt should follow" (added "the") instead of the bare literal "prompt" substitution, to keep the sentence grammatical while satisfying the "old phrase gone" acceptance check.

## Deviations from Plan

None — plan executed exactly as written for all 3 tasks. The 3 out-of-scope doc-comment mentions noted above were left alone (no fix applied), which is a deliberate non-deviation (scope discipline), not an auto-fix.

## Issues Encountered
None.

## Known Stubs / Follow-ups
- `ManabaseAnalysisService.cs:25` (`<param name="deckName">... used in the ChatGPT prompt).</param>`), `ManabaseViewModel.cs:7` (class summary "... ChatGPT swap prompt)"), and `ManabaseController.cs:13` (class summary "... an optional ChatGPT ...") still say "ChatGPT" in internal (non-visible) doc-comment prose. These were not part of this plan's enumerated doc-comment list (only DeckPacketController.cs:151 and DeckAnalysisPacketService.cs:1005/:1815 were scoped) and don't fail any acceptance criterion. Candidate for a follow-up naming-cleanup pass if full identifier-form eradication across doc comments is later desired.

## Grep Status (identifier-form chatgpt/ChatGpt across the 13 edited files)
Remaining matches are all D3 keep-list user-visible copy or the noted doc-comment prose (not renamed, see above) — zero identifier/attribute-form `chatgpt-`/`ChatGpt` remains:
- `ManabaseAnalysisService.cs:25`, `ManabaseViewModel.cs:7`, `ManabaseController.cs:13` — doc-comment prose (out of scope, see above)
- `Manabase.cshtml:625` — "Copy this prompt for ChatGPT / Claude" (KEEP)
- `DeckPrimer.cshtml:6,42` — "ChatGPT-ready primer prompt" / "for ChatGPT, Claude, or Gemini" (KEEP)
- `ContentKb/Detail.cshtml:35,41,42` — "paste into ChatGPT..." / "Copy this ChatGPT-ready prompt" / "Copy prompt for ChatGPT" (KEEP)
- `DeckAnalysisPacketService.cs:2131` — `"ChatGPT"` TargetAiPlatform model-key default (KEEP)

## Build & Test Status
- `dotnet build DeckFlow.sln`: **0 Warning(s), 0 Error(s)** (verified after each task and at plan completion).
- `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~Manabase"`: **149/149 passed**.
- `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~HelpContentService|FullyQualifiedName~HelpController"`: **12/12 passed**.
- `npx playwright test print-manabase-results` (chromium-desktop + chromium-mobile, headless via webServer auto-start, `DECKFLOW_DISABLE_AUTO_BROWSER=true`): **2/2 passed**.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All AICLEAN-02/03 requirements for this plan's file set are satisfied; ready for 85-05 (final wave-boundary byte-identical validation across 85-02/85-03/85-04).
- No blockers. The 3 flagged out-of-scope doc-comment mentions are cosmetic-only and don't block anything downstream.

## Self-Check: PASSED

All 13 modified files confirmed present on disk; all 3 task commits (`984faffc`, `54da2447`, `0df9a582`) confirmed in `git log`.

---
*Phase: 85-chatgpt-naming-cleanup*
*Completed: 2026-07-05*
