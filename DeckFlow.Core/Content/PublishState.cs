namespace DeckFlow.Core.Content;

/// <summary>
/// Publish-state badge for a content entry as shown in operator/admin surfaces.
/// </summary>
public enum PublishState
{
    /// <summary>The entry has never been pushed to production.</summary>
    NeverPublished,

    /// <summary>The entry was pushed at least once but is currently hidden from production display.</summary>
    PushedHidden,

    /// <summary>The entry is visible and the local content is not newer than the latest production push.</summary>
    Published,

    /// <summary>The entry is visible and the local content is newer than the latest production push.</summary>
    LocalNewer,
}

/// <summary>
/// Provides the locked UI display strings for <see cref="PublishState"/>.
/// </summary>
public static class PublishStateExtensions
{
    /// <summary>
    /// Converts a <see cref="PublishState"/> to the exact shared display vocabulary used by UI callers.
    /// </summary>
    /// <param name="state">The publish state to display.</param>
    /// <returns>The locked display string for <paramref name="state"/>.</returns>
    public static string ToDisplayString(this PublishState state)
        => state switch
        {
            PublishState.NeverPublished => "Never published",
            PublishState.PushedHidden => "Pushed-hidden",
            PublishState.Published => "Published",
            PublishState.LocalNewer => "Local-newer",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown publish state."),
        };
}
