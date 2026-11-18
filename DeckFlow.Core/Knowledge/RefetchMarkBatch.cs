namespace DeckFlow.Core.Knowledge;

/// <summary>Reports the work committed by one refetch mark transaction.</summary>
internal sealed record RefetchMarkBatch(int DecksSelected, int DecksMarked, bool Completed);
