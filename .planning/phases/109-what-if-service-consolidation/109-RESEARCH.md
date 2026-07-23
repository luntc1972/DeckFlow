# Phase 109: What-If Service Consolidation - Research

**Researched:** 2026-07-23
**Domain:** ASP.NET Core service consolidation / controller de-duplication (C#, internal refactor, no new external dependency)
**Confidence:** HIGH — every claim below is grounded in code read on branch `gsd/cycle19-cut-lab-upgrade`, head `3d5f6341` (Phase 108 complete).

## Summary

Cut Lab's what-if **preview** path is already consolidated behind `ICutLabWhatifPreviewService.ComputeSwapPreviewAsync` (`DeckFlow.Web/Services/CutLab/CutLabWhatifPreviewService.cs`), injected into both `CutLabApiController` and `CutLabController`. The **commit** path (restore-B-then-accept-A swap) is duplicated near-verbatim in both controllers, plus a third, partially-overlapping validation copy already lives *inside* `CutLabWhatifPreviewService.ComputeSwapPreviewAsync` itself (lines 71-83). Three copies of "is this swap pair legal" logic exist today with two different call contracts (throw vs. bool-return).

The underlying rule engine is **already shared** — both commit paths call the same `CutLabDecisionApplier.Apply` (commander-lock no-op, 100-card overshoot guard) and the same `CutLabWorkingList.Derive`/`CutLabLockRules.EnforceCommanderLock`. CLUP-05 is therefore not "these rules diverge today" so much as "these rules are duplicated in two call sites with zero shared tests, and one real, if currently latent, contract divergence exists in what each path returns and how it re-renders after commit." The no-JS commit branch does **not** call `ICutLabUiPatchBuilder` at all — it falls through to `ICutLabPageService.ProcessAsync` (the full intake/render pipeline), while the JSON path calls `ICutLabUiPatchBuilder.BuildAsync` (the Phase 108 patch-DTO contract). This is the single biggest architectural asymmetry the new service must paper over without changing either transport's observable output.

**Primary recommendation:** Extend the existing `ICutLabWhatifPreviewService` in place — rename to `ICutLabWhatifService` with two methods, `PreviewSwapAsync` (existing logic, renamed for symmetry) and `CommitSwapAsync` (new, extracted from both controllers' duplicated commit blocks). `CommitSwapAsync` returns a lightweight `CutLabWhatifCommitResult` (`Applied: bool`, `State: CutLabState`, `ErrorMessage: string?`) that both controllers adapt locally — the API controller still calls `ICutLabUiPatchBuilder.BuildAsync` on the returned state to build `CutLabWhatifApiResponse.Patch` (preserving the Phase 108 patch-DTO contract byte-for-byte), and the no-JS controller still calls `ICutLabPageService.ProcessAsync` on the returned state (preserving its current full-page re-render). Do **not** try to make the service also own patch-building or page-rendering — those are transport-specific projections, not commit logic, and folding them in would break the Phase 108 contract boundary this phase is not scoped to touch.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| What-if pair validation (locked/commander/cut-pile membership) | API / Backend (`ICutLabWhatifService`) | — | Pure state-derivation rule, no I/O; must be identical for both transports |
| What-if swap commit (restore B, accept A, overshoot-guard atomicity) | API / Backend (`ICutLabWhatifService`) | — | Same as above — currently duplicated in `CutLabDecisionApplier.Apply` call sites |
| Commander-lock / quantity-legality enforcement | API / Backend (`CutLabDecisionApplier`, `CutLabLockRules`) | `ICutLabWhatifService` (consumer) | Already centralized in `CutLabDecisionApplier.Apply`; the new service is a thin orchestrator over it, not a new rule owner |
| JSON patch projection (Phase 108 contract) | API / Backend (`CutLabApiController` + `ICutLabUiPatchBuilder`) | — | Out of scope for this phase; consumed, not modified |
| No-JS full-page re-render | Frontend Server (SSR) (`CutLabController` + `ICutLabPageService`) | — | Out of scope for this phase; consumed, not modified |
| HTTP transport concerns (same-origin check, `[ValidateAntiForgeryToken]`, status codes) | API / Backend + Frontend Server | — | Stays in each controller; not part of the shared service |

## Standard Stack

No new packages. This phase is a pure C# refactor inside the existing ASP.NET Core 10 MVC codebase (`DeckFlow.Web`). No installation section, no Package Legitimacy Audit — no external dependency is added or upgraded.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Extend `ICutLabWhatifPreviewService` → `ICutLabWhatifService` (recommended) | Create a brand-new `ICutLabWhatifCommitService` alongside the existing preview interface | Keeps preview/commit split, but CLUP-04 explicitly asks for "one `ICutLabWhatifService` path" — a second interface re-fragments what the requirement asks to unify. Rejected. |
| Extend in place (recommended) | Fold preview AND commit AND patch-building into one god-service | Would violate the Phase 108 patch-DTO boundary (`ICutLabUiPatchBuilder` is reused by `decide`/`adjust`/`whatif` endpoints) and the no-JS full-page-render boundary (`ICutLabPageService`). Over-scopes CLUP-04. Rejected. |

## Architecture Patterns

### Current Commit Sequence — exact per-file breakdown

**API path** — `CutLabApiController.PostWhatifCommitAsync` (`DeckFlow.Web/Controllers/Api/CutLabApiController.cs:268-338`):
1. `SameOriginRequestValidator.IsValid(Request)` → 403 (line 270-273)
2. Null request / required-field check → 400 `"Cut Lab state, card out, and card in are required."` (line 275-283)
3. `CutLabStateSerializer.Deserialize(request.CutLabStateJson)` (line 287) — this call itself runs `CutLabGoalRules.ClampGoals(CutLabFloorRules.ClampFloors(CutLabLockRules.EnforceCommanderLock(state)))` (`CutLabStateSerializer.cs:80`), so commander-lock is already re-asserted on every deserialize.
4. `state.Pool.Count == 0` → 400 `InvalidStateMessage` = `"Cut Lab state is invalid. Re-import the pool and try again."` (line 288-291) — **API-only check, no-JS has no equivalent.**
5. `ValidateWhatifPair(state, request.CardOut, request.CardIn)` (line 293) — **throws** `InvalidOperationException(CutLabMessages.NoChangeMessage)` on failure (private method, lines 512-526): checks `CutLabWorkingList.Derive(...)`-derived `cardOutPoolCard` exists and `!IsLocked` (no explicit `IsCommander` check — relies on the deserialize-time lock invariant), then checks `CutLabWorkingList.AcceptedCardNames(state.Decisions).Contains(cardIn)`.
6. Second, **raw-pool** re-check: `state.Pool.FirstOrDefault(... cardOut ...)` → 400 if `null || IsLocked` (line 294-298). **No explicit `IsCommander` check here.**
7. `afterRestore = CutLabDecisionApplier.Apply(state, request.CardIn, Restore, CutLabCutRoundEngine.WhatifSwapKey)` (line 300-304)
8. `afterSwap = CutLabDecisionApplier.Apply(afterRestore, request.CardOut, Accept, CutLabCutRoundEngine.WhatifSwapKey)` (line 305-309)
9. Overshoot/atomicity guard: `afterSwap.Decisions.Count == afterRestore.Decisions.Count` → 400 `NoChangeMessage` (line 310-314, comment: *"the overshoot guard can refuse the replacement cut, so a half-applied swap must be rejected"*)
10. `commanderNames = GetCommanderNames(afterSwap)`, `floorByRole = BuildFloorMap(afterSwap.RoleFloors)` (line 316-317)
11. `patch = await _patchBuilder.BuildAsync(afterSwap, afterSwap.Intent.PlayExperience, commanderNames, floorByRole, cancellationToken: cancellationToken)` (line 318-323) — **full** `BuildAsync` (re-runs analysis context + simulation + round-plan), not the light `BuildAdjustPatch`. `floorWarnings` param omitted → `BuildAsync` computes floor warnings for the **next proposal**, not the just-swapped card (`CutLabUiPatchBuilder.cs:116`).
12. Returns `200 OK` with `CutLabWhatifApiResponse { CardOut, CardIn, Patch, CutLabStateJson = patch.CutLabStateJson }` (line 325-331). `Deltas`/`ChangedFamilyCount` left at record defaults (`[]`/`0`) since those fields are preview-only.
13. `catch (InvalidOperationException or ArgumentException)` → `_logger.LogWarning(...)`, 400 `NoChangeMessage` (line 333-337). **Original exception message is discarded** — always the generic copy.
14. Uses the ASP.NET-bound `CancellationToken cancellationToken` parameter throughout.

**No-JS path** — `CutLabController.Whatif` "keep" branch (`DeckFlow.Web/Controllers/CutLabController.cs:264-337`):
1. `request ??= new CutLabRequest()` (line 270)
2. Required-field + `IsWhatifIntent(intent)` check → re-render full page with `error: NoChangeMessage` via `CutLabView(request, error: ...)` (line 272-278) — **no Pool.Count==0 check anywhere in this action.**
3. `CutLabStateSerializer.Deserialize(request.CutLabStateJson)` (line 282) — same commander-lock re-assertion as API.
4. `IsValidWhatifPair(state, cardOut, cardIn)` (line 283) — **returns bool** (private method, lines 383-399): checks working-list `cardOutPoolCard` exists, `!IsLocked`, **and explicitly `!IsCommander`** (line 388-389, unlike the API's `ValidateWhatifPair`), then checks cut-pile membership for `cardIn`. On `false` → `RenderWhatifViewAsync(request, state, null, NoChangeMessage)` — this re-runs `_pageService.ProcessAsync` to fully re-render the page even for a rejected swap (line 285).
5. `if (intent == "preview")` branch calls the shared `_whatifPreviewService.ComputeSwapPreviewAsync` and returns (line 288-294) — **not part of commit, but shares the action method with it.**
6. "keep" branch: second raw-pool re-check `state.Pool.FirstOrDefault(... cardOut ...)` → reject if `null || IsLocked || IsCommander` (line 296-300) — **explicit `IsCommander` check present, unlike API's equivalent re-check.**
7. `afterRestore` / `afterSwap` via `CutLabDecisionApplier.Apply` — **identical calls** to the API path (line 302-311).
8. Same overshoot/atomicity guard, same comment verbatim (line 312-316).
9. `RehydrateIntakeRequestFromState(request, afterSwap)` + `request.CutLabStateJson = CutLabStateSerializer.Serialize(afterSwap)` (line 318-319) — **API path has no equivalent; this exists because the no-JS flow re-derives deck-text/intent fields for the full-page re-render.**
10. `result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted)` (line 321) — **full intake/analysis pipeline**, not `ICutLabUiPatchBuilder`. This is the architectural fork point: two completely different downstream code paths compute "what does the page look like after this commit."
11. `return View("CutLab", CutLabViewModel.From(request, result))` (line 322).
12. Error handling has **three** distinct catch clauses, none matching the API's single clause:
    - `catch (InvalidOperationException exception)` → `CutLabView(request, error: exception.Message)` — **surfaces the real exception message**, unlike API's fixed copy (line 324-327).
    - `catch (OperationCanceledException)` → `CutLabView(request, error: "The request timed out. Try again.")` (line 328-331) — **API has no explicit `OperationCanceledException` handling; it would propagate uncaught (not wrapped by the `InvalidOperationException or ArgumentException` filter) to the global exception handler.**
    - `catch (Exception exception)` → `_logger.LogError(...)`, `CutLabView(request, error: NoChangeMessage)` (line 332-336) — **catch-all not present in the API controller.**
13. Uses `HttpContext.RequestAborted` directly (not a bound `CancellationToken` parameter) for both the preview call and `_pageService.ProcessAsync`.

### The Third Validation Copy — inside the preview service itself

`CutLabWhatifPreviewService.ComputeSwapPreviewAsync` (`DeckFlow.Web/Services/CutLab/CutLabWhatifPreviewService.cs:56-88`) **re-validates independently** of either controller's pre-check: derives the working list, finds `cardOutPoolCard`, throws `InvalidOperationException(NoChangeMessage)` if missing or `IsLocked` (lines 71-77, no `IsCommander` check, same rationale as the API's `ValidateWhatifPair` — relies on the deserialize-time invariant), then checks cut-pile membership for `cardIn` (lines 79-83). This means the controller-level `ValidateWhatifPair`/`IsValidWhatifPair` pre-checks are **currently redundant for the preview path** — `ComputeSwapPreviewAsync` would reject an invalid pair on its own. They are *not* redundant for the commit path today because commit does not call `ComputeSwapPreviewAsync` at all.

### Commander-Lock / Quantity-Legality / Floor-Warning — where they actually live

- **Commander lock:** `CutLabLockRules.EnforceCommanderLock` (`CutLabLockRules.cs:12-27`) forces every `IsCommander` card's `IsLocked = true`. Called from `CutLabStateSerializer.Deserialize` (`CutLabStateSerializer.cs:80` — runs on **every** deserialize, both transports), and again inside every `CutLabDecisionApplier.Apply` branch (`CutLabDecisionApplier.cs:29, 60, 86`). **This means the explicit `IsCommander` checks in `IsValidWhatifPair` (no-JS) and the no-JS raw-pool re-check are currently redundant, not divergent** — but they are a stronger, more explicit invariant restatement than the API's `IsLocked`-only checks. Recommend the consolidated service keep the explicit `IsCommander` check (belt-and-suspenders) so CLUP-05's "commander locks... covered by shared tests" is provably true even if the deserialize-time invariant is ever weakened by a future change.
- **Quantity legality (100-card overshoot guard):** `CutLabDecisionApplier.Apply`, `Accept` branch (`CutLabDecisionApplier.cs:40-50`) — computes `remaining = workingList.Sum(quantity) - 100`; if the card's quantity exceeds `remaining`, the decision is silently dropped (state returned unchanged) rather than throwing. Both commit paths detect this via the **decision-count-unchanged** heuristic (`afterSwap.Decisions.Count == afterRestore.Decisions.Count`), not via a direct return value from `Apply`. This heuristic is itself a duplicated 5-line block in both controllers today (comment text is copy-pasted verbatim) and should move into the shared `CommitSwapAsync`.
- **Floor warnings:** `CutLabFloorRules.Evaluate` (referenced by `CutLabUiPatchBuilder.BuildFloorWarnings`, `CutLabUiPatchBuilder.cs:174+`, and `CutLabApiController.BuildFloorWarnings`, `CutLabApiController.cs:398-419`). Neither commit path computes floor warnings for the *just-swapped* card explicitly — the API path relies on `ICutLabUiPatchBuilder.BuildAsync`'s default (`floorWarnings: null` → computed for the **next proposal**, `CutLabUiPatchBuilder.cs:116`), and the no-JS path relies on whatever `ICutLabPageService.ProcessAsync` computes internally as part of the full pipeline. **Floor-warning computation for swap commits is not currently a shared concern — it is out of scope for the new `ICutLabWhatifService`.** CLUP-05's floor-warning requirement is satisfied because both downstream consumers (`ICutLabUiPatchBuilder` and `ICutLabPageService`) already call into the same `CutLabFloorRules.Evaluate`; the new service does not need to touch this.

### Recommended Interface Shape

```csharp
// Source: derived from DeckFlow.Web/Services/CutLab/CutLabWhatifPreviewService.cs (existing)
// and the duplicated commit blocks in CutLabApiController.cs / CutLabController.cs (extracted)
namespace DeckFlow.Web.Services.CutLab;

/// <summary>Computes and commits Cut Lab what-if swaps for both the JSON API and no-JS transports.</summary>
public interface ICutLabWhatifService
{
    /// <summary>Computes the metric deltas for a hypothetical swap without persisting it. (Renamed from ComputeSwapPreviewAsync — same signature/behavior.)</summary>
    Task<CutLabWhatifPreview> PreviewSwapAsync(CutLabState state, string cardOut, string cardIn, CancellationToken cancellationToken);

    /// <summary>Validates and atomically applies a restore-then-accept swap, or reports why it could not be applied.</summary>
    Task<CutLabWhatifCommitResult> CommitSwapAsync(CutLabState state, string cardOut, string cardIn, CancellationToken cancellationToken);
}

/// <summary>Result of a Cut Lab what-if commit attempt.</summary>
public sealed record CutLabWhatifCommitResult
{
    /// <summary>True when the swap was applied; false when rejected (locked/invalid pair/overshoot).</summary>
    public bool Applied { get; init; }

    /// <summary>The resulting state — the post-swap state when Applied, otherwise the original input state unchanged.</summary>
    public required CutLabState State { get; init; }

    /// <summary>Card removed by the swap when Applied.</summary>
    public string? CardOut { get; init; }

    /// <summary>Card restored by the swap when Applied.</summary>
    public string? CardIn { get; init; }

    /// <summary>Rejection reason when !Applied; always <see cref="CutLabMessages.NoChangeMessage"/> today (both existing paths use one fixed copy for rejections).</summary>
    public string? ErrorMessage { get; init; }
}
```

**Adapter behavior per transport (must preserve exactly):**

- **`CutLabApiController.PostWhatifCommitAsync`:** keep steps 1-4 (same-origin, null/required, deserialize, `Pool.Count==0`) in the controller — these are HTTP-shape concerns, not swap-commit logic. Replace steps 5-9 with one call: `CutLabWhatifCommitResult result = await _whatifService.CommitSwapAsync(state, request.CardOut, request.CardIn, cancellationToken)`. If `!result.Applied` → `BadRequest(new { Message = result.ErrorMessage ?? CutLabMessages.NoChangeMessage })` (preserves today's fixed-copy behavior since `ErrorMessage` will always be `NoChangeMessage` given the current rule set). If `Applied` → keep steps 10-12 unchanged (`_patchBuilder.BuildAsync(result.State, ...)`, build `CutLabWhatifApiResponse`). **The `try/catch (InvalidOperationException or ArgumentException)` wrapper can be removed for the commit call** since `CommitSwapAsync` no longer throws for expected-invalid-pair cases — it must still wrap `_patchBuilder.BuildAsync` for genuine simulation/analysis failures, matching current behavior since those exceptions originate downstream of the swap logic today too.
- **`CutLabController.Whatif` "keep" branch:** keep steps 1-3 (null-coalesce, required-field/intent check, deserialize) and the "preview" branch (step 5) untouched. Replace steps 4 and 6-8 with: `CutLabWhatifCommitResult result = await _whatifService.CommitSwapAsync(state, cardOut, cardIn, HttpContext.RequestAborted)`. If `!result.Applied` → `RenderWhatifViewAsync(request, state, null, result.ErrorMessage ?? CutLabMessages.NoChangeMessage)` (note: pass the **original** `state`, not `result.State`, to preserve today's "leave state unchanged on rejection" behavior — `result.State` already equals the original `state` on rejection per the record contract above, so `result.State` is safe to use here too, but using `state` directly is more obviously correct and matches the current code's variable usage). If `Applied` → keep steps 9-11 unchanged (`RehydrateIntakeRequestFromState`, `Serialize`, `_pageService.ProcessAsync`, `View(...)`) using `result.State` in place of `afterSwap`. **Error-handling catch clauses (`InvalidOperationException`/`OperationCanceledException`/`Exception`) stay exactly as-is** — they now only guard `_pageService.ProcessAsync` and `Deserialize`, not the swap logic, which is the same scope they effectively guard today once the swap-rejection paths are pulled into `CommitSwapAsync`.

**`CommitSwapAsync` internal implementation** (extracted, one copy, used by both):
1. `ArgumentNullException`/`ArgumentException.ThrowIfNullOrWhiteSpace` guards on `state`/`cardOut`/`cardIn` (matches `ComputeSwapPreviewAsync`'s existing guard style, `CutLabWhatifPreviewService.cs:62-64`).
2. Working-list-based validation: reuse `CutLabWorkingList.Derive` + explicit `IsLocked` **and** `IsCommander` check on the working-list `cardOutPoolCard` (adopt the no-JS path's stricter, more explicit check — behaviorally identical today due to the deserialize invariant, but strictly safer). Reuse `CutLabWorkingList.AcceptedCardNames(state.Decisions).Contains(cardIn)` for cut-pile membership.
3. On validation failure → return `new CutLabWhatifCommitResult { Applied = false, State = state, ErrorMessage = CutLabMessages.NoChangeMessage }` (no throw — this replaces both `ValidateWhatifPair`'s throw-contract and `IsValidWhatifPair`'s bool-contract with one try-pattern result type).
4. Raw-pool re-check (both paths do this today as defense-in-depth against a working-list/raw-pool state mismatch) — fold into the same validation pass rather than keeping it as a separate second check; the current double-check (working-list check, then raw-pool check) both check the *same underlying pool card* in practice since `CutLabWorkingList.Derive` never mutates `IsLocked`/`IsCommander`. A single raw-pool-based check on the deserialized `state.Pool` is sufficient and removes the redundant second lookup while preserving identical accept/reject behavior — confirm this simplification is safe via the shared test suite (see Test Inventory) before removing the double-check, since it is a minor behavior-preservation risk worth flagging as `[ASSUMED]` (see Assumptions Log).
5. `afterRestore = CutLabDecisionApplier.Apply(state, cardIn, Restore, CutLabCutRoundEngine.WhatifSwapKey)`.
6. `afterSwap = CutLabDecisionApplier.Apply(afterRestore, cardOut, Accept, CutLabCutRoundEngine.WhatifSwapKey)`.
7. Overshoot guard: `if (afterSwap.Decisions.Count == afterRestore.Decisions.Count) return new CutLabWhatifCommitResult { Applied = false, State = state, ErrorMessage = CutLabMessages.NoChangeMessage };` — **note: return the original `state`, not `afterRestore`, on rejection**, matching current behavior where neither controller ever surfaces `afterRestore` as a partial result.
8. Success → `return new CutLabWhatifCommitResult { Applied = true, State = afterSwap, CardOut = cardOut, CardIn = cardIn };`.

`CutLabMessages` is currently `internal static class CutLabMessages` (`DeckFlow.Web/Services/CutLab/CutLabMessages.cs:3`) — same assembly as the new service, no visibility change needed.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Commander-lock / overshoot-guard enforcement | A new rule engine inside `ICutLabWhatifService` | `CutLabDecisionApplier.Apply` + `CutLabLockRules.EnforceCommanderLock` (already exist) | These are already the single source of truth for both current commit paths; re-deriving the rules in the new service would create a fourth copy instead of zero |
| Swap-pair validation | A third bespoke validation routine | Consolidate the existing three (API `ValidateWhatifPair`, no-JS `IsValidWhatifPair`, preview-service internal check) into one method on `ICutLabWhatifService` | All three already implement the same rule with only throw-vs-bool and IsCommander-explicitness differences |

**Key insight:** Nothing in this phase requires new business logic — it requires deleting two duplicated call sites and standardizing on one result-returning method that both existing rule primitives (`CutLabDecisionApplier`, `CutLabLockRules`, `CutLabWorkingList`) already satisfy.

## Common Pitfalls

### Pitfall 1: Silently changing the rejection contract from throw to return-value breaks nothing today, but only if both controllers stop wrapping the call in `try/catch (InvalidOperationException or ArgumentException)`
**What goes wrong:** If `CommitSwapAsync` is implemented as "return a result object" but a caller keeps its old `catch (InvalidOperationException or ArgumentException)` wrapper "just in case," a genuine downstream exception (e.g. from `_patchBuilder.BuildAsync`) would now be swallowed into the generic `NoChangeMessage`, hiding real bugs behind a message that used to only mean "invalid swap pair."
**Why it happens:** The API controller's existing catch clause currently covers *both* the swap-validation throw and any exception from `_patchBuilder.BuildAsync` — moving swap validation to a non-throwing contract narrows what that catch clause should legitimately cover.
**How to avoid:** Scope the API controller's `try/catch` to wrap only `_patchBuilder.BuildAsync` (and `commanderNames`/`floorByRole` derivation) after the `CommitSwapAsync` call, not the `CommitSwapAsync` call itself, once it no longer throws for expected-invalid-pair cases.
**Warning signs:** A test asserting `PostWhatifCommitAsync_ReturnsBadRequestForLockedCardOutWithoutChangingState` still passes, but a *new* test injecting a fake `_patchBuilder` that throws `InvalidOperationException` unexpectedly also returns 400 `NoChangeMessage` instead of propagating/500ing as today's code would for a non-swap-validation failure. Add that negative test.

### Pitfall 2: Losing the no-JS path's real exception message
**What goes wrong:** The no-JS `Whatif` action's `catch (InvalidOperationException exception) → CutLabView(request, error: exception.Message)` surfaces the actual message today. If `CommitSwapAsync` is consolidated to never throw, this catch clause becomes dead code for the commit branch specifically (it can still fire from `Deserialize` or `_pageService.ProcessAsync`) — that is fine, but a plan must not delete this catch clause thinking it is now unreachable; it is still reachable via other calls in the same method body.

### Pitfall 3: Assuming `IsCommander` implies `IsLocked` is a universal invariant rather than a re-asserted one
**What goes wrong:** The invariant holds only because every state-mutation path (`Deserialize`, `Apply`, adjustment applier) calls `CutLabLockRules.EnforceCommanderLock`. If `CommitSwapAsync` is ever handed a `CutLabState` constructed by a test double or future caller that bypasses these call sites, an explicit `IsCommander` check (not relying purely on `IsLocked`) is the safety net — this is why the recommended shared validation adopts the no-JS path's stricter explicit check rather than the API path's `IsLocked`-only check.
**How to avoid:** Keep the explicit `!card.IsCommander` check in the consolidated validation (see interface shape above), even though it is currently provably redundant.

## Code Examples

### Existing shared preview call (pattern to mirror for commit)
```csharp
// Source: DeckFlow.Web/Controllers/Api/CutLabApiController.cs:238-240
CutLabWhatifPreview preview = await _whatifPreviewService
    .ComputeSwapPreviewAsync(state, request.CardOut, request.CardIn, cancellationToken)
    .ConfigureAwait(false);
```

### Existing duplicated swap-application block (to be extracted into `CommitSwapAsync`)
```csharp
// Source: DeckFlow.Web/Controllers/Api/CutLabApiController.cs:300-314
// (byte-for-byte duplicated, including the comment, in DeckFlow.Web/Controllers/CutLabController.cs:302-316)
CutLabState afterRestore = CutLabDecisionApplier.Apply(
    state,
    request.CardIn,
    CutLabDecideAction.Restore,
    CutLabCutRoundEngine.WhatifSwapKey);
CutLabState afterSwap = CutLabDecisionApplier.Apply(
    afterRestore,
    request.CardOut,
    CutLabDecideAction.Accept,
    CutLabCutRoundEngine.WhatifSwapKey);
// Why: the overshoot guard can refuse the replacement cut, so a half-applied swap must be rejected.
if (afterSwap.Decisions.Count == afterRestore.Decisions.Count)
{
    return BadRequest(new { Message = CutLabMessages.NoChangeMessage });
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Preview logic duplicated per controller | Preview logic in `ICutLabWhatifPreviewService` | Phase 107 (pre-108) | Establishes the exact extraction pattern this phase repeats for commit |
| Ad-hoc JSON response building per endpoint | `ICutLabUiPatchBuilder` server-authored patch DTO | Phase 108 (CLUP-01/02/03) | The API commit adapter in this phase must keep calling this unchanged |

**Deprecated/outdated:** None — this is the natural next step of the Phase 107→108 consolidation trend, not a reversal of it.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Collapsing the "working-list check" + "raw-pool re-check" into one raw-pool-based check in `CommitSwapAsync` is behavior-preserving, because `CutLabWorkingList.Derive` never mutates `IsLocked`/`IsCommander` on existing cards (only `Quantity`, and only adds new synthetic basics with `IsLocked=false`/`IsCommander=false`). | Architecture Patterns → Recommended Interface Shape, step 4 | If some other code path *does* toggle `IsLocked` on a working-list-derived card without persisting it back to `state.Pool` (not found in this research), collapsing the double-check could accept a swap that today's raw-pool re-check would reject. Verify with a passing test asserting the collapsed check still rejects the existing `PostWhatifCommitAsync_ReturnsBadRequestForLockedCardOutWithoutChangingState` fixture before relying on the simplification; keep the double-check if that test cannot be made to pass trivially. |

## Test Inventory

**Test seam pattern used by these services:** constructor injection of interfaces; controller tests build fakes implementing `ICutLabWhatifPreviewService` (e.g. `FakeWhatifPreviewService` in both `CutLabControllerTests.cs:827` and `CutLabApiControllerTests.cs:953`) rather than mocking. The new `ICutLabWhatifService` should follow the same pattern — a `FakeWhatifService` per test file, or a single shared fake if both test files already share helpers (they currently do not; each file defines its own private fake class).

**Existing what-if tests found (`grep -rn "Whatif" DeckFlow.Web.Tests`):**

| File | Test | Currently Tests | Migration |
|------|------|------------------|-----------|
| `CutLabWhatifTests.cs` (505 lines) | Multiple `[Fact]`s around lines 22-320 | `ICutLabWhatifPreviewService`/`CutLabWhatifPreviewService` directly (metric-delta computation, feature-flag gate, round-key registration, forbidden-origin, bad-request-on-missing-fields, bad-request-on-invalid/locked pair, goal-aware deltas) | **Rename file/class-scope only if desired; logic stays.** Add new `CommitSwapAsync`-focused fixtures here (or a new `CutLabWhatifCommitServiceTests` region) covering: happy-path swap + round tagging, locked-card rejection, commander-card rejection, cut-pile-membership rejection, overshoot-guard rejection, state-unchanged-on-rejection. |
| `CutLabControllerTests.cs:565` | `Whatif_Keep_CommitsRestoreAndAcceptUnderWhatifRound` | Business rule: round-key tagging on commit | **Migrate assertion to the new service-level test.** Keep a thin controller-level smoke test only if it verifies the no-JS-specific adapter behavior (view model shape, `RehydrateIntakeRequestFromState` call), not the swap-rule outcome itself. |
| `CutLabControllerTests.cs:597` | `Whatif_LockedCardOut_RerendersNoChangeAndLeavesStateUnchanged` | Business rule: locked-card rejection + state-unchanged | **Migrate to service-level test** (duplicate of the API version below). Controller test should shrink to "given `CommitSwapAsync` returns `Applied=false`, controller re-renders with the error message" using a fake `ICutLabWhatifService`. |
| `CutLabControllerTests.cs:650` | `Whatif_Preview_UsesSharedServiceWithoutResolveSingleCalls` | Preview delegation + resolve-avoidance | **Stays as-is** (preview path unaffected by this phase; still a legitimate controller-level integration check of the real `CutLabWhatifPreviewService` wired through `CutLabAnalysisContextBuilder`). |
| `CutLabApiControllerTests.cs:558` | `PostWhatifCommitAsync_SwapsCardsAtomicallyAndTagsAcceptedCutWithWhatifRound` | Business rule: round-key tagging + patch response shape | **Split.** Round-tagging assertion migrates to service-level test (duplicate of the no-JS version above). Patch-response-shape assertion (`payload.Patch.WhatifCardInOptions` contains restored card) stays as a thin controller test verifying the API adapter still calls `_patchBuilder.BuildAsync` correctly. |
| `CutLabApiControllerTests.cs:607` | `PostWhatifCommitAsync_ReturnsBadRequestForLockedCardOutWithoutChangingState` | Business rule: locked-card rejection | **Migrate to service-level test** (duplicate of the no-JS version). Controller test shrinks to "fake service returns rejection → controller returns 400 with that message." |
| `CutLabApiControllerTests.cs:647` | `PostWhatifCommitAsync_ReturnsBadRequest_WhenReplacementCutWouldOvershootRemainingBudget` | Business rule: overshoot guard | **Migrate to service-level test.** No no-JS equivalent test exists today — this is a genuine coverage gap the consolidation fixes for free once the rule lives in one tested place. |

**Net effect:** 3 controller-level business-rule tests become 1 shared service-level test each (round-tagging, locked-rejection, overshoot-rejection) — 6 duplicated assertions collapse to 3 canonical ones, plus each controller keeps 1-2 thin adapter tests (patch-shape for API, view-model-shape for no-JS). New coverage: commander-card-specific rejection and cut-pile-membership rejection at the service level, which today are only indirectly exercised.

## DI Registration

Single registration point, `DeckFlow.Web/Program.cs:183`:
```csharp
builder.Services.AddScoped<DeckFlow.Web.Services.CutLab.ICutLabWhatifPreviewService, DeckFlow.Web.Services.CutLab.CutLabWhatifPreviewService>();
```
If the interface is renamed to `ICutLabWhatifService` (recommended), this single line changes to:
```csharp
builder.Services.AddScoped<DeckFlow.Web.Services.CutLab.ICutLabWhatifService, DeckFlow.Web.Services.CutLab.CutLabWhatifService>();
```
No other DI registrations reference this type (verified — only Program.cs, the two controllers, the service file, and 3 test files reference `ICutLabWhatifPreviewService` repo-wide).

## Blast Radius / Risks

**Files referencing `ICutLabWhatifPreviewService` by name (rename must touch all):**
- `DeckFlow.Web/Program.cs:183`
- `DeckFlow.Web/Controllers/CutLabController.cs:14, 21, 26, 29`
- `DeckFlow.Web/Controllers/Api/CutLabApiController.cs:22, 29, 35, 41`
- `DeckFlow.Web/Services/CutLab/CutLabWhatifPreviewService.cs:7` (interface declaration itself)
- `DeckFlow.Web.Tests/CutLabControllerTests.cs` (fake class + constructor param, multiple lines around 698, 827)
- `DeckFlow.Web.Tests/CutLabApiControllerTests.cs` (fake class + constructor param, multiple lines around 38, 953)
- `DeckFlow.Web.Tests/CutLabWhatifTests.cs` (direct instantiation of the concrete service)

No Razor views, TypeScript, or JS reference this interface — it is a pure server-side C# type, never serialized by name to the client. No JSON contract changes (the recommended adapter design preserves `CutLabWhatifApiResponse`/`CutLabWhatifApiRequest` exactly).

**EOL/formatting caution:** Repo enforces LF via `.gitattributes`; the changed-lines format gate (`format-gate` CI, `.githooks` pre-commit) only checks touched lines. Since Codex will implement this via `codex exec` per project convention, the dispatch prompt must carry the standard per-file line-ending preservation instruction. `CutLabController.cs`, `CutLabApiController.cs`, and `CutLabWhatifPreviewService.cs` are existing `.cs` files with `eol: unspecified` per `.gitattributes` conventions noted in project `CLAUDE.md` — touch only the lines being extracted/replaced, do not reflow the surrounding method bodies.

**Runtime verification limitation:** Local Windows dev server cannot reach Scryfall (documented in prior sessions — TLS fingerprint block). `PostWhatifAsync`/`PostWhatifCommitAsync`/no-JS `Whatif` all depend on resolved card data flowing through `ICutLabAnalysisContextBuilder`/`ICutLabSimulationService`, which ultimately resolve via Scryfall in a live pipeline. Plan verification must rely on **xUnit** (`dotnet build` clean + full test run — fakes/stubs already isolate Scryfall per the existing test seam pattern) and **vitest** (`npm test` in `DeckFlow.Web/`, relevant only if any TS patch-rendering code touches what-if UI, which this phase's scope does not appear to require) and `tsc` (via the MSBuild TypeScript target on `dotnet build`). Defer any live `decide`/`whatif`/`whatif/commit` Scryfall-dependent smoke test to prod UAT, consistent with the Phase 108 precedent (`.foreman/ledger-104-execute-2026-07-20.md` deferred UAT for the same reason).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Web.Tests`), Vitest (via `npm test` in `DeckFlow.Web/`, TS-only — not expected to be touched by this phase) |
| Config file | `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`; no `vitest.config` changes expected |
| Quick run command | `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabWhatif\|FullyQualifiedName~CutLabController\|FullyQualifiedName~CutLabApiController"` |
| Full suite command | `dotnet build DeckFlow.sln && dotnet test DeckFlow.sln` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CLUP-04 | `ICutLabWhatifService` exposes preview + commit used by both controllers | unit + integration (controller-level with fakes) | `dotnet test --filter FullyQualifiedName~CutLabWhatif` | ✅ existing files, new test methods needed |
| CLUP-05 | Preview non-destructive, commit atomic, commander-lock/quantity/floor-warning rules identical both paths | unit | `dotnet test --filter FullyQualifiedName~CutLabWhatif` | ✅ (rejection/atomicity fixtures exist for API, need porting/adding for shared service + no-JS parity) |

### Sampling Rate
- **Per task commit:** `dotnet build DeckFlow.sln` (clean, no new warnings) + targeted `dotnet test --filter` above.
- **Per wave merge:** Full `dotnet test DeckFlow.sln`.
- **Phase gate:** Full suite green before `/gsd-verify-work`; no browser/Scryfall-dependent UAT locally (see Blast Radius note above) — defer to prod.

### Wave 0 Gaps
- [ ] No new test file strictly required — extend `CutLabWhatifTests.cs` with `CommitSwapAsync` fixtures (happy path, locked, commander, cut-pile-miss, overshoot, state-unchanged-on-rejection).
- [ ] Update the two existing `FakeWhatifPreviewService` classes (`CutLabControllerTests.cs`, `CutLabApiControllerTests.cs`) to implement the renamed interface and its new `CommitSwapAsync` member.
- [ ] No framework install needed — xUnit/vitest infrastructure already present and sufficient.

## Security Domain

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V4 Access Control | Indirectly | Same-origin check (`SameOriginRequestValidator`) and `[ValidateAntiForgeryToken]` stay in each controller, untouched by this refactor — the new service has no HTTP-layer awareness |
| V5 Input Validation | Yes | Swap-pair validation (locked/commander/cut-pile membership) consolidates into one tested implementation instead of two, reducing drift risk between transports |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Client-tampered `CutLabStateJson` re-locking a commander or bypassing quantity legality | Tampering | `CutLabLockRules.EnforceCommanderLock` on every deserialize (unchanged by this phase) + the consolidated `CommitSwapAsync` re-validating server-side regardless of what the client claims |
| Divergent validation between the two transports allowing a swap through one endpoint that the other would reject | Tampering / Elevation of Privilege (locally, "commit a swap that violates commander-lock via whichever transport has the weaker check") | This phase's entire purpose — eliminating this class of drift is CLUP-05's stated goal |

This phase does not add new authentication, session, or cryptography surface — no V2/V3/V6 rows needed.

## Sources

### Primary (HIGH confidence — direct code read, this session)
- `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` (full file, 527 lines)
- `DeckFlow.Web/Controllers/CutLabController.cs` (lines 1-50, 255-435)
- `DeckFlow.Web/Services/CutLab/CutLabWhatifPreviewService.cs` (full file, 168 lines)
- `DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs` (full file, 94 lines)
- `DeckFlow.Web/Services/CutLab/CutLabLockRules.cs` (lines 1-40)
- `DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs` (lines 1-100)
- `DeckFlow.Web/Services/CutLab/CutLabUiPatchBuilder.cs` (lines 1-130)
- `DeckFlow.Web/Services/CutLab/CutLabMessages.cs` (grep, `NoChangeMessage` constant location)
- `DeckFlow.Web/Models/Api/CutLabWhatifApiRequest.cs`, `CutLabWhatifApiResponse.cs` (full files)
- `DeckFlow.Web/Program.cs` (grep, line 183 DI registration)
- `DeckFlow.Web.Tests/CutLabWhatifTests.cs`, `CutLabControllerTests.cs`, `CutLabApiControllerTests.cs` (grep for `Whatif`, targeted line reads)
- `DeckFlow.Web/package.json` (test script confirmation)
- `.planning/config.json` (workflow flags: `nyquist_validation: true`, `cross_ai_execution: true`)

### Secondary / Tertiary
None used — this phase required no external library research; all findings are internal-codebase-only.

## Open Questions

1. **Should the interface actually be renamed, or should a new `CommitSwapAsync` simply be added to the existing `ICutLabWhatifPreviewService` name?**
   - What we know: CLUP-04 explicitly names the target interface `ICutLabWhatifService` (singular, transport-agnostic name, dropping "Preview").
   - What's unclear: Whether the planner wants a rename (touches 7 files listed in Blast Radius) or considers "add a method to the existing interface, keep the name" an equally valid reading of CLUP-04's "one `ICutLabWhatifService` path" wording (the requirement could be read as "one code path" rather than "one interface literally named that").
   - Recommendation: Rename — it is a mechanical, low-risk change (7 files, all internal, no serialization boundary crossed) and it makes the interface name accurately describe what it now does (preview + commit), avoiding a stale "Preview"-only name once commit logic joins it.

## Metadata

**Confidence breakdown:**
- Standard stack: N/A — no new stack, pure internal refactor
- Architecture: HIGH — every divergence documented against exact file:line reads on the actual branch/commit in scope
- Pitfalls: HIGH — derived directly from observed code structure, not speculative

**Research date:** 2026-07-23
**Valid until:** Valid as long as branch `gsd/cycle19-cut-lab-upgrade` head remains `3d5f6341` for the files listed in Sources; re-verify line numbers if further Cut Lab commits land before Phase 109 execution.
