using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates the §6 mana-base prototype: hypergeometric math, Karsten source/land
/// targets, and the analyzer's verdict against the real "Buffs by Hans" (Xyris Temur)
/// deck that was cross-checked live against the Salubrious Snail manabase tool.
/// </summary>
public sealed class ManabaseAnalyzerTests
{
    [Fact]
    public void Hypergeometric_AtLeast_MatchesKnownTwoLandKeepProbability()
    {
        // 7-card opener from a 60-card deck with 24 lands, P(>= 2 lands) ≈ 0.84.
        double p = Hypergeometric.AtLeast(60, 24, 7, 2);
        Assert.InRange(p, 0.82, 0.86);
    }

    [Fact]
    public void Hypergeometric_AtLeast_ZeroRequirementIsCertain()
    {
        Assert.Equal(1.0, Hypergeometric.AtLeast(99, 36, 8, 0));
    }

    [Theory]
    [InlineData(1, 0.90)]
    [InlineData(2, 0.91)]
    [InlineData(4, 0.93)]
    [InlineData(7, 0.96)]
    public void ConsistencyThreshold_Is89PlusManaValue(int manaValue, double expected)
    {
        Assert.Equal(expected, KarstenManabase.ConsistencyThreshold(manaValue), 3);
    }

    [Fact]
    public void SingletonLandTarget_LowCurveTemurDeck_LandsNearThirtySeven()
    {
        // Buffs by Hans: 100 cards, 1 commander, avgMV 2.59, ~6 cheap ramp/draw, 1 common MDFC.
        double target = KarstenManabase.SingletonLandTarget(
            totalCards: 100,
            commanderCount: 1,
            averageManaValue: 2.59,
            rampAndDrawUnderThree: 6,
            mdfcCommon: 1);

        Assert.InRange(target, 36.0, 38.0);
    }

    [Fact]
    public void SourcesNeeded_SinglePip_SixtyCard_IsAroundFourteen()
    {
        // Karsten's canonical 60-card single-pip one-drop ≈ 14 sources.
        int need = KarstenManabase.SourcesNeeded(deckSize: 60, totalLands: 24, pips: 1, manaValue: 1);
        Assert.InRange(need, 13, 16);
    }

    [Fact]
    public void SourcesNeeded_DoublePip_IsHarderThanSinglePip()
    {
        int single = KarstenManabase.SourcesNeeded(60, 24, pips: 1, manaValue: 2);
        int doublePip = KarstenManabase.SourcesNeeded(60, 24, pips: 2, manaValue: 2);
        Assert.True(doublePip > single, $"double-pip {doublePip} should exceed single-pip {single}");
    }

    [Fact]
    public void Analyze_BuffsByHans_FlagsBlueAsWeakestColor()
    {
        ManabaseDeck deck = BuildBuffsByHans();

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        // Snail tool verdict: land count roughly OK, color-limited, weakest color blue.
        Assert.Equal(36, report.ActualLands);
        Assert.InRange(report.TargetLands, 36.0, 38.0);
        Assert.NotNull(report.WeakestColor);
        Assert.Equal(ManaColor.Blue, report.WeakestColor!.Color);
        Assert.False(report.WeakestColor.IsAdequate);
    }

    [Fact]
    public void Analyze_TurnOnePip_CountsOnlyUntappedSources()
    {
        // Green one-drop; two green lands but one enters tapped → only 1 untapped source.
        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 1.0,
            Sources = new List<ManaSource>
            {
                new() { Name = "Forest", Produces = new[] { ManaColor.Green }, EntersUntapped = true },
                new() { Name = "Tapped Dual", Produces = new[] { ManaColor.Green }, EntersUntapped = false },
            },
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Llanowar Elves", ManaValue = 1, Pips = Pip((ManaColor.Green, 1)) },
            },
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);
        ColorSourceFinding green = Assert.Single(report.ColorFindings);

        // Driver is the one-drop, so the tapped land does not count toward its supply.
        Assert.Equal(1.0, green.ActualSources);
    }

    [Fact]
    public void MdfcCountsLowerTheLandTarget()
    {
        double withoutMdfc = KarstenManabase.SingletonLandTarget(100, 1, 3.0, 8);
        double withMdfc = KarstenManabase.SingletonLandTarget(100, 1, 3.0, 8, mdfcCommon: 4);

        // Four common MDFCs shave ~0.74 land each.
        Assert.True(withMdfc < withoutMdfc - 2.5, $"{withMdfc} should be well below {withoutMdfc}");
    }

    [Fact]
    public void Classify_TapsCountsMdfcBackFaces()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Bala Ged Recovery // Bala Ged Sanctuary",
                Quantity = 1,
                ManaCost = "{2}{G}",
                ManaValue = 3,
                TypeLine = "Sorcery // Land",
                OracleText = "Return target permanent card... // Bala Ged Sanctuary enters the battlefield tapped.",
                ProducedMana = new[] { "G" },
                Rarity = "uncommon",
                Layout = "modal_dfc",
                HasLandFace = true,
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.Equal(1, deck.MdfcCommon);
        Assert.Equal(0, deck.MdfcMythic);
        // The land back counts as a partial (0.8) source, not a land slot.
        ManaSource back = Assert.Single(deck.Sources);
        Assert.False(back.IsLand);
        Assert.Equal(0.8, back.Weight, 2);
    }

    /// <summary>
    /// Minimal classified model of the real deck: 36 lands with the actual per-color
    /// supply (G 18 / U 15 / R 15 including tri-lands, duals and fetches), plus the
    /// double-pip spells that strain each color.
    /// </summary>
    private static ManabaseDeck BuildBuffsByHans()
    {
        var sources = new List<ManaSource>();
        void AddLands(string name, int count, params ManaColor[] colors)
        {
            for (int i = 0; i < count; i++)
            {
                sources.Add(new ManaSource { Name = name, Produces = colors });
            }
        }

        // Basics
        AddLands("Forest", 10, ManaColor.Green);
        AddLands("Island", 6, ManaColor.Blue);
        AddLands("Mountain", 6, ManaColor.Red);
        // Tri / any-color
        AddLands("Command Tower", 1, ManaColor.Blue, ManaColor.Red, ManaColor.Green);
        AddLands("Frontier Bivouac", 1, ManaColor.Blue, ManaColor.Red, ManaColor.Green);
        // Duals (Karluk lands, temples, guildgates, bounce)
        AddLands("Simic Growth Chamber", 1, ManaColor.Blue, ManaColor.Green);
        AddLands("Gruul Turf", 1, ManaColor.Red, ManaColor.Green);
        AddLands("Izzet Boilerworks", 1, ManaColor.Blue, ManaColor.Red);
        AddLands("Temple of Epiphany", 1, ManaColor.Blue, ManaColor.Red);
        AddLands("Temple of Abandon", 1, ManaColor.Red, ManaColor.Green);
        AddLands("Temple of Mystery", 1, ManaColor.Blue, ManaColor.Green);
        AddLands("Izzet Guildgate", 1, ManaColor.Blue, ManaColor.Red);
        AddLands("Gruul Guildgate", 1, ManaColor.Red, ManaColor.Green);
        AddLands("Simic Guildgate", 1, ManaColor.Blue, ManaColor.Green);
        AddLands("Kessig Wolf Run", 1, ManaColor.Red);
        // Fetches — count as all three at full weight here (basic fetch in a tri deck).
        AddLands("Evolving Wilds", 1, ManaColor.Blue, ManaColor.Red, ManaColor.Green);
        AddLands("Terramorphic Expanse", 1, ManaColor.Blue, ManaColor.Red, ManaColor.Green);

        var spells = new List<SpellRequirement>
        {
            // Blue double-pip — the strain the tool flagged (Surrakar Spellblade 1UU).
            new() { Name = "Surrakar Spellblade", ManaValue = 3, Pips = Pip((ManaColor.Blue, 2)) },
            new() { Name = "Cold-Eyed Selkie", ManaValue = 3, Pips = Pip((ManaColor.Blue, 2)) },
            // Green double-pip.
            new() { Name = "Ohran Viper", ManaValue = 3, Pips = Pip((ManaColor.Green, 2)) },
            // Red double-pip in a gold cost (Sunder Shaman RRGG).
            new() { Name = "Sunder Shaman", ManaValue = 4, Pips = Pip((ManaColor.Red, 2), (ManaColor.Green, 2)), IsGold = true },
            new() { Name = "Neheb, Dreadhorde Champion", ManaValue = 4, Pips = Pip((ManaColor.Red, 2)) },
        };

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            Sources = sources,
            Spells = spells,
            AverageManaValue = 2.59,
            RampAndDrawUnderThree = 6,
            IsSingleton = true,
        };
    }

    private static IReadOnlyDictionary<ManaColor, int> Pip(params (ManaColor Color, int Count)[] pips)
        => pips.ToDictionary(p => p.Color, p => p.Count);
}
