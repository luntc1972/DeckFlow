using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="ContentKbPromptResolver"/> — the sibling-or-reconstruct resolver that
/// gives the public copy button and the Studio review queue identical paste-ready prompts.
/// </summary>
public sealed class ContentKbPromptResolverTests
{
    private const string Notes =
        "---\ntitle: T\nsource: S\n---\n## Summary\nOff-axis builds.\n\n## Key Clips\n- **[02:49]** A clip.";

    [Theory]
    [InlineData("content-kb/slug/abc123.md", "content-kb/slug/abc123.prompt.md")]
    [InlineData("content-kb/The-Command-Zone/xY_9.md", "content-kb/The-Command-Zone/xY_9.prompt.md")]
    public void PromptPathFor_MapsNotesPathToSibling(string notesPath, string expected)
        => Assert.Equal(expected, ContentKbPromptResolver.PromptPathFor(notesPath));

    [Theory]
    [InlineData("")]
    [InlineData("content-kb/slug/abc123.txt")]
    [InlineData("content-kb/slug/abc123.prompt.md")]
    public void PromptPathFor_NonNotesPath_ReturnsNull(string path)
        => Assert.Null(ContentKbPromptResolver.PromptPathFor(path));

    [Fact]
    public void BuildOrReconstruct_SiblingPresent_ReturnsSiblingVerbatim()
    {
        const string baked = "BAKED PROMPT TEXT";

        var result = ContentKbPromptResolver.BuildOrReconstruct(baked, Notes, "T", "S", "https://x");

        Assert.Equal(baked, result);
    }

    [Fact]
    public void BuildOrReconstruct_NoSibling_ReconstructsFromNotesBody()
    {
        var result = ContentKbPromptResolver.BuildOrReconstruct(null, Notes, "T", "S", "https://x");

        Assert.NotNull(result);
        Assert.Contains("TASK:", result, StringComparison.Ordinal);
        Assert.Contains("Off-axis builds.", result, StringComparison.Ordinal);
        // The reconstructed prompt frames the body only — frontmatter is stripped.
        Assert.DoesNotContain("title: T", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOrReconstruct_NoSiblingAndNoNotes_ReturnsNull()
        => Assert.Null(ContentKbPromptResolver.BuildOrReconstruct(null, null, "T", "S", "https://x"));

    [Fact]
    public void BuildOrReconstruct_BakedEqualsReconstructed_ForSameNotes()
    {
        // What the orchestrator bakes: Wrap over the notes body split from the artifact text.
        var (_, body) = ContentArtifactParser.SplitHeader(Notes);
        var baked = ContentKbPromptWrapper.Wrap("T", "S", "https://x", body);

        // What a pre-bake entry reconstructs at serve/review time from the same notes file.
        var reconstructed = ContentKbPromptResolver.BuildOrReconstruct(null, Notes, "T", "S", "https://x");

        Assert.Equal(baked, reconstructed);
    }
}
