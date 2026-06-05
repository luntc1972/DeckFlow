using System.Text;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ContentKbClipParser"/>.
/// </summary>
public sealed class ContentKbClipParserTests
{
    [Fact]
    public void ParseKeyClips_ExtractsTimestampedBullets_AndStopsAtTags()
    {
        const string body = """
## Summary

Ignore this summary text.

## Key Clips

- **[02:14]** First clip text.
- **[10:05]** Second clip text.

## Tags

Should not be parsed.
""";

        var clips = ContentKbClipParser.ParseKeyClips(body);

        Assert.Equal(2, clips.Count);
        Assert.Equal("02:14", clips[0].TimestampLabel);
        Assert.Equal("First clip text.", clips[0].Excerpt);
        Assert.Equal("10:05", clips[1].TimestampLabel);
        Assert.Equal("Second clip text.", clips[1].Excerpt);
    }

    [Fact]
    public void ParseKeyClips_IgnoresSummarySection()
    {
        const string body = """
## Summary

- **[00:10]** Summary bullets do not count.

## Key Clips

- **[02:14]** Real clip text.
""";

        var clips = ContentKbClipParser.ParseKeyClips(body);

        Assert.Single(clips);
        Assert.Equal("02:14", clips[0].TimestampLabel);
        Assert.Equal("Real clip text.", clips[0].Excerpt);
    }

    [Fact]
    public void ParseKeyClips_TruncatesOver150Words_AtSentenceBoundary_WithEllipsis()
    {
        var prefix = string.Join(" ", Enumerable.Range(1, 120).Select(i => $"alpha{i}")) + ".";
        var overflow = string.Join(" ", Enumerable.Range(1, 40).Select(i => $"beta{i}")) + ".";
        var body = $$"""
## Key Clips

- **[02:14]** {{prefix}} {{overflow}}
""";

        var clips = ContentKbClipParser.ParseKeyClips(body);

        var clip = Assert.Single(clips);
        Assert.EndsWith("...", clip.Excerpt, StringComparison.Ordinal);
        Assert.Contains(prefix, clip.Excerpt, StringComparison.Ordinal);
        Assert.DoesNotContain("beta40", clip.Excerpt, StringComparison.Ordinal);
        Assert.True(clip.Excerpt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 121);
    }

    [Fact]
    public void ParseKeyClips_NoSection_ReturnsEmptyList()
    {
        const string body = """
## Summary

Nothing to parse here.
""";

        var clips = ContentKbClipParser.ParseKeyClips(body);

        Assert.Empty(clips);
    }

    [Fact]
    public void ParseArtifact_NoFrontMatter_ReturnsEmptyList()
    {
        const string raw = """
## Key Clips

- **[02:14]** This should not be parsed without front matter.
""";

        var clips = ContentKbClipParser.ParseArtifact(raw);

        Assert.Empty(clips);
    }

    [Fact]
    public void BuildDeepLink_RecognizedYoutubeWatchUrl_AppendsTimestampParam()
    {
        var deepLink = ContentKbClipParser.BuildDeepLink("https://www.youtube.com/watch?v=abc123&list=playlist", "02:14");

        Assert.EndsWith("t=134s", deepLink, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDeepLink_UnknownSourceUrl_ReturnsBareUrl()
    {
        const string sourceUrl = "https://example.com/video/abc123";

        var deepLink = ContentKbClipParser.BuildDeepLink(sourceUrl, "02:14");

        Assert.Equal(sourceUrl, deepLink);
    }

    [Theory]
    [InlineData("1:02:14", 3734)]
    [InlineData("02:14", 134)]
    public void ParseTimestampLabel_ParsesSupportedTimestampFormats(string timestampLabel, int expectedSeconds)
    {
        var seconds = ContentKbClipParser.ParseTimestampLabelToSeconds(timestampLabel);

        Assert.Equal(expectedSeconds, seconds);
    }
}
