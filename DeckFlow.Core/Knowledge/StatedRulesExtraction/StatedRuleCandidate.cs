namespace DeckFlow.Core.Knowledge.StatedRulesExtraction;

/// <summary>
/// Band-capable stated rule candidate extracted from creator transcript guidance.
/// </summary>
public sealed record StatedRuleCandidate
{
    /// <summary>Rule category used to group related creator guidance.</summary>
    public required string Category { get; init; }

    /// <summary>Controlled-vocabulary metric targeted by the stated rule.</summary>
    public required string Metric { get; init; }

    /// <summary>Single comparator value used by <c>gte</c>, <c>lte</c>, and <c>eq</c> rules.</summary>
    public double? Value { get; init; }

    /// <summary>Inclusive lower bound used by <c>range</c> rules.</summary>
    public double? ValueMin { get; init; }

    /// <summary>Inclusive upper bound used by <c>range</c> rules.</summary>
    public double? ValueMax { get; init; }

    /// <summary>Comparator describing how the extracted value or band should be interpreted.</summary>
    public required string Comparator { get; init; }

    /// <summary>Optional conditional scope such as archetype, curve, color, or bracket.</summary>
    public string? Condition { get; init; }

    /// <summary>Optional clip timestamp in seconds when the advice appears in the source video.</summary>
    public int? ClipTimestampSeconds { get; init; }

    /// <summary>Transcript excerpt or paraphrase supporting the extracted rule.</summary>
    public required string SourceClip { get; init; }

    /// <summary>Confidence score assigned to the extracted rule on a 0.0 to 1.0 scale.</summary>
    public required double Confidence { get; init; }

    /// <summary>Optional raw or canonical card name referenced by the rule when a specific card is mentioned.</summary>
    public string? CardReference { get; init; }

    /// <summary>Optional card-grounding status for rules that mention a specific card name.</summary>
    public bool? CardGrounded { get; init; }

    /// <summary>UTC publish date of the source video carrying this rule for recency and provenance.</summary>
    public required DateTimeOffset VideoDateUtc { get; init; }
}
