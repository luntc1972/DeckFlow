using System.Globalization;
using DeckFlow.Core.Knowledge.ProfileFusion;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using Xunit;

namespace DeckFlow.Core.Tests.ProfileFusion;

public sealed class StatedRuleRecencyCollapserTests
{
    [Fact]
    public void Collapse_KeepsNewestRuleActiveAndOlderRuleSupersededForSameMetricAndCondition()
    {
        var older = CreateCandidate(
            metric: "draw",
            condition: "archetype:control",
            comparator: "range",
            confidence: 0.45,
            sourceClip: "Run 13 to 18 draw effects.",
            videoDateUtc: "2025-04-01T00:00:00Z");
        var newer = CreateCandidate(
            metric: "draw",
            condition: "archetype:control",
            comparator: "gte",
            confidence: 0.90,
            sourceClip: "I now stay closer to 12 draw spells.",
            videoDateUtc: "2026-05-01T00:00:00Z");

        RecencyCollapseResult result = StatedRuleRecencyCollapser.Collapse([older, newer]);

        StatedRuleCandidate active = Assert.Single(result.Active);
        StatedRuleCandidate superseded = Assert.Single(result.Superseded);
        Assert.Same(newer, active);
        Assert.Same(older, superseded);
    }

    [Fact]
    public void Collapse_PreservesConditionalityByKeepingDifferentConditionsActive()
    {
        var control = CreateCandidate(
            metric: "counter",
            condition: "archetype:control",
            comparator: "gte",
            confidence: 0.80,
            sourceClip: "Blue control wants eight or more counters.",
            videoDateUtc: "2026-01-10T00:00:00Z");
        var splash = CreateCandidate(
            metric: "counter",
            condition: "archetype:splash",
            comparator: "gte",
            confidence: 0.70,
            sourceClip: "Blue splash decks can stay lighter on counters.",
            videoDateUtc: "2026-02-10T00:00:00Z");

        RecencyCollapseResult result = StatedRuleRecencyCollapser.Collapse([control, splash]);

        Assert.Equal(2, result.Active.Count);
        Assert.Empty(result.Superseded);
        Assert.Same(control, result.Active[0]);
        Assert.Same(splash, result.Active[1]);
    }

    [Fact]
    public void Collapse_RetainsSupersededRuleDataForLedgerHistory()
    {
        var older = CreateCandidate(
            metric: "board-wipe",
            condition: "meta:casual",
            comparator: "lte",
            confidence: 0.61,
            sourceClip: "Reconsider your fourth and fifth wipe.",
            videoDateUtc: "2025-03-15T00:00:00Z",
            value: 5,
            valueMin: 3,
            valueMax: 5,
            clipTimestampSeconds: 91);
        var newer = CreateCandidate(
            metric: "board-wipe",
            condition: "meta:casual",
            comparator: "lte",
            confidence: 0.62,
            sourceClip: "Three to five wipes is still my ceiling.",
            videoDateUtc: "2026-03-15T00:00:00Z",
            value: 5,
            valueMin: 3,
            valueMax: 5,
            clipTimestampSeconds: 15);

        RecencyCollapseResult result = StatedRuleRecencyCollapser.Collapse([older, newer]);

        StatedRuleCandidate superseded = Assert.Single(result.Superseded);
        Assert.Equal(5, superseded.Value);
        Assert.Equal(3, superseded.ValueMin);
        Assert.Equal(5, superseded.ValueMax);
        Assert.Equal(91, superseded.ClipTimestampSeconds);
        Assert.Equal("Reconsider your fourth and fifth wipe.", superseded.SourceClip);
    }

    [Fact]
    public void Collapse_ReturnsEmptyListsForEmptyInput()
    {
        RecencyCollapseResult result = StatedRuleRecencyCollapser.Collapse([]);

        Assert.Empty(result.Active);
        Assert.Empty(result.Superseded);
    }

    [Fact]
    public void Collapse_ReturnsDeterministicOrderingByFirstSeenBucket()
    {
        var drawOlder = CreateCandidate(
            metric: "draw",
            condition: "archetype:control",
            comparator: "range",
            confidence: 0.40,
            sourceClip: "Thirteen to eighteen draw.",
            videoDateUtc: "2025-01-01T00:00:00Z");
        var ramp = CreateCandidate(
            metric: "ramp",
            condition: null,
            comparator: "range",
            confidence: 0.75,
            sourceClip: "Seven to twelve ramp.",
            videoDateUtc: "2026-01-01T00:00:00Z");
        var drawNewer = CreateCandidate(
            metric: "draw",
            condition: "archetype:control",
            comparator: "gte",
            confidence: 0.90,
            sourceClip: "Twelve draw is enough now.",
            videoDateUtc: "2026-06-01T00:00:00Z");

        RecencyCollapseResult result = StatedRuleRecencyCollapser.Collapse([drawOlder, ramp, drawNewer]);

        Assert.Equal(2, result.Active.Count);
        Assert.Same(drawNewer, result.Active[0]);
        Assert.Same(ramp, result.Active[1]);
        Assert.Same(drawOlder, Assert.Single(result.Superseded));
    }

    private static StatedRuleCandidate CreateCandidate(
        string metric,
        string? condition,
        string comparator,
        double confidence,
        string sourceClip,
        string videoDateUtc,
        double? value = 10,
        double? valueMin = null,
        double? valueMax = null,
        int? clipTimestampSeconds = 42)
    {
        return new StatedRuleCandidate
        {
            Category = "deckbuilding",
            Metric = metric,
            Value = value,
            ValueMin = valueMin,
            ValueMax = valueMax,
            Comparator = comparator,
            Condition = condition,
            ClipTimestampSeconds = clipTimestampSeconds,
            SourceClip = sourceClip,
            Confidence = confidence,
            CardReference = null,
            CardGrounded = null,
            VideoDateUtc = DateTimeOffset.Parse(videoDateUtc, CultureInfo.InvariantCulture),
        };
    }
}
