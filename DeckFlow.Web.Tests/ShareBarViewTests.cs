using System.IO;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>File-scan guard that the share-bar partial keeps its channels + a11y markers.</summary>
public sealed class ShareBarViewTests
{
    private static string ReadPartial()
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "DeckFlow.Web", "Views", "Shared", "_ShareBar.cshtml"));

    [Fact]
    public void ShareBar_has_channels_copy_native_and_aria()
    {
        var html = ReadPartial();

        Assert.Contains("@model DeckFlow.Web.Seo.ShareLinks", html);
        Assert.Contains("aria-label=\"Share DeckFlow\"", html);
        Assert.Contains("data-share-bar", html);
        Assert.Contains("share-bar__copy", html);
        Assert.Contains("share-bar__native", html);
        Assert.Contains("hidden", html);
        Assert.Contains("@Model.RedditUrl", html);
        Assert.Contains("@Model.XUrl", html);
        Assert.Contains("@Model.BlueskyUrl", html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
    }
}
