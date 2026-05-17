using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Phase 10 cross-AI contract: every prompt builder × every AI platform must
/// instruct the model to wrap its JSON response in `&lt;result&gt;...&lt;/result&gt;`
/// tags. The unified response shim in
/// <see cref="JsonTextFormatterService.ExtractJsonPayload"/> depends on
/// this contract for backwards-compatible parsing across all three AIs.
///
/// Tests exercise the dispatcher entrypoint (which routes per
/// <c>request.TargetAiPlatform</c>) so any future variant added to the switch
/// is automatically covered as long as it appears in this matrix.
/// </summary>
public sealed class ResultContractTests
{
    private static readonly string[] AiPlatforms = ["ChatGPT", "Claude", "Gemini"];

    // ---- BuildAnalysisPrompt ----

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void BuildAnalysisPrompt_emits_result_wrap_directive_for_every_ai(string platform)
    {
        var request = new DeckAnalysisRequest
        {
            TargetAiPlatform = platform,
            DeckName = "Test Deck",
            Format = "Commander",
            TargetCommanderBracket = "Cedh"
        };

        var prompt = DeckAnalysisPacketService.BuildAnalysisPrompt(
            request,
            decklistText: "1 Sol Ring\n1 Mana Crypt",
            referenceText: "Sol Ring: Add 2 mana.",
            deckProfileSchemaJson: "{\"type\":\"object\"}",
            commanderName: "Atraxa",
            selectedQuestionIds: Array.Empty<string>(),
            bannedCards: Array.Empty<string>());

        AssertContainsResultWrap(prompt, platform);
    }

    // ---- BuildSetUpgradePrompt ----

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void BuildSetUpgradePrompt_emits_result_wrap_directive_for_every_ai(string platform)
    {
        var request = new DeckAnalysisRequest
        {
            TargetAiPlatform = platform,
            DeckName = "Test Deck"
        };

        var prompt = DeckAnalysisPacketService.BuildSetUpgradePrompt(
            request,
            decklistText: "1 Sol Ring",
            deckProfileJson: "{}",
            commanderName: "Atraxa",
            generatedSetPacket: "Sample set packet text",
            bannedCards: Array.Empty<string>());

        AssertContainsResultWrap(prompt, platform);
    }

    // ---- BuildComparisonPrompt ----

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void BuildComparisonPrompt_emits_result_wrap_directive_for_every_ai(string platform)
    {
        var deckA = BuildSampleDeckSummary("Deck A", "Atraxa");
        var deckB = BuildSampleDeckSummary("Deck B", "Kraum");

        var prompt = DeckComparisonService.BuildComparisonPrompt(
            deckA,
            deckB,
            deckAListText: "1 Sol Ring (Atraxa list)",
            deckBListText: "1 Mana Crypt (Kraum list)",
            deckAComboText: string.Empty,
            deckBComboText: string.Empty,
            comparisonContextText: "context",
            comparisonSchemaJson: "{}",
            targetAiPlatform: platform);

        AssertContainsResultWrap(prompt, platform);
    }

    // ---- BuildFollowUpPrompt ----

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void BuildFollowUpPrompt_emits_result_wrap_directive_for_every_ai(string platform)
    {
        var prompt = DeckComparisonService.BuildFollowUpPrompt(
            comparisonSchemaJson: "{\"type\":\"object\"}",
            targetAiPlatform: platform);

        AssertContainsResultWrap(prompt, platform);
    }

    // ---- BuildPrompt (CedhMetaGap) ----

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void CedhMetaGap_BuildPrompt_emits_result_wrap_directive_for_every_ai(string platform)
    {
        var prompt = MetaGapService.BuildPrompt(
            commanderName: "Atraxa",
            myDeckEntries: Array.Empty<DeckFlow.Core.Models.DeckEntry>(),
            myDeckCombos: null,
            selectedEntries: Array.Empty<EdhTop16Entry>(),
            referenceDeckCombos: Array.Empty<DeckFlow.Web.Services.CommanderSpellbookResult?>(),
            oracleNameMap: new Dictionary<string, string>(),
            schemaJson: "{}",
            targetAiPlatform: platform);

        AssertContainsResultWrap(prompt, platform);
    }

    // ---- Cross-AI matrix sanity ----

    [Fact]
    public void Every_dispatcher_returns_distinct_content_per_ai_platform()
    {
        // Sanity: the dispatcher is actually routing per platform — outputs are not byte-identical.
        var request = new DeckAnalysisRequest { TargetAiPlatform = "ChatGPT", Format = "Commander" };
        var chatgpt = DeckAnalysisPacketService.BuildAnalysisPrompt(
            request, "1 Sol Ring", "Sol Ring text", "{}", "Atraxa",
            Array.Empty<string>(), Array.Empty<string>());

        request.TargetAiPlatform = "Claude";
        var claude = DeckAnalysisPacketService.BuildAnalysisPrompt(
            request, "1 Sol Ring", "Sol Ring text", "{}", "Atraxa",
            Array.Empty<string>(), Array.Empty<string>());

        request.TargetAiPlatform = "Gemini";
        var gemini = DeckAnalysisPacketService.BuildAnalysisPrompt(
            request, "1 Sol Ring", "Sol Ring text", "{}", "Atraxa",
            Array.Empty<string>(), Array.Empty<string>());

        Assert.NotEqual(chatgpt, claude);
        Assert.NotEqual(chatgpt, gemini);
        Assert.NotEqual(claude, gemini);
    }

    [Fact]
    public void Claude_variant_uses_xml_skeleton_with_no_api_role_blocks()
    {
        var request = new DeckAnalysisRequest { TargetAiPlatform = "Claude", Format = "Commander" };
        var claude = DeckAnalysisPacketService.BuildAnalysisPrompt(
            request, "1 Sol Ring", "Sol Ring text", "{}", "Atraxa",
            Array.Empty<string>(), Array.Empty<string>());

        // D-04 invariant: Claude prompts must NOT contain Anthropic-API role blocks.
        Assert.DoesNotContain("<system>", claude);
        Assert.DoesNotContain("<human>", claude);
        Assert.DoesNotContain("<assistant>", claude);

        // D-02 invariant: data-tag taxonomy present.
        Assert.Contains("<role>", claude);
        Assert.Contains("<deck>", claude);
        Assert.Contains("<output_schema>", claude);
    }

    // ---- helpers ----

    private static void AssertContainsResultWrap(string prompt, string platform)
    {
        Assert.False(string.IsNullOrWhiteSpace(prompt), $"{platform} variant returned empty prompt");
        // Every variant references <result>...</result> tags somewhere — either
        // via the shared ResultWrapInstruction const (which contains
        // the literal `<result>...</result>` substring) or via the Claude
        // variant's explicit "Wrap your final structured output in
        // <result>...</result> tags" directive.
        Assert.Contains("<result>", prompt);
        Assert.Contains("</result>", prompt);
    }

    [Fact]
    public void BuildAnalysisPrompt_for_Gemini_ends_with_mandate_block()
    {
        var request = new DeckAnalysisRequest
        {
            TargetAiPlatform = "Gemini",
            DeckName = "Test Deck",
            Format = "Commander",
            TargetCommanderBracket = "Cedh"
        };
        var prompt = DeckAnalysisPacketService.BuildAnalysisPrompt(
            request,
            decklistText: "1 Sol Ring\n1 Mana Crypt",
            referenceText: "Sol Ring: Add 2 mana.",
            deckProfileSchemaJson: "{\"type\":\"object\"}",
            commanderName: "Atraxa",
            selectedQuestionIds: System.Array.Empty<string>(),
            bannedCards: System.Array.Empty<string>());

        Assert.Contains("MANDATORY", prompt);
        Assert.EndsWith("Nothing else after </result>.", prompt);
    }

    private static DeckComparisonService.DeckComparisonDeckSummary BuildSampleDeckSummary(string name, string commander)
    {
        var bracket = CommanderBracketCatalog.Options[0];
        return new DeckComparisonService.DeckComparisonDeckSummary(
            Name: name,
            CommanderName: commander,
            Bracket: bracket,
            MainboardCount: 99,
            Lands: 36,
            Creatures: 30,
            AverageManaValue: 2.5m,
            ManaCurve: new Dictionary<string, int>(),
            ColorIdentity: ["W", "U", "B", "G"],
            CategorySummaries: [],
            Ramp: 12,
            Draw: 10,
            Interaction: 8,
            Wipes: 2,
            Recursion: 3,
            ClosingPower: 5,
            SharedThemes: [],
            ComboSummaries: [],
            AlmostComboSummaries: [],
            IncludedComboCount: 0,
            AlmostIncludedComboCount: 0);
    }
}
