---
created: 2026-08-04T21:15:00.000Z
title: Recompute Goals and Compare-to-baseline automatically on accept (instead of marking them stale)
area: engine
files:
  - DeckFlow.Web/Views/Deck/CutLab.cshtml
  - DeckFlow.Web/wwwroot/ts/cut-lab.ts
  - DeckFlow.Web/Services/CutLab/
---

## Problem

Cut Lab has two classes of number on one screen and the user has to remember which is which:

- **Live** — cut counts and structural findings update on every accept.
- **Point-in-time** — Goals and the Compare-to-baseline table only refresh when the user presses
  **Recalculate goals** / **Recalculate analysis**.

So the default state after any cut is silently stale numbers in two panels.

## Status: partially mitigated, root cause open

Plan 07-06 Task 4 shipped the *minimum viable* fix — the staleness is now **named** rather than
removed. `CutLab.cshtml` marks both `Current` column headers "(as of your last recalculation)", and
the surrounding prose already stated the behavior. The user can now see which numbers are stale.

They still have to act on it manually. That is the part this ticket carries.

## Why it was deliberately deferred

07-06 is a copy-and-docs plan. Recomputing on accept is an **engine and performance** change, not a
wording change (07-CONTEXT decision D-3), and it was explicitly ruled out of scope there rather than
silently absorbed. Logging it was part of that plan's Task 4.

## The actual question to answer first

This is not simply "call recalculate after accept". The reason it is not already automatic is cost:
the Goals and Compare numbers come from the Monte Carlo simulation path, and an accept is a
high-frequency action. Before implementing, establish:

1. What a full Goals + Compare recompute actually costs at the pool sizes Cut Lab supports, measured
   — not estimated.
2. Whether the existing reduced-`trials` override (added in phase 103-05 for the in-loop delta path)
   makes a per-accept recompute affordable at acceptable fidelity.
3. Whether a debounced or idle-time recompute is preferable to a synchronous one, given accepts
   often come in bursts.

If the measured cost rules it out, the correct outcome is to close this ticket with the measurement
recorded — the staleness markers then stand as the permanent answer, not a stopgap.

## Acceptance

- The per-accept recompute cost is measured and written down, whichever way the decision goes.
- If implemented: Goals and Compare reflect the current working list after an accept, with no manual
  press, and the "(as of your last recalculation)" markers are removed in the same change so the UI
  does not lie in the other direction.
- If implemented: no regression in accept latency beyond a stated budget.

## Context

- Deferred from `.planning/workstreams/cycle21-cut-lab/phases/07-cutlab-workflow-ux/07-06-PLAN.md`
  Task 4, per 07-CONTEXT decision D-3.
