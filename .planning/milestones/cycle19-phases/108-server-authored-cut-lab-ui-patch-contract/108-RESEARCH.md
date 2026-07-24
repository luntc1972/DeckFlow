---
phase: 108
slug: server-authored-cut-lab-ui-patch-contract
status: draft
created: 2026-07-23
review_status: claude_plan_checker_passed
---

# Phase 108 Research — Server-Authored Cut Lab UI Patch Contract

## Objective

Research how to plan Phase 108: make Cut Lab live UI mutations render server-authored state instead of re-deriving C# domain rules in TypeScript.

Requirement coverage:
- CLUP-01: JSON mutation endpoints return a server-authored UI patch containing serialized state, current count, cards remaining, export eligibility, proposal rows, structural finding rows, and what-if option data.
- CLUP-02: `cut-lab.ts` renders returned patch data instead of recomputing domain rules already owned by C#.
- CLUP-03: Quantity legality, accepted cuts, current counts, and export readiness display identically after JSON mutations and full no-JS server round trips.

## Current Architecture

### Server source-of-truth pieces

- `DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs`
  - Owns working-list derivation from immutable pool + latest accepted decisions + quantity adjustments.
  - Applies quantity adjustment folding and materialized added-basic handling.
- `DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs`
  - Owns decision append/restore behavior, commander-lock enforcement, and whole-entry overshoot guard.
- `DeckFlow.Web/Services/CutLab/CutLabAdjustmentApplier.cs`
  - Owns quantity delta validation, singleton rejection, legal multiple caps, added-basic materialization, and commander-lock rejection.
- `DeckFlow.Web/Services/CutLab/CutLabLegality.cs`
  - Owns legal max and multi-copy card recognition.
- `DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs`
  - Owns deterministic proposal queue, round labels, round banner body copy, and cards remaining to target.
- `DeckFlow.Web/Services/CutLab/CutLabPageService.cs`
  - Orchestrates initial/full-page Cut Lab state: resolved entries, analysis context, role floors, findings, round plan, current snapshot, and initial proposal deltas.
- `DeckFlow.Web/Models/CutLabViewModel.cs`
  - Converts `CutLabProcessResult` into Razor-renderable state: sticky bar, proposal, cuts made, findings, floors, goals, compare rows, what-if options, lock pool rows, package state.
- `DeckFlow.Web/Models/CutLab/CutLabFindingPresenter.cs`
  - Shared presenter for structural findings across Razor and JSON decide response.
- `DeckFlow.Web/Controllers/Api/CutLabApiController.cs`
  - JSON live endpoints:
    - `POST /api/cut-lab/decide`
    - `POST /api/cut-lab/adjust`
    - `POST /api/cut-lab/whatif`
    - `POST /api/cut-lab/whatif/commit`

### Existing API response shape

- `CutLabDecideApiResponse` already returns a rich server response:
  - `CutLabStateJson`
  - `NextProposal`
  - `ProposalDeltas`
  - `FloorWarnings`
  - `CardsRemaining`
  - `CutsMade`
  - `StructuralFindings`
  - `ComboDataAvailable`
  - `CategoryDataAvailable`
- `CutLabAdjustApiResponse` is minimal:
  - `CutLabStateJson`
  - `CardsRemaining`
- `CutLabWhatifApiResponse` is split:
  - preview: deltas, changed family count, card out/in, no state
  - commit: card out/in, `CutLabStateJson`, no refreshed proposal/finding/options patch

### Client-side re-derivation to remove or quarantine

`DeckFlow.Web/wwwroot/ts/cut-lab.ts` currently repeats domain decisions after JSON mutations:

- `currentCountFromSerializedState`
  - Re-derives accepted decisions, adjustment folding, added-basic counts, and current count from serialized state.
  - Diverges from `CutLabWorkingList.Derive` and `CutLabLegality.LegalMax` because it only clamps at zero and does not apply server legal caps.
- `cardsRemainingFromSerializedState`
  - Recomputes remaining-to-target from the client-derived count.
- `buildCutsMadeFromSerializedState`
  - Rebuilds accepted cuts and round labels from serialized decisions.
  - Only special-cases `whatif-swap`; it does not share `CutLabCutRoundEngine.LabelFor`.
- `rebuildWhatifSelectOptionsFromState`
  - Recomputes cut-pile and working-list what-if options from serialized state.
  - Does not apply full server working-list derivation for quantity-adjusted/multi-copy/added-basic cases.
- `patchStickyBar`
  - Uses server `cardsRemaining` but overrides export eligibility with client-derived current count when available.
- `handleAdjustSubmit`
  - Consumes minimal adjust response, then computes exact count and patches only the adjusted row.
- `handleWhatifKeep`
  - After commit, rebuilds cuts made, current count, export state, and what-if select options from serialized state.

Client-side helpers can remain for local scenario serialization, no-JS form hidden-state sync, and defensive fallback, but live JSON mutation rendering should prefer server patch fields.

## Planning Recommendation

Use a shared server projection service rather than adding one-off fields independently to each endpoint.

Recommended new seam:
- `DeckFlow.Web/Services/CutLab/CutLabUiPatchBuilder.cs`
- DTOs in `DeckFlow.Web/Models/Api/CutLabUiPatchDto.cs`

The builder should accept:
- authoritative `CutLabState`
- current play experience / commander names / role floor map
- optional pre-resolved card data where the API already has it
- request cancellation token

The builder should return one typed patch object containing:
- `CutLabStateJson`
- `CurrentCount`
- `CardsRemaining`
- `CanBuildExport`
- `NextProposal`
- `ProposalDeltas`
- `FloorWarnings`
- `CutsMade`
- `StructuralFindings`
- `ComboDataAvailable`
- `CategoryDataAvailable`
- `WhatifCardOutOptions`
- `WhatifCardInOptions`

The builder should use existing services:
- `CutLabWorkingList.Derive`
- `CutLabCutRoundEngine.BuildFindingsAndRoundPlan`
- `CutLabFindingPresenter.BuildFindings` / `BuildFindingGroups`
- `CutLabFloorRules.Evaluate`
- `ICutLabAnalysisContextBuilder`
- `ICutLabSimulationService`

The builder should not own:
- decision mutation rules
- quantity adjustment rules
- what-if commit atomicity
- no-JS controller flow
- DOM rendering

## Endpoint Strategy

### Decide endpoint

`PostDecideAsync` already computes before-context, floor warnings, after-context, findings, round plan, proposal deltas, and response rows. Phase 108 should move duplicated response-shaping into `CutLabUiPatchBuilder`.

Target:
- `CutLabDecideApiResponse` contains `Patch: CutLabUiPatchDto`.
- For transition safety, keeping existing top-level fields is acceptable only if tests prove they mirror `Patch` exactly; otherwise remove them and update TypeScript in the same phase.

### Adjust endpoint

`PostAdjustAsync` currently computes a working list and round plan but returns only state JSON + cards remaining. It should return `Patch: CutLabUiPatchDto` after applying the adjustment.

This removes the client’s need to compute:
- current count
- remaining-to-cut
- export enabled
- next proposal changes caused by quantity edits
- what-if option changes caused by adding/removing basics or legal multiples

### What-if commit endpoint

`PostWhatifCommitAsync` currently applies restore + accept atomically but returns only state JSON. It should return `Patch: CutLabUiPatchDto` for the committed state.

What-if preview remains non-mutating. It can continue returning deltas only in Phase 108; full what-if service consolidation belongs to Phase 109.

## No-JS Parity Strategy

No-JS routes in `CutLabController` re-render the full Razor page through `ICutLabPageService` and `CutLabViewModel.From`.

Phase 108 should prove parity by comparing patch fields against `CutLabViewModel.From` for the same state/result where practical:
- current count
- cards remaining
- export eligibility
- cuts made
- next proposal card/round/banner
- structural finding group headings/leads/evidence
- what-if card-out/card-in options

Do not rewrite no-JS controller behavior in Phase 108. The goal is same visible state, not a controller consolidation. Phase 109 owns what-if service consolidation.

## Testing Inventory

Server tests to extend:
- `DeckFlow.Web.Tests/CutLabApiControllerTests.cs`
  - Existing decide tests already cover rich response data.
  - Add adjust and what-if commit patch tests.
  - Add parity test for server patch fields against view-model projection for same state where possible.
- `DeckFlow.Web.Tests/CutLabPageServiceTests.cs`
  - Existing tests prove round plan, current snapshot, and proposal deltas are server-side.
  - Add or reuse fixtures for patch builder.
- New recommended test file:
  - `DeckFlow.Web.Tests/CutLabUiPatchBuilderTests.cs`

TypeScript tests to extend:
- `DeckFlow.Web/ts-tests/cut-lab-adjust.test.ts`
- `DeckFlow.Web/ts-tests/cut-lab-proposal.test.ts`
- `DeckFlow.Web/ts-tests/cut-lab-whatif.test.ts`

Focused browser specs to preserve:
- `DeckFlow.Web/e2e/cut-lab-smoke.spec.ts`
- `DeckFlow.Web/e2e/cut-lab-tuning.spec.ts`
- `DeckFlow.Web/e2e/cut-lab-whatif.spec.ts`
- `DeckFlow.Web/e2e/cut-lab-structure.spec.ts`

## Risks and Constraints

- Patch-builder overreach: avoid rebuilding the whole `CutLabViewModel` in API responses. Return only live-patch fields required by mutation rendering.
- Simulation cost: decide already computes proposal deltas; adjust and commit patch generation may add analysis/simulation work. Use existing `InLoopTrials`, cache seeding, and fail-open behavior.
- Contract churn: TypeScript and C# DTOs must change together; tests should fail if response shape drifts.
- Double source of truth: if old top-level decide fields stay during transition, add tests that assert they match `Patch`.
- No-JS safety: do not remove no-JS full render paths.
- Feature boundary: what-if preview/commit service consolidation is Phase 109, not Phase 108.
- Foreman constraint: do not edit Foreman ledger during this draft planning run. Foreman model-routing updates are explicitly deferred by user request.

## Validation Architecture

Automated validation should cover three layers:

1. Server patch builder/unit tests
   - Build patch from known state.
   - Assert counts, export eligibility, proposal, cuts made, structural findings, and what-if options.
   - Assert server legality and working-list behavior are used for quantity-adjusted states.

2. API contract tests
   - Decide, adjust, and what-if commit responses include `Patch`.
   - `Patch.CutLabStateJson` round-trips through `CutLabStateSerializer`.
   - Adjust and commit no longer require client-side count/cuts/options derivation.

3. TypeScript rendering tests
   - `handleAdjustSubmit` and `handleWhatifKeep` consume server patch fields.
   - Legacy serialized-state helpers are not called in live mutation success paths, except for hidden-input sync or explicitly documented fallback.
   - Export enablement follows `patch.canBuildExport`.

Validation commands:
- `dotnet.exe test DeckFlow.Web.Tests --filter CutLab`
- `npm test -- --run`
- `node_modules/.bin/tsc --noEmit`
- focused Playwright smoke after implementation if server/browser are available.

## Research Complete

Phase 108 is plannable as a three-slice implementation:
1. Introduce the shared server patch DTO/builder and test it directly.
2. Wire decide/adjust/what-if commit endpoints to return patch data.
3. Update TypeScript live mutation handlers to render server patch fields and remove live-path domain re-derivation.

## RESEARCH COMPLETE
