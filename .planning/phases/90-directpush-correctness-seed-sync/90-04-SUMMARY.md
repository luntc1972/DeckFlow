---
phase: 90-directpush-correctness-seed-sync
plan: 04
subsystem: content-kb
tags: [feature-flags, blazor-server, dapper, postgres, git, content-kb, tdd]

# Dependency graph
requires:
  - phase: 90-directpush-correctness-seed-sync
    provides: "90-01's sync.directpush-gitbody web-DB flag (registered + seeded FALSE) — this plan's Studio-side read target"
  - phase: 90-directpush-correctness-seed-sync
    provides: "90-03's awaiting_confirm_utc marker foundation (not consumed yet — reserved for 90-05/90-06)"
provides:
  - "IProdContentReader.ReadFlagAsync — a structurally read-only, fail-closed prod feature_flags accessor (throwing default interface method; real-implemented only on ProdContentReader)"
  - "DirectPushCoordinator re-exports + stages content-kb/seed/index-seed.json on every git-durability run, via the same shared IContentKbOrchestrator.ExportIndexToFileAsync factory Publish uses"
  - "DirectPush's durability commit drops [skip render] when sync.directpush-gitbody reads ON; flag OFF (shipped default) stays byte-identical"
  - "DurabilityCommitSubjectPattern now recognizes both the flag-OFF and flag-ON commit-subject shapes as this stage's own"
affects: [90-05, 90-06, 90-07]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Throwing default interface method (89-02/90-03 idiom) extended to a Studio-side prod accessor: IProdContentReader.ReadFlagAsync throws by default so FakeProdContentReader compiles unchanged; only ProdContentReader real-implements it"
    - "Coordinator-scoped test double distinct from the shared read-all fake: FakeDirectPushFlagReader (TestDoubles) implements only the flag-read seam DirectPushCoordinator needs, kept separate from FakeProdContentReader (the pull-from-prod read-all double)"

key-files:
  created:
    - DeckFlow.Studio.Tests/Services/ProdContentReaderTests.cs
    - DeckFlow.Studio.Tests/TestDoubles/FakeDirectPushFlagReader.cs
  modified:
    - DeckFlow.Studio/Services/IProdContentReader.cs
    - DeckFlow.Studio/Services/ProdContentReader.cs
    - DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs
    - DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs
    - DeckFlow.Studio.Tests/DirectPushPageTests.cs
    - README.md

key-decisions:
  - "ReadFlagAsync fails CLOSED inside its own try/catch (returns false on any non-cancellation exception, never propagates a connection/query failure to the caller) — the inverse of the web-side IFeatureFlagCache D-13 default-on, per D-04."
  - "DurabilityCommitSubjectPattern's trailing ' [skip render]' was made OPTIONAL (not just the message-building changed) — a genuine correctness fix, not a nice-to-have: without it, a flag-ON commit's own subject would be misclassified foreign on the next ahead-of-origin check and permanently block the push."
  - "Seed export runs on EVERY CommitAndPushBodiesAsync invocation (not gated on changedCount) so the committed seed always reflects the current approved set; the commit-gate (changedCount > 0) and message wording stay BODY-ONLY so 'N body|bodies' keeps meaning exactly that."
  - "The coordinator's flag-reader test double (FakeDirectPushFlagReader) was extracted to TestDoubles/ as its own file rather than nested in DirectPushCoordinatorTests, because DirectPushPageTests.cs (bUnit) also needed to register it in its DI service collection — this file was NOT in the plan's files_modified list but the constructor's new required parameter made it a genuine build/DI blocker (Rule 3)."

requirements-completed: [SYNC-08]

# Metrics
duration: ~25min
completed: 2026-07-07
---

# Phase 90 Plan 04: DirectPush Seed Re-export + Flag-Gated Redeploy Summary

**DirectPush now re-exports and stages `index-seed.json` through the same shared factory Publish uses on every durability commit, and drops `[skip render]` only when the new `sync.directpush-gitbody` web-DB flag reads ON via a fail-closed Studio-side accessor — flag OFF (shipped default) stays byte-identical to today.**

## Performance

- **Duration:** ~25 min
- **Tasks:** 2 (each TDD RED-conceptually-verified via build-then-test, no separate RED commit — both tasks landed as single `feat` commits after build+test verification)
- **Files modified:** 8 (4 production, 2 test, 1 new test file, 1 new test double, README)

## Accomplishments
- `IProdContentReader.ReadFlagAsync` exists as a throwing default interface method (mirrors the 89-02/90-03 idiom) — real-implemented ONLY on `ProdContentReader` as a single `SELECT enabled FROM feature_flags WHERE key = @key`, identical connection setup to `ReadAllAsync` (SslMode.Require, normalized string), no DDL/write, fail-closed (returns `false`) on a missing row, null `enabled`, or any caught connection/query failure.
- `FakeProdContentReader` (the existing pull-from-prod read-all test double) compiles and behaves unchanged — verified both at build time (no CS0535) and at runtime (a dedicated test proves the un-overridden `ReadFlagAsync` still throws `NotSupportedException`).
- `DirectPushCoordinator.CommitAndPushBodiesAsync` now calls `_orchestrator.ExportIndexToFileAsync(<repoRoot>/content-kb/seed/index-seed.json, ...)` on every run and stages the seed alongside the copied bodies; a seed-export failure throws immediately (before any body copy), never falling through to a silent bodies-only commit. The old "never stages the seed" anti-pattern doc comment is replaced with the new re-export contract.
- The durability commit message drops `[skip render]` only when `sync.directpush-gitbody` reads ON through the coordinator's new `IProdContentReader` dependency; flag OFF (shipped default, D-05) is provably byte-identical — the existing happy-path test's assertions were extended, not weakened.
- `DurabilityCommitSubjectPattern`'s trailing `" [skip render]"` is now optional, so a flag-ON commit (no trailing phrase) is still recognized as this stage's own durability commit on a later ahead-of-origin check — closing a correctness bug that dropping the phrase would otherwise introduce (a flag-ON push would permanently self-block as "foreign" on the very next run).

## Task Commits

Each task was committed atomically:

1. **Task 1: Add read-only ReadFlagAsync to the prod content reader (fail-closed)** - `e19e037c` (feat)
2. **Task 2: DirectPush re-exports the seed via the shared factory + drops [skip render] under the flag** - `af608679` (feat)

**Plan metadata:** commit pending (this SUMMARY + STATE/ROADMAP update)

## Files Created/Modified
- `DeckFlow.Studio/Services/IProdContentReader.cs` - Added `ReadFlagAsync` as a throwing default interface method.
- `DeckFlow.Studio/Services/ProdContentReader.cs` - Real-implemented `ReadFlagAsync`: single SELECT against `feature_flags`, fail-closed on any caught exception.
- `DeckFlow.Studio.Tests/Services/ProdContentReaderTests.cs` - New. Throwing-DIM proof via the unmodified `FakeProdContentReader`; a fail-closed connection-failure test that always runs; enabled-true/enabled-false/missing-key round trips gated behind `DECKFLOW_POSTGRES_TESTS=1` + a dedicated test-only connection-string env var (never the production `DECKFLOW_DATABASE_CONNECTION_STRING`).
- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` - Added `IProdContentReader _prodReader` (new required ctor param), `SeedRelative`/`DirectPushGitBodyFlagKey` consts, seed re-export + staging in `CommitAndPushBodiesAsync`, flag-gated `[skip render]` message building, widened `DurabilityCommitSubjectPattern`, and a `ReadDirectPushGitBodyFlagAsync` helper.
- `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs` - `Build()` now takes an optional `IProdContentReader` (defaults to `FakeDirectPushFlagReader` with the flag OFF); updated the happy-path and committed-count tests for the now-staged seed path; added flag-ON-drops-phrase, flag-OFF-then-ON-still-recognized-as-own, and seed-export-failure-surfaces tests.
- `DeckFlow.Studio.Tests/TestDoubles/FakeDirectPushFlagReader.cs` - New shared test double (`internal sealed class`) implementing only the flag-read seam `DirectPushCoordinator` needs; `ReadAllAsync` throws (unused by this coordinator).
- `DeckFlow.Studio.Tests/DirectPushPageTests.cs` - Registered `FakeDirectPushFlagReader` in the bUnit DI service collection (the coordinator's new required constructor parameter otherwise breaks every page render); updated Stage 4's commit-path assertion for the now-staged seed.
- `README.md` - New bullet documenting the seed re-export + flag-gated redeploy behavior, in the live-behavior Content-KB section (not the historical v1.7 release note, which is left as the dated record of what shipped then).

## Decisions Made
- **Fail-closed inside `ReadFlagAsync` itself**, not left to the caller: any caught connection/query failure (other than cancellation) returns `false` directly, matching D-04's "Studio must never assume ON if it cannot confirm" — there is no code path where a prod-read failure could be misread as "flag ON."
- **`DurabilityCommitSubjectPattern` regex change is load-bearing, not cosmetic.** Making the `RenderSkipPhrase` suffix optional was necessary the moment the message-building code could omit it — otherwise a flag-ON commit would immediately misclassify itself as foreign on the very next `CommitAndPushBodiesAsync` call (Rule 1 auto-fix: this is a bug directly caused by the flag-gated message change, not a separate feature).
- **Seed export is unconditional** (runs even when the eventual `changedCount` gate skips the commit) so the seed on disk is always freshly regenerated; the `changedCount`/message-wording gate stays scoped to bodies only, preserving "N body|bodies" semantics and the regex's `\d+` meaning.
- **`FakeDirectPushFlagReader` extracted to its own `TestDoubles/` file** (not nested in `DirectPushCoordinatorTests`) because `DirectPushPageTests.cs` (bUnit) also needed it to satisfy the coordinator's new required constructor parameter in its own DI service collection — a genuine cross-file reuse need discovered during Task 2, not scope creep.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `DirectPushPageTests.cs` broke on the new required constructor parameter**
- **Found during:** Task 2, after adding `IProdContentReader prodReader` to `DirectPushCoordinator`'s constructor and running the full `DeckFlow.Studio.Tests` suite (not just the filtered `DirectPushCoordinator`/`ProdContentReader` subset the plan's `<verify>` block specified).
- **Issue:** `DirectPushPageTests.cs` (bUnit) builds its own DI `Services` collection to render the real `DirectPush` page and had no registration for `IProdContentReader`; every test that reaches Stage 4 failed DI resolution. One Stage-4 test (`DirectPush_Stage4_AfterDbWrite_CommitsBodiesAndPushes_NoRedeploy`) also asserted the now-obsolete "never the seed" commit-path shape.
- **Fix:** Registered a `FakeDirectPushFlagReader` (flag OFF by default, matching D-05) in the bUnit service collection; updated the Stage-4 test's commit-path assertion to expect the seed staged alongside the body, `[skip render]` still present (flag OFF).
- **Files modified:** `DeckFlow.Studio.Tests/DirectPushPageTests.cs`, `DeckFlow.Studio.Tests/TestDoubles/FakeDirectPushFlagReader.cs` (new, shared with Task 2's coordinator tests).
- **Verification:** `dotnet test DeckFlow.Studio.Tests` — 324 passed, 3 skipped (the Postgres-gated `ProdContentReaderTests`), 0 failed.
- **Committed in:** `af608679` (Task 2 commit).

**2. [Rule 1 - Bug] `DurabilityCommitSubjectPattern` would misclassify a flag-ON commit as foreign**
- **Found during:** Task 2, while designing the flag-gated message change — recognized before writing any test, not discovered via a failing test.
- **Issue:** The existing regex required the literal `" [skip render]"` suffix to recognize a commit as "our own durability commit." Once the flag-ON path could omit that suffix, the very next `CommitAndPushBodiesAsync` run's ahead-of-origin check would classify its own prior commit as foreign and throw `DirectPushUnreviewedCommitsException`, permanently blocking the push under the flag.
- **Fix:** Made the trailing `" [skip render]"` group optional in the regex (`(?: \[skip render\])?$`) so both shapes are recognized as own.
- **Files modified:** `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs`.
- **Verification:** New test `CommitAndPushBodiesAsync_FlagOffThenOn_BothCommitSubjects_RecognizedAsOwnDurabilityCommit` asserts a flag-ON-shaped subject already ahead of origin triggers a catch-up push (`PushedExistingCommits`), not a foreign-commit refusal.
- **Committed in:** `af608679` (Task 2 commit, same as the feature it protects — not a separate fix commit since it was designed in from the start, not discovered after the fact).

---

**Total deviations:** 2 auto-fixed (1 Rule 3 blocking, 1 Rule 1 bug caught during design)
**Impact on plan:** Both were necessary for correctness/build-integrity; neither expanded scope beyond what SYNC-08/D-09 require. `DirectPushPageTests.cs` was not in the plan's `files_modified` list but the constructor change made touching it unavoidable.

## Issues Encountered
None beyond the two deviations above.

## User Setup Required

None - no external service configuration required. `sync.directpush-gitbody` remains seeded OFF (90-01); this plan's flag-gated behavior only activates after an operator flips the flag, which is out of this plan's scope (rollout precondition is the 90-01/D-11 git-coverage audit, not yet run).

## Next Phase Readiness
- SYNC-08's seed re-export is complete and always-on (not flag-gated) — every future DirectPush commit carries a fresh seed regardless of `sync.directpush-gitbody`.
- D-09's `[skip render]` gating is complete; Plan 90-05 (hash-gated ordering re-plumb) and Plan 90-06 (resume UI) can now assume a real Render redeploy happens under the flag, which SYNC-09's deploy-confirm poll (90-05/90-07) depends on.
- `IProdContentReader.ReadFlagAsync` is a stable, reusable seam — any future Studio code needing to read a web-DB flag can call it without adding a second accessor.
- No blockers. `DeckFlow.sln` builds with 0 warnings/0 errors; full suite green (Core 1149, Studio 324 + 3 Postgres-skip, Web 1235 + 12 Postgres-skip).

## Self-Check: PASSED

All 8 created/modified files verified present on disk (re-verified via `[ -f ... ]`
checks); both task commit hashes (`e19e037c`, `af608679`) verified present in
`git log --oneline --all`.

---
*Phase: 90-directpush-correctness-seed-sync*
*Completed: 2026-07-07*
