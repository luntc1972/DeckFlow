using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Covers <see cref="ManabaseAnalyzer"/> report-aggregate surfaces the primary suite does not
/// assert directly: the <c>ColorSpellCounts</c> denominator, the <c>CommanderColors</c> identity
/// union, the colorless-spell mana-only limiting factor end-to-end, the empty-deck verdict, and
/// the strict orthogonality of <see cref="CommanderImportance"/> to the land target (Central, Low,
/// and Standard all produce an identical target for the same deck).
/// </summary>
public sealed class ManabaseAnalyzerCoverageTests
{
    [Fact]
    public void Analyze_ColorSpellCounts_CountEachColorsDemandingSpells()
    {
        // Two blue spells, one red spell → ColorSpellCounts {Blue:2, Red:1}.
        var sources = new List<ManaSource>();
        for (int i = 0; i < 18; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        for (int i = 0; i < 18; i++)
        {
            sources.Add(new ManaSource { Name = "Mountain", Produces = new[] { ManaColor.Red } });
        }

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Blue One", ManaValue = 2, Pips = Pip((ManaColor.Blue, 1)) },
                new() { Name = "Blue Two", ManaValue = 3, Pips = Pip((ManaColor.Blue, 1)) },
                new() { Name = "Red One", ManaValue = 2, Pips = Pip((ManaColor.Red, 1)) },
            },
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.Equal(2, report.ColorSpellCounts[ManaColor.Blue]);
        Assert.Equal(1, report.ColorSpellCounts[ManaColor.Red]);
    }

    [Fact]
    public void Analyze_CommanderColors_UnionAcrossCommanders()
    {
        // A partner pair (WU + BR) yields a four-color commander identity in CommanderColors.
        var sources = new List<ManaSource>();
        foreach (ManaColor c in new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red })
        {
            for (int i = 0; i < 9; i++)
            {
                sources.Add(new ManaSource { Name = c.ToString(), Produces = new[] { c } });
            }
        }

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 2,
            AverageManaValue = 2.5,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Tymna", ManaValue = 2, Pips = Pip((ManaColor.White, 1)), IsCommander = true },
                new() { Name = "Kraum", ManaValue = 4, Pips = Pip((ManaColor.Blue, 1), (ManaColor.Black, 1), (ManaColor.Red, 1)), IsGold = true, IsCommander = true },
            },
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.Contains(ManaColor.White, report.CommanderColors);
        Assert.Contains(ManaColor.Blue, report.CommanderColors);
        Assert.Contains(ManaColor.Black, report.CommanderColors);
        Assert.Contains(ManaColor.Red, report.CommanderColors);
        Assert.DoesNotContain(ManaColor.Green, report.CommanderColors);
    }

    [Fact]
    public void Analyze_ColorlessPayoff_RowLimitingFactorIsMana()
    {
        // A colorless payoff produces a castability row whose limiting factor is purely "mana".
        var sources = new List<ManaSource>();
        for (int i = 0; i < 36; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 4.0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Ugin", ManaValue = 6, Pips = new Dictionary<ManaColor, int>() },
            },
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        CardCastability ugin = Assert.Single(report.Castability);
        Assert.Equal("mana", ugin.LimitingFactor);
    }

    [Fact]
    public void Analyze_Importance_IsOrthogonalToLandTarget_AcrossAllThreeLevels()
    {
        // Central / Standard / Low must all produce the SAME land target for one deck; importance
        // only moves the color verdict, never the regression-driven target or its breakdown.
        var deck = BuildWuDeck();

        ManabaseReport central = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Central);
        ManabaseReport standard = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard);
        ManabaseReport low = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Low);

        Assert.Equal(central.TargetLands, standard.TargetLands);
        Assert.Equal(standard.TargetLands, low.TargetLands);
        Assert.Equal(central.LandTarget!.FinalTarget, low.LandTarget!.FinalTarget);
    }

    [Fact]
    public void Analyze_LowImportance_DoesNotForceCommanderAsWorstDriver()
    {
        // With Low importance the commander is treated as a normal spell: a well-supported commander
        // color must not be promoted over a genuinely worse non-commander color.
        var deck = BuildWuDeck();

        ManabaseReport low = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Low);

        // The black off-commander bomb is the real weak point regardless of the WU commander.
        if (low.WeakestColor is not null)
        {
            Assert.Equal(ManaColor.Black, low.WeakestColor.Color);
        }
    }

    [Fact]
    public void Analyze_EmptyDeck_HasNoFindings_AndAdequateColorSummary()
    {
        // A deck with no colored spells yields no findings, no weakest color, and a healthy verdict.
        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 0,
            AverageManaValue = 0,
            Sources = new List<ManaSource>
            {
                new() { Name = "Wastes", Produces = System.Array.Empty<ManaColor>() },
            },
            Spells = new List<SpellRequirement>(),
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.Empty(report.ColorFindings);
        Assert.Null(report.WeakestColor);
        Assert.Contains("every color adequately supported", report.Summary);
    }

    private static ManabaseDeck BuildWuDeck()
    {
        // WU commander very well supported; only 3 black sources for an off-commander BB bomb so
        // black is the true worst color irrespective of commander importance.
        var sources = new List<ManaSource>();
        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }

        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        for (int i = 0; i < 3; i++)
        {
            sources.Add(new ManaSource { Name = "Swamp", Produces = new[] { ManaColor.Black } });
        }

        var spells = new List<SpellRequirement>
        {
            new() { Name = "Brago", ManaValue = 4, Pips = Pip((ManaColor.White, 1), (ManaColor.Blue, 1)), IsGold = true, IsCommander = true },
            new() { Name = "Black Bomb", ManaValue = 4, Pips = Pip((ManaColor.Black, 2)) },
        };

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 3.0,
            Sources = sources,
            Spells = spells,
        };
    }

    private static IReadOnlyDictionary<ManaColor, int> Pip(params (ManaColor Color, int Count)[] pips)
        => pips.ToDictionary(p => p.Color, p => p.Count);
}
