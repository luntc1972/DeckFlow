---
phase: 108
plan: 02
title: JSON Mutation Endpoint Patch Responses
status: complete
completed: 2026-07-23
requirements_addressed: [CLUP-01, CLUP-03]
executor: codex (gpt-5.4 medium)
verifier: claude
---

# Plan 108-02 Summary — JSON Mutation Endpoint Patch Responses

## What was built
`CutLabApiController` decide / adjust / what-if-commit success responses now return a
server-authored `CutLabUiPatchDto` (built via `_patchBuilder.BuildAsync`) instead of
leaving the browser to re-derive visible state. Legacy top-level transition fields, where
retained, are populated directly from `Patch` and asserted to mirror it.

- `CutLabDecideApiResponse.Patch`, `CutLabAdjustApiResponse.Patch` (non-null),
  `CutLabWhatifApiResponse.Patch` (nullable — null for previews).
- Constructor now requires `ICutLabUiPatchBuilder` (ArgumentNullException guard); test
  factories in both suites updated.

## Key files
- modified: `DeckFlow.Web/Controllers/Api/CutLabApiController.cs`
- modified: `DeckFlow.Web/Models/Api/CutLabDecideApiResponse.cs`
- modified: `DeckFlow.Web/Models/Api/CutLabAdjustApiResponse.cs`
- modified: `DeckFlow.Web/Models/Api/CutLabWhatifApiResponse.cs`
- modified: `DeckFlow.Web.Tests/CutLabApiControllerTests.cs`
- modified: `DeckFlow.Web.Tests/CutLabWhatifTests.cs`

## Verification (Self-Check: PASSED)
- Build clean: Web + Tests, 0 warnings / 0 errors.
- `CutLabApiControllerTests|CutLabWhatifTests`: 34 passed / 0 failed.
- **Decide double-compute removed** (cross-AI fix): `PostDecideAsync` delegates after-state
  shaping to `BuildAsync` and no longer recomputes analysis-context/round-plan/`ComputeProposalDeltas`
  — the 4000-trial sim runs once (Render 512MB tier).
- **What-if commit async conversion** (cross-AI fix): now genuine `async Task<ActionResult<>>`,
  zero `Task.FromResult` remaining. All guards return BEFORE patch build — verified ordering:
  same-origin 403 (282), body/state BadRequest (287/292/300), no-change (307), **atomic
  overshoot BadRequest (323) precedes `_patchBuilder.BuildAsync` (328)**. T-108-04 preserved;
  overshoot test asserts patch builder not called.
- Endpoint protections (`FeatureFlagGate`, `RequestSizeLimit`, same-origin, attributes) unchanged.
- EOL: all 6 files LF (CR=0); no whitespace churn (132+ / 62− real content only).

## Deviations
- None functional. Codex hit one transient build-output file lock (overlapping builds);
  sequential rerun succeeded clean.

## Commits
- `8f60f4df` feat(cut-lab): return server patch from decide endpoint
- `8b5f0591` feat(cut-lab): return server patch from adjust endpoint
- `c2d53098` feat(cut-lab): return server patch from what-if commit
