using System.ComponentModel.DataAnnotations;

namespace DeckFlow.Web.Configuration;

/// <summary>
/// Configurable thresholds used to derive admin harvest health signals.
/// </summary>
public sealed class HarvestHealthOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "HarvestHealth";

    /// <summary>The number of qualifying runs used to evaluate health signals.</summary>
    public const int RecentRunsWindow = 10;

    /// <summary>Queued deck count that must be exceeded to flag the backlog.</summary>
    public int BacklogFloor { get; set; } = 100;

    /// <summary>Number of consecutive qualifying runs required for a trend warning.</summary>
    [Range(1, RecentRunsWindow)]
    public int BacklogGrowthRunCount { get; set; } = 3;
}
