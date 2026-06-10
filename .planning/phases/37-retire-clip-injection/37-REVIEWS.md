---
phase: 37
reviewers: [codex]
reviewer_models: { codex: "gpt-5.5 (reasoning: medium)" }
reviewed_at: 2026-06-10T22:05:23Z
plans_reviewed: [37-01-PLAN.md, 37-02-PLAN.md]
rounds: 2
verdict: APPROVE WITH CHANGES   # round 1 = BLOCK (build-green); round 2 = APPROVE WITH CHANGES, changes APPLIED → converged (no HIGH remains)
round1_verdict: BLOCK
round2_verdict: APPROVE WITH CHANGES
overall_risk: MEDIUM-LOW
converged: true
---

## Convergence Summary (2 rounds)

- **Round 1 (BLOCK):** Codex found a HIGH build-green failure — `IContentKbRelevanceService` deleted in 37-01 (Wave 1) while admin consumers + admin tests were fenced to 37-02 (Wave 2) → CS0246/CS0535 at the Wave-1 boundary. Plus 2 MEDIUM (kb-selection delete-ordering; narrow packet-field grep) + 2 LOW (dead CSS; Task1/2 atomicity).
- **Replan (`ae1d520`):** 37-01 became the COMPLETE removal wave (admin score-preview + browse-page strip + dead CSS pulled forward; Tasks 1-3 one atomic commit). 37-02 reduced to pure un-dark (flag flip, RET-04 XSS test, RET-06 add-only pointer, nav copy).
- **Round 2 (APPROVE WITH CHANGES):** Codex confirmed the BLOCKER cleared and no new HIGH (build-green / XSS / completeness) introduced. Remaining: 1 MEDIUM (stale "build after each task" wording in 37-01 vs the intended single atomic boundary) + 1 LOW (stale `content-kb-admin.js` comment in the KEEP file `kb-entry-filter.ts:3`).
- **Changes APPLIED (orchestrator, plan-doc edits):** (1) 37-01 build-gate header + Task 2 `<verify>`/acceptance reworded — build runs ONCE after Task 3; a build after Task 1/2 is expected-to-fail, not a regression. (2) Fence note documents the `kb-entry-filter.ts:3` comment as a harmless dangling comment excluded from the removal grep (KEEP file untouched). → **Converged; no HIGH remains. Cleared for execution.**

---

# Cross-AI Plan Review — Phase 37

Reviewer: **Codex (gpt-5.5, medium)** — independent peer review of the plans, read against live source (`danger-full-access`, read-only intent). The Claude `gsd-plan-checker` had PASSED these plans; Codex is the authoritative gate per project workflow and caught a build-green BLOCKER the checker missed.

**Orchestrator note:** the HIGH/BLOCK finding (build breaks between waves) was independently verified against live source before recording:
- `AdminContentKbController.cs:26/:39/:50/:84` consume `IContentKbRelevanceService` (`_relevanceService`).
- `AdminContentKbControllerTests.cs:369` implements `IContentKbRelevanceService` (`FakeContentKbRelevanceService`).
- `37-01-PLAN.md:88` ALLOWED-FILE-SET fence forbids touching `AdminContentKbController.cs` (deferred to 37-02); `:235/:245` defer `AdminContentKbControllerTests.cs` to 37-02.
- 37-01 Task 2 deletes `ContentKbRelevanceService.cs` (incl. the interface) → after the Wave-1 commit, the admin controller + its test still reference a deleted type ⇒ `dotnet build` fails (CS0246 / CS0535). RET-02 "build 0/0 at every commit boundary" is violated.
- Dead CSS confirmed at `site-common.css:340-433` (`.kb-pin-btn`, `.kb-follow-btn`, `.kb-selection-tray*`) — fenced out of both plans.

---

## Codex Review

**Summary** — The removal graph is mostly well researched and the XSS/back-compat reasoning is sound, but the current wave split is not build-green. Wave 1 deletes `IContentKbRelevanceService` while Wave 2 still owns live admin consumers of that interface. That will break `dotnet build` at the 37-01 boundary, so the plans need sequencing changes before execution.

**Strengths**
- The plans correctly include the two packet fields: `ExpertContextJson` and `ExpertSelectionJson`.
- RET-04 is treated as a real security requirement: `ContentKbController.cs:20` preserves Markdig `.DisableHtml()`, and `Views/ContentKb/Detail.cshtml:44` is the only public raw rendered artifact surface.
- The browse-page `kb-selection.ts` load at `Views/ContentKb/Index.cshtml:145` is caught and assigned to 37-02.
- No destructive DB migration is planned; `is_evergreen` is left intact.
- RET-06 correctly reuses the existing detail-page copy affordance at `Views/ContentKb/Detail.cshtml:35-43` instead of rebuilding it.

**Concerns**
- **HIGH — 37-01 Task 2 / 37-02 Task 3: build breaks between waves.** 37-01 deletes `ContentKbRelevanceService.cs`, including `IContentKbRelevanceService`, but 37-01's hard fence explicitly forbids touching `AdminContentKbController.cs`. The live controller still references the interface at `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs:26` and `:39`, with null guard/assignment at `:45` and `:50`. Its tests also implement the deleted interface at `DeckFlow.Web.Tests/AdminContentKbControllerTests.cs:369`. Therefore 37-01 cannot build 0/0 unless the admin score-preview removal moves into the same build-green unit as the interface deletion, or interface deletion is deferred until 37-02.

- **MEDIUM — 37-01 Task 2 deletes `kb-selection.ts` before 37-02 removes all browse-page callers.** After 37-01, `/content-kb` would still render the selection tray at `Views/ContentKb/Index.cshtml:62-71`, pin/follow buttons at `:103-121`, and the deleted script at `:145`. Build may not catch this Razor-to-static-asset break, but the page would carry dead expert-selection UI and reference a removed client bundle/API until 37-02 lands.

- **MEDIUM — 37-01 acceptance greps under-check removed packet fields.** The task action removes `ExpertContextJson`, `ExpertSelectionJson`, `PinnedVideoIds`, and `FollowedCreators` broadly, but acceptance only greps `DeckAnalysisRequest.cs`. Real consumers exist in `DeckController.cs:793-828`, `PacketArtifactStore.cs:287-299` and `:319-337`, `DeckAnalysisPacketService.cs:548-577` and `:699-743`, and `PacketArtifactStore.cs:913-914`. Build catches some paths if fields are removed, but a half-retired field left in the model could pass with dead threading still present.

- **LOW — dead expert-selection CSS is omitted from both plan fences.** `DeckFlow.Web/wwwroot/css/site-common.css:340-433` still defines `.kb-pin-btn`, `.kb-follow-btn`, and `.kb-selection-tray*`. This will not break the build, but it contradicts the "full code removal" intent and leaves visible dead feature artifacts in shared CSS.

- **LOW — 37-01 Task 1 conflicts with "build after each task."** It says to add tests using the post-removal prompt variant signature before the signature is changed, then notes they may not compile until Task 2. That is acceptable only if Task 1 and Task 2 are one commit/build boundary; otherwise it violates RET-02 sequencing.

**Suggestions**
- Move all admin relevance-preview removal from 37-02 Task 3 into 37-01 before deleting `IContentKbRelevanceService`, including `AdminContentKbControllerTests.cs`.
- Move `Views/ContentKb/Index.cshtml` selection-UI strip into 37-01, or delay deleting `kb-selection.ts` and `ContentKbSearchApiController.cs` until the browse page no longer references them.
- Add a solution-wide final grep for:
  `ContentKbRelevanceService|IContentKbRelevanceService|ExpertSelection\b|ContentKbArchetypeDeriver|ContentKbClipSanitizer|ContentKbExcerpt|ContentKbSearchApiController|kb-selection|ExpertContextJson|ExpertSelectionJson|PinnedVideoIds|FollowedCreators`
  across `DeckFlow.Web` and `DeckFlow.Web.Tests`, excluding `bin/obj`.
- Either remove the dead selection CSS in the same phase or explicitly document it as deferred cleanup with a reason.
- Treat 37-01 Task 1 + Task 2 as one atomic commit if the new tests use the final signature.

**Risk Assessment** — **HIGH**. The target end-state is coherent, but the current wave boundary will break the build because admin consumers outlive the deleted relevance interface.

`VERDICT: BLOCK`

---

---

## Round 2 — Codex Re-Review (post-replan `ae1d520`)

**Summary** — The prior HIGH/BLOCKER is cleared. 37-01 now owns the admin `IContentKbRelevanceService` consumers and admin tests in the same wave that deletes the interface (`37-01-PLAN.md` fence + Tasks 2/3). Live source confirmed those were real consumers (`AdminContentKbController.cs:26/:39`, `AdminContentKbControllerTests.cs:369`) — no longer deferred to 37-02. 37-02 is now pure un-dark/copy/XSS-test; its only re-touch of `DeckAnalysis.cshtml` is the add-only RET-06 note after 37-01 removes the widget.

**Strengths** — 37-01 fence now includes the pulled-forward admin controller/view/VM/admin-TS/admin-tests + browse `Index.cshtml`; kb-selection callers removed with the deleted TS/API; both packet JSON fields covered; RET-04 `.DisableHtml()` intact + regression test in 37-02; no destructive `is_evergreen` migration; RET-06 reuses existing copy affordance; solution-wide final grep present.

**Concerns**
- **MEDIUM — stale build-gate wording vs intended atomic boundary.** `37-01` said "Build gate (run after each task)" while Task 3 correctly requires Task 2+3 to land atomically with a single build after Task 3. End-state build-green; executor instructions were internally inconsistent. → **APPLIED FIX:** header + Task 2 verify/acceptance reworded to "build once, after Task 3; a build after Task 1/2 is expected-to-fail."
- **LOW — stale kept-file comment.** `kb-entry-filter.ts:3` mentions `content-kb-admin.js`; both plans forbid touching `kb-entry-filter.ts`. Not a build/runtime consumer. → **APPLIED FIX:** fence note documents it as a harmless dangling comment excluded from the removal grep.

**Risk Assessment** — **MEDIUM-LOW**. Original cross-wave build breaker fixed; no new HIGH build-green / XSS / completeness issue. Remaining risk was execution-ambiguity wording, now resolved.

`VERDICT: APPROVE WITH CHANGES` → changes applied → **converged.**

---

## Disposition — CLEARED FOR EXECUTION

Round 1 BLOCK resolved by replan `ae1d520`; round 2 APPROVE-WITH-CHANGES items applied as plan-doc edits. No HIGH remains. Cleared to execute:

```
/gsd:execute-phase 37
```

**Replan must address:**
1. **(HIGH, must-fix)** Make the `IContentKbRelevanceService` deletion and ALL its consumers (`AdminContentKbController.cs` score-preview + `AdminContentKbControllerTests.cs`) land in one build-green unit. Either pull the admin score-preview removal forward into 37-01, or defer the interface/service deletion into 37-02 with the admin strip. The two cannot straddle the Wave 1/Wave 2 commit boundary.
2. **(MEDIUM)** Resolve the `kb-selection.ts` / `ContentKbSearchApiController.cs` delete-vs-caller ordering so the browse page never references a removed bundle/endpoint at any commit boundary (move the `Index.cshtml` strip earlier, or delete the bundle/API later).
3. **(MEDIUM)** Broaden the removed-packet-field acceptance grep beyond `DeckAnalysisRequest.cs` to the full consumer set (`DeckController.cs`, `PacketArtifactStore.cs`, `DeckAnalysisPacketService.cs`) + a solution-wide final sweep (excluding `bin/obj`).
4. **(LOW)** Decide dead-CSS (`site-common.css:340-433`): remove in-phase or explicitly defer with a reason.
5. **(LOW)** Make 37-01 Task 1 (RET-01/05 tests) + Task 2 (removal) one atomic commit, since the new tests use the post-removal signature.
