using DeckFlow.Core.Bracket;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Bracket;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Pins complete Bracket prompts before the computed reasons renderer changes.</summary>
public sealed class BracketPromptByteIdentityTests
{
    /// <summary>Pins classification without a balancer target.</summary>
    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Classification_MatchesGolden(string platformName)
    {
        var classification = new BracketClassification(
            BracketNumber: 3,
            DetectedGameChangers: ["Cyclonic Rift"],
            DetectedMassLandDenial: [],
            DetectedExtraTurnCards: [],
            TwoCardCombos: [],
            ComboDetectionAvailable: true,
            EffectiveDate: "2026-02-09");
        var actual = BuildRegistry().Build(AiPlatform.Normalize(platformName), classification,
            null, "Literal classification deck", BuildTiers(), BuildCatalog());

        Assert.Equal(BracketPromptGoldens.Classification(platformName), actual, StringComparer.Ordinal);
    }

    /// <summary>Pins every computed reason and the closer for a lower balancer target.</summary>
    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Balancer_MatchesGolden(string platformName)
    {
        var classification = new BracketClassification(
            BracketNumber: 4,
            DetectedGameChangers: ["Mana Crypt", "The One Ring", "Jeweled Lotus", "Cyclonic Rift"],
            DetectedMassLandDenial: ["Armageddon", "Ravages of War"],
            DetectedExtraTurnCards: ["Time Warp", "Temporal Manipulation"],
            TwoCardCombos:
            [
                new TwoCardCombo(["Thassa's Oracle", "Demonic Consultation"], ["Win on the spot"]),
                new TwoCardCombo(["Thassa's Oracle", "Tainted Pact"], ["Win on the spot"]),
            ],
            ComboDetectionAvailable: true,
            EffectiveDate: "2026-02-09");
        var actual = BuildRegistry().Build(AiPlatform.Normalize(platformName), classification,
            2, "Literal balancer deck", BuildTiers(), BuildCatalog());

        Assert.Equal(BracketPromptGoldens.Balancer(platformName), actual, StringComparer.Ordinal);
    }

    private static BracketPromptVariantRegistry BuildRegistry() =>
        new(new IBracketPromptVariant[]
        {
            new ChatGptBracketPromptVariant(),
            new ClaudeBracketPromptVariant(),
            new GeminiBracketPromptVariant(),
        });

    private static IReadOnlyList<BracketTier> BuildTiers() =>
    [
        new(1, "Exhibition", "Bracket 1: Exhibition", "Theme-first showcase decks; optimization takes a back seat. Expect 9+ turns before a win or loss.", "Expect 9+ turns", MaxGameChangers: 0),
        new(2, "Core",       "Bracket 2: Core",       "Unoptimized, straightforward decks with incremental, disruptable wins. Expect 8+ turns.",           "Expect 8+ turns", MaxGameChangers: 0),
        new(3, "Upgraded",   "Bracket 3: Upgraded",   "Strong synergy and card quality with meaningful interaction; explosive but earned wins. Expect 6+ turns.", "Expect 6+ turns", MaxGameChangers: 3),
        new(4, "Optimized",  "Bracket 4: Optimized",  "Fast, lethal, efficient decks with Game Changers, fast mana, and explosive lines. Expect 4+ turns.", "Expect 4+ turns", MaxGameChangers: -1),
        new(5, "cEDH",       "Bracket 5: cEDH",       "Metagame-tuned competitive Commander built for maximum efficiency and consistency. Games can end any turn.", "Games can end any turn", MaxGameChangers: -1),
    ];

    private static GameChangerCatalog BuildCatalog() =>
        new(
            EffectiveDate: new DateOnly(2026, 2, 9),
            GameChangers: ["Cyclonic Rift", "Jeweled Lotus", "Mana Crypt", "The One Ring"],
            MassLandDenialCards: ["Armageddon", "Ravages of War"],
            ExtraTurnCards: ["Time Warp", "Temporal Manipulation"],
            Tiers: BuildTiers());
}
