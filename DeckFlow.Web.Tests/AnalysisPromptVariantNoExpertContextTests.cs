using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Verifies deck-analysis prompt variants no longer emit the retired Expert Context block.
/// </summary>
public sealed class AnalysisPromptVariantNoExpertContextTests
{
    [Fact]
    public void ChatGpt_build_omits_expert_context_header()
    {
        var prompt = BuildPrompt(new ChatGptAnalysisPromptVariant());

        Assert.DoesNotContain("## Expert Context", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Claude_build_omits_expert_context_header()
    {
        var prompt = BuildPrompt(new ClaudeAnalysisPromptVariant());

        Assert.DoesNotContain("## Expert Context", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Gemini_build_omits_expert_context_header()
    {
        var prompt = BuildPrompt(new GeminiAnalysisPromptVariant());

        Assert.DoesNotContain("## Expert Context", prompt, StringComparison.Ordinal);
    }

    private static string BuildPrompt(IAnalysisPromptVariant variant)
    {
        return variant.Build(
            new DeckAnalysisRequest
            {
                Format = "Commander",
                TargetCommanderBracket = "cEDH"
            },
            "1 Sol Ring",
            "Reference text",
            "{}",
            "Atraxa",
            Array.Empty<string>(),
            Array.Empty<string>(),
            comboResult: null,
            includeCardVersions: false,
            enrichments: new AnalysisPromptEnrichments());
    }
}
