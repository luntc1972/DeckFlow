using DeckFlow.Core.Manabase;
using Xunit;

namespace DeckFlow.Core.Tests.Manabase;

public sealed class CedhLandTargetHybridTests
{
    [Fact]
    public void DisabledContext_MatchesHistoricFiveArgOverload()
    {
        double legacy = KarstenManabase.CedhLandTarget(100, 1, 1.5, 14);
        double explicitDisabled = KarstenManabase.CedhLandTarget(
            100, 1, 1.5, 14, 0, CedhLandContext.Disabled);

        Assert.Equal(Math.Max(28.0, KarstenManabase.SingletonLandTarget(100, 1, 1.5, 14) - 3.5), legacy, 6);
        Assert.Equal(legacy, explicitDisabled, 6);
    }

    [Fact]
    public void EnabledContext_NoBaseline_UsesCurveTargetWithSafetyFloor()
    {
        double target = KarstenManabase.CedhLandTarget(
            100, 1, 1.0, 11, 0, new CedhLandContext(null, 0, Enabled: true));

        Assert.Equal(27.974, target, 3);
        Assert.True(target > 22.0);
        Assert.True(target < 28.0);
    }

    [Fact]
    public void EnabledContext_BaselineNudgesTowardMean()
    {
        double target = KarstenManabase.CedhLandTarget(
            100, 1, 1.1, 12, 0, new CedhLandContext(25.0, 10, Enabled: true));

        Assert.Equal(26.50375, target, 3);
    }

    [Fact]
    public void EnabledContext_BaselineWithTooSmallSample_IsIgnored()
    {
        double target = KarstenManabase.CedhLandTarget(
            100, 1, 1.1, 12, 0, new CedhLandContext(25.0, 9, Enabled: true));

        Assert.Equal(28.0075, target, 4);
    }

    [Fact]
    public void EnabledContext_SafetyFloor_ClampsDegenerateLowCurve()
    {
        double target = KarstenManabase.CedhLandTarget(
            100, 1, 0.0, 24, 0, new CedhLandContext(null, 0, Enabled: true));

        Assert.Equal(22.0, target, 6);
    }

    [Fact]
    public void EnabledContext_HighBaseline_IsRaisedButClampedToCeiling()
    {
        double target = KarstenManabase.CedhLandTarget(
            100, 1, 4.9, 0, 0, new CedhLandContext(46.8, 18, Enabled: true));

        Assert.Equal(45.0, target, 6);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(999.0)]
    public void EnabledContext_CorruptBaselineMean_FallsBackToCurveTarget(double mean)
    {
        double target = KarstenManabase.CedhLandTarget(
            100, 1, 1.5, 14, 0, new CedhLandContext(mean, 18, Enabled: true));

        Assert.Equal(28.7015, target, 4);
    }
}
