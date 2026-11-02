namespace DeckFlow.Core.Knowledge;

/// <summary>Describes why a retention pruning run ended.</summary>
public enum DeckPruningStopReason
{
    /// <summary>All eligible removals completed.</summary>
    Completed,
    /// <summary>The free-disk probe returned no usable measurement.</summary>
    DiskProbeUnavailable,
    /// <summary>The configured free-disk floor prevented another batch.</summary>
    DiskFloor,
    /// <summary>The requested maximum number of removals was reached.</summary>
    MaxDecksReached,
    /// <summary>The stored-live-rows marker backfill has not completed, so nothing was read or removed.</summary>
    MarkerBackfillPending,
}
