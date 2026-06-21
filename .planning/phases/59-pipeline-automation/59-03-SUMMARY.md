---
phase: 59-pipeline-automation
plan: 03
subsystem: studio-pipeline-automation
tags: [auto-distill, auto-approve, one-click, harvest-ui, blazor, subscription-only]
requires:
  - "IAutoApproveSignal + ClipCountAutoApproveSignal + DistillResult.DistilledVideos (Plan 01)"
  - "AutoApproveSettings + AutoApproveSettingsStore + Harvest-page Auto-approve panel (Plan 02)"
  - "DeckFlow.Studio Harvest page harvest/distill flow + DI conventions (Cycle 9)"
provides:
  - "One-click 'Harvest + Auto-distill' action (subscription-only inline distill) on Harvest.razor"
  - "Shared ApplyAutoApproveAsync post-distill step (one-click + manual Stage B reuse, D-09)"
  - "Per-video outcome summary card with canonical-sourced counts (D-11)"
affects:
  - "Phase 61 HSEL (harvest selection reshapes the same Harvest surface)"
  - "Phase 62 SUI (presentation polish over the same Harvest surface)"
tech-stack:
  added: []
  patterns:
    - "Subscription-vs-metered gate mirrors Core's hard refusal (ContentKbOrchestrator.cs:244) in the UI"
    - "Harvest-ready ids = ListPendingDistillAsync ∩ selected (never raw selected ids)"
    - "Shared post-distill auto-approve method callable from both the one-click and manual paths"
    - "Extracted RunHarvestCoreAsync + BuildHarvestProgress shared by both harvest buttons (DRY)"
key-files:
  created: []
  modified:
    - DeckFlow.Studio/Pages/Harvest.razor
    - DeckFlow.Studio/Program.cs
    - DeckFlow.Studio.Tests/HarvestPageTests.cs
    - README.md
decisions:
  - "One-click inline distill is SUBSCRIPTION-ONLY (D-08 AMENDED); metered harvests then stops with a requires-subscription message — no DistillAsync, no silent spend (SC4)"
  - "Auto-approve is a SHARED post-distill step (ApplyAutoApproveAsync) so one-click AND manual Stage B both benefit; metered live distill auto-approve is DEFERRED (Core refuses) (D-09)"
  - "Distill input = harvest-ready ids (ListPendingDistillAsync ∩ selected), excluding skipped/no-caption/already-distilled (D-10, HIGH #2)"
  - "Outcome card counts are canonical-sourced: N=harvestReadyIds.Count, M=VideosDistilled, K=ApplyAutoApproveAsync return, L=M−K, D=VideosFiltered, F/ids=DistillFailed/FailedVideoIds (D-11, HIGH #3)"
  - "ApplyAutoApproveAsync only mutates approval_status (publish stays a separate gate, T-59-06)"
metrics:
  duration: ~50m
  completed: 2026-06-20
  tasks: 2 of 3 (Task 3 = operator human-verify checkpoint, PENDING)
  files: 4
---

# Phase 59 Plan 03: One-Click Harvest → Auto-Distill → Auto-Approve Summary

Delivered the phase headline (AUTO-01): a single Studio "Harvest + Auto-distill" action that, on a
subscription ($0) provider, harvests the selected videos and then distills exactly the harvest-ready
ones inline — no separate Distill click — applying the persisted auto-approve settings (AUTO-02) to
flip high-clip distills to `approved`, and surfacing a single per-video outcome summary card. Metered
providers harvest but do not live-distill (Core refuses); the manual Distill section stays as a
fallback and shares the same auto-approve step.

## What Was Built

### Task 1 — One-click flow + DI + recording fakes (commits 7a5cbd95 RED, e9ae01c9 GREEN)
- **Program.cs:** registered `IAutoApproveSignal -> ClipCountAutoApproveSignal` singleton beside the
  content-KB singletons.
- **Harvest.razor:** injected `IAutoApproveSignal` + `IContentSiteIndexStore`. Added a default
  **"Harvest + Auto-distill"** button beside the original "Harvest Selected" (kept as the D-12
  fallback). New handler `HarvestAndAutoDistillAsync`:
  1. **D-08 metered gate (AMENDED):** always harvests; if `!DistillConfig.IsSubscriptionProvider`,
     surfaces "Live distill requires a subscription provider…" and STOPS before distill — no
     `DistillAsync`, no `SetApprovalStatusAsync`, no silent spend (mirrors Core's refusal at
     ContentKbOrchestrator.cs:244; SC4).
  2. **Subscription path:** runs the harvest body, then `ListPendingDistillAsync` ∩ the just-selected
     ids = `harvestReadyIds` (HIGH #2 / D-10 — excludes skipped/no-caption/already-distilled). Empty →
     outcome card with harvested/0-distillable, no distill call.
  3. Inline `DistillAsync(dryRun:false, isSubscriptionProvider:true, redistill:false,
     videoIds:harvestReadyIds)` off the Blazor sync context with the disposal-safe progress sink.
  4. Shared `ApplyAutoApproveAsync` post-distill, then badge/cap refresh.
- **`ApplyAutoApproveAsync(DistillResult)` (D-09 shared step):** when auto-approve is enabled, selects
  `DistilledVideos` where `AutoApproveSignal.ShouldAutoApprove(clipCount, cutoff)`, batch-flips them via
  `IndexStore.SetApprovalStatusAsync(keys, "approved", ct)` (only approval_status mutated, T-59-06),
  returns the count flipped (0 when disabled / none qualify).
- **Refactor:** extracted `RunHarvestCoreAsync` + `BuildHarvestProgress` from `HarvestSelectedAsync`
  so both harvest buttons share one harvest body (no duplication).
- **Tests (Codex MEDIUM):** replaced the throwing stubs with recording/configurable fakes —
  `RecordingDistillOrchestrator` (records distill `videoIds`, returns a configured `DistillResult`,
  `ListPendingDistillAsync` returns a configured pending set) and a recording batch
  `SetApprovalStatusAsync` on `MapSiteIndexStore`. Added 8 bUnit cases: subscription inline chain
  (SC1), mixed-batch harvest-ready-only distill input (HIGH #2), ≥cutoff approve / below-cutoff hold
  (SC2), OFF=no-approve (SC3), metered no-distill/requires-subscription (SC4), continue-on-failure
  (D-10), canonical outcome card (D-11).

### Task 2 — Manual Stage B shared auto-approve + outcome card + README (commit 729b5e4d)
- The per-video **outcome summary card** (one card: harvested / distilled / auto-approved /
  left-in-review / dropped / failed + failed ids) was added in Task 1's markup; every count maps to a
  named canonical source (N=`_outcomeHarvestReadyCount`, M=`VideosDistilled`, K=`ApplyAutoApproveAsync`
  return, L=M−K, D=`VideosFiltered`, F/ids=`DistillFailed`/`FailedVideoIds`) — no count inferred from
  UI badge state (HIGH #3).
- **`RunDistillStageBAsync`** now calls the shared `ApplyAutoApproveAsync` after a successful live
  distill, so a manual-fallback subscription distill auto-approves ≥cutoff videos through the same
  step (D-09 reuse). Metered live distill never reaches it (Core refuses) → metered auto-approve
  DEFERRED, documented in code.
- **bUnit:** `ManualStageB_Subscription_SharedAutoApprove_ApprovesAboveCutoff` (Load harvested →
  select → Run Distill → `SetApprovalStatusAsync('approved')`).
- **README:** new "What's new in Cycle 10" entry documenting the subscription-only one-click default
  path, the Auto-approve panel (on/off + cutoff, default ON/5, approval-only not publish), the
  canonical outcome card, and that metered providers do not one-click live-distill.

### Task 3 — Operator end-to-end verification (CHECKPOINT — PENDING)
`checkpoint:human-verify`, `gate="blocking"`. Operator-only, no code. Requires a running Studio with a
subscription backend + live YouTube harvest, so it cannot be automated. The plan is paused here for
operator sign-off of SC1–SC4, D-07 persistence, harvest-ready-only distill, canonical outcome counts,
and the D-12 manual fallback (see the eight how-to-verify steps in 59-03-PLAN.md, Task 3).

## Deviations from Plan

None affecting product behavior.
- The Task 2 outcome card was implemented in Task 1's commit (it shares the same markup region as the
  one-click button); Task 2's commit then wired the manual Stage B reuse + README. The acceptance
  criteria are satisfied across the two commits.

## Deferred Issues (out of scope)
- **DEF-59-01 (carried from Plan 01):** the pre-existing `DeckFlow.Web.Tests` build break (uncommitted
  card_text/Manabase working-tree changes) makes `DeckFlow.sln` fail to build. Unrelated to this plan —
  `DeckFlow.Studio` does not reference `DeckFlow.Web`. Not touched per the dirty-tree warning.

## Verification

- `DeckFlow.Studio.Tests` build: **Build succeeded, 0 errors**. `DeckFlow.Core` build: **Build
  succeeded, 0 errors**. Built the affected projects directly (NOT `DeckFlow.sln`) per the dirty-tree
  warning.
- `HarvestPageTests`: **18 passed, 0 failed** (9 prior + 8 one-click + 1 manual-Stage-B), run via the
  Windows dotnet. Full Studio.Tests suite ran 68 deterministically green on re-run; one transient
  bUnit `GetRequiredEventBindingEntry` render-timing flake appeared once and did not reproduce (known
  bUnit/WSL flakiness, not a regression — the targeted Harvest tests are deterministic).
- Acceptance greps (Harvest.razor): `Auto-distill`=3 (≥1), `ListPendingDistillAsync`=5 (≥1),
  `ApplyAutoApprove`=4 (≥2 — inline + Stage B), `Harvest Selected` still present (D-12),
  `SetApprovalStatusAsync`=1 (in the shared step).
- Changed-lines format gate: clean on all changed lines (one off-hunk IDE0161 on a pre-existing
  test-file line, ignored by the gate as designed; `.razor` is not C#-format-gated).
- No file deletions in any commit. Only this plan's files staged; the pre-existing
  card_text/Manabase working-tree changes and the `DeckFlow.sln` break were not touched.

## Success Criteria

- [x] One action harvests → distills (subscription) → review-ready entry (SC1, AUTO-01)
- [x] ≥cutoff auto-approves, below-cutoff holds (SC2, AUTO-02)
- [x] Auto-approve OFF → everything enters the review queue (SC3)
- [x] Metered does NOT one-click live-distill (requires-subscription message); provider/model unchanged (SC4)
- [x] Distill input = harvest-ready ids only (D-10); canonical per-video outcome card (D-11); manual fallback intact + shares auto-approve (D-09, D-12)
- [ ] **Operator end-to-end verification (Task 3 checkpoint) — PENDING human sign-off**

## Self-Check: PASSED

All modified files exist; all three task commits (7a5cbd95 RED, e9ae01c9 GREEN, 729b5e4d) are in the
git history. (Code Tasks 1–2 complete; Task 3 is the operator human-verify checkpoint, pending.)
