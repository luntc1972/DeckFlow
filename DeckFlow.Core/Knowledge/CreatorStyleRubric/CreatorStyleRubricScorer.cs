using DeckFlow.Core.Knowledge.ProfileFusion;

namespace DeckFlow.Core.Knowledge.CreatorStyleRubric;

/// <summary>
/// Scores a submitted deck against a creator's fused numeric targets.
/// </summary>
public static class CreatorStyleRubricScorer
{
    /// <summary>
    /// Scores the submitted deck against the supplied creator targets.
    /// </summary>
    /// <param name="creatorSlug">Creator slug associated with the target profile.</param>
    /// <param name="creatorTargets">Fused creator targets to compare.</param>
    /// <param name="submittedStats">Measured submitted-deck statistics keyed by measured metric names.</param>
    /// <returns>A deterministic set of per-metric rubric scores.</returns>
    public static RubricScoreResult Score(
        string creatorSlug,
        IReadOnlyList<FusedTarget> creatorTargets,
        SubmittedDeckStats submittedStats)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creatorSlug);
        ArgumentNullException.ThrowIfNull(creatorTargets);
        ArgumentNullException.ThrowIfNull(submittedStats);

        // Why: FusedTarget.Metric carries the STATED vocabulary, so it must be resolved through
        // StatedMetricKeyMapper.TryMapToMeasuredKey before joining the MEASURED submittedStats keys.
        var submittedMetrics = new Dictionary<string, double>(submittedStats.Metrics, StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<RubricMetricScore> metricScores = creatorTargets
            .Select(target => CreateScore(target, submittedMetrics))
            .OrderBy(score => score.Metric, StringComparer.Ordinal)
            .ToArray();

        return new RubricScoreResult
        {
            CreatorSlug = creatorSlug,
            MetricScores = metricScores,
        };
    }

    private static RubricMetricScore CreateScore(
        FusedTarget target,
        IReadOnlyDictionary<string, double> submittedMetrics)
    {
        if (StatedMetricKeyMapper.TryMapToMeasuredKey(target.Metric, out string measuredKey) &&
            submittedMetrics.TryGetValue(measuredKey, out double submittedValue))
        {
            double delta = submittedValue - target.Value;

            return new RubricMetricScore
            {
                Metric = measuredKey,
                TargetValue = target.Value,
                SubmittedValue = submittedValue,
                Delta = delta,
                Weight = target.Weight,
                Verdict = GetVerdict(delta),
                Confidence = target.Confidence,
            };
        }

        return new RubricMetricScore
        {
            Metric = target.Metric,
            TargetValue = target.Value,
            SubmittedValue = null,
            Delta = null,
            Weight = target.Weight,
            Verdict = "insufficient-measured",
            Confidence = target.Confidence,
        };
    }

    private static string GetVerdict(double delta)
    {
        if (delta == 0)
        {
            return "on-target";
        }

        return delta < 0 ? "under" : "over";
    }
}
