namespace DeckFlow.Core.Knowledge.CreatorStyleRubric;

/// <summary>
/// Represents the per-metric rubric score emitted for one creator target.
/// </summary>
public sealed record RubricMetricScore
{
    /// <summary>
    /// Gets the canonical metric key used for this score row.
    /// </summary>
    public required string Metric { get; init; }

    /// <summary>
    /// Gets the creator target value.
    /// </summary>
    public required double TargetValue { get; init; }

    /// <summary>
    /// Gets the submitted deck value when a comparable measured metric exists.
    /// </summary>
    public double? SubmittedValue { get; init; }

    /// <summary>
    /// Gets the submitted-minus-target delta when a comparable measured metric exists.
    /// </summary>
    public double? Delta { get; init; }

    /// <summary>
    /// Gets the weight carried forward from the fused creator target.
    /// </summary>
    public required double Weight { get; init; }

    /// <summary>
    /// Gets the score verdict: <c>on-target</c> for an exact match, <c>under</c> when the submitted value is below target,
    /// <c>over</c> when it is above target, or <c>insufficient-measured</c> when no comparable measured value exists.
    /// </summary>
    public required string Verdict { get; init; }

    /// <summary>
    /// Gets the fused confidence band copied verbatim from the creator target.
    /// </summary>
    public string? Confidence { get; init; }
}

/// <summary>
/// Represents the deterministic rubric-scoring result for one creator.
/// </summary>
public sealed record RubricScoreResult
{
    /// <summary>
    /// Gets the creator slug associated with this rubric result.
    /// </summary>
    public required string CreatorSlug { get; init; }

    /// <summary>
    /// Gets the ordered metric scores.
    /// </summary>
    public required IReadOnlyList<RubricMetricScore> MetricScores { get; init; }
}
