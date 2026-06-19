# Phase 56 — Deferred / Out-of-Scope Items

## From 56-03 execution (2026-06-18)

- **`DeckFlow.sln` build error: `RenderPublishStateBadge` does not exist (Publish.razor:54).**
  - Out of scope for Plan 56-03 (ALLOWED FILE SET = FakeContentKbOrchestrator.cs, Blocked.razor,
    NavMenu.razor, BlockedPageTests.cs only). The error is in `DeckFlow.Studio/Pages/Publish.razor`,
    which is being edited concurrently by Plan 56-01/02 execution in the **same working tree**.
  - Cause: Plan 02 added the PUB-03 publish-state summary markup that calls `RenderPublishStateBadge(...)`
    (56-PATTERNS.md lines 399-409) but the static `RenderPublishStateBadge` method (lines 412-421) was
    not yet present when the full-solution build ran. `Publish.razor` and `PublishPageTests.cs` are
    uncommitted in the working tree at the time of 56-03 completion.
  - NOT caused by 56-03 changes. 56-03's own files build clean and its 3 bUnit tests pass.
  - Resolution owner: Plan 56-02 executor (will add the missing static method per PATTERNS.md).
