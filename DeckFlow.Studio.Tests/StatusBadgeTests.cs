using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Studio.Shared;
using Xunit;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// bUnit tests for <see cref="StatusBadge"/>: asserts that each <see cref="VideoStatus"/>
/// value renders the expected label substring and Bootstrap badge class (SUI-01).
/// Badge markup is byte-identical to the original Harvest.razor RenderBadge output.
/// </summary>
public sealed class StatusBadgeTests : BunitContext
{
    [Theory]
    [InlineData(VideoStatus.NotHarvested, "Not harvested", "bg-secondary")]
    [InlineData(VideoStatus.Harvested, "Harvested", "bg-info")]
    [InlineData(VideoStatus.Distilled, "Distilled", "bg-success")]
    [InlineData(VideoStatus.Approved, "Approved", "bg-primary")]
    [InlineData(VideoStatus.Published, "Published", "bg-success")]
    [InlineData(VideoStatus.Blocked, "Blocked", "bg-danger")]
    [InlineData(VideoStatus.Duplicate, "Already in DB", "bg-warning")]
    public void StatusBadge_RendersExpectedLabelAndClass(VideoStatus status, string expectedLabel, string expectedClass)
    {
        // Act
        var cut = Render<StatusBadge>(p => p.Add(b => b.Status, status));

        // Assert: label text present in the badge
        Assert.Contains(expectedLabel, cut.Markup);
        // Assert: expected Bootstrap badge class present
        Assert.Contains(expectedClass, cut.Markup);
    }

    [Fact]
    public void StatusBadge_Published_ContainsCheckIcon()
    {
        // Why: Published badge renders a check icon; this pin ensures the icon is never dropped.
        var cut = Render<StatusBadge>(p => p.Add(b => b.Status, VideoStatus.Published));

        Assert.Contains("oi-check", cut.Markup);
    }
}
