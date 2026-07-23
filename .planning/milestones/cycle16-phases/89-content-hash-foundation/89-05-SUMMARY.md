---
phase: 89-content-hash-foundation
plan: 05
subsystem: content-kb
tags: [content-hash, sha256, sync, render-guard, logging]

requires:
  - phase: 89-content-hash-foundation
    provides: "89-01: ContentSiteIndexRow.BodySha256 property + ComputeBodySha256 helper + body_sha256-inclusive BuildSignature"
  - phase: 89-content-hash-foundation
    provides: "89-02: body_sha256 column round-trips through all reads/writes/upserts"
provides:
  - "Publish-time body_sha256 compute on the ContentKbOrchestrator upsert-row literal, via the ONE shared ComputeBodySha256 helper"
  - "Detail-render body-hash guard (ContentKbController) that recomputes the on-disk hash and logs a structured warning on mismatch or null/legacy stored hash, fail-open"
  - "README ops note documenting the new operator-visible 'Content KB body hash mismatch' warning"
affects: [90-directpush-correctness-seed-sync, 91-reconcile-seed-lifecycle, 93-round-trip-integration-test]

tech-stack:
  added: []
  patterns:
    - "Both sides of the hash (publish-compute and render-guard) call the identical ComputeBodySha256(rawArtifactText) helper — no second hash path exists anywhere in the codebase (D-01)"
    - "Fail-open + structured-log data-integrity guard, mirroring P88's serve-side approval filter posture — never throw/404/blank the body on a detected mismatch"

key-files:
  created:
    - "DeckFlow.Core.Tests/Orchestration/ContentKbOrchestratorBodyHashTests.cs"
    - "DeckFlow.Web.Tests/TestDoubles/FakeLogger.cs"
  modified:
    - "DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs"
    - "DeckFlow.Web/Controllers/ContentKbController.cs"
    - "DeckFlow.Web.Tests/ContentKbControllerTests.cs"
    - "README.md"

key-decisions:
  - "D-01/D-02/D-05/D-06/D-07 honored exactly as specified in 89-CONTEXT.md — no deviations. The guard sits immediately after SplitHeader(raw), calls ComputeBodySha256(raw) (which internally re-derives the same split), and never alters control flow on mismatch."

patterns-established:
  - "FakeLogger<T> (DeckFlow.Web.Tests/TestDoubles) — a minimal ILogger<T> test double recording (LogLevel, formatted message) tuples, used wherever a test needs to assert a specific structured warning fired (or did not)."

requirements-completed: [SYNC-01, SYNC-03]

duration: ~50min
completed: 2026-07-07
---

# Phase 89 Plan 05: Content-Hash Foundation Summary

Publish-time `body_sha256` compute (`ContentKbOrchestrator`) and the detail-render body-hash guard (`ContentKbController`) both now call the ONE shared `ContentSiteIndexContentSignature.ComputeBodySha256` helper over the identical `SplitHeader` body, closing the loop opened by 89-01/89-02: a served page's body hash is now provably comparable to what was stored at publish time, and any drift (the CP437 mojibake class) surfaces as a structured `Content KB body hash mismatch` warning instead of being silently served.

## Performance

- **Duration:** ~50 min
- **Tasks:** 2 completed
- **Files modified:** 6 (4 modified, 2 created)

## Accomplishments

- `ContentKbOrchestrator`'s publish upsert now sets `BodySha256 = ContentSiteIndexContentSignature.ComputeBodySha256(artifactText)` on the `ContentSiteIndexRow` literal passed to `UpsertContentColumnsOnlyAsync` — reusing the already-written `artifactText`, no second hash path.
- `ContentKbController.Detail` recomputes `ComputeBodySha256(raw)` immediately after the existing `SplitHeader(raw)` call and compares it to `row.BodySha256`; on mismatch OR a null/legacy stored hash it logs `_logger.LogWarning("Content KB body hash mismatch for row {ContentKbRowId}: stored={StoredHash} computed={ComputedHash}", ...)` (named placeholders only) and continues serving the body unchanged — fail-open (D-05), detail-render only (D-07), no feature flag (D-06).
- README gained a one-line ops note under "Content Knowledge Base" explaining what the new warning means, when it fires, and that the entry still serves.
- Both test files exercise the RED→GREEN TDD cycle for real: the orchestrator test proved `BodySha256` was null before the compute was wired in (caught and fixed an unrelated fixture gap — the default fake clip fixture's all-zero timestamps fail `DistillationValidation.ValidateClips`, requiring a non-zero-timestamp override to reach the publish call at all); the controller tests proved the mismatch/null-hash warnings did not fire before the guard was added.

## Task Commits

Each task was committed as a RED→GREEN pair:

1. **Task 1: Compute body_sha256 at publish time**
   - RED: `a89f5bb1` `test(89-05): add failing test for publish-time body_sha256 compute`
   - GREEN: `2f85b097` `feat(89-05): compute body_sha256 at publish time via shared helper`
2. **Task 2: Add the detail-render body-hash guard + README ops note**
   - RED: `a9ba6880` `test(89-05): add failing tests for detail-render body-hash guard`
   - GREEN: `d3419f9b` `feat(89-05): add detail-render body-hash guard, fail-open + log`

## Files Created/Modified

- `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` - `BodySha256 = ComputeBodySha256(artifactText)` added to the publish upsert-row literal
- `DeckFlow.Core.Tests/Orchestration/ContentKbOrchestratorBodyHashTests.cs` - new: asserts the upserted row's `BodySha256` is a real 64-hex SHA-256 equal to `ComputeBodySha256` of the exact artifact text written to disk for that row
- `DeckFlow.Web/Controllers/ContentKbController.cs` - `Detail` action gains the hash-recompute-and-compare guard right after `SplitHeader(raw)`
- `DeckFlow.Web.Tests/ContentKbControllerTests.cs` - three new tests (matching/no-warning, mismatch/warning+served, null-hash/warning+served with `(none)` sentinel) plus a `BuildWithLogger` helper and a `bodySha256` parameter on the `Row` test-data factory
- `DeckFlow.Web.Tests/TestDoubles/FakeLogger.cs` - new: minimal `ILogger<T>` test double recording level+message tuples
- `README.md` - one-line ops note on the new "Content KB body hash mismatch" warning under the Content Knowledge Base section

## Decisions Made

- D-01/D-02/D-03 (inherited from 89-01) honored exactly — no second hash-computation code path was introduced anywhere; both publish and render call the same static helper.
- D-05 (fail-open + log on mismatch AND null-hash), D-06 (no feature flag), D-07 (detail-render scope only) all honored exactly as specified in 89-CONTEXT.md.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug/test-fixture gap] Default `FakeLlmDistillationService.ClipsResult` fixture fails `ValidateClips` for a full end-to-end distill test**
- **Found during:** Task 1 RED-phase test authoring (confirming the test failed for the *correct* reason before implementing)
- **Issue:** The orchestrator test needed a genuine end-to-end `DistillAsync` call reaching the publish upsert to assert `BodySha256` on the actual upserted row. The shared `FakeLlmDistillationService`'s default `ClipsResult` fixture (used by other, narrower orchestrator tests that never reach `ValidateClips`) sets every clip's timestamp to `0`, which `DistillationValidation.ValidateClips` explicitly rejects ("Clip extraction cannot return every clip with timestamp 0"), throwing before the upsert.
- **Fix:** The new test constructs its own `FakeLlmDistillationService` with an overridden `ClipsResult` carrying one non-zero timestamp, reaching the publish call without touching the shared fixture (scoped to this test only, no change to `FakeOrchestratorStores.cs`).
- **Files modified:** `DeckFlow.Core.Tests/Orchestration/ContentKbOrchestratorBodyHashTests.cs` (test-local override only)
- **Commit:** `a89f5bb1` (test, discovered during RED authoring, before the GREEN implementation commit)

---

**Total deviations:** 1 auto-fixed (test-fixture-scoped, no production code affected)
**Impact on plan:** None on scope — this was a test-authoring correction needed to genuinely prove RED before GREEN, not a change to the plan's deliverables.

## Issues Encountered

- Confirmed genuine TDD RED for both tasks by reverting the implementation via `git apply`/`git checkout` before writing tests, running them to failure for the expected reason, then reapplying the implementation and confirming GREEN — both task commits are real `test(...)` → `feat(...)` pairs, not same-pass authoring (see `## TDD Gate Compliance`).
- Diagnosing Task 1's initial test failure required adding a temporary `CapturingLogger<ContentKbOrchestrator>` to surface the swallowed exception from `DistillVideoAsync`'s catch-all (`ValidateClips` threw before reaching the upsert) — this diagnostic logger stayed in the final test file since it also usefully surfaces the assertion-failure message (`Assert.True(result.DistillFailed == 0, ...)`).

## TDD Gate Compliance

- Task 1: RED `a89f5bb1` (test asserted `BodySha256` non-null/64-hex/split-parity; failed because the upsert-row literal had no `BodySha256` field set — verified by reverting the implementation, running the test to a genuine assertion failure, then reapplying) → GREEN `2f85b097` (all criteria pass).
- Task 2: RED `a9ba6880` (mismatch and null-hash tests failed with "no matching warning entries"; the matching-hash test trivially passed since neither the old nor new code paths log a warning in that case) → GREEN `d3419f9b` (all 3 new tests + all 33 pre-existing `ContentKbControllerTests` pass, 36/36).
- No REFACTOR commit needed for either task — implementation matched the plan's insertion points exactly on the first GREEN pass.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `body_sha256` is now computed at publish time AND verified at render time, both through the one shared helper — the full loop described in 89-CONTEXT.md's domain boundary is closed for Phase 89's scope.
- Phase 90 (DirectPush correctness + seed sync) can rely on every newly-distilled row carrying a real `body_sha256` from this point forward; Phase 89-06 (not yet executed) still owes the one-time backfill for pre-existing rows so the render guard's null-hash branch eventually goes quiet on old content.
- No blockers. `DeckFlow.sln` builds clean (0 warnings, 0 errors) across all 6 projects. `DeckFlow.Core.Tests` 1132/1132, `DeckFlow.Web.Tests` 1226/1238 (12 PG-skip, 0 failed), `DeckFlow.Studio.Tests` 293/293 (confirmed a known pre-existing bUnit event-dispatch flake on `BlockedPageTests.BlockedPage_Unblock_RemovesRow` is unrelated to this plan — reran clean 293/293 on retry; this plan touches no Studio files). Format gate clean on all changed lines across both tasks.

## Known Stubs

None. Both the publish-time compute and the render-guard are fully wired and exercised by tests; no placeholder/mock data paths introduced.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or trust-boundary schema changes. This plan implements exactly the two mitigations the plan's own `<threat_model>` already registered (T-89-02 fail-open-with-log, T-89-11 log-injection-safe structured logging via named placeholders over `row.Id`/hash strings only, T-89-12 accepted negligible per-render SHA-256 cost) — no new surface beyond what was already disclosed.

## Self-Check: PASSED

- FOUND: `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs`
- FOUND: `DeckFlow.Core.Tests/Orchestration/ContentKbOrchestratorBodyHashTests.cs`
- FOUND: `DeckFlow.Web/Controllers/ContentKbController.cs`
- FOUND: `DeckFlow.Web.Tests/ContentKbControllerTests.cs`
- FOUND: `DeckFlow.Web.Tests/TestDoubles/FakeLogger.cs`
- FOUND: `README.md`
- FOUND: commit `a89f5bb1`
- FOUND: commit `2f85b097`
- FOUND: commit `a9ba6880`
- FOUND: commit `d3419f9b`

---
*Phase: 89-content-hash-foundation*
*Completed: 2026-07-07*
