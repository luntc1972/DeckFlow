---
phase: 10-claude-gemini-artifact-optimization
plan: 02
subsystem: api
tags: [aspnet, prompt-engineering, claude, gemini, fanout]

requires:
  - phase: 10-claude-gemini-artifact-optimization
    provides: Per-AI dispatch primitive on BuildAnalysisPrompt (10-01) + ChatGptResultWrapInstruction const
provides:
  - Per-AI dispatch + 3 variants on BuildSetUpgradePrompt (Packet service)
  - Per-AI dispatch + 3 variants on BuildComparisonPrompt and BuildFollowUpPrompt (Comparison service) — both methods accept new targetAiPlatform parameter
  - Per-AI dispatch + 3 variants on BuildPrompt (CedhMetaGap service) — accepts new targetAiPlatform parameter
  - ChatGptResultWrapInstruction promoted to internal const on ChatGptJsonTextFormatterService for cross-service reuse
affects: [10-03]

tech-stack:
  added: []
  patterns:
    - "Cross-service shared constant: when multiple services need to emit the same instruction, declare it as `internal const` on the closest existing utility class rather than private per-service."
    - "Targeted parameter threading: Comparison and CedhMetaGap dispatchers receive a `string targetAiPlatform` parameter (not the full request object) so the test seam stays narrow."
    - "Faithful conditional ports: Claude/Gemini variants of each builder replicate the SAME conditional logic (requiresFullDecklists, etc.) as the ChatGpt variant, not just sentinel strings."

key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/ChatGptJsonTextFormatterService.cs
    - DeckFlow.Web/Services/ChatGptDeckPacketService.cs
    - DeckFlow.Web/Services/ChatGptDeckComparisonService.cs
    - DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs

key-decisions:
  - "ChatGptResultWrapInstruction promoted from private-per-service to internal-on-ChatGptJsonTextFormatterService. Single source of truth across the three services that emit prompts."
  - "Comparison and CedhMetaGap dispatchers accept `string targetAiPlatform` — narrow seam, no full-request coupling. Caller in BuildAsync threads `request.TargetAiPlatform` through. Packet service caller already passed full request (Phase 9 plumbing) so no change needed there."
  - "All Gemini variants use the patched schema-strictness wording introduced by commit faa6ba3 (after the original contradictory wording was caught in code review of 10-01). Searched and confirmed the original wording is absent from the codebase."
  - "Used `\"<\" + \"task>\"` string-concatenation for literal `<task>` / `</task>` tag emission in every Claude variant — same convention 10-01 established."

patterns-established:
  - "Per-AI dispatch fanout: rename existing body to *ChatGpt, append ChatGptResultWrapInstruction, add new dispatcher with same signature, add *Claude (XML) and *Gemini (markdown+tweaks) variants. Apply identically to every prompt builder."
  - "Cross-service const consumption: qualify with full type name (`ChatGptJsonTextFormatterService.ChatGptResultWrapInstruction`) rather than `using static` to keep call sites self-documenting about the cross-service dependency."

requirements-completed: [AISEL-02, AISEL-03]

duration: 1h 30min (Codex full gpt-5.4, single pass + QA twice)
completed: 2026-05-09
---

# Phase 10-02: Per-AI Dispatch Fanout for Set-Upgrade, Comparison, Follow-Up, and Meta-Gap Prompts

**Per-AI dispatch primitive proven in 10-01 fanned out across all four remaining prompt builders. AISEL-02 and AISEL-03 content-complete; every prompt the user generates is now keyed off request.TargetAiPlatform.**

## Performance

- **Duration:** ~1.5 hours wallclock (Codex full gpt-5.4, one dispatch + two QA passes)
- **Completed:** 2026-05-09
- **Tasks:** 4 (const move + 3 service fanouts)
- **Files modified:** 4
- **Lines changed:** +911 / -9

## Accomplishments

- Closes AISEL-02 and AISEL-03 across all three ChatGPT analysis pages (Packets, Comparison, CedhMetaGap) and both prompt types within each page
- ChatGptResultWrapInstruction now lives on ChatGptJsonTextFormatterService as `internal const` — single source of truth shared by all three services
- Five prompt builders total now dispatch per-AI: BuildAnalysisPrompt (10-01), BuildSetUpgradePrompt, BuildComparisonPrompt, BuildFollowUpPrompt, BuildPrompt (CedhMetaGap). 15 concrete variants in service code.
- D-04 enforcement holds across all four files: zero `<system>` / `<human>` / `<assistant>` runtime tags emitted by any Claude variant (verified by grep filtered to non-comment lines)
- D-13 enforcement: dispatch happens inside each service. Controllers stay AI-agnostic. Comparison and CedhMetaGap dispatchers receive `targetAiPlatform` as a narrow string parameter (not the full request)
- D-07 zero-regression: ChatGpt variants are byte-equivalent to pre-change bodies plus exactly the new ChatGptResultWrapInstruction line in OUTPUT FORMAT
- All Gemini variants use the patched schema-strictness wording from commit `faa6ba3` (original contradictory wording confirmed absent from the codebase via grep)
- Conditional content blocks (full-decklist, preferred-categories, protected-cards, includeCardVersions in BuildSetUpgrade; follow-up directives in BuildFollowUp; reference-deck loops in BuildPrompt) are real ports of the corresponding ChatGpt logic in every Claude/Gemini variant — not just sentinel strings

## Task Commits

Single atomic commit captures all four tasks since they form one coherent fanout and the const-move dependency means task 1 must land with task 2/3/4:

1. **All four tasks** — `93454b6` (feat)

**Plan metadata:** TBD on next docs commit

## Files Created/Modified

- `DeckFlow.Web/Services/ChatGptJsonTextFormatterService.cs` — `+5 / 0`. Added `internal const string ChatGptResultWrapInstruction`.
- `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` — `+365 / -3`. BuildSetUpgradePrompt dispatch + 3 variants. References to ChatGptResultWrapInstruction now qualified.
- `DeckFlow.Web/Services/ChatGptDeckComparisonService.cs` — `+299 / -3`. BuildComparisonPrompt + BuildFollowUpPrompt dispatch + 6 variants total. Caller sites updated to pass request.TargetAiPlatform.
- `DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs` — `+251 / -3`. BuildPrompt dispatch + 3 variants. Caller site updated.

## Decisions Made

- **ChatGptResultWrapInstruction lives on ChatGptJsonTextFormatterService**. The class is the existing utility for response-text formatting and is the natural home for the cross-AI parsing-parity contract. `internal` visibility scopes it to the assembly. Single source of truth.
- **Targeted parameter threading over request-object coupling** for Comparison and CedhMetaGap dispatchers. The dispatcher signatures take `string targetAiPlatform` rather than the full `ChatGptDeck*Request`. Tests can call the dispatcher with a literal string; helpers don't need to know about the request shape.
- **String-concat for literal <task> tag emission** continued from 10-01 convention. Documenting comment near the emission site explains why.
- **One atomic commit for all four tasks** rather than per-task split. Rationale: task 1 (const move) is a precondition for tasks 2/3/4 (which all reference the moved const), so any per-task split would either duplicate the const declaration mid-history or leave a broken intermediate state. The commit message documents each task's contribution.

## Deviations from Plan

None — plan executed exactly as written. Codex used the existing string-concat convention for `<task>` emission (matches 10-01) and confirmed `deckA.Bracket.Label` / `deckB.Bracket.Label` is the correct nested-record access for the comparison Claude variant (the plan flagged this as W-3 in iteration-1 review and the planner's revision locked it correctly).

## Issues Encountered

- One QA-pass grep was too strict (looking for the full Claude `<result>` directive line as a single phrase). Codex re-ran with a simpler phrase to confirm the directive lives in all 5 Claude variants. No correctness issue.
- Sandbox `dotnet build` Roslyn named-pipe permissions surfaced again; resolved with `-m:1 -p:UseSharedCompilation=false`. Local WSL session compiles cleanly without the extra flags.

## Next Phase Readiness

Plan 10-03 unblocked. The remaining work is:
1. Comparison + CedhMetaGap zip round-trip (extend `01-request-context.txt` writer + parser to those two zip layouts so `target_ai_platform` and other form-state round-trips through upload/resume) — closes AISEL-04 fully (Phase 9 only delivered Packets round-trip).
2. `<result>` extraction shim in `ChatGptJsonTextFormatterService.ExtractJsonPayload` — single helper-level insertion (per Open Question Q2 RESOLVED) that covers all four response parsers.

10-03 is Wave 3, depends_on 10-02 (because 10-02 task 1 promoted the shared const into ChatGptJsonTextFormatterService.cs and 10-03 task 1 inserts the regex shim into the same file — wave ordering prevents file-conflict).

After 10-03 ships, Phase 10 reaches the final human-verify checkpoint (Phase 9-style: paste a Claude artifact into claude.ai and a Gemini artifact into gemini.google.com, confirm well-formed JSON returns, and confirm zero regression on the default ChatGPT flow).

---
*Phase: 10-claude-gemini-artifact-optimization*
*Completed: 2026-05-09*
