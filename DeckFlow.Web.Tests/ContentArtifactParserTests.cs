using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ContentArtifactParser.SplitHeader"/> — the frontmatter splitter that
/// feeds the public detail page's clean copy-for-ChatGPT text (T-22-12).
/// </summary>
public sealed class ContentArtifactParserTests
{
    [Fact]
    public void SplitHeader_StripsFrontmatter_LeavingBodyOnly()
    {
        var raw = "---\ntitle: Foo\nsource: EDHRECast\n---\n# Heading\n\nBody text.";

        var (header, body) = ContentArtifactParser.SplitHeader(raw);

        Assert.Equal("Foo", header["title"]);
        Assert.Equal("EDHRECast", header["source"]);
        Assert.DoesNotContain("---", body);
        Assert.DoesNotContain("title:", body);
        Assert.Equal("# Heading\n\nBody text.", body);
    }

    [Fact]
    public void SplitHeader_NoFrontmatter_ReturnsRawUnchanged()
    {
        var raw = "# Just a heading\n\nNo frontmatter here.";

        var (header, body) = ContentArtifactParser.SplitHeader(raw);

        Assert.Empty(header);
        Assert.Equal(raw, body);
    }

    [Fact]
    public void SplitHeader_NormalizesCrlf_AndStripsFrontmatter()
    {
        var raw = "---\r\ntitle: Bar\r\n---\r\nBody after CRLF.";

        var (header, body) = ContentArtifactParser.SplitHeader(raw);

        Assert.Equal("Bar", header["title"]);
        Assert.Equal("Body after CRLF.", body);
    }

    [Fact]
    public void SplitHeader_UnterminatedFrontmatter_ReturnsRaw()
    {
        // Opening --- but no closing --- → not valid frontmatter; return the whole document.
        var raw = "---\ntitle: Broken\nstill going";

        var (header, body) = ContentArtifactParser.SplitHeader(raw);

        Assert.Empty(header);
        Assert.Equal(raw, body);
    }

    [Fact]
    public void SplitHeader_KeyLookupIsCaseInsensitive()
    {
        var raw = "---\nTitle: Mixed\n---\nbody";

        var (header, _) = ContentArtifactParser.SplitHeader(raw);

        Assert.Equal("Mixed", header["title"]);
        Assert.Equal("Mixed", header["TITLE"]);
    }
}
