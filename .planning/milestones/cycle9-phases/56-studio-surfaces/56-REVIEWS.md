---
phase: 56
reviewers: [codex]
reviewed_at: 2026-06-18T16:20:00-06:00
reviewer_model: gpt-5.4 (reasoning_effort=low)
plans_reviewed: [56-01-PLAN.md, 56-02-PLAN.md, 56-03-PLAN.md, 56-04-PLAN.md]
---

# Cross-AI Plan Review — Phase 56 (Studio Surfaces)

## Codex Review (gpt-5.4, low effort)

## Summary
The plan set is generally well-structured, scoped, and traceable to Phase 56 requirements, with especially strong attention to destructive block safety and testability. The main gaps are around coverage of success criterion 1 end-to-end in the actual Studio browse surface, incomplete validation of destructive-action failure paths, and a few places where the plan assumes existing seams or behavior without pinning them tightly enough in tests. Overall this looks executable, but a couple of omissions could let the phase “pass” while still missing operator-visible behavior.

## Strengths
- Clear requirement-to-plan mapping; each plan has a narrow surface and mostly avoids scope bleed.
- Good wave split: Core enum/resolver first, fake/orchestrator unblock support first, UI wiring after dependencies exist.
- Destructive block path explicitly routes through `IContentMaintenanceOrchestrator.BlockVideoAsync` and forbids direct delete calls.
- Confirmation UX for block is spelled out, including warning copy and focus behavior.
- Plans consistently pin “single source of truth” logic (`VideoStatusResolver`, `PublishStateDeriver`) instead of duplicating conditionals in UI.
- Test intent is concrete and mostly behavior-driven, not just grep/build based.
- Plan 03 correctly calls out the `BlockedVideoListResult.Items` vs `.Videos` correction, which is exactly the kind of brownfield footgun that causes churn later.

## Concerns
- `HIGH`: Success criterion 1 says channel browse must show full pipeline status including `Blocked`, computed from videos store, `content_site_index`, and `blocked_videos`. Plan 04 only adds Approved/Published badge arms and block action; it does not clearly add or test a browse scenario where a blocked video returned from the channel lister renders as `Blocked` on initial browse. That end-to-end browse-state proof is missing.
- `HIGH`: Destructive block failure handling is under-specified. Plan 04 says refresh badge on success, but does not explicitly state what happens when `BlockVideoAsync` returns `Success = false` without throwing. If the orchestrator uses result-based failures, the UI may silently reset confirmation and leave the operator with no error.
- `MEDIUM`: Wrong-id safety is only partially pinned. The plan checks that clicked row id reaches `BlockVideoAsync`, but does not verify the post-success `RefreshBadgesAsync(new[]{ vm.VideoId })` refreshes only that row and doesn’t accidentally disturb selection or other row state.
- `MEDIUM`: Plan 02 covers Review with a per-row badge but Publish only with summary counts. Success criterion 6 says Review and Publish pages show the derived publish-state next to each entry. A summary-only Publish surface may not satisfy that literally unless prior product intent already narrowed Publish to aggregate-only.
- `MEDIUM`: Plan 03’s unblock behavior removes the row in-memory without reload, but there is no test pinning subsequent re-browse behavior from success criterion 5 (`Not harvested` on next channel browse). That may be acceptable cross-plan, but currently no plan appears to close that loop.
- `MEDIUM`: Plan 04 relies on existing BROWSE-01/03 behavior and adds a regression test for multi-select harvest, but it does not clearly verify the channel browse list still loads correctly after the Actions column and block confirmation UI are inserted. Brownfield Razor changes often break table markup or event flow.
- `LOW`: Plan 01 says “touch only inserted lines” for enum edit, then also asks for XML docs that may require surrounding edits; that’s minor but internally inconsistent.
- `LOW`: Plan 02 adds a `// Why:` comment in DI registration. That’s harmless, but it’s noise in a brownfield composition root and not worth prescribing unless the file already uses that convention.
- `LOW`: Plan 04 introduces inline `style="width:80px"` despite project constraints mentioning no hand-rolled `style=`. Even if that constraint was mainly about visual styling, this is a plan-level mismatch and avoidable.

## Suggestions
- Add one explicit Harvest-page test that browses a channel containing a blocked video and asserts the row initially renders `Blocked` without any operator action.
- In Plan 04, specify UI behavior for `BlockVideoAsync` returning `Success = false`: show operator-safe error text, do not refresh badge, and keep or reset confirmation deliberately.
- Clarify whether Publish.razor summary-only output satisfies success criterion 6. If not, amend Plan 02 to add per-entry publish-state there too, or narrow the criterion wording now.
- Add a regression test that after adding the Actions column and confirm flow, channel browse still renders all expected rows and “Harvest Selected” still works with row actions present.
- Add a test for unblock followed by a fresh status resolution path, even if implemented in Harvest tests rather than Blocked page tests.
- Remove the inline `style=` from Plan 04 and express width via existing Bootstrap utilities or no fixed width at all.

## Overall Risk
`MEDIUM` — the plans are disciplined and likely implementable, but there are two real delivery risks: the phase may miss an end-to-end proof of blocked-status browse behavior, and the destructive block path is not fully specified for non-exception failure outcomes. Those are fixable before execution.

---

## Consensus Summary

Single reviewer (Codex). Internal gsd-plan-checker passed 0 issues; Codex applied an adversarial
goal-backward pass and surfaced real coverage/edge-case gaps the checker missed.

### Agreed Strengths
- Clean requirement→plan mapping, narrow per-plan surface, correct wave split.
- Destructive Block routed through `IContentMaintenanceOrchestrator.BlockVideoAsync` (never direct store delete), with confirmation UX + warning copy + focus.
- Single-source-of-truth domain logic (`VideoStatusResolver`, `PublishStateDeriver`) — no duplicated UI conditionals.
- `BlockedVideoListResult.Items` brownfield correction caught in 56-03.

### Agreed Concerns (priority order)
1. **[HIGH — CONFIRMED] Block failure path (SC4).** `BlockVideoAsync` returns `ContentMaintenanceResult { bool Success }` (verified in `DeckFlow.Core/Orchestration/ContentMaintenanceResult.cs`). Plan 04 only refreshes the badge on success and handles thrown exceptions; it does NOT specify behavior when `Success == false` returns without throwing → operator could get a silent no-op. Plan 04 must add: on `Success == false`, show operator-safe error, do NOT refresh badge, handle confirmation state deliberately. Add a `[Fact]` for the result-false path.
2. **[HIGH — CONFIRMED, low-cost] Browse-time Blocked badge (SC1).** Resolver returns `Blocked` (56-01 tests Blocked-wins) and Harvest has a Blocked arm, but no test proves a blocked video from the channel lister renders `Blocked` on initial browse. Add one Harvest [Fact]: channel containing a blocked video → row renders `Blocked` with no operator action.
3. **[MEDIUM] Unblock → re-browse loop (SC5).** No plan pins that an unblocked video resolves to `Not harvested` on next browse. Add a resolver/Harvest test closing the loop (in 56-01 or 56-04).
4. **[MEDIUM] Post-block row isolation.** Verify `RefreshBadgesAsync(new[]{ vm.VideoId })` refreshes only that row and does not disturb selection/other rows.
5. **[MEDIUM] Brownfield table integrity (BROWSE-01/03).** No test that the channel list still loads + "Harvest Selected" still works after inserting the Actions column + confirm UI.
6. **[LOW — CONFIRMED] Inline `style="width:80px"` in Plan 04** violates the "no hand-rolled `style=`" constraint. Replace with a Bootstrap utility or drop the fixed width.
7. **[LOW] Plan 01 "touch only inserted lines" vs XML-doc edits** — minor internal inconsistency.
8. **[LOW] Plan 02 `// Why:` comment in DI registration** — noise unless file already uses the convention.

### Divergent / Resolved-by-spec
- **Codex MEDIUM: Publish per-entry vs summary (SC6).** Largely a FALSE POSITIVE — 56-UI-SPEC §Publish.razor (line 360+) deliberately narrows Publish.razor to a summary indicator because it has no per-entry table; Plan 02 follows the locked spec. Note: UI-SPEC line 328 ("identical new Publish State column") contradicts the detailed §Publish summary-only decision — worth a one-line UI-SPEC reconciliation but not a plan defect.

### Overall Risk: MEDIUM
2 HIGH findings block execution per project workflow. Both are fixable with targeted plan edits (failure-path spec + 2-3 tests + style= removal) — no plan restructuring needed.

---

## Codex Re-Review (gpt-5.4, medium effort) — confirmation pass after --reviews replan (717b46f)

1. Prior findings
- HIGH-1 (SC4): `CLOSED` — [56-04 Task 1] explicitly requires `ConfirmBlockAsync` to branch on `result.Success`, skip `RefreshBadgesAsync` on `false`, surface error copy, and Task 3 pins it with `HarvestPage_ConfirmBlock_ResultFailure_ShowsErrorAndLeavesBadgeUnchanged`.
- HIGH-2 (SC1): `CLOSED` — [56-04 Task 3] adds `HarvestPage_ChannelBrowse_BlockedVideoRendersBlockedBadge`, and Task 1 explicitly preserves the existing `VideoStatus.Blocked` render arm for initial browse.

2. New HIGH issues
- none

3. Overall
- `READY TO EXECUTE` — both prior HIGH gaps are now concretely specified and test-pinned, with no new HIGH regression evident in the revision.
