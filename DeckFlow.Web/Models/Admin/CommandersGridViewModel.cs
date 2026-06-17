using DeckFlow.Web.Services.Harvest;

namespace DeckFlow.Web.Models.Admin;

/// <summary>
/// View model for the harvested-commanders partial grid.
/// </summary>
public sealed record CommandersGridViewModel
{
    /// <summary>Processed harvested commanders for the current admin grid page.</summary>
    public IReadOnlyList<HarvestedCommanderRow> HarvestedCommanders { get; init; } = Array.Empty<HarvestedCommanderRow>();

    /// <summary>One-based page number currently rendered by the harvested-commanders grid.</summary>
    public int DeckPage { get; init; } = 1;

    /// <summary>Number of harvested commander rows requested per page.</summary>
    public int DeckPageSize { get; init; } = AdminHarvestViewModel.DefaultDeckPageSize;

    /// <summary>Total distinct processed commanders available to the harvested-commanders grid.</summary>
    public int DeckTotalCount { get; init; }

    /// <summary>Total page count for the harvested-commanders grid, with an empty result still yielding page 1.</summary>
    public int DeckTotalPages => (int)Math.Ceiling((double)Math.Max(DeckTotalCount, 1) / Math.Max(DeckPageSize, 1));
}
