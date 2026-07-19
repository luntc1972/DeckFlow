using DeckFlow.Web.Models.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Guards the shared Cut Lab metric contract against shape drift.</summary>
public sealed class CutLabMetricsContractTests
{
    /// <summary>Ensures the seven metric families stay aligned with the UI contract.</summary>
    [Fact]
    public void MetricFamilies_MatchTheSevenFamilyContract()
    {
        CutLabMetricFamily[] families = Enum.GetValues<CutLabMetricFamily>();

        Assert.Equal(7, families.Length);
        Assert.Equal(
            [
                CutLabMetricFamily.CommanderOnTime,
                CutLabMetricFamily.KeepableHand,
                CutLabMetricFamily.ManaColorReliability,
                CutLabMetricFamily.EarlyInteraction,
                CutLabMetricFamily.PlanPresence,
                CutLabMetricFamily.CategoryByTurn,
                CutLabMetricFamily.FloodScrewCurveRisk,
            ],
            families);
    }

    /// <summary>Ensures flood, screw, and curve remain separate rendered lines.</summary>
    [Fact]
    public void MetricKinds_KeepFloodScrewAndCurveDistinct()
    {
        CutLabMetricKind[] kinds = Enum.GetValues<CutLabMetricKind>();

        Assert.Contains(CutLabMetricKind.Flood, kinds);
        Assert.Contains(CutLabMetricKind.Screw, kinds);
        Assert.Contains(CutLabMetricKind.Curve, kinds);
    }

    /// <summary>Ensures the named noise-floor constants stay exposed and stable.</summary>
    [Fact]
    public void NoiseFloor_ExposesNamedConstants()
    {
        Assert.Equal(1.5, CutLabNoiseFloor.PercentPoints);
        Assert.Equal(1, CutLabNoiseFloor.Cards);
    }
}
