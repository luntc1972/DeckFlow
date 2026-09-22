using DeckFlow.Core.Content;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CreatorSuppressionMatcherTests
{
    [Theory]
    [InlineData("creator-slug")]
    [InlineData("creator alias")]
    [InlineData("  CREATOR-SLUG  ")]
    [InlineData("CREATOR ALIAS")]
    public void IsSuppressed_SlugOrAliasNormalizedMatch_ReturnsTrue(string candidate)
    {
        var matcher = new CreatorSuppressionMatcher(new[]
        {
            new CreatorSuppression
            {
                Slug = "creator-slug",
                Aliases = new[] { "Creator Alias" },
                Reason = "request",
                RequestedUtc = DateTimeOffset.UtcNow,
            },
        });

        Assert.True(matcher.IsSuppressed(candidate));
    }

    [Theory]
    [InlineData("different-creator")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSuppressed_NoMatchOrBlankInput_ReturnsFalse(string candidate)
    {
        var matcher = new CreatorSuppressionMatcher(Array.Empty<CreatorSuppression>());

        Assert.False(matcher.IsSuppressed(candidate));
    }
}
