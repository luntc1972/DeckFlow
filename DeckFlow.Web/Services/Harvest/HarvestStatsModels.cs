using System;
using System.Collections.Generic;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Top-N commander row from deck_queue.commander_name (D-15).</summary>
public sealed record TopCommanderRow(string CommanderName, int DeckCount);

/// <summary>Processed deck row displayed in the admin harvested-decks grid.</summary>
public sealed record HarvestedDeckRow(string DeckId, string? CommanderName, string InsertedUtc, string? LastCheckedUtc);

/// <summary>
/// Full HARV-06 stats payload (D-16). Cached for 60 seconds in IMemoryCache and
/// explicitly invalidated on harvest_runs writes (D-13).
/// </summary>
public sealed record HarvestStatsPayload(
    int TotalDecks,
    int TotalDecks30d,
    int TotalObservations,
    IReadOnlyList<TopCommanderRow> TopCommanders,
    IReadOnlyList<HarvestRunRow> RecentRuns,
    long? PostgresStorageBytes,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? NextScheduledUtc);
