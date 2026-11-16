using DeckFlow.Core.Models;
using DeckFlow.Web.Services.PromptBuilders.MetaGap;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Keeps readiness evidence anchors aligned across the independently maintained variants.
/// </summary>
public sealed class MetaGapReadinessAnchorParityTests
{
    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Readiness_AnchorsPresent_AllVariants(string platform)
    {
        var prompt = Build(platform);
        foreach (var anchor in new[]
        {
            "9-10: nearly the entire core",
            "7-8: most core cards",
            "5-6: roughly half the core",
            "3-4: few core cards",
            "1-2: no shared core",
            "no high-priority staples missing",
            "speed and interaction meet or exceed the REF averages",
            "a few staples missing",
            "within about one turn of REF speed",
            "interaction close to the REF average",
            "several high-priority staples missing",
            "a full turn slower",
            "clearly fewer interaction cards",
            "both slower and less interactive than REF",
            "no win line comparable to REF consensus",
            "core_convergence",
            "missing_staples",
            "speed",
            "interaction",
            "win_lines",
            "readiness_justification must name the values",
            "decimals are allowed",
        })
        {
            Assert.Contains(anchor, prompt, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Readiness_ThinEvidenceTakesLowerBand_AllVariants(string platform)
    {
        var prompt = Build(platform);
        Assert.Contains(
            "If there is only one REF deck or the REF decks have little card overlap, choose the lower band and state that evidence limitation in readiness_justification.",
            prompt, StringComparison.OrdinalIgnoreCase);
    }

    private static string Build(string platform)
    {
        IMetaGapPromptVariant variant = platform switch
        {
            "ChatGPT" => new ChatGptMetaGapPromptVariant(),
            "Claude" => new ClaudeMetaGapPromptVariant(),
            "Gemini" => new GeminiMetaGapPromptVariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };
        return variant.Build(
            "Kraum, Ludevic's Opus",
            [new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "main" }],
            null, [], [], new Dictionary<string, string>(), "{}");
    }
}
