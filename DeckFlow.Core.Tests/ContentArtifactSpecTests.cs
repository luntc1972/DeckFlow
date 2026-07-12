using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for the Content KB artifact file-format and tag JSON contracts.
/// </summary>
public sealed class ContentArtifactSpecTests
{
    /// <summary>
    /// Verifies the artifact format documents the required front-matter and body sections.
    /// </summary>
    [Fact]
    public void ArtifactFileFormat_ContainsRequiredSections()
    {
        Assert.StartsWith("---", ContentArtifactSpec.ArtifactFileFormat, StringComparison.Ordinal);
        Assert.Contains("content_type:", ContentArtifactSpec.ArtifactFileFormat, StringComparison.Ordinal);
        Assert.Contains("stated_rules:", ContentArtifactSpec.ArtifactFileFormat, StringComparison.Ordinal);
        Assert.Contains("## Summary", ContentArtifactSpec.ArtifactFileFormat, StringComparison.Ordinal);
        Assert.Contains("## Key Clips", ContentArtifactSpec.ArtifactFileFormat, StringComparison.Ordinal);
        Assert.Contains("## Tags", ContentArtifactSpec.ArtifactFileFormat, StringComparison.Ordinal);
        Assert.Contains("\n---\n", ContentArtifactSpec.ArtifactFileFormat, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies tag serialization uses JSON arrays and round-trips declared tag lists.
    /// </summary>
    [Fact]
    public void SerializeTags_RoundTripsJsonArray()
    {
        var tags = new[] { "combo", "control" };

        var serialized = ContentArtifactSpec.SerializeTags(tags);
        var deserialized = ContentArtifactSpec.DeserializeTags(serialized);

        Assert.Equal("[\"combo\",\"control\"]", serialized);
        Assert.Equal(tags, deserialized);
    }

    /// <summary>
    /// Verifies empty and missing tag lists canonicalize to non-null empty lists.
    /// </summary>
    [Fact]
    public void SerializeTags_EmptyListCanonicalizesToEmptyJsonArray()
    {
        var serialized = ContentArtifactSpec.SerializeTags(Array.Empty<string>());
        var deserialized = ContentArtifactSpec.DeserializeTags(serialized);
        var fromNull = ContentArtifactSpec.DeserializeTags(null);
        var fromEmpty = ContentArtifactSpec.DeserializeTags(string.Empty);

        Assert.Equal("[]", serialized);
        Assert.NotNull(deserialized);
        Assert.Empty(deserialized);
        Assert.Empty(fromNull);
        Assert.Empty(fromEmpty);
    }

    /// <summary>
    /// Verifies artifact metadata carries natural keys for both YouTube and RSS sources.
    /// </summary>
    [Fact]
    public void ContentArtifactMetadata_ExposesYoutubeAndRssNaturalKeys()
    {
        var properties = typeof(ContentArtifactMetadata).GetProperties();

        Assert.Contains(properties, property => property.Name == nameof(ContentArtifactMetadata.ContentType));
        Assert.Contains(properties, property => property.Name == nameof(ContentArtifactMetadata.StatedRules));
        Assert.Contains(properties, property => property.Name == nameof(ContentArtifactMetadata.YoutubeVideoId));
        Assert.Contains(properties, property => property.Name == nameof(ContentArtifactMetadata.RssGuid));
    }

    [Fact]
    public void SerializeStatedRules_UsesLockedSnakeCaseKeys()
    {
        var serialized = ContentArtifactSpec.SerializeStatedRules(
            [
                new StatedRuleCandidate
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
                }
            ]);

        Assert.Contains("\"category\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"metric\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"value\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"value_min\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"value_max\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"comparator\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"condition\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"clip_ts\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"source_clip\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"confidence\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"card_reference\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"card_grounded\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"video_date\":", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"VideoDateUtc\":", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ClipTimestampSeconds\":", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SourceClip\":", serialized, StringComparison.Ordinal);
    }
}
