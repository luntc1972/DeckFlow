using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Summarizes an operator maintenance operation.</summary>
public sealed record HarvestMaintenanceOutcome(int DecksAffected, int Batches, DeckPruningStopReason StopReason);
