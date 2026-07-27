using DeckFlow.Core.Research;

namespace DeckFlow.Core.Tests;

public sealed class RoleFloorDivergenceStatsTests
{
    [Theory]
    [InlineData(9.0, 6.0, 1.5)]
    [InlineData(3.0, 6.0, 0.5)]
    [InlineData(5.0, 0.0, 0.0)]
    public void ComputeRatio_ReturnsExpectedValue(double commanderMean, double corpusMean, double expected)
    {
        var ratio = RoleFloorDivergenceStats.ComputeRatio(commanderMean, corpusMean);

        Assert.Equal(expected, ratio);
    }

    [Fact]
    public void ComputeZScore_ReturnsExpectedValue()
    {
        var zScore = RoleFloorDivergenceStats.ComputeZScore(9.0, 6.0, 3.0, 40);

        Assert.InRange(zScore, 6.3236, 6.3256);
    }

    [Fact]
    public void ComputeZScore_ZeroSpreadEqualMeans_ReturnsZero()
    {
        var zScore = RoleFloorDivergenceStats.ComputeZScore(6.0, 6.0 + 1e-10, 0.0, 40);

        Assert.Equal(0.0, zScore);
    }

    [Fact]
    public void ComputeZScore_ZeroSpreadUnequalMeans_ReturnsPositiveInfinity()
    {
        var zScore = RoleFloorDivergenceStats.ComputeZScore(9.0, 6.0, 0.0, 40);

        Assert.Equal(double.PositiveInfinity, zScore);
    }

    [Fact]
    public void ClearsBar_WhenAllThresholdsMet_ReturnsTrue()
    {
        var clearsBar = RoleFloorDivergenceStats.ClearsBar(40, 9.0, 6.0, 3.0, 40, 0.667, 1.5, 2.0);

        Assert.True(clearsBar);
    }

    [Fact]
    public void ClearsBar_WhenDeckCountBelowMinimum_ReturnsFalse()
    {
        var clearsBar = RoleFloorDivergenceStats.ClearsBar(39, 9.0, 6.0, 3.0, 40, 0.667, 1.5, 2.0);

        Assert.False(clearsBar);
    }

    [Fact]
    public void ClearsBar_WhenRatioFallsInsideBand_ReturnsFalse()
    {
        var clearsBar = RoleFloorDivergenceStats.ClearsBar(200, 7.2, 6.0, 3.0, 40, 0.667, 1.5, 2.0);

        Assert.False(clearsBar);
    }

    [Fact]
    public void ClearsBar_WhenCorpusMeanIsZero_ReturnsFalse()
    {
        var clearsBar = RoleFloorDivergenceStats.ClearsBar(200, 6.0, 0.0, 0.0, 40, 0.667, 1.5, 2.0);

        Assert.False(clearsBar);
    }

    [Fact]
    public void ClearsFloorBar_BelowMinimumDeckCount_ReturnsFalse()
    {
        var clearsBar = RoleFloorDivergenceStats.ClearsFloorBar(39, 9.0, 6.0, 9.0, 6.0, 3.0, 40, 0.667, 1.5, 2.0, 2.0);

        Assert.False(clearsBar);
    }

    [Theory]
    [InlineData(9.0, 6.0, 9.0, 6.0)]
    [InlineData(3.0, 6.0, 3.0, 6.0)]
    public void ClearsFloorBar_P25DivergentAndSignificant_ReturnsTrue(
        double commanderP25,
        double corpusP25,
        double commanderMean,
        double corpusMean)
    {
        var clearsBar = RoleFloorDivergenceStats.ClearsFloorBar(40, commanderP25, corpusP25, commanderMean, corpusMean, 3.0, 40, 0.667, 1.5, 2.0, 2.0);

        Assert.True(clearsBar);
    }

    [Fact]
    public void ClearsFloorBar_P25InsideNeutralBand_ReturnsFalseEvenWhenMeanIsWildlyDivergent()
    {
        // Why: this is the assertion that distinguishes the new P25-driven bar from the old
        // mean-driven ClearsBar; under ClearsBar these same inputs return true.
        var clearsBar = RoleFloorDivergenceStats.ClearsFloorBar(200, 6.0, 6.0, 18.0, 6.0, 3.0, 40, 0.667, 1.5, 2.0, 2.0);

        Assert.False(clearsBar);
    }

    [Fact]
    public void ClearsFloorBar_P25DivergentButNotSignificant_ReturnsFalse()
    {
        var clearsBar = RoleFloorDivergenceStats.ClearsFloorBar(40, 9.0, 6.0, 6.4, 6.0, 3.0, 40, 0.667, 1.5, 2.0, 2.0);

        Assert.False(clearsBar);
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(3.0, true)]
    [InlineData(1.0, false)]
    public void ClearsFloorBar_ZeroCorpusP25_UsesAbsoluteGapFallback(double commanderP25, bool expected)
    {
        // Why: ComputeRatio returns 0.0 when the denominator is zero, which would otherwise slide
        // under ratioLow and mark every commander divergent-low.
        var clearsBar = RoleFloorDivergenceStats.ClearsFloorBar(40, commanderP25, 0.0, 9.0, 6.0, 3.0, 40, 0.667, 1.5, 2.0, 2.0);

        Assert.Equal(expected, clearsBar);
    }

    [Fact]
    public void ClearsBar_ExistingMeanDrivenBehavior_IsUnchangedByThisPlan()
    {
        var clearsBar = RoleFloorDivergenceStats.ClearsBar(40, 9.0, 6.0, 3.0, 40, 0.667, 1.5, 2.0);

        Assert.True(clearsBar);
    }

    [Theory]
    [InlineData(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }, 0.25, 2.0)]
    [InlineData(new[] { 1.0, 2.0, 3.0, 4.0 }, 0.25, 1.75)]
    [InlineData(new[] { 7.0 }, 0.25, 7.0)]
    public void ComputePercentile_ReturnsExpectedValue(double[] values, double percentile, double expected)
    {
        var actual = RoleFloorDivergenceStats.ComputePercentile(values, percentile);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ComputePercentile_EmptyValues_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RoleFloorDivergenceStats.ComputePercentile(Array.Empty<double>(), 0.25));
    }

    [Theory]
    [InlineData(9.0, 6.0, 3.0, 1.0)]
    [InlineData(6.0, 6.0 + 1e-10, 0.0, 0.0)]
    public void ComputeCohensD_ReturnsExpectedFiniteValue(double commanderMean, double corpusMean, double corpusStdDev, double expected)
    {
        var actual = RoleFloorDivergenceStats.ComputeCohensD(commanderMean, corpusMean, corpusStdDev);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ComputeCohensD_ZeroSpreadAboveBaseline_ReturnsPositiveInfinity()
    {
        var actual = RoleFloorDivergenceStats.ComputeCohensD(9.0, 6.0, 0.0);

        Assert.Equal(double.PositiveInfinity, actual);
    }

    [Fact]
    public void ComputeCohensD_ZeroSpreadBelowBaseline_ReturnsNegativeInfinity()
    {
        var actual = RoleFloorDivergenceStats.ComputeCohensD(3.0, 6.0, 0.0);

        Assert.Equal(double.NegativeInfinity, actual);
    }
}
