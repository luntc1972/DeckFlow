---
phase: 109
reviewers: [codex]
reviewed_at: 2026-07-23T22:43:13Z
reviewer_models: { codex: "gpt-5.5 medium" }
plans_reviewed: [109-01-PLAN.md, 109-02-PLAN.md]
verdict: REVISE (3 HIGH blockers)
---

# Cross-AI Plan Review — Phase 109

## Codex Review (gpt-5.5, medium, read-only)

**Summary**

The plans are strong in intent and mostly well sequenced: 109-01 creates the shared service and locks behavior with service tests before 109-02 moves controllers onto it. The main risks are in 109-02. Two details should block execution until corrected: deleting validation helpers without accounting for existing preview call sites, and contradictory guidance around the API projection `try/catch`.

**Strengths**

- Clear split between service ownership and transport projection: API keeps `ICutLabUiPatchBuilder.BuildAsync`; no-JS keeps `ICutLabPageService.ProcessAsync`.
- Good wave ordering: 109-01 should stay compile-green with no controller commit behavior change before 109-02 adoption.
- Atomicity is treated seriously: `CommitSwapAsync_OvershootReplacementCut_ReturnsNotAppliedWithNoHalfAppliedState` directly guards the prior half-applied-state bug.
- Good test migration goal: business rules move to `CutLabWhatifTests`; controllers retain adapter/HTTP-shape coverage.
- Explicit commander guard in shared commit validation is the right conservative choice.

**Concerns**

- **HIGH: 109-02 deletes `ValidateWhatifPair` but API preview still calls it.**  
  Research/source show `CutLabApiController.PostWhatifAsync` calls `ValidateWhatifPair` before preview. Task `109-02-01` deletes the helper while only modifying `PostWhatifCommitAsync`. That is a compile break unless the API preview path is updated or the helper is retained.

- **HIGH: 109-02 deletes/absorbs `IsValidWhatifPair` but the no-JS preview branch currently depends on its pre-validation.**  
  In `CutLabController.Whatif`, `IsValidWhatifPair` runs before the `intent == "preview"` branch. Removing it as part of the keep-branch refactor can change invalid-preview behavior from `RenderWhatifViewAsync(...)` to exception/catch handling via `CutLabView(...)`, which is observable no-JS behavior drift.

- **HIGH: API catch guidance is internally contradictory.**  
  Task `109-02-01` says the `try/catch (InvalidOperationException or ArgumentException)` should wrap only projection, but also says projection failures must not be swallowed into generic `NoChangeMessage`. If that catch still returns `BadRequest(NoChangeMessage)`, it still swallows `_patchBuilder.BuildAsync` failures. The plan needs an exact intended outcome: remove that catch around projection, rethrow, or return a distinct failure.

- **MEDIUM: 109-01 says preview behavior is unchanged, but 109-02’s helper deletion may force preview behavior changes.**  
  The plan should explicitly cover preview-path validation after helper removal. Right now “preview branch unchanged” and “delete `IsValidWhatifPair`/`ValidateWhatifPair`” are not simultaneously true.

- **MEDIUM: `CommitSwapAsync` cancellation token is unused.**  
  That is probably acceptable because the method is CPU-only and cheap, but the plan should either call `cancellationToken.ThrowIfCancellationRequested()` before work or state intentionally unused. Otherwise analyzers or reviewer expectations may flag it.

- **LOW: Result `CardOut`/`CardIn` casing may drift.**  
  Existing API success returns request strings. The preview service canonicalizes names from pool cards. `CommitSwapAsync` returns input strings per plan. That preserves API output, but tests should lock this if casing matters in JSON.

**Suggestions**

- Amend `109-02-01` to handle API preview explicitly before deleting `ValidateWhatifPair`: either keep a preview-only helper, move shared validation into `ICutLabWhatifService` with a non-throwing validation method, or let `PreviewSwapAsync` own validation and update preview error handling to preserve the same response shape.

- Amend `109-02-02` to preserve no-JS preview invalid-pair rendering. Do not remove the shared pre-check unless preview invalid cases are routed to the same `RenderWhatifViewAsync(request, state, null, NoChangeMessage)` behavior.

- Rewrite the API projection failure requirement. For example: “Do not catch `InvalidOperationException`/`ArgumentException` from `_patchBuilder.BuildAsync`; allow it to propagate to the global error handler,” if that is the desired behavior. Then make `PostWhatifCommitAsync_WhenPatchBuilderThrows_DoesNotReturnGenericNoChange` assert the exact expected result, not just “not generic 400.”

- Add grep gates for preview references in 109-02:
  `rg "ValidateWhatifPair|IsValidWhatifPair" DeckFlow.Web/Controllers`
  should return zero only after both preview and commit paths are accounted for.

- Add a no-JS preview invalid-pair regression test before the helper deletion, especially for locked/commander card-out, to prove the full-page re-render/error surface remains stable.

**Risk Assessment: HIGH**

The architecture is sound, but the execution plan has two likely compile/behavior blockers in 109-02 around helper deletion and preview paths, plus a contradictory API error-handling instruction. Fix those before dispatch. After those corrections, the residual risk drops to medium-low because the service extraction, atomicity tests, DI blast radius, and controller adapter tests are otherwise well scoped.

---

## Consensus Summary

Single reviewer (Codex, per project rule Codex is the authoritative plan reviewer). Verdict: **REVISE — 3 HIGH blockers, 2 MEDIUM, 1 LOW.** Architecture sound; execution-plan gaps around preview call sites and error-handling outcome must be fixed before execution.

### Agreed Strengths
- Clean service-vs-transport split (API keeps `BuildAsync` patch DTO; no-JS keeps `ProcessAsync` re-render).
- Wave ordering (109-01 green before 109-02 flips controllers).
- Atomicity guarded at service level (`CommitSwapAsync_OvershootReplacementCut_...`, prior fix 7cb68348).
- Business-rule test migration to `CutLabWhatifTests`.

### Agreed Concerns (blockers)
- **HIGH** — `ValidateWhatifPair` is also used by API **preview** (`PostWhatifAsync`); 109-02-01 deletes it while editing only commit → compile break.
- **HIGH** — `IsValidWhatifPair` pre-validates before the no-JS **preview** branch (`CutLabController.Whatif`); removing it as part of the keep-branch refactor drifts invalid-preview behavior.
- **HIGH** — API projection catch is contradictory: "wrap only projection" + "must not swallow into NoChange" — plan must state the exact outcome (let `BuildAsync` exceptions propagate to the global handler, and make T-109-04 assert that exact result).
- **MEDIUM** — preview-path validation after helper removal not covered; "preview unchanged" and "delete both validators" can't both hold as written.
- **MEDIUM** — `CommitSwapAsync` cancellation token unused (state intentional or add `ThrowIfCancellationRequested()`).
- **LOW** — `CardOut`/`CardIn` casing: commit returns input strings, preview canonicalizes from pool; lock with a test if JSON casing matters.

### Divergent Views
None (single reviewer).

### Resolution plan
Fold into a convergence replan: consolidate swap-pair validation into the shared `ICutLabWhatifService` (non-throwing) so BOTH preview and commit on BOTH transports route through it; update all four call sites (API preview+commit, no-JS preview+keep) when deleting the two helpers; add preview invalid-pair regression tests (locked/commander card-out) on both transports BEFORE deletion; specify the exact API projection-failure outcome (propagate, not swallow); resolve the cancellation-token note; add grep gate `rg "ValidateWhatifPair|IsValidWhatifPair" DeckFlow.Web/Controllers` → 0. Re-review with Codex until CONVERGED (no HIGH) before execution.
