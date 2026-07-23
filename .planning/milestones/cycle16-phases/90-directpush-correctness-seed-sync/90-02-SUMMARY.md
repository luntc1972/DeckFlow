---
phase: 90-directpush-correctness-seed-sync
plan: 02
subsystem: content-kb-sync
tags: [studio, path-safety, read-only-audit, prod-postgres, directpush]

# Dependency graph
requires:
  - phase: 89-content-hash-foundation
    provides: unified body_sha256 signature and shared prod-read pattern (IProdContentReader)
provides:
  - Shared internal ArtifactPathSafety helper (one Studio path-validation routine)
  - Read-only pre-flip git-coverage audit (IGitBodyCoverageAudit / GitBodyCoverageAudit)
affects: [91-reconcile-seed-lifecycle, sync.directpush-gitbody rollout]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Extract-not-rewrite refactor: move an existing private validation routine to a shared internal static helper when a second consumer needs identical behavior"
    - "Structurally read-only Studio service: constructor-inject only IProdContentReader (never IProdStoreFactory) so a service is compile-time incapable of writing to prod"

key-files:
  created:
    - DeckFlow.Studio/Services/ArtifactPathSafety.cs
    - DeckFlow.Studio/Services/IGitBodyCoverageAudit.cs
    - DeckFlow.Studio/Services/GitBodyCoverageAudit.cs
    - DeckFlow.Studio.Tests/Services/ArtifactPathSafetyTests.cs
    - DeckFlow.Studio.Tests/Services/GitBodyCoverageAuditTests.cs
  modified:
    - DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs

key-decisions:
  - "ArtifactPathSafety.TryBuildContainedPath root param = repoRoot (not repoRoot/content-kb): row.ArtifactPath already carries the 'content-kb/' prefix per its documented shape, and the helper's own IsSafeArtifactPath requires that literal prefix on the input — combining with repoRoot/content-kb would double the segment and reject every valid path. Matches PullFromProdCoordinator's existing proven call shape exactly."
  - "GitBodyCoverageAudit constructor takes only IProdContentReader (no IProdStoreFactory reference anywhere in the file) so the class is structurally incapable of writing to prod, per D-11/T-90-04."

requirements-completed: [SYNC-07]

# Metrics
duration: ~25min
completed: 2026-07-07
---

# Phase 90 Plan 02: Read-only Pre-flip Git-Coverage Audit Summary

**Extracted a shared `ArtifactPathSafety` Studio helper from `PullFromProdCoordinator` and built a read-only `GitBodyCoverageAudit` that reports approved+visible prod rows whose body is missing from the local git content-kb tree.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-07-07T22:35:00Z (approx)
- **Completed:** 2026-07-07T22:45:20Z
- **Tasks:** 2 completed
- **Files modified:** 6 (3 created source, 2 created test, 1 modified)

## Accomplishments
- One shared Studio path-safety guard (`ArtifactPathSafety.IsSafeArtifactPath` / `TryBuildContainedPath`), behavior-preserving extract from `PullFromProdCoordinator`'s former private copy — no per-class validator remains.
- A read-only `GitBodyCoverageAudit` service that reads prod ONLY via the existing `IProdContentReader` (single SELECT, no DDL), filters to approved+visible rows, and reports which of those rows' bodies are missing from the local git `/app` tree — the SYNC-07 flip precondition made explicit and observable.
- 15 new unit tests (9 path-safety + 6 audit) all green; existing `PullFromProd` bUnit suite (17 tests) unaffected.

## Task Commits

Each task was committed atomically:

1. **Task 1: Extract a shared Studio ArtifactPathSafety helper** - `775eeb5b` (refactor)
2. **Task 2: Read-only git-coverage audit service** - `f9b9cde6` (feat, includes the ArtifactPathSafetyTests namespace fix)

**Plan metadata:** (this commit)

## Files Created/Modified
- `DeckFlow.Studio/Services/ArtifactPathSafety.cs` - Shared `internal static` guard: `IsSafeArtifactPath` + `TryBuildContainedPath`, moved verbatim from `PullFromProdCoordinator`
- `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs` - Delegates to `ArtifactPathSafety` at both call sites; private copies removed
- `DeckFlow.Studio/Services/IGitBodyCoverageAudit.cs` - Audit contract + `GitBodyCoverageReport`/`GitBodyCoverageMissingRow` result types
- `DeckFlow.Studio/Services/GitBodyCoverageAudit.cs` - Implementation: `IProdContentReader.ReadAllAsync` → filter approved+visible → `ArtifactPathSafety.TryBuildContainedPath` + `File.Exists` → collect missing
- `DeckFlow.Studio.Tests/Services/ArtifactPathSafetyTests.cs` - 9 tests: valid path, rooted (Unix/Windows/UNC), traversal, wrong-prefix, empty/whitespace, case-insensitive containment
- `DeckFlow.Studio.Tests/Services/GitBodyCoverageAuditTests.cs` - 6 tests: only-approved-visible-missing reported, present excluded, hidden excluded, pending excluded, unsafe path flagged not resolved, exactly-one-read-call (structural read-only proof)

## Decisions Made
- **Path-containment root parameter:** used `repoRoot` directly (matching `PullFromProdCoordinator`'s established usage) rather than `Path.Combine(repoRoot, "content-kb")` as the plan's action prose literally suggested. `ContentSiteIndexRow.ArtifactPath` is documented and stored in `content-kb/{slug}/{id}.md` form (the prefix is already part of the value), and `ArtifactPathSafety.IsSafeArtifactPath` requires that literal `content-kb/` prefix on its input — combining with a `.../content-kb` root would produce a double-`content-kb` segment and make every valid row register as "unsafe," silently breaking the audit's correctness. This follows the plan's own read-first pointer to `PullFromProdCoordinator`'s exact call shape and keeps the ONE shared helper's contract consistent across both callers (Rule 1 auto-fix — behavioral correctness, not a scope change).
- No other deviations from the plan's task actions/acceptance criteria.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Corrected the audit's path-containment root parameter**
- **Found during:** Task 2 (git-coverage audit implementation)
- **Issue:** The plan's action prose described calling `ArtifactPathSafety.TryBuildContainedPath(Path.Combine(repoRoot, "content-kb"), artifactRelativePath, ...)`. Since `row.ArtifactPath` already includes the `content-kb/` prefix (per `ContentArtifactSpec.cs` doc comment) and `ArtifactPathSafety.IsSafeArtifactPath` requires that exact prefix on its input, combining with a `content-kb`-suffixed root would reject every artifact path as unsafe (double segment), making every approved+visible row report as missing regardless of actual presence.
- **Fix:** Call the shared helper with `repoRoot` (not `repoRoot/content-kb`) as the root and the row's `ArtifactPath` as-is — identical to `PullFromProdCoordinator`'s two existing call sites, which the plan's own `<read_first>` section pointed to as the pattern to reuse.
- **Files modified:** `DeckFlow.Studio/Services/GitBodyCoverageAudit.cs`
- **Verification:** `GitBodyCoverageAuditTests.RunAsync_PresentBody_ExcludedFromReport` proves a present body is correctly resolved and excluded; the whole 6-test suite is green.
- **Committed in:** `f9b9cde6` (Task 2 commit)

**2. [Rule 1 - Bug] Fixed test namespace to follow project convention**
- **Found during:** Task 2, before running the full test suite
- **Issue:** `ArtifactPathSafetyTests.cs` (Task 1) was initially placed in namespace `DeckFlow.Studio.Tests.Services` instead of the project-wide single test namespace `DeckFlow.Studio.Tests` (CLAUDE.md: "Tests live in a single namespace per project ... regardless of subfolder").
- **Fix:** Changed the namespace declaration to `DeckFlow.Studio.Tests`.
- **Files modified:** `DeckFlow.Studio.Tests/Services/ArtifactPathSafetyTests.cs`
- **Verification:** `dotnet build DeckFlow.Studio.Tests` clean; full `DeckFlow.Studio.Tests` suite (319 tests) green.
- **Committed in:** `f9b9cde6` (bundled with Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 Rule 1 bug fixes)
**Impact on plan:** Both fixes were necessary for correctness (audit would otherwise flag every row as missing) and convention compliance. No scope creep — no new files, no architectural change.

## Issues Encountered
None beyond the two auto-fixed deviations above.

## User Setup Required
None - no external service configuration required. The audit is invoked manually/programmatically by a future Studio UI hook or an operator script before the `sync.directpush-gitbody` flag flip; wiring that trigger point is out of this plan's scope (plan only delivers the service + tests).

## Next Phase Readiness
- `ArtifactPathSafety` is now the single Studio path-validation routine; any future Studio code needing content-kb path containment (e.g. Phase 91's reconciler) should reuse it rather than adding a third copy.
- `IGitBodyCoverageAudit` is ready to be wired into a Studio page/action or an operator CLI hook as the manual pre-flip verification step referenced in 90-CONTEXT.md D-11 and 90-01's rollout guidance; that wiring is not part of this plan (plan scope was the read-only service + its tests).
- No blockers for Plan 90-03 or later plans in this phase.

---
*Phase: 90-directpush-correctness-seed-sync*
*Completed: 2026-07-07*

## Self-Check: PASSED

All created files verified present on disk; all three task/summary commit hashes (`775eeb5b`, `f9b9cde6`, `1aa784af`) verified present in git log.
