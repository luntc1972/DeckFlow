---
phase: 62-studio-ui-polish
plan: "04"
subsystem: DeckFlow.Studio
tags: [ui-polish, navigation, workflow, bunit]
dependency_graph:
  requires: [62-01, 62-02]
  provides: [grouped-nav, review-publish-link]
  affects: [DeckFlow.Studio/Shared/NavMenu.razor, DeckFlow.Studio/Pages/Review.razor]
tech_stack:
  added: []
  patterns: [RenderFragment, grouped-nav-sections]
key_files:
  created:
    - DeckFlow.Studio.Tests/NavMenuTests.cs
  modified:
    - DeckFlow.Studio/Shared/NavMenu.razor
    - DeckFlow.Studio/Pages/Review.razor
    - DeckFlow.Studio.Tests/ReviewPageTests.cs
    - README.md
decisions:
  - "RenderGoToPublishLink extracted as a RenderFragment method (same pattern as RenderBatchBar / RenderCreatorFilter) to avoid @{} inside else-block Razor limitation"
  - "NavMenu section headers use .nav-section-header CSS class and a .nav-section-divider so they are styleable without touching theme CSS"
  - "A2 verified-not-broken rather than changed: ToggleAllChannelSelections already scopes to GetVisibleChannelVideos() from Phase 61; new bUnit test confirms the scoping invariant"
metrics:
  duration: "25m"
  completed: "2026-06-21T16:51:00Z"
  tasks_completed: 6
  files_changed: 5
---

# Phase 62 Plan 04: Studio UI Polish — Flow Tightening & Grouped Nav Summary

SUI-02 + SUI-04 final polish: Review→Publish shortcut link, grouped Pipeline/Support navigation, and bUnit coverage for all three acceptance items (A1/A2/A3).

## What Was Built

### Task 1 — A3: NavMenu.razor grouped sections
Restructured `NavMenu.razor` from a flat list into two named sections:
- **Pipeline** section header: Home, Harvest, Creators, Review, Publish, Direct Push, Pull from Prod
- **Support** section header (with a visual divider): Skipped, Blocked

All nine existing destinations are preserved. Section headers use `.nav-section-header` and `.nav-section-divider` CSS classes. No link was removed or reordered within the pipeline flow.

### Task 2 — A1: Review.razor "Go to Publish" link
Added `RenderGoToPublishLink()` — a `RenderFragment` method (same pattern as `RenderBatchBar` / `RenderCreatorFilter`) that:
- Is present and links to `/publish` when `_allRows.Count(r => r.ApprovalStatus == "approved") > 0`
- Is absent/empty when approved count is 0
- Shows the count inline: "Go to Publish (N approved)"
- Is navigation only — no behavior, no handler, no data mutation

### Task 3 — A2: Harvest Select-All scoping confirmed
`ToggleAllChannelSelections()` already scopes to `GetVisibleChannelVideos()` (the canonical visible projection from Phase 61). No code change required; invariant confirmed by inspection and a new bUnit test.

### Task 4 — bUnit tests
- **NavMenuTests.cs** (12 tests): all nine hrefs present, both section headers rendered, Pipeline precedes Support in document order, exactly nine `nav a.nav-link` elements
- **ReviewPageTests.cs** (4 new tests): link present when approved>0 with correct href; link absent when approved=0; count shown in link text; SelectAll on pending tab never selects the approved row

### Task 5 — README
Added bullet under "What's new in Cycle 10" documenting the grouped nav and Review→Publish shortcut.

### Task 6 — Build + test
- `DeckFlow.Studio.csproj`: 0 errors, 4 pre-existing NU1903 warnings (SQLitePCLRaw)
- `DeckFlow.Studio.Tests.csproj`: 0 errors
- Studio.Tests: **140/140 passed** (127 pre-existing + 13 new)

## Deviations from Plan

### Auto-fixed Issues

None.

### Observations

**A2 was verify-only:** The plan asked to "confirm/keep Harvest Select-All scoped to the visible/filtered rows." `ToggleAllChannelSelections()` already called `GetVisibleChannelVideos()` from Phase 61. The task produced a bUnit test to lock in the invariant but no production code change.

**Razor @{} limitation inside else blocks:** The first attempt to inline the approved-count variable via `@{ var approvedCount = ... }` inside the `else` block caused Razor RZ1010 error ("Unexpected { after @"). Extracting to `RenderGoToPublishLink()` — matching the existing `RenderBatchBar`/`RenderCreatorFilter` pattern — resolved this cleanly. Tracked as deviation [Rule 3 - Blocking fix].

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1–6  | 09b3c460 | feat(62-04): grouped nav sections, Review→Publish link, A1/A2/A3 bUnit tests (SUI-02/SUI-04) |

## Known Stubs

None. All rendered values are sourced from live `_allRows` state.

## Threat Flags

None. This plan is presentation/navigation only — no new network endpoints, auth paths, file access patterns, or schema changes.

## Self-Check: PASSED

- NavMenuTests.cs: exists at `DeckFlow.Studio.Tests/NavMenuTests.cs`
- Review.razor: `RenderGoToPublishLink` method present, `@RenderGoToPublishLink()` call in markup
- NavMenu.razor: `.nav-section-header` elements present for Pipeline and Support
- Commit 09b3c460 exists in git log
- Studio.Tests: 140/140 passed, 0 failures
