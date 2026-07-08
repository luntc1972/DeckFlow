using System;

using DeckFlow.Core.Manabase;
using DeckFlow.Web.Services.Manabase;

using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Phase 2 (plan-presence): <see cref="PlanRoleClassifier"/> — the pure 3-source role resolver
/// (crowd categories → combo piece → oracle-text heuristic, first-hit-wins). The I/O that fetches
/// categories and the combo set lives in the service; this tests the pure decision. Counter handling
/// is mode-dependent: a pure counterspell earns Interaction only in cEDH.
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
    [InlineData("Protection", PlanRole.Interaction)]
    [InlineData("Tutor", PlanRole.TutorCombo)]
    [InlineData("Combo Piece", PlanRole.TutorCombo)]
    [InlineData("Card Draw", PlanRole.Engine)]
    [InlineData("Value Engine", PlanRole.Engine)]
    public void FromCategories_MapsKeywordToRole(string category, PlanRole expected)
    {
        // These keywords are mode-independent; Casual is representative.
        Assert.Equal(expected, PlanRoleClassifier.FromCategories(new[] { category }, ManabaseMode.Casual));
    }

    [Theory]
    [InlineData(ManabaseMode.Cedh, PlanRole.Interaction)]
    [InlineData(ManabaseMode.Casual, PlanRole.None)]
    public void FromCategories_CounterTag_IsInteractionOnlyInCedh(ManabaseMode mode, PlanRole expected)
    {
        Assert.Equal(expected, PlanRoleClassifier.FromCategories(new[] { "Counterspell" }, mode));
    }

    [Theory]
    [InlineData("Ramp")]
    [InlineData("Mana Rock")]
    [InlineData("Lands")]
    [InlineData("Utility")]
    public void FromCategories_ResourceTags_YieldNone(string category)
    {
        Assert.Equal(PlanRole.None, PlanRoleClassifier.FromCategories(new[] { category }, ManabaseMode.Casual));
    }

    [Fact]
    public void FromCategories_MultipleTags_CombineAsFlags()
    {
        PlanRole roles = PlanRoleClassifier.FromCategories(new[] { "Win Condition", "Card Draw" }, ManabaseMode.Casual);

        Assert.Equal(PlanRole.Payoff | PlanRole.Engine, roles);
    }

    [Fact]
    public void FromHeuristic_PermanentDraw_IsEngine_ButOneShotDrawSpellIsNot()
    {
        PlanRole engine = PlanRoleClassifier.FromHeuristic(
            Fact("Artifact", "At the beginning of your upkeep, draw a card."), ManabaseMode.Casual);
        Assert.True(engine.HasFlag(PlanRole.Engine));

        PlanRole oneShot = PlanRoleClassifier.FromHeuristic(
            Fact("Sorcery", "Draw two cards."), ManabaseMode.Casual);
        Assert.False(oneShot.HasFlag(PlanRole.Engine));
    }

    [Fact]
    public void FromHeuristic_TutorAndInteractionAndPayoff_Detected()
    {
        Assert.True(PlanRoleClassifier.FromHeuristic(
            Fact("Sorcery", "Search your library for a card, then shuffle."), ManabaseMode.Casual).HasFlag(PlanRole.TutorCombo));

        Assert.True(PlanRoleClassifier.FromHeuristic(
            Fact("Instant", "Destroy target creature."), ManabaseMode.Casual).HasFlag(PlanRole.Interaction));

        Assert.True(PlanRoleClassifier.FromHeuristic(
            Fact("Sorcery", "Take an extra turn after this one."), ManabaseMode.Casual).HasFlag(PlanRole.Payoff));
    }

    [Fact]
    public void FromHeuristic_PureCounterspell_StrippedInCasual_KeptInCedh()
    {
        CardFact counter = Fact("Instant", "Counter target spell.");
        Assert.False(PlanRoleClassifier.FromHeuristic(counter, ManabaseMode.Casual).HasFlag(PlanRole.Interaction));
        Assert.True(PlanRoleClassifier.FromHeuristic(counter, ManabaseMode.Cedh).HasFlag(PlanRole.Interaction));
    }

    [Fact]
    public void FromHeuristic_NarrowCounter_StrippedInCasual_KeptInCedh()
    {
        // Negate-style narrow counter: DeckStatClassifier.IsCounterspellCard misses it (exact
        // "counter target spell" only), but the casual carve-out still strips it.
        CardFact negate = Fact("Instant", "Counter target noncreature spell.");
        Assert.False(PlanRoleClassifier.FromHeuristic(negate, ManabaseMode.Casual).HasFlag(PlanRole.Interaction));
        Assert.True(PlanRoleClassifier.FromHeuristic(negate, ManabaseMode.Cedh).HasFlag(PlanRole.Interaction));
    }

    [Fact]
    public void FromHeuristic_RemovalAndCounterWithRemoval_KeptInCasual()
    {
        // Real removal always counts, even in casual.
        Assert.True(PlanRoleClassifier.FromHeuristic(
            Fact("Instant", "Destroy target creature."), ManabaseMode.Casual).HasFlag(PlanRole.Interaction));

        // A counter that ALSO removes has removal merit beyond the counter, so it stays.
        Assert.True(PlanRoleClassifier.FromHeuristic(
            Fact("Instant", "Counter target spell. Destroy target creature."), ManabaseMode.Casual)
            .HasFlag(PlanRole.Interaction));

        // Board wipes and non-counter instants (burn, combat tricks) are interaction in both modes.
        Assert.True(PlanRoleClassifier.FromHeuristic(
            Fact("Sorcery", "Destroy all creatures."), ManabaseMode.Casual).HasFlag(PlanRole.Interaction));
        Assert.True(PlanRoleClassifier.FromHeuristic(
            Fact("Instant", "Deal 3 damage to any target."), ManabaseMode.Casual).HasFlag(PlanRole.Interaction));
    }

    [Fact]
    public void FromHeuristic_PlainCreature_YieldsNone()
    {
        Assert.Equal(PlanRole.None, PlanRoleClassifier.FromHeuristic(
            Fact("Creature — Bear", "Vanilla 2/2."), ManabaseMode.Casual));
    }

    [Fact]
    public void Classify_CategoriesWinOverComboAndHeuristic()
    {
        // Card is a combo piece AND its oracle text would tutor, but a Payoff category is present:
        // first-hit-wins means the category role is used, not TutorCombo.
        CardFact fact = Fact("Creature", "Search your library for a card.");

        PlanRole roles = PlanRoleClassifier.Classify(fact, new[] { "Win Condition" }, isComboPiece: true, ManabaseMode.Casual);

        Assert.Equal(PlanRole.Payoff, roles);
    }

    [Fact]
    public void Classify_ComboPieceWins_WhenNoCategoryRole()
    {
        // Resource-only category (no role) + combo piece -> TutorCombo, heuristic not consulted.
        CardFact fact = Fact("Creature", "Vanilla.");

        PlanRole roles = PlanRoleClassifier.Classify(fact, new[] { "Ramp" }, isComboPiece: true, ManabaseMode.Casual);

        Assert.Equal(PlanRole.TutorCombo, roles);
    }

    [Theory]
    [InlineData(ManabaseMode.Cedh, PlanRole.Interaction)]
    [InlineData(ManabaseMode.Casual, PlanRole.None)]
    public void Classify_PureCounterspell_HeuristicInteractionOnlyInCedh(ManabaseMode mode, PlanRole expected)
    {
        CardFact fact = Fact("Instant", "Counter target spell.");

        PlanRole roles = PlanRoleClassifier.Classify(fact, Array.Empty<string>(), isComboPiece: false, mode);

        Assert.Equal(expected, roles);
    }
}
