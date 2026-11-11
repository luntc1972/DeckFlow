namespace DeckFlow.Core.Knowledge;

/// <summary>Cumulative decks affected and batches completed by a pruning operation.</summary>
public sealed record DeckPruningProgress(int DecksAffected, int Batches);
