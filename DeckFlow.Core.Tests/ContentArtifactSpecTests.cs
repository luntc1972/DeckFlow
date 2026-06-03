using DeckFlow.Core.Knowledge;

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

        Assert.Contains(properties, property => property.Name == nameof(ContentArtifactMetadata.YoutubeVideoId));
        Assert.Contains(properties, property => property.Name == nameof(ContentArtifactMetadata.RssGuid));
    }
}
