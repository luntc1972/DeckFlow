using System.Text.Json;
using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ContentKbExcerpt"/> JSON round-tripping.
/// </summary>
public sealed class ContentKbExcerptTests
{
    [Fact]
    public void ContentKbExcerpt_JsonRoundTrip_PreservesAllProperties()
    {
        var excerpt = new ContentKbExcerpt
        {
            Source = "EDHRECast",
            Title = "How To Build Better Removal Suites",
            VideoUrl = "https://www.youtube.com/watch?v=abc123&t=134s",
            TimestampLabel = "02:14",
            Excerpt = "Run enough interaction that you can trade up on tempo.",
            HarvestDate = new DateTimeOffset(2026, 6, 5, 12, 34, 56, TimeSpan.Zero),
            Score = 7.25
        };

        var json = JsonSerializer.Serialize(excerpt);
        var roundTripped = JsonSerializer.Deserialize<ContentKbExcerpt>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(excerpt.Source, roundTripped.Source);
        Assert.Equal(excerpt.Title, roundTripped.Title);
        Assert.Equal(excerpt.VideoUrl, roundTripped.VideoUrl);
        Assert.Equal(excerpt.TimestampLabel, roundTripped.TimestampLabel);
        Assert.Equal(excerpt.Excerpt, roundTripped.Excerpt);
        Assert.Equal(excerpt.HarvestDate, roundTripped.HarvestDate);
        Assert.Equal(excerpt.Score, roundTripped.Score);
    }
}
