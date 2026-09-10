using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.ProfileFusion;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using Xunit;

namespace DeckFlow.Core.Tests.ProfileFusion;

public sealed class CreatorStatedRulesSeedFusionTests
{
    [Fact]
    public void ReadCommittedSeed_ContainsOnlyValidHandAuthoredRules()
    {
        IReadOnlyList<StatedRuleCandidate> rules = ReadSeed();

        Assert.Equal(10, rules.Count);
        Assert.All(rules, static rule => Assert.Equal("hand-authored", rule.Provenance));
        Assert.All(rules, static rule => Assert.Contains(rule.Metric, StatedRulesMetricVocabulary.Metrics));
        Assert.All(rules, static rule => Assert.Contains(rule.Comparator, StatedRulesMetricVocabulary.Comparators));
        Assert.All(rules, static rule => Assert.Null(rule.ClipTimestampSeconds));
    }

    [Fact]
    public void Fuse_CommittedSeed_ReproducesP90Ledger()
    {
        MeasuredMetric[] measured =
        [
            CreateMeasuredMetric("category_ratio:ramp", 12.0, effectiveSampleSize: 10.5),
            CreateMeasuredMetric("category_ratio:draw", 11.1, effectiveSampleSize: 10.5),
            CreateMeasuredMetric("category_ratio:board-wipe", 1.2, effectiveSampleSize: 10.5),
            CreateMeasuredMetric("category_ratio:counter", 12.0, effectiveSampleSize: 9.5),
            CreateMeasuredMetric("karsten:target_lands", 37.0, effectiveSampleSize: 10.0),
            CreateMeasuredMetric("karsten:land_delta", 0.4, effectiveSampleSize: 10.0),
        ];

        IReadOnlyList<FusedTarget> result = ProfileFusionEngine.Fuse(measured, ReadSeed());

        Assert.Equal(10, result.Count);

        FusedTarget land = GetTarget(result, "land_count", condition: null);
        Assert.Equal("agree", land.Verdict);
        Assert.Equal(37.4, land.Value, 6);

        Assert.Equal("agree", GetTarget(result, "ramp", condition: null).Verdict);

        FusedTarget draw = GetTarget(result, "draw", condition: null);
        Assert.Equal("conflict", draw.Verdict);
        Assert.NotNull(draw.Conflict);

        FusedTarget boardWipe = GetTarget(result, "board-wipe", condition: null);
        // ROADMAP success criterion 3: agreement-not-hypocrisy depends on the upper-bound seed encoding.
        Assert.Equal("agree", boardWipe.Verdict);
        Assert.Null(boardWipe.Conflict);

        AssertInsufficientConditionBreakdown(GetTarget(result, "counter", "archetype:control"));
        AssertInsufficientConditionBreakdown(GetTarget(result, "tutor", "bracket:2"));
        AssertInsufficientConditionBreakdown(GetTarget(result, "land_count", "curve:low-aggressive-mulligan"));

        Assert.Equal("philosophy-stated-only", GetTarget(result, "interaction", "archetype:proactive").Verdict);
        Assert.Equal("philosophy-stated-only", GetTarget(result, "opener_probability", condition: null).Verdict);

        FusedTarget removal = GetTarget(result, "removal", condition: null);
        Assert.Equal("insufficient-measured", removal.Verdict);
        Assert.Null(removal.VerdictReason);
        Assert.Equal("stated", removal.Source);
    }

    private static void AssertInsufficientConditionBreakdown(FusedTarget target)
    {
        Assert.Equal("insufficient-measured", target.Verdict);
        Assert.Equal("no-condition-breakdown", target.VerdictReason);
    }

    private static MeasuredMetric CreateMeasuredMetric(string metric, double value, double effectiveSampleSize)
    {
        return new MeasuredMetric
        {
            Metric = metric,
            Value = value,
            NumDecks = 39,
            Distribution = new MetricDistribution
            {
                Mean = value,
                Min = value,
                Max = value,
                StdDev = 0.1,
                EffectiveSampleSize = effectiveSampleSize,
            },
        };
    }

    private static FusedTarget GetTarget(IReadOnlyList<FusedTarget> targets, string metric, string? condition)
    {
        return Assert.Single(targets, target => target.Metric == metric && target.Condition == condition);
    }

    private static IReadOnlyList<StatedRuleCandidate> ReadSeed()
    {
        string solutionRoot = FindSolutionRoot();
        string seedPath = Path.Combine(solutionRoot, ContentKbPaths.CreatorStatedRulesSeedRelativePath);
        string json = File.ReadAllText(seedPath);
        Dictionary<string, List<StatedRuleCandidate>>? seeds = JsonSerializer.Deserialize<Dictionary<string, List<StatedRuleCandidate>>>(
            json,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            });

        Assert.NotNull(seeds);
        return Assert.IsType<List<StatedRuleCandidate>>(seeds["salubrioussnail"]);
    }

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DeckFlow.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the DeckFlow.sln solution root from the test base directory.");
    }
}
