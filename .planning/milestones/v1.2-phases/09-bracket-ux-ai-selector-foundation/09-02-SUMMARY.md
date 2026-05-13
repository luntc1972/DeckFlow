---
phase: 09-bracket-ux-ai-selector-foundation
plan: "02"
subsystem: ChatGPT request models + zip round-trip
tags: [models, services, round-trip, ai-selector]
dependency_graph:
  requires: []
  provides:
    - TargetAiPlatform property on ChatGptDeckRequest (POST binding + zip round-trip)
    - TargetAiPlatform property on ChatGptDeckComparisonRequest (POST binding)
    - TargetAiPlatform property on ChatGptCedhMetaGapRequest (POST binding)
    - target_ai_platform serialized in 01-request-context.txt
    - ParsedRequestContext.TargetAiPlatform init property
    - LoadFromZip restores TargetAiPlatform (legacy zip defaults to ChatGPT)
  affects:
    - DeckFlow.Web/Models/ChatGptDeckRequest.cs
    - DeckFlow.Web/Models/ChatGptDeckComparisonRequest.cs
    - DeckFlow.Web/Models/ChatGptCedhMetaGapRequest.cs
    - DeckFlow.Web/Services/ChatGptDeckPacketService.cs
    - DeckFlow.Web/Services/ChatGptRequestContextParser.cs
    - DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs
tech_stack:
  added: []
  patterns:
    - null-guard backing-field on string property (existing pattern, extended)
    - init property on sealed record (existing pattern, extended)
key_files:
  created: []
  modified:
    - DeckFlow.Web/Models/ChatGptDeckRequest.cs
    - DeckFlow.Web/Models/ChatGptDeckComparisonRequest.cs
    - DeckFlow.Web/Models/ChatGptCedhMetaGapRequest.cs
    - DeckFlow.Web/Services/ChatGptDeckPacketService.cs
    - DeckFlow.Web/Services/ChatGptRequestContextParser.cs
    - DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs
decisions:
  - "TargetAiPlatform zip round-trip wired only for Packets page (LoadFromZip); Comparison and CEDH load methods left untouched until Phase 10"
  - "Legacy zips without target_ai_platform key silently default to ChatGPT via the null-guard setter"
metrics:
  duration: "~6 minutes"
  completed: "2026-05-08"
  tasks_completed: 2
  tasks_total: 2
  files_changed: 6
---

# Phase 9 Plan 02: TargetAiPlatform Model + Round-Trip Summary

## One-liner

TargetAiPlatform string property (default "ChatGPT", null-guard) added to all three ChatGPT request models; full zip round-trip wired for the Packets page — writer emits `target_ai_platform:` in `01-request-context.txt`, parser reads it, `LoadFromZip` restores it; Comparison and CEDH untouched until Phase 10.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | Add TargetAiPlatform property to all three request models | d166900 | ChatGptDeckRequest.cs, ChatGptDeckComparisonRequest.cs, ChatGptCedhMetaGapRequest.cs |
| 2 | Wire round-trip — writer, parser, loader | 5b4e777 | ChatGptDeckPacketService.cs, ChatGptRequestContextParser.cs, ChatGptPacketArtifactStore.cs |

## What Was Built

- **Three request models** gained a `TargetAiPlatform` string property with `_targetAiPlatform = "ChatGPT"` backing field and `value ?? "ChatGPT"` null-guard setter, following the exact existing pattern.
- **`BuildRequestContextText`** in `ChatGptDeckPacketService` now emits `target_ai_platform: {value}` immediately after the `target_commander_bracket:` line in `01-request-context.txt`.
- **`ChatGptRequestContextParser`**: local variable `targetAiPlatform`, new `case "target_ai_platform"` in the inline-scalar switch, return constructor field `TargetAiPlatform = ...`, and `TargetAiPlatform { get; init; }` property on `ParsedRequestContext`.
- **`ChatGptPacketArtifactStore.LoadFromZip`**: restore block `if (parsed.TargetAiPlatform is not null) { request.TargetAiPlatform = parsed.TargetAiPlatform; }` added after the `DeckSource` restore block.

## Deviations from Plan

**[Rule 3 - Blocking] Symlinked node_modules to unblock TypeScript build in worktree**

- **Found during:** Task 1 dotnet build verification
- **Issue:** The git worktree had no `DeckFlow.Web/node_modules`; MSBuild TypeScript target failed with "Cannot find module .../tsc".
- **Fix:** Created a symlink from the worktree's `DeckFlow.Web/node_modules` to the main repo's `DeckFlow.Web/node_modules`. Symlink is worktree-local and not committed.
- **Files modified:** None committed — symlink is worktree-only.

## Known Stubs

None. `TargetAiPlatform` is fully wired for the Packets page round-trip. The Comparison and CEDH pages have the property for POST binding but no zip round-trip — this is intentional Phase 10 scope, not a stub.

## Threat Flags

None. T-09-03, T-09-04, T-09-05 from the plan's threat model were reviewed and all dispositioned `accept` — no security-sensitive branching on `TargetAiPlatform` in Phase 9.

## Self-Check: PASSED

Files verified:
- FOUND: DeckFlow.Web/Models/ChatGptDeckRequest.cs (TargetAiPlatform x4)
- FOUND: DeckFlow.Web/Models/ChatGptDeckComparisonRequest.cs (TargetAiPlatform x4)
- FOUND: DeckFlow.Web/Models/ChatGptCedhMetaGapRequest.cs (TargetAiPlatform x4)
- FOUND: target_ai_platform in ChatGptDeckPacketService.cs line 1659
- FOUND: ParsedRequestContext.TargetAiPlatform init property line 282
- FOUND: parsed.TargetAiPlatform in ChatGptPacketArtifactStore.cs line 222
- Commits d166900 and 5b4e777 exist on worktree-agent-a4e106da88d9d9def
- dotnet build DeckFlow.Web: 0 errors, 0 warnings
- dotnet build DeckFlow.Web.Tests: 0 errors, 0 warnings
