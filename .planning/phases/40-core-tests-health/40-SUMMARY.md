# Phase 40 — Core.Tests Health (ship gate) — SUMMARY

**Status:** Complete · 2026-06-12 · executed inline (gsd-debugger investigation + Codex fix + Claude review), no separate PLAN.md.

## Why this phase existed
During Phase 39 verification, VSTest ran reliably in WSL for the first time and revealed `DeckFlow.Core.Tests` was **not actually green** — prior "Core NNN passed" claims were `dotnet build` + grep counts, never real test runs. 9 failures surfaced (4 were Phase 39's own new loader tests, fixed in-phase; 5 were pre-existing and span Phases 26/37.5/37.6/38). This blocked the v1.6 "no failing tests" ship gate.

## Verdict: every failure was TEST-SIDE — zero production regressions
gsd-debugger investigated the scariest one; all three classes resolved test-only.

## Fixes (commit `e205e27`, test files only)
1. **T1 — `BlockedVideoStoreTests` null-case ×2** (`AddBlockAsync_BlankId`, `RunBlockVideoAsync_BlankId`): `Assert.ThrowsAsync<ArgumentException>` is exact-match; `ArgumentException.ThrowIfNullOrWhiteSpace(null)` throws the derived `ArgumentNullException` → null InlineData failed. Fix: `Assert.ThrowsAnyAsync<ArgumentException>`. (Origin 37.6 / 38-05 retarget.)
2. **T3 — `CommandRunnerValidateClipsTests.RunDistillAsync_AllZeroClipTimestamps_RejectsBeforeStoringRows`**: STALE TEST. It called `RunDistillAsync(dryRun:false)` but left `isSubscriptionProvider` default-`false`, so the Phase-37.5-02 metered-provider fail-closed guard (`ContentKbCommandRunners.cs:410`) returned exit 1 before reaching clip validation; its fake also didn't override `ClassifyAsync` (interface-default throws). Added `isSubscriptionProvider: true` + a "keep" `ClassifyAsync` override; the existing assertions (exit 0, status "failed", empty writes) were already correct. The authoritative contract is in `RunDistillAsyncTests` fail-closed test. (Test added RED in `7431705`, never went green.)
3. **T2 — flaky SQLite-integration store tests** (ContentSiteIndex / ContentVideoStore / LlmSpendLedger rotating): concurrent SQLite temp-db/connection-pool contention under parallel xUnit collections (sequential was always green). Fix: `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in new `DeckFlow.Core.Tests/AssemblyInfo.cs`. (Origin 26/37.5.)

## Verification
- `DeckFlow.Core.Tests`: **320 / 0 failed**, confirmed repeatable in BOTH parallel (default) AND sequential (`xUnit.parallelizeTestCollections=false`) modes (4 runs total across this + Phase 39 close).
- `DeckFlow.Web.Tests`: 602 passed / 0 failed / 5 PG-skip.
- `dotnet build DeckFlow.sln`: 0 errors / 0 warnings.
- Scope: 3 test files (`BlockedVideoStoreTests.cs`, `CommandRunnerValidateClipsTests.cs`, new `AssemblyInfo.cs`) — zero production code touched.

## Requirements
- TEST-01 (fix stable test bugs) ✅ — T1 + T3.
- TEST-02 (eliminate parallel-isolation flakiness) ✅ — T2.

## Outcome
v1.6 "no failing tests" ship gate MET. Both test suites green and deterministic. See memory `project_core_tests_not_green` (RESOLVED).
