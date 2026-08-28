namespace DeckFlow.Web.Services.Scryfall;

/// <summary>Snapshot of cache activity since cache construction, plus the current flag state.</summary>
public sealed record ScryfallCollectionCacheStatistics(
    bool Enabled,
    long Hits,
    long Misses,
    long Stores,
    long Bypasses);
