namespace DeckFlow.Core.Content;

/// <summary>
/// Lightweight creator style-profile summary used when listing available creators.
/// </summary>
public sealed record CreatorStyleProfileSummary
{
    /// <summary>
    /// Gets the creator slug.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Gets the platform identifier associated with the creator.
    /// </summary>
    public required string Platform { get; init; }

    /// <summary>
    /// Gets the minimum deck count used for the profile.
    /// </summary>
    public required int MinDecks { get; init; }

    /// <summary>
    /// Gets a value indicating whether the profile sample is insufficient.
    /// </summary>
    public bool InsufficientSample { get; init; }

    /// <summary>
    /// Gets the UTC timestamp for when the profile was last updated.
    /// </summary>
    public required DateTimeOffset UpdatedUtc { get; init; }
}
