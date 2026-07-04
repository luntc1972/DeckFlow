// Why: ADR-0001 - analysis prompt variants are intentionally decoupled; this test instantiates
// each concrete variant directly without a shared helper and proves Gemini is covered too.
// WINCON-04: the win-condition/combo map block must appear in all three paste artifacts, and the
// flag-OFF (null winConMapText) path must stay byte-identical.
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// 3-platform parity tests (WINCON-04): asserts the win-condition/combo map block appears in all
/// three prompt variants when supplied and that the null-path output is byte-identical to the
/// with-block output minus the contiguous win-con block. No shared variant helper.
/// </summary>
public sealed class WinConMapPromptParityTests
{
    // Why: ADR-0001 - no shared helper; concrete variants instantiated inline.
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

    private static string Build(string platformName, string? winConMapText)
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
            enrichments: new AnalysisPromptEnrichments(WinConMapText: winConMapText));
    }

    // -- Win-con block present in all three variants when supplied --

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void WinConMap_Block_AppearsInAllThreeVariants(string platformName)
    {
        var winConMapText =
            "WIN CONDITION & COMBO MAP (DeckFlow heuristic first pass - the AI must confirm castability, board state, and color access before treating any line below as a live win condition)\n"
            + "  Kiki-Jiki, Mirror Breaker, Restoration Angel -> Infinite combat steps\n"
            + "Near-combos, one card away (not currently a win line): missing Splinter Twin (have: Deceiver Exarch)";

        var result = Build(platformName, winConMapText);

        Assert.Contains("WIN CONDITION", result, StringComparison.Ordinal);
        Assert.Contains("one card away (not currently a win line)", result, StringComparison.Ordinal);
    }

    // -- OFF-path byte identity: null output == with-block output minus the block --

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void WinConMap_NullPath_ByteIdenticalToExcisedBlockPath(string platformName)
    {
        const string sentinel = "WIN_CON_MAP_PARITY_SENTINEL";

        var withBlock = Build(platformName, sentinel);
        var nullPath = Build(platformName, winConMapText: null);
        var insertedBlock = Environment.NewLine + sentinel + Environment.NewLine;

        var firstIndex = withBlock.IndexOf(insertedBlock, StringComparison.Ordinal);
        Assert.True(firstIndex >= 0, $"Expected the inserted win-con map block in the {platformName} output.");
        var lastIndex = withBlock.LastIndexOf(insertedBlock, StringComparison.Ordinal);
        Assert.Equal(firstIndex, lastIndex);

        var excised = withBlock.Remove(firstIndex, insertedBlock.Length);
        Assert.Equal(nullPath, excised);
    }

    // -- Absence assertion: without the block, the sentinel label does not leak into the output --

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void WinConMap_NullPath_DoesNotContainBlockLabel(string platformName)
    {
        var result = Build(platformName, winConMapText: null);

        Assert.DoesNotContain("WIN CONDITION", result, StringComparison.Ordinal);
        Assert.DoesNotContain("one card away (not currently a win line)", result, StringComparison.Ordinal);
    }
}
