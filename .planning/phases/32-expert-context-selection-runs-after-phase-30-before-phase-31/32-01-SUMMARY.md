---
phase: 32-expert-context-selection
plan: 01
subsystem: content-kb
tags: [evergreen, tier-fill, relevance, schema-migration]
requires: []
provides:
  - "is_evergreen column (SQLite + Postgres, self-healing migration, ordinal 13)"
  - "IContentSiteIndexStore.SetEvergreenAsync"
  - "ContentSiteIndexRow.IsEvergreen"
  - "ContentKbExcerpt.ClipOrigin"
  - "ExpertSelection (public sealed record)"
  - "IContentKbRelevanceService.GetMergedClipsAsync (4-tier fill merge)"
affects:
  - DeckFlow.Core/Content/ContentSiteIndexStore.cs
  - DeckFlow.Web/Services/ContentKbRelevanceService.cs
tech-stack:
  added: []
  patterns:
    - "tier-fill merge (pinned -> followed -> auto -> evergreen) with budget-trim waterfall"
    - "insert-only column preservation in preserving-upsert (mirrors is_visible)"
key-files:
  created:
    - DeckFlow.Web.Tests/ContentKbMergedClipsTests.cs
    - DeckFlow.Web.Tests/ContentSiteIndexStoreTests.cs
  modified:
    - DeckFlow.Core/Content/IContentSiteIndexStore.cs
    - DeckFlow.Core/Content/ContentSiteIndexStore.cs
    - DeckFlow.Core/Knowledge/ContentArtifactSpec.cs
    - DeckFlow.Web/Models/ContentKbExcerpt.cs
    - DeckFlow.Web/Services/ContentKbRelevanceService.cs
    - DeckFlow.Web.Tests/TestDoubles/FakeContentSiteIndexStore.cs
    - DeckFlow.Web.Tests/ContentKbRelevanceServiceTests.cs
    - DeckFlow.Web.Tests/ContentKbExcerptTests.cs
    - DeckFlow.Web.Tests/DeckAnalysisPacketServiceExpertContextTests.cs
    - DeckFlow.Web.Tests/AdminContentKbControllerTests.cs
key-decisions:
  - "ExpertSelection promoted to public sealed record (namespace-scoped) to resolve plan contradiction: a public interface method cannot expose an internal type (CS0051/CS0246)"
  - "GetMergedClipsAsync added to two more IContentKbRelevanceService test fakes the plan's implementer list omitted (compile-blocker from growing the public interface)"
  - "Auto path kept untouched per plan; CalculateScoreAndDimensions added as an ungated mirror of ScoreArtifact for tier-2 1-dimension gate relaxation"
requirements-completed: [SEL-02, SEL-05, SEL-06]
duration: ~25 min
completed: 2026-06-07
---

# Phase 32 Plan 01: Evergreen Flag + Tier-Fill Merge Core Summary

Added the artifact-level `is_evergreen` flag (self-healing column on SQLite + Postgres, `SetEvergreenAsync`, `ContentSiteIndexRow.IsEvergreen`) and the engineering core of the phase: `GetMergedClipsAsync`, a four-tier fill merge (pinned → followed → auto → evergreen) with a tier-aware budget-trim waterfall, pin cap of 3, and a `ClipOrigin` marker on every clip. The existing auto-only path (`GetRelevantClipsAsync`, `ScoreArtifact` gate) is byte-unchanged.

- **Tasks:** 3 (Task 1 schema/store, Task 2 model/merge, Task 3 tests)
- **Files:** 10 modified/created (8 planned + 2 authorized fake stubs)
- **Commits:** `3b1029d`, `79c4854`, `20febfd`
- **Executor:** Codex (gpt-5.4, medium) — Claude review

## Build / Test Results

- `dotnet build DeckFlow.Core -warnaserror:CS1591` — succeeded, 0 errors
- `dotnet build DeckFlow.Web` — succeeded, 0 errors
- `dotnet build DeckFlow.Web.Tests` — succeeded, 0 errors
- `dotnet test --filter "ContentKbMergedClipsTests|IsEvergreen|ContentKbExcerptTests"` — 10 passed / 0 failed
- `dotnet test --filter "ContentKbRelevanceServiceTests"` (auto-path regression) — 11 passed / 0 failed

## Deviations from Plan

**[Rule 4 - Architectural, authorized] ExpertSelection accessibility** — Found during: Task 2. Plan declared `ExpertSelection` as `internal sealed record` but put `GetMergedClipsAsync(ExpertSelection …)` on the public `IContentKbRelevanceService` interface (CS0051 + CS0246 + CS0535). Fix: promoted `ExpertSelection` to a `public sealed record` at namespace scope. Only valid resolution; Plan 02 consumes the method via the DI interface. Commit `79c4854`.

**[Rule 2 - Missing critical, authorized] Two more interface fakes** — Found during: Task 2. Growing the public `IContentKbRelevanceService` broke two test fakes the plan's implementer enumeration missed (`DeckAnalysisPacketServiceExpertContextTests.cs`, `AdminContentKbControllerTests.cs`, both CS0535). Fix: added minimal `GetMergedClipsAsync` stubs matching each fake's existing pattern. Commit `79c4854`.

**Total deviations:** 2 (both authorized by reviewer). **Impact:** none on behavior; both forced compile-fixes from a self-contradictory / incomplete plan implementer list.

## Reviewer Notes (Claude)

- All 9 Task-1 acceptance greps pass; `EXCLUDED.is_evergreen` present in `UpsertSql` only; preserving-upsert lists `is_evergreen` in INSERT cols but omits it from DO UPDATE SET (admin curation preserved on seed reload).
- ScoreArtifact gate (`dimensionsHit >= 2 ? score : 0d`) intact; auto-path interface signature unchanged.
- **Tech debt:** scoring sub-expressions now exist twice — `ScoreArtifact` (gated) and `CalculateScoreAndDimensions` (ungated mirror). Plan-mandated to protect the proven auto path, but the two must stay in sync if weights change. Acceptable; flagged for future consolidation.

## Issues Encountered

None unresolved.

## Next Phase Readiness

Ready for 32-02 (selection persistence + cache key). `GetMergedClipsAsync`, `ExpertSelection`, `ClipOrigin`, and `SetEvergreenAsync` contracts are in place for downstream consumption.

## Self-Check: PASSED
