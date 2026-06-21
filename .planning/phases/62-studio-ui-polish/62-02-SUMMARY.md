---
phase: 62-studio-ui-polish
plan: "02"
subsystem: DeckFlow.Studio
tags: [studio, ux, creator-filter, blazor, bunit]
dependency_graph:
  requires: [62-01]
  provides: [SUI-05]
  affects:
    - DeckFlow.Studio/Pages/Harvest.razor
    - DeckFlow.Studio/Pages/Review.razor
    - DeckFlow.Studio/Services/CreatorNameResolver.cs
    - DeckFlow.Studio.Tests/CreatorNameResolverTests.cs
    - DeckFlow.Studio.Tests/HarvestPageTests.cs
    - README.md
tech_stack:
  added: []
  patterns:
    - Pure static helper (CreatorNameResolver — no I/O, string split only)
    - RenderFragment method pattern (mirrors RenderBatchBar, avoids RZ1010 inside @if blocks)
    - Canonical visible projection (creator predicate folded into GetVisibleChannelVideos — T-62-03)
key_files:
  created:
    - DeckFlow.Studio/Services/CreatorNameResolver.cs
    - DeckFlow.Studio.Tests/CreatorNameResolverTests.cs
  modified:
    - DeckFlow.Studio/Pages/Harvest.razor
    - DeckFlow.Studio/Pages/Review.razor
    - DeckFlow.Studio.Tests/HarvestPageTests.cs
    - README.md
decisions:
  - "Creator filter not rendered for single-creator views (count <= 1) — no dropdown clutter when it adds no value"
  - "Review.razor creator filter uses RenderCreatorFilter() RenderFragment (not inline @{ } in else block) to avoid Razor RZ1010 compiler error"
  - "Harvest creator filter @{ } variable moved before outer @if to avoid RZ1010; same Razor constraint"
  - "Filter resets on new browse (Harvest) and tab switch (Review) so stale creator from A doesn't persist into B"
  - "Selections cleared when Review creator filter changes to prevent acting on hidden rows"
metrics:
  duration: "~40 minutes"
  completed: "2026-06-21"
  tasks_completed: 7
  files_changed: 6
---

# Phase 62 Plan 02: Creator Filter on Harvest and Review Summary

Creator filter (SUI-05) added to both Harvest browse and Review queue: operators can narrow lists to one creator without breaking existing filter composition or the canonical visible/selected projection.

## What Was Built

**`CreatorNameResolver`** (pure static helper, no I/O):
- `FromArtifactPath`: extracts the creator slug from `content-kb/<creator>/<id>.md`; returns "Unknown" for empty, rooted, traversal-containing, or too-short paths (T-62-02 containment guard)
- `FromChannelTitle`: trims the raw channel title; returns "Unknown" for null/whitespace

**Harvest.razor** (SUI-05, T-62-03):
- `_browsCreatorFilter` field; dropdown rendered when `browseCreators.Count > 1`
- Creator predicate folded into `GetVisibleChannelVideos()` — the canonical visible projection; Select-All, the harvest set, and the skip exclusion all route through it, so a row hidden by creator filter can never be harvested
- Filter resets on each new browse so a stale A-channel filter doesn't carry over to a new B-channel browse
- `OnBrowseCreatorFilterChanged` handler

**Review.razor** (SUI-05):
- `_reviewCreatorFilter` field; `CreatorFilteredRows` computed property (tab-filter × creator-filter)
- `RenderCreatorFilter()` RenderFragment — mirrors the `RenderBatchBar` pattern to work around Razor RZ1010 when a variable computation would otherwise need `@{ }` inside an `@if/else` HTML block
- `ToggleSelectAll` and `RenderBatchBar` both use `CreatorFilteredRows` so batch actions respect the filter
- Filter and selections reset on tab switch; selections cleared on filter change

**Tests**:
- `CreatorNameResolverTests`: 9 facts covering normal path, backslash normalization, null/empty, too-short, rooted, traversal, extra-nesting, channel-title normal, channel-title fallback
- `HarvestPageTests` extended: 6 new creator-filter facts — narrowing, all-creators restore, compose-with-unharvested-default, compose-with-skip-exclusion, select-A-filter-to-B-not-harvested (T-62-03 compose test), single-creator-no-dropdown

**README**: bullet added under Phase 62 Studio changes documenting creator filter behavior, source (ChannelTitle vs ArtifactPath), composition rules, and Publish exclusion.

## Deviations from Plan

None — plan executed exactly as written. One test bug caught and fixed during implementation: `CreatorFilter_ComposesWithSkipExclusion` initially had only Alice rows (1 distinct creator → no dropdown rendered → `#browseCreatorFilter` not found); fixed by adding a Bob row so the dropdown renders.

## Test Results

Studio build: 0 errors, pre-existing warnings only (SQLitePCLRaw vuln advisory + CS0414 unused field carried from prior plans).
Studio.Tests: 123/123 pass (6 new creator-filter tests + 9 new CreatorNameResolver tests = 15 new).
Pre-existing parallel-isolation flake (2 tests fail on some full-suite runs, pass when run alone) — known WSL issue, not introduced by this plan.

## Known Stubs

None — all filter paths are wired to real runtime data (ChannelTitle from YouTube lister; ArtifactPath from content_site_index store rows).

## Threat Flags

No new network endpoints, auth paths, file-access patterns, or schema changes. CreatorNameResolver reuses the same containment/traversal-rejection logic as `ReadArtifactSafe` and only performs in-memory string split — no filesystem access (T-62-02 mitigated).

## Self-Check: PASSED

- `/mnt/c/users/chrislunt/source/personal/deckflow-cycle10-run/DeckFlow.Studio/Services/CreatorNameResolver.cs` — FOUND
- `/mnt/c/users/chrislunt/source/personal/deckflow-cycle10-run/DeckFlow.Studio.Tests/CreatorNameResolverTests.cs` — FOUND
- Commits 5214c3ce, 828faf42, 7c53ccfa — all on cycle10
