using DeckFlow.Web.Services.PromptBuilders.Analysis;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Verifies computed bracket facts across all three independently worded Analysis variants,
/// including input-not-floor framing, score-on goldens, and null/whitespace byte identity.
/// </summary>
public sealed class AnalysisBracketFactsPromptParityTests
{
    private static string Build(string platformName, string? bracketFactsText, string? scoreBlockText = null)
        => AnalysisPromptParityHarness.Build(platformName,
            new AnalysisPromptEnrichments(BracketFactsText: bracketFactsText, ScoreBlockText: scoreBlockText));

    private static string StartMarker(string platformName) =>
        platformName == "Claude" ? "<bracket_facts>" : "## Computed bracket facts";

    private static string EndMarker(string platformName) =>
        platformName == "Claude" ? "</bracket_facts>" + Environment.NewLine : "";

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void BracketFacts_Block_AppearsInAllThreeVariants(string platformName)
    {
        const string facts = "BRACKET_FACTS_SENTINEL";
        var result = Build(platformName, facts, "SCORE_SENTINEL");
        Assert.Equal(1, result.Split(facts, StringSplitOptions.None).Length - 1);
        var startMarker = StartMarker(platformName);
        var start = result.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start > result.IndexOf("SCORE_SENTINEL", StringComparison.Ordinal));
        var endMarker = facts + Environment.NewLine + EndMarker(platformName);
        var end = result.IndexOf(endMarker, start, StringComparison.Ordinal) + endMarker.Length;
        var block = PacketByteIdentityFixtures.NormalizeForGoldenComparison(result[start..end]);
        Assert.Equal(AnalysisGoldens.BracketFactsBlock(platformName), block);
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void BracketFacts_NullPath_ByteIdenticalToExcisedPath(string platformName)
    {
        const string sentinel = "BRACKET_FACTS_SENTINEL";
        var withFacts = Build(platformName, sentinel);
        var nullPath = Build(platformName, null);
        Assert.Equal(nullPath, Build(platformName, ""));
        Assert.Equal(nullPath, Build(platformName, " \t\r\n"));
        var startMarker = Environment.NewLine + StartMarker(platformName) + Environment.NewLine;
        var start = withFacts.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        var endMarker = sentinel + Environment.NewLine + EndMarker(platformName);
        var end = withFacts.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end >= 0);
        var excised = withFacts.Remove(start, end + endMarker.Length - start);
        if (platformName == PacketByteIdentityFixtures.ChatGpt)
        {
            var validationBlock = PacketByteIdentityFixtures.ChatGptHeuristicValidationBlock;
            var validationIndex = excised.IndexOf(validationBlock, StringComparison.Ordinal);
            Assert.True(validationIndex >= 0);
            excised = excised.Remove(validationIndex, validationBlock.Length);
        }

        Assert.Equal(nullPath, excised);
    }

    [Theory]
    [InlineData("ChatGPT", "\n")]
    [InlineData("Claude", "\n")]
    [InlineData("Gemini", "\n")]
    [InlineData("ChatGPT", "\r\n")]
    [InlineData("Claude", "\r\n")]
    [InlineData("Gemini", "\r\n")]
    public void BracketFacts_TrailingNewline_ByteIdenticalToTrimmed_AllVariants(string platformName, string trailingNewline)
    {
        var facts = "BRACKET_FACTS_SENTINEL" + trailingNewline;
        var withTrailingNewline = Build(platformName, facts);
        var trimmed = Build(platformName, facts.TrimEnd());

        Assert.Equal(trimmed, withTrailingNewline);
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void BracketFacts_FramedAsInputNotFloor_AllVariants(string platformName)
    {
        const string sentinel = "BRACKET_FACTS_SENTINEL";
        var result = Build(platformName, sentinel);
        var marker = StartMarker(platformName);
        var start = result.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        var framing = result[start..result.IndexOf(sentinel, start, StringComparison.Ordinal)];
        Assert.Contains("input", framing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("your own bracket judgment", framing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a minimum", framing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("floor", framing, StringComparison.OrdinalIgnoreCase);
    }
}
