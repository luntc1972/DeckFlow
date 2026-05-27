using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for content tag vocabulary and related tag discriminator contracts.
/// </summary>
public sealed class ContentTagVocabularyTests
{
    /// <summary>
    /// Verifies shared discriminator constants match the planned database CHECK values.
    /// </summary>
    [Fact]
    public void DiscriminatorConstants_MatchDatabaseCheckValues()
    {
        Assert.Equal("captions", TranscriptSource.Captions);
        Assert.Equal("whisper", TranscriptSource.Whisper);
        Assert.Equal("pending", TranscriptStatus.Pending);
        Assert.Equal("captions", TranscriptStatus.Captions);
        Assert.Equal("whisper", TranscriptStatus.Whisper);
        Assert.Equal("failed", TranscriptStatus.Failed);
        Assert.Equal("skipped_over_cap", TranscriptStatus.SkippedOverCap);
        Assert.Equal("skipped_no_captions", TranscriptStatus.SkippedNoCaptions);
        Assert.Equal("youtube_channel", ContentSourceType.Youtube);
        Assert.Equal("podcast_rss", ContentSourceType.Podcast);
        Assert.Equal("archetype", ContentTagDimension.Archetype);
        Assert.Equal("bracket", ContentTagDimension.Bracket);
        Assert.Equal("card_category", ContentTagDimension.CardCategory);
    }

    /// <summary>
    /// Verifies unknown tag dimensions are rejected.
    /// </summary>
    [Fact]
    public void IsValid_RejectsUnknownDimension()
    {
        var isValid = ContentTagVocabulary.IsValid("color", "blue");

        Assert.False(isValid);
    }

    /// <summary>
    /// Verifies unknown values within a known tag dimension are rejected.
    /// </summary>
    [Fact]
    public void IsValid_RejectsUnknownValueInKnownDimension()
    {
        var isValid = ContentTagVocabulary.IsValid(ContentTagDimension.Archetype, "not-a-real-archetype");

        Assert.False(isValid);
    }

    /// <summary>
    /// Verifies every declared allowlist value is accepted for its dimension.
    /// </summary>
    [Fact]
    public void IsValid_AcceptsEveryDeclaredValueAcrossAllDimensions()
    {
        Assert.All(
            ContentTagVocabulary.Archetypes,
            value => Assert.True(ContentTagVocabulary.IsValid(ContentTagDimension.Archetype, value)));
        Assert.All(
            ContentTagVocabulary.Brackets,
            value => Assert.True(ContentTagVocabulary.IsValid(ContentTagDimension.Bracket, value)));
        Assert.All(
            ContentTagVocabulary.CardCategories,
            value => Assert.True(ContentTagVocabulary.IsValid(ContentTagDimension.CardCategory, value)));
    }
}
