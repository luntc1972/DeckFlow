---
phase: 45-harvest-distill-ui
verified: 2026-06-15T17:55:00Z
status: human_needed
score: 14/14
overrides_applied: 0
human_verification:
  - test: "Re-distill end-to-end: select already-distilled video, confirm amber banner + both checkboxes enable dry-run, then run live distill; confirm the video is ACTUALLY re-processed (prior output cleared and replaced), not silently skipped"
    expected: "After double-confirm + live run, the previously-distilled video has new distill output; badge stays Distilled but with fresh timestamps/content. Studio console shows 'ClearDistillOutputAsync' call (log line or re-distill count > 0)."
    why_human: "The redistill:true end-to-end path through ClearDistillOutputAsync -> DistillVideoAsync requires a live LLM run on an already-distilled video. The unit tests (ContentKbOrchestratorDistillTests) prove the skip-bypass and ClearDistillOutput call in isolation; the human-verify in 45-04 confirmed the UI gate and live distill but the re-distill flow was not exercised because no already-distilled video was available during that smoke run."
  - test: "Cap-exceeded block: set selection so projected spend exceeds remaining; confirm Stage B / Run Distill remains blocked. Raise cap via session override; confirm block clears. Restart Studio; confirm cap reverts to $15.00 (env default), not the raised value."
    expected: "Block shows 'alert alert-danger' with cap-exceeded copy. After cap raise the alert disappears and Stage B becomes available. After Studio restart cap display shows $15.00 (or env value), not the override."
    why_human: "Override persistence (D-03) requires a restart to verify non-persistence. Cannot verify a process restart programmatically."
  - test: "Cancel-on-dispose mid-harvest: start a long harvest, close the browser tab (circuit drop) mid-run; confirm Studio server console shows zero ObjectDisposedException and zero unobserved exceptions from post-dispose progress callbacks."
    expected: "Studio console clean after tab close. Harvest stops cleanly (OperationCanceledException logged). No unobserved TaskException or ObjectDisposedException in logs."
    why_human: "Disposal-race safety on circuit drop (T-45-18) requires a browser tab close while an operation is in flight. The 45-03 smoke exercised this but was non-conclusive due to the 'no enabled source' caveat; should be re-exercised with a real harvest source enabled. (See 45-03 SUMMARY caveat.)"
---

# Phase 45: Harvest + Distill UI — Verification Report

**Phase Goal:** Deliver a Harvest + Distill UI in DeckFlow Studio (Blazor) that lets the operator browse a YouTube channel, queue videos by URL/ID, see per-video status badges, run a non-blocking harvest with live progress, and drive a two-stage spend-gated distill flow with cap enforcement, session override, and re-distill double-confirm.
**Requirements:** HARV-01 (channel browse), HARV-02 (paste queue), HARV-03 (status badges), HARV-04 (non-blocking harvest + cancel), HARV-05 (distill spend gate)
**Verified:** 2026-06-15T17:55:00Z
**Status:** human_needed — all automated truths VERIFIED; 3 items require human testing (re-distill end-to-end, cap-persist restart, cancel-on-dispose with real source)
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Req | Status | Evidence |
|---|-------|-----|--------|----------|
| 1 | Operator can browse a YouTube channel and see recent videos in a table with thumbnails, titles, published dates | HARV-01 | VERIFIED | `Harvest.razor:800-810` — `Task.Run(() => Lister.ListRecentAsync(...))` off sync context; table at lines ~60-120 renders thumbnail (`img.youtube.com/vi/{VideoId}/default.jpg`), title, published, badge |
| 2 | Operator can paste video URLs/IDs to a queue with per-row remove | HARV-02 | VERIFIED | `Harvest.razor:880-920` — `GetByIdsAsync` in `Task.Run`; per-row remove `btn-outline-danger` with `aria-label="Remove video from queue"` confirmed by grep (1 hit) |
| 3 | Each listed/queued video shows exactly one status badge from the UI-SPEC vocabulary (not-harvested/harvested/distilled/blocked/duplicate) | HARV-03 | VERIFIED | `VideoStatusResolver.ResolveStatusAsync` called at list-build (grep: 3 call sites in Harvest.razor). `VideoStatus.cs` defines all 5 enum members matching UI-SPEC. `RenderBadge` switch in Harvest.razor maps all 5 values to Bootstrap badge + label text |
| 4 | Harvest runs off the Blazor sync context with live progress log and does not freeze the circuit | HARV-04 | VERIFIED | `Task.Run` appears 6+ times in Harvest.razor (grep confirmed); `HarvestOrchestrator.HarvestAsync` at line 1150 inside `Task.Run`; progress log `<pre role="log" aria-live="polite">` at line 308 |
| 5 | CancellationTokenSource disposed on component Dispose, cancelling in-flight op on circuit drop | HARV-04 | VERIFIED | `private CancellationTokenSource? _cts` (line 732); `Dispose()` calls `_cts?.Cancel(); _cts?.Dispose()` — exactly one CTS field confirmed by grep (1 hit) |
| 6 | Progress sink mutates _logLines and calls StateHasChanged only inside InvokeAsync, post-Dispose updates swallowed | HARV-04 | VERIFIED | `Harvest.razor:1093-1110` — `InvokeAsync(() => { try { _logLines.Add(msg); StateHasChanged(); } catch (ObjectDisposedException) { } catch (InvalidOperationException) { } })` (grep confirms ObjectDisposedException swallow at line 1105); same pattern for distill sink at 1265-1274 |
| 7 | A Harvest nav entry links to /harvest | HARV-01 | VERIFIED | `NavMenu.razor:18-19` — `<NavLink class="nav-link" href="harvest">` with `oi-cloud-download` icon; existing Home `NavLinkMatch.All` entry at line 13 unchanged |
| 8 | ILlmSpendLedger exposes GetMonthlyCapUsd() returning the configured monthly cap | HARV-05 | VERIFIED | `ILlmSpendLedger.cs:51` — `decimal GetMonthlyCapUsd();`; `SpendLedgerBase.cs:136` — `public decimal GetMonthlyCapUsd() => ReadMonthlyCapUsd();`; `ReadMonthlyCapUsd()` promoted to `protected` at line 140 |
| 9 | A single DECKFLOW_LLM_PROVIDER read drives both the registered distiller AND StudioDistillConfig.IsSubscriptionProvider (HIGH-1) | HARV-05 | VERIFIED | `Program.cs:57-60` — `providerEnv` read once; line 80 — `LlmDistillationProviderFactory.Resolve(providerEnv, ...)` (factory, not hardcoded); line 81 — `new StudioDistillConfig(isSubscriptionProvider)`. `grep -c "new LlmDistillationService" Program.cs` = 0 (hardcoded removed) |
| 10 | SessionCapOverride is an app-scoped singleton; OverrideUsd when set raises the cap for the whole app | HARV-05 | VERIFIED | `SessionCapOverride.cs:16` — `public decimal? OverrideUsd { get; set; }`; XML doc states "app-scoped" / "entire running app process"; `Program.cs:65-73` — single `capOverride` instance captured in ledger resolver closure sharing the override with the orchestrator |
| 11 | Stage A dry-run shows projected spend, would-run count, and monthly cap remaining before any live distill | HARV-05 | VERIFIED | `Harvest.razor:558-580` — dry-run result card (`card border-primary`) renders `WouldRun`, `ProjectedSpendUsd`, remaining; `RunDistillStageAAsync` at 1280-1306 calls `DistillAsync(dryRun: true, redistill: redistillConfirmed, ...)` |
| 12 | Re-distilling already-distilled videos requires two sequential checkbox confirmations before dry-run enables | HARV-05 | VERIFIED | `Harvest.razor:748-749` — `_redistillCheck1`, `_redistillCheck2` backing fields; lines 453/469 — second checkbox revealed only when first is checked; `redistillConfirmed = _redistillCheck1 && _redistillCheck2` at line 393; dry-run button disabled unless gate clear |
| 13 | Stage B requires reviewed-spend confirmation before live Run Distill button enables | HARV-05 | VERIFIED | `Harvest.razor` — `_distillSpendConfirmed` checkbox; Stage B `btn-primary` disabled when `!_distillSpendConfirmed && !DistillConfig.IsSubscriptionProvider` (line 1322); Stage B hidden until dry-run succeeds |
| 14 | DistillAsync is called with redistill:true ONLY after both confirmations; redistill:false otherwise; projected spend exceeding cap blocks live distill | HARV-05 | VERIFIED | Stage A call at line 1289: `redistill: redistillConfirmed`; Stage B call at line 1378: `redistill: redistillConfirmed` (same gate). `capExceeded` at line 555 blocks Stage B when metered provider. `ContentKbOrchestrator.cs:313-325` — redistill bypass guarded by `redistill && requestedKeys is not null && requestedKeys.Contains(naturalKey)` then `ClearDistillOutputAsync` on live run |

**Score:** 14/14 truths VERIFIED (automated)

---

### Required Artifacts

| Artifact | Status | Evidence |
|----------|--------|---------|
| `DeckFlow.Core/Content/ILlmSpendLedger.cs` | VERIFIED | `decimal GetMonthlyCapUsd()` at line 51; all pre-existing members preserved |
| `DeckFlow.Core/Content/SpendLedgerBase.cs` | VERIFIED | `public decimal GetMonthlyCapUsd() => ReadMonthlyCapUsd()` at line 136; `protected decimal ReadMonthlyCapUsd()` at line 140 |
| `DeckFlow.Core/Content/VideoStatus.cs` | VERIFIED | `public enum VideoStatus` with 5 members: NotHarvested, Harvested, Distilled, Blocked, Duplicate |
| `DeckFlow.Core/Content/VideoStatusResolver.cs` | VERIFIED | `public sealed class VideoStatusResolver` in Core (not Studio — HIGH-2); real store queries for all 4 resolution paths; `ContentSourceType.Youtube` constant used (no literal — LOW-1) |
| `DeckFlow.Core/Orchestration/IDistillOrchestrator.cs` | VERIFIED | `bool redistill = false` parameter between `isSubscriptionProvider` and `videoIds` at line 28; XML doc present |
| `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` | VERIFIED | Redistill bypass at lines 313-325: `redistill && requestedKeys is not null && requestedKeys.Contains(naturalKey)` → `ClearDistillOutputAsync` on live run; fall-through to existing distill path |
| `DeckFlow.Studio/StudioDistillConfig.cs` | VERIFIED | `public sealed record StudioDistillConfig(bool IsSubscriptionProvider)` |
| `DeckFlow.Studio/SessionCapOverride.cs` | VERIFIED | `public sealed class SessionCapOverride` with `decimal? OverrideUsd { get; set; }`; XML doc states app-scoped reality (no false per-circuit claim) |
| `DeckFlow.Studio/Program.cs` | VERIFIED | Single `providerEnv` read drives factory distiller + `isSubscriptionProvider`; override-aware ledger singleton; `VideoStatusResolver` singleton registered (line 98) |
| `DeckFlow.Studio/Pages/Harvest.razor` | VERIFIED | 1513 lines (well above 120-line minimum); all HARV-01..05 wiring confirmed by grep |
| `DeckFlow.Studio/Services/ActionOrchestratorProgress.cs` | VERIFIED | `internal sealed class ActionOrchestratorProgress : IOrchestratorProgress`; `_ = _sink(message)` fire-and-forget at line 42 |
| `DeckFlow.Studio/Shared/NavMenu.razor` | VERIFIED | `href="harvest"` + `oi-cloud-download` at lines 18-19; existing Home `NavLinkMatch.All` retained |
| `DeckFlow.Core.Tests/LlmSpendLedgerTests.cs` | VERIFIED | `GetMonthlyCapUsd_ReturnsDefaultWhenNoConfigurationSet`, `GetMonthlyCapUsd_ReturnsConfiguredValueWhenResolverProvided`, `WouldExceedCapAsync_RespectsRaisedCapFromResolver` all present |
| `DeckFlow.Core.Tests/VideoStatusResolverTests.cs` | VERIFIED | 4 `[Fact]` methods: Blocked/Distilled/FoundInSecondEnabledSource/NotFoundInAnySources; in-file fakes, no mocking library |
| `DeckFlow.Core.Tests/Orchestration/ContentKbOrchestratorDistillTests.cs` | VERIFIED | 2 `[Fact]` methods: default `redistill=false` skips + `redistill=true` bypasses; `ClearDistillOutputCalled` tracking asserted |

---

### Key Link Verification

| From | To | Via | Status | Evidence |
|------|----|-----|--------|---------|
| `Harvest.razor` | `IYouTubeChannelVideoLister.ListRecentAsync` | `Task.Run` off sync context | WIRED | Line 800-803 |
| `Harvest.razor` | `IYouTubeChannelVideoLister.GetByIdsAsync` | `Task.Run` off sync context | WIRED | Line 880/890 |
| `Harvest.razor` | `VideoStatusResolver.ResolveStatusAsync` | per-video at list-build | WIRED | 3 call sites (lines 809, 904, 1425) |
| `Harvest.razor` | `IHarvestOrchestrator.HarvestAsync` | `Task.Run` + `ActionOrchestratorProgress` + `InvokeAsync(StateHasChanged)` | WIRED | Lines 1123-1150 |
| `Harvest.razor` | `IDistillOrchestrator.DistillAsync` (Stage A) | `Task.Run`, `dryRun: true`, `redistill: redistillConfirmed` | WIRED | Lines 1280-1289 |
| `Harvest.razor` | `IDistillOrchestrator.DistillAsync` (Stage B) | `Task.Run`, `dryRun: false`, `redistill: redistillConfirmed` | WIRED | Lines 1373-1378 |
| `Harvest.razor` | `ILlmSpendLedger.GetMonthlyCapUsd / GetMonthlyTotalAsync` | `RefreshCapDisplayAsync` at init + post-distill | WIRED | Lines 1214-1216 |
| `Harvest.razor` | `SessionCapOverride.OverrideUsd` | Raise-cap input + `RaiseCapAsync` | WIRED | Lines 1221-1236 |
| `Program.cs` | `LlmDistillationProviderFactory.Resolve(providerEnv, ...)` | Single `providerEnv` read drives factory | WIRED | Lines 57-80; `new LlmDistillationService` count = 0 |
| `Program.cs` | `SessionCapOverride` → ledger resolver closure | `capOverride.OverrideUsd` read in resolver at line 70-71 | WIRED | Lines 65-73 |
| `SpendLedgerBase` | `ReadMonthlyCapUsd()` | `GetMonthlyCapUsd() => ReadMonthlyCapUsd()` at line 136 | WIRED | Line 136 (method + delegate) |
| `ContentKbOrchestrator` | `IContentVideoStore.ClearDistillOutputAsync` | `redistill` bypass at lines 313-322 | WIRED | Lines 313-325 |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| `Harvest.razor` — channel table | `_browseVideos` | `IYouTubeChannelVideoLister.ListRecentAsync` (live YouTube API via YoutubeExplode) | Yes | FLOWING |
| `Harvest.razor` — paste queue | `_queue` | `IYouTubeChannelVideoLister.GetByIdsAsync` (live YouTube API) | Yes | FLOWING |
| `Harvest.razor` — status badges | `VideoStatus` per video | `VideoStatusResolver.ResolveStatusAsync` → real store queries (blocked/index/source/video stores) | Yes | FLOWING |
| `Harvest.razor` — cap display | `_monthlyCap`, `_monthlySpent` | `SpendLedger.GetMonthlyCapUsd()` (resolver/env) + `GetMonthlyTotalAsync` (DB query) | Yes | FLOWING |
| `Harvest.razor` — dry-run result | `_distillDryRunResult` | `IDistillOrchestrator.DistillAsync(dryRun:true)` → real orchestrator pass | Yes | FLOWING |
| `Harvest.razor` — live distill result | `_distillLiveResult` | `IDistillOrchestrator.DistillAsync(dryRun:false)` → real LLM calls | Yes | FLOWING |

---

### Behavioral Spot-Checks

Step 7b: SKIPPED — Studio is a Blazor Server app requiring a live browser circuit; no headless runnable entry points for automated spot-checks. Human verification checkpoints in 45-03 (APPROVED) and 45-04 (APPROVED) cover the runtime behaviors.

---

### Probe Execution

Step 7c: No probe scripts declared or conventional for this phase. SKIPPED.

---

### Requirements Coverage

| Requirement | Source Plan(s) | Status | Evidence |
|-------------|---------------|--------|---------|
| HARV-01: Channel browse | 45-03 | SATISFIED | `ListRecentAsync` in `Task.Run`; table with thumbnail/title/published/badge; tinting for Harvested/Distilled |
| HARV-02: URL/ID paste queue with per-row remove | 45-03 | SATISFIED | `GetByIdsAsync`; per-row `btn-outline-danger` with `aria-label="Remove video from queue"`; duplicate badge for Harvested/Distilled |
| HARV-03: One status badge per video from UI-SPEC vocabulary | 45-02, 45-03 | SATISFIED | `VideoStatus` enum (5 members); `VideoStatusResolver` with real store queries; `RenderBadge` switch in Harvest.razor covering all 5 cases |
| HARV-04: Non-blocking harvest with live progress and cancel-on-circuit-drop | 45-03 | SATISFIED | `Task.Run` + `ActionOrchestratorProgress` + `InvokeAsync`-marshalled progress; `_cts` disposed on `Dispose()`; `ObjectDisposedException` swallowed in progress sink |
| HARV-05: Distill spend gate (dry-run, re-distill double-confirm, cap + session override, cap-exceeded block, redistill:true only after double-confirm) | 45-01, 45-02, 45-04 | SATISFIED | All sub-requirements verified in code (see Truths 8-14) |

---

### Anti-Patterns Found

| File | Pattern | Severity | Notes |
|------|---------|----------|-------|
| `Harvest.razor:32` | `placeholder="..."` HTML attribute | Info | Not a stub — this is an `<input placeholder="">` UI hint, not a code anti-pattern |
| `Harvest.razor:431` | `placeholder="..."` HTML attribute | Info | Same — numeric input placeholder for cap-raise field |

No `TODO`, `FIXME`, `TBD`, `XXX`, or `PLACEHOLDER` code markers found in any phase-modified files.
No `return null`, `return {}`, or empty-implementation stubs found in the key production files.

---

### Build Status

| Project | Result | C# Errors | C# Warnings |
|---------|--------|-----------|-------------|
| `DeckFlow.Core` | Build succeeded | 0 | 0 |
| `DeckFlow.Core.Tests` | Build succeeded | 0 | 0 |
| `DeckFlow.Web` | Build succeeded | 0 | 0 |
| `DeckFlow.Studio` | Build succeeded (code) | 0 CS errors | 0 CS warnings |

**Note on Studio build during verification:** DeckFlow.Studio was running (process 38492) and held a file lock on `DeckFlow.Core.dll`, causing MSB3021/MSB3027 file-copy errors (not C# compilation errors). No `error CS*` or `warning CS*` lines were emitted. The Studio project compiles clean — the file-lock is a transient environment condition, not a code defect. The 45-04 SUMMARY reports "Build succeeded. 0 errors, 0 new warnings." from the post-implementation build run when Studio was not running.

**xUnit analyzer warnings (pre-existing):** `DeckFlow.Core.Tests/Orchestration/EnsureYoutubeSourceTests.cs` has 3 xUnit2017 `Assert.True()` warnings — these are pre-existing and not introduced by Phase 45.

---

### Human Verification Required

#### 1. Re-distill end-to-end (redistill:true path, HIGH-4)

**Test:** Select an already-distilled video on the Harvest page. Confirm the amber re-distill banner appears. Check both the "Re-distill..." and "Yes, I understand..." checkboxes. Run Estimate Spend (dry-run). Confirm WouldRun = 1 (not 0). Then run live distill. Confirm the video is actually re-processed — check the Studio console for a "re-distilling ..." log line or a VideosDistilled > 0 result; verify the video's distill output has been replaced (fresh summary/clips visible in the DB or admin UI).

**Expected:** Dry-run with redistill:true shows WouldRun = 1 for the already-distilled video. Live run shows VideosDistilled = 1, new distill output in DB. Studio console shows the "re-distilling {videoId} (redistill=true)" log line from ContentKbOrchestrator.

**Why human:** The redistill path through `ClearDistillOutputAsync` → `DistillVideoAsync` requires a live LLM run against a pre-existing distilled video. The Core.Tests unit tests (`ContentKbOrchestratorDistillTests`) prove the skip-bypass and `ClearDistillOutputCalled` assertion in isolation. The 45-04 human-verify confirmed the UI gate and live distill flow but did not exercise a re-distill of an already-distilled video during that smoke session.

#### 2. Cap-exceeded block and session override non-persistence

**Test:** Configure selection so projected spend exceeds remaining cap. Confirm the Stage B / Run Distill button is blocked and the red alert appears. Use "Raise cap (this session)" to raise the cap above projected spend. Confirm the block clears. Restart Studio. Confirm cap display shows $15.00 (or the DECKFLOW_LLM_MONTHLY_CAP_USD env value), not the raised value.

**Expected:** Block enforced when projected > remaining. Block clears after cap raise. After Studio restart, cap is back to env/default ($15.00 if no env var set) — the override did not persist.

**Why human:** Non-persistence of SessionCapOverride (D-03) requires verifying a process restart. Cannot be tested programmatically without controlling the process lifecycle.

#### 3. Cancel-on-dispose mid-harvest with a real enabled source

**Test:** Start a harvest with a real YouTube source configured (auto-ensure will create one). Mid-harvest, close the browser tab. Check Studio server console for any ObjectDisposedException or unobserved exception from a late progress callback.

**Expected:** Harvest stops cleanly (OperationCanceledException logged). Zero ObjectDisposedException / zero unobserved TaskException in Studio console.

**Why human:** The 45-03 SUMMARY notes a caveat: the cancel-on-dispose/disposal-race path was verified clean during the smoke run, but no enabled YouTube source existed at the time, so the harvest hit the "0 sources enabled" guard immediately rather than running long enough to exercise the mid-flight cancel. Should be re-verified with a real source enabled (auto-ensure was added in a follow-up quick task after 45-03).

---

### Gaps Summary

No gaps found. All 14 must-have truths are VERIFIED in code. The 3 human verification items are runtime UI behaviors that cannot be verified by grep/file inspection:
- The re-distill end-to-end path (HIGH-4) is the highest-priority item; the code wiring is correct but the full live flow needs a human smoke with an already-distilled video.
- The other two items (cap-persist restart, cancel-on-dispose with real source) are lower priority but needed to close the 45-03 SUMMARY caveat cleanly.

Phase 45 code is complete and correctly wired. Human verification is the only remaining gate.

---

_Verified: 2026-06-15T17:55:00Z_
_Verifier: Claude (gsd-verifier)_
