---
phase: 38
reviewers: [codex]
reviewed_at: 2026-06-12T15:29:50Z
reviewer_models: { codex: gpt-5.5 (medium) }
plans_reviewed:
  - 38-01-controller-base-shell-PLAN.md
  - 38-02-sync-convert-judge-PLAN.md
  - 38-03-lookup-categories-PLAN.md
  - 38-04-packet-primer-PLAN.md
  - 38-05-commandrunners-split-PLAN.md
  - 38-06-tests-route-parity-PLAN.md
verdict: MEDIUM / BLOCK (2 HIGH, 3 MEDIUM, 1 LOW)
---

# Cross-AI Plan Review — Phase 38 (Controller SRP Split)

Reviewer: **Codex gpt-5.5 (medium)**, full-access sandbox, validated plan text against live source.
Claude's secondary `gsd-plan-checker` already caught + fixed two runtime breaks (view discovery, exception-handler repoint) in commit `2677b72`. Codex confirmed those fixes are real, then found further plan/source mismatches.

## Codex Review

**Summary.** Decomposition strategy is sound and the known view-discovery / exception-handler fixes are real and mostly sufficient. Several plan-level defects remain that can cause compile warnings/errors or weaken SC1/SC3 proof: an unused logger in the new judge controller, a non-exhaustive CLI method allocation, an incomplete relocated-fakes list, a namespace gap in the expander registration, and a route-parity proof that sidesteps the conventional `/Deck/Error` route.

### Strengths
- Both live MVC name-couplings correctly identified: `UseExceptionHandler("/Deck/Error")` (`Program.cs:389`) and `Url.Action("Home","Deck")` (`Views/Deck/Error.cshtml:10`).
- View-location risk is real; Plan 01 addresses it centrally; all Deck views live under `Views/Deck/`.
- Every `DeckController` action except `Error()` is attribute-routed; `Error()` is the only conventional action. `MapDefaultControllerRoute()` present (`Program.cs:419`).
- Web split ordering correct: 02→03→04 all mutate/delete `DeckController.cs`, sequential.
- DI subset mapping good; no shared controller instance state relied on across actions.

### Concerns (severity-tagged, source-grounded)

- **HIGH — Plan 38-02 (`:148`):** `JudgeQuestionsController` planned to inject/store `ILogger<JudgeQuestionsController>` "for symmetry/future logging," but the live `JudgeQuestions` action has NO service or logger dependency (`DeckController.cs:148`). An assigned-but-unused `_logger` creates a new compiler warning → violates SRP-03 (no new warnings). **Fix:** make `JudgeQuestionsController` parameterless; no unused logger field.

- **HIGH — Plan 38-05 (`:73`):** CLI allocation still incomplete vs live `CommandRunners.cs`. Missing: public `ResolveConflicts` (`:170`, used by `RunCompareAsync`); private helpers/types `BuildProbeRequest` (`:1867`), `CreateDeckEntryLoader` (`:2171`), `ScryfallCardDto` (`:2178`). **Fix:** make the allocation list exhaustive — every public/internal method AND every private helper/type referenced by moved methods.

- **MEDIUM — Plan 38-06 (`:81`):** fake-relocation list stale. Live test file has 22 nested doubles, not 19; missing `AlternateNameSingleCardLookupService` (`DeckControllerTests.cs:1061`), `MultiMechanicSingleCardLookupService` (`:1070`), `PartiallyFailingMechanicLookupService` (`:1110`). One used by an existing test (`:425`). **Fix:** relocate all 22.

- **MEDIUM — Plan 38-01 (`:175`):** expander registration references `new DeckViewLocationExpander` from `Program.cs` (namespace `DeckFlow.Web`), but the new type is planned in `DeckFlow.Web.Controllers`; no `using DeckFlow.Web.Controllers` in `Program.cs`. **Fix:** add the using, or instantiate `new DeckFlow.Web.Controllers.DeckViewLocationExpander()`.

- **MEDIUM — Plan 38-06 (`:145`):** attribute-route diff is sound for attribute-routed actions but cannot prove literal SC1 — live `Error()` is conventional (`DeckController.cs:80`), reachable as `/Deck/Error`. Moving it to `ShellController` with no route makes `/Shell/Error` reachable instead — sufficient for `UseExceptionHandler` but NOT for "pre/post URL list identical." **Fix:** preserve `/Deck/Error` explicitly (e.g. `[Route("Deck/Error")]` on `ShellController.Error`, leaving `UseExceptionHandler("/Deck/Error")` unchanged) OR document this as an accepted SC1 exception.

- **LOW — multiple plans:** build-verify often greps only `error|Build succeeded` (e.g. `38-01:127`), which can hide NEW warnings while acceptance requires none. **Fix:** capture warnings explicitly (as Plan 06 partially does).

### Risk Assessment
Overall **MEDIUM**. Strategy sound; concrete plan↔source mismatches warrant **BLOCK** until plan text is corrected. After fixes, safe to proceed as a mechanical refactor with route parity + build verification.

## Consensus Summary

Single external reviewer (Codex) this round; Claude's `gsd-plan-checker` was the secondary gate (its 2 blockers already fixed pre-review). Net actionable set for replan:

### Agreed Concerns (priority order)
1. **(HIGH)** Drop unused logger from `JudgeQuestionsController` — parameterless. [38-02]
2. **(HIGH)** Make Plan 05 CLI method/helper allocation exhaustive — add `ResolveConflicts`, `BuildProbeRequest`, `CreateDeckEntryLoader`, `ScryfallCardDto`, and any other referenced helper/type. [38-05]
3. **(MED)** Relocate all 22 nested test doubles, not 19. [38-06]
4. **(MED)** Fix expander namespace/using in Program.cs registration. [38-01]
5. **(MED)** Resolve `/Deck/Error` SC1 parity — preferred: keep `[Route("Deck/Error")]` on `ShellController.Error` and leave `UseExceptionHandler("/Deck/Error")` unchanged (also simplifies the prior blocker-2 repoint). [38-01 + 38-06]
6. **(LOW)** Build-verify steps capture warnings explicitly, not just `error|Build succeeded`. [all]
