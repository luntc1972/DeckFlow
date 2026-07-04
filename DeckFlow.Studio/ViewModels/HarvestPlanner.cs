using DeckFlow.Core.Content;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Pure planning logic for the Harvest + Distill page, extracted from the page code-behind (H1
/// split). Every method is a static pure function over the in-memory video lists and settings — no
/// I/O, no rendering, no component state — so the selection/visibility rules, channel grouping, and
/// auto-approve key selection are unit-testable without a bUnit render. The page keeps the I/O loops
/// (orchestrator calls, log streaming, cancellation) and calls into these helpers for the decisions.
/// Unlike DirectPush — whose linear read→diff→upload→write pipeline lifted cleanly into an injectable
/// <c>DirectPushCoordinator</c> — Harvest's I/O is interleaved with UI state (log streaming, per-channel
/// progress, VideoViewModel.Status mutation), so only the stateless decisions were extracted here.
/// </summary>
public static class HarvestPlanner
{
    /// <summary>
    /// The single canonical visible projection of browsed channel videos (HSEL-01/02, SUI-05):
    /// skipped ids are always excluded; unless <paramref name="showAll"/> is set only NotHarvested
    /// rows show; and when <paramref name="creatorFilter"/> is non-empty only rows whose resolved
    /// creator name matches are kept. Select-All and the harvested set both route through this so a
    /// row hidden by filter or skip can never be harvested even if it was selected before being hidden.
    /// </summary>
    public static IReadOnlyList<VideoViewModel> FilterVisibleChannelVideos(
        IReadOnlyList<VideoViewModel> channelVideos,
        ISet<string> skippedVideoIds,
        bool showAll,
        string creatorFilter)
        => channelVideos
            .Where(vm => !skippedVideoIds.Contains(vm.VideoId)
                && (showAll || vm.Status == VideoStatus.NotHarvested)
                && (string.IsNullOrEmpty(creatorFilter)
                    || CreatorNameResolver.FromChannelTitle(vm.ChannelTitle) == creatorFilter))
            .ToList();

    /// <summary>
    /// The browsed videos that are hidden from the harvest list because they were skipped or blocked,
    /// scoped to the creator filter. Kept separate from <see cref="FilterVisibleChannelVideos"/> so a
    /// hidden row is never selectable/harvestable — it is surfaced only for un-skip / un-block.
    /// </summary>
    /// <param name="channelVideos">All browsed rows.</param>
    /// <param name="skippedVideoIds">The set of skipped video ids.</param>
    /// <param name="creatorFilter">Creator name to narrow to, or empty for all creators.</param>
    /// <returns>The skipped or blocked rows matching the creator filter.</returns>
    public static IReadOnlyList<VideoViewModel> FilterHiddenChannelVideos(
        IReadOnlyList<VideoViewModel> channelVideos,
        ISet<string> skippedVideoIds,
        string creatorFilter)
        => channelVideos
            .Where(vm => (skippedVideoIds.Contains(vm.VideoId) || vm.Status == VideoStatus.Blocked)
                && (string.IsNullOrEmpty(creatorFilter)
                    || CreatorNameResolver.FromChannelTitle(vm.ChannelTitle) == creatorFilter))
            .ToList();

    /// <summary>
    /// All videos selected for harvest: the selected VISIBLE channel videos plus the selected queue
    /// videos. Callers pass the already-filtered visible list so a row hidden by the unharvested
    /// filter or by skip cannot be harvested (Codex HIGH).
    /// </summary>
    public static IReadOnlyList<VideoViewModel> CombineSelected(
        IReadOnlyList<VideoViewModel> visibleChannelVideos,
        IReadOnlyList<VideoViewModel> queueVideos)
        => visibleChannelVideos.Where(v => v.Selected)
            .Concat(queueVideos.Where(v => v.Selected))
            .ToList();

    /// <summary>
    /// All videos selected for distill: the harvest selection combined with the selected DB-backed
    /// pending-distill videos, de-duplicated by <see cref="VideoViewModel.VideoId"/> so a video that
    /// is both browsed and pending counts once.
    /// </summary>
    public static IReadOnlyList<VideoViewModel> CombineForDistill(
        IReadOnlyList<VideoViewModel> harvestSelected,
        IReadOnlyList<VideoViewModel> pendingDistillVideos)
        => harvestSelected
            .Concat(pendingDistillVideos.Where(v => v.Selected))
            .GroupBy(v => v.VideoId)
            .Select(g => g.First())
            .ToList();

    /// <summary>
    /// Resolves a channel URL + name for each selected video and groups them by channel so each
    /// channel gets exactly one EnsureYoutubeSource + Harvest call. A video carries its own ChannelId
    /// when available; otherwise <paramref name="lastBrowsedChannel"/> is the fallback. Videos with no
    /// resolvable channel are returned in <see cref="HarvestGroupPlan.UnresolvedVideoIds"/> and excluded
    /// from the groups.
    /// </summary>
    public static HarvestGroupPlan ResolveChannelGroups(
        IReadOnlyList<VideoViewModel> selectedVideos,
        string lastBrowsedChannel)
    {
        var resolvable = new List<(string ChannelUrl, string ChannelName, VideoViewModel Video)>();
        var unresolvedIds = new List<string>();

        foreach (var v in selectedVideos)
        {
            string? channelUrl = !string.IsNullOrWhiteSpace(v.ChannelId)
                ? $"https://www.youtube.com/channel/{v.ChannelId}"
                : (!string.IsNullOrWhiteSpace(lastBrowsedChannel) ? lastBrowsedChannel : null);

            if (channelUrl is null)
            {
                unresolvedIds.Add(v.VideoId);
                continue;
            }

            var channelName = v.ChannelTitle ?? v.ChannelId ?? lastBrowsedChannel;
            resolvable.Add((channelUrl, channelName ?? channelUrl, v));
        }

        var groups = resolvable
            .GroupBy(x => x.ChannelUrl)
            .Select(g => new HarvestChannelGroup(
                g.Key,
                g.First().ChannelName,
                g.Select(x => x.Video.VideoId).ToList()))
            .ToList();

        return new HarvestGroupPlan(groups, unresolvedIds);
    }

    /// <summary>
    /// Selects the natural keys to auto-approve after a distill (D-09): when
    /// <paramref name="enabled"/>, the distilled videos whose clip count is at or above
    /// <paramref name="cutoff"/> per <paramref name="signal"/>. Returns an empty list when disabled or
    /// when none qualify, so the caller can skip the batch approval call entirely.
    /// </summary>
    public static IReadOnlyList<(string NaturalKeyType, string NaturalKeyValue)> SelectAutoApproveKeys(
        IReadOnlyList<DistilledVideoResult> distilledVideos,
        bool enabled,
        int cutoff,
        IAutoApproveSignal signal)
    {
        if (!enabled || distilledVideos.Count == 0)
        {
            return Array.Empty<(string, string)>();
        }

        return distilledVideos
            .Where(v => signal.ShouldAutoApprove(v.ClipCount, cutoff))
            .Select(v => (v.NaturalKeyType, v.NaturalKeyValue))
            .ToList();
    }
}

/// <summary>One channel's harvest group: the channel URL/name and the video ids harvested together.</summary>
public sealed record HarvestChannelGroup(string ChannelUrl, string ChannelName, IReadOnlyList<string> VideoIds);

/// <summary>The grouped harvest plan: channel groups to harvest plus the unresolved video ids skipped.</summary>
public sealed record HarvestGroupPlan(
    IReadOnlyList<HarvestChannelGroup> Groups,
    IReadOnlyList<string> UnresolvedVideoIds);
