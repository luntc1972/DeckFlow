---
phase: 59
reviewers: [codex]
plans_reviewed: [59-01-PLAN.md, 59-02-PLAN.md, 59-03-PLAN.md]
---

# Cross-AI Plan Review — Phase 59

## Codex Review (gpt-5.4, effort low)

**Cross-Plan Findings**

1. `HIGH` Phase 59’s metered-provider story is inconsistent with the current Core contract. `ContentKbOrchestrator.DistillAsync` explicitly refuses any live run where `!dryRun && !isSubscriptionProvider`, so Plan 03’s claim that metered providers can still complete distill and then reuse the shared auto-approve step is not true with the current code path. If that contract is intentional, D-09/SC4 are overstated; if metered live distill must work, one of these plans needs to explicitly change the Core gate.
2. `HIGH` Plan 03 does not define a reliable source of “just harvested ids.” `HarvestSelectedAsync` currently gets an aggregate `HarvestResult` only; it does not return per-video harvest success. Chaining all selected ids into distill will include unresolved/skipped/already-harvested videos and can produce transcript-missing failures, which breaks the “one clean action” goal.
3. `MEDIUM` The outcome-summary requirements in Plan 03 are underspecified versus current available data. `HarvestResult` gives counts, `DistillResult` gives counts and failed ids, but neither gives enough detail to compute an exact per-video “harvested/distilled/auto-approved/left-in-review/dropped/failed” ledger unless the plan adds more structured result state.
4. `MEDIUM` Plan 02 persists operator-controlled cutoff values but only treats corrupt JSON as invalid. It should also clamp or validate semantically bad values, especially negative cutoffs loaded from disk.

**59-01-PLAN**

**Summary**  
This is the strongest of the three plans. It keeps the clip-count heuristic within scope, respects the “no schema/provider/model change” constraint, and creates a clean seam for future replacement. The main risk is not architecture but whether the added `DistillResult` shape is sufficient and unambiguous for the Studio host to make approval decisions without leaking Core concerns upward awkwardly.

**Strengths**
- Keeps the signal exactly where the context says it belongs: clip count, no `confidence` field, no provider swap.
- Uses a swappable seam instead of hardcoding `clipCount >= cutoff` in the page.
- Surfaces the natural-key pair the host will actually need for `SetApprovalStatusAsync`.
- Keeps auto-approval out of Core orchestration, which matches the Studio-host ownership decision.
- Test scope covers the key boundaries and dry-run behavior.

**Concerns**
- `MEDIUM` The public record `DistilledVideoOutcome` and the existing private `DistillVideoOutcome` are dangerously similar names. That is easy to misread during implementation and review.
- `MEDIUM` The plan does not call out the podcast path explicitly. `GetContentNaturalKeyInfo(video)` supports YouTube and podcast, so tests should verify the public result model is not accidentally YouTube-only.
- `LOW` Adding an interface for a single comparison is slightly more abstraction than the current need demands, though the context justifies it.

**Suggestions**
- Rename one of the two outcome types so the public result DTO and private internal accumulator are not near-homonyms.
- Add one test proving `DistilledVideos` carries the correct natural key for a podcast-backed video, not only YouTube.
- Add one test that `DistilledVideos` ordering is deterministic enough for the host/tests, or explicitly state that ordering is not contractual.

**Risk Assessment**  
`LOW` — technically coherent, scoped correctly, and aligned with the phase constraints.

**59-02-PLAN**

**Summary**  
This plan is mostly sound and appropriately lightweight. JSON-file persistence in the Studio data dir fits the operator-local tooling model, and placing controls on `Harvest.razor` matches point-of-use expectations. The main gap is value validation and the practical behavior of saving on every UI change.

**Strengths**
- Good choice of persistence mechanism: simple, local, no schema migration burden.
- Correctly reuses `studioDataDirectory` instead of inventing a new storage convention.
- Default source of truth comes from Core’s `DefaultCutoff`, which avoids drift.
- The UI placement is sensible: the operator sees it next to Distill behavior.
- Corrupt-file fallback is explicitly planned.

**Concerns**
- `MEDIUM` The plan validates file parse failures but not invalid semantic values. A persisted negative cutoff should not silently survive load.
- `MEDIUM` Saving on every change can persist transient values from the numeric input and may cause noisier writes than necessary.
- `LOW` The plan does not specify whether the cutoff input is disabled when auto-approve is off. That is a UX detail, but it affects clarity.

**Suggestions**
- Clamp loaded cutoff values to `>= 0`, and consider enforcing a reasonable upper bound too.
- Save on blur or explicit change commit, not on every keystroke, unless Blazor’s current event wiring already gives stable commits.
- Disable or visually de-emphasize the cutoff input when `Enabled=false`.
- Add a test for “negative value in file” falling back or clamping, not just corrupt JSON.

**Risk Assessment**  
`LOW-MEDIUM` — the architecture is fine; the remaining risks are validation and UX polish rather than plan failure.

**59-03-PLAN**

**Summary**  
This plan is the headline feature, but it is also where the biggest correctness gaps are. The intended operator flow is right, and the separation between approval and publish remains intact, but the plan currently assumes capabilities the codebase does not expose cleanly: exact harvested-id tracking, metered live distill completion, and precise per-video summary accounting. Without tightening those seams first, the implementation will either become messy or fail SC1/SC4 in edge cases.

**Strengths**
- Correctly retains the manual Distill path instead of replacing it.
- Correctly centralizes auto-approve into a shared post-distill step so manual distill can benefit too.
- Keeps the spend gate concept intact for metered providers.
- Explicitly calls out continue-on-failure and “nothing silently lost,” which matches the operator workflow needs.

**Concerns**
- `HIGH` The current Core distill path refuses live metered runs. Plan 03 assumes a confirmed metered run can still complete and then auto-approve, but that is blocked today.
- `HIGH` “Collects the harvested video ids” is not supported by current `HarvestSelectedAsync`/`HarvestResult`. Using selected ids as a proxy will over-include skipped/unresolved/already-harvested videos and can generate false distill failures.
- `HIGH` The outcome card requires exact counts, but the current result objects do not provide enough structured information to derive them cleanly. That invites UI-side guesswork.
- `MEDIUM` The new one-click button sits in the Harvest card, but the settings and distill readiness logic live in the Distill card and pending-distill loader. The plan does not fully define how those two surfaces reconcile when selection spans browsed, queued, and pending-distill rows.
- `MEDIUM` The test plan is heavier than it looks. Current `HarvestPageTests` stubs throw for `DistillAsync` and `SetApprovalStatusAsync`; getting reliable bUnit coverage here means building substantial new test doubles, not just adding assertions.
- `LOW` README changes are fine, but they are not the risky part of this plan and should not distract from resolving the Core/runtime mismatches first.

**Suggestions**
- Resolve the metered-provider contradiction before implementation. Either:
  1. Explicitly keep one-click subscription-only for this phase and narrow D-09/SC4 wording, or
  2. Add a preceding plan change that makes confirmed metered live distill legal in Core.
- Add a structured harvest result that returns the exact per-video dispositions needed downstream, or refactor the one-click flow to distill only ids proven harvest-ready.
- Define the outcome-card counts from canonical data, not inferred UI state.
- Split the implementation into two internal checkpoints:
  1. one-click harvest→distill works correctly for subscription providers,
  2. auto-approve + summary card + manual-stage reuse.
- Add explicit tests for mixed batches: unresolved ids, already-harvested selections, filtered videos, and partial harvest failure.

**Risk Assessment**  
`HIGH` — this is the plan most likely to miss the phase success criteria unless the metered-path contract and harvested-id accounting are clarified up front.

**Overall Risk Assessment**

`MEDIUM-HIGH`

The dependency order is good and Plan 01/02 are solid. The phase risk is concentrated in Plan 03, specifically around SC4 and the missing per-video handoff between harvest and distill. If those two issues are corrected before implementation, the phase looks achievable without scope creep. If they are not, the team is likely to ship a version that works only in the happy-path subscription case while the plan still claims broader behavior.

---

## Consensus Summary

Single reviewer (Codex; Claude/Gemini skipped — self / not installed).

### Agreed Strengths
- Plans 01 & 02 are solid: clip-count signal kept in scope, swappable seam, JSON-file persistence in studio data dir, controls at point-of-use, no schema/provider swap.
- Dependency ordering (01→02→03) correct.
- Plan 03 correctly retains manual Distill, centralizes auto-approve into a shared post-distill step, keeps approve vs publish separate.

### Agreed Concerns (HIGH — block execution)
1. **HIGH — metered contradiction (verified true).** `ContentKbOrchestrator.DistillAsync` (line 244) returns `Success=false, AbortedReason` for any `!dryRun && !isSubscriptionProvider` run — Core REFUSES live distill on metered providers. CONTEXT decision **D-09** ("auto-approve still applies to metered distills after the operator confirms and the distill completes") is impossible today: metered live distill never completes. D-08/SC4 metered wording is overstated.
2. **HIGH — no per-video harvested-id source.** `HarvestSelectedAsync`/`HarvestResult` return aggregate counts, not per-video success. Using selected ids as the distill input over-includes skipped/unresolved/already-harvested videos → false transcript-missing failures, breaking SC1 "one clean action".
3. **HIGH — outcome-card data gap.** `HarvestResult`/`DistillResult` lack enough structured per-video state to compute the exact harvested/distilled/auto-approved/left-in-review/dropped/failed ledger (D-11) without UI-side guesswork.

### Lower-severity (address in replan)
- MEDIUM 59-02: validate/clamp semantically bad persisted cutoff (e.g. negative), not just corrupt JSON; save on blur not keystroke; disable cutoff input when auto-approve off.
- MEDIUM 59-01: rename near-homonym types `DistilledVideoOutcome` (public) vs `DistillVideoOutcome` (private); add podcast-natural-key test (not YouTube-only).
- MEDIUM 59-03: bUnit doubles are heavier than implied (current HarvestPageTests stubs throw for DistillAsync/SetApprovalStatusAsync).

### Divergent Views
None (single reviewer).

### Overall
Codex: **MEDIUM-HIGH**. Risk concentrated in Plan 03 (SC4 + harvest→distill per-video handoff). Plans 01/02 ship as-is with minor hardening.

---

## Codex Re-Review (convergence round, gpt-5.4 effort low) — 2026-06-20

1. HIGH #1 metered contradiction: `RESOLVED` — Plan 03 now scopes one-click live distill to `IsSubscriptionProvider=true` only, and explicitly forbids `DistillAsync`/auto-approve on metered providers.

2. HIGH #2 no per-video harvested-id source: `RESOLVED` — the revised flow now derives distill input from `ListPendingDistillAsync ∩ selected ids`, so skipped/no-caption/already-distilled selections are excluded from the live distill call.

3. HIGH #3 outcome-card data gap: `RESOLVED` — the revision now defines canonical structured sources for each card field, with `DistillResult.DistilledVideos` supplying the per-video data needed for auto-approved vs left-in-review counts.

New concerns:
- `MEDIUM` — Plan 03 still leaves ambiguity around the card’s “harvested N” definition by allowing either `harvestReadyIds.Count` or `HarvestResult.Captions + Whisper`. Those are not always equivalent, especially when selected videos were already distilled or otherwise excluded from pending-distill. The implementation should pick one canonical meaning and stick to it.

Overall risk: `MEDIUM`

**Convergence outcome:** all 3 HIGH RESOLVED; overall risk MEDIUM-HIGH → MEDIUM. The new MEDIUM ('harvested N' ambiguity) was fixed in 59-03 by pinning N=harvestReadyIds.Count as the single canonical definition. No HIGH remaining → execution unblocked.
