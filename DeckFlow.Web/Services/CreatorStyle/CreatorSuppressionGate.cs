using DeckFlow.Core.Content;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>Identity-aware creator-suppression gate for Web consumers.</summary>
public sealed class CreatorSuppressionGate : ICreatorSuppressionGate
{
    private readonly ICreatorIdentityResolver _identityResolver;
    private readonly ICreatorSuppressionStore _suppressionStore;

    /// <summary>Creates an identity-aware creator-suppression gate.</summary>
    /// <param name="identityResolver">Resolves alternate creator representations.</param>
    /// <param name="suppressionStore">Reads suppression records.</param>
    public CreatorSuppressionGate(ICreatorIdentityResolver identityResolver, ICreatorSuppressionStore suppressionStore)
    {
        _identityResolver = identityResolver ?? throw new ArgumentNullException(nameof(identityResolver));
        _suppressionStore = suppressionStore ?? throw new ArgumentNullException(nameof(suppressionStore));
    }

    /// <inheritdoc />
    public async Task<bool> IsSuppressedAsync(string creatorSlug, CancellationToken cancellationToken = default)
    {
        var matcher = new CreatorSuppressionMatcher(await _suppressionStore.ListAsync(cancellationToken).ConfigureAwait(false));
        return await IsSuppressedAsync(creatorSlug, matcher, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetUnsuppressedAsync(IEnumerable<string> creatorSlugs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorSlugs);
        var matcher = new CreatorSuppressionMatcher(await _suppressionStore.ListAsync(cancellationToken).ConfigureAwait(false));
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (string slug in creatorSlugs)
        {
            if (!await IsSuppressedAsync(slug, matcher, cancellationToken).ConfigureAwait(false))
            {
                result.Add(slug);
            }
        }

        return result;
    }

    private async Task<bool> IsSuppressedAsync(string creatorSlug, CreatorSuppressionMatcher matcher, CancellationToken cancellationToken)
    {
        CreatorIdentity? identity = await _identityResolver.ResolveAsync(creatorSlug, cancellationToken).ConfigureAwait(false);
        if (identity is null)
        {
            return matcher.IsSuppressed(creatorSlug);
        }

        return new[] { creatorSlug, identity.CanonicalSlug }
            .Concat(identity.DisplayNames)
            .Concat(identity.FolderSlugs)
            .Any(matcher.IsSuppressed);
    }
}
