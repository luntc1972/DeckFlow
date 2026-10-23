namespace DeckFlow.Core.Knowledge;

/// <summary>Reports the work completed by a retention pruning run.</summary>
public sealed record DeckPruningResult(int DecksRemoved, int Batches, DeckPruningStopReason StopReason);
