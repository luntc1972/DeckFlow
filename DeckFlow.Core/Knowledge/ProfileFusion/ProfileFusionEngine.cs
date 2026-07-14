using DeckFlow.Core.Knowledge.StatedRulesExtraction;

namespace DeckFlow.Core.Knowledge.ProfileFusion;

/// <summary>
/// Composes recency collapse, metric mapping, classification, and conflict evaluation into a fused ledger.
/// </summary>
public static class ProfileFusionEngine
{
    private const string MeasuredSource = "measured-weighted";
    private const string StatedSource = "stated";
    private const string SupersededSource = "stated-superseded";

    /// <summary>
    /// Fuses measured creator metrics with stated creator rules into deterministic ledger rows.
    /// </summary>
    /// <param name="measured">Measured metrics for a creator profile.</param>
    /// <param name="statedRules">Stated rules extracted from creator content.</param>
    /// <returns>Deterministically ordered fused targets.</returns>
    public static IReadOnlyList<FusedTarget> Fuse(
        IReadOnlyList<MeasuredMetric> measured,
        IReadOnlyList<StatedRuleCandidate> statedRules)
    {
        ArgumentNullException.ThrowIfNull(measured);
        ArgumentNullException.ThrowIfNull(statedRules);

        if (statedRules.Count == 0)
        {
            return [];
        }

        RecencyCollapseResult collapse = StatedRuleRecencyCollapser.Collapse(statedRules);
        var measuredByMetric = measured.ToDictionary(static item => item.Metric, StringComparer.OrdinalIgnoreCase);
        var fused = new List<FusedTarget>(collapse.Active.Count + collapse.Superseded.Count);

        foreach (StatedRuleCandidate rule in collapse.Active.OrderBy(static rule => rule.Metric, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(static rule => rule.Condition ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            fused.Add(FuseActiveRule(rule, measuredByMetric));
        }

        foreach (StatedRuleCandidate rule in collapse.Superseded.OrderBy(static rule => rule.Metric, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(static rule => rule.Condition ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                     .ThenByDescending(static rule => rule.VideoDateUtc))
        {
            fused.Add(CreateSupersededHistory(rule));
        }

        return fused;
    }

    private static FusedTarget FuseActiveRule(
        StatedRuleCandidate rule,
        IReadOnlyDictionary<string, MeasuredMetric> measuredByMetric)
    {
        if (MetricClassification.Classify(rule.Metric) == MetricKind.Philosophy)
        {
            return CreateStatedTarget(rule, verdict: "philosophy-stated-only", source: StatedSource);
        }

        MeasuredResolution? measured = ResolveMeasured(rule, measuredByMetric);
        double statedValue = GetRepresentativeStatedValue(rule);
        (double? statedMin, double? statedMax) = GetStatedBand(rule);

        if (!string.IsNullOrWhiteSpace(rule.Condition))
        {
            return new FusedTarget
            {
                Metric = rule.Metric,
                Condition = rule.Condition,
                Value = measured?.Value ?? statedValue,
                Weight = 1.0,
                Source = MeasuredSource,
                StatedMin = statedMin,
                StatedMax = statedMax,
                MeasuredValue = measured?.Value,
                NumDecks = measured?.NumDecks,
                EffectiveSampleSize = measured?.EffectiveSampleSize,
                Verdict = "insufficient-measured",
                VerdictReason = "no-condition-breakdown",
                SourceClip = rule.SourceClip,
                VideoDateUtc = rule.VideoDateUtc,
                Confidence = ToConfidenceBand(rule.Confidence),
                Conflict = null,
            };
        }

        if (measured is null)
        {
            return new FusedTarget
            {
                Metric = rule.Metric,
                Condition = rule.Condition,
                Value = statedValue,
                Weight = 1.0,
                Source = MeasuredSource,
                StatedMin = statedMin,
                StatedMax = statedMax,
                MeasuredValue = null,
                NumDecks = null,
                EffectiveSampleSize = null,
                Verdict = "insufficient-measured",
                VerdictReason = null,
                SourceClip = rule.SourceClip,
                VideoDateUtc = rule.VideoDateUtc,
                Confidence = ToConfidenceBand(rule.Confidence),
                Conflict = null,
            };
        }

        MeasuredResolution matched = measured.Value;

        if (!IsSupportedComparator(rule.Comparator))
        {
            return new FusedTarget
            {
                Metric = rule.Metric,
                Condition = rule.Condition,
                Value = matched.Value,
                Weight = 1.0,
                Source = MeasuredSource,
                StatedMin = statedMin,
                StatedMax = statedMax,
                MeasuredValue = matched.Value,
                NumDecks = matched.NumDecks,
                EffectiveSampleSize = matched.EffectiveSampleSize,
                Verdict = "insufficient-measured",
                VerdictReason = null,
                SourceClip = rule.SourceClip,
                VideoDateUtc = rule.VideoDateUtc,
                Confidence = ToConfidenceBand(rule.Confidence),
                Conflict = null,
            };
        }

        ConflictCalculationResult conflict = ConflictCalculator.Evaluate(rule, matched.Value, matched.EffectiveSampleSize);

        return new FusedTarget
        {
            Metric = rule.Metric,
            Condition = rule.Condition,
            Value = matched.Value,
            Weight = 1.0,
            Source = MeasuredSource,
            StatedMin = statedMin,
            StatedMax = statedMax,
            MeasuredValue = matched.Value,
            NumDecks = matched.NumDecks,
            EffectiveSampleSize = matched.EffectiveSampleSize,
            Verdict = conflict.Verdict,
            VerdictReason = conflict.VerdictReason,
            SourceClip = rule.SourceClip,
            VideoDateUtc = rule.VideoDateUtc,
            Confidence = ToConfidenceBand(rule.Confidence),
            Conflict = conflict.Conflict,
        };
    }

    private static FusedTarget CreateStatedTarget(StatedRuleCandidate rule, string verdict, string source)
    {
        (double? statedMin, double? statedMax) = GetStatedBand(rule);

        return new FusedTarget
        {
            Metric = rule.Metric,
            Condition = rule.Condition,
            Value = GetRepresentativeStatedValue(rule),
            Weight = 1.0,
            Source = source,
            StatedMin = statedMin,
            StatedMax = statedMax,
            MeasuredValue = null,
            NumDecks = null,
            EffectiveSampleSize = null,
            Verdict = verdict,
            VerdictReason = null,
            SourceClip = rule.SourceClip,
            VideoDateUtc = rule.VideoDateUtc,
            Confidence = ToConfidenceBand(rule.Confidence),
            Conflict = null,
        };
    }

    private static FusedTarget CreateSupersededHistory(StatedRuleCandidate rule)
    {
        (double? statedMin, double? statedMax) = GetStatedBand(rule);

        return new FusedTarget
        {
            Metric = rule.Metric,
            Condition = rule.Condition,
            Value = GetRepresentativeStatedValue(rule),
            Weight = 1.0,
            Source = SupersededSource,
            StatedMin = statedMin,
            StatedMax = statedMax,
            MeasuredValue = null,
            NumDecks = null,
            EffectiveSampleSize = null,
            Verdict = "superseded",
            VerdictReason = null,
            SourceClip = rule.SourceClip,
            VideoDateUtc = rule.VideoDateUtc,
            Confidence = ToConfidenceBand(rule.Confidence),
            Conflict = null,
        };
    }

    private static MeasuredResolution? ResolveMeasured(
        StatedRuleCandidate rule,
        IReadOnlyDictionary<string, MeasuredMetric> measuredByMetric)
    {
        return StatedMetricKeyMapper.GetMapKind(rule.Metric) switch
        {
            StatedMetricMapKind.Direct => TryResolveDirect(rule.Metric, measuredByMetric, out MeasuredResolution direct)
                ? direct
                : null,
            StatedMetricMapKind.Derived when rule.Metric.Equals("land_count", StringComparison.OrdinalIgnoreCase) =>
                TryResolveLandCount(measuredByMetric, out MeasuredResolution derived)
                    ? derived
                    : null,
            _ => null,
        };
    }

    private static bool TryResolveDirect(
        string metric,
        IReadOnlyDictionary<string, MeasuredMetric> measuredByMetric,
        out MeasuredResolution measured)
    {
        measured = default;

        if (!StatedMetricKeyMapper.TryMapToMeasuredKey(metric, out string measuredKey) ||
            !measuredByMetric.TryGetValue(measuredKey, out MeasuredMetric? metricValue))
        {
            return false;
        }

        measured = new MeasuredResolution(
            metricValue.Value,
            metricValue.NumDecks,
            metricValue.Distribution?.EffectiveSampleSize);
        return true;
    }

    private static bool TryResolveLandCount(
        IReadOnlyDictionary<string, MeasuredMetric> measuredByMetric,
        out MeasuredResolution measured)
    {
        measured = default;

        if (!measuredByMetric.TryGetValue("karsten:target_lands", out MeasuredMetric? targetLands) ||
            !measuredByMetric.TryGetValue("karsten:land_delta", out MeasuredMetric? landDelta))
        {
            return false;
        }

        // Why: the phase plan explicitly adopts RESEARCH Assumption A2: approximate land_count as target_lands + land_delta.
        measured = new MeasuredResolution(
            targetLands.Value + landDelta.Value,
            targetLands.NumDecks,
            targetLands.Distribution?.EffectiveSampleSize ?? landDelta.Distribution?.EffectiveSampleSize);
        return true;
    }

    private static (double? Min, double? Max) GetStatedBand(StatedRuleCandidate rule)
    {
        return rule.Comparator switch
        {
            "range" => (rule.ValueMin, rule.ValueMax),
            "lte" => (rule.ValueMin, rule.Value ?? rule.ValueMax),
            "gte" => (rule.Value ?? rule.ValueMin, rule.ValueMax),
            "eq" => (rule.Value, rule.Value),
            _ => (rule.ValueMin ?? rule.Value, rule.ValueMax ?? rule.Value),
        };
    }

    private static double GetRepresentativeStatedValue(StatedRuleCandidate rule)
    {
        if (rule.Value.HasValue)
        {
            return rule.Value.Value;
        }

        if (rule.ValueMin.HasValue && rule.ValueMax.HasValue)
        {
            return (rule.ValueMin.Value + rule.ValueMax.Value) / 2.0;
        }

        if (rule.ValueMin.HasValue)
        {
            return rule.ValueMin.Value;
        }

        if (rule.ValueMax.HasValue)
        {
            return rule.ValueMax.Value;
        }

        return 0.0;
    }

    private static string ToConfidenceBand(double confidence)
    {
        if (confidence >= 0.8)
        {
            return "high";
        }

        if (confidence >= 0.5)
        {
            return "med";
        }

        return "low";
    }

    private static bool IsSupportedComparator(string comparator)
        => comparator is "range" or "lte" or "gte" or "eq";

    private readonly record struct MeasuredResolution(
        double Value,
        int NumDecks,
        double? EffectiveSampleSize);
}
