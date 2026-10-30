namespace DeckFlow.Core.Knowledge;

/// <summary>Reports the work committed by one stored-live-row marker backfill transaction.</summary>
internal sealed record StoredLiveRowsBackfillBatch(int DecksRechecked, int MarkersChanged, bool Completed);
