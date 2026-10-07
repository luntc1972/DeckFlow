using DeckFlow.Core.Content;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>Identity-aware creator-suppression gate for Web consumers.</summary>
public sealed class CreatorSuppressionGate : ICreatorSuppressionGate
{
    private readonly CreatorSuppressionFilter _filter;

    /// <summary>Creates an identity-aware creator-suppression gate.</summary>
    /// <param name="identityResolver">Resolves alternate creator representations.</param>
    /// <param name="suppressionStore">Reads suppression records.</param>
    public CreatorSuppressionGate(ICreatorIdentityResolver identityResolver, ICreatorSuppressionStore suppressionStore)
    {
        ArgumentNullException.ThrowIfNull(identityResolver);
        ArgumentNullException.ThrowIfNull(suppressionStore);
        _filter = new CreatorSuppressionFilter(suppressionStore, new Lazy<ICreatorIdentityResolver>(() => identityResolver));
    }

    /// <inheritdoc />
    public Task<bool> IsSuppressedAsync(string creatorSlug, CancellationToken cancellationToken = default)
        => _filter.IsSuppressedAsync(creatorSlug, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetUnsuppressedAsync(IEnumerable<string> creatorSlugs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorSlugs);
        var slugs = creatorSlugs.ToList();
        var unsuppressed = await _filter.GetUnsuppressedAsync(slugs, cancellationToken).ConfigureAwait(false);
        return unsuppressed.ToHashSet(StringComparer.Ordinal);
    }
}
