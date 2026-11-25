namespace DeckFlow.Web.Services.Harvest;

/// <summary>Summarizes the currently eligible maintenance work.</summary>
public sealed record HarvestMaintenancePreview(int RetentionExcessDecks, int PurgeCandidates, int RefetchCandidates)
{
    /// <summary>Indicates that counts wait for the stored-live-row marker backfill.</summary>
    public bool MarkerBackfillPending { get; init; }

    /// <summary>Counts stored metadata-less decks queued for the harvest to refetch. The count is not persisted with a run.</summary>
    public int QueuedForRefetch { get; init; }
}
