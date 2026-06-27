---
phase: 56-studio-surfaces
plan: 02
subsystem: DeckFlow.Studio (Blazor Server)
tags: [studio, publish-state, blazor, bunit, PUB-03]
requires:
  - PublishStateDeriver (DeckFlow.Core/Content/PublishStateDeriver.cs, Phase 55)
  - PublishState enum + ToDisplayString (Phase 55)
provides:
  - PublishStateDeriver registered in Studio DI
  - Review.razor "Publish State" column
  - Publish.razor per-state count summary
affects:
  - DeckFlow.Studio/Program.cs
  - DeckFlow.Studio/Pages/Review.razor
  - DeckFlow.Studio/Pages/Publish.razor
tech-stack:
  added: []
  patterns:
    - "Static RenderFragment switch for badge rendering"
    - "Derivation routed exclusively through PublishStateDeriver.Derive (single source of truth)"
    - "Summary computed inside the existing approved-rows Task.Run (no extra store call)"
key-files:
  created: []
  modified:
    - DeckFlow.Studio/Program.cs
    - DeckFlow.Studio/Pages/Review.razor
    - DeckFlow.Studio/Pages/Publish.razor
    - DeckFlow.Studio.Tests/ReviewPageTests.cs
    - DeckFlow.Studio.Tests/PublishPageTests.cs
decisions:
  - "Publish.razor is summary-only (per-state counts), no per-entry table — matches 56-UI-SPEC.md"
  - "Future-dated push timestamp used in the Publish test to deterministically yield Published (not Local-newer)"
metrics:
  duration: ~25 min
  completed: 2026-06-18
---

# Phase 56 Plan 02: Studio Publish-State Surfaces Summary

Surfaced the Phase-55 derived publish-state (Never published / Pushed-hidden / Published / Local-newer) in Studio's Review and Publish pages, with all derivation routed through the single `PublishStateDeriver.Derive(...)` and zero inline four-state logic.

## What Was Built

- **Task 1 — DI registration (`a512e65`):** Added `builder.Services.AddSingleton<PublishStateDeriver>();` immediately after `VideoStatusResolver` in `Program.cs`. This closes a Phase-55 gap — the deriver was never registered, so any page injecting it would have failed at runtime.
- **Task 2 — Review.razor column (`0106bc1`):** Added a `Publish State` column between Status and Actions. Each row renders `RenderPublishStateBadge(Deriver.Derive(vm.PushedToProdUtc, vm.IsVisible, vm.IndexedUtc))`. `ReviewViewModel` gained three read-only properties (`PushedToProdUtc`, `IsVisible`, `IndexedUtc`) populated from the row; expand-row colspan bumped 6 → 7. bUnit test `ReviewPage_PublishStateColumn_ShowsNeverPublishedForUnpushedRow` added (TDD).
- **Task 3 — Publish.razor summary (`8592264`):** Added a per-state count summary (`_publishStateSummary`) computed by `GroupBy` over `Deriver.Derive(...)` inside the existing approved-rows `Task.Run` (no second store call). Summary renders only when `Count > 0`; no per-entry table (summary-only by design per 56-UI-SPEC.md). bUnit test `PublishPage_PublishStateSummary_RendersCountsForApprovedRows` added (TDD).

## Verification

- `dotnet build DeckFlow.sln` — 0 errors (2 pre-existing CS1574 XML-doc warnings, out of scope).
- `dotnet test DeckFlow.Studio.Tests --filter ReviewPageTests` — Passed 12/12.
- `dotnet test DeckFlow.Studio.Tests --filter PublishPageTests` — Passed 12/12.
- Combined Review+Publish run — Passed 24/24, 0 failed.
- `grep -c "Deriver.Derive(" Review.razor Publish.razor` — both 1 (>0).
- No inline `PushedToProdUtc.HasValue` four-state logic in either page (confirmed).
- LF line endings preserved in all five files.

VSTest ran successfully in WSL this session, so no build-only fallback was needed.

## Deviations from Plan

None — plan executed exactly as written. The plan anticipated `PublishPageTests.cs` might need creation; it already existed with a `RenderPublish` helper, so Task 3 extended it (registered the deriver + added the new summary test) rather than creating it. This matches the plan's "create or extend" intent.

## Threat Mitigations Applied

- **T-56-02-01 (logic drift):** Both pages call `PublishStateDeriver.Derive`; no inline four-state if/else (verified by grep). Single source of truth preserved.
- **T-56-02-02 (operator misreads status):** Badges carry text labels; Published badge includes the `oi oi-check` icon + text per UI-SPEC.
- **T-56-02-03 (sync-context block):** Publish summary computed inside the existing `Task.Run` that already fetches rows — no second store call, no blocking the Blazor circuit.
- **T-56-02-SC (package installs):** No package installs; bUnit 2.7.2 + xUnit 2.9.3 already present.

## Notes

- A concurrent executor committed plan 56-01 (`637f63a`, `fe685c0`) into the same `cycle9` branch/tree during this run. My three commits (`a512e65`, `0106bc1`, `8592264`) are intact and disjoint in files. No conflict.

## Self-Check: PASSED

- Files exist: Program.cs, Review.razor, Publish.razor, ReviewPageTests.cs, PublishPageTests.cs — all FOUND.
- Commits exist: a512e65, 0106bc1, 8592264 — all FOUND in git.
