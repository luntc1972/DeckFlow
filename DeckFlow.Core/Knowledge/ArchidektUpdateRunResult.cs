namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Reports refresh-only Archidekt update work; counters map to harvest_runs pages_polled,
/// refreshes_requeued, refreshes_drained, and new_ids_seen.
/// </summary>
/// <remarks>
/// Why: D-05 keeps refresh traffic out of decks_enqueued by carrying no enqueued or drained-deck member.
/// </remarks>
/// <param name="PagesPolled">Number of listing pages polled.</param>
/// <param name="RefreshesRequeued">Number of known decks requeued from listing changes.</param>
/// <param name="RefreshesDrained">Number of refresh rows drained.</param>
/// <param name="NewIdsSeen">Number of previously unseen listing IDs inserted for bulk.</param>
/// <param name="DecksSkipped">Number of refresh decks skipped after import failures.</param>
/// <param name="Duration">Elapsed update-run duration.</param>
public sealed record ArchidektUpdateRunResult(int PagesPolled, int RefreshesRequeued, int RefreshesDrained, int NewIdsSeen, int DecksSkipped, TimeSpan Duration);
