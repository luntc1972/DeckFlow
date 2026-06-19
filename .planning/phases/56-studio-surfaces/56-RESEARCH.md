# Phase 56: Studio Surfaces — Research

**Researched:** 2026-06-18
**Domain:** Blazor Server UI wiring — DeckFlow.Studio, DeckFlow.Core Content/Orchestration stores
**Confidence:** HIGH — all findings verified against actual source files

---

## Summary

Phase 56 is a brownfield wiring phase. Nearly all required domain logic already exists and is
unit-tested in `DeckFlow.Core`; the work is surfacing it in the Studio Blazor UI. The largest
blocks of new work are (1) extending `VideoStatus` enum with `Approved` and `Published` members
and updating `VideoStatusResolver` to drive them from `IContentSiteIndexStore` rows, (2) adding
a new "Blocked Videos" Blazor page, and (3) injecting `PublishStateDeriver` (built in Phase 55)
into the existing Review and Publish pages. The existing `Harvest.razor` already covers the
channel-browse, multi-select harvest, and paste-URL single-video flows from Phase 45 — ADD-01
gap analysis shows no missing capability, only a minor UX gap documented below.

The three stores Studio needs for this phase (`IBlockedVideoStore`, `IContentSiteIndexStore`,
`IContentVideoStore`) are already singleton-registered in `Program.cs`. `IContentMaintenanceOrchestrator`
(block/unblock/list) is already available via `AddContentKbOrchestrator()`. `PublishStateDeriver`
is a pure stateless class in `DeckFlow.Core.Content` and needs only DI registration in
`Program.cs` plus injection into the two pages.

**Primary recommendation:** Wire real code path-by-path; never duplicate status derivation
logic; use `VideoStatusResolver` as the single status query engine across all three browse
surfaces (channel list, queue, pending-distill) and add `Approved`/`Published` members to it.

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| BROWSE-01 | Select a YouTube channel and see a video list | `IYouTubeChannelVideoLister.ListRecentAsync` already called in `Harvest.razor:BrowseChannelAsync`; depth/limit controlled by `_browseLimit`/`_browseSkip` fields |
| BROWSE-02 | Each video shows a DeckFlow pipeline status badge | `VideoStatusResolver.ResolveStatusAsync` already called per-video; needs two new `VideoStatus` members (`Approved`, `Published`) + resolver update to read them from `ContentSiteIndexRow.ApprovalStatus` and `PushedToProdUtc` |
| BROWSE-03 | Multi-select + harvest the selected set | Already wired: `HarvestSelectedAsync` in `Harvest.razor` operates on `GetAllSelectedVideos()` from both channel and queue lists |
| REM-01 | Block a video — hard-delete + blocklist | `IContentMaintenanceOrchestrator.BlockVideoAsync` in `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs:561`; does block-write-first then delete |
| REM-02 | View blocked-videos list + Unblock | `IContentMaintenanceOrchestrator.ListBlockedAsync` + `UnblockVideoAsync`; need a new Blazor page or section |
| ADD-01 | Add single video by URL/ID + harvest | Covered by existing "URL/ID Paste Queue" section in `Harvest.razor:AddToQueueAsync`; single URL is valid input to `GetByIdsAsync` |
| PUB-03 | Show derived publish-state in Review + Publish pages | `PublishStateDeriver.Derive(pushedToProdUtc, isVisible, localIndexedUtc)` built in Phase 55; not yet injected into either page |
</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Channel video listing | Core Integration (`IYouTubeChannelVideoLister`) | Studio UI (`Harvest.razor`) | Network I/O and parsing live in Core; page drives calls via `Task.Run` off sync context |
| Per-video pipeline status badge | Core (`VideoStatusResolver`) | Studio UI (badge render) | Multi-store query logic must be in Core for unit-testability (same rationale as Phase 45) |
| Block/unblock/list-blocked | Core Orchestration (`IContentMaintenanceOrchestrator`) | Studio UI (new Blocked page) | Domain behavior already in Core; page wires it |
| Publish-state derivation | Core (`PublishStateDeriver`) | Studio UI (Review + Publish pages) | Pure function, no Blazor dependency; lives in Core (same rationale as Phase 55) |
| Blocked-videos UI page | Studio UI (new `Pages/Blocked.razor`) | — | No Core changes needed; page calls `ListBlockedAsync` + `UnblockVideoAsync` |
| Publish-state display column | Studio UI (Review.razor + Publish.razor) | — | Read `ContentSiteIndexRow.PushedToProdUtc` + `IsVisible` + `IndexedUtc`; call `PublishStateDeriver` |

---

## Standard Stack

### Core (no new packages — all already in solution)

| Library | Version | Purpose | Source |
|---------|---------|---------|--------|
| Blazor Server | net10.0 (in-box) | Component model, `@inject`, `Task.Run`, `InvokeAsync` | `DeckFlow.Studio.csproj` |
| bUnit | 2.7.2 | Blazor component testing | `DeckFlow.Studio.Tests.csproj` |
| xUnit | 2.9.3 | Test runner for bUnit tests | `DeckFlow.Studio.Tests.csproj` |

All domain logic uses existing `DeckFlow.Core` types. No new NuGet packages required.

**Installation:** none needed.

---

## Package Legitimacy Audit

No new packages for this phase. Existing packages (`bUnit 2.7.2`, `xUnit 2.9.3`) were
approved in Phase 46. No audit action required.

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

---

## Architecture Patterns

### System Architecture Diagram

```
Harvest.razor (existing)
  BrowseChannelAsync ──→ IYouTubeChannelVideoLister.ListRecentAsync(channelUrl, limit, skip)
                     ──→ VideoStatusResolver.ResolveStatusAsync(videoId)  ← NEW: returns Approved/Published
                     ──→ RenderBadge(vm.Status)                           ← NEW: 2 additional cases

  HarvestSelectedAsync ──→ IHarvestOrchestrator.HarvestAsync(videoIds, sourceId)  [unchanged]

  AddToQueueAsync ──→ IYouTubeChannelVideoLister.GetByIdsAsync(singleId)  [already works]

[NEW] Blocked.razor
  OnInitializedAsync ──→ IContentMaintenanceOrchestrator.ListBlockedAsync()
  UnblockAsync(id)   ──→ IContentMaintenanceOrchestrator.UnblockVideoAsync(id)

Review.razor (existing — add column)
  OnInitializedAsync ──→ IContentSiteIndexStore.GetAllRowsAsync()
  [NEW] derive state ──→ PublishStateDeriver.Derive(row.PushedToProdUtc, row.IsVisible, row.IndexedUtc)

Publish.razor (existing — add column)
  OnInitializedAsync ──→ IContentSiteIndexStore.GetApprovedRowsAsync()
  [NEW] derive state ──→ PublishStateDeriver.Derive(row.PushedToProdUtc, row.IsVisible, row.IndexedUtc)
```

### Recommended Project Structure

```
DeckFlow.Core/Content/
├── VideoStatus.cs          # Add Approved + Published members
├── VideoStatusResolver.cs  # Update ResolveStatusAsync to return Approved/Published
├── PublishState.cs         # Already built (Phase 55)
├── PublishStateDeriver.cs  # Already built (Phase 55)

DeckFlow.Studio/Pages/
├── Harvest.razor           # Add block-action per row; update badge render (2 new cases)
├── Review.razor            # Add PublishState column
├── Publish.razor           # Add PublishState column
├── Blocked.razor           # NEW — list blocked videos, unblock action

DeckFlow.Studio/Program.cs  # Register PublishStateDeriver as singleton
```

---

## Investigation Target Findings

### 1. Core Block/Unblock/List/Delete Methods

**All four operations go through `IContentMaintenanceOrchestrator`.**

`IContentMaintenanceOrchestrator` (`DeckFlow.Core/Orchestration/IContentMaintenanceOrchestrator.cs`):

```csharp
// [VERIFIED: source file]
Task<ContentMaintenanceResult> BlockVideoAsync(
    string youtubeVideoId,
    string? reason,
    IOrchestratorProgress? progress = null,
    CancellationToken cancellationToken = default);

Task<ContentMaintenanceResult> UnblockVideoAsync(
    string youtubeVideoId,
    IOrchestratorProgress? progress = null,
    CancellationToken cancellationToken = default);

Task<BlockedVideoListResult> ListBlockedAsync(
    IOrchestratorProgress? progress = null,
    CancellationToken cancellationToken = default);
```

`DeleteVideoByYoutubeIdAsync` is on `IContentVideoStore` (`DeckFlow.Core/Content/IContentVideoStore.cs:147`) but it is **called internally by `BlockVideoAsync`** — Studio does not call it directly.

`BlockVideoAsync` implementation (`ContentKbOrchestrator.cs:561`):
1. Writes block row first (`AddBlockAsync`) — prevents delete-but-reharvest race
2. Calls `DeleteVideoByYoutubeIdAsync` — removes content rows + FK-cascaded children
3. Calls `GetByNaturalKeyAsync` + `DeleteByIdAsync` — removes site-index row

**`BlockedVideo` record** (`IBlockedVideoStore.cs:49`):
```csharp
// [VERIFIED: source file]
public sealed record BlockedVideo
{
    public required string YoutubeVideoId { get; init; }
    public string? Reason { get; init; }
    public required DateTimeOffset BlockedUtc { get; init; }
}
```

`IContentMaintenanceOrchestrator` is already available in Studio via `AddContentKbOrchestrator()`
and the scoped DI forwarding in `ServiceCollectionExtensions.cs:27`. Studio pages inject it via
`[Inject] private IContentMaintenanceOrchestrator MaintenanceOrchestrator { get; set; }`.

**`IBlockedVideoStore` is also directly registered** as a singleton in `Program.cs:59`. The
`Blocked.razor` page can inject either the orchestrator façade (for block/unblock) or the store
directly (for listing). The orchestrator façade is preferred for block/unblock (wraps error
handling + progress); direct store injection is acceptable for `ListBlockedAsync` reads.

### 2. The Harvest Lister

`IYouTubeChannelVideoLister` (`DeckFlow.Core/Integration/`):

```csharp
// [VERIFIED: source file]
Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(
    string channelUrl,   // URL, id, handle (@Handle), or slug
    int limit,           // max videos (Harvest.razor default: 25, max: 200)
    int skip = 0,        // skip N most-recent before listing
    CancellationToken ct = default);

Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsAsync(
    IReadOnlyList<string> videoIds,
    CancellationToken ct = default);

Task<IReadOnlyList<YouTubeChannelVideo>> ListPlaylistAsync(
    string playlistUrl,
    int limit,
    int skip = 0,
    CancellationToken ct = default);
```

**`YouTubeChannelVideo` record** (`DeckFlow.Core/Integration/YouTubeChannelVideo.cs`):
```csharp
// [VERIFIED: source file]
public sealed record YouTubeChannelVideo
{
    public required string VideoId { get; init; }
    public required string Url { get; init; }
    public required string Title { get; init; }
    public TimeSpan? Duration { get; init; }
    public DateTimeOffset? PublishedUtc { get; init; }
    public long? ViewCount { get; init; }
    public string? ChannelId { get; init; }
    public string? ChannelTitle { get; init; }
}
```

**Concurrency constraint (CRITICAL):** `MetadataLookupConcurrency = 1` (`YouTubeChannelVideoLister.cs:158`).
YoutubeExplode's `AngleSharp.BrowsingContext.CreateChild` is not safe under concurrent access even
with separate `YoutubeClient` instances. All lister calls must be wrapped in `Task.Run` (as they
already are in `Harvest.razor`) to move off the Blazor sync context, but must NOT be parallelized
across videos.

**Depth/pagination:** `limit` controls how many videos are fetched; `skip` skips the first N
most-recent. These map 1:1 to `_browseLimit` (default 25, max 200) and `_browseSkip` in
`Harvest.razor`. No server-side pagination cursor exists — this is a full-fetch-then-slice model.

**Channel URL forms accepted:** channel URL, `@Handle`, handle without `@`, channel ID, channel slug.
Playlist URLs are detected by `list=` or `playlist?` in the input string.

### 3. PublishStateDeriver / Publish-State (Phase 55)

`PublishStateDeriver` (`DeckFlow.Core/Content/PublishStateDeriver.cs`):

```csharp
// [VERIFIED: source file]
public sealed class PublishStateDeriver
{
    public PublishState Derive(
        DateTimeOffset? pushedToProdUtc,
        bool isVisible,
        DateTimeOffset localIndexedUtc) { ... }
}
```

`PublishState` enum members and display strings (`DeckFlow.Core/Content/PublishState.cs`):

| Enum member | `ToDisplayString()` | Condition |
|-------------|--------------------|----|
| `NeverPublished` | `"Never published"` | `pushedToProdUtc` is null |
| `PushedHidden` | `"Pushed-hidden"` | pushed, but `!isVisible` |
| `Published` | `"Published"` | pushed, visible, local not newer |
| `LocalNewer` | `"Local-newer"` | pushed, visible, local strictly after push |

**Caller contract:** pass `row.PushedToProdUtc`, `row.IsVisible`, `row.IndexedUtc` from a
`ContentSiteIndexRow`. All three fields exist on the row after Phase 55.

**`PublishStateDeriver` is NOT yet registered in `DeckFlow.Studio/Program.cs`** (grep confirms
no reference in source pages). Plan must add `builder.Services.AddSingleton<PublishStateDeriver>()`
before first use.

### 4. content_site_index Store — Relevant Columns

`ContentSiteIndexRow` (`DeckFlow.Core/Knowledge/ContentArtifactSpec.cs`):

```csharp
// [VERIFIED: source file]
public DateTimeOffset? PushedToProdUtc { get; init; }  // Phase 55, line ~133
public required DateTimeOffset IndexedUtc { get; init; }
public bool IsVisible { get; init; }
public bool IsHidden { get; init; }
public string ApprovalStatus { get; init; } = "pending";  // "pending"|"approved"|"rejected"
public DateTimeOffset? PublishedUtc { get; init; }  // YouTube publish date — NOT prod push time
public string? YoutubeVideoId { get; init; }
```

`IContentSiteIndexStore.GetAllRowsAsync()` — used by Review page.
`IContentSiteIndexStore.GetApprovedRowsAsync()` — used by Publish page.
Both return fully-populated rows including `PushedToProdUtc` and `IndexedUtc`.

### 5. Videos Store — Harvested vs Distilled

**Harvested:** a row exists in `content_videos` (any enabled source). Query:
`IContentVideoStore.GetVideoByYoutubeIdAsync(sourceId, youtubeVideoId)` — not null.

**Distilled:** a row exists in `content_site_index`. Query:
`IContentSiteIndexStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, youtubeVideoId)` — not null.

**Approved:** `content_site_index` row exists AND `row.ApprovalStatus == "approved"`.

**Published:** `content_site_index` row exists AND `row.PushedToProdUtc` is not null AND
`row.IsVisible == true` AND local `IndexedUtc` ≤ `PushedToProdUtc`.

`VideoStatusResolver` currently returns only: `NotHarvested`, `Harvested`, `Distilled`, `Blocked`.
It does **not** return `Approved` or `Published` — these two states are **new** in Phase 56.

### 6. DeckFlow.Studio Existing Pages

Current pages and their DI composition:

| Page | Route | Key Injected Services |
|------|-------|----------------------|
| `Home.razor` | `/` | — |
| `Harvest.razor` | `/harvest` | `IYouTubeChannelVideoLister`, `IHarvestOrchestrator`, `IContentSourceManager`, `VideoStatusResolver`, `IDistillOrchestrator`, `StudioDistillConfig`, `SessionCapOverride`, `ILlmSpendLedger` |
| `Review.razor` | `/review` | `IContentSiteIndexStore`, `ContentKbOrchestratorOptions` |
| `Publish.razor` | `/publish` | `IGitRepository`, `IContentKbOrchestrator`, `IContentSiteIndexStore`, `ContentKbOrchestratorOptions` |
| `DirectPush.razor` | `/direct-push` | `StudioConfig`, `IProdStoreFactory`, `ISshArtifactUploader`, `IContentKbOrchestrator`, `IContentSiteIndexStore`, `ContentKbOrchestratorOptions` |

**DI registration pattern:** all stores and orchestrators are registered as singletons in
`Program.cs` (except orchestrator slices which are scoped via `AddContentKbOrchestrator()`).
Pages use Blazor `[Inject]` attribute on private properties.

**On-demand store-factory pattern (Phase 47):** `IProdStoreFactory` creates prod-pointing
`IContentSiteIndexStore` on first use for `DirectPush.razor`. Not relevant to Phase 56 —
Phase 56 operates on the local store only.

**Component structure pattern:**
- `@implements IDisposable` + `CancellationTokenSource _cts` in every page
- `Task.Run(...)` to move all store/network calls off the Blazor sync context
- `InvokeAsync(StateHasChanged)` (with disposal-safe try/catch) for progress updates
- `ActionOrchestratorProgress` for streaming log lines
- Mutable `private sealed class *ViewModel` records inside the `@code` block
- Lifecycle via `protected override async Task OnInitializedAsync()`

**NavMenu entries** (`Shared/NavMenu.razor`): Home, Harvest, Review, Publish, Direct Push.
A "Blocked" entry will need to be added to `NavMenu.razor`.

### 7. DeckFlow.Studio.Tests — bUnit Harness Pattern

**Test project:** `DeckFlow.Studio.Tests.csproj` — `net10.0`, bUnit 2.7.2, xUnit 2.9.3.

**Pattern** (from `ReviewPageTests.cs`):
```csharp
// [VERIFIED: source file]
public sealed class HarvestBlockTests : BunitContext
{
    private (IRenderedComponent<Blocked> Cut, FakeContentSiteIndexStore Store) RenderBlocked(...)
    {
        var store = new FakeContentSiteIndexStore();
        Services.AddSingleton<IContentSiteIndexStore>(store);
        Services.AddSingleton<IContentMaintenanceOrchestrator>(new FakeMaintenanceOrchestrator());
        // ... register all required services
        var cut = RenderComponent<Blocked>();
        return (cut, store);
    }
}
```

**Existing test doubles** in `DeckFlow.Studio.Tests/TestDoubles/`:
- `FakeContentSiteIndexStore` — full interface implementation with call-tracking; includes `StampCalls` for PUB-01 assertions
- `FakeContentKbOrchestrator` — covers harvest/distill/export/maintenance orchestrator methods
- `FakeGitRepository`, `FakeProdStoreFactory`, `FakeSshArtifactUploader`

`FakeContentSiteIndexStore` already tracks `StampCalls`, `SingleApprovalCalls`, `BatchApprovalCalls`.
A `FakeContentMaintenanceOrchestrator` (for block/unblock) may not exist yet — check at plan time
whether `FakeContentKbOrchestrator` implements `IContentMaintenanceOrchestrator` or a separate
fake is needed.

### 8. Status Computation — Badge State Mapping

The requirements specify six badge states for the channel-browse status column. Mapping each
to the precise data condition:

| Badge | VideoStatus enum member | Data condition |
|-------|------------------------|----------------|
| `Not harvested` | `VideoStatus.NotHarvested` | Not in `content_videos` for any enabled source AND not blocked |
| `Harvested` | `VideoStatus.Harvested` | In `content_videos`, no `content_site_index` row, not blocked |
| `Distilled` | `VideoStatus.Distilled` | `content_site_index` row exists, `approval_status = "pending"` or `"rejected"`, not blocked |
| `Approved` | **NEW** `VideoStatus.Approved` | `content_site_index` row with `approval_status = "approved"`, `pushed_to_prod_utc` null, not blocked |
| `Published` | **NEW** `VideoStatus.Published` | `content_site_index` row, `approval_status = "approved"`, `pushed_to_prod_utc` not null, `is_visible = true` |
| `Blocked` | `VideoStatus.Blocked` | In `blocked_videos` — wins over all others |

**Key design decision:** `VideoStatusResolver.ResolveStatusAsync` must be extended to return
`Approved` and `Published`. The resolver already fetches the `ContentSiteIndexRow` at step 2
(`GetByNaturalKeyAsync`). Adding the two new states only requires reading `row.ApprovalStatus`
and `row.PushedToProdUtc`/`row.IsVisible` from the already-fetched row — no extra store calls.

**Important separation of concerns:**
- `VideoStatus` (6-state enum) = pipeline badge for the channel-browse view
- `PublishState` (4-state enum) = derived publish state for Review/Publish pages
- These are **separate** enums serving separate UI surfaces. Do not conflate them.

**Resolution order for updated `VideoStatusResolver`** (first match wins):
1. `IsBlockedAsync` → `Blocked`
2. `GetByNaturalKeyAsync` → if found:
   a. `row.PushedToProdUtc != null && row.IsVisible` → `Published`
   b. `row.ApprovalStatus == "approved"` → `Approved`
   c. else → `Distilled`
3. `GetVideoByYoutubeIdAsync` on enabled sources → `Harvested`
4. else → `NotHarvested`

**Gap in spec (flag for planning):** the requirements say `Published` badge appears when the
video is pushed. But `is_visible = true` is set by the site admin, not automatically on push.
A pushed-but-hidden video would show `Approved` (not `Published`) by this resolution order.
This is likely the correct operator-facing behavior (mirrors `PushedHidden` in `PublishState`),
but the planner should confirm or document the accepted semantics.

### 9. ADD-01 Gap Analysis

**Existing paste-URL flow** (`Harvest.razor:AddToQueueAsync`):
- Accepts one or more video URLs/IDs (one per line) in a textarea
- Calls `Lister.GetByIdsAsync(idLines)` to resolve each to a `YouTubeChannelVideo`
- Resolves status badge via `StatusResolver.ResolveStatusAsync`
- Adds resolved videos to `_queueVideos` for selection and harvest

**Single-URL case:** pasting a single YouTube URL (e.g. `https://youtu.be/abc123`) works
as-is — `GetByIdsAsync` accepts a single-element list. The metadata (title, published date,
ChannelId) is populated from the YouTube watch page.

**Gap found:** when a single URL is pasted, `ChannelId` is populated from the metadata, so
`HarvestSelectedAsync` can resolve the channel source via `EnsureYoutubeSourceAsync`. This
works correctly. However, if `GetByIdsAsync` fails for a private/unlisted video, it silently
omits the video (error-suppression is intentional per `YouTubeChannelVideoLister.cs:138-143`).
The operator gets no explicit "could not find" feedback in the current UI — the queue table
simply stays empty. ADD-01 polish = surface a clearer "0 videos resolved" message when the
paste queue returns empty after a non-empty input.

**Conclusion:** ADD-01 is confirmation + minor UX polish, not a new feature. Estimated effort: small.

### 10. CSS / Styling Constraints for Studio

Studio uses Bootstrap 5 (bundled via `wwwroot/css/bootstrap/bootstrap.min.css`) plus
`wwwroot/css/site.css`. It does NOT share `site-common.css` or the guild theme CSS from
`DeckFlow.Web`. CLAUDE.md's `site-common.css` layout rule applies only to `DeckFlow.Web`.

Studio styling conventions (from existing pages):
- Bootstrap utility classes only (`btn`, `badge`, `alert`, `card`, `table-sm`, etc.)
- No custom CSS beyond `site.css` (Blazor scoped CSS used only for layout partials)
- Badge colors: `bg-secondary` (not harvested), `bg-info text-dark` (harvested),
  `bg-success` (distilled/approved), `bg-danger` (blocked), `bg-warning text-dark` (duplicate)
- New badge colors for `Approved` and `Published` must be chosen by the planner
  — suggested: `bg-primary` for Approved, `bg-success text-light` for Published (distinct from
  Distilled's `bg-success` — consider adding a checkmark or different label)

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Publish-state derivation in Review/Publish | Custom if/else in page `@code` | `PublishStateDeriver.Derive(...)` | Phase 55 built the single source of truth; duplication violates T-55-02-04 |
| Block/unblock/delete | Direct store calls from the page | `IContentMaintenanceOrchestrator.BlockVideoAsync/UnblockVideoAsync` | Orchestrator owns delete ordering (block-first, then delete) to prevent reharvest race |
| Status resolution | New ad-hoc store queries in page | `VideoStatusResolver.ResolveStatusAsync` | Single engine; all badge logic in one place; testable in isolation |
| Channel video listing | New HTTP code | `IYouTubeChannelVideoLister.ListRecentAsync` | Already handles concurrency, AngleSharp serialization, metadata enrichment |

**Key insight:** all domain logic already exists and is tested; this phase is wiring, not invention.

---

## Common Pitfalls

### Pitfall 1: Adding `Approved`/`Published` to VideoStatus but not updating `VideoStatusResolver`

**What goes wrong:** The enum has new members but `ResolveStatusAsync` never returns them;
all formerly-approved/published entries show `Distilled`.

**How to avoid:** Update both `VideoStatus.cs` AND `VideoStatusResolver.ResolveStatusAsync`
in the same plan/commit. Add unit tests to `VideoStatusResolverTests.cs` covering both new states.

### Pitfall 2: Duplicating publish-state derivation logic in Review/Publish pages

**What goes wrong:** Page `@code` blocks inline `if (row.PushedToProdUtc != null)` logic
instead of calling `PublishStateDeriver.Derive`. Logic drifts from the Core deriver.

**How to avoid:** Inject `PublishStateDeriver` and call `Deriver.Derive(row.PushedToProdUtc, row.IsVisible, row.IndexedUtc)` — never write the four-state logic inline.

### Pitfall 3: Calling Block without Orchestrator (wrong delete order)

**What goes wrong:** Page calls `IBlockedVideoStore.AddBlockAsync` + `IContentVideoStore.DeleteVideoByYoutubeIdAsync` directly. If delete fails after block-write, video is blocked but KB artifacts are not deleted (orphaned content). If block-write order is reversed, a delete-success + block-fail leaves a deletable-but-not-blocked video.

**How to avoid:** Always call `IContentMaintenanceOrchestrator.BlockVideoAsync`. It owns the safe ordering (block row first, then delete).

### Pitfall 4: Forgetting `Task.Run` for store calls in new Blazor pages

**What goes wrong:** `await blockStore.ListBlockedAsync()` called directly on the Blazor sync context; blocks SignalR circuit; spinner never renders.

**How to avoid:** Every store or orchestrator call in a new page must be wrapped in `Task.Run(...)`. Mirror `Harvest.razor` and `Review.razor` pattern exactly.

### Pitfall 5: Badge rendering not extended for new VideoStatus values

**What goes wrong:** `RenderBadge(vm.Status)` switch expression in `Harvest.razor:1450` hits the `_` default and shows `"Unknown"` badge for `Approved`/`Published` videos.

**How to avoid:** Add `VideoStatus.Approved` and `VideoStatus.Published` cases to the switch in the same plan that adds the enum members. Add a test that the switch arm is exhaustive.

### Pitfall 6: PublishStateDeriver not registered in Studio Program.cs

**What goes wrong:** `[Inject] private PublishStateDeriver Deriver` fails with DI exception at runtime.

**How to avoid:** Plan must include a task to add `builder.Services.AddSingleton<PublishStateDeriver>()` in `Program.cs` before wiring the pages.

### Pitfall 7: NavMenu not updated for new Blocked page

**What goes wrong:** `Blocked.razor` is unreachable from the Studio UI. The operator must type the URL manually.

**How to avoid:** Add a `NavLink` entry to `Shared/NavMenu.razor` in the same plan as the new page.

---

## Code Examples

### Calling BlockVideoAsync from a Blazor page

```csharp
// Source: DeckFlow.Core/Orchestration/IContentMaintenanceOrchestrator.cs + Harvest.razor pattern
[Inject]
private IContentMaintenanceOrchestrator MaintenanceOrchestrator { get; set; } = default!;

private async Task BlockVideoAsync(string videoId, string? reason)
{
    _operationInFlight = true;
    try
    {
        var progress = new ActionOrchestratorProgress(msg =>
            InvokeAsync(() =>
            {
                try { _logLines.Add(msg); StateHasChanged(); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }));

        var result = await Task.Run(
            () => MaintenanceOrchestrator.BlockVideoAsync(videoId, reason, progress, _cts.Token),
            _cts.Token);

        if (result.Success)
        {
            // refresh channel list badge for this video
            await RefreshBadgesAsync(new[] { videoId });
        }
    }
    catch (OperationCanceledException) { }
    finally
    {
        _operationInFlight = false;
        await InvokeAsync(StateHasChanged);
    }
}
```

### Deriving publish state for a Review row

```csharp
// Source: DeckFlow.Core/Content/PublishStateDeriver.cs (Phase 55)
[Inject]
private PublishStateDeriver PublishDeriver { get; set; } = default!;

// In ReviewViewModel constructor (after row load):
PublishState = PublishDeriver.Derive(
    row.PushedToProdUtc,
    row.IsVisible,
    row.IndexedUtc);

// Display (use extension method from PublishState.cs):
@row.PublishState.ToDisplayString()
```

### Updated VideoStatusResolver resolution order

```csharp
// Source: DeckFlow.Core/Content/VideoStatusResolver.cs — updated logic
public async Task<VideoStatus> ResolveStatusAsync(string youtubeVideoId, CancellationToken ct = default)
{
    if (await _blockedStore.IsBlockedAsync(youtubeVideoId, ct))
        return VideoStatus.Blocked;

    var indexRow = await _indexStore.GetByNaturalKeyAsync(
        ContentSourceType.Youtube, youtubeVideoId, ct);

    if (indexRow is not null)
    {
        // NEW: distinguish Approved and Published from Distilled
        if (indexRow.PushedToProdUtc.HasValue && indexRow.IsVisible)
            return VideoStatus.Published;
        if (indexRow.ApprovalStatus == "approved")
            return VideoStatus.Approved;
        return VideoStatus.Distilled;
    }

    var sources = await _sourceStore.ListEnabledSourcesAsync(ct);
    foreach (var source in sources)
    {
        var video = await _videoStore.GetVideoByYoutubeIdAsync(source.Id, youtubeVideoId, ct);
        if (video is not null)
            return VideoStatus.Harvested;
    }

    return VideoStatus.NotHarvested;
}
```

### bUnit test pattern for new Blocked page

```csharp
// Source: DeckFlow.Studio.Tests/ReviewPageTests.cs pattern
public sealed class BlockedPageTests : BunitContext
{
    private IRenderedComponent<Blocked> RenderBlocked(IEnumerable<BlockedVideo> blocked)
    {
        var fake = new FakeBlockedVideoStore(blocked);
        var fakeMaintenance = new FakeContentMaintenanceOrchestrator();
        Services.AddSingleton<IBlockedVideoStore>(fake);
        Services.AddSingleton<IContentMaintenanceOrchestrator>(fakeMaintenance);
        return RenderComponent<Blocked>();
    }
}
```

---

## Runtime State Inventory

This phase adds new UI to Studio but does not rename anything. The `blocked_videos` table
already exists (Phase 37.6). No runtime state migration required.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | `blocked_videos` table in local `content-kb.db` | None — already exists and populated |
| Live service config | Studio runs locally; no prod service config change | None |
| OS-registered state | None | None — verified |
| Secrets/env vars | No new env vars | None |
| Build artifacts | None | None |

---

## Open Questions (RESOLVED)

1. **`Published` badge when `is_visible = false` but `pushed_to_prod_utc` not null**
   - What we know: `PublishState.PushedHidden` handles this for Review/Publish pages
   - What's unclear: for the channel-browse badge (`VideoStatus`), should a pushed-but-hidden
     entry show `Published` or something else (e.g. `Approved`)?
   - Recommendation: use "pushed AND visible" as the `Published` badge condition (consistent
     with `PublishState.Published`); pushed-but-hidden shows `Approved` (still in limbo from
     operator perspective). Document the accepted semantics in the plan.
   - **RESOLVED:** pushed-but-hidden surfaces as `Approved`; `Published` requires pushed AND
     visible. The accepted badge vocabulary is locked in `56-UI-SPEC.md` (badge vocabulary
     section) and implemented by `VideoStatusResolver` per 56-01 (BROWSE-02).

2. **Should the Block action appear in Harvest.razor channel list or be on a dedicated page?**
   - What we know: Harvest.razor already has per-row actions; adding "Block" inline is the
     lowest-friction UX. A dedicated `Blocked.razor` page is needed for REM-02 (list + unblock).
   - Recommendation: add "Block" button as a per-row action in the channel-browse table in
     `Harvest.razor`; create `Blocked.razor` for the list/unblock view. Both plans can be
     in the same wave.
   - **RESOLVED:** both placements ship — the inline per-row "Block" action lands in
     `Harvest.razor` (56-04, REM-01) and the dedicated list/unblock view ships as
     `Blocked.razor` (56-03, REM-02). The 4-plan structure realizes this split.

3. **Does `FakeContentKbOrchestrator` already implement `IContentMaintenanceOrchestrator`?**
   - What we know: `IContentKbOrchestrator : IHarvestOrchestrator, IDistillOrchestrator, IContentMaintenanceOrchestrator, IContentSourceManager, IContentIndexExporter` — so `FakeContentKbOrchestrator` likely implements all slices
   - Recommendation: verify at plan time; if the fake already covers `BlockVideoAsync/UnblockVideoAsync/ListBlockedAsync` then no new fake is needed for the new page's tests.
   - **RESOLVED:** verified in source — `FakeContentKbOrchestrator` implements
     `IContentMaintenanceOrchestrator` (`ListBlockedAsync`/`BlockVideoAsync`/`UnblockVideoAsync`
     all present but currently `throw new NotImplementedException()`). No new fake is needed; the
     56-03 tests wire a canned `ListBlockedAsync` return on the existing fake (see 56-PATTERNS.md
     Blocked.razor test helper).

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | bUnit 2.7.2 + xUnit 2.9.3 |
| Config file | `DeckFlow.Studio.Tests.csproj` (no separate jest/vitest config) |
| Quick run command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj -nologo` |
| Full suite command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -nologo` |
| Core unit tests | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -nologo` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| BROWSE-02 | Channel-browse badge shows `Approved` and `Published` | unit | `dotnet test DeckFlow.Core.Tests` (VideoStatusResolverTests) | ❌ Wave 0 — new test cases |
| REM-01 | Block action hard-deletes + records in blocklist | unit | `dotnet test DeckFlow.Studio.Tests` (BlockedPageTests.Block_HardDeletes) | ❌ Wave 0 |
| REM-02 | Blocked list loads + Unblock removes row | unit bUnit | `dotnet test DeckFlow.Studio.Tests` (BlockedPageTests.UnblockAsync_RemovesRow) | ❌ Wave 0 — new page |
| ADD-01 | Paste single URL → resolves video → adds to queue | unit bUnit | `dotnet test DeckFlow.Studio.Tests` (HarvestPageTests) | ❌ Wave 0 — gap test |
| PUB-03 | Review page shows publish-state column | unit bUnit | `dotnet test DeckFlow.Studio.Tests` (ReviewPageTests) | ❌ Wave 0 — new column |
| PUB-03 | Publish page shows publish-state column | unit bUnit | `dotnet test DeckFlow.Studio.Tests` (PublishPageTests) | ❌ Wave 0 — new column |

### Sampling Rate

- **Per task commit:** `dotnet build DeckFlow.sln -nologo` (0 errors)
- **Per wave merge:** `dotnet test DeckFlow.Core.Tests DeckFlow.Studio.Tests`
- **Phase gate:** Full solution test suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `DeckFlow.Core.Tests/Content/VideoStatusResolverTests.cs` — add Approved + Published state cases
- [ ] `DeckFlow.Studio.Tests/BlockedPageTests.cs` — covers REM-01 (block action) + REM-02 (list/unblock)
- [ ] Extend `ReviewPageTests.cs` — publish-state column visibility
- [ ] Extend `PublishPageTests.cs` — publish-state column visibility
- [ ] `FakeContentMaintenanceOrchestrator` or verify `FakeContentKbOrchestrator` covers block/unblock — needed before `BlockedPageTests.cs` can compile

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Studio is local-only (no auth surface) |
| V3 Session Management | no | Blazor Server circuit; no custom session |
| V4 Access Control | partial | Studio has no auth — all pages are operator-accessible by design; not a concern for local-only tool |
| V5 Input Validation | yes | Block reason, channel URL, paste queue input — validate before passing to Core methods |
| V6 Cryptography | no | No crypto operations |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal via video ID in block reason | Tampering | `ArgumentException.ThrowIfNullOrWhiteSpace` in Core orchestrator already validates `youtubeVideoId`; reason is stored as-is (no path operations) |
| Crash on empty block reason | Tampering | `reason` parameter is `string?` — null is valid; orchestrator accepts it |
| Stale badge after block action | Information Disclosure | Re-call `StatusResolver.ResolveStatusAsync` after `BlockVideoAsync` succeeds to refresh the badge (same pattern as post-harvest badge refresh) |
| Progress message leaking internal paths | Information Disclosure | Use `progress?.Report(...)` with operator-safe copy; do not echo `exception.Message` to progress (mirrors Phase 47 HIGH-2 pattern) |

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All compilation | ✓ | 10.x (active) | — |
| `DeckFlow.Core` | All domain logic | ✓ | In-solution reference | — |
| `DeckFlow.Studio` | Blazor app | ✓ | In-solution reference | — |
| `content-kb.db` (SQLite) | `IBlockedVideoStore`, `IContentSiteIndexStore` | ✓ | Exists locally at `artifacts/studio/content-kb.db` | — |
| YoutubeExplode (in DeckFlow.Core) | `IYouTubeChannelVideoLister` | ✓ | Already a Core dependency | — |

No missing dependencies.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `FakeContentKbOrchestrator` already implements `IContentMaintenanceOrchestrator` (block/unblock/list) | Studio Tests | If it doesn't, a new `FakeContentMaintenanceOrchestrator` must be created before `BlockedPageTests.cs` compiles |
| A2 | `VideoStatus.Approved` and `VideoStatus.Published` members do not yet exist | Investigation 8 | If they already exist (added in a recent commit), the plan can skip the enum extension step |

**If this table is empty:** All claims in this research were verified — these two items are genuine unknowns that should be confirmed at plan time with a quick `grep`.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| CLI-only block/unblock (Phase 37.6) | Studio UI via `IContentMaintenanceOrchestrator` | Phase 56 | Operator no longer needs CLI access to block bad videos |
| No publish-state in Studio (Phase 46/47) | `PublishStateDeriver` + column in Review/Publish | Phase 55 → 56 | Operators can see at a glance what has been pushed to prod |
| `VideoStatus` stops at `Distilled` | `Approved` + `Published` states added | Phase 56 | Channel browse shows full pipeline progression without navigating to Review page |

---

## Sources

### Primary (HIGH confidence)

All findings verified against actual source files in the repository.

- `DeckFlow.Core/Content/IBlockedVideoStore.cs` — block/unblock interface signatures
- `DeckFlow.Core/Orchestration/IContentMaintenanceOrchestrator.cs` — orchestrator façade
- `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs:561-714` — BlockVideoAsync, UnblockVideoAsync, ListBlockedAsync implementations
- `DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs` + `IYouTubeChannelVideoLister.cs` — lister interface and concurrency constraints
- `DeckFlow.Core/Content/VideoStatus.cs` + `VideoStatusResolver.cs` — current badge resolution
- `DeckFlow.Core/Content/PublishState.cs` + `PublishStateDeriver.cs` — Phase 55 deriver
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` — store interface with `StampPushedToProdAsync`
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs:120-167` — `ContentSiteIndexRow` shape incl. `PushedToProdUtc`
- `DeckFlow.Studio/Pages/Harvest.razor` — full existing page (1514 lines); ADD-01 gap analysis
- `DeckFlow.Studio/Pages/Review.razor` — existing Review page
- `DeckFlow.Studio/Pages/Publish.razor` — existing Publish page
- `DeckFlow.Studio/Program.cs` — DI registrations; confirms `PublishStateDeriver` not yet registered
- `DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs` — bUnit fake patterns
- `DeckFlow.Studio.Tests/ReviewPageTests.cs` — bUnit harness pattern
- `DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj` — bUnit 2.7.2 + xUnit 2.9.3
- `.planning/phases/55-publish-state-foundation/55-02-SUMMARY.md` — Phase 55 deriver API confirmation
- `.planning/phases/55-publish-state-foundation/55-01-SUMMARY.md` — `PushedToProdUtc` column details

---

## Metadata

**Confidence breakdown:**
- Standard Stack: HIGH — all types verified in source
- Architecture: HIGH — all interface signatures verified
- Pitfalls: HIGH — all verified from actual code patterns and previous phase notes
- ADD-01 gap analysis: HIGH — full Harvest.razor source read

**Research date:** 2026-06-18
**Valid until:** 2026-07-18 (stable — DeckFlow.Core interfaces move slowly)
