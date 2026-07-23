---
phase: 109
slug: what-if-service-consolidation
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-23
review_status: claude_plan_checker_passed
---

# Phase 109 — Validation Strategy

> Per-phase validation contract for consolidating Cut Lab what-if preview + commit
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
| 109-01-01 | 01 | 1 | CLUP-04 | `ICutLabWhatifService` exposes `PreviewSwapAsync` + `CommitSwapAsync` returning non-throwing `CutLabWhatifCommitResult`; interface renamed from `ICutLabWhatifPreviewService` | unit/compile | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabWhatifTests` | ✅ (extend `CutLabWhatifTests.cs`) | pending |
| 109-01-02 | 01 | 1 | CLUP-04 | DI + all name-only references (`Program.cs:183`, both controllers, both fakes) updated; solution stays green with zero behavior change (controllers still use their old commit blocks) | unit/compile | `dotnet.exe test DeckFlow.Web.Tests --filter "CutLabApiControllerTests\|CutLabControllerTests\|CutLabWhatifTests"` | ✅ | pending |
| 109-01-03 | 01 | 1 | CLUP-05 | Shared service tests own preview-non-destructive + every commit rule (valid, locked, commander, cut-pile-miss, overshoot-atomicity `7cb68348`, floor-state preserved, invalid-pair no-throw) | unit | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabWhatifTests` | ✅ | pending |
| 109-02-01 | 02 | 2 | CLUP-04, CLUP-05 | API `PostWhatifCommitAsync` routes commit through `CommitSwapAsync`; `ValidateWhatifPair` removed; try/catch narrowed to wrap ONLY the post-commit `_patchBuilder.BuildAsync` (Phase 108 patch DTO contract preserved) | unit | `dotnet.exe test DeckFlow.Web.Tests --filter "CutLabApiControllerTests\|CutLabWhatifTests"` | ✅ | pending |
| 109-02-02 | 02 | 2 | CLUP-04, CLUP-05 | No-JS `Whatif` "keep" routes through `CommitSwapAsync`; `IsValidWhatifPair` removed; all three catch clauses preserved (`InvalidOperationException` real-message surface, `OperationCanceledException` timeout copy, catch-all); full-page re-render via `_pageService.ProcessAsync` unchanged | unit | `dotnet.exe test DeckFlow.Web.Tests --filter "CutLabControllerTests\|CutLabWhatifTests"` | ✅ | pending |
| 109-02-03 | 02 | 2 | CLUP-05 | Duplicate controller business-rule tests removed; only thin delegation/HTTP-shape tests remain, plus pitfall negatives (T-109-04 masked `BuildAsync` failure, T-109-05 real-message surface) | unit | `dotnet.exe test DeckFlow.Web.Tests --filter "CutLabApiControllerTests\|CutLabControllerTests\|CutLabWhatifTests"` | ✅ | pending |

## Wave 0 Requirements

Existing infrastructure covers all phase requirements — no new test project, framework, or dependency.

- Extend `DeckFlow.Web.Tests/CutLabWhatifTests.cs` with the eight service-level `[Fact]`s named in 109-01 task 3 (reuse the file's existing fake analysis/simulation seams; no live Scryfall/HTTP).
- Update the two existing `FakeWhatifPreviewService` fakes (`CutLabControllerTests.cs`, `CutLabApiControllerTests.cs`) to implement the renamed interface + new `CommitSwapAsync`.

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

Approval: approved 2026-07-23 by GSD plan checker (behavior-preservation, error-handling narrowing, atomicity guard, and test-migration all confirmed against source).
