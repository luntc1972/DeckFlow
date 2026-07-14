using DeckFlow.Core.Knowledge.ProfileFusion;
using Xunit;

namespace DeckFlow.Core.Tests.ProfileFusion;

public sealed class MetricClassificationTests
{
    public static TheoryData<string> ObservableMetrics =>
        new()
        {
            "ramp",
            "removal",
            "draw",
            "finishers",
            "win-cons",
            "counter",
            "protection",
            "board-wipe",
            "tutor",
            "recursion",
            "utility",
            "karsten:target_lands",
            "karsten:land_delta",
            "karsten:health_score",
            "combo_density:included_per_deck",
            "land_count",
        };

    public static TheoryData<string> PhilosophyMetrics =>
        new()
        {
            "interaction",
            "opener_probability",
            "pip_distribution",
            "power_level_philosophy",
        };

    [Theory]
    [MemberData(nameof(ObservableMetrics))]
    public void Classify_ReturnsObservableForEachMeasuredOrDerivedMetric(string statedMetric)
    {
        Assert.Equal(MetricKind.Observable, MetricClassification.Classify(statedMetric));
    }

    [Theory]
    [MemberData(nameof(PhilosophyMetrics))]
    public void Classify_ReturnsPhilosophyForEachStatedOnlyMetric(string statedMetric)
    {
        Assert.Equal(MetricKind.Philosophy, MetricClassification.Classify(statedMetric));
    }

    [Theory]
    [MemberData(nameof(ObservableMetrics))]
    [MemberData(nameof(PhilosophyMetrics))]
    public void Classify_RemainsConsistentWithMapperResult(string statedMetric)
    {
        MetricKind expected = StatedMetricKeyMapper.GetMapKind(statedMetric) == StatedMetricMapKind.StatedOnly
            ? MetricKind.Philosophy
            : MetricKind.Observable;

        Assert.Equal(expected, MetricClassification.Classify(statedMetric));
    }
}
