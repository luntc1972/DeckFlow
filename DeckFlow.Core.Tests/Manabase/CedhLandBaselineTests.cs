using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>Tests the pure cEDH gate and land-baseline rollup helper.</summary>
public sealed class CedhLandBaselineTests
{
    [Theory]
    [InlineData(94, 2.7, false)]
    [InlineData(95, 2.7, true)]
    [InlineData(101, 2.7, true)]
    [InlineData(102, 2.7, false)]
    [InlineData(100, 2.71, false)]
    public void PassesCedhGate_UsesExpectedBoundaries(int cardCount, double avgManaValue, bool expected)
    {
        Assert.Equal(expected, CedhLandBaseline.PassesCedhGate(cardCount, avgManaValue));
    }

    [Fact]
    public void Build_FiltersSamplesAndComputesCommanderAndOverallStats()
    {
        var samples = new List<CedhDeckSample>
        {
            new("Kinnan, Bonder Prodigy", "16-32 winner", 25, 2.0, 100),
            new("Kinnan, Bonder Prodigy", "16-32 winner", 26, 2.1, 99),
            new("Kinnan, Bonder Prodigy", "16-32 winner", 27, 2.2, 101),
            new("Tivit, Seller of Secrets", "64+ top16", 30, 2.4, 100),
            new("Tivit, Seller of Secrets", "64+ top16", 31, 2.5, 100),
            new("Blue Farm", "33-63 top4", 28, 3.0, 100),
            new("Winota, Joiner of Forces", "33-63 top4", 29, 2.0, 90),
        };

        CedhLandBaselineResult result = CedhLandBaseline.Build(samples, "2026-07");

        Assert.Equal("2026-07", result.Month);
        Assert.Equal(7, result.RawSampleSize);
        Assert.Equal(5, result.SampleSize);
        Assert.Equal(1, result.DroppedForCurve);
        Assert.Equal(1, result.DroppedForIncomplete);

        Assert.Equal(27.8, result.Overall.MeanLands, 1);
        Assert.Equal(25, result.Overall.MinLands);
        Assert.Equal(31, result.Overall.MaxLands);

        CedhLandStats kinnan = Assert.Contains("Kinnan, Bonder Prodigy", result.Commanders);
        Assert.Equal(3, kinnan.SampleSize);
        Assert.Equal(26.0, kinnan.MeanLands, 3);
        Assert.Equal(1.0, kinnan.StandardDeviation, 3);
        Assert.DoesNotContain("Tivit, Seller of Secrets", result.Commanders.Keys);

        CedhLandTierStat winnerTier = Assert.Single(result.Tiers, t => t.Tier == "16-32 winner");
        Assert.Equal(3, winnerTier.SampleSize);
        Assert.Equal(26.0, winnerTier.MeanLands, 3);

        CedhLandHistogramEntry lands26 = Assert.Single(result.Histogram, h => h.Lands == 26);
        Assert.Equal(1, lands26.Count);
    }

    [Fact]
    public void ToSnapshot_RoundsAndKeepsOnlyNAtLeastThreeCommanders()
    {
        var samples = new[]
        {
            new CedhDeckSample("Kinnan, Bonder Prodigy", "16-32 winner", 25, 2.0, 100),
            new CedhDeckSample("Kinnan, Bonder Prodigy", "16-32 winner", 26, 2.0, 100),
            new CedhDeckSample("Kinnan, Bonder Prodigy", "16-32 winner", 28, 2.0, 100),
            new CedhDeckSample("Tivit, Seller of Secrets", "64+ top16", 30, 2.0, 100),
            new CedhDeckSample("Tivit, Seller of Secrets", "64+ top16", 31, 2.0, 100),
        };

        CedhLandBaselineSnapshot snapshot = CedhLandBaseline.ToSnapshot(
            CedhLandBaseline.Build(samples, "2026-07"));

        Assert.Equal("2026-07", snapshot.Generated);
        Assert.Equal(5, snapshot.SampleSize);
        Assert.Equal(28.0, snapshot.OverallMeanLands, 1);

        CedhCommanderBaselineSnapshot kinnan = Assert.Contains("Kinnan, Bonder Prodigy", snapshot.Commanders);
        Assert.Equal(3, kinnan.N);
        Assert.Equal(26.3, kinnan.LandsMean, 1);
        Assert.Equal(1.5, kinnan.LandsSd, 1);
        Assert.DoesNotContain("Tivit, Seller of Secrets", snapshot.Commanders.Keys);
    }
}
