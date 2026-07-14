using DeckFlow.Core.Bracket;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.Bracket;
using DeckFlow.Web.Services.PromptBuilders.Comparison;
using DeckFlow.Web.Services.PromptBuilders.FollowUp;
using DeckFlow.Web.Services.PromptBuilders.MetaGap;
using DeckFlow.Web.Services.PromptBuilders.Primer;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Invariant: EVERY ChatGPT prompt variant must emit the execute-immediately header as its first
/// line. ChatGPT's web UI silently converts large pastes into attached .txt files and then treats
/// the content as a reference document ("which task do you want me to run?") instead of executing
/// it; the leading header is the product guarantee that the packet runs in one round-trip anyway.
/// The per-family goldens only pin the seven variants that exist today — this suite is the gate
/// that fails when an eighth ChatGPT variant ships without the header. Per ADR-0001 the header
/// text itself stays hand-authored in each variant (no shared production constant); this test is
/// the mechanical enforcement that all hand-authored copies agree.
/// </summary>
public sealed class ChatGptPromptHeaderInvariantTests
{
    [Fact]
    public void Analysis_prompt_starts_with_execute_now_header()
        => AssertStartsWithHeader(new ChatGptAnalysisPromptVariant().Build(
            new DeckAnalysisRequest { Format = "Commander", TargetCommanderBracket = "cEDH" },
            "1 Sol Ring",
            "Reference text",
            "{}",
            "Atraxa",
            Array.Empty<string>(),
            Array.Empty<string>(),
            comboResult: null,
            includeCardVersions: false,
            enrichments: new AnalysisPromptEnrichments()));

    [Fact]
    public void Comparison_prompt_starts_with_execute_now_header()
        => AssertStartsWithHeader(new ChatGptComparisonPromptVariant().Build(
            CreateComparisonSummary("Deck A"),
            CreateComparisonSummary("Deck B"),
            "1 Sol Ring",
            "1 Sol Ring",
            "Deck A combos",
            "Deck B combos",
            "comparison context",
            "{}"));

    [Fact]
    public void FollowUp_prompt_starts_with_execute_now_header()
        => AssertStartsWithHeader(new ChatGptFollowUpPromptVariant().Build("{}"));

    [Fact]
    public void MetaGap_prompt_starts_with_execute_now_header()
        => AssertStartsWithHeader(new ChatGptMetaGapPromptVariant().Build(
            "Atraxa",
            new[] { CreateDeckEntry("Sol Ring") },
            myDeckCombos: null,
            Array.Empty<EdhTop16Entry>(),
            Array.Empty<CommanderSpellbookResult?>(),
            new Dictionary<string, string>(),
            "{}"));

    [Fact]
    public void Primer_prompt_starts_with_execute_now_header()
        => AssertStartsWithHeader(new ChatGptPrimerPromptVariant().Build(
            new DeckPrimerRequest { Format = "Commander" },
            "1 Sol Ring",
            new[] { new PrimerSectionEntry("overview", 1, "Overview", "Help text", "Core") },
            comboResult: null,
            top16Entries: null,
            categoryDistribution: null,
            bracketNumber: 3));

    [Fact]
    public void Bracket_prompt_starts_with_execute_now_header()
        => AssertStartsWithHeader(new ChatGptBracketPromptVariant().Build(
            new BracketClassification(
                BracketNumber: 3,
                DetectedGameChangers: Array.Empty<string>(),
                DetectedMassLandDenial: Array.Empty<string>(),
                DetectedExtraTurnCards: Array.Empty<string>(),
                TwoCardCombos: null,
                ComboDetectionAvailable: false,
                EffectiveDate: "2026-02-09"),
            targetBracketNumber: null,
            deckName: null,
            new BracketTier[]
            {
                new(3, "Upgraded", "Bracket 3: Upgraded", "Strong synergy.", "Expect 6+ turns", MaxGameChangers: 3),
            },
            new GameChangerCatalog(
                EffectiveDate: new DateOnly(2026, 2, 9),
                GameChangers: Array.Empty<string>(),
                MassLandDenialCards: Array.Empty<string>(),
                ExtraTurnCards: Array.Empty<string>(),
                Tiers: Array.Empty<BracketTier>())));

    [Fact]
    public void SetUpgrade_prompt_starts_with_execute_now_header()
        => AssertStartsWithHeader(new ChatGptSetUpgradePromptVariant().Build(
            new DeckAnalysisRequest { Format = "Commander", TargetCommanderBracket = "cEDH" },
            "1 Sol Ring",
            "{}",
            "Atraxa",
            generatedSetPacket: null,
            Array.Empty<string>()));

    // Why: AppendLine emits "\r\n" under Windows dotnet.exe while the expected header const uses
    // "\n\n"; strip "\r" so the assertion is OS-independent (same normalization as the goldens).
    private static void AssertStartsWithHeader(string prompt)
        => Assert.StartsWith(
            PacketByteIdentityFixtures.ChatGptImmediateHeader,
            prompt.Replace("\r", string.Empty),
            StringComparison.Ordinal);

    private static DeckComparisonService.DeckComparisonDeckSummary CreateComparisonSummary(string name)
        => new(
            Name: name,
            CommanderName: "Atraxa",
            Bracket: new CommanderBracketOption("cedh", "Bracket 5: cEDH", "Competitive.", "Games can end any turn"),
            MainboardCount: 99,
            Lands: 30,
            Creatures: 25,
            AverageManaValue: 2.5m,
            ManaCurve: new Dictionary<string, int>(),
            ColorIdentity: Array.Empty<string>(),
            CategorySummaries: Array.Empty<string>(),
            Ramp: 10,
            Draw: 10,
            Interaction: 10,
            Wipes: 2,
            Recursion: 2,
            ClosingPower: 5,
            SharedThemes: Array.Empty<string>(),
            ComboSummaries: Array.Empty<string>(),
            AlmostComboSummaries: Array.Empty<string>(),
            IncludedComboCount: 0,
            AlmostIncludedComboCount: 0);

    private static DeckEntry CreateDeckEntry(string name)
        => new()
        {
            Name = name,
            NormalizedName = CardNormalizer.Normalize(name),
            Quantity = 1,
            Board = "mainboard",
        };
}
