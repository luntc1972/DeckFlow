using DeckFlow.Core.Models;
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
/// Phase 15 SC5 proof: adding a 4th AI platform via AiPlatform.AllForTesting
/// plus stub variants — one per builder family — produces working dispatch
/// without editing any production switch expression, request-model setter,
/// Razor partial, or RequestContextParser. The lack of edits below the
/// internal seam in AiPlatform.cs IS the test — this file demonstrates
/// that the registry pattern carries the OCP claim.
/// </summary>
public sealed class AiPlatformExtensionTests
{
    private static readonly AiPlatform TestPlatform =
        new("Test", "Test Platform", "Stub for SC5 4th-platform extension proof.");

    // ---- Stub variant nested classes (one per family) ----

    private sealed class StubTestAnalysisVariant : IAnalysisPromptVariant
    {
        public AiPlatform Platform => TestPlatform;

        public string Build(
            DeckAnalysisRequest request,
            string decklistText,
            string referenceText,
            string deckProfileSchemaJson,
            string? commanderName,
            IReadOnlyList<string> selectedQuestionIds,
            IReadOnlyList<string> bannedCards,
            CommanderSpellbookResult? comboResult,
            bool includeCardVersions) =>
            "<test-analysis-stub/>";
    }

    private sealed class StubTestSetUpgradeVariant : ISetUpgradePromptVariant
    {
        public AiPlatform Platform => TestPlatform;

        public string Build(
            DeckAnalysisRequest request,
            string decklistText,
            string deckProfileJson,
            string? commanderName,
            string? generatedSetPacket,
            IReadOnlyList<string> bannedCards) =>
            "<test-setupgrade-stub/>";
    }

    private sealed class StubTestComparisonVariant : IComparisonPromptVariant
    {
        public AiPlatform Platform => TestPlatform;

        public string Build(
            DeckComparisonService.DeckComparisonDeckSummary deckA,
            DeckComparisonService.DeckComparisonDeckSummary deckB,
            string deckAListText,
            string deckBListText,
            string deckAComboText,
            string deckBComboText,
            string comparisonContextText,
            string comparisonSchemaJson) =>
            "<test-comparison-stub/>";
    }

    private sealed class StubTestFollowUpVariant : IFollowUpPromptVariant
    {
        public AiPlatform Platform => TestPlatform;

        public string Build(string comparisonSchemaJson) =>
            "<test-followup-stub/>";
    }

    private sealed class StubTestMetaGapVariant : IMetaGapPromptVariant
    {
        public AiPlatform Platform => TestPlatform;

        public string Build(
            string commanderName,
            IReadOnlyList<DeckEntry> myDeckEntries,
            CommanderSpellbookResult? myDeckCombos,
            IReadOnlyList<EdhTop16Entry> selectedEntries,
            IReadOnlyList<CommanderSpellbookResult?> referenceDeckCombos,
            IReadOnlyDictionary<string, string> oracleNameMap,
            string schemaJson) =>
            "<test-metagap-stub/>";
    }

    // ---- AllForTesting seam test ----

    [Fact]
    public void AllForTesting_extends_All_with_test_platform()
    {
        var extended = AiPlatform.AllForTesting(TestPlatform);

        Assert.Equal(AiPlatform.All.Count + 1, extended.Count);
        Assert.Contains(TestPlatform, extended);
        // Production All is unchanged — seam does not mutate static state.
        Assert.Equal(3, AiPlatform.All.Count);
    }

    // ---- Registry dispatch facts (one per family) ----

    [Fact]
    public void AnalysisRegistry_dispatches_to_test_variant_when_test_platform_supplied()
    {
        var registry = new AnalysisPromptVariantRegistry(new IAnalysisPromptVariant[]
        {
            new ChatGptAnalysisPromptVariant(),
            new ClaudeAnalysisPromptVariant(),
            new GeminiAnalysisPromptVariant(),
            new StubTestAnalysisVariant(),
        });

        var result = registry.Build(
            TestPlatform,
            new DeckAnalysisRequest(),
            decklistText: "1 Sol Ring",
            referenceText: "Sol Ring: Add 2 mana.",
            deckProfileSchemaJson: "{}",
            commanderName: null,
            selectedQuestionIds: Array.Empty<string>(),
            bannedCards: Array.Empty<string>());

        Assert.Equal("<test-analysis-stub/>", result);
    }

    [Fact]
    public void SetUpgradeRegistry_dispatches_to_test_variant_when_test_platform_supplied()
    {
        var registry = new SetUpgradePromptVariantRegistry(new ISetUpgradePromptVariant[]
        {
            new ChatGptSetUpgradePromptVariant(),
            new ClaudeSetUpgradePromptVariant(),
            new GeminiSetUpgradePromptVariant(),
            new StubTestSetUpgradeVariant(),
        });

        var result = registry.Build(
            TestPlatform,
            new DeckAnalysisRequest(),
            decklistText: "1 Sol Ring",
            deckProfileJson: "{}",
            commanderName: null,
            generatedSetPacket: null,
            bannedCards: Array.Empty<string>());

        Assert.Equal("<test-setupgrade-stub/>", result);
    }

    [Fact]
    public void ComparisonRegistry_dispatches_to_test_variant_when_test_platform_supplied()
    {
        var bracket = CommanderBracketCatalog.Options[0];
        var deckA = new DeckComparisonService.DeckComparisonDeckSummary(
            Name: "Deck A",
            CommanderName: "Atraxa",
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
        var deckB = new DeckComparisonService.DeckComparisonDeckSummary(
            Name: "Deck B",
            CommanderName: "Kraum",
            Bracket: bracket,
            MainboardCount: 99,
            Lands: 36,
            Creatures: 30,
            AverageManaValue: 2.5m,
            ManaCurve: new Dictionary<string, int>(),
            ColorIdentity: ["U", "R"],
            CategorySummaries: [],
            Ramp: 10,
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

        var registry = new ComparisonPromptVariantRegistry(new IComparisonPromptVariant[]
        {
            new ChatGptComparisonPromptVariant(),
            new ClaudeComparisonPromptVariant(),
            new GeminiComparisonPromptVariant(),
            new StubTestComparisonVariant(),
        });

        var result = registry.Build(
            TestPlatform,
            deckA,
            deckB,
            deckAListText: "1 Sol Ring",
            deckBListText: "1 Mana Crypt",
            deckAComboText: string.Empty,
            deckBComboText: string.Empty,
            comparisonContextText: "context",
            comparisonSchemaJson: "{}");

        Assert.Equal("<test-comparison-stub/>", result);
    }

    [Fact]
    public void FollowUpRegistry_dispatches_to_test_variant_when_test_platform_supplied()
    {
        var registry = new FollowUpPromptVariantRegistry(new IFollowUpPromptVariant[]
        {
            new ChatGptFollowUpPromptVariant(),
            new ClaudeFollowUpPromptVariant(),
            new GeminiFollowUpPromptVariant(),
            new StubTestFollowUpVariant(),
        });

        var result = registry.Build(TestPlatform, comparisonSchemaJson: "{}");

        Assert.Equal("<test-followup-stub/>", result);
    }

    [Fact]
    public void MetaGapRegistry_dispatches_to_test_variant_when_test_platform_supplied()
    {
        var registry = new MetaGapPromptVariantRegistry(new IMetaGapPromptVariant[]
        {
            new ChatGptMetaGapPromptVariant(),
            new ClaudeMetaGapPromptVariant(),
            new GeminiMetaGapPromptVariant(),
            new StubTestMetaGapVariant(),
        });

        var result = registry.Build(
            TestPlatform,
            commanderName: "Atraxa",
            myDeckEntries: Array.Empty<DeckEntry>(),
            myDeckCombos: null,
            selectedEntries: Array.Empty<EdhTop16Entry>(),
            referenceDeckCombos: Array.Empty<CommanderSpellbookResult?>(),
            oracleNameMap: new Dictionary<string, string>(),
            schemaJson: "{}");

        Assert.Equal("<test-metagap-stub/>", result);
    }

    // ---- Normalize fallback test ----

    [Fact]
    public void Normalize_does_not_recognize_test_platform_key()
    {
        // Production AiPlatform.All does NOT contain Test; Normalize falls back to Default.
        // This documents that AiPlatform.Test is a test-time fiction, not a production
        // platform — addresses D-05's rationale for the InternalsVisibleTo seam over
        // a production Test field.
        var result = AiPlatform.Normalize("Test");

        Assert.Equal(AiPlatform.Default, result);
    }
}
