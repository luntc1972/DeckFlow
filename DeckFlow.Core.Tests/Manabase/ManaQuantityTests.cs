using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// MQ-02 (per-source mana quantity): a source pays up to its mana amount in pips, but all of ONE
/// chosen color, so a multi-color source can never pay two DIFFERENT colored pips. The flag-off path
/// (amount == 1) is byte-identical to the prior one-source-per-pip behavior, and mana amount never
/// touches the Karsten color-source counts.
/// </summary>
public sealed class ManaQuantityTests
{
    private static readonly IReadOnlyList<ManaColor> AnyColor =
        new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green };

    private static readonly IReadOnlyList<ManaColor> Colorless = System.Array.Empty<ManaColor>();

    // ---- ManaProductionAmount.Parse --------------------------------------------------------

    [Theory]
    [InlineData("{T}: Add {C}{C}.", 2)] // Sol Ring / Ancient Tomb
    [InlineData("{T}: Add {C}{C}{C}.", 3)] // Mana Vault
    [InlineData("{T}: Add three mana of any one color.", 3)] // Gilded Lotus (word form)
    [InlineData("{T}: Add two mana of any one color.", 2)]
    [InlineData("{T}: Add {2}.", 2)] // generic burst
    [InlineData("{T}: Add {G}.", 1)] // normal dork
    [InlineData("({T}: Add {G}.)", 1)] // basic land reminder text
    [InlineData("{T}: Add {W}{U}.", 1)] // fixed split across colors → safe default
    [InlineData("{T}: Add five mana in any combination of colors.", 1)] // Chromatic Orrery → not one-color
    [InlineData("{T}: Add {C} for each Swamp you control.", 1)] // scaling → not credited
    [InlineData("{T}: Add {W/U}{W/U}.", 1)] // hybrid pips → out of scope
    [InlineData("Whenever this attacks, draw a card.", 1)] // no mana clause
    [InlineData(null, 1)]
    [InlineData("", 1)]
    public void Parse_ReturnsExpectedAmount(string? oracle, int expected)
    {
        Assert.Equal(expected, ManaProductionAmount.Parse(oracle));
    }

    // ---- ColorsCoverable payment model (the BLOCKER fix) -----------------------------------

    [Fact]
    public void GildedLotus_PaysThreeOfOneChosenColor()
    {
        // One any-color source making 3 mana covers three pips of a SINGLE color.
        bool ok = CastabilitySimulator.ColorsCoverableForTest(
            new[] { (AnyColor, 3) },
            new[] { (ManaColor.Blue, 3) },
            effectiveCost: 3);

        Assert.True(ok);
    }

    [Fact]
    public void GildedLotus_CannotPayThreeDifferentColoredPips()
    {
        // The headline BLOCKER: a single 3-mana any-color source must NOT satisfy {U}{B}{R}, because
        // Gilded Lotus makes three mana of ONE chosen color, not three independent any-color units.
        bool ok = CastabilitySimulator.ColorsCoverableForTest(
            new[] { (AnyColor, 3) },
            new[] { (ManaColor.Blue, 1), (ManaColor.Black, 1), (ManaColor.Red, 1) },
            effectiveCost: 3);

        Assert.False(ok);
    }

    [Fact]
    public void AnyColorTwoManaSource_CannotSplitAcrossTwoColors()
    {
        bool ok = CastabilitySimulator.ColorsCoverableForTest(
            new[] { (AnyColor, 2) },
            new[] { (ManaColor.Blue, 1), (ManaColor.Red, 1) },
            effectiveCost: 2);

        Assert.False(ok);
    }

    [Fact]
    public void TwoSeparateLotuses_CanPayTwoDifferentColors()
    {
        // Two distinct multi-color sources each lock to one color → together they cover two colors.
        bool ok = CastabilitySimulator.ColorsCoverableForTest(
            new[] { (AnyColor, 3), (AnyColor, 3) },
            new[] { (ManaColor.Blue, 1), (ManaColor.Red, 1) },
            effectiveCost: 2);

        Assert.True(ok);
    }

    [Fact]
    public void CapacityMatching_IsExact_NotGreedy()
    {
        // Codex counterexample: a greedy that wastes the 1-capacity source on W (then can't cover U)
        // false-rejects. The exact solver locks the 2-capacity WU source to W (covers {W}{W}) and the
        // 1-capacity WU source to U → castable.
        var wu = new[] { ManaColor.White, ManaColor.Blue } as IReadOnlyList<ManaColor>;
        bool ok = CastabilitySimulator.ColorsCoverableForTest(
            new[] { (wu, 1), (wu, 2) },
            new[] { (ManaColor.White, 2), (ManaColor.Blue, 1) },
            effectiveCost: 3);

        Assert.True(ok);
    }

    [Fact]
    public void ColorlessSource_PaysGenericNotColor()
    {
        // Sol Ring (colorless, amount 2) covers two generic but cannot pay a colored pip.
        Assert.True(CastabilitySimulator.ColorsCoverableForTest(
            new[] { (Colorless, 2) }, System.Array.Empty<(ManaColor, int)>(), effectiveCost: 2));

        Assert.False(CastabilitySimulator.ColorsCoverableForTest(
            new[] { (Colorless, 2) }, new[] { (ManaColor.Blue, 1) }, effectiveCost: 2));
    }

    [Fact]
    public void AmountOne_MatchesOneSourcePerPip()
    {
        // Flag-off equivalence: three single-blue amount-1 sources cover UUU; one cannot.
        var one = ManaColor.Blue;
        Assert.True(CastabilitySimulator.ColorsCoverableForTest(
            new[] { (new[] { one } as IReadOnlyList<ManaColor>, 1), (new[] { one } as IReadOnlyList<ManaColor>, 1), (new[] { one } as IReadOnlyList<ManaColor>, 1) },
            new[] { (ManaColor.Blue, 3) },
            effectiveCost: 3));

        Assert.False(CastabilitySimulator.ColorsCoverableForTest(
            new[] { (new[] { one } as IReadOnlyList<ManaColor>, 1) },
            new[] { (ManaColor.Blue, 3) },
            effectiveCost: 3));
    }

    // ---- Analyzer-level: flag wiring, invariants, affordability ----------------------------

    [Fact]
    public void ColorFindings_AreInvariant_ToManaQuantityFlag()
    {
        ManabaseDeck deck = SolRingBlueDeck();

        ManabaseReport off = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, useManaQuantity: false);
        ManabaseReport on = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, useManaQuantity: true);

        // The Karsten color math must NOT move with mana amount (locked decision).
        Assert.Equal(off.TargetLands, on.TargetLands);
        Assert.Equal(off.ColorFindings.Count, on.ColorFindings.Count);
        for (int i = 0; i < off.ColorFindings.Count; i++)
        {
            Assert.Equal(off.ColorFindings[i].Color, on.ColorFindings[i].Color);
            Assert.Equal(off.ColorFindings[i].ActualSources, on.ColorFindings[i].ActualSources);
            Assert.Equal(off.ColorFindings[i].RequiredSources, on.ColorFindings[i].RequiredSources);
        }
    }

    [Fact]
    public void ManaQuantity_RaisesAffordability_OfAColorlessPayoff()
    {
        // A deck rich in Sol Rings (colorless, amount 2 when the flag is on) casts an expensive
        // colorless payoff MORE often once the burst mana is modeled.
        ManabaseDeck deck = SolRingBlueDeck();

        ManabaseReport off = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, useManaQuantity: false);
        ManabaseReport on = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, useManaQuantity: true);

        int castOff = off.Castability.First(c => c.Name == "Big Colorless").CastPercent;
        int castOn = on.Castability.First(c => c.Name == "Big Colorless").CastPercent;

        Assert.True(castOn > castOff, $"expected mana-quantity to raise cast% (off={castOff}, on={castOn})");
    }

    [Fact]
    public void ConditionalGrantedSource_IgnoresManaAmount()
    {
        // A conditional source is gated by a per-trial Bernoulli roll on one speculative unit; its
        // mana amount must be ignored even with the flag on. A deck of conditional amount-3 sources
        // casts identically to the same deck with amount-1 conditional sources.
        ManabaseDeck three = ConditionalDeck(manaAmount: 3);
        ManabaseDeck oneUnit = ConditionalDeck(manaAmount: 1);

        int castThree = ManabaseAnalyzer.Analyze(three, ManabaseMode.Casual, useManaQuantity: true)
            .Castability.First(c => c.Name == "Big Colorless").CastPercent;
        int castOne = ManabaseAnalyzer.Analyze(oneUnit, ManabaseMode.Casual, useManaQuantity: true)
            .Castability.First(c => c.Name == "Big Colorless").CastPercent;

        Assert.Equal(castOne, castThree);
    }

    // ---- builders --------------------------------------------------------------------------

    private static SpellRequirement BigColorless() => new()
    {
        Name = "Big Colorless",
        ManaValue = 6,
        Pips = new Dictionary<ManaColor, int>(),
    };

    private static ManabaseDeck SolRingBlueDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 30; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        // Many "Sol Ring"-style colorless rocks (amount 2) so the burst mana is reliably drawn.
        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource
            {
                Name = "Sol Ring",
                Produces = System.Array.Empty<ManaColor>(),
                IsLand = false,
                ManaAmount = 2,
            });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement> { BigColorless() },
            AverageManaValue = 3.0,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck ConditionalDeck(int manaAmount)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 30; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource
            {
                Name = "Granted",
                Produces = System.Array.Empty<ManaColor>(),
                IsLand = false,
                Weight = 0.25,
                IsConditional = true,
                ManaAmount = manaAmount,
            });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement> { BigColorless() },
            AverageManaValue = 3.0,
            IsSingleton = true,
        };
    }
}
