# Phase 56: Studio Surfaces — Pattern Map

**Mapped:** 2026-06-18
**Files analyzed:** 9 new/modified files
**Analogs found:** 9 / 9

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Core/Content/VideoStatus.cs` | model (enum) | — | `DeckFlow.Core/Content/VideoStatus.cs` (current) | exact — extend in place |
| `DeckFlow.Core/Content/VideoStatusResolver.cs` | service | request-response | `DeckFlow.Core/Content/VideoStatusResolver.cs` (current) | exact — modify in place |
| `DeckFlow.Studio/Pages/Harvest.razor` | component | event-driven | `DeckFlow.Studio/Pages/Harvest.razor` (current) | exact — modify in place |
| `DeckFlow.Studio/Pages/Review.razor` | component | CRUD | `DeckFlow.Studio/Pages/Review.razor` (current) | exact — modify in place |
| `DeckFlow.Studio/Pages/Publish.razor` | component | CRUD | `DeckFlow.Studio/Pages/Publish.razor` (current) | exact — modify in place |
| `DeckFlow.Studio/Pages/Blocked.razor` | component | CRUD | `DeckFlow.Studio/Pages/Review.razor` | role-match (list + per-row action) |
| `DeckFlow.Studio/Shared/NavMenu.razor` | component | — | `DeckFlow.Studio/Shared/NavMenu.razor` (current) | exact — add entry |
| `DeckFlow.Studio/Program.cs` | config | — | `DeckFlow.Studio/Program.cs` (current) | exact — add singleton |
| `DeckFlow.Core.Tests/VideoStatusResolverTests.cs` | test | — | `DeckFlow.Core.Tests/VideoStatusResolverTests.cs` (current) | exact — add cases |
| `DeckFlow.Studio.Tests/BlockedPageTests.cs` | test | — | `DeckFlow.Studio.Tests/ReviewPageTests.cs` | role-match |
| `DeckFlow.Studio.Tests/ReviewPageTests.cs` (extend) | test | — | `DeckFlow.Studio.Tests/ReviewPageTests.cs` (current) | exact — add cases |
| `DeckFlow.Studio.Tests/PublishPageTests.cs` (extend) | test | — | `DeckFlow.Studio.Tests/ReviewPageTests.cs` | role-match |

---

## Pattern Assignments

### `DeckFlow.Core/Content/VideoStatus.cs` (model, enum extension)

**Analog:** `DeckFlow.Core/Content/VideoStatus.cs` lines 1-23 (current file)

**Current enum pattern** (lines 1-23):
```csharp
namespace DeckFlow.Core.Content;

/// <summary>
/// Badge state for a YouTube video as shown in the Harvest page status column.
/// Members map one-to-one to the UI-SPEC badge vocabulary (45-UI-SPEC.md lines 110-126).
/// </summary>
public enum VideoStatus
{
    /// <summary>The video has not been harvested into any enabled source.</summary>
    NotHarvested,

    /// <summary>The video has been harvested and exists in at least one enabled source, but has not been distilled.</summary>
    Harvested,

    /// <summary>The video has been distilled; a content_site_index row exists for it. Implies harvested.</summary>
    Distilled,

    /// <summary>The video is blocked and will be skipped on future harvest runs.</summary>
    Blocked,

    /// <summary>The video is a duplicate of an already-harvested or already-distilled entry (UI-layer signal).</summary>
    Duplicate,
}
```

**Add two new members after `Distilled`, before `Blocked`:**
```csharp
    /// <summary>
    /// The video has been distilled and approved (approval_status = "approved") but not yet
    /// pushed to prod (pushed_to_prod_utc is null OR is_visible = false). (Phase 56)
    /// </summary>
    Approved,

    /// <summary>
    /// The video has been pushed to prod (pushed_to_prod_utc not null) AND is visible
    /// (is_visible = true). Pushed-but-hidden maps to Approved, not Published. (Phase 56)
    /// </summary>
    Published,
```

**XML doc convention:** match the existing `<summary>` pattern. Reference the accepted semantic
(pushed-but-hidden = `Approved`) in the `Published` summary.

---

### `DeckFlow.Core/Content/VideoStatusResolver.cs` (service, request-response)

**Analog:** `DeckFlow.Core/Content/VideoStatusResolver.cs` lines 1-91 (current file)

**Current resolution logic to replace** (lines 64-74):
```csharp
// 2. Distilled: a content_site_index row exists.
// Why: use ContentSourceType.Youtube constant — never the raw string literal (LOW-1).
var indexRow = await _indexStore.GetByNaturalKeyAsync(
    ContentSourceType.Youtube,
    youtubeVideoId,
    ct).ConfigureAwait(false);

if (indexRow is not null)
{
    return VideoStatus.Distilled;
}
```

**Replace with (same store call, extended return logic):**
```csharp
// 2. Index row exists — distinguish Approved/Published/Distilled without extra store calls.
// Why: use ContentSourceType.Youtube constant — never the raw string literal (LOW-1).
var indexRow = await _indexStore.GetByNaturalKeyAsync(
    ContentSourceType.Youtube,
    youtubeVideoId,
    ct).ConfigureAwait(false);

if (indexRow is not null)
{
    // Published: pushed AND visible (pushed-but-hidden shows Approved per accepted semantic —
    // mirrors PublishState.PushedHidden; operator considers it still in limbo).
    if (indexRow.PushedToProdUtc.HasValue && indexRow.IsVisible)
    {
        return VideoStatus.Published;
    }

    // Approved: in KB and admin approved it, but not yet live on prod.
    if (indexRow.ApprovalStatus == "approved")
    {
        return VideoStatus.Approved;
    }

    return VideoStatus.Distilled;
}
```

**Resolution order stays:** Blocked (1) → Published/Approved/Distilled (2) → Harvested (3) → NotHarvested (4).
No new ctor parameters, no new store calls. Only the `if (indexRow is not null)` block changes.

**XML doc update** — replace the `<list>` items in the `<remarks>` to reflect the new 6-state resolution.

---

### `DeckFlow.Studio/Pages/Harvest.razor` (component, event-driven)

**Analog:** `DeckFlow.Studio/Pages/Harvest.razor` (current file, 1514 lines)

**Imports pattern** (lines 1-7, existing — no change needed):
```razor
@page "/harvest"
@implements IDisposable
@using DeckFlow.Core.Content
@using DeckFlow.Core.Integration
@using DeckFlow.Core.Orchestration
@using DeckFlow.Studio.Services
```

**Inject pattern** (lines 684-709, existing — add `IContentMaintenanceOrchestrator`):
```csharp
// Existing injections (keep as-is):
[Inject]
private IYouTubeChannelVideoLister Lister { get; set; } = default!;
[Inject]
private IHarvestOrchestrator HarvestOrchestrator { get; set; } = default!;
[Inject]
private IContentSourceManager SourceManager { get; set; } = default!;
[Inject]
private VideoStatusResolver StatusResolver { get; set; } = default!;

// NEW injection for Phase 56 REM-01:
[Inject]
private IContentMaintenanceOrchestrator MaintenanceOrchestrator { get; set; } = default!;
```

**RenderBadge switch — current pattern to extend** (lines 1450-1458):
```csharp
private static RenderFragment RenderBadge(VideoStatus status) => status switch
{
    VideoStatus.NotHarvested => @<span class="badge bg-secondary">Not harvested</span>,
    VideoStatus.Harvested    => @<span class="badge bg-info text-dark">Harvested</span>,
    VideoStatus.Distilled    => @<span class="badge bg-success">Distilled</span>,
    VideoStatus.Blocked      => @<span class="badge bg-danger">Blocked</span>,
    VideoStatus.Duplicate    => @<span class="badge bg-warning text-dark">Already in DB</span>,
    _                        => @<span class="badge bg-secondary">Unknown</span>,
};
```

**Add two arms before the `_` default (per UI-SPEC badge vocabulary):**
```csharp
    VideoStatus.Approved  => @<span class="badge bg-primary">Approved</span>,
    VideoStatus.Published => @<span class="badge bg-success text-white"><span class="oi oi-check me-1" aria-hidden="true"></span>Published</span>,
```

**RefreshBadgesAsync pattern** (lines 1421-1442 — existing, already handles new status values
once the resolver returns them — NO change needed there):
```csharp
private async Task RefreshBadgesAsync(IReadOnlyList<string> videoIds)
{
    foreach (var videoId in videoIds)
    {
        var newStatus = await StatusResolver.ResolveStatusAsync(videoId);
        var channelVm = _channelVideos.FirstOrDefault(v => v.VideoId == videoId);
        if (channelVm is not null) { channelVm.Status = newStatus; }
        // queue re-applies Duplicate logic...
    }
}
```

**Block action per-row — new state field on VideoViewModel:**
```csharp
// Add to VideoViewModel (inner sealed class):
public bool PendingBlock { get; set; }
```

**Block action pattern — Task.Run + ActionOrchestratorProgress + disposal-safe InvokeAsync**
(copy from existing `HarvestSelectedAsync` at lines 1086-1193):
```csharp
private async Task ConfirmBlockAsync(VideoViewModel vm)
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
            () => MaintenanceOrchestrator.BlockVideoAsync(
                vm.VideoId, reason: null, progress, _cts.Token),
            _cts.Token);

        if (result.Success)
        {
            await RefreshBadgesAsync(new[] { vm.VideoId });
        }
    }
    catch (OperationCanceledException) { }
    finally
    {
        vm.PendingBlock = false;
        _operationInFlight = false;
        await InvokeAsync(StateHasChanged);
    }
}
```

**Block button markup (per UI-SPEC surface contract 3):**
```html
@if (!vm.PendingBlock)
{
    <button class="btn btn-sm btn-outline-danger"
            @onclick="() => vm.PendingBlock = true"
            disabled="@(_operationInFlight || vm.Status == VideoStatus.Blocked)"
            aria-label="Block @vm.Title">
        Block Video
    </button>
}
else
{
    <button class="btn btn-sm btn-danger"
            @onclick="() => ConfirmBlockAsync(vm)"
            disabled="@_operationInFlight"
            aria-label="Confirm block @vm.Title">
        Confirm Block
    </button>
    <button class="btn btn-sm btn-outline-secondary ms-1"
            @onclick="() => vm.PendingBlock = false"
            aria-label="Keep @vm.Title">
        Keep Video
    </button>
    <span class="text-danger small ms-2">This will delete KB artifacts.</span>
}
```

**ADD-01 state fields** (new, after existing `_queueAddError`):
```csharp
private bool _addToQueueDone;
private int _lastAddCount;
```

**ADD-01 "0 resolved" feedback** — set fields in `AddToQueueAsync` after the foreach, then render:
```html
@if (_addToQueueDone && _lastAddCount == 0 && !string.IsNullOrWhiteSpace(_pasteQueueText))
{
    <div class="alert alert-warning py-2 mt-2">
        No videos found for the pasted input. The video may be private, unlisted, or the URL may be invalid.
    </div>
}
```

---

### `DeckFlow.Studio/Pages/Review.razor` (component, CRUD)

**Analog:** `DeckFlow.Studio/Pages/Review.razor` lines 1-694 (current file)

**Inject pattern** (lines 234-239, existing — add `PublishStateDeriver`):
```csharp
// Existing:
[Inject]
private IContentSiteIndexStore IndexStore { get; set; } = default!;
[Inject]
private ContentKbOrchestratorOptions Options { get; set; } = default!;

// NEW for Phase 56 PUB-03:
[Inject]
private PublishStateDeriver Deriver { get; set; } = default!;
```

**`@using` directive to add** (after existing `@using DeckFlow.Core.Knowledge`):
```razor
@using DeckFlow.Core.Content
```

(needed for `PublishState` enum reference in the `RenderPublishStateBadge` switch)

**Table header addition** — after the existing `<th scope="col">Status</th>`, before `<th scope="col">Actions</th>`:
```html
<th scope="col">Publish State</th>
```

**Table cell addition** — after the existing `<td>@RenderApprovalBadge(vm.ApprovalStatus)...</td>` cell, before `<td>` actions cell:
```html
<td>@RenderPublishStateBadge(Deriver.Derive(vm.PushedToProdUtc, vm.IsVisible, vm.IndexedUtc))</td>
```

**New `RenderPublishStateBadge` static method** (add alongside existing `RenderApprovalBadge` at line 609):
```csharp
private static RenderFragment RenderPublishStateBadge(PublishState state) => state switch
{
    PublishState.NeverPublished => @<span class="badge bg-secondary">Never published</span>,
    PublishState.PushedHidden   => @<span class="badge bg-warning text-dark">Pushed-hidden</span>,
    PublishState.Published      => @<span class="badge bg-success text-white"><span class="oi oi-check me-1" aria-hidden="true"></span>Published</span>,
    PublishState.LocalNewer     => @<span class="badge bg-info text-dark">Local-newer</span>,
    _                           => @<span class="badge bg-secondary">Unknown</span>,
};
```

**`ReviewViewModel` — three new properties** (add to the `sealed class ReviewViewModel` at lines 651-693):
```csharp
// Phase 56 PUB-03: publish-state inputs (populated in ctor from ContentSiteIndexRow).
public DateTimeOffset? PushedToProdUtc { get; }
public bool IsVisible { get; }
public DateTimeOffset IndexedUtc { get; }
```

**`ReviewViewModel` ctor additions** (inside constructor after existing assignments):
```csharp
PushedToProdUtc = row.PushedToProdUtc;
IsVisible = row.IsVisible;
IndexedUtc = row.IndexedUtc;
```

**Colspan update** — the expand row currently has `colspan="6"` (line 163). Adding one column makes it `colspan="7"`.

---

### `DeckFlow.Studio/Pages/Publish.razor` (component, CRUD)

**Analog:** `DeckFlow.Studio/Pages/Publish.razor` lines 1-584 (current file)

**Inject pattern** (lines 204-215, existing — add `PublishStateDeriver`):
```csharp
// Existing:
[Inject]
private IGitRepository Git { get; set; } = default!;
[Inject]
private IContentKbOrchestrator Orchestrator { get; set; } = default!;
[Inject]
private IContentSiteIndexStore IndexStore { get; set; } = default!;
[Inject]
private ContentKbOrchestratorOptions Options { get; set; } = default!;

// NEW for Phase 56 PUB-03:
[Inject]
private PublishStateDeriver Deriver { get; set; } = default!;
```

**`@using` directive to add** (after existing `@using DeckFlow.Core.Content`):
```razor
@using DeckFlow.Core.Content
```

(may already exist — verify; if missing add it)

**New state field** (add alongside existing private fields around line 220):
```csharp
// Phase 56 PUB-03: publish-state summary for approved rows.
private List<(PublishState State, int Count)> _publishStateSummary = new();
```

**`OnInitializedAsync` update** — after the `rows.Count` assignment (line 283), compute the summary:
```csharp
// Phase 56 PUB-03: derive publish-state summary from approved rows.
// Why: Task.Run block — compute inside the same Task.Run that fetches rows (no second store call).
var summary = rows
    .GroupBy(r => Deriver.Derive(r.PushedToProdUtc, r.IsVisible, r.IndexedUtc))
    .Select(g => (State: g.Key, Count: g.Count()))
    .OrderBy(x => x.State)
    .ToList();
// (Assign to _publishStateSummary via InvokeAsync or directly — see InvokeAsync pattern below)
```

**Summary display markup** — insert after the `_approvedCount` paragraph (after line 48):
```html
@if (_publishStateSummary.Count > 0)
{
    <div class="d-flex gap-2 flex-wrap mb-2">
        @foreach (var (state, count) in _publishStateSummary)
        {
            <span>@RenderPublishStateBadge(state) <span class="text-muted small">@count</span></span>
        }
    </div>
}
```

**`RenderPublishStateBadge` method** — same static method as in Review.razor (copy exactly):
```csharp
private static RenderFragment RenderPublishStateBadge(PublishState state) => state switch
{
    PublishState.NeverPublished => @<span class="badge bg-secondary">Never published</span>,
    PublishState.PushedHidden   => @<span class="badge bg-warning text-dark">Pushed-hidden</span>,
    PublishState.Published      => @<span class="badge bg-success text-white"><span class="oi oi-check me-1" aria-hidden="true"></span>Published</span>,
    PublishState.LocalNewer     => @<span class="badge bg-info text-dark">Local-newer</span>,
    _                           => @<span class="badge bg-secondary">Unknown</span>,
};
```

---

### `DeckFlow.Studio/Pages/Blocked.razor` (component, CRUD — NEW FILE)

**Analog:** `DeckFlow.Studio/Pages/Review.razor` lines 1-694 (closest role-match: list page with per-row action)

**Page header pattern** (copy from Review.razor lines 1-12, adapt):
```razor
@page "/blocked"
@implements IDisposable
@using DeckFlow.Core.Content
@using DeckFlow.Core.Orchestration

<PageTitle>Blocked Videos</PageTitle>

<h1 class="h4 fw-semibold">Blocked Videos</h1>
<p class="text-muted">Videos blocked from harvest. Unblock to allow re-harvest.</p>

<article class="content px-4">
```

**Loading/error/empty states** (copy from Review.razor lines 14-30, adapt):
```html
@if (_loading)
{
    <div class="d-flex align-items-center gap-2 mt-3">
        <span class="spinner-border spinner-border-sm text-primary"
              role="status"
              aria-label="Operation in progress">
            <span class="visually-hidden">Loading...</span>
        </span>
        <span class="text-muted">Loading blocked videos...</span>
    </div>
}
else if (!string.IsNullOrEmpty(_loadError))
{
    <div class="alert alert-danger py-2 mt-3">Could not load blocked videos — @_loadError</div>
}
else if (_blocked.Count == 0)
{
    <p class="text-muted mt-3">No blocked videos. Block videos from the Harvest page to prevent re-harvest.</p>
}
else
{
    // table (see below)
}
```

**Table pattern** (copy table structure from Review.razor lines 81-97, adapt columns):
```html
<div class="table-responsive mt-3">
    <table class="table table-sm table-hover align-middle">
        <thead class="table-light">
            <tr>
                <th scope="col">Video ID</th>
                <th scope="col" style="width:140px">Blocked at</th>
                <th scope="col">Reason</th>
                <th scope="col" style="width:90px">Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var video in _blocked)
            {
                <tr>
                    <td>
                        <a href="https://youtu.be/@video.YoutubeVideoId"
                           target="_blank"
                           rel="noopener noreferrer">@video.YoutubeVideoId</a>
                    </td>
                    <td>@video.BlockedUtc.ToString("yyyy-MM-dd HH:mm") UTC</td>
                    <td>@(video.Reason ?? "—")</td>
                    <td>
                        <button class="btn btn-sm btn-outline-secondary"
                                @onclick="() => UnblockAsync(video.YoutubeVideoId)"
                                disabled="@_operationInFlight"
                                aria-label="Unblock @video.YoutubeVideoId">
                            Unblock Video
                        </button>
                    </td>
                </tr>
            }
        </tbody>
    </table>
</div>
```

**`@code` block inject pattern** (copy from Review.razor lines 233-239):
```csharp
@code {
    [Inject]
    private IContentMaintenanceOrchestrator MaintenanceOrchestrator { get; set; } = default!;

    // Page state
    private bool _loading = true;
    private string _loadError = string.Empty;
    private bool _operationInFlight;
    private List<BlockedVideoListResult.BlockedVideoListItem> _blocked = new();

    // CTS for disposal-safe cancellation
    private CancellationTokenSource _cts = new();
```

**`OnInitializedAsync` pattern** (copy from Review.razor lines 277-303, adapt):
```csharp
protected override async Task OnInitializedAsync()
{
    try
    {
        // Why: Task.Run moves the store call off the Blazor sync context (Pitfall 4).
        var result = await Task.Run(
            () => MaintenanceOrchestrator.ListBlockedAsync(progress: null, _cts.Token),
            _cts.Token);
        _blocked = result.Items.ToList();
    }
    catch (OperationCanceledException)
    {
        // Component disposed mid-load — swallow.
    }
    catch (Exception ex)
    {
        _loadError = ex.Message;
    }
    finally
    {
        _loading = false;
        await InvokeAsync(StateHasChanged);
    }
}
```

**Unblock action pattern** (no confirmation — recovery action, not destructive):
```csharp
private async Task UnblockAsync(string videoId)
{
    if (_operationInFlight) return;
    _operationInFlight = true;
    try
    {
        await Task.Run(
            () => MaintenanceOrchestrator.UnblockVideoAsync(videoId, progress: null, _cts.Token),
            _cts.Token);
        _blocked.RemoveAll(b => b.YoutubeVideoId == videoId);
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
        // Why: surface failure inline; no page reload needed on unblock error.
        _loadError = $"Unblock failed — {ex.Message}";
    }
    finally
    {
        _operationInFlight = false;
        await InvokeAsync(() =>
        {
            try { StateHasChanged(); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        });
    }
}
```

**`Dispose` pattern** (copy from Review.razor lines 638-648 exactly):
```csharp
public void Dispose()
{
    _cts.Cancel();
    _cts.Dispose();
}
```

---

### `DeckFlow.Studio/Shared/NavMenu.razor` (component)

**Analog:** `DeckFlow.Studio/Shared/NavMenu.razor` lines 1-49 (current file)

**Existing entry pattern** (lines 32-36):
```html
<div class="nav-item px-3">
    <NavLink class="nav-link" href="direct-push">
        <span class="oi oi-data-transfer-upload" aria-hidden="true"></span> Direct Push
    </NavLink>
</div>
```

**Add immediately after the Direct Push entry:**
```html
<div class="nav-item px-3">
    <NavLink class="nav-link" href="blocked">
        <span class="oi oi-ban" aria-hidden="true"></span> Blocked
    </NavLink>
</div>
```

Icon `oi-ban` is already in the Open Iconic set. No CSS changes needed.

---

### `DeckFlow.Studio/Program.cs` (config — add singleton)

**Analog:** `DeckFlow.Studio/Program.cs` lines 53-107 (existing `AddSingleton` registrations)

**Existing pattern** (line 107):
```csharp
builder.Services.AddSingleton<VideoStatusResolver>();
```

**Add immediately after `VideoStatusResolver` (or alongside it):**
```csharp
// Why: PublishStateDeriver is a pure stateless class; singleton is safe and avoids allocation
// per-request. Pages inject it via [Inject] to derive publish state from ContentSiteIndexRow fields.
builder.Services.AddSingleton<PublishStateDeriver>();
```

No other Program.cs changes needed — `IContentMaintenanceOrchestrator` is already available via
`builder.Services.AddContentKbOrchestrator()` at line 106.

---

### `DeckFlow.Core.Tests/VideoStatusResolverTests.cs` (test — add cases)

**Analog:** `DeckFlow.Core.Tests/VideoStatusResolverTests.cs` lines 163-240+ (current test file)

**Existing test structure pattern** (lines 167-182):
```csharp
[Fact]
public async Task ResolveStatusAsync_BlockedVideo_ReturnsBlocked()
{
    // Arrange: blocked=true; other stores would return Distilled if reached — proves blocked wins.
    var resolver = new VideoStatusResolver(
        new FakeBlockedVideoStore(isBlocked: true),
        new FakeSiteIndexStore(row: MakeIndexRow()),
        new FakeSourceStore([MakeSource(1)]),
        new FakeVideoStore(1, "vid001", MakeVideo(1, "vid001")));

    // Act
    var status = await resolver.ResolveStatusAsync("vid001");

    // Assert
    Assert.Equal(VideoStatus.Blocked, status);
}
```

**`MakeIndexRow` helper to extend** (lines 149-161) — new overload with `approvalStatus` and `pushedToProdUtc` params:
```csharp
private static ContentSiteIndexRow MakeIndexRow(
    string approvalStatus = "pending",
    DateTimeOffset? pushedToProdUtc = null,
    bool isVisible = false)
    => new()
    {
        Id = 1L,
        Source = "test-source",
        Title = "Test Video",
        VideoUrl = "https://youtu.be/vid001",
        ArtifactPath = "content-kb/test-source/vid001.md",
        IndexedUtc = DateTimeOffset.UtcNow,
        ArchetypeTags = [],
        BracketTags = [],
        CardCategoryTags = [],
        ApprovalStatus = approvalStatus,
        PushedToProdUtc = pushedToProdUtc,
        IsVisible = isVisible,
    };
```

**New test cases to add:**
```csharp
[Fact]
public async Task ResolveStatusAsync_ApprovedNotPushed_ReturnsApproved()
{
    // Arrange: not blocked + index row with approval_status="approved" + no push.
    var resolver = new VideoStatusResolver(
        new FakeBlockedVideoStore(isBlocked: false),
        new FakeSiteIndexStore(row: MakeIndexRow(approvalStatus: "approved", pushedToProdUtc: null)),
        new FakeSourceStore([MakeSource(1)]),
        new FakeVideoStore(hitSourceId: 0, hitYoutubeVideoId: "", hitResult: null));

    var status = await resolver.ResolveStatusAsync("vid001");

    Assert.Equal(VideoStatus.Approved, status);
}

[Fact]
public async Task ResolveStatusAsync_PushedAndVisible_ReturnsPublished()
{
    // Arrange: not blocked + index row with push timestamp + is_visible=true.
    var resolver = new VideoStatusResolver(
        new FakeBlockedVideoStore(isBlocked: false),
        new FakeSiteIndexStore(row: MakeIndexRow(
            approvalStatus: "approved",
            pushedToProdUtc: DateTimeOffset.UtcNow,
            isVisible: true)),
        new FakeSourceStore([MakeSource(1)]),
        new FakeVideoStore(hitSourceId: 0, hitYoutubeVideoId: "", hitResult: null));

    var status = await resolver.ResolveStatusAsync("vid001");

    Assert.Equal(VideoStatus.Published, status);
}

[Fact]
public async Task ResolveStatusAsync_PushedButHidden_ReturnsApproved()
{
    // Arrange: pushed but is_visible=false → Published badge not shown; shows Approved
    // (accepted semantic: pushed-but-hidden is "still in limbo" — mirrors PublishState.PushedHidden).
    var resolver = new VideoStatusResolver(
        new FakeBlockedVideoStore(isBlocked: false),
        new FakeSiteIndexStore(row: MakeIndexRow(
            approvalStatus: "approved",
            pushedToProdUtc: DateTimeOffset.UtcNow,
            isVisible: false)),
        new FakeSourceStore([MakeSource(1)]),
        new FakeVideoStore(hitSourceId: 0, hitYoutubeVideoId: "", hitResult: null));

    var status = await resolver.ResolveStatusAsync("vid001");

    Assert.Equal(VideoStatus.Approved, status);
}
```

---

### `DeckFlow.Studio.Tests/BlockedPageTests.cs` (test — NEW FILE)

**Analog:** `DeckFlow.Studio.Tests/ReviewPageTests.cs` lines 1-100+ (bUnit harness pattern)

**File header and class pattern** (copy from ReviewPageTests.cs lines 1-15):
```csharp
using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// bUnit behavioral tests for Blocked.razor.
/// Covers REM-01 (block action via orchestrator) and REM-02 (list + unblock).
/// </summary>
public sealed class BlockedPageTests : BunitContext
{
```

**Render helper pattern** (copy from ReviewPageTests.cs lines 50-65, adapt):
```csharp
private IRenderedComponent<Blocked> RenderBlocked(IEnumerable<BlockedVideoListResult.BlockedVideoListItem> blocked)
{
    var fakeMaintenance = new FakeContentKbOrchestrator();
    // Pre-populate ListBlockedAsync canned return:
    fakeMaintenance.CannedBlockedResult = new BlockedVideoListResult
    {
        Items = blocked.ToList().AsReadOnly(),
    };
    Services.AddSingleton<IContentMaintenanceOrchestrator>(fakeMaintenance);
    return RenderComponent<Blocked>();
}
```

Note: `FakeContentKbOrchestrator` implements `IContentMaintenanceOrchestrator` (verified at lines
79-102 of the fake). The `BlockVideoAsync`, `UnblockVideoAsync`, and `ListBlockedAsync` methods
currently `throw new NotImplementedException()` — the plan must wire canned returns for
`ListBlockedAsync` on the fake before `BlockedPageTests.cs` can compile and pass.

**Test case pattern** (copy xUnit `[Fact]` + `WaitForAssertion` style from ReviewPageTests.cs):
```csharp
[Fact]
public void BlockedPage_NoBlockedVideos_ShowsEmptyState()
{
    var cut = RenderBlocked(Array.Empty<BlockedVideoListResult.BlockedVideoListItem>());

    cut.WaitForAssertion(() =>
    {
        var text = cut.Markup;
        Assert.Contains("No blocked videos", text);
    });
}

[Fact]
public void BlockedPage_WithBlockedVideos_ShowsTable()
{
    var videos = new[]
    {
        new BlockedVideoListResult.BlockedVideoListItem { YoutubeVideoId = "abc123", BlockedUtc = DateTimeOffset.UtcNow, Reason = "spam" },
    };
    var cut = RenderBlocked(videos);

    cut.WaitForAssertion(() =>
    {
        Assert.Contains("abc123", cut.Markup);
        Assert.Contains("Unblock Video", cut.Markup);
    });
}
```

---

### `DeckFlow.Studio.Tests/ReviewPageTests.cs` and `PublishPageTests.cs` (extend existing)

**Analog:** `DeckFlow.Studio.Tests/ReviewPageTests.cs` (existing — extend in place)

**`ReviewViewModel` PUB-03 test pattern** (extend `RenderReview` to register `PublishStateDeriver`):
```csharp
// In RenderReview helper, add before Render<Review>():
Services.AddSingleton<PublishStateDeriver>();
```

**New test case — publish state column visible:**
```csharp
[Fact]
public void ReviewPage_PublishStateColumn_ShowsNeverPublishedForUnpushedRow()
{
    var rows = new[] { MakeYoutubeRow(1, "vid1", "approved") };
    // MakeYoutubeRow must leave PushedToProdUtc = null → PublishState.NeverPublished.
    var (cut, _) = RenderReview(rows);

    cut.WaitForAssertion(() =>
    {
        Assert.Contains("Never published", cut.Markup);
    });
}
```

**`PublishPageTests` render helper** (mirror ReviewPageTests pattern):
```csharp
private IRenderedComponent<Publish> RenderPublish(IEnumerable<ContentSiteIndexRow> approvedRows)
{
    var store = new FakeContentSiteIndexStore();
    foreach (var r in approvedRows) store.Rows.Add(r);
    Services.AddSingleton<IContentSiteIndexStore>(store);
    Services.AddSingleton<IContentKbOrchestrator>(new FakeContentKbOrchestrator());
    Services.AddSingleton<IGitRepository>(new FakeGitRepository());
    Services.AddSingleton<PublishStateDeriver>();
    Services.AddSingleton(new ContentKbOrchestratorOptions { ArtifactRoot = "/tmp/art" });
    return RenderComponent<Publish>();
}
```

---

## Shared Patterns

### Task.Run + Disposal-Safe InvokeAsync (ALL new async operations)

**Source:** `DeckFlow.Studio/Pages/Review.razor` lines 277-303 and `Publish.razor` lines 271-307

**Apply to:** Every store/orchestrator call in `Blocked.razor`, every new action in `Harvest.razor`

```csharp
// Pattern: Task.Run wraps I/O; InvokeAsync with disposal-safe catch updates UI state.
var result = await Task.Run(async () =>
{
    await Store.EnsureSchemaAsync(_cts.Token).ConfigureAwait(false);
    return await Store.SomeQueryAsync(_cts.Token).ConfigureAwait(false);
}, _cts.Token);

// For progress-firing multi-step ops, disposal-safe InvokeAsync:
await InvokeAsync(() =>
{
    try { StateHasChanged(); }
    catch (ObjectDisposedException) { }
    catch (InvalidOperationException) { }
});
```

### ActionOrchestratorProgress + _logLines (orchestrator calls that emit progress)

**Source:** `DeckFlow.Studio/Pages/Harvest.razor` lines 1097-1101, `Publish.razor` lines 330-337

**Apply to:** `ConfirmBlockAsync` in `Harvest.razor`, `UnblockAsync` in `Blocked.razor` (if progress wired)

```csharp
var progress = new ActionOrchestratorProgress(msg =>
    InvokeAsync(() =>
    {
        try { _logLines.Add(msg); StateHasChanged(); }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }));
```

### IDisposable + CancellationTokenSource (ALL pages)

**Source:** `DeckFlow.Studio/Pages/Review.razor` lines 638-648

**Apply to:** `Blocked.razor` (new page must implement `IDisposable`)

```csharp
private CancellationTokenSource _cts = new();

public void Dispose()
{
    _cts.Cancel();
    _cts.Dispose();
}
```

### [Inject] Property Pattern (ALL pages)

**Source:** `DeckFlow.Studio/Pages/Review.razor` lines 234-239

**Apply to:** Every `[Inject]` in `Blocked.razor` and new injections in `Harvest.razor`, `Review.razor`, `Publish.razor`

```csharp
[Inject]
private IContentMaintenanceOrchestrator MaintenanceOrchestrator { get; set; } = default!;
```

### Static RenderFragment Switch (badge rendering)

**Source:** `DeckFlow.Studio/Pages/Review.razor` lines 609-614, `Harvest.razor` lines 1450-1458

**Apply to:** new `RenderPublishStateBadge` in `Review.razor` and `Publish.razor`

```csharp
private static RenderFragment RenderApprovalBadge(string status) => status switch
{
    "approved" => @<span class="badge bg-success">Approved</span>,
    "rejected" => @<span class="badge bg-danger">Rejected</span>,
    _          => @<span class="badge bg-secondary">Pending</span>,
};
```

### bUnit WaitForAssertion (ALL Studio tests)

**Source:** `DeckFlow.Studio.Tests/ReviewPageTests.cs` lines 87-91

**Apply to:** Every new `[Fact]` in `BlockedPageTests.cs` and extended tests in `ReviewPageTests.cs`/`PublishPageTests.cs`

```csharp
cut.WaitForAssertion(() =>
{
    var badges = cut.FindAll("ul.nav-tabs button[role='tab'] .badge");
    Assert.Equal(4, badges.Count);
});
```

Why `WaitForAssertion`: bUnit's `Task.Run` inside `OnInitializedAsync` completes asynchronously;
`WaitForAssertion` polls until the assertion passes or times out (default 1s). Never `Assert.*`
directly after `Render<T>()` without waiting.

### In-file Fakes Pattern (Core unit tests)

**Source:** `DeckFlow.Core.Tests/VideoStatusResolverTests.cs` lines 17-119

**Apply to:** new test cases in `VideoStatusResolverTests.cs` — use the existing in-file `FakeSiteIndexStore`, extend `MakeIndexRow` with optional parameters rather than creating a new fake class.

```csharp
// Fake constructed with specific state for the assertion:
new FakeSiteIndexStore(row: MakeIndexRow(approvalStatus: "approved", pushedToProdUtc: null, isVisible: false))
```

---

## No Analog Found

All files have close analogs in the codebase. No entries in this section.

---

## Key Constraints (from CLAUDE.md + RESEARCH.md)

- **No new NuGet packages.** bUnit 2.7.2 and xUnit 2.9.3 are already present.
- **LF line endings.** `.gitattributes` enforces LF; new `.razor` and `.cs` files must be LF.
- **Format gate.** New/changed C# lines must pass `scripts/format-check-changed.sh staged`.
- **Never call `IBlockedVideoStore` directly for block/unblock from UI.** Always route through `IContentMaintenanceOrchestrator` — it owns the safe ordering (block-first, delete-second).
- **Never duplicate publish-state derivation.** Always call `PublishStateDeriver.Derive(...)` — never inline if/else logic from it.
- **Bootstrap 5 utility classes only.** No new CSS files, no scoped `.razor.css` for new pages, no `site-common.css` (that's DeckFlow.Web only).
- **`FakeContentKbOrchestrator` implements `IContentMaintenanceOrchestrator`** (verified: lines 79-102). Its `BlockVideoAsync`, `UnblockVideoAsync`, `ListBlockedAsync` all `throw NotImplementedException` — wire canned returns on the fake before new tests can compile.

---

## Metadata

**Analog search scope:** `DeckFlow.Studio/Pages/`, `DeckFlow.Studio/Shared/`, `DeckFlow.Studio/Program.cs`, `DeckFlow.Core/Content/`, `DeckFlow.Core.Tests/`, `DeckFlow.Studio.Tests/`
**Files scanned:** 11 source files read directly
**Pattern extraction date:** 2026-06-18
