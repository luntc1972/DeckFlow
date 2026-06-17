# Phase 45: Harvest + Distill UI - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-15
**Phase:** 45-harvest-distill-ui
**Areas discussed:** Provider mode source, Spend cap control, Channel browse scope, Session/queue persistence

---

## Provider Mode (isSubscriptionProvider)

| Option | Description | Selected |
|--------|-------------|----------|
| Auto-detect from provider | Composition root sets it from the wired distill backend (claude-CLI = subscription/$0, OpenAI = metered). No operator input. Matches Phase 42 D-05. | ✓ |
| Operator toggle in UI | Checkbox; flexible but operator can misset → wrong spend gate. | |
| Config/env only | Read a DECKFLOW_* env var at startup; invisible in UI. | |

**User's choice:** Auto-detect from provider.
**Notes:** Current backend is the claude CLI ($0). Resolved host-side; page reads the resolved flag.

---

## Spend Cap Control

| Option | Description | Selected |
|--------|-------------|----------|
| Read-only display | Show cap + remaining; cap edited via config/env only. | |
| Editable from page | Operator can raise cap inline. | ✓ |
| No cap UI | Drop "remaining"; show only dry-run projection. | |

**User's choice:** Editable from page.
**Follow-up (Cap store):** Cap today is read from `DECKFLOW_LLM_MONTHLY_CAP_USD` (default $15) via
a per-check resolver; ledger exposes only `WouldExceedCapAsync`. "Editable" needs a writable cap
source + a read surface for cap/remaining.

| Cap-store option | Description | Selected |
|------------------|-------------|----------|
| Session/runtime override | In-memory override fed to the resolver; resets to env/default on restart; no new store. | ✓ |
| Persisted writable setting | New settings store surviving restart; permanently relaxes the guard. | |
| Back to read-only display | Edit via config/env only. | |

**User's choice:** Session/runtime override.
**Notes:** Cost-safety — raised cap auto-reverts to $15 on Studio restart. Ledger read surface
(cap + month-total) still needs adding regardless.

---

## Channel Browse Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Operator-set count, default 25 | Numeric input feeds the orchestrator limit param; already-harvested rows tinted. | ✓ |
| Fixed recent-N | Hardcode e.g. 50, no input. | |
| All channel videos | No limit; risky for large channels given serialized lister. | |

**User's choice:** Operator-set count, default 25.
**Notes:** Lister runs serialized (SemaphoreSlim(1), Pitfall 6) — large counts slow; default modest.

---

## Session / Queue Persistence

| Option | Description | Selected |
|--------|-------------|----------|
| In-memory only | Component state; refresh/circuit-drop clears the view; simplest. | ✓ |
| Persist draft | Save queue/selection to DB or localStorage; more code + new surface. | |

**User's choice:** In-memory only.
**Notes:** Per UI-SPEC Pitfall 7 (Dispose-cancels), a circuit drop also cancels an in-flight run.
Accepted: partial harvest safe to resume, partial distill spend already in ledger. No resume
affordance this phase.

---

## Claude's Discretion

- HARV-01 channel-video listing path: direct `IYouTubeChannelVideoLister` call vs a thin
  orchestrator list method (planner/research decides).
- HARV-03 status-badge / dedup data path: direct store queries at render time vs a query helper.
- Exact shape of the ledger read surface added for the cap display.
- Progress-sink → `InvokeAsync(StateHasChanged)` bridge details + any re-render batching.

## Deferred Ideas

- Persisted writable spend cap (rejected in favor of session override).
- Persisted draft queue/selection (rejected; in-memory only).
- Block/unblock + hard-delete maintenance (Phase 46+).
- Review queue / approve-reject / publish (REVQ-* / Phase 46).
