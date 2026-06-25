using System;
using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// NSM-RAMP-DISCLOSURE: the ramp disclosure surfaces the card NAMES behind the Ramp counts.
/// <see cref="ManabaseReport.RampSourceNames"/> projects the exact rock/dork predicate, and
/// <see cref="ManabaseReport.RampAndDrawNames"/> the ≤2 MV ramp/draw credit — both de-duplicated
/// by name in deck order. A card that is both a rock/dork and ≤2 MV ramp/draw appears in BOTH.
/// </summary>
public sealed class ManabaseAnalyzerRampNamesTests
{
    private static CardFact Rock(string name) => new()
    {
        Name = name,
        Quantity = 1,
        ManaCost = "{1}",
        ManaValue = 1,
        TypeLine = "Artifact",
        OracleText = "{T}: Add {C}{C}.",
        ProducedMana = new[] { "C" },
    };

    private static CardFact Dork(string name) => new()
    {
        Name = name,
        Quantity = 1,
        ManaCost = "{G}",
        ManaValue = 1,
        TypeLine = "Creature — Elf Druid",
        OracleText = "{T}: Add {G}.",
        ProducedMana = new[] { "G" },
    };

    private static CardFact DrawSpell(string name) => new()
    {
        Name = name,
        Quantity = 1,
        ManaCost = "{U}",
        ManaValue = 1,
        TypeLine = "Sorcery",
        OracleText = "Look at the top three cards of your library, then put them back. Draw a card.",
    };

    private static CardFact Land(string name, string produces) => new()
    {
        Name = name,
        Quantity = 1,
        ManaValue = 0,
        TypeLine = "Basic Land",
        OracleText = $"{{T}}: Add {{{produces}}}.",
        ProducedMana = new[] { produces },
    };

    // Deck order: a both-card rock, a both-card dork, a pure ≤2 MV draw spell, a duplicate of the
    // rock (to exercise name de-dup), plus a couple of lands so Analyze has a valid base.
    private static ManabaseReport AnalyzeRampDeck()
    {
        var cards = new List<CardFact>
        {
            Rock("Sol Ring"),
            Dork("Llanowar Elves"),
            DrawSpell("Ponder"),
            Rock("Sol Ring"),
            Land("Forest", "G"),
            Land("Island", "U"),
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);
        return ManabaseAnalyzer.Analyze(deck);
    }

    [Fact]
    public void RampSourceNames_ListsRockAndDork()
    {
        ManabaseReport report = AnalyzeRampDeck();

        Assert.Contains("Sol Ring", report.RampSourceNames);
        Assert.Contains("Llanowar Elves", report.RampSourceNames);
        Assert.DoesNotContain("Ponder", report.RampSourceNames);     // a draw spell is not a mana source
    }

    [Fact]
    public void RampAndDrawNames_ListsBothCardAndPureDrawSpell()
    {
        ManabaseReport report = AnalyzeRampDeck();

        Assert.Contains("Sol Ring", report.RampAndDrawNames);          // the both-card
        Assert.Contains("Ponder", report.RampAndDrawNames);            // pure ≤2 MV draw
    }

    [Fact]
    public void BothCard_AppearsInBothLists()
    {
        ManabaseReport report = AnalyzeRampDeck();

        Assert.Contains("Sol Ring", report.RampSourceNames);
        Assert.Contains("Sol Ring", report.RampAndDrawNames);
    }

    [Fact]
    public void Lists_AreDeduplicatedByName()
    {
        ManabaseReport report = AnalyzeRampDeck();

        // Two "Sol Ring" cards collapse to a single name in each list.
        Assert.Equal(
            report.RampSourceNames.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            report.RampSourceNames.Count);
        Assert.Equal(
            report.RampAndDrawNames.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            report.RampAndDrawNames.Count);
        Assert.Single(report.RampSourceNames, n => string.Equals(n, "Sol Ring", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Lists_PreserveFirstSeenDeckOrder()
    {
        ManabaseReport report = AnalyzeRampDeck();

        // Rocks/dorks in deck order: Sol Ring (1st), Llanowar Elves (2nd).
        Assert.Equal(new[] { "Sol Ring", "Llanowar Elves" }, report.RampSourceNames);

        // Ramp/draw in deck order: Sol Ring, Llanowar Elves, Ponder (each mana-permanent + the draw).
        Assert.Equal(new[] { "Sol Ring", "Llanowar Elves", "Ponder" }, report.RampAndDrawNames);
    }
}
