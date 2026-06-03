using DeckFlow.Web.Services.Harvest;

namespace DeckFlow.Web.Models.Admin;

/// <summary>
/// View model for /Admin/Harvest. Bundles the current schedule snapshot, active run,
/// recent runs, operator banner, and HARV-06 stats payload.
/// </summary>
public sealed record AdminHarvestViewModel
{
    /// <summary>Default number of harvested commander rows shown per page.</summary>
    public const int DefaultDeckPageSize = 25;

    /// <summary>Allowed manual job durations in seconds.</summary>
    public static readonly int[] AllowedDurationSeconds = { 900, 1800, 3600 };
    /// <summary>Allowed automatic harvest intervals in hours.</summary>
    public static readonly int[] AllowedIntervalHours = { 2, 4, 8, 24 };

    /// <summary>Current persisted harvest schedule settings.</summary>
    public required HarvestScheduleSnapshot Schedule { get; init; }

    /// <summary>Currently active harvest run, if one is in progress.</summary>
    public HarvestRunRow? ActiveRun { get; init; }

    /// <summary>Recent harvest runs shown in the admin console.</summary>
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

    /// <summary>Last operator-facing status banner shown by the admin console.</summary>
    public string? LastBanner { get; init; }

    /// <summary>Selectable harvest interval options in hours.</summary>
    public IReadOnlyList<int> IntervalOptions { get; init; } = AllowedIntervalHours;

    /// <summary>Selectable manual job duration options in seconds.</summary>
    public IReadOnlyList<int> DurationOptions { get; init; } = AllowedDurationSeconds;

    /// <summary>Aggregated harvest statistics for the admin dashboard.</summary>
    public HarvestStatsPayload? Stats { get; init; }
}
