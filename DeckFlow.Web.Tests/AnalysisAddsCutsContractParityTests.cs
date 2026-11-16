using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Verifies the adds and cuts contract in each independently maintained Analysis variant.
/// </summary>
public sealed class AnalysisAddsCutsContractParityTests
{
    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Adds_RequireTwoSynergyLinks_AllVariants(string platformName)
    {
        var sentences = Sentences(Build(platformName));
        var links = Assert.Single(sentences, sentence =>
            sentence.Contains("synergy link", StringComparison.OrdinalIgnoreCase)
            && sentence.Contains("each add", StringComparison.OrdinalIgnoreCase));
        Assert.Matches(@"(?i)at least (two|2)\b", links);
        Assert.Contains("specific interaction", links, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("commander", links, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("named card already in the deck", links, StringComparison.OrdinalIgnoreCase);
        var exemption = Assert.Single(sentences, sentence =>
            sentence.Contains("structural adds", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("exempt", exemption, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("two-link rule", exemption, StringComparison.OrdinalIgnoreCase);
        AssertStructuralClasses(exemption);
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Adds_NameReplacementAndSharedRole_AllVariants(string platformName)
    {
        var replacement = Assert.Single(Sentences(Build(platformName)), sentence =>
            sentence.Contains("replaces", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("each add", replacement, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("card", replacement, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shared role", replacement, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Adds_CarrySourceTag_MemoryPicksMarkedVerify_AllVariants(string platformName)
    {
        var source = Assert.Single(Sentences(Build(platformName)), sentence =>
            sentence.Contains("[supplied]", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("each add", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data supplied in the prompt", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[memory]", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("your own knowledge", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("every [memory] add", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verify on Scryfall", source, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Cuts_ListUnlinkedFirst_StructuralExempt_AllVariants(string platformName)
    {
        var cuts = Assert.Single(Sentences(Build(platformName)), sentence =>
            sentence.Contains("no synergy link", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("cuts", cuts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("first", cuts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("structural", cuts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must not be cut for lacking links", cuts, StringComparison.OrdinalIgnoreCase);
        AssertStructuralClasses(cuts);
    }

    private static void AssertStructuralClasses(string sentence)
    {
        foreach (var cardClass in new[] { "lands", "generic ramp", "removal", "board wipes" })
        {
            Assert.Contains(cardClass, sentence, StringComparison.OrdinalIgnoreCase);
        }
    }

    // A sentence ends at a line break or a period followed by a space.
    private static string[] Sentences(string prompt) =>
        prompt.Split(["\r\n", "\n", ". "], StringSplitOptions.RemoveEmptyEntries);

    private static string Build(string platformName)
    {
        var registry = new AnalysisPromptVariantRegistry(new IAnalysisPromptVariant[]
        {
            new ChatGptAnalysisPromptVariant(),
            new ClaudeAnalysisPromptVariant(),
            new GeminiAnalysisPromptVariant(),
        });
        return registry.Build(
            AiPlatform.Normalize(platformName),
            new DeckAnalysisRequest { Format = "Commander", TargetCommanderBracket = "cEDH" },
            "1 Sol Ring", "Reference text", "{}", commanderName: null,
            selectedQuestionIds: [], bannedCards: [], comboResult: null,
            includeCardVersions: false, enrichments: new AnalysisPromptEnrichments());
    }
}
