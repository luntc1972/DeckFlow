---
status: resolved
trigger: "RunDistillAsync returns exit 1 instead of 0 when single video fails clip-validation (all-zero timestamps)"
created: 2026-06-12T13:02:00Z
updated: 2026-06-13T07:46:00Z
---

## Current Focus

hypothesis: per-video InvalidOperationException from ValidateClips (all-zero) is NOT caught at the per-video catch (~788); propagates to outer catch (~256) which returns 1
test: read RunDistillAsync overloads + DistillVideoAsync, trace exit code path
expecting: find where validation failure escalates to batch return 1
next_action: read ContentKbCommandRunners.cs around 256, 788, 884

## Symptoms

expected: exitCode 0, StatusUpdates==[(10,"failed")], Summaries/Clips/UpsertedRows empty
actual: exitCode 1 (fails at line 70)
errors: none surfaced; Assert.Equal(0, exitCode) fails actual=1
reproduction: dotnet test --filter RunDistillAsync_AllZeroClipTimestamps
started: unknown (Phase 37.5/37.6/38 region)

## Eliminated

## Evidence

## Resolution

root_cause: Test calls internal RunDistillAsync with dryRun:false and isSubscriptionProvider defaulted to false. The fail-closed guard at ContentKbCommandRunners.cs:410-416 fires (`if (!dryRun && !isSubscriptionProvider) return 1;`) BEFORE any video/validation runs. The ValidateClips all-zero rejection at 1100/1265 is never reached. Per-video catch at 1171 is correct and irrelevant. The test never passed — it was a RED test (7431705, 37.5-01) whose assumed contract (exit 0) was overridden by the metered-provider fail-closed contract added in 37.5-02 (f66ac58). The companion FakeLlmDistillationService also never overrides ClassifyAsync (default interface method throws NotSupportedException), so even with the guard bypassed it would fail at ClassifyAsync.
fix: VERDICT B - stale test. Fix the test, not production code.
verification: Sibling RunDistillAsyncTests.RunDistillAsync_MeteredProvider_FailsClosedWithoutClassifying (line 334) asserts the opposite/correct contract (exit !=0, no classify, empty status) for isSubscriptionProvider:false.

UPDATE 2026-06-13: VERDICT B fix was ALREADY APPLIED, shipped in 664c11e (v1.6). The current test file (CommandRunnerValidateClipsTests.cs) passes isSubscriptionProvider:true (line 69) so the fail-closed guard does NOT fire, and FakeLlmDistillationService.ClassifyAsync IS overridden to return "keep" (line 275) — both contradicting the stale root-cause text above, which analyzed the pre-fix RED test. Empirically verified 2026-06-13: `dotnet test --filter RunDistillAsync_AllZeroClipTimestamps` => Passed 1/0/0. The test now correctly asserts: all-zero clips => video marked "failed", nothing stored, batch exit 0. No further action; note closed.
files_changed: [DeckFlow.Core.Tests/CommandRunnerValidateClipsTests.cs (in 664c11e)]
