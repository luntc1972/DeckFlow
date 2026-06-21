using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Unit tests for <see cref="CreatorNameResolver"/> — pure string parsing, no I/O.
/// Covers: normal artifact path, missing/odd paths, and channel-title fallback.
/// </summary>
public sealed class CreatorNameResolverTests
{
    // ── FromArtifactPath ───────────────────────────────────────────────────

    [Fact]
    public void FromArtifactPath_NormalPath_ReturnsCreatorSlug()
    {
        // content-kb/<creator>/<id>.md → <creator>
        var result = CreatorNameResolver.FromArtifactPath("content-kb/salubrioussnail/abc123.md");
        Assert.Equal("salubrioussnail", result);
    }

    [Fact]
    public void FromArtifactPath_BackslashPath_ReturnsCreatorSlug()
    {
        // Windows-style separator still parsed correctly.
        var result = CreatorNameResolver.FromArtifactPath("content-kb\\the-command-zone\\xyz.md");
        Assert.Equal("the-command-zone", result);
    }

    [Fact]
    public void FromArtifactPath_NullOrEmpty_ReturnsUnknown()
    {
        Assert.Equal("Unknown", CreatorNameResolver.FromArtifactPath(null));
        Assert.Equal("Unknown", CreatorNameResolver.FromArtifactPath(string.Empty));
        Assert.Equal("Unknown", CreatorNameResolver.FromArtifactPath("   "));
    }

    [Fact]
    public void FromArtifactPath_TooShortPath_ReturnsUnknown()
    {
        // Only one or two segments — no creator slot at index 1.
        Assert.Equal("Unknown", CreatorNameResolver.FromArtifactPath("content-kb"));
        Assert.Equal("Unknown", CreatorNameResolver.FromArtifactPath("content-kb/onlyone"));
    }

    [Fact]
    public void FromArtifactPath_RootedPath_ReturnsUnknown()
    {
        // Reject absolute paths — security guard mirrors ReadArtifactSafe.
        Assert.Equal("Unknown", CreatorNameResolver.FromArtifactPath("/content-kb/creator/id.md"));
        Assert.Equal("Unknown", CreatorNameResolver.FromArtifactPath("C:\\content-kb\\creator\\id.md"));
    }

    [Fact]
    public void FromArtifactPath_TraversalSegment_ReturnsUnknown()
    {
        // ".." in any segment must be rejected (T-62-02).
        Assert.Equal("Unknown", CreatorNameResolver.FromArtifactPath("content-kb/../creator/id.md"));
        Assert.Equal("Unknown", CreatorNameResolver.FromArtifactPath("content-kb/creator/../id.md"));
    }

    [Fact]
    public void FromArtifactPath_OddPath_ReturnsSlugOrUnknown()
    {
        // Paths with extra nesting still return the second segment.
        Assert.Equal("creator", CreatorNameResolver.FromArtifactPath("content-kb/creator/sub/id.md"));
    }

    // ── FromChannelTitle ───────────────────────────────────────────────────

    [Fact]
    public void FromChannelTitle_Normal_ReturnsTrimmedTitle()
    {
        Assert.Equal("The Command Zone", CreatorNameResolver.FromChannelTitle("  The Command Zone  "));
        Assert.Equal("SaluSnail", CreatorNameResolver.FromChannelTitle("SaluSnail"));
    }

    [Fact]
    public void FromChannelTitle_NullOrWhitespace_ReturnsUnknown()
    {
        Assert.Equal("Unknown", CreatorNameResolver.FromChannelTitle(null));
        Assert.Equal("Unknown", CreatorNameResolver.FromChannelTitle(string.Empty));
        Assert.Equal("Unknown", CreatorNameResolver.FromChannelTitle("   "));
    }
}
