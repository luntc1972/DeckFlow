namespace DeckFlow.Core.Content;

/// <summary>One creator-scoped deletion operation.</summary>
/// <param name="StoreName">Stable store identifier.</param>
/// <param name="DeleteAsync">Deletion operation.</param>
internal sealed record CreatorPurgeStep(
    string StoreName,
    Func<CreatorIdentity, CancellationToken, Task<int>> DeleteAsync);
