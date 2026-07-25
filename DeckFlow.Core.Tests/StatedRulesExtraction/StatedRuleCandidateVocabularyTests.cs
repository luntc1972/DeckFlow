using System.Linq;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using Xunit;

namespace DeckFlow.Core.Tests.StatedRulesExtraction;

public sealed class StatedRuleCandidateVocabularyTests
{
    [Fact]
    public void Metrics_ContainLockedClosedVocabulary()
    {
        Assert.Equal(20, StatedRulesMetricVocabulary.Metrics.Count);

        foreach (string category in ContentTagVocabulary.CardCategories)
        {
            Assert.Contains(category, StatedRulesMetricVocabulary.Metrics);
        }

        Assert.DoesNotContain(
            StatedRulesMetricVocabulary.Metrics,
            metric => metric.StartsWith("lift:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Comparators_MatchLockedVocabulary()
    {
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "gte",
            "lte",
            "eq",
            "range",
        };

        Assert.True(expected.SetEquals(StatedRulesMetricVocabulary.Comparators));
    }
}
