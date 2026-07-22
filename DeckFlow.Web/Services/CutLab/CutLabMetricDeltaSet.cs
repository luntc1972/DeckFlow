using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Shared metric-delta fold output reused across Cut Lab simulation surfaces.</summary>
public sealed record CutLabMetricDeltaSet
{
    /// <summary>All granular deltas that could be computed for the before/after metrics.</summary>
    public IReadOnlyList<CutLabMetricDelta> Deltas { get; init; } = [];

    /// <summary>How many metric families changed meaningfully.</summary>
    public int ChangedFamilyCount { get; init; }

    /// <summary>Builds the shared delta fold from before and after metric snapshots.</summary>
    public static CutLabMetricDeltaSet From(
        IReadOnlyList<CutLabMetricValue> before,
        IReadOnlyList<CutLabMetricValue> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        IReadOnlyDictionary<CutLabMetricKind, CutLabMetricValue> afterMetrics = after
            .ToDictionary(metric => metric.Kind);
        IReadOnlyList<CutLabMetricDelta> deltas = before
            .Select(metric => afterMetrics.TryGetValue(metric.Kind, out CutLabMetricValue? afterMetric)
                ? CutLabMetricDelta.Between(metric, afterMetric)
                : null)
            .Where(delta => delta is not null)
            .Cast<CutLabMetricDelta>()
            .ToArray();

        return new CutLabMetricDeltaSet
        {
            Deltas = deltas,
            ChangedFamilyCount = deltas.Where(delta => delta.IsMeaningful).Select(delta => delta.Family).Distinct().Count(),
        };
    }
}
