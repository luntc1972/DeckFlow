// Why: ADR-0001 - analysis prompt variants are intentionally decoupled; this test instantiates
// each concrete variant directly without a shared helper and proves Gemini is covered too.
// INTERACT-03: the interaction audit block must appear in all three paste artifacts, and the
// flag-OFF (null interactionAuditText) path must stay byte-identical.
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// 3-platform parity tests (INTERACT-03): asserts the interaction-audit block appears in all three
/// prompt variants when supplied and that the null-path output is byte-identical to the with-audit
/// output minus the contiguous audit block. No shared variant helper.
/// </summary>
public sealed class InteractionAuditPromptParityTests
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

    private static string Build(string platformName, string? interactionAuditText)
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
            enrichments: new AnalysisPromptEnrichments(InteractionAuditText: interactionAuditText));
    }

    // -- Interaction audit block present in all three variants when supplied --

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void InteractionAudit_Block_AppearsInAllThreeVariants(string platformName)
    {
        var interactionAuditText =
            "INTERACTION AUDIT (DeckFlow heuristic first pass - verify against the cards)\n"
            + "  Counterspells: approximately 2 - Counterspell, Mana Drain";

        var result = Build(platformName, interactionAuditText);

        Assert.Contains("INTERACTION AUDIT", result, StringComparison.Ordinal);
        Assert.Contains("Counterspells", result, StringComparison.Ordinal);
    }

    // -- OFF-path byte identity: null output == with-audit output minus the block --

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void InteractionAudit_NullPath_ByteIdenticalToExcisedAuditPath(string platformName)
    {
        const string sentinel = "INTERACTION_AUDIT_PARITY_SENTINEL";

        var withAudit = Build(platformName, sentinel);
        var nullPath = Build(platformName, interactionAuditText: null);
        var insertedBlock = Environment.NewLine + sentinel + Environment.NewLine;

        var firstIndex = withAudit.IndexOf(insertedBlock, StringComparison.Ordinal);
        Assert.True(firstIndex >= 0, $"Expected the inserted interaction audit block in the {platformName} output.");
        var lastIndex = withAudit.LastIndexOf(insertedBlock, StringComparison.Ordinal);
        Assert.Equal(firstIndex, lastIndex);

        var excised = withAudit.Remove(firstIndex, insertedBlock.Length);
        Assert.Equal(nullPath, excised);
    }
}
