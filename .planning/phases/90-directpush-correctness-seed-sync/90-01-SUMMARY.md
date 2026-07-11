---
phase: 90-directpush-correctness-seed-sync
plan: 01
subsystem: content-kb
tags: [feature-flags, content-kb, aspnet-core-mvc, sqlite, postgres, tdd]

# Dependency graph
requires:
  - phase: 89-content-hash-foundation
    provides: body_sha256 hash + fail-open render guard on ContentKbController.Detail (reused, not touched, this plan)
provides:
  - "sync.directpush-gitbody web-DB feature flag, registered in FeatureFlagCatalog, seeded FALSE in both Postgres and SQLite dialects"
  - "ContentKbArtifactPathResolver drops the /data-SFTP-first overlay fallback when the flag is ON (git-/app-only body resolution)"
  - "ContentKbController.Detail returns a real 404 for a missing /app body when the flag is ON, instead of the legacy 200 'artifact unavailable' shell"
affects: [90-02, 90-03, 90-04, 90-05, 90-06, 90-07, studio-directpush]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Inline IFeatureFlagCache.IsEnabled(key) gate consulted inside a resolution/serving path (not FeatureFlagGateAttribute, which is action-level all-or-nothing) - mirrors the ScryfallTaggerLookupService house pattern"
    - "Constructor-injected IFeatureFlagCache auto-wires through plain AddSingleton<T>() DI registration with no factory delegate - no Program.cs change needed for new constructor dependencies on already-simple-registered types"

key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
    - DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs
    - DeckFlow.Web/Controllers/ContentKbController.cs
    - DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs
    - DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs
    - DeckFlow.Web.Tests/ContentKbArtifactPathResolverTests.cs
    - DeckFlow.Web.Tests/ContentKbControllerTests.cs
    - DeckFlow.Web.Tests/ContentKbSeedLoaderTests.cs

key-decisions:
  - "Program.cs required NO change for either new IFeatureFlagCache constructor dependency: ContentKbArtifactPathResolver and ContentKbController are both registered via plain AddSingleton<T>()/implicit MVC controller DI with no factory lambda, so the container auto-resolves the new constructor parameter from the existing AddDeckFlowFeatureFlags() registration. Deviates from the plan's literal 'update Program.cs' instruction but satisfies the same intent (dependency correctly threaded)."
  - "Task 3 followed the plan-level TDD gate: a RED commit (test only, controller ctor scaffolded but MissingFile branch unchanged) confirmed the new flag-ON-missing-file test failed on the expected 200-vs-404 assertion (not a compile error), then a GREEN commit implemented the flag check."

requirements-completed: [SYNC-07]

# Metrics
duration: ~25min
completed: 2026-07-07
---

# Phase 90 Plan 01: DirectPush Correctness — sync.directpush-gitbody Serving Flip Summary

**Registered a web-DB feature flag (seeded OFF in both dialects) that, when flipped ON, makes Content-KB body serving git-`/app`-only — dropping the `/data`-SFTP-first overlay fallback and returning a real 404 for a body missing from git, instead of masking the gap or rendering a 200 shell.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-07-07
- **Tasks:** 3 (Task 3 is TDD: RED + GREEN sub-commits)
- **Files modified:** 9 (4 production, 5 test)

## Accomplishments
- `sync.directpush-gitbody` flag exists in `FeatureFlagCatalog.Descriptions` (build-time guard satisfied) and is seeded `FALSE` in both `PostgresSeedSql` and `SqliteSeedSql`, closing the D-13 default-on landmine before it can activate.
- `ContentKbArtifactPathResolver.TryResolveExistingArtifact` short-circuits to `MissingFile` after a git-tree miss when the flag is ON, never consulting `DataOverlayBase` — reusing `IsSafeArtifactPath`/`IsContainedUnderRoot` verbatim, no second path-validation routine.
- `ContentKbController.Detail`'s `MissingFile` branch returns `NotFound()` under the flag (after the existing structured warning log), and keeps the legacy 200 "artifact unavailable" shell when the flag is OFF — byte-identical to today.
- Flag OFF (the shipped default) is provably byte-identical: every pre-existing resolver/controller/seed-loader test that didn't explicitly opt into `directPushGitBodyOn: true` still passes unchanged.

## Task Commits

Each task was committed atomically:

1. **Task 1: Register sync.directpush-gitbody (catalog + seed FALSE both dialects)** - `f48da298` (feat)
2. **Task 2: Flag-gate ContentKbArtifactPathResolver to git-only when flag ON** - `9a5d5efb` (feat)
3. **Task 3: Return 404 (not the 200 shell) for a missing /app body under the flag** - RED `51fadf5f` (test), GREEN `ee2a0e18` (feat)

**Plan metadata:** commit pending (this SUMMARY + STATE/ROADMAP update)

## Files Created/Modified
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` - Added the `sync.directpush-gitbody` operator description.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` - Seeded `sync.directpush-gitbody` FALSE in both `PostgresSeedSql` and `SqliteSeedSql`.
- `DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs` - Injected `IFeatureFlagCache`; git-miss + flag-ON now short-circuits to `MissingFile` before the `DataOverlayBase` fallback block.
- `DeckFlow.Web/Controllers/ContentKbController.cs` - Injected `IFeatureFlagCache`; `Detail`'s `MissingFile` branch returns `NotFound()` when the flag is ON, keeps the 200 shell when OFF.
- `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` - Added `sync.directpush-gitbody` to the seeded-flag description guard.
- `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` - Added SQLite seed-value assertion + a Postgres-literal landmine guard (mirrors the `analysis.mulligan-eval` precedent).
- `DeckFlow.Web.Tests/ContentKbArtifactPathResolverTests.cs` - Added flag-ON git-hit, flag-ON overlay-ignored, and flag-ON invalid-path tests; `Build()` now takes an optional `directPushGitBodyOn` param.
- `DeckFlow.Web.Tests/ContentKbControllerTests.cs` - Added flag-ON missing-file→404 and flag-ON present-artifact→200 tests; `Build()`/`BuildWithLogger()` share one `FakeFeatureFlagCache` between resolver and controller per test.
- `DeckFlow.Web.Tests/ContentKbSeedLoaderTests.cs` - Updated the resolver construction call site for the new constructor parameter (flag explicitly OFF).

## Decisions Made
- **No Program.cs change needed for either DI thread.** Both `ContentKbArtifactPathResolver` (`AddSingleton<ContentKbArtifactPathResolver>()`) and `ContentKbController` (implicit MVC controller activation) are registered without a factory delegate, so the container resolves the new `IFeatureFlagCache` constructor parameter automatically from the existing `AddDeckFlowFeatureFlags()` registration. The plan's `files_modified` frontmatter listed `Program.cs`; it was read and confirmed unnecessary to touch — build succeeded end-to-end without any edit there.
- **Shared flag-cache instance in tests.** Rather than two independent `FakeFeatureFlagCache` instances (one for the resolver, one for the controller) that could silently drift, the test `Build()` helpers construct one instance and thread it through both, so a single `directPushGitBodyOn` parameter drives both layers consistently per test case.

## Deviations from Plan

None functionally — all three tasks' `must_haves` artifacts and truths are satisfied exactly as specified. One documentation-level deviation:

**1. [Rule 3 - Blocking, resolved as no-op] Program.cs did not need editing**
- **Found during:** Task 2 (resolver flag injection) and Task 3 (controller flag injection)
- **Issue:** The plan's `files_modified` list and Task 2's action text called for updating `Program.cs`'s DI registration to supply `IFeatureFlagCache`.
- **Fix:** Verified via full-solution build that both types resolve the new constructor parameter automatically (plain `AddSingleton<T>()` / implicit MVC controller DI, no factory lambda) — no source change required.
- **Files modified:** None (verification only).
- **Verification:** `dotnet build DeckFlow.sln` clean; `dotnet test DeckFlow.Web.Tests` green including all new flag-ON tests, proving the flag cache is actually reaching both classes at runtime.
- **Committed in:** N/A (no change to commit).

---

**Total deviations:** 1 (Rule 3, resolved as a no-op — plan's stated file-touch target was unnecessary given existing DI shape)
**Impact on plan:** None. The `must_haves.artifacts` and `key_links` (grep-checkable) are all satisfied; the DI wiring works identically to what an explicit Program.cs edit would have produced.

## Issues Encountered
- One pre-existing test, `HarvestRunStoreTests.EnsureSchemaAsync_MigratesOldSqliteCheckConstraint_Idempotently`, failed once during a full-suite parallel run with a native SQLite interop stack trace (`SafeHandle.DangerousAddRef`), then passed both in isolation and on a full-suite rerun. This matches the CLAUDE.md-documented "VSTest unreliable in WSL" constraint and is unrelated to this plan's changes (not a Content-KB or feature-flag test, no shared state touched). Not fixed — out of scope per the deviation-rules scope boundary (pre-existing, unrelated file).

## User Setup Required

None - no external service configuration required. `sync.directpush-gitbody` ships OFF; no operator action needed until a later plan's pre-flip git-coverage audit (D-11) clears the flip.

## Next Phase Readiness
- SYNC-07's serving flip is complete and flag-gated; flag-OFF behavior is provably byte-identical (existing test suite green with no assertion changes for flag-unset paths).
- Plans 90-02 through 90-07 (SYNC-08/09/10, the DirectPush ordering/stamp/seed-export rework) can now build on a resolver/controller that already understands `sync.directpush-gitbody` — Studio's read-only flag accessor (D-04) has a stable web-DB flag key to read.
- No blockers. The D-11 pre-flip git-coverage audit and the actual flag flip remain future/operator-triggered work, out of this plan's scope.

## Self-Check: PASSED

All 9 created/modified files confirmed present on disk; all 5 commit hashes
(`f48da298`, `9a5d5efb`, `51fadf5f`, `ee2a0e18`, `1ccce1c7`) confirmed present
in `git log --oneline --all`.

---
*Phase: 90-directpush-correctness-seed-sync*
*Completed: 2026-07-07*
