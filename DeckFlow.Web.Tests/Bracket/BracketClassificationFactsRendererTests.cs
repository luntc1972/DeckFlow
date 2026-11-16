using DeckFlow.Core.Bracket;
using DeckFlow.Web.Services.PromptBuilders.Bracket;
using Xunit;

namespace DeckFlow.Web.Tests.Services.PromptBuilders.Bracket;

public sealed class BracketClassificationFactsRendererTests
{
    [Fact]
    public void Render_ListsGameChangerCountAgainstCap()
    {
        var classification = Classification() with { DetectedGameChangers = ["Demonic Tutor", "Vampiric Tutor"] };
        var actual = BracketClassificationFactsRenderer.Render(classification, Tiers());

        Assert.Equal(Lines(
            "- 2 Game Changers (B3 allows up to 3).",
            "- No two-card win combos detected.",
            "- 0 mass land denial.",
            "- 0 extra-turn cards (informational only; does not affect bracket number)."), actual);
        Assert.DoesNotContain("tutor-count gate", actual);
        Assert.Contains("- 1 Game Changer (B3 allows up to 3).", BracketClassificationFactsRenderer.Render(
            classification with { DetectedGameChangers = ["Demonic Tutor"] }, Tiers()));
        Assert.Contains("- 2 Game Changers (B4 has no cap).", BracketClassificationFactsRenderer.Render(
            classification with { BracketNumber = 4 }, Tiers()));
        Assert.Throws<ArgumentNullException>(() => BracketClassificationFactsRenderer.Render(null!, Tiers()));
        Assert.Throws<ArgumentNullException>(() => BracketClassificationFactsRenderer.Render(classification, null!));
    }

    [Fact]
    public void Render_ListsEachTwoCardCombo()
    {
        var classification = Classification() with
        {
            TwoCardCombos =
            [
                new TwoCardCombo(["Thassa's Oracle", "Demonic Consultation"], ["Win"]),
                new TwoCardCombo(["Thassa's Oracle", "Tainted Pact"], ["Win"]),
            ],
        };
        var actual = BracketClassificationFactsRenderer.Render(classification, Tiers());

        Assert.Equal(Lines(
            "- 0 Game Changers (B3 allows up to 3).",
            "- 2-card win combo: Thassa's Oracle + Demonic Consultation.",
            "- 2-card win combo: Thassa's Oracle + Tainted Pact.",
            "- 0 mass land denial.",
            "- 0 extra-turn cards (informational only; does not affect bracket number)."), actual);
        Assert.DoesNotContain("tutor-count gate", actual);
    }

    [Fact]
    public void Render_OmitsComboLine_WhenDetectionUnavailable()
    {
        var classification = Classification() with { ComboDetectionAvailable = false };
        var actual = BracketClassificationFactsRenderer.Render(classification, Tiers());

        Assert.Equal(Lines(
            "- 0 Game Changers (B3 allows up to 3).",
            "- 0 mass land denial.",
            "- 0 extra-turn cards (informational only; does not affect bracket number)."), actual);
        Assert.DoesNotContain("combo", actual, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No two-card win combos detected.", actual);
        Assert.DoesNotContain("tutor-count gate", actual);
    }

    [Fact]
    public void Render_ListsMassLandDenialAndExtraTurns()
    {
        var classification = Classification() with
        {
            DetectedMassLandDenial = ["Armageddon", "Ravages of War"],
            DetectedExtraTurnCards = ["Time Warp", "Temporal Manipulation"],
        };
        var actual = BracketClassificationFactsRenderer.Render(classification, Tiers());

        Assert.Equal(Lines(
            "- 0 Game Changers (B3 allows up to 3).",
            "- No two-card win combos detected.",
            "- 1 mass land denial: Armageddon.",
            "- 1 mass land denial: Ravages of War.",
            "- 2 extra-turn cards (informational only; does not affect bracket number)."), actual);
        Assert.DoesNotContain("tutor-count gate", actual);
        Assert.Contains("- 1 extra-turn card (informational only; does not affect bracket number).",
            BracketClassificationFactsRenderer.Render(
                classification with { DetectedExtraTurnCards = ["Time Warp"] }, Tiers()));
    }

    private static BracketClassification Classification() =>
        new(3, [], [], [], [], true, "2026-11-16");

    private static IReadOnlyList<BracketTier> Tiers() =>
    [
        new(3, "Upgraded", "Bracket 3", "Upgraded decks", "Turns 6-7", 3),
        new(4, "Optimized", "Bracket 4", "Optimized decks", "Turns 4-5", -1),
    ];

    private static string Lines(params string[] lines) =>
        string.Join(Environment.NewLine, lines) + Environment.NewLine;
}
