namespace DeckFlow.Core.Manabase;

/// <summary>Where a weighted baseline metric came from.</summary>
public enum ManabaseBaselineSource
{
    /// <summary>The commander's own cell (sample was solid).</summary>
    Commander,

    /// <summary>A linear blend of the commander cell and the global bracket baseline.</summary>
    Blended,

    /// <summary>The global-per-bracket baseline (commander cell thin or missing).</summary>
    Global,

    /// <summary>No data available for this metric.</summary>
    None,
}

/// <summary>One weighted baseline metric (lands, ramp, or draw) and where its value came from.</summary>
public sealed record ManabaseBaselineMetric(double? Value, ManabaseBaselineSource Source);

/// <summary>Confidence-weighted per-bracket baseline for a commander's mana base.</summary>
public sealed record ManabaseBaselineResult(
    ManabaseBaselineMetric Lands,
    ManabaseBaselineMetric Ramp,
    ManabaseBaselineMetric Draw,
    double? TotalSources,
    int CommanderDeckCount);

/// <summary>
/// Turns a commander's per-bracket average lands/ramp/draw (with its sample size) plus a
/// global-per-bracket fallback into a confidence-weighted baseline. Pure: no I/O.
/// </summary>
public static class ManabaseBaselineWeighting
{
    /// <summary>Below this deck count the commander cell is ignored in favor of the global baseline.</summary>
    public const int LowDeckThreshold = 100;

    /// <summary>At or above this deck count the commander cell is trusted fully.</summary>
    public const int HighDeckThreshold = 400;

    /// <summary>
    /// Compute the weighted baseline for all three metrics. A negative <paramref name="commanderDeckCount"/>
    /// is treated as a thin sample (falls back to the global baseline). Metric averages are assumed
    /// non-negative (guaranteed by the upstream corpus aggregation) and are not validated here.
    /// </summary>
    public static ManabaseBaselineResult Compute(
        double? commanderLands, double? commanderRamp, double? commanderDraw, int commanderDeckCount,
        double? globalLands, double? globalRamp, double? globalDraw)
    {
        var lands = new ManabaseBaselineMetric(null, ManabaseBaselineSource.None);
        var ramp = new ManabaseBaselineMetric(null, ManabaseBaselineSource.None);
        var draw = new ManabaseBaselineMetric(null, ManabaseBaselineSource.None);
        return new ManabaseBaselineResult(lands, ramp, draw, null, commanderDeckCount);
    }
}
