using System;
using System.Collections.Generic;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Processed commander aggregate row displayed in the admin harvested-commanders grid.</summary>
public sealed record HarvestedCommanderRow(string CommanderName, int DeckCount, string? LastProcessedUtc);

/// <summary>
/// Full HARV-06 stats payload (D-16). Cached for 60 seconds in IMemoryCache and
/// explicitly invalidated on harvest_runs writes (D-13).
/// </summary>
public sealed record HarvestStatsPayload(
    int TotalDecks,
    int TotalDecks30d,
    int QueuedDeckCount,
    int DistinctCommanderCount,
    int TotalObservations,
    IReadOnlyList<HarvestRunRow> RecentRuns,
    long? DatabaseSizeBytes,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? NextScheduledUtc,
    HarvestHealthSignals Health);

/// <summary>Reasons that the harvest backlog requires operator attention.</summary>
public enum HarvestBacklogReason
{
    None,
    AboveFloor,
    Growing,
    AboveFloorAndGrowing
}

/// <summary>Derived harvest-health values displayed by the admin overview.</summary>
public sealed record HarvestHealthSignals(
    bool BacklogFlagged,
    HarvestBacklogReason BacklogReason,
    int BacklogFloor,
    int BacklogGrowthRunCount,
    int ZeroDiscoveryStreak,
    bool ZeroDiscoveryStreakCapped);
