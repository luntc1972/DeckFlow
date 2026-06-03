using DeckFlow.Core.Integration;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="YouTubeVideoListExport"/> text rendering.
/// </summary>
public sealed class YouTubeVideoListExportTests
{
    [Fact]
    public void BuildText_RendersHeaderRowsUrlsAndTotals()
    {
        var videos = new[]
        {
            Video("vid-1", "First Video", 249_277, new DateTimeOffset(2025, 12, 16, 0, 0, 0, TimeSpan.Zero)),
            Video("vid-2", "Second Video", null, null),
        };

        var text = YouTubeVideoListExport.BuildText(
            "@salubrioussnail",
            videos,
            new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains("Channel: @salubrioussnail", text, StringComparison.Ordinal);
        Assert.Contains("Captured: 2026-06-03. 2 most recent uploads", text, StringComparison.Ordinal);
        Assert.Contains("249,277", text, StringComparison.Ordinal);
        Assert.Contains("2025-12-16", text, StringComparison.Ordinal);
        Assert.Contains("    https://youtu.be/vid-1", text, StringComparison.Ordinal);
        // Missing views/date render as "?" instead of failing the export.
        Assert.Contains("?", text, StringComparison.Ordinal);
        Assert.Contains("Second Video", text, StringComparison.Ordinal);
        Assert.EndsWith("Total listed: 2\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildText_BlankChannelThrows()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            YouTubeVideoListExport.BuildText("  ", [], DateTimeOffset.UtcNow));
    }

    private static YouTubeChannelVideo Video(string id, string title, long? views, DateTimeOffset? published)
        => new()
        {
            VideoId = id,
            Url = "https://youtu.be/" + id,
            Title = title,
            Duration = null,
            PublishedUtc = published,
            ViewCount = views,
        };
}
