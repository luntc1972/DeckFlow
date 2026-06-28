using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Fast unit tests for <see cref="HarvestPlanner"/> — the pure Harvest planning logic extracted from
/// the page code-behind (H1 split): the visible-projection filter, the selected-set combinations, the
/// channel grouping, and the auto-approve key selection. These ran only through a whole-page bUnit
/// render before; now they are testable directly.
/// </summary>
public sealed class HarvestPlannerTests
{
    private sealed class CutoffSignal : IAutoApproveSignal
    {
        public bool ShouldAutoApprove(int clipCount, int cutoff) => clipCount >= cutoff;
    }

    private static VideoViewModel Vm(
        string videoId,
        VideoStatus status = VideoStatus.NotHarvested,
        bool selected = false,
        string? channelId = null,
        string? channelTitle = null)
        => new(videoId, $"https://youtu.be/{videoId}", $"Title {videoId}", null, status, channelId, channelTitle)
        {
            Selected = selected,
        };

    // ── FilterVisibleChannelVideos ──────────────────────────────────────────

    [Fact]
    public void FilterVisible_ExcludesSkipped_AndNonNotHarvested_ByDefault()
    {
        var videos = new[]
        {
            Vm("a", VideoStatus.NotHarvested),
            Vm("b", VideoStatus.Harvested),
            Vm("c", VideoStatus.NotHarvested),
        };
        var skipped = new HashSet<string> { "c" };

        var visible = HarvestPlanner.FilterVisibleChannelVideos(videos, skipped, showAll: false, creatorFilter: "");

        Assert.Single(visible);
        Assert.Equal("a", visible[0].VideoId);
    }

    [Fact]
    public void FilterVisible_ShowAll_IncludesHarvested_ButStillExcludesSkipped()
    {
        var videos = new[]
        {
            Vm("a", VideoStatus.NotHarvested),
            Vm("b", VideoStatus.Harvested),
            Vm("c", VideoStatus.Distilled),
        };
        var skipped = new HashSet<string> { "c" };

        var visible = HarvestPlanner.FilterVisibleChannelVideos(videos, skipped, showAll: true, creatorFilter: "");

        Assert.Equal(new[] { "a", "b" }, visible.Select(v => v.VideoId));
    }

    [Fact]
    public void FilterVisible_CreatorFilter_KeepsOnlyMatchingCreator()
    {
        var videos = new[]
        {
            Vm("a", channelTitle: "Channel One"),
            Vm("b", channelTitle: "Channel Two"),
        };
        var filter = CreatorNameResolver.FromChannelTitle("Channel One");

        var visible = HarvestPlanner.FilterVisibleChannelVideos(
            videos, new HashSet<string>(), showAll: false, creatorFilter: filter);

        Assert.Single(visible);
        Assert.Equal("a", visible[0].VideoId);
    }

    // ── CombineSelected ─────────────────────────────────────────────────────

    [Fact]
    public void CombineSelected_TakesSelectedFromBothLists()
    {
        var visible = new[] { Vm("a", selected: true), Vm("b", selected: false) };
        var queue = new[] { Vm("c", selected: true) };

        var combined = HarvestPlanner.CombineSelected(visible, queue);

        Assert.Equal(new[] { "a", "c" }, combined.Select(v => v.VideoId));
    }

    // ── CombineForDistill ───────────────────────────────────────────────────

    [Fact]
    public void CombineForDistill_DedupesByVideoId()
    {
        var harvestSelected = new[] { Vm("a"), Vm("dup") };
        var pending = new[] { Vm("dup", selected: true), Vm("b", selected: true), Vm("c", selected: false) };

        var combined = HarvestPlanner.CombineForDistill(harvestSelected, pending);

        Assert.Equal(new[] { "a", "dup", "b" }, combined.Select(v => v.VideoId));
    }

    // ── ResolveChannelGroups ────────────────────────────────────────────────

    [Fact]
    public void ResolveChannelGroups_GroupsByChannelId()
    {
        var selected = new[]
        {
            Vm("v1", channelId: "chan1", channelTitle: "One"),
            Vm("v2", channelId: "chan1", channelTitle: "One"),
            Vm("v3", channelId: "chan2", channelTitle: "Two"),
        };

        var plan = HarvestPlanner.ResolveChannelGroups(selected, lastBrowsedChannel: "");

        Assert.Equal(2, plan.Groups.Count);
        Assert.Empty(plan.UnresolvedVideoIds);
        var chan1 = plan.Groups.Single(g => g.ChannelUrl.EndsWith("chan1", StringComparison.Ordinal));
        Assert.Equal(new[] { "v1", "v2" }, chan1.VideoIds);
        Assert.Equal("One", chan1.ChannelName);
    }

    [Fact]
    public void ResolveChannelGroups_FallsBackToLastBrowsedChannel_WhenNoChannelId()
    {
        var selected = new[] { Vm("v1") };

        var plan = HarvestPlanner.ResolveChannelGroups(selected, lastBrowsedChannel: "https://youtube.com/@creator");

        var group = Assert.Single(plan.Groups);
        Assert.Equal("https://youtube.com/@creator", group.ChannelUrl);
        Assert.Empty(plan.UnresolvedVideoIds);
    }

    [Fact]
    public void ResolveChannelGroups_NoChannelId_NoFallback_IsUnresolved()
    {
        var selected = new[] { Vm("v1"), Vm("v2", channelId: "chan1") };

        var plan = HarvestPlanner.ResolveChannelGroups(selected, lastBrowsedChannel: "");

        Assert.Single(plan.Groups);
        Assert.Equal(new[] { "v1" }, plan.UnresolvedVideoIds);
    }

    // ── SelectAutoApproveKeys ───────────────────────────────────────────────

    private static DistilledVideoResult Distilled(string keyValue, int clipCount)
        => new()
        {
            NaturalKeyType = ContentSourceType.Youtube,
            NaturalKeyValue = keyValue,
            ClipCount = clipCount,
        };

    [Fact]
    public void SelectAutoApproveKeys_Disabled_ReturnsEmpty()
    {
        var videos = new[] { Distilled("a", 10) };

        var keys = HarvestPlanner.SelectAutoApproveKeys(videos, enabled: false, cutoff: 5, new CutoffSignal());

        Assert.Empty(keys);
    }

    [Fact]
    public void SelectAutoApproveKeys_KeepsOnlyAtOrAboveCutoff()
    {
        var videos = new[] { Distilled("low", 3), Distilled("hit", 5), Distilled("high", 9) };

        var keys = HarvestPlanner.SelectAutoApproveKeys(videos, enabled: true, cutoff: 5, new CutoffSignal());

        Assert.Equal(new[] { "hit", "high" }, keys.Select(k => k.NaturalKeyValue));
    }
}
