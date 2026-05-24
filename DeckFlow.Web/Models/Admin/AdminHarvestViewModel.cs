using DeckFlow.Web.Services.Harvest;

namespace DeckFlow.Web.Models.Admin;

/// <summary>
/// View model for /Admin/Harvest. Bundles the current schedule snapshot, active run,
/// recent runs, operator banner, and HARV-06 stats payload.
/// </summary>
public sealed record AdminHarvestViewModel
{
    public const int DefaultDeckPageSize = 25;

    public static readonly int[] AllowedDurationSeconds = { 900, 1800, 3600 };
    public static readonly int[] AllowedIntervalHours = { 2, 4, 8, 24 };

    public required HarvestScheduleSnapshot Schedule { get; init; }

    public HarvestRunRow? ActiveRun { get; init; }

    public IReadOnlyList<HarvestRunRow> RecentRuns { get; init; } = Array.Empty<HarvestRunRow>();

    /// <summary>Processed harvested commanders for the current admin grid page.</summary>
    public IReadOnlyList<HarvestedCommanderRow> HarvestedCommanders { get; init; } = Array.Empty<HarvestedCommanderRow>();

    /// <summary>One-based page number currently rendered by the harvested-commanders grid.</summary>
    public int DeckPage { get; init; } = 1;

    /// <summary>Number of harvested commander rows requested per page.</summary>
    public int DeckPageSize { get; init; } = DefaultDeckPageSize;

    /// <summary>Total distinct processed commanders available to the harvested-commanders grid.</summary>
    public int DeckTotalCount { get; init; }

    /// <summary>Total page count for the harvested-commanders grid, with an empty result still yielding page 1.</summary>
    public int DeckTotalPages => (int)Math.Ceiling((double)Math.Max(DeckTotalCount, 1) / Math.Max(DeckPageSize, 1));

    public string? LastBanner { get; init; }

    public IReadOnlyList<int> IntervalOptions { get; init; } = AllowedIntervalHours;

    public IReadOnlyList<int> DurationOptions { get; init; } = AllowedDurationSeconds;

    public HarvestStatsPayload? Stats { get; init; }
}
