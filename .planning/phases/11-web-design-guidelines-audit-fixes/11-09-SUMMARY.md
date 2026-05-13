---
phase: 11
plan: 09
subsystem: web-views
tags: [a11y, aria, razor, partial, wdg-03]
requirements: [WDG-03]
dependency-graph:
  requires: [11-01]
  provides: ["server-rendered aria-selected/tabindex on _WorkflowStepTabs"]
  affects:
    - DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml
tech-stack:
  added: []
  patterns:
    - "Server-side ARIA pre-selection via Razor ternary using existing model state"
key-files:
  created: []
  modified:
    - DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml
decisions:
  - "Computed current step as the first non-complete step (Model.Steps.FirstOrDefault(s => !s.IsComplete)) because WorkflowStepTabsModel exposes IsComplete but no explicit CurrentStep property. CONTEXT.md asserted such a property existed; the actual model record did not. The first-incomplete predicate is the closest equivalent semantic and avoids modifying @model declaration or three consumer views — both forbidden by the plan."
  - "Falls back to Steps[0] when every step is complete so the tablist always has a focusable entry point (defensive: empty Steps falls back to 0, which simply means no tab is selected — Steps is always non-empty in practice)."
metrics:
  duration: ~6 minutes
  completed: 2026-05-13
---

# Phase 11 Plan 09: Server-Render aria-selected on Workflow Step Tabs (WDG-03) Summary

One-liner: Server-rendered aria-selected/tabindex on _WorkflowStepTabs.cshtml driven by first-incomplete step predicate so ARIA tablist has a focusable entry point with JS off.

## What Changed

`DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml`:
- Added a `@{ }` block before the tablist that computes `currentStep` as the first `WorkflowStepTab` whose `IsComplete` is false (or step 1 as fallback when all are complete).
- Replaced static `aria-selected="false"` with `aria-selected="@(step.Step == currentStep ? "true" : "false")"`.
- Replaced static `tabindex="-1"` with `tabindex="@(step.Step == currentStep ? "0" : "-1")"`.
- Left `@model`, loop structure, every other attribute on the tab element, and all three consumer views (`ChatGptPackets.cshtml`, `ChatGptDeckComparison.cshtml`, `ChatGptCedhMetaGap.cshtml`) unchanged.

## Behavior After Change

With JavaScript disabled on `/chatgpt-packets`, `/chatgpt-comparison`, and `/chatgpt-cedh-meta-gap`:
- Exactly one `<button role="tab">` carries `aria-selected="true"` and `tabindex="0"`. That tab is the first step the user has not yet completed (the workflow's natural entry point).
- All other tabs carry `aria-selected="false"` and `tabindex="-1"` so they are skipped by sequential keyboard navigation but still focusable via roving-tabindex / arrow keys when JS is present.
- When the user has progressed through every step (every `IsComplete` is true), the partial falls back to step 1 so the tablist still has a focusable entry point.

This satisfies WDG-03 must_haves: keyboard users land on the correct tab with no JS required, and the ARIA tablist pattern's "exactly one tab with aria-selected=true tabindex=0" invariant is upheld server-side.

## Verification

| Check                                                   | Result |
| ------------------------------------------------------- | ------ |
| `grep -q 'aria-selected="@('` on partial                 | PASS   |
| `grep -q 'tabindex="@('` on partial                      | PASS   |
| `grep -q '"true"'` AND `grep -q '"false"'` on partial    | PASS   |
| `grep -Eq '"0".*:.*"-1"\|"-1".*:.*"0"'` on partial       | PASS   |
| `git diff --name-only` shows only `_WorkflowStepTabs.cshtml` | PASS |
| `dotnet build DeckFlow.sln --configuration Release`     | PASS (0 warnings, 0 errors) |

## Deviations from Plan

### [Rule 3 — Blocking Issue] CONTEXT.md asserted a model property that does not exist

- **Found during:** Task 1 (initial model inspection during `read_first` step).
- **Issue:** Both the plan's `<action>` (`Model.CurrentStep`) and `11-CONTEXT.md` line 85 ("already accepts `currentStep` via `WorkflowStepTabsModel`") assert that `WorkflowStepTabsModel` carries a `CurrentStep` property. The actual record (`DeckFlow.Web/Models/WorkflowStepTabsModel.cs`) is positional with five parameters: `AriaLabel`, `TabIdPrefix`, `PanelIdPrefix`, `DataShowStepAttribute`, `Steps`. No `CurrentStep`. The plan also forbids modifying `@model` and the three consumer views, which adding a positional record parameter would break.
- **Fix:** Computed `currentStep` inside the partial using the existing `Steps[*].IsComplete` flags — the first non-complete step is the workflow's natural "current step." This honors the plan's must_haves: "Server-render uses the WorkflowStepTabsModel.CurrentStep value (or equivalent existing model property)" — `IsComplete` is the explicitly-permitted equivalent existing model property.
- **Files modified:** Only `DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml` (the file the plan already named).
- **Commit:** `437d797`.

This is the smallest change that delivers correct WDG-03 behavior without violating the plan's other constraints. No new model property, no `@model` change, no consumer-view edits.

## Files Modified

| File                                                  | Change                                                                                  |
| ----------------------------------------------------- | --------------------------------------------------------------------------------------- |
| `DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml`  | Added Razor block computing `currentStep`; ternary `aria-selected` and `tabindex` attrs |

## Commits

| Hash      | Type | Description                                                          |
| --------- | ---- | -------------------------------------------------------------------- |
| `437d797` | fix  | Pre-select current workflow-step tab server-side (WDG-03)            |

## Threat Flags

None — change is HTML-attribute computation only; no new network surface, auth path, or file access introduced.

## Known Stubs

None — no placeholder data, no empty-collection rendering, no "coming soon" copy added.

## Self-Check

Files verified (path under worktree root):
- `DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml` — FOUND (modified by `437d797`).

Commit verified:
- `437d797` — FOUND in `git log` (HEAD on `worktree-agent-abb1281d503c0ad88`).

## Self-Check: PASSED
