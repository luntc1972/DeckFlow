using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Summarizes an operator maintenance operation. Decks affected are removed for drain or purge and marked for refetch; stop reason is null for refetch.</summary>
public sealed record HarvestMaintenanceOutcome(HarvestMaintenanceOperation Operation, int DecksAffected, int Batches, DeckPruningStopReason? StopReason);
