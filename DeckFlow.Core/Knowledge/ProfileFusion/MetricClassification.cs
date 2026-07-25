namespace DeckFlow.Core.Knowledge.ProfileFusion;

/// <summary>
/// Describes whether a stated metric resolves against measured data or remains stated philosophy.
/// </summary>
public enum MetricKind
{
    /// <summary>The metric is observable in measured deck data.</summary>
    Observable,

    /// <summary>The metric remains a stated-only philosophy or preference.</summary>
    Philosophy,
}

/// <summary>
/// Classifies stated metrics into observable or philosophy partitions.
/// </summary>
public static class MetricClassification
{
    /// <summary>
    /// Classifies a stated metric using the same mapping table that drives measured joins.
    /// </summary>
    /// <param name="statedMetric">The stated metric key to classify.</param>
    /// <returns>The deterministic partition for the supplied stated metric key.</returns>
    public static MetricKind Classify(string statedMetric)
    {
        ArgumentNullException.ThrowIfNull(statedMetric);

        return StatedMetricKeyMapper.GetMapKind(statedMetric) == StatedMetricMapKind.StatedOnly
            ? MetricKind.Philosophy
            : MetricKind.Observable;
    }
}
