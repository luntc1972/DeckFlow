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

    /// <summary>
    /// Looks up a curated creator by its normalized channel reference
    /// (<see cref="CreatorSourceStore.NormalizeChannelRef"/>). Used at harvest time to resolve the
    /// creator a browsed group came from so it can be linked to the ensured content source.
    /// </summary>
    /// <param name="normalizedChannelRef">The trim+lowercase normalized channel reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching creator, or <see langword="null"/> when none matches.</returns>
    Task<CreatorSource?> GetByNormalizedRefAsync(string normalizedChannelRef, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links a curated creator to the content source it produces and stamps the canonical slug.
    /// Idempotent — a single UPDATE keyed by creator id; safe to re-run on every harvest.
    /// </summary>
    /// <param name="creatorId">Creator row id.</param>
    /// <param name="contentSourceId">The linked <c>content_sources</c> row id.</param>
    /// <param name="canonicalSlug">The content source's persisted slug (authoritative over the
    /// provisional display-derived slug stored at add time).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LinkContentSourceAsync(long creatorId, long contentSourceId, string canonicalSlug, CancellationToken cancellationToken = default);
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

    /// <summary>
    /// The KB slug for this creator. Provisional (display-derived) at add time; overwritten with the
    /// canonical <c>content_sources</c> slug once linked at harvest. <see langword="null"/> on legacy
    /// rows added before this column existed and never re-listed.
    /// </summary>
    public string? SourceSlug { get; init; }

    /// <summary>
    /// The linked <c>content_sources</c> row id, set idempotently at harvest. <see langword="null"/>
    /// until the creator has been harvested at least once (status "pending first harvest").
    /// </summary>
    public long? ContentSourceId { get; init; }
}
