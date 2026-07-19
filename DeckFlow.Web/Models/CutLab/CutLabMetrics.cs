using DeckFlow.Core.Manabase;

namespace DeckFlow.Web.Models.CutLab;

/// <summary>Display unit for one Cut Lab metric value.</summary>
public enum CutLabMetricUnit
{
    /// <summary>The metric is expressed in percentage points.</summary>
    Percent,

    /// <summary>The metric is expressed in cards.</summary>
    Cards,
}

/// <summary>Visual delta direction for one Cut Lab metric.</summary>
public enum CutLabMetricDirection
{
    /// <summary>The metric increased after the proposal.</summary>
    Up,

    /// <summary>The metric decreased after the proposal.</summary>
    Down,

    /// <summary>The metric did not move meaningfully.</summary>
    None,
}

/// <summary>The seven shared metric families every Cut Lab simulation surface consumes.</summary>
public enum CutLabMetricFamily
{
    /// <summary>Commander castability by the relevant early turn.</summary>
    CommanderOnTime,

    /// <summary>Opening-hand keepability.</summary>
    KeepableHand,

    /// <summary>Mana and color reliability.</summary>
    ManaColorReliability,

    /// <summary>Early interaction availability.</summary>
    EarlyInteraction,

    /// <summary>Opening-hand plan presence.</summary>
    PlanPresence,

    /// <summary>Fixed cEDH category-by-turn checkpoints for Phase 103.</summary>
    CategoryByTurn,

    /// <summary>Flood, screw, and curve risk lines.</summary>
    FloodScrewCurveRisk,
}

/// <summary>Granular metric identity for one rendered Cut Lab line item.</summary>
public enum CutLabMetricKind
{
    /// <summary>Commander-on-time probability.</summary>
    CommanderOnTime,

    /// <summary>Keepable-hand percentage.</summary>
    KeepableHand,

    /// <summary>Composite mana and color reliability read.</summary>
    ManaColorReliability,

    /// <summary>Early interaction availability.</summary>
    EarlyInteraction,

    /// <summary>Plan-presence percentage.</summary>
    PlanPresence,

    /// <summary>Commander-by-turn fixed default for Phase 103.</summary>
    CommanderByTurn,

    /// <summary>Engine-by-turn fixed default for Phase 103.</summary>
    EngineByTurn,

    /// <summary>Representative-line-by-turn fixed default for Phase 103.</summary>
    RepresentativeLineByTurn,

    /// <summary>Flood risk line.</summary>
    Flood,

    /// <summary>Screw risk line.</summary>
    Screw,

    /// <summary>Curve risk line.</summary>
    Curve,
}

/// <summary>One numeric Cut Lab metric value captured in a baseline or working snapshot.</summary>
public sealed record CutLabMetricValue
{
    /// <summary>The granular metric identity.</summary>
    public CutLabMetricKind Kind { get; init; }

    /// <summary>The parent family this metric belongs to.</summary>
    public CutLabMetricFamily Family { get; init; }

    /// <summary>User-facing display label for this metric line.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Raw numeric value for this metric line.</summary>
    public double Value { get; init; }

    /// <summary>Display unit for <see cref="Value"/>.</summary>
    public CutLabMetricUnit Unit { get; init; }
}

/// <summary>Compact numeric baseline stored in Cut Lab state without carrying simulation objects.</summary>
public sealed record CutLabMetricSnapshot
{
    /// <summary>All numeric metric lines captured for the snapshot.</summary>
    public IReadOnlyList<CutLabMetricValue> Metrics { get; init; } = [];
}

/// <summary>Before-and-after numeric change for one Cut Lab metric line.</summary>
public sealed record CutLabMetricDelta
{
    /// <summary>The granular metric identity.</summary>
    public CutLabMetricKind Kind { get; init; }

    /// <summary>The parent family this metric belongs to.</summary>
    public CutLabMetricFamily Family { get; init; }

    /// <summary>User-facing display label for this metric line.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Numeric value before the proposed change.</summary>
    public double Before { get; init; }

    /// <summary>Numeric value after the proposed change.</summary>
    public double After { get; init; }

    /// <summary>Signed numeric delta between <see cref="After"/> and <see cref="Before"/>.</summary>
    public double Delta { get; init; }

    /// <summary>Display direction for the delta.</summary>
    public CutLabMetricDirection Direction { get; init; }

    /// <summary>True when the delta exceeds the configured noise floor.</summary>
    public bool IsMeaningful { get; init; }
}

/// <summary>All metric deltas surfaced for one proposed card cut.</summary>
public sealed record CutLabProposalDeltas
{
    /// <summary>The proposed card name.</summary>
    public string CardName { get; init; } = string.Empty;

    /// <summary>All granular deltas computed for the proposal.</summary>
    public IReadOnlyList<CutLabMetricDelta> Deltas { get; init; } = [];

    /// <summary>How many metric families changed meaningfully.</summary>
    public int ChangedFamilyCount { get; init; }
}

/// <summary>Named display thresholds that suppress Monte Carlo noise in delta rendering.</summary>
public static class CutLabNoiseFloor
{
    // Why: UI-SPEC A3 fixes the default percent-point noise floor at display time; Task 3 may tune
    // the number later, but the show/hide behavior stays the same.
    /// <summary>Default display threshold for percentage-point deltas.</summary>
    public const double PercentPoints = 1.5;

    // Why: UI-SPEC A3 fixes the default card-count noise floor at display time; Task 3 may tune the
    // number later, but the show/hide behavior stays the same.
    /// <summary>Default display threshold for card-count deltas.</summary>
    public const int Cards = 1;
}

/// <summary>Fixed cEDH category-by-turn defaults reused across all Phase 103 metric projections.</summary>
public static class CutLabCategoryByTurnDefaults
{
    /// <summary>Commander or explosive-start checkpoint for Phase 103.</summary>
    public const int CommanderByTurn = CedhMulliganCalibration.TurnCapExplosive;

    /// <summary>Early-engine checkpoint for Phase 103.</summary>
    public const int EngineByTurn = CedhMulliganCalibration.TurnCapEngine;

    /// <summary>Representative-line checkpoint for Phase 103.</summary>
    public const int RepresentativeLineByTurn = CedhMulliganCalibration.RepresentativeLineTurnCap;
}
