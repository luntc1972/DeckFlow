---
phase: 56-studio-surfaces
plan: 03
subsystem: DeckFlow.Studio
tags: [studio, blocked-videos, blazor, bunit, REM-02]
requires:
  - IContentMaintenanceOrchestrator (DeckFlow.Core, already DI-registered via AddContentKbOrchestrator)
provides:
  - "/blocked Studio page (list + unblock)"
  - "Blocked nav entry"
  - "FakeContentKbOrchestrator canned maintenance returns (also consumed by Plan 04)"
affects:
  - DeckFlow.Studio
  - DeckFlow.Studio.Tests
tech-stack:
  added: []
  patterns: [Task.Run-off-sync-context, disposal-safe-InvokeAsync, IDisposable+CTS, bUnit-WaitForAssertion]
key-files:
  created:
    - DeckFlow.Studio/Pages/Blocked.razor
    - DeckFlow.Studio.Tests/BlockedPageTests.cs
  modified:
    - DeckFlow.Studio.Tests/TestDoubles/FakeContentKbOrchestrator.cs
    - DeckFlow.Studio/Shared/NavMenu.razor
decisions:
  - "Unblock is a recovery action: btn-outline-secondary, no confirmation step (per UI-SPEC)."
  - "BlockedVideoListResult exposes .Items (not .Videos); PATTERNS.md correction honored."
metrics:
  duration: ~25m
  completed: 2026-06-18
  tasks: 3
  files: 4
  executor: Codex (gpt-5.4) via cross-AI; Claude orchestrated/verified
---

# Phase 56 Plan 03: Blocked Videos Studio Surface Summary

Wired the existing Core block/unblock/list domain logic into a new Studio `/blocked` page so the
operator can view blocked videos and unblock any of them without dropping to the CLI (REM-02), plus
canned `IContentMaintenanceOrchestrator` returns on the test fake (also unblocks Plan 04).

## What Was Built

- **Task 1 — FakeContentKbOrchestrator canned returns** (commit `b71d315`): replaced the three
  `NotImplementedException` bodies (`BlockVideoAsync`, `UnblockVideoAsync`, `ListBlockedAsync`) with
  configurable canned returns — `CannedBlockedResult` (default empty `Items`), `CannedMaintenanceResult`
  (default `{ Success = true }`), and call-recording lists `BlockCalls` / `UnblockCalls`. Uses
  `result.Items` (not `.Videos`). `ResetCorpusAsync` and the source-manager methods left throwing as-is.
- **Task 2 — Blocked.razor + NavMenu** (commit `8dc0e0e`): new `/blocked` page (`@implements IDisposable`)
  that lists each blocked video (youtu.be link, blocked-at UTC, reason or em-dash) with an Unblock action.
  `ListBlockedAsync` and `UnblockVideoAsync` run inside `Task.Run` off the Blazor sync context; UI updates
  via disposal-safe `InvokeAsync`. Loading-spinner, alert-danger error, and empty-state copy per UI-SPEC.
  Unblock removes the row in-memory (no reload), `btn-outline-secondary`, no confirmation. NavMenu gains a
  Blocked entry (`href="blocked"`, `oi-ban`) after Direct Push.
- **Task 3 — BlockedPageTests** (commit `7c4bd87`): 3 bUnit `[Fact]`s — empty state, populated table,
  and unblock-removes-row (pins that the fake recorded `UnblockVideoAsync` for the clicked id).

## Verification

- `dotnet build DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj` — 0 errors, 0 warnings.
- `dotnet build DeckFlow.Studio/DeckFlow.Studio.csproj` — 0 errors (1 pre-existing Core xmldoc warning).
- `dotnet test --filter BlockedPageTests` — **Passed 3/3, 0 failed** (VSTest ran cleanly in WSL this time).
- `grep '.Items' Blocked.razor` = 1; `grep '.Videos' Blocked.razor` = 0; `grep 'href="blocked"' NavMenu.razor` = 1.
- Format gate (`scripts/format-check-changed.sh staged`) — exit 0 on each commit. LF preserved.

## Deviations from Plan

None affecting this plan's files — plan executed exactly as written (with the PATTERNS.md `.Items`
correction the plan itself called out).

### Out-of-Scope Discovery (logged, not fixed)

- The **full-solution** `dotnet build DeckFlow.sln` reports 1 error: `RenderPublishStateBadge` does not
  exist in `DeckFlow.Studio/Pages/Publish.razor:54`. This is **Plan 56-02's** PUB-03 work-in-progress
  (the summary markup calling `RenderPublishStateBadge` was added but its static method not yet defined),
  edited concurrently in the **same shared working tree**. It is outside Plan 56-03's ALLOWED FILE SET and
  not caused by any 56-03 change. Logged to `deferred-items.md`; resolution owner is the Plan 56-02
  executor. Plan 56-03's own two projects build clean and its tests pass in isolation.

## Commits

- `b71d315` test(phase-56): wire canned maintenance returns on FakeContentKbOrchestrator
- `8dc0e0e` feat(phase-56): add Blocked videos Studio page + nav entry
- `7c4bd87` test(phase-56): add BlockedPageTests bUnit coverage

## Threat Surface

All four 56-03 STRIDE entries satisfied: exact-id passed to UnblockVideoAsync (T-01, bUnit-pinned),
all orchestrator calls in Task.Run + disposal-safe InvokeAsync (T-02), operator-safe error copy with no
stack traces (T-03), unblock as intentional reversible recovery (T-04, accept). No package installs (T-SC).

## Self-Check: PASSED

- FOUND: DeckFlow.Studio/Pages/Blocked.razor
- FOUND: DeckFlow.Studio.Tests/BlockedPageTests.cs
- FOUND: DeckFlow.Studio.Tests/TestDoubles/FakeContentKbOrchestrator.cs (canned returns)
- FOUND: DeckFlow.Studio/Shared/NavMenu.razor (href="blocked")
- FOUND commit: b71d315, 8dc0e0e, 7c4bd87
