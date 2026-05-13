---
phase: 10-claude-gemini-artifact-optimization
plan: 01
subsystem: api
tags: [aspnet, prompt-engineering, claude, gemini, dispatch-primitive]

requires:
  - phase: 09-bracket-ux-ai-selector-foundation
    provides: TargetAiPlatform request property + Phase 9 setter that normalizes null/unknown to "ChatGPT"
provides:
  - Per-AI dispatch pattern in ChatGptDeckPacketService.BuildAnalysisPrompt (switch on TargetAiPlatform)
  - private static helpers BuildAnalysisPromptChatGpt / BuildAnalysisPromptClaude / BuildAnalysisPromptGemini
  - Shared private const ChatGptResultWrapInstruction
  - Claude XML skeleton variant proven on the highest-traffic prompt builder
  - Gemini markdown+tweaks variant proven on the highest-traffic prompt builder
affects: [10-02, 10-03]

tech-stack:
  added: []
  patterns:
    - "Switch-expression dispatch on request.TargetAiPlatform at top of each Build*Prompt method (D-13)"
    - "Per-AI prompt body lives in *ChatGpt/*Claude/*Gemini private static helpers in same file"
    - "Shared cross-AI result-wrap instruction const referenced by all three variants (D-08)"

key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/ChatGptDeckPacketService.cs

key-decisions:
  - "Used string-concat (`'<' + 'task>'`) when emitting literal <task> and </task> at runtime to keep the file legible across tools, with a documenting comment near the emission site so future readers understand why."
  - "Reproduced ALL conditional content (full-decklist, preferred_categories, protected_cards, includeCardVersions blocks) in the Claude variant by faithfully porting from BuildAnalysisPromptChatGpt — not by emitting sentinel strings only. Acceptance grep guards verify the distinctive strings; the conditional logic itself is real."

patterns-established:
  - "Per-AI prompt dispatch primitive: switch-expression on TargetAiPlatform with three arms (Claude, Gemini, default) and per-AI helpers using identical signatures so existing callers stay unchanged"
  - "Cross-AI <result>...</result> output-wrap directive appended in OUTPUT FORMAT (markdown variants) or inside <task> (XML variant) — primary path; existing fenced-JSON instruction stays as a fallback"

requirements-completed: [AISEL-02, AISEL-03]

duration: 1h (Codex full gpt-5.4, single pass + QA twice)
completed: 2026-05-09
---

# Phase 10-01: Per-AI Dispatch Primitive + Packets-Claude/Gemini Analysis Prompt Variants

**Per-AI dispatch on request.TargetAiPlatform proven on BuildAnalysisPrompt with full Claude XML skeleton and full Gemini markdown+tweaks variants; ChatGPT path unchanged except for the new <result> wrap directive.**

## Performance

- **Duration:** ~1 hour wallclock (Codex full gpt-5.4, one dispatch + two QA passes)
- **Completed:** 2026-05-09
- **Tasks:** 3 (dispatch + ChatGpt rename + result-wrap append; Claude variant; Gemini variant)
- **Files modified:** 1
- **Lines changed:** +444

## Accomplishments

- Validated D-13 (in-service per-AI dispatch) on the highest-traffic prompt builder before fanning out in plan 10-02
- Validated D-01..D-08 content split (Claude XML / Gemini markdown+tweaks / ChatGPT append-only) on the same builder
- Single switch-expression at top of BuildAnalysisPrompt routes to three private static helpers — caller (BuildAsync line 433) untouched
- Pre-change ChatGPT prompt body preserved byte-equivalent inside renamed BuildAnalysisPromptChatGpt PLUS the new ChatGptResultWrapInstruction line in OUTPUT FORMAT (zero-regression target met — Phase 10 SC #4)
- Claude variant emits the D-02 tag taxonomy (`<role>`, `<commander>`, `<bracket>`, `<deck>`, `<reference>` with nested `<cards>`/`<combos>`/`<banlist>`, `<questions>`, `<output_schema>`, `<task>`) with data sections first and instructions in `<task>` last (D-03)
- Claude variant ports all conditional content from the ChatGpt body (full-decklist, preferred_categories, protected_cards, includeCardVersions) — not just sentinel strings; acceptance grep guards verify the distinctive strings exist; the conditional logic itself replicates the source
- Zero `<system>` / `<human>` / `<assistant>` tags anywhere in the file (D-04 enforced; verified by grep guard returning 0 lines)
- Gemini variant layers four tweaks (persona block, step-by-step scaffolding, schema-strictness language at start of OUTPUT FORMAT, ChatGptResultWrapInstruction at end) onto a copy of the ChatGpt markdown skeleton (D-05, D-06)
- All three variants instruct the AI to wrap JSON response in `<result>...</result>` tags (D-08); server response parser shim is built in plan 10-03

## Task Commits

Single atomic commit captures all three tasks since they touch the same file and form one coherent dispatch primitive:

1. **All three tasks** — `6c24180` (feat)

**Plan metadata:** TBD on next docs commit

## Files Created/Modified

- `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` — added per-AI dispatch + three private helpers + shared const, +444 lines

## Decisions Made

- **String-concat for literal `<task>` tag emission** at runtime: `builder.AppendLine("<" + "task>")`. Documenting comment placed near the emission site. Avoids any in-file XML/doc parser confusion while still emitting the correct byte sequence to the user.
- **Faithful conditional port over sentinel-only emission** for the Claude variant: the `if (requiresFullDecklists)`, preferred-categories, protected-cards, and includeCardVersions conditional blocks each contain real ports of the corresponding ChatGpt logic (verified during QA pass 2 by visual inspection — not just the distinctive strings the grep guards check).
- **No deviation from plan structure**: dispatch + three helpers in a single file, exactly as the plan specified.

## Deviations from Plan

None — plan executed exactly as written. Three-task scope delivered as one atomic commit because the boundaries between tasks (dispatch primitive, Claude variant body, Gemini variant body) are not externally observable — together they form the in-service dispatch primitive and would not compile or run correctly if split mid-flight.

## Issues Encountered

- Sandbox Roslyn named-pipe permissions tripped the default `dotnet build` invocation. Resolved by running `dotnet build DeckFlow.sln -m:1 -p:UseSharedCompilation=false` inside the Codex sandbox. Build also passes cleanly via plain `dotnet build DeckFlow.sln` from the local WSL session.

## Next Phase Readiness

Plan 10-02 unblocked. Pattern is proven: BuildSetUpgradePrompt, BuildComparisonPrompt, BuildFollowUpPrompt, BuildPrompt (CedhMetaGap) can each get the same switch-expression dispatch + three helpers using this implementation as the analog.

Plan 10-03 (zip round-trip + `<result>` extraction shim) is independent of 10-02's prompt-content fanout but depends on 10-02's const promotion (10-02 task 1 moves `ChatGptResultWrapInstruction` from `ChatGptDeckPacketService` to `ChatGptJsonTextFormatterService` so all three services can share it). Wave 3 ordering for 10-03 prevents file-conflict on `ChatGptJsonTextFormatterService.cs`.

---
*Phase: 10-claude-gemini-artifact-optimization*
*Completed: 2026-05-09*
