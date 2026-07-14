using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>Pin tests for the cEDH mulligan keep calibration constants.</summary>
public sealed class CedhMulliganCalibrationTests
{
    [Fact]
    public void Constants_MatchCalibratedDefaults()
    {
        Assert.Equal(3, CedhMulliganCalibration.TurnCapExplosive);
        Assert.Equal(2, CedhMulliganCalibration.TurnCapEngine);
        Assert.Equal(4, CedhMulliganCalibration.RepresentativeLineTurnCap);
        Assert.Equal(2, CedhMulliganCalibration.BridgeInteractionMin);
        Assert.Equal(2, CedhMulliganCalibration.BridgeDevelopmentMin);
    }

    [Fact]
    public void Constants_SatisfyOrderingInvariant()
    {
        Assert.True(CedhMulliganCalibration.TurnCapEngine < CedhMulliganCalibration.TurnCapExplosive);
        Assert.True(CedhMulliganCalibration.TurnCapExplosive <= CedhMulliganCalibration.RepresentativeLineTurnCap);
        Assert.True(CedhMulliganCalibration.BridgeInteractionMin >= 1);
        Assert.True(CedhMulliganCalibration.BridgeDevelopmentMin >= 1);
    }
}
