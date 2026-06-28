using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;

namespace DeckFlow.Studio.Pages;

/// <summary>
/// Code-behind for the Harvest + Distill page (HARV-01). Extracted from the
/// inline @code block (H1 god-component split). Behavior unchanged.
/// </summary>
public partial class Harvest
{
    // ── Injected services ───────────────────────────────────────────────────
    [Inject]
    private IYouTubeChannelVideoLister Lister { get; set; } = default!;

    [Inject]
    private IHarvestOrchestrator HarvestOrchestrator { get; set; } = default!;

    [Inject]
    private IContentSourceManager SourceManager { get; set; } = default!;

    [Inject]
    private VideoStatusResolver StatusResolver { get; set; } = default!;

    // Why: Injected now so the inject block is stable; distill wiring lands in Wave 4 (Plan 04).
    [Inject]
    private IDistillOrchestrator DistillOrchestrator { get; set; } = default!;

    [Inject]
    private StudioDistillConfig DistillConfig { get; set; } = default!;

    [Inject]
    private SessionCapOverride CapOverride { get; set; } = default!;

    [Inject]
    private ILlmSpendLedger SpendLedger { get; set; } = default!;

    [Inject]
    private IContentMaintenanceOrchestrator MaintenanceOrchestrator { get; set; } = default!;

    [Inject]
    private AutoApproveSettingsStore AutoApproveSettingsStore { get; set; } = default!;

    // Why: the swappable auto-approve decision (clip count vs cutoff, D-01/D-02) — the one-click
    // and manual Stage B paths both resolve this to decide which distilled videos auto-approve.
    [Inject]
    private IAutoApproveSignal AutoApproveSignal { get; set; } = default!;

    // Why: batch approval setter (D-09) — the shared auto-approve step flips approval_status to
    // 'approved' for at/above-cutoff distilled videos; only approval_status is mutated.
    [Inject]
    private IContentSiteIndexStore IndexStore { get; set; } = default!;

    [Inject]
    private ICreatorSourceStore CreatorStore { get; set; } = default!;

    [Inject]
    private ISkippedVideoStore SkippedStore { get; set; } = default!;

    // ── Section 1 state ─────────────────────────────────────────────────────
    // SRC-02: saved creators for the browse dropdown. Selecting one fills _channelInput; the
    // URL/handle input remains the one-off fallback when no creator is selected.
    private List<CreatorSource> _creators = new();
    private string _selectedCreatorRef = string.Empty;
    // HSEL-01/02: default to not-yet-harvested only (toggle to show all); skipped ids are always
    // excluded from selection. Loaded fresh per browse.
    private bool _showAllVideos;
    private HashSet<string> _skippedVideoIds = new();
    // SUI-05: browse-list creator filter. Empty string = "All creators". Folded into
    // GetVisibleChannelVideos() so Select-All and the harvested set both respect it (T-62-03).
    private string _browsCreatorFilter = string.Empty;
    private string _channelInput = string.Empty;
    private int _browseLimit = 25;
    private int _browseSkip;
    private bool _isBrowsingChannel;
    private bool _channelBrowseDone;
    private string _channelBrowseError = string.Empty;
    private List<VideoViewModel> _channelVideos = new();
    private bool _allChannelSelected;
    // Why: tracks the last successfully-browsed channel URL so harvest can auto-ensure a source
    // for it. Empty string means no channel has been browsed yet in this session.
    private string _lastBrowsedChannel = string.Empty;

    // ── Section 2 state ─────────────────────────────────────────────────────
    private string _pasteQueueText = string.Empty;
    private bool _isAddingToQueue;
    private string _queueAddError = string.Empty;
    private bool _addToQueueDone;
    private int _lastAddCount;
    private string _lastAddInput = string.Empty;
    private List<VideoViewModel> _queueVideos = new();
    private bool _allQueueSelected;

    // ── Section 3 harvest state ──────────────────────────────────────────────
    private CancellationTokenSource? _cts;
    private bool _operationInFlight;
    private List<string> _logLines = new();
    private string _blockError = string.Empty;
    private HarvestResult? _harvestResult;
    private bool _harvestCancelled;
    private bool _focusConfirmPending;
    private ElementReference _confirmBlockButton;

    // ── One-click harvest→auto-distill→auto-approve outcome (AUTO-01 / D-11) ─────────────────
    // The single per-video outcome summary card reads these. Every count maps to a canonical source:
    //   N harvested        = _outcomeHarvestReadyCount (ListPendingDistillAsync ∩ selected)
    //   M distilled        = DistillResult.VideosDistilled
    //   K auto-approved    = ApplyAutoApproveAsync return (rows actually flipped to 'approved')
    //   L left-in-review   = M - K
    //   D dropped          = DistillResult.VideosFiltered
    //   F failed (+ ids)   = DistillResult.DistillFailed / FailedVideoIds
    private bool _showOutcomeCard;
    private int _outcomeHarvestReadyCount;
    private int _outcomeAutoApprovedCount;
    private DistillResult? _oneClickDistillResult;
    // Why: subscription-only inline distill (D-08 AMENDED). On a metered provider the one-click
    // action harvests but does NOT distill — it surfaces this message and points to the manual section.
    private string _oneClickMeteredMessage = string.Empty;

    // ── Section 4 distill state ──────────────────────────────────────────────
    // Cap display (D-02): loaded at page init and refreshed after distill.
    private decimal _monthlyCap;
    private decimal _monthlySpent;
    private string _capRaiseInput = string.Empty;

    // Auto-approve settings (D-04/D-05/D-06/D-07): persisted on/off + clip cutoff. Loaded at page
    // init from AutoApproveSettingsStore, saved on commit (not keystroke). Default ON/5.
    private AutoApproveSettings _autoApproveSettings = AutoApproveSettings.Default;

    // Re-distill double-confirm (T-45-10 / Pitfall 3).
    // _redistillCheck1: "Re-distill already-distilled videos"
    // _redistillCheck2: "Yes, I understand — overwrite existing distill output"
    // redistillConfirmed (computed in markup): _redistillCheck1 && _redistillCheck2
    private bool _redistillCheck1;
    private bool _redistillCheck2;

    // Stage A dry-run state.
    private bool _distillDryRunInFlight;
    private List<string> _distillLogLines = new();
    private DistillResult? _distillDryRunResult;

    // Stage B live-distill state.
    private bool _distillSpendConfirmed;
    private bool _distillLiveInFlight;
    private DistillResult? _distillLiveResult;
    private bool _distillCancelled;

    // Stage B timing (quick task 260615-t7m): live elapsed clock + total time after completion.
    private Stopwatch? _distillStopwatch;
    private TimeSpan? _distillTotalElapsed;
    private CancellationTokenSource? _distillTickerCts;

    // DB-backed pending-distill loader (quick task 260615-p4d): survives an app/circuit
    // restart so harvested-but-not-distilled videos can be selected without re-browsing.
    private List<VideoViewModel> _pendingDistillVideos = new();
    private bool _pendingLoaded;
    private bool _loadingPending;
    private bool _allPendingSelected;
    private string _pendingDistillMessage = string.Empty;

    // ── Section 1: Channel or Playlist Browse (HARV-01) ────────────────────
    private async Task BrowseChannelAsync()
    {
        if (_operationInFlight || _isBrowsingChannel || string.IsNullOrWhiteSpace(_channelInput))
        {
            return;
        }

        _isBrowsingChannel = true;
        _channelBrowseError = string.Empty;
        _channelBrowseDone = false;
        _channelVideos.Clear();
        // SUI-05: reset creator filter when browsing a new channel so stale A-filter doesn't carry over.
        _browsCreatorFilter = string.Empty;

        var input = _channelInput.Trim();
        // Why: detect a playlist URL by the presence of "list=" or "playlist?" in the input.
        // Channel URLs and handles never contain these tokens.
        var isPlaylist = input.Contains("list=", StringComparison.OrdinalIgnoreCase)
            || input.Contains("playlist?", StringComparison.OrdinalIgnoreCase);

        // Why: CTS created per-operation so cancel-on-dispose works for browse too.
        using var browseCts = new CancellationTokenSource();
        try
        {
            // Why: Task.Run moves the lister off the Blazor sync context (Pattern 2 — Pitfall 1).
            // AngleSharp requires serialized access; lister enforces SemaphoreSlim(1) internally.
            var videos = await Task.Run(
                () => isPlaylist
                    ? Lister.ListPlaylistAsync(input, _browseLimit, _browseSkip, browseCts.Token)
                    : Lister.ListRecentAsync(input, _browseLimit, _browseSkip, browseCts.Token),
                browseCts.Token);

            // Resolve badges for each video at list-build time (HARV-03).
            foreach (var v in videos)
            {
                var status = await StatusResolver.ResolveStatusAsync(v.VideoId, browseCts.Token);
                _channelVideos.Add(new VideoViewModel(v.VideoId, v.Url, v.Title, v.PublishedUtc, status, v.ChannelId, v.ChannelTitle));
            }

            // HSEL-02: load the skip set so skipped candidates are filtered out of selection.
            // Own try/catch — a store failure must NOT reach the browse catch, whose message
            // includes ex.Message and could leak the content-kb.db path (D-07). On failure the
            // filter just doesn't hide skips this round (non-fatal).
            try
            {
                var skipped = await SkippedStore.ListSkippedAsync(browseCts.Token);
                _skippedVideoIds = skipped.Select(s => s.YoutubeVideoId).ToHashSet(StringComparer.Ordinal);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                _skippedVideoIds = new();
            }

            // Why: record last successfully-browsed channel (not playlist) so it can serve as
            // a fallback source hint for videos that lack ChannelId. Do NOT overwrite with a
            // playlist URL — a playlist spans channels and is not a valid channel source URL.
            if (!isPlaylist)
            {
                _lastBrowsedChannel = input;
            }

            _channelBrowseDone = true;
        }
        catch (OperationCanceledException)
        {
            _channelBrowseError = "Browse was cancelled.";
        }
        catch (Exception ex)
        {
            _channelBrowseError = $"Could not fetch — {ex.Message}. Check the URL format and try again.";
        }
        finally
        {
            _isBrowsingChannel = false;
            _allChannelSelected = false;
        }
    }

    // HSEL-01/02 (Codex HIGH): the single canonical visible projection. Default shows only
    // not-yet-harvested videos; "Show all" reveals every status; skipped ids are ALWAYS excluded.
    // SUI-05: creator filter also folded in here (T-62-03) so Select-All and the harvested set
    // both respect it — a row hidden by creator filter can never be harvested.
    // Rendering, Select-All, and the harvested set all route through this so a row hidden by the
    // filter or by skip can never be harvested even if it was selected before being hidden.
    private IReadOnlyList<VideoViewModel> GetVisibleChannelVideos()
        => HarvestPlanner.FilterVisibleChannelVideos(
            _channelVideos, _skippedVideoIds, _showAllVideos, _browsCreatorFilter);

    // Markup-facing alias for the canonical visible projection (render loop + empty-state gate).
    private IReadOnlyList<VideoViewModel> VisibleChannelVideos => GetVisibleChannelVideos();

    private void ToggleAllChannelSelections()
    {
        _allChannelSelected = !_allChannelSelected;
        foreach (var vm in GetVisibleChannelVideos())
        {
            vm.Selected = _allChannelSelected;
        }
    }

    // HSEL-02: skip a candidate so it no longer surfaces in selection. Distinct from Block —
    // writes only the skip list (no artifact delete, no blocklist).
    private async Task SkipVideoAsync(VideoViewModel vm)
    {
        if (_operationInFlight)
        {
            return;
        }

        try
        {
            await SkippedStore.AddSkipAsync(vm.VideoId, null);
            _skippedVideoIds.Add(vm.VideoId);
            vm.Selected = false;
        }
        catch (Exception)
        {
            // Why: never echo exception.Message (may leak the DB path) — generic operator-safe copy only.
            _blockError = "Could not skip the video. Try again.";
        }

        await InvokeAsync(StateHasChanged);
    }

    // ── Section 2: URL/ID Paste Queue (HARV-02) ─────────────────────────────
    private async Task AddToQueueAsync()
    {
        if (_operationInFlight || _isAddingToQueue || string.IsNullOrWhiteSpace(_pasteQueueText))
        {
            return;
        }

        _addToQueueDone = false;
        _lastAddCount = 0;
        var pasteAtStart = _pasteQueueText;
        _lastAddInput = pasteAtStart;

        var rawLines = _pasteQueueText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (rawLines.Count == 0)
        {
            return;
        }

        _isAddingToQueue = true;
        _queueAddError = string.Empty;

        using var addCts = new CancellationTokenSource();
        try
        {
            // Why: a pasted playlist URL is expanded via ListPlaylistAsync (bounded by Count); the
            // remaining lines resolve as individual video ids/urls through GetByIdsAsync. Lister calls
            // are serialized (no Task.WhenAll) to honor AngleSharp's single-thread constraint (Pitfall 6).
            var playlistLines = rawLines
                .Where(l => l.Contains("list=", StringComparison.OrdinalIgnoreCase)
                    || l.Contains("playlist?", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var idLines = rawLines.Except(playlistLines).ToList();
            var limit = _browseLimit;
            var videos = await Task.Run(
                async () =>
                {
                    var collected = new List<YouTubeChannelVideo>();
                    foreach (var pl in playlistLines)
                    {
                        collected.AddRange(await Lister.ListPlaylistAsync(pl, limit, 0, addCts.Token).ConfigureAwait(false));
                    }
                    if (idLines.Count > 0)
                    {
                        collected.AddRange(await Lister.GetByIdsAsync(idLines.AsReadOnly(), addCts.Token).ConfigureAwait(false));
                    }
                    return (IReadOnlyList<YouTubeChannelVideo>)collected;
                },
                addCts.Token);

            int added = 0;
            foreach (var v in videos)
            {
                // Skip if already in queue (by VideoId).
                if (_queueVideos.Any(q => q.VideoId == v.VideoId))
                {
                    continue;
                }

                var status = await StatusResolver.ResolveStatusAsync(v.VideoId, addCts.Token);

                // Why: the paste queue shows Duplicate badge for already-in-DB videos (HARV-02).
                // Duplicate = Harvested or Distilled — a pre-harvest warning, not auto-exclusion.
                var displayStatus = (status == VideoStatus.Harvested || status == VideoStatus.Distilled)
                    ? VideoStatus.Duplicate
                    : status;

                _queueVideos.Add(new VideoViewModel(v.VideoId, v.Url, v.Title, v.PublishedUtc, displayStatus, v.ChannelId, v.ChannelTitle));
                added++;
            }

            _lastAddCount = added;
            _pasteQueueText = string.Empty;
        }
        catch (ArgumentException ex)
        {
            // Why: GetByIdsAsync throws ArgumentException on unparseable input (T-45-06).
            _queueAddError = $"Could not fetch — {ex.Message}";
        }
        catch (OperationCanceledException)
        {
            _queueAddError = "Queue add was cancelled.";
        }
        catch (Exception ex)
        {
            _queueAddError = $"Could not fetch — {ex.Message}";
        }
        finally
        {
            _addToQueueDone = true;
            _isAddingToQueue = false;
            _allQueueSelected = false;
        }
    }

    private void RemoveFromQueue(VideoViewModel vm)
    {
        _queueVideos.Remove(vm);
    }

    private void ToggleAllQueueSelections()
    {
        _allQueueSelected = !_allQueueSelected;
        foreach (var vm in _queueVideos)
        {
            vm.Selected = _allQueueSelected;
        }
    }

    // ── Section 3: Harvest Trigger (HARV-04) ────────────────────────────────
    private IReadOnlyList<VideoViewModel> GetAllSelectedVideos()
        // Codex HIGH: only VISIBLE channel videos count — a row selected then hidden by the
        // unharvested filter or by skip must not be harvested.
        => HarvestPlanner.CombineSelected(GetVisibleChannelVideos(), _queueVideos);

    // ── DB-backed pending-distill loader (quick task 260615-p4d) ─────────────

    /// <summary>
    /// Combines the session-selected videos with the selected DB-backed pending-distill videos,
    /// de-duplicating by VideoId so a video that is both browsed and pending counts once.
    /// Used by the Distill section only — the Harvest section keeps <see cref="GetAllSelectedVideos"/>.
    /// </summary>
    private IReadOnlyList<VideoViewModel> GetAllSelectedForDistill()
        => HarvestPlanner.CombineForDistill(GetAllSelectedVideos(), _pendingDistillVideos);

    /// <summary>
    /// Loads harvested-but-not-yet-distilled videos across all enabled sources from the local DB so
    /// the operator can distill them after an app/circuit restart without re-browsing a channel.
    /// </summary>
    private async Task LoadPendingDistillAsync()
    {
        if (_operationInFlight || _loadingPending)
        {
            return;
        }

        _loadingPending = true;
        _pendingDistillMessage = string.Empty;

        // Why: a short DB read — do NOT reuse an in-flight distill CTS. A fresh local CTS keeps
        // this load cancel-on-dispose-safe without tying it to a running distill operation.
        using var pendingCts = new CancellationTokenSource();
        try
        {
            var pending = await Task.Run(
                () => DistillOrchestrator.ListPendingDistillAsync(pendingCts.Token),
                pendingCts.Token);

            _pendingDistillVideos = pending
                .Select(p => new VideoViewModel(p.YoutubeVideoId, p.VideoUrl, p.Title, p.PublishedUtc, VideoStatus.Harvested)
                {
                    Selected = false,
                })
                .ToList();
            _pendingLoaded = true;
            _allPendingSelected = false;

            if (_pendingDistillVideos.Count == 0)
            {
                _pendingDistillMessage = "No harvested videos pending distill.";
            }
        }
        catch (OperationCanceledException)
        {
            _pendingDistillMessage = "Loading pending-distill videos was cancelled.";
        }
        catch (Exception ex)
        {
            _pendingDistillMessage = $"Could not load pending-distill videos — {ex.Message}";
        }
        finally
        {
            _loadingPending = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void ToggleAllPendingSelections()
    {
        _allPendingSelected = !_allPendingSelected;
        foreach (var vm in _pendingDistillVideos)
        {
            vm.Selected = _allPendingSelected;
        }
    }

    private async Task HarvestSelectedAsync()
    {
        if (_operationInFlight)
        {
            return;
        }

        var selectedVideos = GetAllSelectedVideos();
        if (selectedVideos.Count == 0)
        {
            return;
        }

        _operationInFlight = true;
        _logLines.Clear();
        _harvestResult = null;
        _harvestCancelled = false;
        // Clear any prior one-click outcome so the plain harvest path shows only its own status.
        _showOutcomeCard = false;
        _oneClickMeteredMessage = string.Empty;
        _cts = new CancellationTokenSource();

        try
        {
            await RunHarvestCoreAsync(selectedVideos, BuildHarvestProgress());
        }
        catch (OperationCanceledException)
        {
            _logLines.Add("Harvest cancelled.");
            _harvestCancelled = true;
        }
        finally
        {
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// One-click harvest → auto-distill (subscription only) → auto-approve (AUTO-01 / AUTO-02).
    /// SUBSCRIPTION path: harvest, then distill exactly the harvest-ready ids returned by
    /// <c>ListPendingDistillAsync</c> ∩ selected (D-10 / HIGH #2), then apply the shared auto-approve
    /// step. METERED path (D-08 AMENDED): harvest only — Core refuses live metered distill
    /// (ContentKbOrchestrator.cs:244), so this surfaces a requires-subscription message and points the
    /// operator to the manual Distill section, with no DistillAsync or approval call (SC4).
    /// </summary>
    private async Task HarvestAndAutoDistillAsync()
    {
        if (_operationInFlight)
        {
            return;
        }

        var selectedVideos = GetAllSelectedVideos();
        if (selectedVideos.Count == 0)
        {
            return;
        }

        _operationInFlight = true;
        _logLines.Clear();
        _harvestResult = null;
        _harvestCancelled = false;
        _showOutcomeCard = false;
        _oneClickDistillResult = null;
        _oneClickMeteredMessage = string.Empty;
        _outcomeHarvestReadyCount = 0;
        _outcomeAutoApprovedCount = 0;
        _cts = new CancellationTokenSource();

        var progress = BuildHarvestProgress();
        var selectedIds = selectedVideos.Select(v => v.VideoId).ToList();

        try
        {
            // (2) Always harvest first — both paths run the harvest body.
            var harvestOk = await RunHarvestCoreAsync(selectedVideos, progress);

            // (1) D-08 GATE: a metered provider does NOT live-distill (Core refuses at line 244).
            // Surface the requires-subscription message and stop before distill — no silent spend.
            if (!DistillConfig.IsSubscriptionProvider)
            {
                _oneClickMeteredMessage =
                    "Live distill requires a subscription provider. Harvest completed; use the Distill section below to preview/confirm.";
                return;
            }

            if (!harvestOk)
            {
                // Harvest itself failed/partial — the harvest status line already explains; do not distill.
                return;
            }

            // (3) Obtain harvest-ready ids: ListPendingDistillAsync ∩ selected (HIGH #2). Excludes
            // skipped/no-caption and already-distilled videos (which are not pending-distill).
            var pending = await Task.Run(
                () => DistillOrchestrator.ListPendingDistillAsync(_cts.Token),
                _cts.Token);
            var selectedSet = new HashSet<string>(selectedIds, StringComparer.Ordinal);
            var harvestReadyIds = pending
                .Select(p => p.YoutubeVideoId)
                .Where(selectedSet.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            _outcomeHarvestReadyCount = harvestReadyIds.Count;

            if (harvestReadyIds.Count == 0)
            {
                // Nothing distillable — show the outcome card with harvested/0-distilled and stop.
                _oneClickDistillResult = new DistillResult { Success = true };
                _showOutcomeCard = true;
                return;
            }

            // (4) Distill inline (subscription path; dryRun:false). redistill:false — one-click never
            // re-distills (the manual fallback owns the re-distill double-confirm, D-12).
            var distillProgress = new ActionOrchestratorProgress(msg =>
                InvokeAsync(() =>
                {
                    try
                    {
                        _logLines.Add(msg);
                        StateHasChanged();
                    }
                    catch (ObjectDisposedException) { }
                    catch (InvalidOperationException) { }
                }));

            var ids = harvestReadyIds.AsReadOnly();
            var result = await Task.Run(
                () => DistillOrchestrator.DistillAsync(
                    limit: ids.Count,
                    dryRun: false,
                    isSubscriptionProvider: true,
                    redistill: false,
                    videoIds: ids,
                    progress: distillProgress,
                    cancellationToken: _cts.Token),
                _cts.Token);

            _oneClickDistillResult = result;

            // (5) Shared auto-approve step (D-09): flip >=cutoff distills to 'approved' when enabled.
            _outcomeAutoApprovedCount = await ApplyAutoApproveAsync(result);

            _showOutcomeCard = true;

            await RefreshBadgesAsync(ids);
            if (_pendingLoaded)
            {
                await LoadPendingDistillAsync();
            }

            await RefreshCapDisplayAsync();
        }
        catch (OperationCanceledException)
        {
            _logLines.Add("Harvest + Auto-distill cancelled.");
            _harvestCancelled = true;
        }
        finally
        {
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// Builds the disposal-safe harvest progress sink (T-45-18): marshals log append + render through
    /// InvokeAsync and swallows post-dispose exceptions on a dropped circuit.
    /// </summary>
    private ActionOrchestratorProgress BuildHarvestProgress() =>
        new(msg =>
            InvokeAsync(() =>
            {
                try
                {
                    _logLines.Add(msg);
                    StateHasChanged();
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }));

    /// <summary>
    /// Runs the harvest body (resolve channel per video, group, EnsureYoutubeSource + HarvestAsync per
    /// group, continue-on-failure) for the supplied selected videos, sets <see cref="_harvestResult"/>,
    /// and refreshes badges on success. Shared by the plain "Harvest Selected" and one-click paths.
    /// Caller owns the <see cref="_operationInFlight"/> guard, the CTS, and OperationCanceledException.
    /// </summary>
    /// <returns><c>true</c> when the harvest completed successfully; otherwise <c>false</c>.</returns>
    private async Task<bool> RunHarvestCoreAsync(
        IReadOnlyList<VideoViewModel> selectedVideos,
        ActionOrchestratorProgress progress)
    {
        // Resolve a channel URL and name for each selected video.
        // Videos from the explicit-id queue carry ChannelId from YouTube metadata;
        // browsed videos carry ChannelId from the playlist/channel feed.
        // _lastBrowsedChannel is a fallback for videos without ChannelId.
        // Resolve a channel URL/name per selected video and group by channel so each channel gets
        // exactly one EnsureYoutubeSourceAsync + one HarvestAsync call (pure logic in HarvestPlanner).
        var plan = HarvestPlanner.ResolveChannelGroups(selectedVideos, _lastBrowsedChannel);
        var groups = plan.Groups;

        if (groups.Count == 0)
        {
            // All selected videos are unresolved — abort cleanly without throwing.
            _logLines.Add("Could not determine a channel for the selected video(s).");
            await InvokeAsync(StateHasChanged);
            return false;
        }

        if (plan.UnresolvedVideoIds.Count > 0)
        {
            _logLines.Add($"Warning: skipping {plan.UnresolvedVideoIds.Count} video(s) with no resolvable channel — ids: {string.Join(", ", plan.UnresolvedVideoIds)}");
        }

        var selectedIds = selectedVideos.Select(v => v.VideoId).ToList().AsReadOnly();

        // Why: Task.Run moves all IO off the Blazor sync context (Pitfall 1).
        _harvestResult = await Task.Run(
            async () =>
            {
                int totalCaptions = 0;
                int totalWhisper = 0;
                int totalSkipped = 0;
                bool anyGroupFailed = false;
                string? firstFailureMessage = null;

                foreach (var group in groups)
                {
                    // Why: EnsureYoutubeSourceAsync creates or re-enables the content source
                    // for each channel so each group's harvest has a valid sourceId.
                    var src = await SourceManager.EnsureYoutubeSourceAsync(
                        group.ChannelUrl,
                        group.ChannelName,
                        progress,
                        _cts!.Token).ConfigureAwait(false);

                    if (!src.Success || src.Id is null)
                    {
                        anyGroupFailed = true;
                        firstFailureMessage ??= src.Message ?? $"Could not ensure a content source for {group.ChannelUrl}.";
                        continue;
                    }

                    var r = await HarvestOrchestrator.HarvestAsync(
                        limit: group.VideoIds.Count,
                        videoIds: group.VideoIds,
                        sourceId: src.Id,
                        progress: progress,
                        cancellationToken: _cts.Token).ConfigureAwait(false);

                    if (!r.Success)
                    {
                        anyGroupFailed = true;
                        firstFailureMessage ??= r.Message;
                    }

                    totalCaptions += r.Captions;
                    totalWhisper += r.Whisper;
                    totalSkipped += r.SkippedNoCaptions;
                }

                return new HarvestResult
                {
                    Success = !anyGroupFailed && groups.Count > 0,
                    Captions = totalCaptions,
                    Whisper = totalWhisper,
                    SkippedNoCaptions = totalSkipped,
                    Message = anyGroupFailed ? firstFailureMessage : null,
                };
            },
            _cts!.Token);

        // Re-resolve badges for harvested videos to reflect new DB state.
        if (_harvestResult.Success)
        {
            await RefreshBadgesAsync(selectedIds);
        }

        return _harvestResult.Success;
    }

    /// <summary>
    /// Shared post-distill auto-approve step (D-09): when auto-approve is enabled, selects the
    /// distilled videos whose clip count is at or above the persisted cutoff (via
    /// <see cref="IAutoApproveSignal"/>) and batch-flips their <c>approval_status</c> to 'approved'
    /// (only approval_status is mutated). Returns the number of rows actually flipped (0 when disabled
    /// or none qualify). Authored as a shared method so a completing subscription distill — one-click
    /// or the manual Stage B fallback — benefits identically; metered live distill auto-approve is
    /// DEFERRED (Core refuses metered live distill, so this never runs for metered today).
    /// </summary>
    private async Task<int> ApplyAutoApproveAsync(DistillResult result)
    {
        // Why: pure key selection (enabled + clip-count cutoff via the swappable signal) lives in
        // HarvestPlanner; the page keeps the store write + cancellation. Empty selection => no call.
        var keys = HarvestPlanner.SelectAutoApproveKeys(
            result.DistilledVideos,
            _autoApproveSettings.Enabled,
            _autoApproveSettings.Cutoff,
            AutoApproveSignal);

        if (keys.Count == 0)
        {
            return 0;
        }

        return await IndexStore.SetApprovalStatusAsync(keys, "approved", _cts!.Token);
    }

    private void CancelOperation()
    {
        _cts?.Cancel();
    }

    private void BeginBlock(VideoViewModel vm)
    {
        vm.PendingBlock = true;
        _focusConfirmPending = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusConfirmPending)
        {
            _focusConfirmPending = false;
            try { await _confirmBlockButton.FocusAsync(); }
            catch (Exception) { }
        }
    }

    // ── Section 4: Distill Spend Gate (HARV-05) ─────────────────────────────

    /// <summary>
    /// Loads monthly cap and current-month spend at page init (D-02).
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await RefreshCapDisplayAsync();
        // Why: persisted auto-approve settings (D-07) — load once at init so the panel reflects
        // the operator's last choice across Studio restarts.
        _autoApproveSettings = AutoApproveSettingsStore.Load();
        await LoadCreatorsAsync();
    }

    /// <summary>
    /// Loads the saved creators for the browse dropdown (SRC-02). Failure is non-fatal — the
    /// paste-URL fallback always remains usable, so a load error just leaves the dropdown empty.
    /// </summary>
    private async Task LoadCreatorsAsync()
    {
        try
        {
            var creators = await CreatorStore.ListAsync();
            _creators = creators.ToList();
        }
        catch (Exception)
        {
            // Why: dropdown is a convenience over the URL fallback; never block browse on a load error.
            _creators = new();
        }
    }

    /// <summary>
    /// SRC-02: when a creator is picked, fill the browse target with its channel ref. The empty
    /// option ("paste a URL instead") leaves <see cref="_channelInput"/> for manual entry.
    /// </summary>
    private void OnCreatorSelected(ChangeEventArgs args)
    {
        _selectedCreatorRef = args.Value?.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(_selectedCreatorRef))
        {
            _channelInput = _selectedCreatorRef;
        }
    }

    /// <summary>
    /// SUI-05: updates the browse-list creator filter. Empty string means "All creators".
    /// The predicate is folded into <see cref="GetVisibleChannelVideos"/> (T-62-03) so the
    /// filtered set automatically narrows Select-All and the harvested video set.
    /// </summary>
    private void OnBrowseCreatorFilterChanged(ChangeEventArgs args)
    {
        _browsCreatorFilter = args.Value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Persists the current auto-approve on/off + cutoff (D-04/D-05/D-07). Called on toggle change
    /// and cutoff commit (blur), not per keystroke. The store clamps a bad cutoff before disk (T-59-03).
    /// </summary>
    private void SaveAutoApproveSettings()
    {
        AutoApproveSettingsStore.Save(_autoApproveSettings);
    }

    /// <summary>
    /// Handles the auto-approve on/off toggle: updates the record (records are immutable) and persists.
    /// </summary>
    private void OnAutoApproveEnabledChanged(ChangeEventArgs args)
    {
        var enabled = args.Value is bool b && b;
        _autoApproveSettings = _autoApproveSettings with { Enabled = enabled };
        SaveAutoApproveSettings();
    }

    /// <summary>
    /// Handles the cutoff commit (onchange/blur): parses the input, rebuilds the record, and persists.
    /// </summary>
    private void OnAutoApproveCutoffChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var cutoff))
        {
            _autoApproveSettings = _autoApproveSettings with { Cutoff = cutoff };
            SaveAutoApproveSettings();
        }
    }

    private async Task RefreshCapDisplayAsync()
    {
        _monthlyCap = SpendLedger.GetMonthlyCapUsd();
        var monthKey = DateTime.UtcNow.ToString("yyyy-MM");
        _monthlySpent = await SpendLedger.GetMonthlyTotalAsync(monthKey);
    }

    /// <summary>
    /// Validates and applies a session cap override from the operator input (D-03).
    /// The new value is written to <see cref="SessionCapOverride.OverrideUsd"/>; the singleton
    /// ledger immediately reflects it (Pitfall 6 mitigated — same singleton seen by orchestrator).
    /// </summary>
    private async Task RaiseCapAsync()
    {
        if (!decimal.TryParse(_capRaiseInput, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var newCap)
            || newCap < 0m)
        {
            // Why: V5 input validation — ignore invalid or negative input without throwing.
            return;
        }

        // Why: OverrideUsd write propagates to both the page display (GetMonthlyCapUsd) and
        // the orchestrator's WouldExceedCapAsync call because both resolve the same singleton (D-03).
        CapOverride.OverrideUsd = newCap;
        await RefreshCapDisplayAsync();
        StateHasChanged();
    }

    /// <summary>
    /// Stage A: runs a dry-run distill to project spend before any live execution.
    /// Passes <paramref name="redistillConfirmed"/> as the <c>redistill:</c> named argument so
    /// re-distill videos are counted in WouldRun only when the operator double-confirmed (HIGH-4).
    /// </summary>
    private async Task RunDistillStageAAsync(
        IReadOnlyList<string> distillIds,
        bool redistillConfirmed)
    {
        if (_operationInFlight)
        {
            return;
        }

        _operationInFlight = true;
        _distillDryRunInFlight = true;
        _distillLogLines.Clear();
        _distillDryRunResult = null;
        _distillLiveResult = null;
        _distillSpendConfirmed = false;
        _distillCancelled = false;
        _cts = new CancellationTokenSource();

        // Why: progress sink must marshal _distillLogLines.Add and StateHasChanged through
        // InvokeAsync — same disposal-safe pattern as harvest (T-45-18).
        var progress = new ActionOrchestratorProgress(msg =>
            InvokeAsync(() =>
            {
                try
                {
                    _distillLogLines.Add(msg);
                    StateHasChanged();
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }));

        try
        {
            // Why: Task.Run moves DistillAsync off the Blazor sync context (Pitfall 1).
            // redistill: is passed by name (HIGH-4) — value is the same gate as the dry-run button
            // enable condition, so re-distill videos enter distillIds and are counted in WouldRun
            // only when redistillConfirmed is true.
            _distillDryRunResult = await Task.Run(
                () => DistillOrchestrator.DistillAsync(
                    limit: distillIds.Count,
                    dryRun: true,
                    isSubscriptionProvider: DistillConfig.IsSubscriptionProvider,
                    redistill: redistillConfirmed,
                    videoIds: distillIds,
                    progress: progress,
                    cancellationToken: _cts.Token),
                _cts.Token);

            await RefreshCapDisplayAsync();
        }
        catch (OperationCanceledException)
        {
            _distillLogLines.Add("Distill cancelled.");
            _distillCancelled = true;
        }
        finally
        {
            _distillDryRunInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// Stage B: runs the live distill after the operator has reviewed the dry-run and
    /// confirmed spend. Passes the same <paramref name="redistillConfirmed"/> gate so
    /// already-distilled videos are only re-processed when the double-confirm was given (HIGH-4).
    /// </summary>
    private async Task RunDistillStageBAsync(
        IReadOnlyList<string> distillIds,
        bool redistillConfirmed)
    {
        // Why: the reviewed-spend confirmation is a spend-safety gate (HARV-05). A subscription
        // ($0) provider has no metered spend, so the dry-run + confirm are skipped and Run Distill
        // is reachable directly; metered providers still require _distillSpendConfirmed.
        if (_operationInFlight || (!_distillSpendConfirmed && !DistillConfig.IsSubscriptionProvider))
        {
            return;
        }

        _operationInFlight = true;
        _distillLiveInFlight = true;
        _distillLogLines.Clear();
        _distillLiveResult = null;
        _distillCancelled = false;
        _cts = new CancellationTokenSource();

        // Start elapsed clock and 1-second ticker for live progress display (260615-t7m).
        _distillStopwatch = Stopwatch.StartNew();
        _distillTotalElapsed = null;
        _distillTickerCts = new CancellationTokenSource();
        // Why: fire-and-forget ticker is display-only; PeriodicTimer + InvokeAsync keeps
        // re-renders on the Blazor sync context. OperationCanceledException and
        // ObjectDisposedException are swallowed so a circuit drop never throws from the ticker.
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await timer.WaitForNextTickAsync(_distillTickerCts.Token))
                {
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        });

        // Why: same disposal-safe progress pattern as Stage A and harvest (T-45-18).
        var progress = new ActionOrchestratorProgress(msg =>
            InvokeAsync(() =>
            {
                try
                {
                    _distillLogLines.Add(msg);
                    StateHasChanged();
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }));

        try
        {
            // Why: dryRun: false executes the live distill. redistill: is the same gate as Stage A
            // so the re-distill guard is consistent end-to-end (HIGH-4). Named argument form used
            // to decouple from parameter position.
            _distillLiveResult = await Task.Run(
                () => DistillOrchestrator.DistillAsync(
                    limit: distillIds.Count,
                    dryRun: false,
                    isSubscriptionProvider: DistillConfig.IsSubscriptionProvider,
                    redistill: redistillConfirmed,
                    videoIds: distillIds,
                    progress: progress,
                    cancellationToken: _cts.Token),
                _cts.Token);

            // Refresh badges for distilled videos and update cap display.
            if (_distillLiveResult.Success || _distillLiveResult.VideosDistilled > 0)
            {
                // Why: D-09 reuse — a completing SUBSCRIPTION distill via the manual fallback (D-12)
                // auto-approves >=cutoff videos through the SAME shared step as the one-click path.
                // Metered live distill never reaches here (Core refuses at line 244), so the shared
                // step simply never runs for metered — metered auto-approve is DEFERRED.
                _outcomeAutoApprovedCount = await ApplyAutoApproveAsync(_distillLiveResult);

                await RefreshBadgesAsync(distillIds);

                // Why: reload the DB-backed pending list so just-distilled videos drop off
                // (quick task 260615-p4d). Only reload if it was previously loaded so an
                // operator who never used the loader isn't surprised by a new table appearing.
                if (_pendingLoaded)
                {
                    await LoadPendingDistillAsync();
                }
            }

            await RefreshCapDisplayAsync();
        }
        catch (OperationCanceledException)
        {
            _distillLogLines.Add("Distill cancelled.");
            _distillCancelled = true;
        }
        finally
        {
            // Stop elapsed clock and ticker before the final render so the result card shows
            // the frozen total time rather than a still-ticking value (260615-t7m).
            _distillStopwatch?.Stop();
            _distillTotalElapsed = _distillStopwatch?.Elapsed;
            _distillTickerCts?.Cancel();
            _distillTickerCts?.Dispose();
            _distillTickerCts = null;

            _distillLiveInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RefreshBadgesAsync(IReadOnlyList<string> videoIds)
    {
        foreach (var videoId in videoIds)
        {
            var newStatus = await StatusResolver.ResolveStatusAsync(videoId);

            var channelVm = _channelVideos.FirstOrDefault(v => v.VideoId == videoId);
            if (channelVm is not null)
            {
                channelVm.Status = newStatus;
            }

            var queueVm = _queueVideos.FirstOrDefault(v => v.VideoId == videoId);
            if (queueVm is not null)
            {
                // Re-apply duplicate logic for queue items.
                queueVm.Status = (newStatus == VideoStatus.Harvested || newStatus == VideoStatus.Distilled)
                    ? VideoStatus.Duplicate
                    : newStatus;
            }
        }
    }

    private async Task ConfirmBlockAsync(VideoViewModel vm)
    {
        if (_operationInFlight)
        {
            return;
        }

        _operationInFlight = true;
        _blockError = string.Empty;
        _cts = new CancellationTokenSource();

        var progress = new ActionOrchestratorProgress(msg =>
            InvokeAsync(() =>
            {
                try
                {
                    _logLines.Add(msg);
                    StateHasChanged();
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }));

        try
        {
            // Why: Task.Run moves the destructive orchestrator call off the Blazor sync context.
            // BlockVideoAsync owns block-first/delete-second ordering — never call a store delete directly.
            var result = await Task.Run(
                () => MaintenanceOrchestrator.BlockVideoAsync(vm.VideoId, reason: null, progress, _cts.Token),
                _cts.Token);

            if (result.Success)
            {
                // Re-resolve the badge so the row flips to Blocked.
                await RefreshBadgesAsync(new[] { vm.VideoId });
            }
            else
            {
                // Result-based failure (NO exception): surface operator-safe copy, do NOT refresh the badge.
                _blockError = !string.IsNullOrWhiteSpace(result.Message)
                    ? result.Message
                    : "Block failed — the video was not removed.";
                _logLines.Add(_blockError);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            // Why: do NOT echo exception.Message (may leak paths) — generic operator-safe copy only.
            _blockError = "Block failed — the video was not removed.";
            _logLines.Add(_blockError);
        }
        finally
        {
            // Clear the confirm state on EVERY outcome (success, result-false, throw).
            vm.PendingBlock = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Elapsed formatting (260615-t7m) ─────────────────────────────────────
    private static string FormatElapsed(TimeSpan t) =>
        t.TotalSeconds < 60 ? $"{t.TotalSeconds:F0}s" : $"{(int)t.TotalMinutes}m {t.Seconds}s";

    // ── IDisposable: cancel in-flight op on circuit drop (D-06) ─────────────
    /// <summary>
    /// Cancels and disposes the active <see cref="CancellationTokenSource"/> so any in-flight
    /// harvest is stopped when the operator closes the tab or the SignalR circuit drops (D-06).
    /// </summary>
    public void Dispose()
    {
        // Why: CTS disposed on component disposal so a circuit drop cancels in-flight ops (D-06).
        _cts?.Cancel();
        _cts?.Dispose();
        // Why: ticker CTS must also be cancelled on disposal so the PeriodicTimer loop exits cleanly
        // when the operator closes the tab mid-distill (260615-t7m).
        _distillTickerCts?.Cancel();
        _distillTickerCts?.Dispose();
        _distillStopwatch?.Stop();
    }
}
