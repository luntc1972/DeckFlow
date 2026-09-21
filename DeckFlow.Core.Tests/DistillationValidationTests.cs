using DeckFlow.Core.Knowledge;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class DistillationValidationTests
{
    [Fact]
    public void SanitizeTags_DropsUnknownWhitespaceAndDuplicates_PreservesFirstValidOrder()
    {
        var payload = new TagsPayload(
            ["tempo", "Aristocrats", "  ", "ARISTOCRATS", "mill", "tokens"],
            ["", "Optimized", "optimized", "battlecruiser"],
            ["artifacts", "draw", "DRAW", "ramp"]);

        var sanitized = DistillationValidation.SanitizeTags(payload);

        Assert.Equal(["aristocrats", "tokens"], sanitized.Archetype);
        Assert.Equal(["Optimized"], sanitized.Bracket);
        Assert.Equal(["draw", "ramp"], sanitized.CardCategory);
    }

    [Fact]
    public void SanitizeTags_AllInvalidOrNullArrays_AllowsEmptyResults()
    {
        var payload = new TagsPayload(null!, null!, [" ", "tempo"]);

        var sanitized = DistillationValidation.SanitizeTags(payload);

        Assert.Empty(sanitized.Archetype);
        Assert.Empty(sanitized.Bracket);
        Assert.Empty(sanitized.CardCategory);
    }

    [Fact]
    public void SanitizeClips_DropsNegativeTimestamps_ClampsToEight_AndPreservesOrder()
    {
        var clips = new List<ClipItem>
        {
            new(-1, "drop"),
            new(10, "1"),
            new(20, "2"),
            new(30, "3"),
            new(40, "4"),
            new(50, "5"),
            new(60, "6"),
            new(70, "7"),
            new(80, "8"),
            new(90, "9"),
        };

        var sanitized = DistillationValidation.SanitizeClips(clips);

        Assert.Equal(8, sanitized.Count);
        Assert.Equal([10, 20, 30, 40, 50, 60, 70, 80], sanitized.Select(clip => clip.TimestampSeconds).ToArray());
    }

    [Fact]
    public void SanitizeClips_AcceptsFewerThanThree()
    {
        var sanitized = DistillationValidation.SanitizeClips(
            [new ClipItem(12, "one"), new ClipItem(null, "two")]);

        Assert.Equal(2, sanitized.Count);
    }

    [Fact]
    public void TruncateSummary_TruncatesToFirstTwoHundredWords()
    {
        var summary = string.Join(" ", Enumerable.Range(1, 205).Select(index => $"word{index}"));

        var truncated = DistillationValidation.TruncateSummary(summary);

        Assert.Equal(200, DistillationValidation.CountWords(truncated));
        Assert.DoesNotContain("word201", truncated, StringComparison.Ordinal);
    }
    [Theory]
    [InlineData("He said \"one two three four five six seven eight\" then", 8)]
    [InlineData("The player's deck is ready", 0)]
    [InlineData("He said ‘one two three four five six seven eight’ then", 8)]
    [InlineData("He said \"go on\" then", 0)]
    [InlineData("He said \"one two three four five six seven", 7)]
    [InlineData("He said \"one two three four\nfive six seven eight\" then", 8)]
    public void QuotedWordCount_QuoteScenarios_ReturnsExpectedCount(string excerpt, int expected)
    {
        Assert.Equal(expected, DistillationValidation.QuotedWordCount(excerpt));
    }

    [Fact]
    public void ValidateClips_QuoteOverCap_Throws()
    {
        var excerpt = "He said \"" + string.Join(' ', Enumerable.Range(0, 26).Select(index => "w" + index)) + "\" then";

        Assert.Throws<InvalidOperationException>(() => DistillationValidation.ValidateClips(
            [new ClipItem(1, excerpt), new ClipItem(2, "two"), new ClipItem(3, "three")]));
    }
}
