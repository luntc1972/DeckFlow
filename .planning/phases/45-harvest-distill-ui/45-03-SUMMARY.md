---
phase: 45-harvest-distill-ui
plan: "03"
subsystem: Studio
tags: [blazor, harvest-ui, progress-bridge, badge-resolution, nav, circuit-safety]
dependency_graph:
  requires: ["45-02"]
  provides: [Harvest.razor, ActionOrchestratorProgress, NavMenu-harvest-entry]
  affects: [DeckFlow.Studio]
tech_stack:
  added: []
  patterns: [background-task-task-run, fire-and-forget-progress-bridge, disposal-safe-invokeasync, per-video-badge-resolve, blazor-page-inject]
key_files:
  created:
    - DeckFlow.Studio/Pages/Harvest.razor
    - DeckFlow.Studio/Services/ActionOrchestratorProgress.cs
  modified:
    - DeckFlow.Studio/Shared/NavMenu.razor
decisions:
  - "Progress sink wraps InvokeAsync around BOTH _logLines.Add and StateHasChanged so list mutation and render are atomic on the Blazor sync context (T-45-18)"
  - "ObjectDisposedException and InvalidOperationException swallowed inside InvokeAsync body to silence late callbacks after circuit drop without unobserved exception"
  - "IDistillOrchestrator injected now (distill call not made) to keep the inject block stable for Wave 4 insertion without another file-modify round"
  - "GetByIdsAsync catch ArgumentException surfaces user-readable error per T-45-06 input validation threat"
  - "Badge resolution for paste queue maps Harvested/Distilled -> Duplicate (pre-harvest warning, not auto-exclusion per HARV-02)"
  - "BrowseChannel and AddToQueue use per-call local CTS (not shared _cts) so cancel-on-dispose only affects the long-running HarvestAsync"
metrics:
  duration: "~30 minutes"
  completed: "2026-06-15"
  tasks_completed: 2
  files_changed: 3
---

# Phase 45 Plan 03: Harvest Page — Channel Browse + Paste Queue + Badges + Harvest Trigger

**One-liner:** Single-file Blazor Harvest.razor with channel browse, paste queue, per-video status badges via VideoStatusResolver, non-blocking HarvestAsync wrapped in Task.Run with InvokeAsync-marshalled/disposal-safe progress sink and CTS-on-Dispose cancellation, plus ActionOrchestratorProgress bridge and NavMenu Harvest entry.

## What Was Built

### Task 1: ActionOrchestratorProgress bridge + NavMenu Harvest entry

**`DeckFlow.Studio/Services/ActionOrchestratorProgress.cs`** — `internal sealed class ActionOrchestratorProgress : IOrchestratorProgress`. Constructor takes `Func<string, Task> sink` (guarded with `ArgumentNullException.ThrowIfNull`). `Report(string message)` fire-and-forgets `_ = _sink(message)` — synchronous by contract, cannot await. XML doc explains this is intentional (Phase 42 D-08 async-reordering prevention).

**`DeckFlow.Studio/Shared/NavMenu.razor`** — added a second `<div class="nav-item px-3">` directly below the Home entry, containing `<NavLink class="nav-link" href="harvest">` with `<span class="oi oi-cloud-download" aria-hidden="true"></span> Harvest`. No `Match="NavLinkMatch.All"` (root-only semantics). Existing Home entry unchanged.

### Task 2: Harvest.razor — channel browse, paste queue, badges, harvest trigger

**`DeckFlow.Studio/Pages/Harvest.razor`** — 654-line single-file Blazor component.

**Section 1 — Channel Browse (HARV-01):**
- Text input with `@@SomeCreator`-placeholder (Razor-escaped), numeric input for count (default 25, D-04), "Browse Channel" `btn-outline-primary` disabled while `_operationInFlight`.
- On click: `Task.Run(() => Lister.ListRecentAsync(channelInput, browseLimit, browseCts.Token))` (Pattern 2 — off the Blazor sync context, AngleSharp concurrency guard inside lister).
- For each returned video, calls `VideoStatusResolver.ResolveStatusAsync` at list-build time.
- Table: Checkbox | Thumbnail (40×30, `https://img.youtube.com/vi/{VideoId}/default.jpg`, `onerror` hide) | Title | Published | Badge.
- `table-secondary` row tinting for Harvested/Distilled rows (D-04).
- Empty-result and error copy per UI-SPEC Copywriting Contract.
- "Harvest or distill in progress — channel browse unavailable" alert when `_operationInFlight`.

**Section 2 — URL/ID Paste Queue (HARV-02):**
- Textarea, "Add to Queue" `btn-outline-secondary`, `Task.Run(() => Lister.GetByIdsAsync(rawLines, ct))`.
- `ArgumentException` from unparseable input caught and surfaced as user error (T-45-06).
- Dedup check per video ID before appending to queue.
- Badge for already-in-DB: maps Harvested/Distilled status → `VideoStatus.Duplicate` (`Already in DB` badge — warning, not auto-exclusion per HARV-02).
- Per-row remove button: `btn-outline-danger btn-sm` with `oi oi-x` and `aria-label="Remove video from queue"` (accessibility).

**Status badges (HARV-03):** `RenderBadge(VideoStatus)` switch expression maps all five `VideoStatus` values to the exact Bootstrap badge class + label from the UI-SPEC vocabulary table. Label text always accompanies color.

**Section 3 — Harvest trigger (HARV-04):**
- Selection summary with already-harvested count; `text-warning` inline note when already-harvested videos are selected.
- "Harvest Selected" `btn-primary` disabled when 0 selected or `_operationInFlight`.
- **Background-task pattern (Pattern 1 verbatim):** `new _cts = new CancellationTokenSource()`, set `_operationInFlight = true`, clear `_logLines`, build `ActionOrchestratorProgress` with disposal-safe sink, `await Task.Run(() => HarvestOrchestrator.HarvestAsync(..., _cts.Token))`.
- **Disposal-safe progress sink (T-45-18):** `new ActionOrchestratorProgress(msg => InvokeAsync(() => { try { _logLines.Add(msg); StateHasChanged(); } catch (ObjectDisposedException) { } catch (InvalidOperationException) { } }))` — both mutation and render inside InvokeAsync; swallows disposal-race exceptions.
- `catch (OperationCanceledException)` appends "Harvest cancelled." to log.
- `finally` sets `_operationInFlight = false` + `await InvokeAsync(StateHasChanged)`.
- Completion: `oi oi-check text-success` + Captions/Whisper/SkippedNoCaptions summary; re-resolves badges for harvested video IDs.
- Failure: `oi oi-warning text-danger` + `HarvestResult.Message`.
- Scrollable log `<pre role="log" aria-live="polite">`.
- Cancel button "Cancel Harvest" `btn-outline-danger btn-sm` calls `_cts.Cancel()`.

**Section 4 — Distill placeholder:** Card titled "Distill" with "Distill controls added next (Wave 4 / Plan 04)" note. `IDistillOrchestrator`, `StudioDistillConfig`, `SessionCapOverride`, `ILlmSpendLedger` all injected but not called.

**`IDisposable.Dispose()`:** `_cts?.Cancel(); _cts?.Dispose();` — circuit drop cancels in-flight harvest (D-06).

**`_operationInFlight`:** Single shared lock for browse (guarded separately with `_isBrowsingChannel`), add-to-queue (guarded with `_isAddingToQueue`), and harvest. No `Task.WhenAll` over lister (Pitfall 2 — AngleSharp single-threaded).

## Human-Verify Checkpoint (Task 3): PENDING

Task 3 is a `type="checkpoint:human-verify" gate="blocking"` checkpoint. It has NOT been executed — the following browser smoke verifications are **pending operator approval**.

**What must be verified manually:**
1. Studio starts, "Harvest" nav entry (cloud-download icon) appears below Home, routes to /harvest.
2. Channel browse: paste handle/URL, click "Browse Channel" → table with thumbnail, title, published, badge; harvested rows tinted.
3. Paste queue: paste video URLs/IDs → rows with badges; already-in-DB shows "Already in DB" badge; remove button works; invalid input shows user error.
4. Harvest: select ≥2 videos, click "Harvest Selected" → log streams live, tab stays responsive; summary shows counts; badges update.
5. Cancel-on-dispose: start harvest, close tab → Studio console shows cancellation, NO unobserved ObjectDisposedException.
6. No secrets/connection strings in page or Studio logs.

**Plan is NOT marked complete in ROADMAP.md.** Orchestrator finalizes tracking after the operator signals approval.

## Deviations from Plan

None — plan executed exactly as written.

**Deviation note — `@using DeckFlow.Core.Integration` added:** The plan interfaces list (`IYouTubeChannelVideoLister`, `YouTubeChannelVideo`) live in `DeckFlow.Core.Integration`. The plan's `@using` block listed `DeckFlow.Core.Content` and `DeckFlow.Core.Orchestration` but not `Integration`; the extra directive was required to resolve the type. This is a Rule 3 auto-fix (blocking compile error), not an architectural change.

## Grep Gate Results

| Gate | Expected | Actual | Pass? |
|------|----------|--------|-------|
| `@page "/harvest"` | 1 | 1 | Yes |
| `@implements IDisposable` | 1 | 1 | Yes |
| `@using DeckFlow.Core.Content` | 1 | 1 | Yes |
| `ListRecentAsync` | ≥1 | 1 | Yes |
| `GetByIdsAsync` | ≥1 | 2 (call + comment) | Yes |
| `HarvestAsync` | ≥1 | 2 (call + comment) | Yes |
| `ResolveStatusAsync` | ≥1 | 3 | Yes |
| `Task.Run` | ≥2 | 6 | Yes |
| `InvokeAsync` | ≥2 | 3 | Yes |
| `_cts` | ≥3 | 7 | Yes |
| `img.youtube.com/vi/` | ≥1 | 2 | Yes |
| `role="log"` | 1 | 1 | Yes |
| `aria-live="polite"` | 1 | 1 | Yes |
| `aria-label="Remove video from queue"` | 1 | 1 | Yes |
| `Task.WhenAll` | 0 | 0 | Yes |
| `ObjectDisposedException` swallow | 1 | 1 | Yes |
| NavMenu `href="harvest"` | 1 | 1 | Yes |
| NavMenu `oi-cloud-download` | 1 | 1 | Yes |
| NavMenu `NavLinkMatch.All` retained | 1 | 1 | Yes |
| ActionOrchestratorProgress sealed class | 1 | 1 | Yes |
| `_ = _sink(message);` fire-and-forget | 1 | 1 | Yes |

## Build Result

`dotnet build DeckFlow.sln` — Build succeeded. 0 errors, 0 new warnings.

## Known Stubs

- Section 4 (Distill) is a deliberate placeholder card per plan spec. `IDistillOrchestrator`, `StudioDistillConfig`, `SessionCapOverride`, `ILlmSpendLedger` are injected but not called. Plan 04 (Wave 4) wires this section. This does not prevent the plan's goal (Harvest workflow, HARV-01..04) from being achieved.

## Threat Flags

No new network endpoints, auth paths, or schema changes introduced by this plan.

T-45-06 (paste queue input validation): `ArgumentException` from `GetByIdsAsync` caught and surfaced as user error. Implemented.
T-45-07 (SSRF via channel URL): All outbound HTTP through YoutubeExplode (trusted lib). Accepted.
T-45-08 (circuit blocking / AngleSharp): `Task.Run` on all lister/orchestrator calls; single `_operationInFlight`; no `Task.WhenAll`. Implemented.
T-45-18 (post-Dispose progress callback): `InvokeAsync` body catches `ObjectDisposedException` + `InvalidOperationException`. Implemented.
T-45-09 (info disclosure): Page renders only video metadata and progress text — no connection string, provider value, or ledger key. Verified by code review (pending human smoke at step 7).

## Self-Check: PASSED

- `DeckFlow.Studio/Pages/Harvest.razor` — FOUND
- `DeckFlow.Studio/Services/ActionOrchestratorProgress.cs` — FOUND
- `DeckFlow.Studio/Shared/NavMenu.razor` — MODIFIED (Harvest entry added)
- Commit 778e1ce (Task 1) — FOUND
- Commit 21c70c3 (Task 2) — FOUND
- Build: succeeded 0 errors 0 warnings
