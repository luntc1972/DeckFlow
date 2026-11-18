using DeckFlow.Core.Bracket;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Bracket;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class BracketCardNameListTests
{
    [Fact]
    public void Join_PreservesCommasWithinNames()
    {
        Assert.Equal("Braids, Cabal Minion; Mana Crypt",
            BracketCardNameList.Join(["Braids, Cabal Minion", "Mana Crypt"]));
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Prompt_GameChangerTrimList_UsesSemicolonSeparator(string platformName)
    {
        var classification = new BracketClassification(
            BracketNumber: 3,
            DetectedGameChangers: ["Braids, Cabal Minion", "Mana Crypt"],
            DetectedMassLandDenial: [],
            DetectedExtraTurnCards: [],
            TwoCardCombos: [],
            ComboDetectionAvailable: true,
            EffectiveDate: "2026-02-09");

        var prompt = new BracketPromptVariantRegistry([
            new ChatGptBracketPromptVariant(),
            new ClaudeBracketPromptVariant(),
            new GeminiBracketPromptVariant(),
        ]).Build(AiPlatform.Normalize(platformName), classification, 2,
            "Game Changer separator deck", BuildTiers(), BuildCatalog());

        Assert.Contains("Trim 2 of: Braids, Cabal Minion; Mana Crypt", prompt,
            StringComparison.Ordinal);
    }

    private static IReadOnlyList<BracketTier> BuildTiers() =>
    [
        new(1, "Exhibition", "Bracket 1: Exhibition", "Theme", "Expect 9+ turns", 0),
        new(2, "Core", "Bracket 2: Core", "Core", "Expect 8+ turns", 0),
        new(3, "Upgraded", "Bracket 3: Upgraded", "Upgraded", "Expect 6+ turns", 3),
    ];

    private static GameChangerCatalog BuildCatalog() =>
        new(new DateOnly(2026, 2, 9), ["Braids, Cabal Minion", "Mana Crypt"], [], [], BuildTiers());
}
