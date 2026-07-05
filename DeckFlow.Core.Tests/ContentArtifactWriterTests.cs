using System.IO;
using DeckFlow.Core.Knowledge;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for rendering and writing Content KB artifact files.
/// </summary>
public sealed class ContentArtifactWriterTests : IDisposable
{
    private readonly string _artifactRoot;

    public ContentArtifactWriterTests()
    {
        _artifactRoot = Path.Combine(Path.GetTempPath(), $"content-artifact-writer-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_artifactRoot))
        {
            Directory.Delete(_artifactRoot, recursive: true);
        }
    }

    [Fact]
    public void ToText_EmitsLockedSectionsTagsAndClipTimestamps()
    {
        const string summary = "This is a concise standalone summary under the upstream 200-word limit.";

        var text = ContentArtifactWriter.ToText(
            CreateMetadata(),
            summary,
            [
                (134, "The host explains why cheap interaction matters."),
                (null, "The guest gives an untimed strategic takeaway."),
            ]);

        Assert.StartsWith("---", text, StringComparison.Ordinal);
        Assert.Contains("## Summary", text, StringComparison.Ordinal);
        Assert.Contains("## Key Clips", text, StringComparison.Ordinal);
        Assert.Contains("## Tags", text, StringComparison.Ordinal);
        Assert.Contains(summary, text, StringComparison.Ordinal);
        Assert.Contains($"  archetype: {ContentArtifactSpec.SerializeTags(["combo", "control"])}", text, StringComparison.Ordinal);
        Assert.Contains($"  bracket: {ContentArtifactSpec.SerializeTags(["cEDH"])}", text, StringComparison.Ordinal);
        Assert.Contains($"  card_category: {ContentArtifactSpec.SerializeTags(["win-cons", "counter"])}", text, StringComparison.Ordinal);
        Assert.Contains("- **[02:14]** The host explains why cheap interaction matters.", text, StringComparison.Ordinal);

        var unknownTimestampClip = Assert.Single(
            text.Split(Environment.NewLine),
            line => line.Contains("untimed strategic takeaway", StringComparison.Ordinal));
        Assert.StartsWith("- The guest", unknownTimestampClip, StringComparison.Ordinal);
        Assert.DoesNotContain("[00:00]", unknownTimestampClip, StringComparison.Ordinal);
        Assert.DoesNotContain('[', unknownTimestampClip);
    }

    [Fact]
    public void ComputeRelativeArtifactPath_ReturnsSanitizedRelativePath()
    {
        var relativePath = ContentArtifactWriter.ComputeRelativeArtifactPath("Command Zone", "abc_123");

        Assert.Equal("content-kb/Command-Zone/abc_123.md", relativePath);
        Assert.False(Path.IsPathRooted(relativePath));
    }

    [Theory]
    [InlineData("../secret", "abc123")]
    [InlineData("/rooted", "abc123")]
    [InlineData("source", "../secret")]
    [InlineData("source", "C:\\secret")]
    public void ComputeRelativeArtifactPath_RejectsTraversalAndRootedSegments(string sourceSlug, string videoId)
    {
        Assert.Throws<ArgumentException>(() => ContentArtifactWriter.ComputeRelativeArtifactPath(sourceSlug, videoId));
    }

    [Fact]
    public void WriteFile_CreatesParentDirectoryAndWritesText()
    {
        const string text = "artifact body";

        var writtenPath = ContentArtifactWriter.WriteFile(_artifactRoot, "Command Zone", "abc_123", text);

        Assert.True(Path.IsPathFullyQualified(writtenPath));
        Assert.True(File.Exists(writtenPath));
        Assert.EndsWith(Path.Combine("Command-Zone", "abc_123.md"), writtenPath, StringComparison.Ordinal);
        Assert.Equal(text, File.ReadAllText(writtenPath));
    }

    [Fact]
    public void ComputeRelativePromptPath_ReturnsSiblingPromptPath()
    {
        var relativePath = ContentArtifactWriter.ComputeRelativePromptPath("Command Zone", "abc_123");

        Assert.Equal("content-kb/Command-Zone/abc_123.prompt.md", relativePath);
        Assert.False(Path.IsPathRooted(relativePath));
    }

    [Fact]
    public void WritePromptFile_WritesSiblingNextToNotes()
    {
        ContentArtifactWriter.WriteFile(_artifactRoot, "Command Zone", "abc_123", "notes body");

        var promptPath = ContentArtifactWriter.WritePromptFile(_artifactRoot, "Command Zone", "abc_123", "paste-ready prompt");

        Assert.True(File.Exists(promptPath));
        Assert.EndsWith(Path.Combine("Command-Zone", "abc_123.prompt.md"), promptPath, StringComparison.Ordinal);
        Assert.Equal("paste-ready prompt", File.ReadAllText(promptPath));
        // Sibling sits in the same directory as the notes artifact.
        Assert.Equal(
            Path.GetDirectoryName(Path.Combine(_artifactRoot, "Command-Zone", "abc_123.md")),
            Path.GetDirectoryName(promptPath));
    }

    private static ContentArtifactMetadata CreateMetadata()
        => new()
        {
            Source = "The Command Zone",
            Title = "cEDH Tier List",
            Url = "https://www.youtube.com/watch?v=abc_123",
            YoutubeVideoId = "abc_123",
            RssGuid = null,
            ArchetypeTags = ["combo", "control"],
            BracketTags = ["cEDH"],
            CardCategoryTags = ["win-cons", "counter"],
            GeneratedUtc = DateTimeOffset.Parse("2026-05-27T12:34:56Z"),
        };
}
