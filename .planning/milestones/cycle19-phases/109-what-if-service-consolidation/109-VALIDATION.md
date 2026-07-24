---
phase: 109
slug: what-if-service-consolidation
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-23
review_status: claude_plan_checker_passed
---

# Phase 109 — Validation Strategy

> Per-phase validation contract for consolidating Cut Lab what-if validation + preview + commit
> behind one `ICutLabWhatifService` shared by the JSON API and no-JS transports.

## Test Infrastructure

| Property | Value |
|----------|-------|
| Framework | xUnit (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`); Vitest + `tsc` run via the MSBuild TypeScript target on build (no TS source changes expected this phase) |
| Config file | `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`; no `vitest.config`/`tsconfig` changes expected |
| Quick run command | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabWhatifTests` |
| Full suite command | `dotnet.exe build DeckFlow.sln && dotnet.exe test DeckFlow.sln` |
| Estimated runtime | ~120-240 seconds locally |

## Sampling Rate

- After every task commit: run the task's named `--filter` xUnit command; `dotnet.exe build DeckFlow.sln` clean (no new warnings).
- After every plan wave: run `dotnet.exe test DeckFlow.Web.Tests --filter "CutLabApiControllerTests|CutLabControllerTests|CutLabWhatifTests"`.
- Before `/gsd-verify-work`: full `dotnet.exe test DeckFlow.sln` green (Web.Tests + Core.Tests).
- Max feedback latency: no task goes more than one commit without an automated server-side check.
- **No Scryfall/browser gate:** local Windows dev server cannot reach Scryfall (TLS-fingerprint block); unit fakes isolate Scryfall via the existing test seam. Scryfall-dependent live smoke is deferred to prod UAT, consistent with Phase 108.

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------------|-----------|-------------------|-------------|--------|
| 109-01-01 | 01 | 1 | CLUP-04 | `ICutLabWhatifService` exposes `PreviewSwapAsync` + one non-throwing `TryValidateSwap(out error)` (single validation source) + `CommitSwapAsync` (calls `ThrowIfCancellationRequested()`, validates via `TryValidateSwap`, preserves input casing) returning non-throwing `CutLabWhatifCommitResult`; interface renamed from `ICutLabWhatifPreviewService` | unit/compile | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabWhatifTests` | ✅ (extend `CutLabWhatifTests.cs`) | verified |
| 109-01-02 | 01 | 1 | CLUP-04 | DI + all name-only references (`Program.cs:183`, both controllers, both fakes) updated; fakes gain `TryValidateSwap` (default true) + `CommitSwapAsync` stub; solution stays green with zero behavior change (controllers still use their old helpers/commit blocks) | unit/compile | `dotnet.exe test DeckFlow.Web.Tests --filter "CutLabApiControllerTests\|CutLabControllerTests\|CutLabWhatifTests"` | ✅ | verified |
| 109-01-03 | 01 | 1 | CLUP-05 | Shared service tests own preview-non-destructive + `TryValidateSwap` (valid/locked/commander/cut-pile) + input-casing preservation + every commit rule (valid, locked, commander, cut-pile-miss, overshoot-atomicity `7cb68348`, floor-state preserved, invalid-pair no-throw) | unit | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabWhatifTests` | ✅ | verified |
| 109-02-01 | 02 | 2 | CLUP-04, CLUP-05 | API transport (vertical slice): `PostWhatifAsync` preview pre-validates via `TryValidateSwap` (invalid → `BadRequest(NoChangeMessage)`); `PostWhatifCommitAsync` routes commit through `CommitSwapAsync` with the post-commit `_patchBuilder.BuildAsync` projection moved OUTSIDE the swap-validation catch so real `BuildAsync` failures PROPAGATE (T-109-04, not swallowed); `ValidateWhatifPair` deleted; API business-rule tests migrated to thin adapters + preview-invalid regression (locked/commander) + `PostWhatifCommitAsync_WhenPatchBuilderThrows_PropagatesAndDoesNotReturnGenericNoChange` (asserts exception via `Assert.ThrowsAsync`) | unit | `dotnet.exe test DeckFlow.Web.Tests --filter "CutLabApiControllerTests\|CutLabWhatifTests"` | ✅ | verified |
| 109-02-02 | 02 | 2 | CLUP-04, CLUP-05 | No-JS transport (vertical slice): shared preview pre-check routes through `TryValidateSwap` (position preserved before preview+keep branches → no-JS preview invalid-pair behavior unchanged, T-109-07); `Whatif` "keep" routes through `CommitSwapAsync`; `IsValidWhatifPair` deleted; all three catch clauses preserved (`InvalidOperationException` real-message surface, `OperationCanceledException` timeout copy, catch-all); full-page re-render via `_pageService.ProcessAsync` unchanged; no-JS business-rule tests migrated to thin adapters + preview-invalid regression (locked/commander) + `Whatif_Keep_WhenPageServiceThrowsInvalidOperation_SurfacesRealMessage` (T-109-05); FINAL grep gate `rg "ValidateWhatifPair\|IsValidWhatifPair" DeckFlow.Web/Controllers` → 0 | unit + grep | `dotnet.exe test DeckFlow.Web.Tests --filter "CutLabControllerTests\|CutLabWhatifTests"` | ✅ | verified |

## Wave 0 Requirements

Existing infrastructure covers all phase requirements — no new test project, framework, or dependency.

- Extend `DeckFlow.Web.Tests/CutLabWhatifTests.cs` with the thirteen service-level `[Fact]`s named in 109-01 task 3 (preview non-destructiveness; `TryValidateSwap` valid/locked/commander/cut-pile; `CommitSwapAsync` valid/casing/locked/commander/cut-pile-miss/overshoot/floor/invalid-no-throw) — reuse the file's existing fake analysis/simulation seams; no live Scryfall/HTTP.
- Update the two existing `FakeWhatifPreviewService` fakes (`CutLabControllerTests.cs`, `CutLabApiControllerTests.cs`) to implement the renamed interface + new `TryValidateSwap` (default true) + `CommitSwapAsync`, and (in 109-02) make both members configurable so adapter tests can drive valid/invalid and applied/not-applied.
- Add per-transport preview-invalid regression tests in 109-02 (API: `PostWhatifAsync_WhenValidationRejects{Locked,Commander}CardOut_ReturnsBadRequestNoChange`; no-JS: `Whatif_Preview_WhenValidationRejects{Locked,Commander}CardOut_RerendersNoChange`) BEFORE deleting the corresponding private helper (green-between-waves discipline).

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Full Cut Lab what-if preview + commit browser smoke on both transports | CLUP-04, CLUP-05 | Commit path resolves cards through the Scryfall-backed pipeline; local server cannot reach Scryfall | Deferred to prod UAT: run a Cut Lab sample through what-if preview then keep on the JS path and the no-JS `/cut-lab/whatif` form, confirm identical committed counts/export eligibility and identical rejection copy for a locked/commander swap. |

## Validation Sign-Off

- [x] All tasks have automated verify or Wave 0 dependencies.
- [x] Sampling continuity: no 3 consecutive tasks without automated verify.
- [x] Wave 0 covers all missing references.
- [x] No watch-mode flags.
- [x] Feedback latency target recorded.
- [x] `nyquist_compliant: true` set in frontmatter.

Approval: approved 2026-07-23 by GSD plan checker; revised 2026-07-23 (cross-AI convergence replan folding Codex REVISE findings — shared `TryValidateSwap` re-homes preview validation across all four call sites, API projection propagates outside the catch, cancellation token consumed, input-casing locked, 109-02 restructured into two atomic per-transport vertical slices to preserve green-between-tasks).
