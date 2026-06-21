namespace DeckFlow.Core.Content;

/// <summary>
/// Persists the operator's curated list of creators/channels to harvest from (SRC-01). Local Studio
/// convenience list; lives in the Content KB database beside the blocked/skipped lists.
/// </summary>
public interface ICreatorSourceStore
{
    /// <summary>
    /// Ensures the creator-sources schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a curated creator. Idempotent on the normalized channel reference — adding the same
    /// channel (ignoring surrounding whitespace and case) twice yields a single row.
    /// </summary>
    /// <param name="displayName">Operator-facing creator name.</param>
    /// <param name="channelRef">Channel URL, handle, or id the operator browses by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(string displayName, string channelRef, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a curated creator by surrogate id.
    /// </summary>
    /// <param name="id">Creator row id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when a row was removed; otherwise <see langword="false"/>.</returns>
    Task<bool> RemoveAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists curated creators in a deterministic order (display name, then id).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Curated creator rows.</returns>
    Task<IReadOnlyList<CreatorSource>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One curated creator/channel row.
/// </summary>
public sealed record CreatorSource
{
    /// <summary>Surrogate row id.</summary>
    public required long Id { get; init; }

    /// <summary>Operator-facing creator name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Channel URL, handle, or id used to browse this creator.</summary>
    public required string ChannelRef { get; init; }

    /// <summary>UTC timestamp when the creator row was added.</summary>
    public required DateTimeOffset AddedUtc { get; init; }
}
