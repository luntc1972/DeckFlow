using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Studio.Shared;
using Xunit;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// bUnit tests for <see cref="PublishStateBadge"/>: asserts that each <see cref="PublishState"/>
/// value renders the expected label substring and Bootstrap badge class. Markup is byte-identical
/// to the three RenderPublishStateBadge methods (Home/Publish/Review) it replaced.
/// </summary>
public sealed class PublishStateBadgeTests : BunitContext
{
    [Theory]
    [InlineData(PublishState.NeverPublished, "Never published", "bg-secondary")]
    [InlineData(PublishState.PushedHidden, "Pushed-hidden", "bg-warning")]
    [InlineData(PublishState.Published, "Published", "bg-success")]
    [InlineData(PublishState.LocalNewer, "Local-newer", "bg-info")]
    public void PublishStateBadge_RendersExpectedLabelAndClass(PublishState state, string expectedLabel, string expectedClass)
    {
        // Act
        var cut = Render<PublishStateBadge>(p => p.Add(b => b.State, state));

        // Assert: label text present in the badge
        Assert.Contains(expectedLabel, cut.Markup);
        // Assert: expected Bootstrap badge class present
        Assert.Contains(expectedClass, cut.Markup);
    }

    [Fact]
    public void PublishStateBadge_Published_ContainsCheckIcon()
    {
        // Why: the Published badge renders a check icon; this pin ensures it is never dropped.
        var cut = Render<PublishStateBadge>(p => p.Add(b => b.State, PublishState.Published));

        Assert.Contains("oi-check", cut.Markup);
    }

    [Fact]
    public void PublishStateBadge_UnknownValue_FallsBackToUnknownSecondary()
    {
        // Why: any out-of-range value must hit the default arm (Unknown / bg-secondary), never blank.
        var cut = Render<PublishStateBadge>(p => p.Add(b => b.State, (PublishState)999));

        Assert.Contains("Unknown", cut.Markup);
        Assert.Contains("bg-secondary", cut.Markup);
    }
}
