using System.IO;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
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
    public void ToText_EmitsContentTypeAndStatedRulesBetweenGeneratedUtcAndClosingFence()
    {
        var text = ContentArtifactWriter.ToText(
            CreateMetadata() with
            {
                ContentType = "youtube",
                StatedRules = [CreateRule()]
            },
            "summary",
            []).ReplaceLineEndings("\n");

        var generatedLine = "generated_utc: \"2026-05-27T12:34:56Z\"\n";
        var contentTypeLine = "content_type: \"youtube\"\n";
        var statedRulesLinePrefix = "stated_rules: [{\"category\":\"ramp\"";
        var generatedIndex = text.IndexOf(generatedLine, StringComparison.Ordinal);
        var contentTypeIndex = text.IndexOf(contentTypeLine, StringComparison.Ordinal);
        var statedRulesIndex = text.IndexOf(statedRulesLinePrefix, StringComparison.Ordinal);
        var closingFenceIndex = text.IndexOf("---\n\n## Summary", StringComparison.Ordinal);

        Assert.True(generatedIndex >= 0);
        Assert.True(contentTypeIndex > generatedIndex);
        Assert.True(statedRulesIndex > contentTypeIndex);
        Assert.True(closingFenceIndex > statedRulesIndex);
    }

    [Fact]
    public void ToText_PreservesPreexistingFrontmatterAndBodyBytes()
    {
        const string summary = "This is a concise standalone summary under the upstream 200-word limit.";
        var clips = new (int? TimestampSeconds, string Excerpt)[]
        {
            (134, "The host explains why cheap interaction matters."),
            (null, "The guest gives an untimed strategic takeaway."),
        };
        var baseline = """
            ---
            source: "The Command Zone"
            title: "cEDH Tier List"
            url: "https://www.youtube.com/watch?v=abc_123"
            video_id: "abc_123"
            tags:
              archetype: ["combo","control"]
              bracket: ["cEDH"]
              card_category: ["win-cons","counter"]
            generated_utc: "2026-05-27T12:34:56Z"
            ---

            ## Summary

            This is a concise standalone summary under the upstream 200-word limit.

            ## Key Clips

            - **[02:14]** The host explains why cheap interaction matters.
            - The guest gives an untimed strategic takeaway.

            ## Tags

            **Archetypes/Strategy:** combo, control
            **Format/Bracket:** cEDH
            **Card Categories:** win-cons, counter
            
            """.ReplaceLineEndings("\n");

        var text = ContentArtifactWriter.ToText(
            CreateMetadata(),
            summary,
            clips).ReplaceLineEndings("\n");
        var normalized = text.Replace("content_type: \"youtube\"\n", string.Empty, StringComparison.Ordinal)
            .Replace("stated_rules: []\n", string.Empty, StringComparison.Ordinal);

        Assert.Equal(baseline, normalized);
    }

    [Fact]
    public void ToText_EmitsSnakeCaseStatedRuleKeysOnly()
    {
        var text = ContentArtifactWriter.ToText(
            CreateMetadata() with
            {
                ContentType = "youtube",
                StatedRules = [CreateRule()]
            },
            "summary",
            []);

        Assert.Contains("\"video_date\":", text, StringComparison.Ordinal);
        Assert.Contains("\"card_reference\":", text, StringComparison.Ordinal);
        Assert.Contains("\"card_grounded\":", text, StringComparison.Ordinal);
        Assert.Contains("\"clip_ts\":", text, StringComparison.Ordinal);
        Assert.Contains("\"value_min\":", text, StringComparison.Ordinal);
        Assert.Contains("\"source_clip\":", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"VideoDateUtc\":", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ClipTimestampSeconds\":", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SourceClip\":", text, StringComparison.Ordinal);
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
            ContentType = "youtube",
            ArchetypeTags = ["combo", "control"],
            BracketTags = ["cEDH"],
            CardCategoryTags = ["win-cons", "counter"],
            GeneratedUtc = DateTimeOffset.Parse("2026-05-27T12:34:56Z"),
        };

    private static StatedRuleCandidate CreateRule()
        => new()
        {
            Category = "ramp",
            Metric = "lands",
            Value = 37,
            ValueMin = 36,
            ValueMax = 38,
            Comparator = "range",
            Condition = "control shells",
            ClipTimestampSeconds = 134,
            SourceClip = "Play 36-38 lands in control shells.",
            Confidence = 0.91,
            CardReference = "Rhystic Study",
            CardGrounded = true,
            VideoDateUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
        };
}
