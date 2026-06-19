namespace DeckFlow.Core.Content;

/// <summary>
/// Derives the authoritative publish state for a content entry from persisted publish and local-index signals.
/// </summary>
public sealed class PublishStateDeriver
{
    /// <summary>
    /// Derives the publish state using the locked precedence order.
    /// </summary>
    /// <param name="pushedToProdUtc">Timestamp of the last production push, if any.</param>
    /// <param name="isVisible">Whether the entry is currently visible in production.</param>
    /// <param name="localIndexedUtc">Timestamp of the latest local distill/index state.</param>
    /// <returns>The derived <see cref="PublishState"/>.</returns>
    public PublishState Derive(DateTimeOffset? pushedToProdUtc, bool isVisible, DateTimeOffset localIndexedUtc)
    {
        if (!pushedToProdUtc.HasValue)
        {
            return PublishState.NeverPublished;
        }

        if (!isVisible)
        {
            return PublishState.PushedHidden;
        }

        var pushedUtc = pushedToProdUtc.Value.ToUniversalTime().UtcDateTime;
        var localUtc = localIndexedUtc.ToUniversalTime().UtcDateTime;

        if (localUtc > pushedUtc)
        {
            return PublishState.LocalNewer;
        }

        return PublishState.Published;
    }
}
