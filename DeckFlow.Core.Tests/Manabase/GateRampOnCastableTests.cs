using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Efficacy R2 finding M3: the ramp-colored-cost gate. Without it the simulator deploys a mana dork
/// from a hand that cannot pay the dork's own colored cost and still credits its mana toward the
/// payoff — inflating cast % for thin-splash ramp packages. With the gate on, a ramp piece is credited
/// only once the board could actually pay the ramp's own cost (mirrors 17Lands).
///
/// These lock in that the gate demonstrably de-optimizes the exact thin-splash shape it targets. The
/// Web analysis service now passes <c>gateRampOnCastable: true</c> unconditionally (decoupled from the
/// land-ramp-sim flag), so this is the behavior every real analysis gets.
/// </summary>
public sealed class GateRampOnCastableTests
{
    // A green mana dork (Llanowar Elves: costs {G}, taps for G) splashed into a deck whose colored
    // sources are almost entirely blue. The payoff is generic-heavy blue, castable on color from the
    // Islands but happy to be rushed by the dork's extra mana. Green is a 2-source splash, so the dork
    // is usually stranded in hand — exactly when the ungated sim wrongly credits its mana.
    private static IReadOnlyList<CardFact> ThinGreenSplashRampDeck() => new List<CardFact>
    {
        Dork("Llanowar Elves", "{G}", "G"),
        Land("Forest", 2, "G"),
        Land("Island", 34, "U"),
        // Generic-heavy blue payoff (MV 6): plentiful blue covers the colour, so the only lever left is
        // whether the dork's ramp mana is (correctly) credited.
        SpellCard("Big Blue", 6, "{5}{U}", "Draw three cards."),
        // Inert filler padded out to a ~99-card library.
        SpellCard("Filler", 3, "{3}", "Vanilla.", qty: 61),
    };

    [Fact]
    public void Gate_ThinSplashRamp_LowersCastPercentVersusUngated()
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(ThinGreenSplashRampDeck(), isSingleton: true);
        SpellRequirement payoff = deck.Spells.First(s => s.Name == "Big Blue");
        int librarySize = deck.TotalCards - deck.CommanderCount;

        CardCastability off = CastabilitySimulator.Simulate(
            deck, librarySize, payoff, effectiveTurn: 6, genericReduction: 0, gateRampOnCastable: false);
        CardCastability on = CastabilitySimulator.Simulate(
            deck, librarySize, payoff, effectiveTurn: 6, genericReduction: 0, gateRampOnCastable: true);

        // The ungated sim credits the stranded green dork's mana; the gate withholds it, so on-curve
        // castability must drop (the dork can no longer rush the payoff from a green-less hand).
        Assert.True(on.CastPercent < off.CastPercent,
            $"gate should lower cast% for thin-splash ramp: off={off.CastPercent}%, on={on.CastPercent}%");
    }

    [Fact]
    public void Gate_MonoColorRamp_IsByteIdentical()
    {
        // Control: when the ramp's colored cost is always payable (mono-green, so the dork's {G} is
        // never stranded), the gate changes nothing — it withholds credit only from UN-castable ramp.
        var monoGreen = new List<CardFact>
        {
            Dork("Llanowar Elves", "{G}", "G"),
            Land("Forest", 36, "G"),
            SpellCard("Big Green", 6, "{5}{G}", "Draw three cards."),
            SpellCard("Filler", 3, "{3}", "Vanilla.", qty: 61),
        };
        ManabaseDeck deck = ManabaseClassifier.Classify(monoGreen, isSingleton: true);
        SpellRequirement payoff = deck.Spells.First(s => s.Name == "Big Green");
        int librarySize = deck.TotalCards - deck.CommanderCount;

        CardCastability off = CastabilitySimulator.Simulate(
            deck, librarySize, payoff, effectiveTurn: 6, genericReduction: 0, gateRampOnCastable: false);
        CardCastability on = CastabilitySimulator.Simulate(
            deck, librarySize, payoff, effectiveTurn: 6, genericReduction: 0, gateRampOnCastable: true);

        Assert.Equal(off.CastPercent, on.CastPercent);
    }

    private static CardFact Dork(string name, string manaCost, string producesLetter) => new()
    {
        Name = name,
        Quantity = 1,
        ManaCost = manaCost,
        ManaValue = ManaCostParser.Parse(manaCost).ManaValue,
        TypeLine = "Creature — Elf Druid",
        OracleText = $"{{T}}: Add {{{producesLetter}}}.",
        ProducedMana = new[] { producesLetter },
    };

    private static CardFact Land(string name, int qty, string producesLetter) => new()
    {
        Name = name,
        Quantity = qty,
        TypeLine = name.StartsWith("Forest") || name.StartsWith("Island") ? $"Basic Land — {name}" : "Land",
        OracleText = $"{{T}}: Add {{{producesLetter}}}.",
        ProducedMana = new[] { producesLetter },
    };

    private static CardFact SpellCard(string name, int mv, string manaCost, string oracle, int qty = 1) => new()
    {
        Name = name,
        Quantity = qty,
        ManaCost = manaCost,
        ManaValue = mv,
        TypeLine = "Sorcery",
        OracleText = oracle,
        ProducedMana = System.Array.Empty<string>(),
    };
}
