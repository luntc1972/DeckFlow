using Bunit;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// bUnit behavioral tests for Blocked.razor (REM-02: list + unblock + empty state).
/// </summary>
public sealed class BlockedPageTests : BunitContext
{
    private IRenderedComponent<Blocked> RenderBlocked(
        IEnumerable<BlockedVideoListResult.BlockedVideoListItem> blocked)
    {
        var fake = new FakeContentKbOrchestrator
        {
            CannedBlockedResult = new BlockedVideoListResult { Items = blocked.ToList() },
        };
        Services.AddSingleton<IContentMaintenanceOrchestrator>(fake);
        return Render<Blocked>();
    }

    private (IRenderedComponent<Blocked> Cut, FakeContentKbOrchestrator Fake) RenderBlockedWithFake(
        IEnumerable<BlockedVideoListResult.BlockedVideoListItem> blocked)
    {
        var fake = new FakeContentKbOrchestrator
        {
            CannedBlockedResult = new BlockedVideoListResult { Items = blocked.ToList() },
        };
        Services.AddSingleton<IContentMaintenanceOrchestrator>(fake);
        var cut = Render<Blocked>();
        return (cut, fake);
    }

    [Fact]
    public void BlockedPage_NoBlockedVideos_ShowsEmptyState()
    {
        var cut = RenderBlocked(Array.Empty<BlockedVideoListResult.BlockedVideoListItem>());

        cut.WaitForAssertion(() => Assert.Contains("No blocked videos", cut.Markup));
    }

    [Fact]
    public void BlockedPage_WithBlockedVideos_ShowsTable()
    {
        var videos = new[]
        {
            new BlockedVideoListResult.BlockedVideoListItem
            {
                YoutubeVideoId = "abc123",
                BlockedUtc = DateTimeOffset.UtcNow,
                Reason = "spam",
            },
        };

        var cut = RenderBlocked(videos);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("abc123", cut.Markup);
            Assert.Contains("Unblock Video", cut.Markup);
        });
    }

    [Fact]
    public void BlockedPage_Unblock_RemovesRow()
    {
        var videos = new[]
        {
            new BlockedVideoListResult.BlockedVideoListItem
            {
                YoutubeVideoId = "abc123",
                BlockedUtc = DateTimeOffset.UtcNow,
                Reason = "spam",
            },
        };

        var (cut, fake) = RenderBlockedWithFake(videos);

        cut.WaitForAssertion(() => Assert.Contains("abc123", cut.Markup));

        cut.Find("button[aria-label='Unblock abc123']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("abc123", fake.UnblockCalls);
            Assert.DoesNotContain("abc123", cut.Markup);
        });
    }

    [Fact]
    public void BlockedPage_UnblockResultFailure_KeepsRowAndShowsSafeError()
    {
        var videos = new[]
        {
            new BlockedVideoListResult.BlockedVideoListItem
            {
                YoutubeVideoId = "abc123",
                BlockedUtc = DateTimeOffset.UtcNow,
                Reason = "spam",
            },
        };
        const string safeFailureMessage = "Unblock failed. Try again.";
        const string rawDbPath = @"C:\data\deckflow.sqlite";

        var fake = new FakeContentKbOrchestrator
        {
            CannedBlockedResult = new BlockedVideoListResult { Items = videos.ToList() },
            CannedMaintenanceResult = new ContentMaintenanceResult
            {
                Success = false,
                Message = safeFailureMessage,
            },
        };
        Services.AddSingleton<IContentMaintenanceOrchestrator>(fake);

        var cut = Render<Blocked>();

        cut.WaitForAssertion(() => Assert.Contains("abc123", cut.Markup));

        cut.Find("button[aria-label='Unblock abc123']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("abc123", fake.UnblockCalls);
            Assert.Contains("abc123", cut.Markup);
            Assert.Contains(safeFailureMessage, cut.Markup);
            Assert.DoesNotContain(rawDbPath, cut.Markup);
        });
    }
}
