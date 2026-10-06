namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Reports queue rows inserted and terminal rows requeued from an Archidekt listing.
/// </summary>
/// <param name="NewIds">Number of unseen deck IDs inserted as pending rows.</param>
/// <param name="RefreshesRequeued">Number of terminal deck rows requeued as refreshes.</param>
public sealed record ListingUpsertResult(int NewIds, int RefreshesRequeued);
