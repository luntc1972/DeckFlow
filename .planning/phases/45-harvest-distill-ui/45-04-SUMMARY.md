---
phase: 45-harvest-distill-ui
plan: "04"
subsystem: Studio
tags: [blazor, distill-ui, spend-gate, redistill, cap-enforcement, harv-05]
dependency_graph:
  requires: ["45-03", "45-01"]
  provides: [Harvest.razor-distill-section, spend-gate-ui, redistill-confirm-guard]
  affects: [DeckFlow.Studio]
tech_stack:
  added: []
  patterns: [two-stage-dry-run-confirm, redistill-double-confirm, session-cap-override, reuse-task-run-progress-sink]
key_files:
  created: []
  modified:
    - DeckFlow.Studio/Pages/Harvest.razor
decisions:
  - "Both Stage A and Stage B pass redistill: redistillConfirmed by name (HIGH-4) — same bool gate controls distillIds membership and the redistill: argument, preventing any split between which videos are passed and what flag they're processed under"
  - "distillIds built from harvested-only by default; already-distilled videos added only when redistillConfirmed is true — this keeps the dry-run accurate and prevents silent re-distill (T-45-10)"
  - "Stage A and Stage B share _distillLogLines to keep the UX simple — Stage B clears on start so the user sees live progress, not appended dry-run noise"
  - "Cap-exceeded block uses !DistillConfig.IsSubscriptionProvider — subscription providers have $0 marginal cost so no cap block; metered providers must respect the cap"
  - "RaiseCapAsync validates input with decimal.TryParse + non-negative check before writing OverrideUsd; ignores invalid input silently per V5 (no throw, no user-visible error for blank/partial entry)"
  - "OnInitializedAsync calls RefreshCapDisplayAsync so the operator sees current spend numbers immediately on page load before taking any action"
  - "Progress log shows during dry-run in-flight; after dry-run completes the same box is reused for Stage B (cleared on Stage B start) — single pre element, same role=log/aria-live"
metrics:
  duration: "~25 minutes"
  completed: "2026-06-15"
  tasks_completed: 3
  files_changed: 1
---

# Phase 45 Plan 04: Distill Spend Gate — Stage A + Stage B

**One-liner:** Two-stage spend-gated distill flow in Harvest.razor: dry-run projection with re-distill double-confirm guard, monthly cap + remaining display with session override, Stage B reviewed-spend confirm gating live distill, actual spend + failure reporting, and badge refresh — wired end-to-end with redistill:true passed by name only after double-confirm (HIGH-4).

## What Was Built

### Tasks 1 and 2: Stage A dry-run + Stage B live distill (single commit — unified distill section)

**`DeckFlow.Studio/Pages/Harvest.razor`** — Section 4 "Distill Spend Gate (HARV-05)" replacing the Wave 3 placeholder card.

**Cap display (D-02):**
- `OnInitializedAsync` calls `RefreshCapDisplayAsync()` which reads `SpendLedger.GetMonthlyCapUsd()` (synchronous, Wave 1 addition) and `SpendLedger.GetMonthlyTotalAsync(monthKey)` for the current UTC month.
- Renders "Monthly cap: $X.XX | Spent this month: $Y.YY | Remaining: $Z.ZZ" with `text-danger` on remaining when negative.
- Session cap-raise control (D-03): numeric input + "Raise cap" button calls `RaiseCapAsync()` which validates the input with `decimal.TryParse` + non-negative check before writing `CapOverride.OverrideUsd`. Same singleton seen by the orchestrator's `WouldExceedCapAsync` (Pitfall 6 mitigated).

**Re-distill double-confirm (T-45-10 / Pitfall 3):**
- Counts selected videos by `VideoStatus.Harvested` (ready to distill) and `VideoStatus.Distilled` (already distilled).
- When `alreadyDistilledCount > 0`: amber `alert alert-warning` banner with the re-distill copy, followed by two sequential checkboxes:
  1. `_redistillCheck1`: "Re-distill already-distilled videos (additional spend)"
  2. `_redistillCheck2` (revealed only when check 1 is ticked): "Yes, I understand — overwrite existing distill output for K video(s)."
- `redistillConfirmed = _redistillCheck1 && _redistillCheck2` is computed in markup and used as a single gate for:
  - Whether already-distilled videos are added to `distillIds`
  - Whether the dry-run button is enabled
  - The `redistill:` named argument passed to both `DistillAsync` calls

**Stage A dry-run:**
- "Estimate Spend (dry run)" `btn-outline-primary` disabled while `_operationInFlight` or when re-distill guard is incomplete.
- `RunDistillStageAAsync` sets `_operationInFlight = true`, `_distillDryRunInFlight = true`, clears `_distillLogLines`, creates `_cts`, builds the disposal-safe `ActionOrchestratorProgress` sink (same `ObjectDisposedException`/`InvalidOperationException` swallowing pattern as harvest, T-45-18).
- Calls `DistillOrchestrator.DistillAsync(dryRun: true, redistill: redistillConfirmed, videoIds: distillIds, ...)` via `Task.Run` off the Blazor sync context.
- Dry-run result card (`card border-primary`): WouldRun, ProjectedSpendUsd (F4), cap remaining.
- Cap-exceeded block: `alert alert-danger` with the cap-exceeded copy; Stage B hidden until the block clears.
- Refreshes cap display after dry-run.

**Stage B live distill:**
- Confirmation checkbox `_distillSpendConfirmed`: "I have reviewed the estimated spend above and want to proceed with actual distillation." Only visible after a successful dry-run with no cap-exceeded block.
- "Run Distill" `btn-primary` disabled until checkbox checked and `!_operationInFlight`.
- `RunDistillStageBAsync` uses the same `_cts` / `_operationInFlight` lock; passes `dryRun: false` and `redistill: redistillConfirmed` (same gate as Stage A — HIGH-4 end-to-end).
- Result card (`card border-success`): VideosDistilled, LlmCalls, LlmSpendUsd (F4), VideosFiltered, DistillFailed.
- If `DistillFailed > 0`: `alert alert-warning` listing `FailedVideoIds`.
- If `Success == false && AbortedReason != null`: `alert alert-danger` with abort text — no false success card rendered.
- Post-distill badge refresh via `RefreshBadgesAsync` and cap display refresh.

**Single CTS / single lock:** Both stages reuse the existing `_cts` field and `_operationInFlight` bool. No second `CancellationTokenSource` was introduced (verified by grep: `private CancellationTokenSource` appears exactly once).

**Subscription provider indicator:** When `DistillConfig.IsSubscriptionProvider` is true, a `badge bg-info` "$0 Subscription" badge is shown alongside the ready count; metered shows `bg-warning`. Cap enforcement is skipped for subscription providers (cap-exceeded check: `!DistillConfig.IsSubscriptionProvider && ...`).

## Human-Verify Checkpoint (Task 3): PASSED

Task 3 was a `type="checkpoint:human-verify" gate="blocking"` checkpoint. The operator verified the multi-step spend gate, re-distill guard, and cap enforcement in a live Studio run, and typed "approved" on 2026-06-15.

**Verification results (orchestrator-driven deterministic checks + live user dogfood):**

- Cap display (monthly cap / spent this month / remaining): PASS.
- HIGH-1 provider→badge verified LIVE: badge shows "Metered" when `DECKFLOW_LLM_PROVIDER=openai`/unset, "Subscription ($0)" when `=claude` — a single provider decision drives both the badge and the distiller.
- Dry-run button disabled with 0 selected; Stage B hidden before a successful dry-run: PASS.
- Re-distill amber banner absent at rest; session cap-raise input present with revert note: PASS.
- No secrets in page or server logs; 0 `ObjectDisposedException` / 0 unobserved exceptions: PASS.
- LIVE distill (claude CLI, $0) confirmed working end-to-end: videos distilled, badges flip to Distilled, per-video and total timing shown, cancel works. User typed "approved".

**Plan is complete — orchestrator finalized after human approval.**

### Related follow-up quick tasks (context, not part of this plan's scope)

Several follow-on quick tasks shipped during dogfood that made the live flow usable; tracked as their own quick tasks, noted here only as related follow-ups:

- Harvest source auto-ensure (260615-h2v)
- Browse skip/offset (k8o)
- Skip-estimate-on-subscription
- DB-backed pending-distill loader (p4d)
- Clear distill CLI-config error (c9e)
- Per-video + live distill timing (t7m)
- Playlist/queue harvest + per-channel grouping (q3n)

## Deviations from Plan

None — plan executed exactly as written. Tasks 1 and 2 were committed as a single atomic commit because the Stage A dry-run result card (markup) and Stage B methods (code) are interdependent in the same file and both must compile together.

## Grep Gate Results

| Gate | Expected | Actual | Pass? |
|------|----------|--------|-------|
| `dryRun: true` | ≥1 | 1 | Yes |
| `dryRun: false` | ≥1 | 2 | Yes |
| `GetMonthlyCapUsd` | ≥1 | 2 | Yes |
| `GetMonthlyTotalAsync` | ≥1 | 1 | Yes |
| `redistill:` (named arg) | ≥1 | 5 | Yes |
| `OverrideUsd` | ≥1 | 3 | Yes |
| `alert alert-warning` | ≥1 | 2 | Yes |
| `alert alert-danger` | ≥1 | 4 | Yes |
| `card border-primary` | ≥1 | 1 | Yes |
| `card border-success` | ≥1 | 1 | Yes |
| `IsSubscriptionProvider` | ≥1 | 4 | Yes |
| `LlmSpendUsd` | ≥1 | 1 | Yes |
| `VideosDistilled` | ≥1 | 3 | Yes |
| `AbortedReason` | ≥1 | 2 | Yes |
| `FailedVideoIds` | ≥1 | 1 | Yes |
| `private CancellationTokenSource` | 1 | 1 | Yes |
| `private bool _operationInFlight` | 1 | 1 | Yes |
| `_redistillCheck1` (backing field 1) | ≥1 | 6 | Yes |
| `_redistillCheck2` (backing field 2) | ≥1 | 5 | Yes |

## Build Result

`dotnet build DeckFlow.sln` — Build succeeded. 0 errors, 0 new warnings.

## Known Stubs

None — the distill section is fully wired. All injected services (`IDistillOrchestrator`, `StudioDistillConfig`, `SessionCapOverride`, `ILlmSpendLedger`) are called with real arguments.

## Threat Flags

No new network endpoints, auth paths, or schema changes introduced by this plan.

T-45-10 (silent re-distill): `redistillConfirmed` gate enforced at both `distillIds` build and `redistill:` named argument — already-distilled videos are only re-processed after double-confirm. Implemented.
T-45-11 (unbounded spend): Dry-run required before Stage B; Stage B requires reviewed-spend checkbox; cap-exceeded gate blocks Stage B when projected spend exceeds remaining cap. Implemented.
T-45-12 (session cap override): `CapOverride.OverrideUsd` is in-memory app-scoped; input validated non-negative before write; resets on restart. Implemented.
T-45-13 (metered abort shown as success): `AbortedReason` rendered in `alert alert-danger`; success card only renders when `Success || VideosDistilled > 0`. Implemented.
T-45-14 (spend/provider in logs): Page shows cap/spend to operator (intended); no connection strings, provider values, or ledger keys in progress log output. Implemented.

## Self-Check: PASSED

- `DeckFlow.Studio/Pages/Harvest.razor` — FOUND
- Commit 4f3c2df (Tasks 1+2) — FOUND
- Build: succeeded 0 errors 0 warnings
