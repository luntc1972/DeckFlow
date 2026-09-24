using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class FeedbackTypeDisplayTests
{
    [Fact]
    public void GetDisplayName_CreatorRemoval_ReturnsDisplayName()
    {
        Assert.Equal("Creator removal request", FeedbackTypeDisplay.GetDisplayName(FeedbackType.CreatorRemoval));
    }

    [Fact]
    public void GetDisplayName_CommentWithoutDisplay_ReturnsEnumName()
    {
        Assert.Equal("Comment", FeedbackTypeDisplay.GetDisplayName(FeedbackType.Comment));
    }
}
