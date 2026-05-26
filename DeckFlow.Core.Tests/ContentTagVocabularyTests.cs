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
        Assert.Equal("youtube_channel", ContentSourceType.Youtube);
        Assert.Equal("podcast_rss", ContentSourceType.Podcast);
        Assert.Equal("archetype", ContentTagDimension.Archetype);
        Assert.Equal("bracket", ContentTagDimension.Bracket);
        Assert.Equal("card_category", ContentTagDimension.CardCategory);
    }
}
