using System.Collections.Generic;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// MQ-03 (rampCreditV2): the Karsten ramp/draw land-target credit (RampAndDrawUnderThree, −0.28
/// each) must, when the flag is on, count only REPEATABLE ramp + true card draw — not one-shot
/// rituals or Treasure-makers. Flag-off must match the historic broad predicate exactly.
/// </summary>
public sealed class ManaRampCreditTests
{
    private static CardFact Fact(string name, double mv, string typeLine, string oracle) => new()
    {
        Name = name,
        Quantity = 1,
        ManaValue = mv,
        TypeLine = typeLine,
        OracleText = oracle,
    };

    private static int Credit(CardFact card, bool v2) =>
        ManabaseClassifier.Classify(new[] { card }, isSingleton: true, rampCreditV2: v2).RampAndDrawUnderThree;

    public static IEnumerable<object[]> Cards() => new[]
    {
        // name, mv, typeLine, oracle, creditedV2, creditedBroad
        new object[] { "Dark Ritual", 1.0, "Instant", "Add {B}{B}{B}.", false, true },               // one-shot ritual: dropped in v2
        new object[] { "Jeska's Will", 3.0, "Sorcery", "Add {R} for each card...", false, false },    // MV>2: never counted (either way)
        new object[] { "Sol Ring", 1.0, "Artifact", "{T}: Add {C}{C}.", true, true },                 // mana permanent: kept
        new object[] { "Utopia Sprawl", 1.0, "Enchantment — Aura", "Enchant Forest ... Add {G}.", true, true }, // enchantment ramp: kept (broader than rock/dork)
        new object[] { "Rampant Growth", 2.0, "Sorcery", "Search your library for a basic land card, put it onto the battlefield tapped...", true, true }, // land-to-battlefield: kept even though sorcery
        new object[] { "Land Grant", 1.0, "Sorcery", "Search your library for a basic land card, reveal it, and put it into your hand.", false, true },        // land-to-HAND: dropped in v2
        new object[] { "Ponder", 1.0, "Sorcery", "Look at the top three cards ... draw a card.", true, true }, // cantrip: kept
        new object[] { "Wily Goblin", 2.0, "Creature — Goblin", "When Wily Goblin enters, create a Treasure token.", false, true }, // one-shot Treasure: dropped in v2
    };

    [Theory]
    [MemberData(nameof(Cards))]
    public void Credit_NarrowsInV2_MatchesBroadWhenOff(string name, double mv, string type, string oracle, bool v2, bool broad)
    {
        CardFact card = Fact(name, mv, type, oracle);
        Assert.Equal(v2 ? 1 : 0, Credit(card, v2: true));
        Assert.Equal(broad ? 1 : 0, Credit(card, v2: false));
    }

    [Fact]
    public void MultiFacePermanent_WithOneShotManaBack_NotCreditedInV2()
    {
        // Adventure/MDFC: front face is a permanent with no mana, the back/adventure adds one-shot
        // mana. Joined oracle leaks "Add" (broad keeps it), but the FRONT face is not repeatable ramp.
        var card = new CardFact
        {
            Name = "Adventure Creature",
            Quantity = 1,
            ManaValue = 2,
            TypeLine = "Creature — Giant",
            FrontFaceOracleText = "Trample",                 // front: no mana production
            OracleText = "Trample\nStomp {1}{R} — Add {R}{R}.", // joined: leaks one-shot mana
        };

        Assert.Equal(0, Credit(card, v2: true));   // front-face has no "Add" → dropped in v2
        Assert.Equal(1, Credit(card, v2: false));  // broad reads joined "Add" → credited
    }

    [Fact]
    public void RampCreditV2_Off_MatchesBroadPredicate_OnMixedDeck()
    {
        var deck = new[]
        {
            Fact("Dark Ritual", 1, "Instant", "Add {B}{B}{B}."),
            Fact("Sol Ring", 1, "Artifact", "{T}: Add {C}{C}."),
            Fact("Land Grant", 1, "Sorcery", "Search your library for a basic land card ... into your hand."),
            Fact("Ponder", 1, "Sorcery", "... draw a card."),
            Fact("Wily Goblin", 2, "Creature", "create a Treasure token."),
        };

        // Off path == historic broad predicate: all five match (Add / land-search / draw / Treasure).
        Assert.Equal(5, ManabaseClassifier.Classify(deck, rampCreditV2: false).RampAndDrawUnderThree);
        // V2 keeps only Sol Ring (permanent mana) + Ponder (draw) = 2.
        Assert.Equal(2, ManabaseClassifier.Classify(deck, rampCreditV2: true).RampAndDrawUnderThree);
    }
}
