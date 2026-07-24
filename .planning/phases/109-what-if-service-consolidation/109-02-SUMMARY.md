---
phase: 109
plan: 02
title: Both Transports Adopt TryValidateSwap + CommitSwapAsync and Migrate Duplicate Tests
status: complete
completed: 2026-07-23
requirements_addressed: [CLUP-04, CLUP-05]
executor: codex (gpt-5.4 medium)
verifier: claude
---

# Plan 109-02 Summary — Both Transports Adopt the Shared Service

## What was built
All four what-if pair-validation call sites now route through the single
`ICutLabWhatifService`. Both private validators and both duplicated restore-then-accept
blocks are gone. Behavior-preserving refactor plus one deliberate correctness fix.

**JSON API (`CutLabApiController`)**
- `PostWhatifAsync` (preview) pre-validates via `TryValidateSwap`; invalid pair still
  yields `BadRequest(NoChangeMessage)` — previously produced by a helper throwing into
  the catch, now produced explicitly.
- `PostWhatifCommitAsync` derives its swap outcome solely from `CommitSwapAsync`.
- **Catch narrowed (T-109-04 / review HIGH-3):** the `try` now covers only `Deserialize`
  + `Pool.Count == 0` + `CommitSwapAsync`. The post-commit projection
  (`_patchBuilder.BuildAsync` + commander/floor derivation + response build) sits OUTSIDE
  it, so a genuine `BuildAsync` failure propagates to the global handler instead of being
  masked as a generic "no change". This is a real behavior improvement, not just a move.
- `ValidateWhatifPair` deleted.

**No-JS (`CutLabController.Whatif`)**
- The shared pre-check routes through `TryValidateSwap` and stays in its exact prior
  position — before both the preview and keep branches — so no-JS preview invalid-pair
  behavior is byte-for-byte preserved.
- The keep branch derives its outcome from `CommitSwapAsync`; on rejection it re-renders
  with the ORIGINAL `state`; on success it rehydrates/serializes `result.State` and
  full-page re-renders via `_pageService.ProcessAsync`.
- All THREE catch clauses retained (`InvalidOperationException` real-message surface,
  `OperationCanceledException` timeout copy, catch-all) — still reachable via `Deserialize`
  and `ProcessAsync`, so they are NOT dead code (research Pitfall 2).
- `IsValidWhatifPair` deleted.

**Tests** — duplicated controller-level business-rule assertions (locked / commander /
overshoot) migrated to the shared service tests owned by 109-01. What remains at the
controller level: thin delegation/HTTP-shape tests, per-transport preview-invalid
regression tests, and the two pitfall negatives.

## Key files
- modified: `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` (−66 net shape)
- modified: `DeckFlow.Web/Controllers/CutLabController.cs` (−54 net shape)
- modified: `DeckFlow.Web.Tests/CutLabApiControllerTests.cs`
- modified: `DeckFlow.Web.Tests/CutLabControllerTests.cs`

## Commits
- `b13a0724` refactor(cut-lab): route JSON what-if through shared commit service
- `4b0164db` refactor(cut-lab): route no-JS what-if through shared commit service

## Verification (Self-Check: PASSED)
Verified independently by Claude:
- Scope fence: exactly the 4 permitted files.
- **Grep gate (T-109-07) PASSES:** `ValidateWhatifPair|IsValidWhatifPair` → 0 matches in
  `DeckFlow.Web/Controllers`.
- Exactly one `CommitSwapAsync(` call per controller. No `CutLabDecisionApplier.Apply` in
  either what-if path (remaining `DecisionApplier` references are the unrelated decide
  endpoints, verified by method-level read).
- Code read-checked: API try/catch scope and projection placement confirmed correct;
  no-JS pre-check position and all three catches confirmed intact.
- All 11 required test names present; both removed-test gates return 0.
- `PostWhatifCommitAsync_WhenPatchBuilderThrows_...` confirmed to assert propagation via
  `Assert.ThrowsAsync<InvalidOperationException>`, not merely "not a 400".
- EOL: all touched files LF with CR=0, matching baseline. `git diff --stat` 368+/228− vs
  `--ignore-all-space` 352+/212− — the 16-line gap is pure re-indentation from de-nesting
  the projection out of the `try`, which is inherent to the change, not gratuitous reflow.
- Build: 9 warnings = baseline, 0 new.
- xUnit full solution: Core.Tests 1612 passed / 0 failed; Web.Tests 1989 passed / 0 failed
  / 16 skipped (Postgres-gated).
- vitest 79/79, 19 files — unchanged (this phase touches no TypeScript).

## Deviations / issues resolved
- Codex reported its own full-solution run failing on
  `FeatureFlagStoreMigrationTests.EnsureSchemaAsync_RenamesLegacyKey_AndPreservesDisabledValue`
  with `ObjectDisposedException: 'SQLitePCL.sqlite3'`. **Investigated, not waved away.**
  That test class's `Dispose()` calls `SqliteConnection.ClearAllPools()`, which is
  process-global; xUnit runs distinct test classes in parallel, so it can dispose pooled
  handles out from under a concurrently-running test in another class. Pre-existing
  test-isolation defect, timing-dependent, wholly unrelated to Phase 109 (which touches no
  SQLite or feature-flag code). Claude's independent full run was green with 0 failures.
  Logged as a follow-up, not fixed here (out of scope / out of fence).
- Codex also ran the full-solution suite only after commit 1 rather than before it;
  both commits are green in the final state.

## Notes
Scryfall-dependent what-if preview/commit UAT remains deferred to prod (local server
cannot reach Scryfall). Suggested prod check: run a Cut Lab sample through what-if preview
then keep on BOTH the JS path and the no-JS `/cut-lab/whatif` form, confirming identical
committed counts, export eligibility, and identical rejection copy for a locked/commander
swap — that is the cross-transport parity this phase is supposed to guarantee.
