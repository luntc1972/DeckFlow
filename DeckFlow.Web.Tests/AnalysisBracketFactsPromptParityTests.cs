using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Verifies computed bracket facts across all three independently worded Analysis variants,
/// including input-not-floor framing, score-on goldens, and null/whitespace byte identity.
/// </summary>
public sealed class AnalysisBracketFactsPromptParityTests
{
    // Why: ADR-0001 — no shared helper; concrete variants instantiated inline.
    private static AnalysisPromptVariantRegistry BuildRegistry() =>
        new(new IAnalysisPromptVariant[]
        {
            new ChatGptAnalysisPromptVariant(),
            new ClaudeAnalysisPromptVariant(),
            new GeminiAnalysisPromptVariant(),
        });

    private static DeckAnalysisRequest BuildMinimalRequest() =>
        new()
        {
            Format = "Commander",
            TargetCommanderBracket = "cEDH",
        };

    private static string BuildDecklistText() => "1 Sol Ring";

    private static string BuildReferenceText() => "Reference text";

    private static string BuildSchemaJson() => "{}";

    private static string Build(string platformName, string? bracketFactsText, string? scoreBlockText = null)
    {
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);
        return registry.Build(
            platform,
            BuildMinimalRequest(),
            BuildDecklistText(),
            BuildReferenceText(),
            BuildSchemaJson(),
            commanderName: null,
            selectedQuestionIds: [],
            bannedCards: [],
            comboResult: null,
            includeCardVersions: false,
            enrichments: new AnalysisPromptEnrichments(BracketFactsText: bracketFactsText, ScoreBlockText: scoreBlockText));
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void BracketFacts_Block_AppearsInAllThreeVariants(string platformName)
    {
        const string facts = "BRACKET_FACTS_SENTINEL";
        var result = Build(platformName, facts, "SCORE_SENTINEL");
        Assert.Equal(1, result.Split(facts, StringSplitOptions.None).Length - 1);
        var startMarker = platformName == "Claude" ? "<bracket_facts>" : "## Computed bracket facts";
        var start = result.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start > result.IndexOf("SCORE_SENTINEL", StringComparison.Ordinal));
        var endMarker = facts + Environment.NewLine + (platformName == "Claude" ? "</bracket_facts>" + Environment.NewLine : "");
        var end = result.IndexOf(endMarker, start, StringComparison.Ordinal) + endMarker.Length;
        var block = PacketByteIdentityFixtures.NormalizeForGoldenComparison(result[start..end]);
        Assert.Equal(AnalysisGoldens.BracketFactsBlock(platformName), block);

        Assert.Contains(platformName == "Claude" ? "<bracket_facts>" : "## Computed bracket facts", result, StringComparison.Ordinal);
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
        var startMarker = Environment.NewLine + (platformName == "Claude" ? "<bracket_facts>" : "## Computed bracket facts") + Environment.NewLine;
        var start = withFacts.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        var endMarker = sentinel + Environment.NewLine + (platformName == "Claude" ? "</bracket_facts>" + Environment.NewLine : "");
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
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void BracketFacts_FramedAsInputNotFloor_AllVariants(string platformName)
    {
        const string sentinel = "BRACKET_FACTS_SENTINEL";
        var result = Build(platformName, sentinel);
        var marker = platformName == "Claude" ? "<bracket_facts>" : "## Computed bracket facts";
        var start = result.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        var framing = result[start..result.IndexOf(sentinel, start, StringComparison.Ordinal)];
        Assert.Contains("input", framing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("your own bracket judgment", framing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a minimum", framing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("floor", framing, StringComparison.OrdinalIgnoreCase);
    }
}
