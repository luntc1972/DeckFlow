using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CastabilitySimulatorCurveCoverageTests
{
    private const int Trials = 2000;

    [Fact]
    public void LowCurveDeck_HasHigherCurveCoverage_ThanTopHeavyDeck()
    {
        ManabaseDeck lowCurve = Deck(
            lands: 38,
            spells: Enumerable.Range(0, 30).Select(i => Spell($"Cheap {i}", i % 2 == 0 ? 1 : 2)));
        ManabaseDeck topHeavy = Deck(
            lands: 38,
            spells: Enumerable.Range(0, 30).Select(i => Spell($"Expensive {i}", 6)));

        double lowCurveCoverage = CastabilitySimulator.SimulateCurveCoverage(lowCurve, lowCurve.TotalCards, Trials);
        double topHeavyCoverage = CastabilitySimulator.SimulateCurveCoverage(topHeavy, topHeavy.TotalCards, Trials);

        Assert.InRange(lowCurveCoverage, 0.0, 5.0);
        Assert.InRange(topHeavyCoverage, 0.0, 5.0);
        Assert.True(
            lowCurveCoverage > topHeavyCoverage,
            $"expected low-curve coverage {lowCurveCoverage:F2} to beat top-heavy coverage {topHeavyCoverage:F2}");
    }

    [Fact]
    public void CurveCoverage_FlagOff_IsZero()
    {
        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(
            Deck(lands: 38, spells: Enumerable.Range(0, 10).Select(i => Spell($"Cheap {i}", 1))),
            new[] { Row("Cheap", 1, 1) },
            defaultTrials: Trials,
            keepShapes: false);

        Assert.Equal(0.0, result.CurveCoverageTurns);
    }

    private static ManabaseDeck Deck(int lands, IEnumerable<SpellRequirement> spells)
    {
        List<SpellRequirement> spellList = spells.ToList();
        var sources = new List<ManaSource>();
        for (int i = 0; i < lands; i++)
        {
            sources.Add(new ManaSource
            {
                Name = $"Forest {i}",
                Produces = new[] { ManaColor.Green },
                IsLand = true,
                EntersUntapped = true,
            });
        }

        return new ManabaseDeck
        {
            TotalCards = lands + spellList.Count,
            CommanderCount = 0,
            AverageManaValue = spellList.Count > 0 ? spellList.Average(s => s.ManaValue) : 0.0,
            Sources = sources,
            Spells = spellList,
            IsSingleton = true,
        };
    }

    private static SpellRequirement Spell(string name, int manaValue) => new()
    {
        Name = name,
        ManaValue = manaValue,
        Pips = new Dictionary<ManaColor, int> { [ManaColor.Green] = 1 },
    };

    private static CardCastability Row(string name, int manaValue, int onCurveTurn) => new()
    {
        Name = name,
        ManaValue = manaValue,
        OnCurveTurn = onCurveTurn,
        CastPercent = 90,
        LimitingFactor = "mana",
        KeepableTrials = 800,
        Kept7Trials = 600,
        MulliganTo6Trials = 200,
    };
}
