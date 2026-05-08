---
phase: 09-bracket-ux-ai-selector-foundation
plan: "03"
subsystem: Razor views / AI selector UX
tags: [razor, ai-selector, bracket-callout, phase9]
dependency_graph:
  requires:
    - 09-01 (CSS + partials created)
    - 09-02 (TargetAiPlatform on request models)
  provides:
    - _AiSelector rendered at top of Step 2 on all three ChatGPT analysis pages
    - TargetCommanderBracket wrapped in .bracket-callout in ChatGptPackets Step 2
  affects:
    - DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml
tech_stack:
  added: []
  patterns:
    - "@await Html.PartialAsync(\"_AiSelector\", Model.Request.TargetAiPlatform) — consistent with _MoxfieldBulkEditHint pattern"
    - ".bracket-callout div wrapping a .field label inline in the calling view (per 09-01 decision)"
key_files:
  created: []
  modified:
    - DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml
decisions:
  - "_AiSelector inserted after chatgpt-step-heading closes and before the first content div on all three pages — consistent placement across the workflow"
  - "bracket-callout is Packets-only as specified; Comparison and CEDH pages receive only the AI selector"
metrics:
  duration: "~5 minutes"
  completed: "2026-05-08"
  tasks_completed: 2
  tasks_total: 3
  files_created: 0
  files_modified: 3
---

# Phase 9 Plan 03: View Wiring — _AiSelector + Bracket Callout Summary

**One-liner:** _AiSelector partial inserted at the top of Step 2 on all three ChatGPT analysis pages; TargetCommanderBracket select wrapped in .bracket-callout with "Required before generating" eyebrow on the Packets page.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Insert _AiSelector and bracket callout into ChatGptPackets.cshtml | eaf1931 | DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml |
| 2 | Insert _AiSelector into ChatGptDeckComparison.cshtml and ChatGptCedhMetaGap.cshtml | ab368fa | DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml, DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml |

## Checkpoint Pending

Task 3 (checkpoint:human-verify) awaits visual confirmation from user. See checkpoint section below.

## Verification

- `grep -c "PartialAsync.*_AiSelector" ChatGptPackets.cshtml` → 1 (PASS)
- `grep -c "PartialAsync.*_AiSelector" ChatGptDeckComparison.cshtml` → 1 (PASS)
- `grep -c "PartialAsync.*_AiSelector" ChatGptCedhMetaGap.cshtml` → 1 (PASS)
- `grep -c "bracket-callout" ChatGptPackets.cshtml` → 2 (PASS)
- `grep -c "bracket-callout" ChatGptDeckComparison.cshtml` → 0 (PASS)
- `grep -c "bracket-callout" ChatGptCedhMetaGap.cshtml` → 0 (PASS)
- `grep "bracket-callout__label"` → "Required before generating" (PASS)
- `name="TargetCommanderBracket"` count unchanged = 1 (PASS)
- `data-df-select` count unchanged = 5 (PASS)
- `dotnet build DeckFlow.Web` — 0 errors, 0 warnings (PASS)

## Decisions Made

1. **Insertion point** — _AiSelector inserted immediately after the chatgpt-step-heading div closes, before the first content element on each page. This is visually the top of Step 2 content on all three forms.
2. **bracket-callout inlined** — Per 09-01 decision, Razor cannot encapsulate child slot content in a partial; the div wrapper is inlined directly in ChatGptPackets.cshtml.

## Deviations from Plan

None — plan executed exactly as written. All line number targets from the plan's `<interfaces>` block were verified against the actual file state before editing.

## Known Stubs

None. The AI selector renders with live `Model.Request.TargetAiPlatform` value; the bracket callout wraps the live df-select. No placeholder or hardcoded data.

## Threat Flags

None. T-09-06 and T-09-07 from the plan's threat model were reviewed — both dispositioned `accept`. No new security surface introduced.

## Self-Check: PASSED

- [x] DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml modified with both edits
- [x] DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml modified
- [x] DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml modified
- [x] Commit eaf1931 exists
- [x] Commit ab368fa exists
- [x] dotnet build DeckFlow.Web: 0 errors, 0 warnings
