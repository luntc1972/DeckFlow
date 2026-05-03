using DeckFlow.Web.Services.Harvest;

namespace DeckFlow.Web.Models.Admin;

/// <summary>
/// View model for /Admin/Harvest. Bundles the current schedule snapshot, active run,
/// recent runs, operator banner, and HARV-06 stats payload.
/// </summary>
public sealed record AdminHarvestViewModel
{
    public static readonly int[] AllowedDurationSeconds = { 900, 1800, 3600 };
    public static readonly int[] AllowedIntervalHours = { 2, 4, 8, 24 };

    public required HarvestScheduleSnapshot Schedule { get; init; }

    public HarvestRunRow? ActiveRun { get; init; }

    public IReadOnlyList<HarvestRunRow> RecentRuns { get; init; } = Array.Empty<HarvestRunRow>();

    public string? LastBanner { get; init; }

    public IReadOnlyList<int> IntervalOptions { get; init; } = AllowedIntervalHours;

    public IReadOnlyList<int> DurationOptions { get; init; } = AllowedDurationSeconds;

    public HarvestStatsPayload? Stats { get; init; }
}
