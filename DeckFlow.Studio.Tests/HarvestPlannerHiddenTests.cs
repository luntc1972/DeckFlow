using System;
using System.Collections.Generic;
using System.Linq;
using DeckFlow.Core.Content;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using Xunit;

namespace DeckFlow.Studio.Tests;

public sealed class HarvestPlannerHiddenTests
{
    private static VideoViewModel Vm(string id, VideoStatus status, string channelTitle = "Chan")
        => new(id, $"https://youtu.be/{id}", id, null, status, "UC", channelTitle);

    [Fact]
    public void FilterHiddenChannelVideos_ReturnsSkippedAndBlocked_ExcludesVisible()
    {
        var all = new[]
        {
            Vm("s1", VideoStatus.NotHarvested),
            Vm("b1", VideoStatus.Blocked),
            Vm("n1", VideoStatus.NotHarvested),
            Vm("h1", VideoStatus.Harvested),
        };
        var skipped = new HashSet<string>(StringComparer.Ordinal) { "s1" };

        var hidden = HarvestPlanner.FilterHiddenChannelVideos(all, skipped, string.Empty);

        Assert.Equal(new[] { "s1", "b1" }, hidden.Select(v => v.VideoId).ToArray());
    }

    [Fact]
    public void FilterHiddenChannelVideos_RespectsCreatorFilter()
    {
        var all = new[]
        {
            Vm("s1", VideoStatus.NotHarvested, "Alice"),
            Vm("b1", VideoStatus.Blocked, "Bob"),
        };
        var skipped = new HashSet<string>(StringComparer.Ordinal) { "s1" };

        var alice = HarvestPlanner.FilterHiddenChannelVideos(
            all, skipped, CreatorNameResolver.FromChannelTitle("Alice"));

        Assert.Equal(new[] { "s1" }, alice.Select(v => v.VideoId).ToArray());
    }

    [Fact]
    public void FilterHiddenChannelVideos_NoneHidden_ReturnsEmpty()
    {
        var all = new[] { Vm("n1", VideoStatus.NotHarvested), Vm("h1", VideoStatus.Harvested) };

        var hidden = HarvestPlanner.FilterHiddenChannelVideos(
            all, new HashSet<string>(StringComparer.Ordinal), string.Empty);

        Assert.Empty(hidden);
    }
}
