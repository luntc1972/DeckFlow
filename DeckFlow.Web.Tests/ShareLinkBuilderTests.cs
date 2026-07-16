using System;
using DeckFlow.Web.Seo;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="ShareLinkBuilder"/> channel URL generation.</summary>
public sealed class ShareLinkBuilderTests
{
    [Fact]
    public void Build_uses_title_and_encodes_channels()
    {
        var s = ShareLinkBuilder.Build("https://www.deckflow.gg/manabase", "MTG Mana Base Analyzer");

        Assert.Equal("MTG Mana Base Analyzer", s.ShareTitle);
        Assert.Contains("free MTG deck tool", s.ShareText);
        Assert.StartsWith("https://www.reddit.com/submit?", s.RedditUrl);
        Assert.Contains("url=" + Uri.EscapeDataString("https://www.deckflow.gg/manabase"), s.RedditUrl);
        Assert.Contains("title=" + Uri.EscapeDataString("MTG Mana Base Analyzer"), s.RedditUrl);
        Assert.StartsWith("https://twitter.com/intent/tweet?", s.XUrl);
        Assert.Contains("url=" + Uri.EscapeDataString("https://www.deckflow.gg/manabase"), s.XUrl);
        Assert.StartsWith("https://bsky.app/intent/compose?text=", s.BlueskyUrl);
        Assert.Contains(Uri.EscapeDataString("https://www.deckflow.gg/manabase"), s.BlueskyUrl);
    }

    [Fact]
    public void Build_falls_back_to_DeckFlow_when_title_blank()
    {
        var s = ShareLinkBuilder.Build("https://www.deckflow.gg/", "   ");
        Assert.Equal("DeckFlow", s.ShareTitle);
    }

    [Fact]
    public void Build_encodes_special_chars_so_query_is_not_broken()
    {
        var s = ShareLinkBuilder.Build("https://www.deckflow.gg/x", "Tokens & #cEDH");

        Assert.Contains("title=" + Uri.EscapeDataString("Tokens & #cEDH"), s.RedditUrl);
        // Raw unescaped ampersand must not appear in the title param.
        Assert.DoesNotContain("title=Tokens & #cEDH", s.RedditUrl);
    }
}
