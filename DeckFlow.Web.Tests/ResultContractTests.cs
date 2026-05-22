using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.Comparison;
using DeckFlow.Web.Services.PromptBuilders.FollowUp;
using DeckFlow.Web.Services.PromptBuilders.MetaGap;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;
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

    // Phase 15-02: static dispatch replaced by instance dispatch through registries.
    // Minimal registries are constructed inline — no live HTTP/DI required.
    private static AnalysisPromptVariantRegistry BuildAnalysisRegistry() =>
        new AnalysisPromptVariantRegistry(new IAnalysisPromptVariant[]
        {
            new ChatGptAnalysisPromptVariant(),
            new ClaudeAnalysisPromptVariant(),
            new GeminiAnalysisPromptVariant(),
        });

    private static SetUpgradePromptVariantRegistry BuildSetUpgradeRegistry() =>
        new SetUpgradePromptVariantRegistry(new ISetUpgradePromptVariant[]
        {
            new ChatGptSetUpgradePromptVariant(),
            new ClaudeSetUpgradePromptVariant(),
            new GeminiSetUpgradePromptVariant(),
        });

    private static ComparisonPromptVariantRegistry BuildComparisonRegistry() =>
        new ComparisonPromptVariantRegistry(new IComparisonPromptVariant[]
        {
            new ChatGptComparisonPromptVariant(),
            new ClaudeComparisonPromptVariant(),
            new GeminiComparisonPromptVariant(),
        });

    private static FollowUpPromptVariantRegistry BuildFollowUpRegistry() =>
        new FollowUpPromptVariantRegistry(new IFollowUpPromptVariant[]
        {
            new ChatGptFollowUpPromptVariant(),
            new ClaudeFollowUpPromptVariant(),
            new GeminiFollowUpPromptVariant(),
        });

    private static MetaGapPromptVariantRegistry BuildMetaGapRegistry() =>
        new MetaGapPromptVariantRegistry(new IMetaGapPromptVariant[]
        {
            new ChatGptMetaGapPromptVariant(),
            new ClaudeMetaGapPromptVariant(),
            new GeminiMetaGapPromptVariant(),
        });

    // ---- BuildAnalysisPrompt ----

    [Theory]
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

        var registry = BuildAnalysisRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize(request.TargetAiPlatform),
            request,
            decklistText: "1 Sol Ring\n1 Mana Crypt",
            referenceText: "Sol Ring: Add 2 mana.",
            deckProfileSchemaJson: "{\"type\":\"object\"}",
            commanderName: "Atraxa",
            selectedQuestionIds: Array.Empty<string>(),
            bannedCards: Array.Empty<string>());

        AssertContainsResultWrap(prompt, platform);
    }

    // Phase 999.2: Claude variants emit a fenced ```json directive instead of the
    // <result>...</result> wrapper. Parser (JsonTextFormatterService.ExtractJsonPayload)
    // already falls through <result>-regex to brace-scan so fenced-only output parses.
    [Fact]
    public void BuildAnalysisPrompt_for_Claude_emits_fenced_json_directive_not_result_wrapper()
    {
        var request = new DeckAnalysisRequest
        {
            TargetAiPlatform = "Claude",
            DeckName = "Test Deck",
            Format = "Commander",
            TargetCommanderBracket = "Cedh"
        };

        var registry = BuildAnalysisRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize("Claude"),
            request,
            decklistText: "1 Sol Ring\n1 Mana Crypt",
            referenceText: "Sol Ring: Add 2 mana.",
            deckProfileSchemaJson: "{\"type\":\"object\"}",
            commanderName: "Atraxa",
            selectedQuestionIds: Array.Empty<string>(),
            bannedCards: Array.Empty<string>());

        Assert.Contains("fenced ```json code block", prompt);
        Assert.DoesNotContain("<result>", prompt);
        Assert.DoesNotContain("</result>", prompt);
    }

    [Fact]
    public void BuildAnalysisPrompt_for_ChatGpt_emits_fenced_json_directive_not_result_wrapper()
    {
        var request = new DeckAnalysisRequest
        {
            TargetAiPlatform = "ChatGPT",
            DeckName = "Test Deck",
            Format = "Commander",
            TargetCommanderBracket = "Cedh"
        };

        var registry = BuildAnalysisRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize("ChatGPT"),
            request,
            decklistText: "1 Sol Ring\n1 Mana Crypt",
            referenceText: "Sol Ring: Add 2 mana.",
            deckProfileSchemaJson: "{\"type\":\"object\"}",
            commanderName: "Atraxa",
            selectedQuestionIds: Array.Empty<string>(),
            bannedCards: Array.Empty<string>());

        Assert.Contains("fenced ```json code block", prompt);
        Assert.DoesNotContain("<result>", prompt);
        Assert.DoesNotContain("</result>", prompt);
    }

    // ---- BuildSetUpgradePrompt ----

    [Theory]
    [InlineData("Gemini")]
    public void BuildSetUpgradePrompt_emits_result_wrap_directive_for_every_ai(string platform)
    {
        var request = new DeckAnalysisRequest
        {
            TargetAiPlatform = platform,
            DeckName = "Test Deck"
        };

        var registry = BuildSetUpgradeRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize(request.TargetAiPlatform),
            request,
            decklistText: "1 Sol Ring",
            deckProfileJson: "{}",
            commanderName: "Atraxa",
            generatedSetPacket: "Sample set packet text",
            bannedCards: Array.Empty<string>());

        AssertContainsResultWrap(prompt, platform);
    }

    [Fact]
    public void BuildSetUpgradePrompt_for_Claude_emits_fenced_json_directive_not_result_wrapper()
    {
        var request = new DeckAnalysisRequest
        {
            TargetAiPlatform = "Claude",
            DeckName = "Test Deck"
        };

        var registry = BuildSetUpgradeRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize("Claude"),
            request,
            decklistText: "1 Sol Ring",
            deckProfileJson: "{}",
            commanderName: "Atraxa",
            generatedSetPacket: "Sample set packet text",
            bannedCards: Array.Empty<string>());

        Assert.Contains("fenced ```json code block", prompt);
        Assert.Contains("set_upgrade_report", prompt);
        Assert.DoesNotContain("<result>", prompt);
        Assert.DoesNotContain("</result>", prompt);
    }

    [Fact]
    public void BuildSetUpgradePrompt_for_ChatGpt_emits_fenced_json_directive_not_result_wrapper()
    {
        var request = new DeckAnalysisRequest
        {
            TargetAiPlatform = "ChatGPT",
            DeckName = "Test Deck"
        };

        var registry = BuildSetUpgradeRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize("ChatGPT"),
            request,
            decklistText: "1 Sol Ring",
            deckProfileJson: "{}",
            commanderName: "Atraxa",
            generatedSetPacket: "Sample set packet text",
            bannedCards: Array.Empty<string>());

        Assert.Contains("fenced ```json code block", prompt);
        Assert.Contains("set_upgrade_report", prompt);
        Assert.DoesNotContain("<result>", prompt);
        Assert.DoesNotContain("</result>", prompt);
    }

    // ---- BuildComparisonPrompt ----

    [Theory]
    [InlineData("Gemini")]
    public void BuildComparisonPrompt_emits_result_wrap_directive_for_every_ai(string platform)
    {
        var deckA = BuildSampleDeckSummary("Deck A", "Atraxa");
        var deckB = BuildSampleDeckSummary("Deck B", "Kraum");

        var registry = BuildComparisonRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize(platform),
            deckA,
            deckB,
            deckAListText: "1 Sol Ring (Atraxa list)",
            deckBListText: "1 Mana Crypt (Kraum list)",
            deckAComboText: string.Empty,
            deckBComboText: string.Empty,
            comparisonContextText: "context",
            comparisonSchemaJson: "{}");

        AssertContainsResultWrap(prompt, platform);
    }

    [Fact]
    public void BuildComparisonPrompt_for_Claude_emits_fenced_json_directive_not_result_wrapper()
    {
        var deckA = BuildSampleDeckSummary("Deck A", "Atraxa");
        var deckB = BuildSampleDeckSummary("Deck B", "Kraum");

        var registry = BuildComparisonRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize("Claude"),
            deckA,
            deckB,
            deckAListText: "1 Sol Ring (Atraxa list)",
            deckBListText: "1 Mana Crypt (Kraum list)",
            deckAComboText: string.Empty,
            deckBComboText: string.Empty,
            comparisonContextText: "context",
            comparisonSchemaJson: "{}");

        Assert.Contains("fenced ```json code block", prompt);
        Assert.Contains("deck_comparison", prompt);
        Assert.DoesNotContain("<result>", prompt);
        Assert.DoesNotContain("</result>", prompt);
    }

    [Fact]
    public void BuildComparisonPrompt_for_ChatGpt_emits_fenced_json_directive_not_result_wrapper()
    {
        var deckA = BuildSampleDeckSummary("Deck A", "Atraxa");
        var deckB = BuildSampleDeckSummary("Deck B", "Kraum");

        var registry = BuildComparisonRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize("ChatGPT"),
            deckA,
            deckB,
            deckAListText: "1 Sol Ring (Atraxa list)",
            deckBListText: "1 Mana Crypt (Kraum list)",
            deckAComboText: string.Empty,
            deckBComboText: string.Empty,
            comparisonContextText: "context",
            comparisonSchemaJson: "{}");

        Assert.Contains("fenced ```json code block", prompt);
        Assert.Contains("deck_comparison", prompt);
        Assert.DoesNotContain("<result>", prompt);
        Assert.DoesNotContain("</result>", prompt);
    }

    // ---- BuildFollowUpPrompt ----

    [Theory]
    [InlineData("Gemini")]
    public void BuildFollowUpPrompt_emits_result_wrap_directive_for_every_ai(string platform)
    {
        var registry = BuildFollowUpRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize(platform),
            comparisonSchemaJson: "{\"type\":\"object\"}");

        AssertContainsResultWrap(prompt, platform);
    }

    [Fact]
    public void BuildFollowUpPrompt_for_Claude_emits_fenced_json_directive_not_result_wrapper()
    {
        var registry = BuildFollowUpRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize("Claude"),
            comparisonSchemaJson: "{\"type\":\"object\"}");

        Assert.Contains("fenced ```json code block", prompt);
        Assert.Contains("deck_comparison", prompt);
        Assert.DoesNotContain("<result>", prompt);
        Assert.DoesNotContain("</result>", prompt);
    }

    [Fact]
    public void BuildFollowUpPrompt_for_ChatGpt_emits_fenced_json_directive_not_result_wrapper()
    {
        var registry = BuildFollowUpRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize("ChatGPT"),
            comparisonSchemaJson: "{\"type\":\"object\"}");

        Assert.Contains("fenced ```json code block", prompt);
        Assert.Contains("deck_comparison", prompt);
        Assert.DoesNotContain("<result>", prompt);
        Assert.DoesNotContain("</result>", prompt);
    }

    // ---- BuildPrompt (CedhMetaGap) ----

    [Theory]
    [InlineData("Gemini")]
    public void CedhMetaGap_BuildPrompt_emits_result_wrap_directive_for_every_ai(string platform)
    {
        var registry = BuildMetaGapRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize(platform),
            commanderName: "Atraxa",
            myDeckEntries: Array.Empty<DeckFlow.Core.Models.DeckEntry>(),
            myDeckCombos: null,
            selectedEntries: Array.Empty<EdhTop16Entry>(),
            referenceDeckCombos: Array.Empty<DeckFlow.Web.Services.CommanderSpellbookResult?>(),
            oracleNameMap: new Dictionary<string, string>(),
            schemaJson: "{}");

        AssertContainsResultWrap(prompt, platform);
    }

    [Fact]
    public void CedhMetaGap_BuildPrompt_for_Claude_emits_fenced_json_directive_not_result_wrapper()
    {
        var registry = BuildMetaGapRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize("Claude"),
            commanderName: "Atraxa",
            myDeckEntries: Array.Empty<DeckFlow.Core.Models.DeckEntry>(),
            myDeckCombos: null,
            selectedEntries: Array.Empty<EdhTop16Entry>(),
            referenceDeckCombos: Array.Empty<DeckFlow.Web.Services.CommanderSpellbookResult?>(),
            oracleNameMap: new Dictionary<string, string>(),
            schemaJson: "{}");

        Assert.Contains("fenced ```json code block", prompt);
        Assert.Contains("meta_gap", prompt);
        Assert.DoesNotContain("<result>", prompt);
        Assert.DoesNotContain("</result>", prompt);
    }

    [Fact]
    public void CedhMetaGap_BuildPrompt_for_ChatGpt_emits_fenced_json_directive_not_result_wrapper()
    {
        var registry = BuildMetaGapRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize("ChatGPT"),
            commanderName: "Atraxa",
            myDeckEntries: Array.Empty<DeckFlow.Core.Models.DeckEntry>(),
            myDeckCombos: null,
            selectedEntries: Array.Empty<EdhTop16Entry>(),
            referenceDeckCombos: Array.Empty<DeckFlow.Web.Services.CommanderSpellbookResult?>(),
            oracleNameMap: new Dictionary<string, string>(),
            schemaJson: "{}");

        Assert.Contains("fenced ```json code block", prompt);
        Assert.Contains("meta_gap", prompt);
        Assert.DoesNotContain("<result>", prompt);
        Assert.DoesNotContain("</result>", prompt);
    }

    // ---- Cross-AI matrix sanity ----

    [Fact]
    public void Every_dispatcher_returns_distinct_content_per_ai_platform()
    {
        // Sanity: the dispatcher is actually routing per platform — outputs are not byte-identical.
        var registry = BuildAnalysisRegistry();
        var request = new DeckAnalysisRequest { TargetAiPlatform = "ChatGPT", Format = "Commander" };
        var chatgpt = registry.Build(
            AiPlatform.Normalize("ChatGPT"),
            request, "1 Sol Ring", "Sol Ring text", "{}", "Atraxa",
            Array.Empty<string>(), Array.Empty<string>());

        request.TargetAiPlatform = "Claude";
        var claude = registry.Build(
            AiPlatform.Normalize("Claude"),
            request, "1 Sol Ring", "Sol Ring text", "{}", "Atraxa",
            Array.Empty<string>(), Array.Empty<string>());

        request.TargetAiPlatform = "Gemini";
        var gemini = registry.Build(
            AiPlatform.Normalize("Gemini"),
            request, "1 Sol Ring", "Sol Ring text", "{}", "Atraxa",
            Array.Empty<string>(), Array.Empty<string>());

        Assert.NotEqual(chatgpt, claude);
        Assert.NotEqual(chatgpt, gemini);
        Assert.NotEqual(claude, gemini);
    }

    [Fact]
    public void Claude_variant_uses_xml_skeleton_with_no_api_role_blocks()
    {
        var registry = BuildAnalysisRegistry();
        var request = new DeckAnalysisRequest { TargetAiPlatform = "Claude", Format = "Commander" };
        var claude = registry.Build(
            AiPlatform.Normalize("Claude"),
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
        var registry = BuildAnalysisRegistry();
        var prompt = registry.Build(
            AiPlatform.Normalize("Gemini"),
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
