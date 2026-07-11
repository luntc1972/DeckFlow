---
phase: 89-content-hash-foundation
plan: 03
subsystem: content-kb
tags: [content-hash, signature, sync, invariant-guard]

# Dependency graph
requires:
  - phase: 89-01
    provides: "body_sha256-inclusive ContentSiteIndexContentSignature.BuildSignature/AreContentEqual"
provides:
  - "ContentSyncDiffClassifier equal-timestamp branch now compares via ContentSiteIndexContentSignature.AreContentEqual (body-hash-aware)"
  - "OneSignatureSurfaceGuardTests — source-scan invariant locking the SYNC-02 one-signature invariant"
affects:
  - "DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs (Classify caller — inherits body-hash-aware diffing, no code change)"
  - "Phase 90 (DirectPush correctness) — both classifiers now share one signature surface"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Source-scan invariant guard test (mirrors CarveOutGuardTests' repo-root-walk idiom) to lock a 'one surface' architectural rule"

key-files:
  created:
    - "DeckFlow.Core.Tests/Content/OneSignatureSurfaceGuardTests.cs"
  modified:
    - "DeckFlow.Core/Content/ContentSyncDiffClassifier.cs"
    - "DeckFlow.Core.Tests/Content/ContentSyncDiffClassifierTests.cs"

key-decisions:
  - "D-03/D-04 honored exactly as specified in 89-CONTEXT.md — Fingerprint deleted, UTC-direction branches untouched"

patterns-established:
  - "Invariant guard tests scan source files at test-run time (not the compiled assembly) so intent-level rules like 'only one signature surface' fail loudly on reintroduction"

requirements-completed: [SYNC-02]

# Metrics
duration: ~15min
completed: 2026-07-07
---

# Phase 89 Plan 03: Content-Hash Foundation Summary

Deleted `ContentSyncDiffClassifier.Fingerprint` (the divergent title/artifact_path/tags subset scheme) and switched its equal-timestamp tie-breaker onto `ContentSiteIndexContentSignature.AreContentEqual` — the exact body-hash-inclusive comparator `DirectPushCoordinator.ClassifyDiff` already calls — collapsing SYNC-02's two divergent row-signature schemes into one, with a source-scan invariant test locking the "one signature, one home" rule against regression.

## Performance

- **Duration:** ~15 min
- **Tasks:** 2 completed
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments
- `ContentSyncDiffClassifier.Fingerprint` deleted; equal-timestamp tie-breaker now calls `ContentSiteIndexContentSignature.AreContentEqual`, making the classifier body-hash-aware for free (no change needed in `DirectPushCoordinator` or `PullFromProdCoordinator` — both already call into the paths that benefit).
- `indexed_utc` direction branches (`prodUtc > localUtc` → ProdNewer, `localUtc > prodUtc` → local-newer/Diverged) preserved byte-for-byte — the F-51-PG-01 timestamptz-direction guard is intact and re-verified by the existing `Classify_EqualTimestampSameInstantDifferentOffset_TreatedAsEqual` and `Classify_AllFourKinds_AreReachable` tests, both still green.
- New `OneSignatureSurfaceGuardTests` (source-scan, mirrors `CarveOutGuardTests`' repo-root-walk idiom) asserts no `Fingerprint`-style method exists anywhere under `DeckFlow.Core/Content` and `BuildSignature` is defined in exactly one file (`ContentSiteIndexContentSignature.cs`) — a future reintroduction of a second signature scheme fails this test immediately.

## Task Commits

Each task was committed atomically:

1. **Task 1 (RED): Add failing test for body-hash equal-timestamp tie-breaker** - `02c35051` (test)
2. **Task 1 (GREEN): Switch classifier tie-breaker to the unified content signature** - `ca85d75d` (feat)
3. **Task 2: Add one-signature-surface invariant guard** - `b63ad36a` (test)

_No REFACTOR commit — the GREEN implementation matched the plan's structural template on the first pass._

## TDD Gate Compliance

- **RED:** `02c35051` — added `Classify_EqualTimestampSameOtherColumnsDifferentBodyHash_IsDiverged` (plus a companion fully-equal test and a `bodySha256` parameter on the `Row()` test helper). Confirmed fail-fast for the correct reason: `Assert.Single()` got an empty collection because the old `Fingerprint` scheme never inspected `body_sha256`, so equal-timestamp rows differing only in body hash were (wrongly) treated as in-sync. Ran `dotnet test --filter ContentSyncDiffClassifierTests` → 1 failed / 17 passed, confirming the gap.
- **GREEN:** `ca85d75d` — deleted `Fingerprint`, replaced the equal-timestamp branch's comparator with `ContentSiteIndexContentSignature.AreContentEqual`. Ran the same filter → 18/18 passed. Full `DeckFlow.Core.Tests` suite → 1131/1131 passed (0 regressions).
- **REFACTOR:** not needed.

## Files Created/Modified
- `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` - `Fingerprint` method deleted; equal-timestamp branch calls `ContentSiteIndexContentSignature.AreContentEqual`; class/method xmldoc updated to reference the unified signature instead of "fingerprint" terminology; UTC-direction branches untouched.
- `DeckFlow.Core.Tests/Content/ContentSyncDiffClassifierTests.cs` - `Row()` helper gained a `bodySha256` parameter; added `Classify_EqualTimestampSameOtherColumnsDifferentBodyHash_IsDiverged` (RED target) and `Classify_EqualTimestampFullyEqualIncludingBodyHash_EmitsNothing` (companion no-diff lock).
- `DeckFlow.Core.Tests/Content/OneSignatureSurfaceGuardTests.cs` (new) - source-scan invariant guard asserting exactly one signature-building surface remains in `DeckFlow.Core/Content`.

## Decisions Made
- D-03/D-04 honored exactly as specified — no deviations. `Fingerprint` collapsed into the unified signature; direction logic preserved verbatim.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

**Non-printable delimiter character in the deleted `Fingerprint` method.** The old `string.Join('\x1f', ...)` call used a literal U+001F (unit separator) control character between the single quotes — visually indistinguishable from `''` in a terminal/editor. The first `Edit` attempt to delete the method failed twice because the tool's `old_string` (containing a literal `''`) never matched the actual byte content. Diagnosed via `cat -A` (showed `'^_'`, i.e. `^_` = 0x1F) and `xxd`, then removed the method with a targeted Python byte-level line deletion instead of a text-match `Edit`. No behavior impact — this was purely a tooling/diagnostic detour on a method being deleted anyway.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- SYNC-02 is now fully satisfied: `ContentSiteIndexContentSignature` is the sole signature surface, guarded by an automated regression test. `DirectPushCoordinator` and `PullFromProdCoordinator` both consume it (directly and via `ContentSyncDiffClassifier.Classify`, respectively) with zero code changes required in either coordinator.
- Phase 90 (DirectPush correctness + seed sync) can now assume the unified, body-hash-aware signature is used uniformly across DirectPush, Pull, and reconcile diffing.
- Remaining Phase 89 work (SYNC-01 schema/backfill plumbing, SYNC-03 render guard) is out of scope for this plan; see 89-01-SUMMARY.md and the phase's other plans.

---
*Phase: 89-content-hash-foundation*
*Completed: 2026-07-07*

## Self-Check: PASSED

- FOUND: `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs`
- FOUND: `DeckFlow.Core.Tests/Content/ContentSyncDiffClassifierTests.cs`
- FOUND: `DeckFlow.Core.Tests/Content/OneSignatureSurfaceGuardTests.cs`
- FOUND: commit `02c35051`
- FOUND: commit `ca85d75d`
- FOUND: commit `b63ad36a`
