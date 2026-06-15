# Phase 45: Harvest + Distill UI - Research

**Researched:** 2026-06-15
**Domain:** Blazor Server UI wiring over existing Core orchestration (IContentKbOrchestrator,
IYouTubeChannelVideoLister, ILlmSpendLedger, IContentVideoStore, IBlockedVideoStore)
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-01 — isSubscriptionProvider auto-detect:** Resolved host-side from the wired distill backend
(claude-CLI = subscription/$0 = `true`; metered OpenAI = `false`). Current Studio wiring is the
claude-CLI backend. The page reads the resolved singleton `bool`; no operator toggle.

**D-02 — Ledger read surface:** Page shows monthly cap + remaining (cap minus current-month total).
Requires a new getter on `ILlmSpendLedger` for configured cap and current-month total (or a single
"remaining" projection). Cap key is `DECKFLOW_LLM_MONTHLY_CAP_USD`, default `$15.00`, read via
`SpendLedgerBase.ReadMonthlyCapUsd()`.

**D-03 — Runtime cap override:** Operator can raise cap inline from the page. Override is
in-memory/session-only (registered singleton in Studio DI). Resets to env/default $15 on Studio
restart. Must actually feed into `WouldExceedCapAsync` (resolver reads override when present).

**D-04 — Channel browse count:** Operator-set numeric input, default 25, fed to `limit` parameter
of `IYouTubeChannelVideoLister.ListRecentAsync`. Already-harvested rows shown with
`table-secondary` tinting.

**D-05 — Session/queue persistence:** In-memory component state only. Page refresh or SignalR
circuit drop clears state. No new DB storage.

**D-06 — Dispose-cancels:** CancellationTokenSource is disposed on `IDisposable.Dispose()`.
Circuit drop cancels in-flight harvest/distill. Partial harvest is safe to resume; partial distill
spend is already in the ledger. No resume affordance this phase.

### Claude's Discretion

- **HARV-01 channel-video listing path:** Call `IYouTubeChannelVideoLister` directly from the
  Blazor component (already registered as singleton in Studio Program.cs) vs. adding a thin list
  method to the orchestrator facade. Either is acceptable; keep Core console-free and Studio
  free of any DeckFlow.CLI reference.
- **HARV-03 status-badge / dedup data path:** Direct store queries (`IContentVideoStore`,
  `IBlockedVideoStore`) at render time vs. a small query helper. UI-SPEC locks badge vocabulary
  and resolution rules; wiring is discretion.
- Exact shape of the D-02 ledger read surface (separate cap getter + month-total getter vs. a
  single "cap + remaining" projection record).
- Progress-sink bridge details (`IOrchestratorProgress.Report` to `InvokeAsync(StateHasChanged)`)
  including any StateHasChanged batching to avoid log-flood re-renders.

### Deferred Ideas (OUT OF SCOPE)

- Persisted writable spend cap (session override only, D-03).
- Persisted draft queue/selection (in-memory only, D-05).
- Block/unblock + hard-delete maintenance (Phase 46+).
- Review queue / approve-reject / publish (REVQ-* / Phase 46).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| HARV-01 | Paste YouTube channel URL/handle/ID, see recent videos | `IYouTubeChannelVideoLister.ListRecentAsync` already in Studio DI; channel handle/URL/slug parsed by YoutubeExplode `ChannelHandle.TryParse`, `ChannelId.TryParse`, `ChannelSlug.TryParse` |
| HARV-02 | Paste individual YouTube video URLs/IDs to queue | `IYouTubeChannelVideoLister.GetByIdsAsync` resolves URLs/IDs via `YoutubeExplode.Videos.VideoId.TryParse`; throws `ArgumentException` for unparseable input |
| HARV-03 | Per-video harvested/distilled/blocked/duplicate status badge; dedup | `IContentVideoStore.GetVideoByYoutubeIdAsync` detects harvest; `IContentSiteIndexStore.GetByNaturalKeyAsync` detects distill; `IBlockedVideoStore.IsBlockedAsync` detects block |
| HARV-04 | Live harvest progress without UI freeze; cancel wired to IDisposable | `Task.Run` + synchronous `IOrchestratorProgress.Report` bridging to `InvokeAsync(StateHasChanged)` + `CancellationTokenSource` disposed on component `Dispose()` |
| HARV-05 | Spend dry-run gate, re-distill explicit confirm, cap enforcement | `IDistillOrchestrator.DistillAsync(dryRun:true)` for projection; cap read from `SpendLedgerBase` + session override (D-02/D-03); re-distill double-checkbox confirmed before `dryRun:false` |
</phase_requirements>

---

## Summary

Phase 45 is a Blazor Server UI page (`DeckFlow.Studio/Pages/Harvest.razor`) that wires the Studio
operator to the already-complete Core orchestration surface. All harvest/distill/transcription
logic is in `IContentKbOrchestrator` (Phase 42). All status stores are already registered as
singletons in `DeckFlow.Studio/Program.cs`. The orchestrator is registered as scoped via
`AddContentKbOrchestrator()`. The page itself needs no new NuGet packages and no new domain logic
— it is UI plumbing only.

The three non-trivial pieces are:

1. **Ledger read surface** (D-02/D-03): `ILlmSpendLedger` exposes `GetMonthlyTotalAsync` (already
   on the interface) but has no public `ReadMonthlyCapUsd()` — that method is `private` on
   `SpendLedgerBase`. A thin addition to `ILlmSpendLedger` to expose cap + current total is
   needed. The session override (D-03) requires a registered singleton (e.g. `SessionCapOverride`)
   that the ledger resolver can consult.

2. **isSubscriptionProvider detection** (D-01): The CLI derives this by checking
   `DECKFLOW_LLM_PROVIDER != "openai"`. Studio must replicate that logic in `Program.cs` at
   startup and register it as a singleton `bool` (or named constant) for the page to inject.
   Current Studio wiring uses the claude-CLI backend (`LlmDistillationProviderFactory.Resolve`),
   which means `isSubscriptionProvider = true` at current configuration.

3. **Blazor background-task / progress pattern** (Pitfall 7): Long-running harvest/distill cannot
   run on the Blazor synchronization context or it blocks the SignalR circuit. The correct pattern
   is `Task.Run(async () => { ... }, cts.Token)` from `OnInitializedAsync` or a button handler,
   with `IOrchestratorProgress` bridging to `await InvokeAsync(StateHasChanged)`.

**Primary recommendation:** Wire `IYouTubeChannelVideoLister` directly from the component (it is
already a singleton in Studio DI); do NOT add a list method to the orchestrator. For badge
resolution, query `IContentVideoStore` and `IBlockedVideoStore` directly at render time per
UI-SPEC's "Implementation note" — this avoids adding a query helper for a read-only, UI-only
operation.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Channel browse (recent video list) | Blazor Component | Core Integration (`IYouTubeChannelVideoLister`) | Component owns the UX trigger/state; Core owns the YouTube IO |
| URL/ID paste + video resolution | Blazor Component | Core Integration (`IYouTubeChannelVideoLister.GetByIdsAsync`) | Component parses textarea lines; Core resolves metadata via YoutubeExplode |
| Per-video status badge resolution | Blazor Component (at render) | Core Stores (`IContentVideoStore`, `IBlockedVideoStore`, `IContentSiteIndexStore`) | Purely a read/display concern; store queries are cheap point lookups by youtube_video_id |
| Harvest trigger + progress | Blazor Component (Task.Run) | Core Orchestration (`IHarvestOrchestrator`) | Component owns background-task lifecycle (CTS, progress bridge, StateHasChanged); Core owns business logic |
| Distill dry-run + execute | Blazor Component (Task.Run) | Core Orchestration (`IDistillOrchestrator`) | Same pattern as harvest; two-stage (dry-run then execute) is a UI state machine concern |
| Spend cap read + session override | Studio Program.cs + `ILlmSpendLedger` | — | Cap config is server-side; session override is a Studio singleton not known to Core |
| isSubscriptionProvider detection | Studio Program.cs (startup singleton) | Core `LlmDistillationProviderFactory` | Consistent with CLI derivation; page reads the resolved value, never re-derives it |
| Operation lock (`_operationInFlight`) | Blazor Component | — | Single-circuit, single-operator; component state is sufficient |
| NavMenu registration | Studio `Shared/NavMenu.razor` | — | Standard Blazor nav wiring |

---

## Standard Stack

### Core — No new packages required

All libraries are already present in `DeckFlow.Studio/bin/Debug/net10.0/` and `DeckFlow.Core`.
[VERIFIED: csproj + bin directory inspection]

| Library | Version | Purpose | Source |
|---------|---------|---------|--------|
| Microsoft.AspNetCore.Components.Server | net10.0 built-in | Blazor Server, `InvokeAsync`, `IDisposable` component lifecycle | `DeckFlow.Studio.csproj` SDK |
| DeckFlow.Core | project ref | All orchestration, stores, ledger, lister, models | `DeckFlow.Studio.csproj` `<ProjectReference>` |
| YoutubeExplode | 6.6.0 (via Core) | `VideoId.TryParse` for URL/ID resolution, `ChannelHandle.TryParse` for channel parsing | `DeckFlow.Studio/bin/Debug/net10.0/YoutubeExplode.dll` |
| Bootstrap 5 | bundled in wwwroot | All UI components — cards, badges, tables, spinners, buttons | `DeckFlow.Studio/wwwroot/css/bootstrap/bootstrap.min.css` |
| Open Iconic | bundled in wwwroot | Icons (`oi oi-cloud-download`, `oi oi-check`, `oi oi-warning`, `oi oi-x`) | `DeckFlow.Studio/wwwroot/css/open-iconic/` |

**Installation:** No `dotnet add package` commands required. Zero new NuGet packages.
[VERIFIED: bin directory contains all required DLLs]

---

## Package Legitimacy Audit

No new packages are introduced in this phase. All dependencies are already present in the
solution. This section is not applicable.

**Packages added:** none

---

## Architecture Patterns

### System Architecture Diagram

```
Operator Browser
       |
       | SignalR (Blazor Server circuit)
       v
  Harvest.razor (component)
       |
       |-- [Browse Channel] --> IYouTubeChannelVideoLister.ListRecentAsync(url, limit, ct)
       |                              (SemaphoreSlim(1) inside; AngleSharp concurrency guard)
       |                              returns IReadOnlyList<YouTubeChannelVideo>
       |
       |-- [Add to Queue]  --> IYouTubeChannelVideoLister.GetByIdsAsync(ids, ct)
       |                              (VideoId.TryParse per line from textarea)
       |
       |-- [Render badges] --> IContentVideoStore.GetVideoByYoutubeIdAsync(sourceId, ytId)
       |                    --> IContentSiteIndexStore.GetByNaturalKeyAsync("youtube", ytId)
       |                    --> IBlockedVideoStore.IsBlockedAsync(ytId)
       |
       |-- [Harvest Selected]
       |       |
       |       v (Task.Run, off Blazor sync ctx)
       |   IHarvestOrchestrator.HarvestAsync(limit, videoIds, null, progress, cts.Token)
       |       |
       |       v (IOrchestratorProgress.Report -> InvokeAsync(StateHasChanged))
       |   Progress log box updates live
       |       |
       |       v HarvestResult (Captions, Whisper, SkippedNoCaptions, Success, Message)
       |
       |-- [Estimate Spend (dry run)]
       |       |
       |       v (Task.Run)
       |   IDistillOrchestrator.DistillAsync(limit, dryRun:true, isSubscriptionProvider,
       |                                      videoIds, progress, cts.Token)
       |       v DistillResult (WouldRun, ProjectedSpendUsd, DryRun:true)
       |   vs. ILlmSpendLedger.GetMonthlyTotalAsync + GetMonthlyCapUsd -> "remaining"
       |
       |-- [Run Distill] (after dry-run result card + confirmation checkbox)
               |
               v (Task.Run)
           IDistillOrchestrator.DistillAsync(limit, dryRun:false, isSubscriptionProvider,
                                              videoIds, progress, cts.Token)
               v DistillResult (VideosDistilled, LlmCalls, LlmSpendUsd, FailedVideoIds)
```

### Recommended Project Structure

```
DeckFlow.Studio/
├── Pages/
│   └── Harvest.razor              # New — phase 45 primary deliverable
├── Shared/
│   └── NavMenu.razor              # Modify — add "Harvest" nav entry
├── Services/
│   └── ContentKbOrchestratorSmokeService.cs   # Existing (no change)
├── StudioConfig.cs                # Existing (no change)
└── Program.cs                     # Modify — add isSubscriptionProvider singleton,
                                   #           cap-override holder, ledger read surface
```

No new folders. `Harvest.razor` is a single-file Razor component (all `@code` inline, no
code-behind file needed given the single-operator local context).

---

### Pattern 1: Blazor Background Task with Live Progress (Pitfall 7 mitigation)

**What:** Run orchestrator on a background thread. Bridge synchronous `IOrchestratorProgress.Report`
to `InvokeAsync(StateHasChanged)` for live UI updates without blocking the SignalR circuit.

**When to use:** Any button that triggers `HarvestAsync` or `DistillAsync`.

```csharp
// Source: Blazor Server docs + project memory (Pitfall 7 in STATE.md)
// In Harvest.razor @code section:

private CancellationTokenSource? _cts;
private bool _operationInFlight;
private List<string> _logLines = new();

private async Task HarvestSelectedAsync()
{
    if (_operationInFlight) return;
    _operationInFlight = true;
    _cts = new CancellationTokenSource();
    _logLines.Clear();

    var progress = new ActionOrchestratorProgress(async msg =>
    {
        _logLines.Add(msg);
        await InvokeAsync(StateHasChanged);
    });

    _harvestResult = null;
    try
    {
        // Why: Task.Run moves the orchestrator off the Blazor sync context.
        // Without this, long-running IO blocks the SignalR circuit (Pitfall 7).
        _harvestResult = await Task.Run(
            () => _harvestOrchestrator.HarvestAsync(
                limit: _selectedVideoIds.Count,
                videoIds: _selectedVideoIds,
                progress: progress,
                cancellationToken: _cts.Token),
            _cts.Token);
    }
    catch (OperationCanceledException)
    {
        _logLines.Add("Harvest cancelled.");
    }
    finally
    {
        _operationInFlight = false;
        await InvokeAsync(StateHasChanged);
    }
}

public void Dispose()
{
    // Why: CTS disposed on component disposal so a circuit drop cancels in-flight ops (D-06).
    _cts?.Cancel();
    _cts?.Dispose();
}
```

**ActionOrchestratorProgress** — a small Studio-local adapter (not in Core):

```csharp
// New class in DeckFlow.Studio (or inline sealed class in Harvest.razor @code)
// Source: IOrchestratorProgress.cs contract (synchronous void Report)
internal sealed class ActionOrchestratorProgress : IOrchestratorProgress
{
    private readonly Func<string, Task> _sink;

    internal ActionOrchestratorProgress(Func<string, Task> sink)
    {
        _sink = sink;
    }

    public void Report(string message)
    {
        // Why: Report is synchronous by design (OrchestratorProgress.cs contract).
        // Fire-and-forget the async StateHasChanged bridge; we don't await here
        // because Report() cannot be async (would require IProgress<T> instead).
        _ = _sink(message);
    }
}
```

**Batching note:** For high-frequency progress (e.g., 50 videos → 50 rapid Report calls), the
fire-and-forget pattern queues many `InvokeAsync` calls. For Phase 45 volumes (25 default channel
browse, modest harvest sets), this is acceptable. If a future phase harvests 200+ videos in a
single run, add a `_logBatchTimer` (100ms debounce) before accumulating into `_logLines`.

---

### Pattern 2: IYouTubeChannelVideoLister called directly from component

**What:** Call `IYouTubeChannelVideoLister.ListRecentAsync` or `GetByIdsAsync` inside a
`Task.Run` for the same circuit-blocking reason (YouTube HTTP calls are slow).

**Why direct (not via orchestrator):** `IContentKbOrchestrator` has no "list channel videos"
method. The CONTEXT.md Claude's Discretion section explicitly names this choice. The lister is
already registered as a singleton in Studio `Program.cs` (line 56). The page injects
`IYouTubeChannelVideoLister` directly — no CLI reference, no Core change required.

```csharp
// Source: IYouTubeChannelVideoLister.cs (verified)
[Inject] private IYouTubeChannelVideoLister Lister { get; set; } = default!;

private async Task BrowseChannelAsync()
{
    // Why: SemaphoreSlim(1) is INSIDE YouTubeChannelVideoLister (MetadataLookupConcurrency=1).
    // The caller does NOT need its own gate, but must still run off the Blazor sync ctx.
    _channelVideos = await Task.Run(
        () => Lister.ListRecentAsync(_channelInput, _browseLimit, _cts!.Token),
        _cts!.Token);
}
```

---

### Pattern 3: Per-video badge resolution via direct store queries

**What:** Resolve each `YouTubeChannelVideo` to a `VideoStatus` enum at render time by querying
the three stores. Badge state is derived synchronously from the returned records.

**Resolution rules** (from UI-SPEC Status Badge Vocabulary):

```csharp
// Source: 45-UI-SPEC.md Status Badge Vocabulary + IContentVideoStore.cs + IBlockedVideoStore.cs
// Called once per video during BrowseChannelAsync/AddToQueueAsync to pre-cache status.
private async Task<VideoStatus> ResolveStatusAsync(string youtubeVideoId)
{
    // Why: blocked check first — blocked videos should never be harvested regardless of
    // whether a transcript already exists.
    if (await BlockedStore.IsBlockedAsync(youtubeVideoId)) return VideoStatus.Blocked;

    // Need sourceId to look up content_videos; use first enabled source or any source
    // (video may span sources if re-added). For dedup, existence in any source is sufficient.
    // See Open Question #1 for the sourceId resolution detail.
    var contentVideo = await VideoStore.GetVideoByYoutubeIdAsync(/* sourceId */, youtubeVideoId);
    if (contentVideo is null) return VideoStatus.NotHarvested;

    // Check distilled: a content_site_index row keyed by (youtube, youtubeVideoId) exists
    var indexRow = await IndexStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, youtubeVideoId);
    if (indexRow is not null) return VideoStatus.Distilled;

    return VideoStatus.Harvested;
}

// Note: 'duplicate' badge (VideoStatus.Duplicate) is only shown in the paste queue section,
// not the channel browse section. It means GetVideoByYoutubeIdAsync returned non-null
// before the operator hits "Add to Queue" — warn but allow selection.
```

**Caveat — sourceId:** `GetVideoByYoutubeIdAsync` takes a `sourceId` parameter (the owning
content source). For the status-check purpose (is this video already in DB?), the component
needs a source to query against. The simplest approach: load all enabled sources via
`IContentSourceStore` once on page init; for status checks, iterate sources until a match is
found, or query `ContentVideoStore` with a cross-source method if one exists. See Open
Question 1.

---

### Pattern 4: isSubscriptionProvider singleton in Program.cs

**What:** Derive `isSubscriptionProvider` at startup and register it as a singleton `bool`.
Replicates the CLI's derivation exactly.

```csharp
// Source: DeckFlow.CLI/ContentKbCommandRunners.cs lines 95-97 (verified)
// In DeckFlow.Studio/Program.cs after reading configuration:
var providerEnv = builder.Configuration["DECKFLOW_LLM_PROVIDER"]
    ?? Environment.GetEnvironmentVariable(LlmDistillationProviderFactory.EnvironmentVariableName);
var isSubscriptionProvider = !string.IsNullOrWhiteSpace(providerEnv)
    && !string.Equals(providerEnv.Trim(), "openai", StringComparison.OrdinalIgnoreCase);
builder.Services.AddSingleton<bool>(_ => isSubscriptionProvider);  // or named wrapper record
```

**Note:** Registering a raw `bool` singleton is uncommon and fragile in DI. Prefer a small
wrapper record:

```csharp
// New in DeckFlow.Studio (e.g., StudioDistillConfig.cs):
public sealed record StudioDistillConfig(bool IsSubscriptionProvider);
// Program.cs: builder.Services.AddSingleton(new StudioDistillConfig(isSubscriptionProvider));
// Harvest.razor: [Inject] private StudioDistillConfig DistillConfig { get; set; } = default!;
```

---

### Pattern 5: Session cap override + ledger read surface (D-02/D-03)

**What:** A `SessionCapOverride` singleton holds an optional operator-raised cap. The
`ILlmSpendLedger` interface needs two new methods so the page can display cap + remaining
without manually reading `DECKFLOW_LLM_MONTHLY_CAP_USD` itself.

**New interface additions to `ILlmSpendLedger`:**

```csharp
// Source: SpendLedgerBase.cs (ReadMonthlyCapUsd is currently private — must be exposed)
// New on ILlmSpendLedger:
decimal GetMonthlyCapUsd();               // reads env/default; ignores session override
// OR, since the session override is Studio-specific (not Core), expose GetMonthlyCapUsd()
// on ILlmSpendLedger and let Program.cs inject the override via the configurationValueResolver.
```

**Recommended shape (keeps override out of Core):**

```csharp
// New singleton in DeckFlow.Studio:
public sealed class SessionCapOverride
{
    public decimal? OverrideUsd { get; set; }   // null = use env/default
}

// LlmSpendLedger already accepts Func<string, string?> configurationValueResolver in its ctor.
// Slot the override into that resolver:
var capOverride = new SessionCapOverride();
builder.Services.AddSingleton(capOverride);
builder.Services.AddSingleton<ILlmSpendLedger>(_ =>
    new LlmSpendLedger(contentKbDatabasePath, key =>
    {
        if (key == "DECKFLOW_LLM_MONTHLY_CAP_USD" && capOverride.OverrideUsd.HasValue)
            return capOverride.OverrideUsd.Value.ToString("F2", CultureInfo.InvariantCulture);
        return null; // fall through to env var / default
    }));
```

**Adding a cap-read method to `ILlmSpendLedger`:**

`SpendLedgerBase.ReadMonthlyCapUsd()` is `private`. Adding `decimal GetMonthlyCapUsd()` to
`ILlmSpendLedger` and implementing it as `public decimal GetMonthlyCapUsd() => ReadMonthlyCapUsd()`
(after changing the base method to `protected`) allows the page to display "Cap: $X.XX".

Alternatively the page can call `GetMonthlyTotalAsync(yearMonth)` (already on the interface) and
read the cap from the configuration singleton separately. This avoids touching the `ILlmSpendLedger`
interface.

**Simplest path:** Add `decimal GetMonthlyCapUsd()` to `ILlmSpendLedger` and `SpendLedgerBase`
(promote `ReadMonthlyCapUsd` to `protected`). The Harvest page then has:
- Cap: `_ledger.GetMonthlyCapUsd()`
- Spent: `await _ledger.GetMonthlyTotalAsync(currentMonthKey)`
- Remaining: cap - spent

---

### Anti-Patterns to Avoid

- **Calling orchestrator from Blazor sync context directly:** `await _orchestrator.HarvestAsync(...)`
  without `Task.Run` will block the SignalR circuit during long video harvests. Always wrap in
  `Task.Run`. [VERIFIED: STATE.md Pitfall 7]

- **Using `Task.WhenAll` over `IYouTubeChannelVideoLister`:** AngleSharp BrowsingContext is not
  thread-safe. The lister enforces `SemaphoreSlim(1)` internally. Do NOT parallelize calls to the
  lister across multiple channel URLs. [VERIFIED: YouTubeChannelVideoLister.cs line 130 comment]

- **Awaiting `InvokeAsync(StateHasChanged)` inside `IOrchestratorProgress.Report`:** `Report` is
  synchronous by design (`void`, not `Task`). The bridge must fire-and-forget the async call.
  [VERIFIED: OrchestratorProgress.cs]

- **Re-entering harvest/distill while `_operationInFlight = true`:** State machine has one global
  lock. Channel browse during harvest is blocked with an inline note per UI-SPEC.

- **Calling `DistillAsync(dryRun:false)` on a metered provider:** The orchestrator will hard-abort
  with `Success=false, AbortedReason=...`. For the current Studio config (claude backend,
  `isSubscriptionProvider=true`) this path is safe, but the page must surface `AbortedReason`
  if it fires.

- **Not disposing `CancellationTokenSource` in `Dispose()`:** Causes the in-flight harvest/distill
  to continue running after the SignalR circuit drops, consuming server resources.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| YouTube channel video listing | Custom HTTP/scraping | `IYouTubeChannelVideoLister.ListRecentAsync` | Already handles ChannelHandle/ChannelId/ChannelSlug parsing; SemaphoreSlim concurrency guard built in |
| YouTube video ID parsing from URL | Manual regex | `YoutubeExplode.Videos.VideoId.TryParse(rawId)` | Handles `youtube.com/watch?v=`, `youtu.be/`, bare IDs, and more; throws `ArgumentException` on failure — catch that to show user error |
| Harvest/distill logic | Any new harvest or distill code | `IHarvestOrchestrator.HarvestAsync`, `IDistillOrchestrator.DistillAsync` | All logic extracted to Core in Phase 42; adding any new domain logic in the page violates the UI-wrapper contract |
| Progress bridge | Custom IProgress<T> or Channel<T> | `IOrchestratorProgress` + `ActionOrchestratorProgress` local adapter | The synchronous sink design is intentional to avoid async reordering (Phase 42 D-08); wrap in fire-and-forget to `InvokeAsync(StateHasChanged)` |
| Monthly spend enforcement | Custom cap check | `ILlmSpendLedger.WouldExceedCapAsync` | Already accounts for current-month total vs cap; wired into orchestrator for per-call enforcement |
| Video dedup check | SQL query in the page | `IContentVideoStore.GetVideoByYoutubeIdAsync` + `IBlockedVideoStore.IsBlockedAsync` | Correct indices exist; avoids duplicating store logic |

---

## Common Pitfalls

### Pitfall 1: Blazor sync context blocks SignalR circuit (STATE.md Pitfall 7)
**What goes wrong:** Long-running `await _orchestrator.HarvestAsync(...)` runs on the Blazor
synchronization context, freezing the circuit. The operator sees a "loading" spinner forever
or gets disconnected.
**Why it happens:** Blazor Server's synchronization context serializes component rendering and
event handling. Long IO on that context starves the circuit.
**How to avoid:** Always wrap orchestrator calls in `Task.Run(...)`. The component awaits the
returned Task, but the orchestrator executes on a thread pool thread.
**Warning signs:** UI does not update during harvest; "Connection lost" appears in the browser.

### Pitfall 2: AngleSharp concurrency (STATE.md Pitfall 6)
**What goes wrong:** Calling `ListRecentAsync` concurrently (or `GetByIdsAsync` with Task.WhenAll
at concurrency > 1) causes `InvalidOperationException` in `AngleSharp.BrowsingContext.CreateChild`.
**Why it happens:** AngleSharp's HTML parser shares static state. `YouTubeChannelVideoLister`
enforces `MetadataLookupConcurrency = 1` internally, but a second concurrent caller bypasses
this.
**How to avoid:** Never fire two `ListRecentAsync` or `GetByIdsAsync` calls simultaneously.
The `_operationInFlight` lock prevents parallel harvest/distill, and the Browse button is
disabled while any op is in flight. [VERIFIED: YouTubeChannelVideoLister.cs line 130 comment]

### Pitfall 3: Re-distill without explicit confirmation (STATE.md Pitfall 5)
**What goes wrong:** Already-distilled videos are re-queued for distillation silently, incurring
LLM spend to overwrite existing output.
**Why it happens:** `DistillAsync` processes any video with a transcript; it doesn't know whether
the UI warned the operator about existing distill output.
**How to avoid:** The two-checkbox secondary confirmation flow (UI-SPEC Section 4, Stage A) is
the guard. Check `IContentSiteIndexStore.GetByNaturalKeyAsync` for existing distilled status
and show the amber warning before the dry-run button activates. Only pass already-distilled
videos in `videoIds` when the operator has checked both confirmation checkboxes.
**Warning signs:** Distill completes but operator doesn't remember selecting already-distilled
videos; ledger shows unexpected spend.

### Pitfall 4: sourceId required by GetVideoByYoutubeIdAsync
**What goes wrong:** `IContentVideoStore.GetVideoByYoutubeIdAsync(sourceId, ytId)` requires a
`long sourceId`. The component doesn't know which source owns the video before DB lookup.
**Why it happens:** The content_videos table is source-scoped; a YouTube video can exist under
multiple sources if added multiple times.
**How to avoid:** For badge resolution, either (a) load all sources via
`IContentSourceStore.ListSourcesAsync()` on page init and iterate, returning the first match; or
(b) check `ContentSiteIndexStore.GetByNaturalKeyAsync("youtube", ytId)` first (no sourceId
needed) as the proxy for "already in DB". For the `duplicate` badge in the paste queue,
use `GetByNaturalKeyAsync` or iterate sources. See Open Question 1.
**Warning signs:** Badge always shows "Not harvested" even for known-harvested videos.

### Pitfall 5: `GetMonthlyCapUsd()` not yet on `ILlmSpendLedger`
**What goes wrong:** The page tries to call a cap-read method that doesn't exist, causing a
compile error or forcing the page to re-implement the env-var read logic.
**Why it happens:** `SpendLedgerBase.ReadMonthlyCapUsd()` is `private` and the interface exposes
only `WouldExceedCapAsync` and `GetMonthlyTotalAsync`.
**How to avoid:** Add `decimal GetMonthlyCapUsd()` to `ILlmSpendLedger` and promote
`ReadMonthlyCapUsd` to `protected` in `SpendLedgerBase`. This is a small two-line change in Core
before or during Wave 1 of the plan.

### Pitfall 6: Session cap override bypasses orchestrator's internal WouldExceedCapAsync
**What goes wrong:** Operator raises cap via the page override, but `IDistillOrchestrator`
internally calls `WouldExceedCapAsync` on the ledger injected into the orchestrator — if that
ledger instance doesn't see the override, the orchestrator still aborts at the old cap.
**Why it happens:** Studio registers `ILlmSpendLedger` as a singleton with the override resolver
(Pattern 5). `ContentKbOrchestrator` injects `ILlmSpendLedger` — since both use the singleton,
the override is seen by the orchestrator too. This only breaks if Program.cs registers two
separate `ILlmSpendLedger` instances.
**How to avoid:** Register a single `ILlmSpendLedger` singleton that captures the
`SessionCapOverride` reference in its `configurationValueResolver` closure.

---

## Code Examples

### Confirmed: IHarvestOrchestrator.HarvestAsync signature
```csharp
// Source: DeckFlow.Core/Orchestration/IHarvestOrchestrator.cs (verified)
Task<HarvestResult> HarvestAsync(
    int limit,
    IReadOnlyList<string>? videoIds = null,
    long? sourceId = null,
    IOrchestratorProgress? progress = null,
    CancellationToken cancellationToken = default);
```

### Confirmed: IDistillOrchestrator.DistillAsync signature
```csharp
// Source: DeckFlow.Core/Orchestration/IDistillOrchestrator.cs (verified)
Task<DistillResult> DistillAsync(
    int limit,
    bool dryRun,
    bool isSubscriptionProvider,
    IReadOnlyList<string>? videoIds = null,
    IOrchestratorProgress? progress = null,
    CancellationToken cancellationToken = default);
```

### Confirmed: HarvestResult fields
```csharp
// Source: DeckFlow.Core/Orchestration/HarvestResult.cs (verified)
public required bool Success { get; init; }
public int Captions { get; init; }
public int Whisper { get; init; }
public int SkippedNoCaptions { get; init; }
public double WhisperFallbackRatio { get; init; }
public string? Message { get; init; }
```

### Confirmed: DistillResult fields
```csharp
// Source: DeckFlow.Core/Orchestration/DistillResult.cs (verified)
public required bool Success { get; init; }
public int SourcesProcessed { get; init; }
public int VideosDistilled { get; init; }
public int VideosFiltered { get; init; }
public int DistillFailed { get; init; }
public int LlmCalls { get; init; }
public decimal LlmSpendUsd { get; init; }
public int WouldRun { get; init; }
public decimal ProjectedSpendUsd { get; init; }
public IReadOnlyList<string> FailedVideoIds { get; init; } = Array.Empty<string>();
public string? AbortedReason { get; init; }
public bool DryRun { get; init; }
```

### Confirmed: IYouTubeChannelVideoLister signatures
```csharp
// Source: DeckFlow.Core/Integration/IYouTubeChannelVideoLister.cs (verified)
Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(string channelUrl, int limit, CancellationToken ct = default);
Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsAsync(IReadOnlyList<string> videoIds, CancellationToken ct = default);

// GetByIdsAsync accepts YouTube video IDs OR full URLs — YoutubeExplode.VideoId.TryParse handles both.
// Throws ArgumentException on unparseable input. Omits failed/private video IDs silently.
```

### Confirmed: YouTubeChannelVideo model
```csharp
// Source: DeckFlow.Core/Integration/YouTubeChannelVideo.cs (verified)
public required string VideoId { get; init; }    // bare YouTube video ID (e.g. "dQw4w9WgXcQ")
public required string Url { get; init; }
public required string Title { get; init; }
public TimeSpan? Duration { get; init; }
public DateTimeOffset? PublishedUtc { get; init; }
public long? ViewCount { get; init; }
// Note: no thumbnail URL — must construct from VideoId if needed in UI
```

**Thumbnail construction:** YoutubeExplode's `PlaylistVideo` does expose thumbnails during
`ListRecentAsync`, but `YouTubeChannelVideo` record does not capture them (not in the model).
The UI-SPEC requires 40x30px thumbnails. Options: (a) add `ThumbnailUrl` to `YouTubeChannelVideo`
(Core model change); (b) construct the standard YouTube thumbnail URL at render time:
`https://img.youtube.com/vi/{VideoId}/default.jpg`. Option (b) requires no Core change and the
URL is public/stable. See Open Question 2.

### Confirmed: isSubscriptionProvider derivation (CLI pattern to replicate)
```csharp
// Source: DeckFlow.CLI/ContentKbCommandRunners.cs lines 95-97 (verified)
var providerEnv = Environment.GetEnvironmentVariable(LlmDistillationProviderFactory.EnvironmentVariableName);
var isSubscriptionProvider = !string.IsNullOrWhiteSpace(providerEnv)
    && !string.Equals(providerEnv.Trim(), "openai", StringComparison.OrdinalIgnoreCase);
// Current Studio config: DECKFLOW_LLM_PROVIDER defaults to "openai" when unset,
// BUT LlmDistillationProviderFactory defaults to OpenAI when env is null/empty.
// isSubscriptionProvider = false when DECKFLOW_LLM_PROVIDER is unset or "openai".
// isSubscriptionProvider = true when DECKFLOW_LLM_PROVIDER = "claude".
```

**Important:** Current Studio `Program.cs` calls
`LlmDistillationProviderFactory.Resolve(sp.GetRequiredService<HttpClient>())` (the overload that
reads from env). If `DECKFLOW_LLM_PROVIDER` is not set, it defaults to OpenAI. The operator must
set `DECKFLOW_LLM_PROVIDER=claude` in their environment for `isSubscriptionProvider = true`.
This means the page may show "subscription ($0)" only when the env var is set correctly — which
matches the user's actual runtime.

### Confirmed: Studio stores already registered as singletons
```csharp
// Source: DeckFlow.Studio/Program.cs lines 47-56 (verified)
builder.Services.AddSingleton<IContentSourceStore>(_ => new ContentSourceStore(contentKbDatabasePath));
builder.Services.AddSingleton<IContentVideoStore>(_ => new ContentVideoStore(contentKbDatabasePath));
builder.Services.AddSingleton<IContentSiteIndexStore>(_ => new ContentSiteIndexStore(contentKbDatabasePath));
builder.Services.AddSingleton<IBlockedVideoStore>(_ => new BlockedVideoStore(contentKbDatabasePath));
builder.Services.AddSingleton<IContentHarvestRunStore>(_ => new ContentHarvestRunStore(contentKbDatabasePath));
builder.Services.AddSingleton<ILlmSpendLedger>(_ => new LlmSpendLedger(contentKbDatabasePath));
builder.Services.AddSingleton<IYouTubeChannelVideoLister>(sp => new YouTubeChannelVideoLister(...));
// AddContentKbOrchestrator() registers as Scoped — compatible with Blazor Server component injection.
```

**Scoped orchestrator + singleton stores:** The orchestrator is scoped (new instance per circuit).
The stores are singletons (shared across circuits). This is correct — stores handle their own
concurrency internally. The Harvest.razor component can inject both `IHarvestOrchestrator`
(scoped) and `IContentVideoStore` (singleton) without lifecycle conflicts.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|-----------------|--------------|--------|
| Harvest/distill in CLI only | `IContentKbOrchestrator` in Core (Phase 42) | 2026-06-13 | Studio can now call orchestrator without CLI reference |
| Spend cap check only (WouldExceedCapAsync) | `GetMonthlyTotalAsync` also on interface | Phase 49 era | Page can compute remaining budget without re-implementing |
| ADO.NET hand-rolled store queries | Dapper-backed stores (Phase 49) | 2026-06-14 | Store queries are type-safe; no impact on callers |

**Deprecated/outdated:**
- `ContentKbCommandRunners` as the only access path to harvest/distill: superseded by `IContentKbOrchestrator` in Phase 42. CLI now delegates to orchestrator.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Standard YouTube thumbnail URL `https://img.youtube.com/vi/{VideoId}/default.jpg` is stable and publicly accessible for channel-listed videos | Code Examples (thumbnail) | Broken thumbnail images in the UI — low-risk fallback: omit `<img>` or use a placeholder |
| A2 | `DECKFLOW_LLM_PROVIDER=claude` is set in the operator's environment when running Studio for distillation | isSubscriptionProvider pattern | If unset, `isSubscriptionProvider=false`, orchestrator aborts non-dry-run distill with abort message; dry-run still works |
| A3 | `IContentSourceStore` exposes a `ListSourcesAsync()` or equivalent method to enumerate sources for sourceId resolution in badge checks | Open Question 1 | If not exposed, badge check must use `ContentSiteIndexStore.GetByNaturalKeyAsync` as the sole proxy (no sourceId needed), which is actually simpler |

---

## Open Questions

1. **sourceId for GetVideoByYoutubeIdAsync in badge resolution**
   - What we know: `IContentVideoStore.GetVideoByYoutubeIdAsync(sourceId, ytId)` requires a source ID.
     `IContentSiteIndexStore.GetByNaturalKeyAsync("youtube", ytId)` does NOT require a source ID.
   - What's unclear: Is there a cross-source "does this ytId exist anywhere in content_videos?" method
     on `IContentVideoStore`? If not, the simplest badge resolution path is:
     (a) Use `GetByNaturalKeyAsync` to detect "distilled" (implies harvested too), and
     (b) fall back to iterating sources from `IContentSourceStore` to check "harvested" separately.
   - **Recommendation:** Use `GetByNaturalKeyAsync` as the "distilled" signal and derive "harvested"
     from a source-iteration approach. Or add a `GetVideoByYoutubeIdAnySourceAsync(ytId)` method to
     `IContentVideoStore`. The planner should pick one path and note it as a plan decision.

2. **Thumbnail URL for video table**
   - What we know: `YouTubeChannelVideo` has no `ThumbnailUrl` field. UI-SPEC requires 40x30px thumbnails.
   - What's unclear: Whether to add `ThumbnailUrl` to `YouTubeChannelVideo` (Core model change,
     requires `PlaylistVideo.Thumbnails` in `MapVideo`) or construct from VideoId at render time.
   - **Recommendation:** Construct at render time: `https://img.youtube.com/vi/{VideoId}/default.jpg`.
     No Core change needed. If a video thumbnail fails to load, `<img>` shows broken image — add
     `onerror="this.style.display='none'"` as a safety net.

3. **Blazor component injection of scoped orchestrator**
   - What we know: `AddContentKbOrchestrator()` registers as scoped. Blazor Server components can
     inject scoped services via `@inject` or `[Inject]`.
   - What's unclear: Whether the existing `ContentKbOrchestratorSmokeService` (which is also scoped,
     resolved in a `using (var scope ...)` at startup) pattern conflicts with Blazor's component scope.
   - **Recommendation:** Harvest.razor injects `IHarvestOrchestrator` and `IDistillOrchestrator`
     directly via `[Inject]`. Blazor Server creates a new scoped DI scope per circuit; this is the
     correct pattern. No conflict with the startup smoke check.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| dotnet 10 SDK | Build + run Studio | Yes (inferred from working Phase 41/42 builds) | net10.0 | — |
| YoutubeExplode | Channel browse, ID resolution | Yes | 6.6.0 (in bin/) | — |
| SQLite (via Microsoft.Data.Sqlite) | ILlmSpendLedger, stores | Yes | 10.0.0 (in bin/) | — |
| DECKFLOW_LLM_PROVIDER env var | isSubscriptionProvider detection | [ASSUMED] set to "claude" in operator env | — | Defaults to "openai" → isSubscriptionProvider=false → non-dry-run distill blocked |
| MTG_DATA_DIR env var | Studio data directory | [ASSUMED] set per existing Studio setup | — | Falls back to `artifacts/studio/` in cwd (Program.cs line 112) |

**Missing dependencies with no fallback:** None — all required libraries are already bundled.

**Missing dependencies with fallback:**
- `DECKFLOW_LLM_PROVIDER` unset: isSubscriptionProvider=false; dry-run works, execute distill
  blocked by orchestrator guard. Operator must set env var for live distill.

---

## Validation Architecture

`nyquist_validation = true` in `.planning/config.json`. VSTest unreliable in WSL (CLAUDE.md).
Studio has no dedicated test project. Tests must target `DeckFlow.Core.Tests` and
`DeckFlow.Web.Tests` for any Core-side additions (ledger interface change, badge-resolution store
pattern). The Harvest.razor page itself is tested via manual smoke (build clean + browser verify).

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (DeckFlow.Core.Tests + DeckFlow.Web.Tests) |
| Config file | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` |
| Quick run command | `dotnet test DeckFlow.Core.Tests/ -x` |
| Full suite command | `dotnet test DeckFlow.sln --filter "FullyQualifiedName!~DeckFlow.Web.Tests.AdminFeedback"` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| HARV-01 | Channel browse calls `ListRecentAsync(url, limit, ct)` | Manual (browser smoke — Blazor component) | Start Studio, paste channel handle, verify table | N/A — component |
| HARV-02 | `GetByIdsAsync` resolves URLs and bare IDs; invalid IDs show user error | Unit (Core) | `dotnet test DeckFlow.Core.Tests/ -x --filter "YouTubeChannelVideoLister"` | Existing — verify coverage |
| HARV-03 | Badge resolution: blocked → blocked; distilled → distilled; harvested → harvested; neither → not-harvested | Manual (browser smoke) or unit on resolver helper | Browser smoke verify OR extract `ResolveStatusAsync` to testable static helper | ❌ Wave 0 if extracted |
| HARV-04 | Harvest progress reaches UI without circuit freeze; CTS cancels on Dispose | Manual (browser smoke — start harvest, verify log updates live, close tab) | Browser verify | N/A — component |
| HARV-05 | Dry-run result card shows projected spend; re-distill requires two checkbox confirms; actual spend shown post-run | Manual (browser smoke — step through dry-run flow) | Browser verify | N/A — component |
| D-02/D-03 | `GetMonthlyCapUsd()` returns env/default; `SessionCapOverride` raises cap for ledger | Unit (Core + Studio) | `dotnet test DeckFlow.Core.Tests/ -x --filter "LlmSpendLedger"` | ❌ Wave 0 gap for new interface method |

### Sampling Rate

- **Per task commit:** `dotnet build DeckFlow.sln` — zero errors, zero new warnings
- **Per wave merge:** `dotnet test DeckFlow.Core.Tests/` full pass
- **Phase gate:** `dotnet build DeckFlow.sln` clean + browser smoke of all 5 HARV success criteria

### Wave 0 Gaps

- [ ] Unit test for `ILlmSpendLedger.GetMonthlyCapUsd()` new interface method — covers D-02
- [ ] Unit test for `SessionCapOverride` resolver affecting `WouldExceedCapAsync` — covers D-03
- [ ] Manual smoke checklist document (Harvest.razor cannot be unit-tested without bUnit or
      similar; VSTest unreliable in WSL; no test framework for Studio)

*(No Wave 0 framework install needed — xUnit already present in Core.Tests and Web.Tests.
Studio has no test project and none will be added.)*

---

## Security Domain

`security_enforcement` not explicitly set to `false` in config — treated as enabled.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Studio is local-only, single-operator, no auth |
| V3 Session Management | No | SignalR circuit is local-only, no cross-user sessions |
| V4 Access Control | No | Local single-operator tool |
| V5 Input Validation | Yes | YouTube channel URL/handle input; video URL/ID paste; cap override numeric input |
| V6 Cryptography | No | No new crypto in this phase |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| YouTube URL/ID injection via paste queue | Tampering | `YoutubeExplode.VideoId.TryParse` validates before any HTTP call; throws `ArgumentException` on invalid input — catch and show user error |
| Session cap override set to extreme value (e.g., $99999) | Elevation of privilege | Cap override is in-memory only; resets on restart (D-03); no persistent effect; LLM cost risk bounded to operator session |
| Secrets (prod connection string) in log output | Information disclosure | Studio already enforces "configured / not configured" only in logs (Program.cs line 82); Harvest.razor must not log any ledger keys or config values |
| SSRF via channel URL input | Spoofing | All outbound HTTP goes through `YoutubeExplode` (trusted library) which only calls YouTube APIs; Studio is local-only, not a public endpoint |

---

## Sources

### Primary (HIGH confidence)

- Codebase: `DeckFlow.Core/Orchestration/IHarvestOrchestrator.cs` — verified signature
- Codebase: `DeckFlow.Core/Orchestration/IDistillOrchestrator.cs` — verified signature
- Codebase: `DeckFlow.Core/Orchestration/OrchestratorProgress.cs` — verified synchronous-void design
- Codebase: `DeckFlow.Core/Orchestration/HarvestResult.cs` — verified all fields
- Codebase: `DeckFlow.Core/Orchestration/DistillResult.cs` — verified all fields
- Codebase: `DeckFlow.Core/Integration/IYouTubeChannelVideoLister.cs` — verified signatures
- Codebase: `DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs` — verified SemaphoreSlim(1) pattern
- Codebase: `DeckFlow.Core/Integration/LlmDistillationProviderFactory.cs` — verified isSubscriptionProvider derivation logic
- Codebase: `DeckFlow.Core/Content/ILlmSpendLedger.cs` — verified current interface (gap: no GetMonthlyCapUsd)
- Codebase: `DeckFlow.Core/Content/SpendLedgerBase.cs` — verified `ReadMonthlyCapUsd()` is private; `GetMonthlyTotalAsync` is public
- Codebase: `DeckFlow.Studio/Program.cs` — verified all registered singletons, orchestrator DI
- Codebase: `DeckFlow.Studio/DeckFlow.Studio.csproj` — verified no new packages needed; Core ProjectReference present
- Codebase: `DeckFlow.Core/Content/IContentVideoStore.cs` — verified `GetVideoByYoutubeIdAsync` signature requires sourceId
- Codebase: `DeckFlow.Core/Content/IBlockedVideoStore.cs` — verified `IsBlockedAsync` signature
- Codebase: `DeckFlow.Core/Content/IContentSiteIndexStore.cs` — verified `GetByNaturalKeyAsync` (no sourceId needed)
- Codebase: `DeckFlow.Core/Orchestration/ServiceCollectionExtensions.cs` — verified `AddContentKbOrchestrator()` registers as Scoped
- Planning: `.planning/phases/45-harvest-distill-ui/45-CONTEXT.md` — locked decisions D-01..D-06
- Planning: `.planning/phases/45-harvest-distill-ui/45-UI-SPEC.md` — status badge vocabulary, interaction contracts, pitfalls

### Secondary (MEDIUM confidence)

- Planning: `.planning/STATE.md` Pitfalls 5/6/7 — cross-referenced with codebase for accuracy
- Planning: `.planning/ROADMAP.md` Phase 45 detail — confirmed dependency chain

### Tertiary (LOW confidence)

- YouTube thumbnail URL pattern `img.youtube.com/vi/{id}/default.jpg` — standard YouTube pattern,
  not verified against YouTube docs in this session [A1]

---

## Metadata

**Confidence breakdown:**
- Standard Stack: HIGH — all verified in codebase and bin/
- Architecture: HIGH — all orchestrator interfaces verified; pattern derived from existing Phase 42 contracts
- Pitfalls: HIGH — SemaphoreSlim concurrency (code comment), Blazor circuit blocking (STATE.md + well-known pattern), spend re-distill (CONTEXT.md)
- Spend ledger gap: HIGH — interface gap (missing `GetMonthlyCapUsd`) is a confirmed finding from reading the source

**Research date:** 2026-06-15
**Valid until:** 2026-07-15 (stable internal codebase; no upstream API changes expected)
