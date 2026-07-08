using System;

using DeckFlow.Core.Manabase;
using DeckFlow.Web.Services.Manabase;

using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Phase 2 (plan-presence): <see cref="PlanRoleClassifier"/> — the pure 3-source role resolver
/// (crowd categories → combo piece → oracle-text heuristic, first-hit-wins). The I/O that fetches
/// categories and the combo set lives in the service; this tests the pure decision.
/// </summary>
public sealed class PlanRoleClassifierTests
{
    private static CardFact Fact(string typeLine, string? oracle = null, string name = "Card") => new()
    {
        Name = name,
        Quantity = 1,
        TypeLine = typeLine,
        OracleText = oracle,
    };

    [Theory]
    [InlineData("Win Condition", PlanRole.Payoff)]
    [InlineData("Finisher", PlanRole.Payoff)]
    [InlineData("Removal", PlanRole.Interaction)]
    [InlineData("Counterspell", PlanRole.Interaction)]
    [InlineData("Protection", PlanRole.Interaction)]
    [InlineData("Tutor", PlanRole.TutorCombo)]
    [InlineData("Combo Piece", PlanRole.TutorCombo)]
    [InlineData("Card Draw", PlanRole.Engine)]
    [InlineData("Value Engine", PlanRole.Engine)]
    public void FromCategories_MapsKeywordToRole(string category, PlanRole expected)
    {
        Assert.Equal(expected, PlanRoleClassifier.FromCategories(new[] { category }));
    }

    [Theory]
    [InlineData("Ramp")]
    [InlineData("Mana Rock")]
    [InlineData("Lands")]
    [InlineData("Utility")]
    public void FromCategories_ResourceTags_YieldNone(string category)
    {
        Assert.Equal(PlanRole.None, PlanRoleClassifier.FromCategories(new[] { category }));
    }

    [Fact]
    public void FromCategories_MultipleTags_CombineAsFlags()
    {
        PlanRole roles = PlanRoleClassifier.FromCategories(new[] { "Win Condition", "Card Draw" });

        Assert.Equal(PlanRole.Payoff | PlanRole.Engine, roles);
    }

    [Fact]
    public void FromHeuristic_PermanentDraw_IsEngine_ButOneShotDrawSpellIsNot()
    {
        PlanRole engine = PlanRoleClassifier.FromHeuristic(
            Fact("Artifact", "At the beginning of your upkeep, draw a card."));
        Assert.True(engine.HasFlag(PlanRole.Engine));

        PlanRole oneShot = PlanRoleClassifier.FromHeuristic(
            Fact("Sorcery", "Draw two cards."));
        Assert.False(oneShot.HasFlag(PlanRole.Engine));
    }

    [Fact]
    public void FromHeuristic_TutorAndInteractionAndPayoff_Detected()
    {
        Assert.True(PlanRoleClassifier.FromHeuristic(
            Fact("Sorcery", "Search your library for a card, then shuffle.")).HasFlag(PlanRole.TutorCombo));

        Assert.True(PlanRoleClassifier.FromHeuristic(
            Fact("Instant", "Destroy target creature.")).HasFlag(PlanRole.Interaction));

        Assert.True(PlanRoleClassifier.FromHeuristic(
            Fact("Sorcery", "Take an extra turn after this one.")).HasFlag(PlanRole.Payoff));
    }

    [Fact]
    public void FromHeuristic_PlainCreature_YieldsNone()
    {
        Assert.Equal(PlanRole.None, PlanRoleClassifier.FromHeuristic(
            Fact("Creature — Bear", "Vanilla 2/2.")));
    }

    [Fact]
    public void Classify_CategoriesWinOverComboAndHeuristic()
    {
        // Card is a combo piece AND its oracle text would tutor, but a Payoff category is present:
        // first-hit-wins means the category role is used, not TutorCombo.
        CardFact fact = Fact("Creature", "Search your library for a card.");

        PlanRole roles = PlanRoleClassifier.Classify(fact, new[] { "Win Condition" }, isComboPiece: true);

        Assert.Equal(PlanRole.Payoff, roles);
    }

    [Fact]
    public void Classify_ComboPieceWins_WhenNoCategoryRole()
    {
        // Resource-only category (no role) + combo piece -> TutorCombo, heuristic not consulted.
        CardFact fact = Fact("Creature", "Vanilla.");

        PlanRole roles = PlanRoleClassifier.Classify(fact, new[] { "Ramp" }, isComboPiece: true);

        Assert.Equal(PlanRole.TutorCombo, roles);
    }

    [Fact]
    public void Classify_FallsBackToHeuristic_WhenNoCategoriesAndNoCombo()
    {
        CardFact fact = Fact("Instant", "Counter target spell.");

        PlanRole roles = PlanRoleClassifier.Classify(fact, Array.Empty<string>(), isComboPiece: false);

        Assert.Equal(PlanRole.Interaction, roles);
    }
}
