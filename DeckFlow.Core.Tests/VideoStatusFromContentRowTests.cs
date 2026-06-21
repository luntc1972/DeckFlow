using DeckFlow.Core.Content;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for <see cref="VideoStatusResolver.FromContentRow"/> — the shared pure mapper
/// that derives <see cref="VideoStatus"/> from persisted content-site-index row fields.
/// Review.razor and VideoStatusResolver.ResolveStatusAsync both route through this method
/// so the Published/Approved/Distilled rule lives in one place (SUI-01, Codex MEDIUM).
/// </summary>
public sealed class VideoStatusFromContentRowTests
{
    [Fact]
    public void FromContentRow_PushedAndVisible_ReturnsPublished()
    {
        // Arrange: pushed to prod AND is_visible = true → Published.
        var result = VideoStatusResolver.FromContentRow(
            approvalStatus: "approved",
            pushedToProdUtc: DateTimeOffset.UtcNow,
            isVisible: true);

        Assert.Equal(VideoStatus.Published, result);
    }

    [Fact]
    public void FromContentRow_PushedButHidden_ReturnsApproved()
    {
        // Arrange: pushed but is_visible = false → Approved (pushed-hidden limbo semantic).
        var result = VideoStatusResolver.FromContentRow(
            approvalStatus: "approved",
            pushedToProdUtc: DateTimeOffset.UtcNow,
            isVisible: false);

        Assert.Equal(VideoStatus.Approved, result);
    }

    [Fact]
    public void FromContentRow_ApprovedNotPushed_ReturnsApproved()
    {
        // Arrange: approval_status = "approved" + no push timestamp.
        var result = VideoStatusResolver.FromContentRow(
            approvalStatus: "approved",
            pushedToProdUtc: null,
            isVisible: false);

        Assert.Equal(VideoStatus.Approved, result);
    }

    [Fact]
    public void FromContentRow_PendingNoPush_ReturnsDistilled()
    {
        // Arrange: approval_status = "pending" (not yet approved).
        var result = VideoStatusResolver.FromContentRow(
            approvalStatus: "pending",
            pushedToProdUtc: null,
            isVisible: false);

        Assert.Equal(VideoStatus.Distilled, result);
    }

    [Fact]
    public void FromContentRow_RejectedNoPush_ReturnsDistilled()
    {
        // Arrange: approval_status = "rejected".
        var result = VideoStatusResolver.FromContentRow(
            approvalStatus: "rejected",
            pushedToProdUtc: null,
            isVisible: false);

        Assert.Equal(VideoStatus.Distilled, result);
    }
}
