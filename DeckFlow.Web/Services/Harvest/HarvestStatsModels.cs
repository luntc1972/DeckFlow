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
    /// <summary>Backlog is within configured thresholds; no flag shown.</summary>
    None,

    /// <summary>Queued deck count exceeds the configured floor.</summary>
    AboveFloor,

    /// <summary>The backlog grew (enqueued exceeded drained) on enough consecutive qualifying runs.</summary>
    Growing,

    /// <summary>Both the floor and the consecutive-growth condition are met.</summary>
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
