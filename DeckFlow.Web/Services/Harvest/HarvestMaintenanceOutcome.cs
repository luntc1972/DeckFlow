using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Summarizes an operator maintenance operation. Decks affected are removed for drain or purge and marked for refetch; stop reason is null for refetch unless the marker backfill is pending. For Backfill, DecksAffected counts decks rechecked.</summary>
public sealed record HarvestMaintenanceOutcome(HarvestMaintenanceOperation Operation, int DecksAffected, int Batches, DeckPruningStopReason? StopReason);
