using DeckFlow.Web.Services.Harvest;

namespace DeckFlow.Web.Models.Admin;

/// <summary>
/// View model for /Admin/Harvest. Bundles the current schedule snapshot, active run,
/// recent runs, operator banner, and HARV-06 stats payload.
/// </summary>
public sealed record AdminHarvestViewModel
{
    public const int DefaultDeckPageSize = 50;

    public static readonly int[] AllowedDurationSeconds = { 900, 1800, 3600 };
    public static readonly int[] AllowedIntervalHours = { 2, 4, 8, 24 };

    public required HarvestScheduleSnapshot Schedule { get; init; }

    public HarvestRunRow? ActiveRun { get; init; }

    public IReadOnlyList<HarvestRunRow> RecentRuns { get; init; } = Array.Empty<HarvestRunRow>();

    /// <summary>Processed harvested decks for the current admin grid page.</summary>
    public IReadOnlyList<HarvestedDeckRow> HarvestedDecks { get; init; } = Array.Empty<HarvestedDeckRow>();

    /// <summary>One-based page number currently rendered by the harvested-decks grid.</summary>
    public int DeckPage { get; init; } = 1;

    /// <summary>Number of harvested decks requested per page.</summary>
    public int DeckPageSize { get; init; } = DefaultDeckPageSize;

    /// <summary>Total processed decks available to the harvested-decks grid.</summary>
    public int DeckTotalCount { get; init; }

    /// <summary>Total page count for the harvested-decks grid, with an empty result still yielding page 1.</summary>
    public int DeckTotalPages => (int)Math.Ceiling((double)Math.Max(DeckTotalCount, 1) / Math.Max(DeckPageSize, 1));

    public string? LastBanner { get; init; }

    public IReadOnlyList<int> IntervalOptions { get; init; } = AllowedIntervalHours;

    public IReadOnlyList<int> DurationOptions { get; init; } = AllowedDurationSeconds;

    public HarvestStatsPayload? Stats { get; init; }
}
